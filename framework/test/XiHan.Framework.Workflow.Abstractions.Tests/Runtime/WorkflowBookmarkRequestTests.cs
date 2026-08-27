// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 书签创建请求模型测试
/// </summary>
/// <remarks>
/// 请求是书签的"半成品"：只声明等待点语义（Kind/Key/DueTime/CorrelationId/Payload），
/// 标识与实例归属由引擎补齐。所以请求上刻意没有 Id/InstanceId/NodeInstanceId——
/// 一旦有人给请求补上这些字段，就意味着活动可以伪造归属，这里用反射把这条边界钉死。
/// </remarks>
public class WorkflowBookmarkRequestTests
{
    /// <summary>
    /// 新建请求的默认值语义
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_AreEmptyAndUnbounded()
    {
        var request = new WorkflowBookmarkRequest();

        Assert.Equal(string.Empty, request.Kind);
        Assert.Null(request.Key);
        Assert.Empty(request.Payload);
        Assert.Null(request.DueTime);
        Assert.Null(request.CorrelationId);
    }

    /// <summary>
    /// 请求不声明标识与实例归属，由引擎补齐
    /// </summary>
    [Fact]
    public void Type_DoesNotExposeEngineOwnedIdentityFields()
    {
        var type = typeof(WorkflowBookmarkRequest);

        Assert.Null(type.GetProperty("Id"));
        Assert.Null(type.GetProperty("InstanceId"));
        Assert.Null(type.GetProperty("NodeId"));
        Assert.Null(type.GetProperty("NodeInstanceId"));
        Assert.Null(type.GetProperty("CreationTime"));
        Assert.Null(type.GetProperty("TenantId"));
    }

    /// <summary>
    /// 不同请求实例的载荷字典互相独立
    /// </summary>
    [Fact]
    public void Payload_OnDistinctInstances_AreNotShared()
    {
        var first = new WorkflowBookmarkRequest();
        var second = new WorkflowBookmarkRequest();

        first.Payload["title"] = "待办";

        Assert.Empty(second.Payload);
        Assert.NotSame(first.Payload, second.Payload);
    }

    /// <summary>
    /// 请求 JSON 往返保留全部声明字段
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesDeclaredFields()
    {
        var request = new WorkflowBookmarkRequest
        {
            Kind = WorkflowBookmarkKinds.Timer,
            Key = "delay",
            CorrelationId = "biz-1",
            DueTime = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc),
            Payload = { ["seconds"] = 30 }
        };

        var restored = JsonSerializer.Deserialize<WorkflowBookmarkRequest>(JsonSerializer.Serialize(request));

        Assert.NotNull(restored);
        Assert.Equal(WorkflowBookmarkKinds.Timer, restored.Kind);
        Assert.Equal("delay", restored.Key);
        Assert.Equal("biz-1", restored.CorrelationId);
        Assert.Equal(request.DueTime, restored.DueTime);
        Assert.Equal(30m, WorkflowValueConverter.Normalize(restored.Payload["seconds"]));
    }
}
