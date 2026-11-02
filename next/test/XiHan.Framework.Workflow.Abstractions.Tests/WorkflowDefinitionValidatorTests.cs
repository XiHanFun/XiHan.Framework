// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 定义校验：一次返回全部问题，每条带路径、消息键与参数。
/// </summary>
public sealed class WorkflowDefinitionValidatorTests
{
    /// <summary>
    /// 测试用的解析器。
    /// </summary>
    private static readonly WorkflowCatalogResolver _messages = new();

    [Fact]
    public void Validate_AcceptsAWellFormedDefinition()
    {
        using var scope = Validator();
        var definition = WorkflowDefinitionBuilder.Create("ok")
            .AddStart()
            .AddUserTask("approve", null, node => node.WithAssigneeRule(AssigneeRuleTypes.Starter).WithProperty(UserTaskActivity.EmptyAssigneeStrategyProperty, EmptyAssigneeStrategy.Fallback.ToString()).WithProperty(UserTaskActivity.FallbackAssigneeRulesProperty, new JsonArray(new AssigneeRule(AssigneeRuleTypes.User, ["admin"]).ToJson())))
            .AddDecision("check").AddJoin("join", waitAny: true).AddEnd("ok", outcome: "ok").AddEnd("no", outcome: "no")
            .AddTransition("start", "approve").AddTransition("approve", "check")
            .AddTransition("check", "join", "outcome == 'approved'").AddDefaultTransition("check", "no").AddTransition("join", "ok")
            .Build();
        Assert.True(scope.Validator.Validate(definition).IsSuccess);
        Assert.Empty(scope.Validator.Diagnose(definition));
    }

    [Fact]
    public void Validate_ReportsEveryStructuralProblem_WithPathKeyAndArguments()
    {
        using var scope = Validator();
        Assert.Equal(("$.nodes", WorkflowMessages.DefinitionNoNodes), Single(scope.Validator.Diagnose(new WorkflowDefinition { Id = "d", Name = new WorkflowName("empty") })));

        var definition = Broken();
        var issues = scope.Validator.Diagnose(definition);
        (string Path, string Key)[] expected =
        [
            ("$.nodes[1]", WorkflowMessages.DefinitionStartDuplicate),
            ("$.nodes[3]", WorkflowMessages.DefinitionNodeDuplicate),
            ("$.nodes[4].activityType", WorkflowMessages.DefinitionActivityUnknown),
            ("$.nodes[5]", WorkflowMessages.DefinitionNodeUnreachable),
            ("$.nodes[6]", WorkflowMessages.DefinitionDefaultDuplicate),
            ("$.nodes[6].properties.Mode", WorkflowMessages.PropertyUnknownValue),
            ("$.nodes[7].properties.Outcome", WorkflowMessages.PropertyEmpty),
            ("$.nodes[8]", WorkflowMessages.DefinitionUserTaskNoAssignees),
            ("$.nodes[8].properties.CompletionPolicy", WorkflowMessages.PropertyUnknownValue),
            ("$.nodes[8].properties.FallbackAssigneeRules", WorkflowMessages.DefinitionFallbackRulesMissing),
            ("$.nodes[9].properties.AssigneeRules", WorkflowMessages.AssigneeRuleUnknown),
            ("$.nodes[9].properties.AssigneeRules", WorkflowMessages.ExpressionUnexpectedEnd),
            ("$.transitions[1]", WorkflowMessages.DefinitionTransitionDuplicate),
            ("$.transitions[2].sourceNodeId", WorkflowMessages.DefinitionTransitionSourceMissing),
            ("$.transitions[3].targetNodeId", WorkflowMessages.DefinitionTransitionTargetMissing),
            ("$.transitions[4].targetNodeId", WorkflowMessages.DefinitionStartIncoming),
            ("$.transitions[5].condition", WorkflowMessages.ExpressionUnexpectedEnd),
        ];
        foreach (var (path, key) in expected)
        {
            Assert.True(issues.Any(issue => issue.Path == path && issue.MessageKey == key), $"Missing {path} {key} in: {string.Join(" | ", issues.Select(static issue => $"{issue.Path} {issue.MessageKey}"))}");
        }

        // 每条都有消息键；原因文本是日志用的回退，不为空
        Assert.All(issues, static issue => Assert.False(string.IsNullOrEmpty(issue.MessageKey) || string.IsNullOrEmpty(issue.Reason)));
        var unknown = Assert.Single(issues, static issue => issue.MessageKey == WorkflowMessages.DefinitionActivityUnknown);
        Assert.Equal(("nope", (object?)"NoSuchActivity"), ((string?)unknown.Arguments!["node"], unknown.Arguments["type"]));

        // 整体的错误：参数 count，details.errors 是同一组问题（宿主写成 Problem Details 的字段级 errors）
        var result = scope.Validator.Validate(definition);
        Assert.Equal((WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionInvalid, (object?)issues.Count), (result.Error?.Code, result.Error?.MessageKey, result.Error?.Details?["count"]));
        Assert.Equal(issues.Select(static issue => (issue.Path, issue.MessageKey)), Assert.IsAssignableFrom<IReadOnlyList<SchemaViolation>>(result.Error!.Details!["errors"]).Select(static issue => (issue.Path, issue.MessageKey)));

        Assert.Equal(("$.nodes", WorkflowMessages.DefinitionStartMissing), Single(scope.Validator.Diagnose(WorkflowDefinitionBuilder.Create("nostart").AddNode("a", ActivityTypes.Log).Build())));
    }

