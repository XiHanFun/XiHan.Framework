// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 定义构建器产出的模型：节点属性、转移 ID 与默认出边、变量与元数据。
/// </summary>
public sealed class WorkflowDefinitionBuilderTests
{
    [Fact]
    public void Build_ProducesNodesTransitionsAndMetadata()
    {
        var definition = WorkflowDefinitionBuilder.Create("order-approval", "Order approval")
            .WithDescription("desc").WithCategory("sales").WithCompensation().WithProperties(new JsonObject { ["icon"] = "cart" })
            .AddVariable("amount", required: true, description: "The amount")
            .AddVariable("currency", defaultValue: "CNY")
            .AddStart()
            .AddNode("flaky", ActivityTypes.Log, "Flaky", node => node
                .WithProperty("Text", "hello").WithProperty("Count", 3).WithProperty("Flag", true).WithProperty("Shape", new JsonObject { ["k"] = "v" })
                .WithRetry(3, TimeSpan.FromSeconds(2), 1.5).WithTimeout(TimeSpan.FromMinutes(1)).WithContinueOnError().WithDisplayName("Renamed"))
            .AddUserTask("approve", "Approve", node => node.WithAssigneeRule(AssigneeRuleTypes.User, "alice", "bob").WithAssigneeRule(AssigneeRuleTypes.Starter))
            .AddDecision("check").AddParallel("fork").AddJoin("join", waitAny: true).AddDelay("wait", TimeSpan.FromSeconds(90))
            .AddEnd("done", outcome: "ok").AddEnd()
            .AddTransition("start", "flaky").AddTransition("flaky", "approve").AddTransition("approve", "check")
            .AddTransition("check", "done", "outcome == 'approved'", priority: 5, displayName: "approved").AddTransition("check", "done", "amount == 0")
            .AddDefaultTransition("check", "end", "otherwise")
            .Build();

        Assert.Equal(("order-approval", "Order approval", "desc", "sales", true, WorkflowDefinitionStatus.Draft, 1), (definition.Name.Value, definition.DisplayName, definition.Description, definition.Category, definition.EnableCompensation, definition.Status, definition.Version));
        Assert.Equal(32, definition.Id.Length);
        Assert.Equal("cart", definition.Properties!["icon"]!.GetValue<string>());
        Assert.Equal([("amount", true, "The amount"), ("currency", false, null)], definition.Variables.Select(static variable => (variable.Name, variable.Required, variable.Description)));
        Assert.Equal("CNY", definition.Variables[1].DefaultValue!.GetValue<string>());

        var flaky = definition.FindNode("flaky").ValueOrThrow();
        Assert.Equal(("Renamed", "hello", 3, true, "v"), (flaky.DisplayName, flaky.Properties["Text"]!.GetValue<string>(), flaky.Properties["Count"]!.GetValue<int>(), flaky.Properties["Flag"]!.GetValue<bool>(), flaky.Properties["Shape"]!["k"]!.GetValue<string>()));
        Assert.Equal((3, TimeSpan.FromSeconds(2), 1.5), (flaky.RetryPolicy!.MaxAttempts, flaky.RetryPolicy.FirstDelay, flaky.RetryPolicy.BackoffFactor));
        Assert.Equal((TimeSpan.FromMinutes(1), true), (flaky.Timeout, flaky.ContinueOnError));
        Assert.Equal("Flaky", WorkflowDefinitionBuilder.Create("x").AddNode("flaky", ActivityTypes.Log, "Flaky").Build().FindNode("flaky").ValueOrThrow().Title);

        var rules = AssigneeRule.Parse(definition.FindNode("approve").ValueOrThrow().Properties[UserTaskActivity.AssigneeRulesProperty]).ValueOrThrow();
        Assert.Equal([(AssigneeRuleTypes.User, "alice,bob"), (AssigneeRuleTypes.Starter, "")], rules.Select(static rule => (rule.Type, string.Join(',', rule.Values))));
        Assert.Equal(JoinActivity.WaitAny, definition.FindNode("join").ValueOrThrow().Properties[JoinActivity.ModeProperty]!.GetValue<string>());
        Assert.Equal(90, WorkflowValues.ConvertOr(definition.FindNode("wait").ValueOrThrow().Properties[DelayActivity.DurationProperty], 0));
        Assert.Equal("ok", definition.FindNode("done").ValueOrThrow().Properties[EndActivity.OutcomeProperty]!.GetValue<string>());
        Assert.Null(definition.FindNode("end").ValueOrThrow().Properties[EndActivity.OutcomeProperty]);
        Assert.False(definition.FindNode("missing").HasValue);

        Assert.Equal(["start-flaky.1", "flaky-approve.1", "approve-check.1", "check-done.1", "check-done.2", "check-end.1"], definition.Transitions.Select(static transition => transition.Id));
        var approved = definition.Transitions[3];
        Assert.Equal(("outcome == 'approved'", 5, "approved", false), (approved.Condition, approved.Priority, approved.DisplayName, approved.IsDefault));
        var fallback = definition.Transitions[^1];
        Assert.Equal((true, int.MaxValue, "otherwise", null), (fallback.IsDefault, fallback.Priority, fallback.DisplayName, fallback.Condition));
    }

    [Fact]
    public void Build_RejectsInvalidIdentifiers()
    {
        Assert.ThrowsAny<ArgumentException>(() => WorkflowDefinitionBuilder.Create("Bad Name"));
        Assert.ThrowsAny<ArgumentException>(() => WorkflowDefinitionBuilder.Create("ok").AddNode("bad id", ActivityTypes.Log));
        Assert.ThrowsAny<ArgumentException>(() => WorkflowDefinitionBuilder.Create("ok").AddNode("a", "bad type!"));
    }
}
