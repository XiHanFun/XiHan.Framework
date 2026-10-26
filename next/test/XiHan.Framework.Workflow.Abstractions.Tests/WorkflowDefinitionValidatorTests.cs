// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 定义校验：结构、转移、表达式、可达性与活动属性，一次返回全部问题。
/// </summary>
public sealed class WorkflowDefinitionValidatorTests
{
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
        Assert.Empty(scope.Validator.Errors(definition));
    }

    [Fact]
    public void Validate_ReportsEveryStructuralProblem()
    {
        using var scope = Validator();
        var errors = scope.Validator.Errors(new WorkflowDefinition { Id = "d", Name = new WorkflowName("empty") });
        Assert.Contains(errors, error => error.Contains("has no nodes", StringComparison.Ordinal));

        var definition = new WorkflowDefinition
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
        errors = scope.Validator.Errors(definition);
        string[] expected =
        [
            "Node ID 'dup' is duplicated.",
            "activity type 'NoSuchActivity' is not registered.",
            "more than one Start node",
            "Transition ID 't1' is duplicated.",
            "source node 'ghost' does not exist.",
            "target node 'phantom' does not exist.",
            "the Start node must not have incoming transitions.",
            "Transition 't6':",
            "Node 'join' has more than one default transition.",
            "Node 'island' is not reachable from the Start node.",
            "join mode 'Sometimes' is unknown.",
            "the End outcome must not be empty.",
            "a UserTask needs Assignees, AssigneesExpression or AssigneeRules.",
            "completion policy 'Whenever' is unknown.",
            "the Fallback strategy needs FallbackAssigneeRules.",
            "Martian",
            "Node 'task2':",
        ];
        foreach (var fragment in expected)
        {
            Assert.True(errors.Any(error => error.Contains(fragment, StringComparison.Ordinal)), $"Missing '{fragment}' in: {string.Join(" | ", errors)}");
        }

        var result = scope.Validator.Validate(definition);
        Assert.Equal(WorkflowErrors.DefinitionInvalid, result.Error?.Code);
        Assert.Contains("NoSuchActivity", result.Error!.Details!["errors"]!.ToString(), StringComparison.Ordinal);

        Assert.Contains(scope.Validator.Errors(WorkflowDefinitionBuilder.Create("nostart").AddNode("a", ActivityTypes.Log).Build()), error => error.Contains("has no Start node", StringComparison.Ordinal));
    }

    [Fact]
    public void NodesWithoutProperties_ValidateWithoutErrors()
    {
        // 设计器交来的 JSON 里 Start / 普通节点常常不带 properties 或给 null：校验器按空属性对待，不能抛空引用
        const string json = "{\"id\":\"d\",\"name\":\"noprops\",\"version\":1,\"nodes\":[{\"id\":\"start\",\"activityType\":\"Start\"},{\"id\":\"join\",\"activityType\":\"Join\",\"properties\":null},{\"id\":\"end\",\"activityType\":\"End\"}],\"transitions\":[{\"id\":\"t1\",\"sourceNodeId\":\"start\",\"targetNodeId\":\"join\"},{\"id\":\"t2\",\"sourceNodeId\":\"join\",\"targetNodeId\":\"end\"}]}";
        using var scope = Validator();

        var definition = WorkflowJson.DeserializeDefinition(json).ValueOrThrow();

        Assert.Empty(scope.Validator.Errors(definition));
        Assert.True(scope.Validator.Validate(definition).IsSuccess);
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