    [Fact]
    public void Diagnostics_RenderInTheCallersLocale()
    {
        using var scope = Validator();
        var issues = scope.Validator.Diagnose(Broken());
        string Text(string locale, string path, string key)
        {
            var issue = Assert.Single(issues, issue => issue.Path == path && issue.MessageKey == key);
            using var messages = MessageScope.Use(_messages, locale);
            return issue.MessageKey!.L(issue.Arguments);
        }

        Assert.Equal("节点 'island' 从开始节点走不到。", Text("zh-CN", "$.nodes[5]", WorkflowMessages.DefinitionNodeUnreachable));
        Assert.Equal("Node 'island' cannot be reached from the Start node.", Text("en-US", "$.nodes[5]", WorkflowMessages.DefinitionNodeUnreachable));
        Assert.Equal("表达式 'amount >' 意外结束。", Text("zh-CN", "$.transitions[5].condition", WorkflowMessages.ExpressionUnexpectedEnd));
        Assert.Equal("Expression 'amount >' ends unexpectedly.", Text("en-US", "$.transitions[5].condition", WorkflowMessages.ExpressionUnexpectedEnd));
        Assert.Equal("活动属性 'Mode' 的值 'Sometimes' 不在允许的取值范围内。", Text("zh-CN", "$.nodes[6].properties.Mode", WorkflowMessages.PropertyUnknownValue));
        Assert.Equal("指派规则类型 'Martian' 未登记。", Text("zh-CN", "$.nodes[9].properties.AssigneeRules", WorkflowMessages.AssigneeRuleUnknown));
        Assert.Equal("转移 't3' 的源节点 'ghost' 不存在。", Text("zh-CN", "$.transitions[2].sourceNodeId", WorkflowMessages.DefinitionTransitionSourceMissing));
        Assert.Equal("Transition 't3': source node 'ghost' does not exist.", Text("en-US", "$.transitions[2].sourceNodeId", WorkflowMessages.DefinitionTransitionSourceMissing));

        var error = scope.Validator.Validate(Broken()).Error!;
        using (MessageScope.Use(_messages, "zh-CN"))
        {
            Assert.Equal($"工作流定义有 {issues.Count} 处问题。", error.MessageKey!.L(error.Details));
        }

        using (MessageScope.Use(_messages, "en-US"))
        {
            Assert.Equal($"The workflow definition has {issues.Count} problem(s).", error.MessageKey!.L(error.Details));
        }
    }

    [Fact]
    public void DefinitionJsonErrors_CarryMessageKeys()
    {
        var malformed = WorkflowJson.DeserializeDefinition("{\"id\":\"d\",\"name\":\"Bad Name\",\"nodes\":[]}").Error!;
        Assert.Equal((WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionJsonMalformed, (object?)"$.name"), (malformed.Code, malformed.MessageKey, malformed.Details!["path"]));
        using (MessageScope.Use(_messages, "zh-CN"))
        {
            Assert.Equal($"工作流定义的 JSON 在 '$.name' 处不合法（第 1 行，第 {malformed.Details["position"]} 个字节）。", malformed.MessageKey!.L(malformed.Details));
        }

        Assert.Equal(WorkflowMessages.DefinitionJsonMalformed, WorkflowJson.DeserializeDefinition("not json").Error?.MessageKey);
        Assert.Equal(WorkflowMessages.DefinitionJsonNull, WorkflowJson.DeserializeDefinition("null").Error?.MessageKey);

        // 模型自己拒绝的值（这里是不合法的节点 ID）：外层的键本地化，参数是模型的英文说明
        var value = WorkflowJson.DeserializeDefinition("{\"id\":\"d\",\"name\":\"ok\",\"nodes\":[{\"id\":\"bad id\",\"activityType\":\"Log\"}]}").Error!;
        Assert.Equal((WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionJsonValueInvalid), (value.Code, value.MessageKey));
        Assert.Contains("bad id", (string?)value.Details!["reason"], StringComparison.Ordinal);
    }

