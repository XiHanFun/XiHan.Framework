// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 内置活动的描述符（设计器用）。
/// </summary>
/// <remarks>
/// 显示名、说明、分类显示名与输入说明都是消息键（ADR-0106），<see cref="ActivityDescriptor.Localize"/> 按调用方的区域渲染。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class BuiltInActivityDescriptors
{
    /// <summary>
    /// 控制类。
    /// </summary>
    public const string ControlCategory = "Control";

    /// <summary>
    /// 人工类。
    /// </summary>
    public const string HumanCategory = "Human";

    /// <summary>
    /// 集成类。
    /// </summary>
    public const string IntegrationCategory = "Integration";

    /// <summary>
    /// 数据类。
    /// </summary>
    public const string DataCategory = "Data";

    /// <summary>
    /// Start。
    /// </summary>
    public static ActivityDescriptor Start { get; } = Describe(ActivityTypes.Start, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivityStartName, WorkflowMessages.ActivityStartDescription, [Input(StartActivity.SignalNameProperty, "string", descriptionKey: WorkflowMessages.ActivityStartSignalNameInput), Input(StartActivity.CronProperty, "string", descriptionKey: WorkflowMessages.ActivityStartCronInput), Input(StartActivity.TimeZoneProperty, "string")]);

    /// <summary>
    /// End。
    /// </summary>
    public static ActivityDescriptor End { get; } = Describe(ActivityTypes.End, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivityEndName, WorkflowMessages.ActivityEndDescription, [Input(EndActivity.OutcomeProperty, "template")]);

    /// <summary>
    /// Terminate。
    /// </summary>
    public static ActivityDescriptor Terminate { get; } = Describe(ActivityTypes.Terminate, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivityTerminateName, WorkflowMessages.ActivityTerminateDescription, [Input(TerminateActivity.ReasonProperty, "template")]);

    /// <summary>
    /// SetVariable。
    /// </summary>
    public static ActivityDescriptor SetVariable { get; } = Describe(ActivityTypes.SetVariable, Category(DataCategory, WorkflowMessages.ActivityCategoryData), WorkflowMessages.ActivitySetVariableName, WorkflowMessages.ActivitySetVariableDescription, [Input(SetVariableActivity.ValuesProperty, "object"), Input(SetVariableActivity.ExpressionsProperty, "object")]);

    /// <summary>
    /// Decision。
    /// </summary>
    public static ActivityDescriptor Decision { get; } = Describe(ActivityTypes.Decision, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivityDecisionName, WorkflowMessages.ActivityDecisionDescription);

    /// <summary>
    /// Parallel。
    /// </summary>
    public static ActivityDescriptor Parallel { get; } = Describe(ActivityTypes.Parallel, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivityParallelName, WorkflowMessages.ActivityParallelDescription);

    /// <summary>
    /// Join。
    /// </summary>
    public static ActivityDescriptor Join { get; } = Describe(ActivityTypes.Join, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivityJoinName, WorkflowMessages.ActivityJoinDescription, [Input(JoinActivity.ModeProperty, "string", descriptionKey: WorkflowMessages.ActivityJoinModeInput)]);

    /// <summary>
    /// Delay。
    /// </summary>
    public static ActivityDescriptor Delay { get; } = Describe(ActivityTypes.Delay, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivityDelayName, WorkflowMessages.ActivityDelayDescription, [Input(DelayActivity.DurationProperty, "expression"), Input(DelayActivity.UntilProperty, "expression"), Input(DelayActivity.CronProperty, "string"), Input(DelayActivity.TimeZoneProperty, "string")]);

    /// <summary>
    /// UserTask。
    /// </summary>
    public static ActivityDescriptor UserTask { get; } = Describe(ActivityTypes.UserTask, Category(HumanCategory, WorkflowMessages.ActivityCategoryHuman), WorkflowMessages.ActivityUserTaskName, WorkflowMessages.ActivityUserTaskDescription,
    [
        Input(UserTaskActivity.AssigneesProperty, "array"), Input(UserTaskActivity.AssigneesExpressionProperty, "expression"), Input(UserTaskActivity.AssigneeRulesProperty, "array"), Input(UserTaskActivity.ExcludeStarterProperty, "boolean"),
        Input(UserTaskActivity.EmptyAssigneeStrategyProperty, "string"), Input(UserTaskActivity.FallbackAssigneeRulesProperty, "array"), Input(UserTaskActivity.CompletionPolicyProperty, "string"),
        Input(UserTaskActivity.ApprovalThresholdProperty, "string", descriptionKey: WorkflowMessages.ActivityUserTaskApprovalThresholdInput), Input(UserTaskActivity.TitleProperty, "template"),
        Input(UserTaskActivity.FormDataProperty, "object"), Input(UserTaskActivity.FormKeyProperty, "template"), Input(UserTaskActivity.PriorityProperty, "number"),
        Input(UserTaskActivity.DueAfterSecondsProperty, "number"), Input(UserTaskActivity.DueAtProperty, "expression", descriptionKey: WorkflowMessages.ActivityUserTaskDueAtInput), Input(UserTaskActivity.ReminderAfterSecondsProperty, "number"),
        Input(UserTaskActivity.CcUserIdsProperty, "array"), Input(UserTaskActivity.CcRulesProperty, "array"), Input(UserTaskActivity.AllowedOutcomesProperty, "array"),
    ]);

    /// <summary>
    /// PublishEvent。
    /// </summary>
    public static ActivityDescriptor PublishEvent { get; } = Describe(ActivityTypes.PublishEvent, Category(IntegrationCategory, WorkflowMessages.ActivityCategoryIntegration), WorkflowMessages.ActivityPublishEventName, WorkflowMessages.ActivityPublishEventDescription, [Input(PublishEventActivity.EventNameProperty, "template", true), Input(PublishEventActivity.PayloadProperty, "object")]);

    /// <summary>
    /// WaitSignal。
    /// </summary>
    public static ActivityDescriptor WaitSignal { get; } = Describe(ActivityTypes.WaitSignal, Category(IntegrationCategory, WorkflowMessages.ActivityCategoryIntegration), WorkflowMessages.ActivityWaitSignalName, WorkflowMessages.ActivityWaitSignalDescription, [Input(WaitSignalActivity.SignalNameProperty, "template", true), Input(WaitSignalActivity.AcceptAnyCorrelationProperty, "boolean")]);

    /// <summary>
    /// EventGateway。
    /// </summary>
    public static ActivityDescriptor EventGateway { get; } = Describe(ActivityTypes.EventGateway, Category(IntegrationCategory, WorkflowMessages.ActivityCategoryIntegration), WorkflowMessages.ActivityEventGatewayName, WorkflowMessages.ActivityEventGatewayDescription, [Input(EventGatewayActivity.SignalsProperty, "array", true), Input(EventGatewayActivity.TimeoutSecondsProperty, "number"), Input(EventGatewayActivity.AcceptAnyCorrelationProperty, "boolean")]);

    /// <summary>
    /// SubWorkflow。
    /// </summary>
    public static ActivityDescriptor SubWorkflow { get; } = Describe(ActivityTypes.SubWorkflow, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivitySubWorkflowName, WorkflowMessages.ActivitySubWorkflowDescription,
    [
        Input(SubWorkflowActivity.DefinitionNameProperty, "template", true), Input(SubWorkflowActivity.DefinitionVersionProperty, "number"), Input(SubWorkflowActivity.VariablesProperty, "object"), Input(SubWorkflowActivity.VariableExpressionsProperty, "object"),
        Input(SubWorkflowActivity.WaitForCompletionProperty, "boolean"), Input(SubWorkflowActivity.ResultVariableProperty, "string"), Input(SubWorkflowActivity.FailOnChildFaultProperty, "boolean"),
    ]);

    /// <summary>
    /// ForEach。
    /// </summary>
    public static ActivityDescriptor ForEach { get; } = Describe(ActivityTypes.ForEach, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivityForEachName, WorkflowMessages.ActivityForEachDescription,
    [
        Input(ForEachActivity.ItemsProperty, "expression", true), Input(ForEachActivity.ItemVariableProperty, "string"), Input(SubWorkflowActivity.DefinitionNameProperty, "template", true), Input(ForEachActivity.ParallelProperty, "boolean"),
        Input(ForEachActivity.FailFastProperty, "boolean"), Input(SubWorkflowActivity.ResultVariableProperty, "string"),
    ]);

    /// <summary>
    /// Log。
    /// </summary>
    public static ActivityDescriptor Log { get; } = Describe(ActivityTypes.Log, Category(DataCategory, WorkflowMessages.ActivityCategoryData), WorkflowMessages.ActivityLogName, WorkflowMessages.ActivityLogDescription, [Input(LogActivity.MessageProperty, "template", true), Input(LogActivity.LevelProperty, "string")]);

    /// <summary>
    /// Fault。
    /// </summary>
    public static ActivityDescriptor Fault { get; } = Describe(ActivityTypes.Fault, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivityFaultName, WorkflowMessages.ActivityFaultDescription, [Input(FaultActivity.MessageProperty, "template")]);

    /// <summary>
    /// Compensate。
    /// </summary>
    public static ActivityDescriptor Compensate { get; } = Describe(ActivityTypes.Compensate, Category(ControlCategory, WorkflowMessages.ActivityCategoryControl), WorkflowMessages.ActivityCompensateName, WorkflowMessages.ActivityCompensateDescription, [Input(CompensateActivity.NodeIdsProperty, "array")]);

    /// <summary>
    /// Http。
    /// </summary>
    public static ActivityDescriptor Http { get; } = Describe(ActivityTypes.Http, Category(IntegrationCategory, WorkflowMessages.ActivityCategoryIntegration), WorkflowMessages.ActivityHttpName, WorkflowMessages.ActivityHttpDescription,
    [
        Input(HttpActivity.UrlProperty, "template", true), Input(HttpActivity.MethodProperty, "string"), Input(HttpActivity.HeadersProperty, "object"), Input(HttpActivity.BodyProperty, "template"),
        Input(HttpActivity.ContentTypeProperty, "string"), Input(HttpActivity.TimeoutSecondsProperty, "number"), Input(HttpActivity.ResultVariableProperty, "string"), Input(HttpActivity.FailOnErrorStatusProperty, "boolean"),
    ]);

    /// <summary>
    /// ExternalTask。
    /// </summary>
    public static ActivityDescriptor ExternalTask { get; } = Describe(ActivityTypes.ExternalTask, Category(IntegrationCategory, WorkflowMessages.ActivityCategoryIntegration), WorkflowMessages.ActivityExternalTaskName, WorkflowMessages.ActivityExternalTaskDescription, [Input(ExternalTaskActivity.TopicProperty, "template", true), Input(ExternalTaskActivity.PayloadProperty, "object")]);

    /// <summary>
    /// 描述：显示名、说明与分类的显示名都是消息键，没有本地化时显示名回退到活动类型名。
    /// </summary>
    private static ActivityDescriptor Describe(string type, (string Category, string Key) category, string nameKey, string descriptionKey, IReadOnlyList<ActivityInputDescriptor>? inputs = null)
        => new(type, category: category.Category, inputs: inputs) { DisplayNameKey = nameKey, DescriptionKey = descriptionKey, CategoryKey = category.Key };

    /// <summary>
    /// 分类与它显示名的消息键。
    /// </summary>
    private static (string Category, string Key) Category(string category, string key) => (category, key);

    /// <summary>
    /// 输入描述。
    /// </summary>
    private static ActivityInputDescriptor Input(string name, string kind, bool required = false, string? descriptionKey = null) => new(name, kind, required) { DescriptionKey = descriptionKey };
}
