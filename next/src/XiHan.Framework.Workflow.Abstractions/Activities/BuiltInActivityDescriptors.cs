// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 内置活动的描述符（设计器用）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class BuiltInActivityDescriptors
{
    /// <summary>控制类。</summary>
    public const string ControlCategory = "Control";

    /// <summary>人工类。</summary>
    public const string HumanCategory = "Human";

    /// <summary>集成类。</summary>
    public const string IntegrationCategory = "Integration";

    /// <summary>数据类。</summary>
    public const string DataCategory = "Data";

    /// <summary>Start。</summary>
    public static ActivityDescriptor Start { get; } = new(ActivityTypes.Start, "Start", ControlCategory, "The single entry node.", [Input(StartActivity.SignalNameProperty, "string", description: "Start an instance when this signal is raised."), Input(StartActivity.CronProperty, "string", description: "Start an instance on this cron schedule."), Input(StartActivity.TimeZoneProperty, "string")]);

    /// <summary>End。</summary>
    public static ActivityDescriptor End { get; } = new(ActivityTypes.End, "End", ControlCategory, "Ends the instance; an Outcome runs the outcome handlers first.", [Input(EndActivity.OutcomeProperty, "template")]);

    /// <summary>Terminate。</summary>
    public static ActivityDescriptor Terminate { get; } = new(ActivityTypes.Terminate, "Terminate", ControlCategory, "Terminates the instance and its children.", [Input(TerminateActivity.ReasonProperty, "template")]);

    /// <summary>SetVariable。</summary>
    public static ActivityDescriptor SetVariable { get; } = new(ActivityTypes.SetVariable, "Set variables", DataCategory, "Sets variables from literals and expressions.", [Input(SetVariableActivity.ValuesProperty, "object"), Input(SetVariableActivity.ExpressionsProperty, "object")]);

    /// <summary>Decision。</summary>
    public static ActivityDescriptor Decision { get; } = new(ActivityTypes.Decision, "Decision", ControlCategory, "Takes the first matching outgoing transition.");

    /// <summary>Parallel。</summary>
    public static ActivityDescriptor Parallel { get; } = new(ActivityTypes.Parallel, "Parallel", ControlCategory, "Takes every outgoing transition.");

    /// <summary>Join。</summary>
    public static ActivityDescriptor Join { get; } = new(ActivityTypes.Join, "Join", ControlCategory, "Waits for incoming branches.", [Input(JoinActivity.ModeProperty, "string", description: "WaitAll or WaitAny.")]);

    /// <summary>Delay。</summary>
    public static ActivityDescriptor Delay { get; } = new(ActivityTypes.Delay, "Delay", ControlCategory, "Waits for a duration, until a time, or until the next cron occurrence.", [Input(DelayActivity.DurationProperty, "expression"), Input(DelayActivity.UntilProperty, "expression"), Input(DelayActivity.CronProperty, "string"), Input(DelayActivity.TimeZoneProperty, "string")]);

    /// <summary>UserTask。</summary>
    public static ActivityDescriptor UserTask { get; } = new(ActivityTypes.UserTask, "User task", HumanCategory, "Waits for people to act.",
    [
        Input(UserTaskActivity.AssigneesProperty, "array"), Input(UserTaskActivity.AssigneesExpressionProperty, "expression"), Input(UserTaskActivity.AssigneeRulesProperty, "array"), Input(UserTaskActivity.ExcludeStarterProperty, "boolean"),
        Input(UserTaskActivity.EmptyAssigneeStrategyProperty, "string"), Input(UserTaskActivity.FallbackAssigneeRulesProperty, "array"), Input(UserTaskActivity.CompletionPolicyProperty, "string"), Input(UserTaskActivity.TitleProperty, "template"),
        Input(UserTaskActivity.FormDataProperty, "object"), Input(UserTaskActivity.CcUserIdsProperty, "array"), Input(UserTaskActivity.CcRulesProperty, "array"), Input(UserTaskActivity.AllowedOutcomesProperty, "array"),
    ]);

    /// <summary>PublishEvent。</summary>
    public static ActivityDescriptor PublishEvent { get; } = new(ActivityTypes.PublishEvent, "Publish event", IntegrationCategory, "Publishes a workflow.custom message.", [Input(PublishEventActivity.EventNameProperty, "template", true), Input(PublishEventActivity.PayloadProperty, "object")]);

    /// <summary>WaitSignal。</summary>
    public static ActivityDescriptor WaitSignal { get; } = new(ActivityTypes.WaitSignal, "Wait for signal", IntegrationCategory, "Waits for a named signal.", [Input(WaitSignalActivity.SignalNameProperty, "template", true), Input(WaitSignalActivity.AcceptAnyCorrelationProperty, "boolean")]);

    /// <summary>EventGateway。</summary>
    public static ActivityDescriptor EventGateway { get; } = new(ActivityTypes.EventGateway, "Event gateway", IntegrationCategory, "Waits for the first of several signals or a timeout.", [Input(EventGatewayActivity.SignalsProperty, "array", true), Input(EventGatewayActivity.TimeoutSecondsProperty, "number"), Input(EventGatewayActivity.AcceptAnyCorrelationProperty, "boolean")]);

    /// <summary>SubWorkflow。</summary>
    public static ActivityDescriptor SubWorkflow { get; } = new(ActivityTypes.SubWorkflow, "Sub-workflow", ControlCategory, "Starts another workflow.",
    [
        Input(SubWorkflowActivity.DefinitionNameProperty, "template", true), Input(SubWorkflowActivity.DefinitionVersionProperty, "number"), Input(SubWorkflowActivity.VariablesProperty, "object"), Input(SubWorkflowActivity.VariableExpressionsProperty, "object"),
        Input(SubWorkflowActivity.WaitForCompletionProperty, "boolean"), Input(SubWorkflowActivity.ResultVariableProperty, "string"), Input(SubWorkflowActivity.FailOnChildFaultProperty, "boolean"),
    ]);

    /// <summary>ForEach。</summary>
    public static ActivityDescriptor ForEach { get; } = new(ActivityTypes.ForEach, "For each", ControlCategory, "Starts a sub-workflow per item.",
    [
        Input(ForEachActivity.ItemsProperty, "expression", true), Input(ForEachActivity.ItemVariableProperty, "string"), Input(SubWorkflowActivity.DefinitionNameProperty, "template", true), Input(ForEachActivity.ParallelProperty, "boolean"),
        Input(ForEachActivity.FailFastProperty, "boolean"), Input(SubWorkflowActivity.ResultVariableProperty, "string"),
    ]);

    /// <summary>Log。</summary>
    public static ActivityDescriptor Log { get; } = new(ActivityTypes.Log, "Log", DataCategory, "Writes a log line.", [Input(LogActivity.MessageProperty, "template", true), Input(LogActivity.LevelProperty, "string")]);

    /// <summary>Fault。</summary>
    public static ActivityDescriptor Fault { get; } = new(ActivityTypes.Fault, "Fault", ControlCategory, "Faults the instance.", [Input(FaultActivity.MessageProperty, "template")]);

    /// <summary>Compensate。</summary>
    public static ActivityDescriptor Compensate { get; } = new(ActivityTypes.Compensate, "Compensate", ControlCategory, "Compensates completed nodes in reverse order.", [Input(CompensateActivity.NodeIdsProperty, "array")]);

    /// <summary>Http。</summary>
    public static ActivityDescriptor Http { get; } = new(ActivityTypes.Http, "HTTP request", IntegrationCategory, "Sends an HTTP request.",
    [
        Input(HttpActivity.UrlProperty, "template", true), Input(HttpActivity.MethodProperty, "string"), Input(HttpActivity.HeadersProperty, "object"), Input(HttpActivity.BodyProperty, "template"),
        Input(HttpActivity.ContentTypeProperty, "string"), Input(HttpActivity.TimeoutSecondsProperty, "number"), Input(HttpActivity.ResultVariableProperty, "string"), Input(HttpActivity.FailOnErrorStatusProperty, "boolean"),
    ]);

    /// <summary>ExternalTask。</summary>
    public static ActivityDescriptor ExternalTask { get; } = new(ActivityTypes.ExternalTask, "External task", IntegrationCategory, "Waits for an external worker to complete the task.", [Input(ExternalTaskActivity.TopicProperty, "template", true), Input(ExternalTaskActivity.PayloadProperty, "object")]);

    /// <summary>
    /// 输入描述。
    /// </summary>
    private static ActivityInputDescriptor Input(string name, string kind, bool required = false, string? description = null) => new(name, kind, required, description);
}
