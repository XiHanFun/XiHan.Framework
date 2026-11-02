// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 定义 JSON 里没写的成员取模型的初值，与直接构造模型一致（ADR-0107）。
/// </summary>
public sealed class WorkflowDefinitionInitialValuesTests
{
    [Fact]
    public void EveryMember_LeftOut_ReadsAsTheModelsInitialValue()
    {
        // 只写必填成员读出来的，与直接构造（必填成员相同）的逐个成员比：以后加了带初值的 init 成员而初值在读入时丢失，这里就失败
        AssertSameMembers(WorkflowJsonContext.Default.WorkflowDefinition, "{\"id\":\"d\",\"name\":\"ok\"}", new WorkflowDefinition { Id = "d", Name = new WorkflowName("ok") });
        AssertSameMembers(WorkflowJsonContext.Default.WorkflowNode, "{\"id\":\"a\",\"activityType\":\"Log\"}", new WorkflowNode { Id = "a", ActivityType = ActivityTypes.Log });
        AssertSameMembers(WorkflowJsonContext.Default.WorkflowTransition, "{\"id\":\"t\",\"sourceNodeId\":\"a\",\"targetNodeId\":\"b\"}", new WorkflowTransition { Id = "t", SourceNodeId = "a", TargetNodeId = "b" });
        AssertSameMembers(WorkflowJsonContext.Default.WorkflowVariableDefinition, "{\"name\":\"x\"}", new WorkflowVariableDefinition { Name = "x" });
        AssertSameMembers(WorkflowJsonContext.Default.WorkflowRetryPolicy, "{}", new WorkflowRetryPolicy());

        // 实例、节点实例与书签的成员是可写的（set），一直保留初值；一并核对
        AssertSameMembers(WorkflowJsonContext.Default.WorkflowInstance, "{\"id\":\"i\",\"definitionId\":\"d\",\"definitionName\":\"ok\"}", new WorkflowInstance { Id = "i", DefinitionId = "d", DefinitionName = new WorkflowName("ok") });
    }

    [Fact]
    public void LeftOut_Written_AndWrittenAsTheTypeDefault_FollowTheRules()
    {
        // 不写：初值
        var minimal = Read("{\"id\":\"d\",\"name\":\"ok\",\"nodes\":[{\"id\":\"s\",\"activityType\":\"Start\",\"retryPolicy\":{}}]}");
        var policy = minimal.Nodes[0].RetryPolicy!;
        Assert.Equal((1, 1, TimeSpan.FromSeconds(10), 2.0), (minimal.Version, policy.MaxAttempts, policy.FirstDelay, policy.BackoffFactor));

        // 写了：写的值
        var written = Read("{\"id\":\"d\",\"name\":\"ok\",\"version\":3,\"nodes\":[{\"id\":\"s\",\"activityType\":\"Start\",\"retryPolicy\":{\"maxAttempts\":5,\"firstDelay\":\"00:00:30\",\"backoffFactor\":1.5}}]}");
        policy = written.Nodes[0].RetryPolicy!;
        Assert.Equal((3, 5, TimeSpan.FromSeconds(30), 1.5), (written.Version, policy.MaxAttempts, policy.FirstDelay, policy.BackoffFactor));

        // 写成类型的默认值：合法的保留（首次延迟 0），不合法的报问题（版本 0、最多尝试 0 次、退避因子 0）
        var zeroDelay = Read("{\"id\":\"d\",\"name\":\"ok\",\"nodes\":[{\"id\":\"s\",\"activityType\":\"Start\",\"retryPolicy\":{\"firstDelay\":\"00:00:00\"}}]}");
        Assert.Equal(TimeSpan.Zero, zeroDelay.Nodes[0].RetryPolicy!.FirstDelay);
        var zeros = WorkflowJson.DeserializeDefinition("{\"id\":\"d\",\"name\":\"ok\",\"version\":0,\"nodes\":[{\"id\":\"s\",\"activityType\":\"Start\",\"retryPolicy\":{\"maxAttempts\":0,\"backoffFactor\":0}}]}").Error!;
        Assert.Equal(
            [("$.nodes[0].retryPolicy.backoffFactor", WorkflowMessages.DefinitionRetryBackoffInvalid), ("$.nodes[0].retryPolicy.maxAttempts", WorkflowMessages.DefinitionRetryAttemptsInvalid), ("$.version", WorkflowMessages.DefinitionVersionInvalid)],
            Assert.IsAssignableFrom<IReadOnlyList<SchemaViolation>>(zeros.Details!["errors"]).Select(static issue => (issue.Path, issue.MessageKey!)).OrderBy(static issue => issue.Path, StringComparer.Ordinal));

        // 写成 null：与不写相同，取初值
        var nulls = Read("{\"id\":\"d\",\"name\":\"ok\",\"version\":null,\"nodes\":[{\"id\":\"s\",\"activityType\":\"Start\",\"retryPolicy\":{\"maxAttempts\":null,\"firstDelay\":null,\"backoffFactor\":null}}]}");
        policy = nulls.Nodes[0].RetryPolicy!;
        Assert.Equal((1, 1, TimeSpan.FromSeconds(10), 2.0), (nulls.Version, policy.MaxAttempts, policy.FirstDelay, policy.BackoffFactor));
    }

    [Fact]
    public void Writing_KeepsEveryMember_AndRoundTripsAreStable()
    {
        // 写出时不省略等于初值的成员：初值也写出来，读回来的值相同
        var definition = Read("{\"id\":\"d\",\"name\":\"ok\",\"nodes\":[{\"id\":\"s\",\"activityType\":\"Start\",\"retryPolicy\":{}}]}");
        var json = JsonNode.Parse(WorkflowJson.Serialize(definition))!;
        Assert.Equal(1, json["version"]!.GetValue<int>());
        var retry = json["nodes"]![0]!["retryPolicy"]!;
        Assert.Equal((1, "00:00:10", 2.0), (retry["maxAttempts"]!.GetValue<int>(), retry["firstDelay"]!.GetValue<string>(), retry["backoffFactor"]!.GetValue<double>()));
        Assert.Null(json["Version"]);
        Assert.Null(retry["MaxAttempts"]);

        var back = Read(json.ToJsonString());
        Assert.Equal(WorkflowJson.Serialize(definition), WorkflowJson.Serialize(back));
        Assert.Equal(definition.Nodes[0].RetryPolicy, back.Nodes[0].RetryPolicy);

        // 写出的成员名不变：与改动之前写出的文档形状一致
        Assert.Equal(["backoffFactor", "firstDelay", "maxAttempts"], retry.AsObject().Select(static member => member.Key).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// 读入合法的定义。
    /// </summary>
    private static WorkflowDefinition Read(string json) => WorkflowJson.DeserializeDefinition(json).ValueOrThrow();

    /// <summary>
    /// 逐个序列化成员比较：读出来的与直接构造的值相同。
    /// </summary>
    private static void AssertSameMembers<T>(JsonTypeInfo<T> type, string json, T constructed)
        where T : notnull
    {
        var read = JsonSerializer.Deserialize(json, type)!;
        foreach (var property in type.Properties)
        {
            var expected = JsonSerializer.SerializeToNode(property.Get!(constructed), property.PropertyType, type.Options);
            var actual = JsonSerializer.SerializeToNode(property.Get!(read), property.PropertyType, type.Options);
            Assert.True(JsonNode.DeepEquals(expected, actual), $"{typeof(T).Name}.{property.Name}: constructed {expected?.ToJsonString()} but read {actual?.ToJsonString()}");
        }
    }
}