    [Fact]
    public void NodesWithoutProperties_ValidateWithoutErrors()
    {
        // 设计器交来的 JSON 里 Start / 普通节点常常不带 properties 或给 null：校验器按空属性对待，不能抛空引用
        const string json = "{\"id\":\"d\",\"name\":\"noprops\",\"version\":1,\"nodes\":[{\"id\":\"start\",\"activityType\":\"Start\"},{\"id\":\"join\",\"activityType\":\"Join\",\"properties\":null},{\"id\":\"end\",\"activityType\":\"End\"}],\"transitions\":[{\"id\":\"t1\",\"sourceNodeId\":\"start\",\"targetNodeId\":\"join\"},{\"id\":\"t2\",\"sourceNodeId\":\"join\",\"targetNodeId\":\"end\"}]}";
        using var scope = Validator();

        var definition = WorkflowJson.DeserializeDefinition(json).ValueOrThrow();

        Assert.Empty(scope.Validator.Diagnose(definition));
        Assert.True(scope.Validator.Validate(definition).IsSuccess);
    }

    /// <summary>
    /// 一个处处有问题的定义。
    /// </summary>
    private static WorkflowDefinition Broken() => new()
    {
        Id = "d",
        Name = new WorkflowName("broken"),
        Nodes =
        [
            new WorkflowNode { Id = "start", ActivityType = ActivityTypes.Start },
            new WorkflowNode { Id = "start2", ActivityType = ActivityTypes.Start },
            new WorkflowNode { Id = "dup", ActivityType = ActivityTypes.Log },
            new WorkflowNode { Id = "dup", ActivityType = ActivityTypes.Log },
            new WorkflowNode { Id = "nope", ActivityType = "NoSuchActivity" },
            new WorkflowNode { Id = "island", ActivityType = ActivityTypes.Log },
            new WorkflowNode { Id = "join", ActivityType = ActivityTypes.Join, Properties = new JsonObject { [JoinActivity.ModeProperty] = "Sometimes" } },
            new WorkflowNode { Id = "end", ActivityType = ActivityTypes.End, Properties = new JsonObject { [EndActivity.OutcomeProperty] = " " } },
            new WorkflowNode { Id = "task", ActivityType = ActivityTypes.UserTask, Properties = new JsonObject { [UserTaskActivity.CompletionPolicyProperty] = "Whenever", [UserTaskActivity.EmptyAssigneeStrategyProperty] = "Fallback" } },
            new WorkflowNode { Id = "task2", ActivityType = ActivityTypes.UserTask, Properties = new JsonObject { [UserTaskActivity.AssigneeRulesProperty] = new JsonArray(new JsonObject { ["type"] = "Martian", ["values"] = new JsonArray("x") }, new JsonObject { ["type"] = AssigneeRuleTypes.Expression, ["values"] = new JsonArray("1 +") }) } },
        ],
        Transitions =
        [
            new WorkflowTransition { Id = "t1", SourceNodeId = "start", TargetNodeId = "dup" },
            new WorkflowTransition { Id = "t1", SourceNodeId = "dup", TargetNodeId = "nope" },
            new WorkflowTransition { Id = "t3", SourceNodeId = "ghost", TargetNodeId = "dup" },
            new WorkflowTransition { Id = "t4", SourceNodeId = "dup", TargetNodeId = "phantom" },
            new WorkflowTransition { Id = "t5", SourceNodeId = "dup", TargetNodeId = "start" },
            new WorkflowTransition { Id = "t6", SourceNodeId = "nope", TargetNodeId = "join", Condition = "amount >" },
            new WorkflowTransition { Id = "t7", SourceNodeId = "join", TargetNodeId = "end", IsDefault = true },
            new WorkflowTransition { Id = "t8", SourceNodeId = "join", TargetNodeId = "task", IsDefault = true },
            new WorkflowTransition { Id = "t9", SourceNodeId = "task", TargetNodeId = "task2" },
        ],
    };

    /// <summary>
    /// 唯一一条问题的路径与消息键。
    /// </summary>
    private static (string Path, string? Key) Single(IReadOnlyList<SchemaViolation> issues)
    {
        var issue = Assert.Single(issues);
        return (issue.Path, issue.MessageKey);
    }

    /// <summary>
    /// 从容器解析校验器（与引擎相同的活动登记）。
    /// </summary>
    private static ValidatorScope Validator()
    {
        var services = new ServiceCollection();
        services.AddWorkflowEngine(new WorkflowOptions());
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return new ValidatorScope(provider, scope, scope.ServiceProvider.GetRequiredService<WorkflowDefinitionValidator>());
    }

    /// <summary>
    /// 校验器与它的容器。
    /// </summary>
    private sealed class ValidatorScope(ServiceProvider provider, IServiceScope scope, WorkflowDefinitionValidator validator) : IDisposable
    {
        /// <summary>
        /// 校验器。
        /// </summary>
        public WorkflowDefinitionValidator Validator => validator;

        /// <inheritdoc />
        public void Dispose()
        {
            scope.Dispose();
            provider.Dispose();
        }
    }
}
