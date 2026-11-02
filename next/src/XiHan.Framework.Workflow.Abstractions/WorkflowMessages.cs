// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 工作流子系统的消息键。
/// </summary>
/// <remarks>
/// 各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>（RFC-0010）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "workflow";

    /// <summary>
    /// 定义不合法。
    /// </summary>
    /// <remarks>
    /// 参数：<c>errors</c>。
    /// </remarks>
    public const string DefinitionInvalid = "workflow.definition.invalid";

    /// <summary>
    /// 定义不存在或没有已发布版本。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string DefinitionNotFound = "workflow.definition.not_found";

    /// <summary>
    /// 定义状态不允许该操作。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>。
    /// </remarks>
    public const string DefinitionStatus = "workflow.definition.status";

    /// <summary>
    /// 实例不存在。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string InstanceNotFound = "workflow.instance.not_found";

    /// <summary>
    /// 实例状态不允许该操作。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>。
    /// </remarks>
    public const string InstanceStatus = "workflow.instance.status";

    /// <summary>
    /// 子流程深度超限。
    /// </summary>
    /// <remarks>
    /// 参数：<c>depth</c>、<c>max</c>。
    /// </remarks>
    public const string InstanceDepth = "workflow.instance.depth";

    /// <summary>
    /// 一个波次执行的节点数超过上限。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>limit</c>。
    /// </remarks>
    public const string InstanceRunaway = "workflow.instance.runaway";

    /// <summary>
    /// 汇合节点等待的分支已死。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string InstanceJoinDead = "workflow.instance.join_dead";

    /// <summary>
    /// 节点没有可走的出边。
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>。
    /// </remarks>
    public const string InstanceNoTransition = "workflow.instance.no_transition";

    /// <summary>
    /// 缺少必填变量。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string VariableRequired = "workflow.variable.required";

    /// <summary>
    /// 书签不存在。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string BookmarkNotFound = "workflow.bookmark.not_found";

    /// <summary>
    /// 书签不可恢复。
    /// </summary>
    /// <remarks>
    /// 实例不在运行中。参数：<c>id</c>。
    /// </remarks>
    public const string BookmarkNotResumable = "workflow.bookmark.not_resumable";

    /// <summary>
    /// 书签的键与期望不符。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string BookmarkKeyMismatch = "workflow.bookmark.key_mismatch";

    /// <summary>
    /// 活动类型未登记。
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>。
    /// </remarks>
    public const string ActivityNotRegistered = "workflow.activity.not_registered";

    /// <summary>
    /// 活动抛出未处理异常。
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>、<c>reason</c>。
    /// </remarks>
    public const string ActivityFailed = "workflow.activity.failed";

    /// <summary>
    /// 活动属性不合法。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>reason</c>。
    /// </remarks>
    public const string ActivityPropertyInvalid = "workflow.activity.property_invalid";

    /// <summary>
    /// 表达式语法不合法或求值失败。
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>reason</c>。
    /// </remarks>
    public const string ExpressionInvalid = "workflow.expression.invalid";

    /// <summary>
    /// 操作者不是任务的指派人。
    /// </summary>
    /// <remarks>
    /// 参数：<c>task</c>、<c>actor</c>。
    /// </remarks>
    public const string TaskDenied = "workflow.task.denied";

    /// <summary>
    /// 任务结果不在允许范围。
    /// </summary>
    /// <remarks>
    /// 参数：<c>outcome</c>。
    /// </remarks>
    public const string TaskOutcomeInvalid = "workflow.task.outcome_invalid";

    /// <summary>
    /// 指派人已在任务上。
    /// </summary>
    /// <remarks>
    /// 参数：<c>assignee</c>。
    /// </remarks>
    public const string TaskAssigneeExists = "workflow.task.assignee_exists";

    /// <summary>
    /// 没有解析出任何指派人。
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>。
    /// </remarks>
    public const string TaskNoAssignee = "workflow.task.no_assignee";

    /// <summary>
    /// 指派规则类型未登记。
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>。
    /// </remarks>
    public const string AssigneeRuleUnknown = "workflow.assignee.rule_unknown";


    /// <summary>
    /// 实例锁等待超时。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string LockTimeout = "workflow.lock.timeout";

    /// <summary>
    /// 存储不可达。
    /// </summary>
    /// <remarks>
    /// 参数：<c>reason</c>。
    /// </remarks>
    public const string StoreUnavailable = "workflow.store.unavailable";

    /// <summary>
    /// 存储容量已满。
    /// </summary>
    /// <remarks>
    /// 参数：<c>store</c>、<c>limit</c>。
    /// </remarks>
    public const string StoreCapacity = "workflow.store.capacity";

    /// <summary>
    /// 子流程没有正常完成。
    /// </summary>
    /// <remarks>
    /// 没起来或以故障、取消、终止结束；子实例的故障是内层原因。参数：<c>id</c>（子实例）、<c>status</c>（子实例的状态）。
    /// </remarks>
    public const string ChildFailed = "workflow.child.failed";

    /// <summary>
    /// 同一关联 ID 下已有未结束的实例。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>correlation</c>。
    /// </remarks>
    public const string ActiveInstanceExists = "workflow.instance.active_exists";

    /// <summary>
    /// 跳转目标节点不存在。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>node</c>。
    /// </remarks>
    public const string NodeNotFound = "workflow.instance.node_not_found";

    /// <summary>
    /// 迁移目标与未结束的节点不兼容。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>node</c>。
    /// </remarks>
    public const string MigrationIncompatible = "workflow.instance.migration_incompatible";

    /// <summary>
    /// 实例级超时到期。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string InstanceTimedOut = "workflow.instance.timed_out";

    /// <summary>
    /// 任务已被别人认领。
    /// </summary>
    /// <remarks>
    /// 参数：<c>task</c>、<c>actor</c>。
    /// </remarks>
    public const string TaskClaimed = "workflow.task.claimed";

    /// <summary>
    /// 任务的完成策略不支持认领。
    /// </summary>
    /// <remarks>
    /// 参数：<c>task</c>。
    /// </remarks>
    public const string TaskNotClaimable = "workflow.task.not_claimable";

    /// <summary>
    /// 找不到可退回的节点。
    /// </summary>
    /// <remarks>
    /// 参数：<c>task</c>。
    /// </remarks>
    public const string TaskNoReturnTarget = "workflow.task.no_return_target";


    /// <summary>
    /// 待办查询的分页越界。
    /// </summary>
    /// <remarks>
    /// 参数：<c>skip</c>、<c>limit</c>、<c>max</c>。
    /// </remarks>
    public const string TaskQueryPageInvalid = "workflow.task.query_page_invalid";

    /// <summary>
    /// 实例查询的分页越界。
    /// </summary>
    /// <remarks>
    /// 参数：<c>skip</c>、<c>limit</c>、<c>max</c>。
    /// </remarks>
    public const string InstanceQueryPageInvalid = "workflow.instance.query_page_invalid";

    /// <summary>
    /// 实例查询的关联 ID 集合越界。
    /// </summary>
    /// <remarks>
    /// 超过上限或有空白的 ID。参数：<c>count</c>（给出的个数）、<c>max</c>。
    /// </remarks>
    public const string InstanceQueryCorrelationsInvalid = "workflow.instance.query_correlations_invalid";

    /// <summary>
    /// 定义查询的分页越界。
    /// </summary>
    /// <remarks>
    /// 参数：<c>skip</c>、<c>limit</c>、<c>max</c>。
    /// </remarks>
    public const string DefinitionQueryPageInvalid = "workflow.definition.query_page_invalid";

    /// <summary>
    /// 定义查询的排序字段不在白名单里。
    /// </summary>
    /// <remarks>
    /// 参数：<c>sort</c>（给出的值）。
    /// </remarks>
    public const string DefinitionQuerySortInvalid = "workflow.definition.query_sort_invalid";

    /// <summary>
    /// 定义查询的同一排序字段出现了两次。
    /// </summary>
    /// <remarks>
    /// 参数：<c>sort</c>（重复的字段）。
    /// </remarks>
    public const string DefinitionQuerySortDuplicate = "workflow.definition.query_sort_duplicate";

    /// <summary>
    /// 定义查询的排序项超过上限。
    /// </summary>
    /// <remarks>
    /// 参数：<c>count</c>（给出的项数）、<c>max</c>。
    /// </remarks>
    public const string DefinitionQuerySortsTooMany = "workflow.definition.query_sorts_too_many";

    /// <summary>
    /// 实例查询的排序字段不在白名单里。
    /// </summary>
    /// <remarks>
    /// 参数：<c>sort</c>（给出的值）。
    /// </remarks>
    public const string InstanceQuerySortInvalid = "workflow.instance.query_sort_invalid";

    /// <summary>
    /// 实例查询的同一排序字段出现了两次。
    /// </summary>
    /// <remarks>
    /// 参数：<c>sort</c>（重复的字段）。
    /// </remarks>
    public const string InstanceQuerySortDuplicate = "workflow.instance.query_sort_duplicate";

    /// <summary>
    /// 实例查询的排序项超过上限。
    /// </summary>
    /// <remarks>
    /// 参数：<c>count</c>（给出的项数）、<c>max</c>。
    /// </remarks>
    public const string InstanceQuerySortsTooMany = "workflow.instance.query_sorts_too_many";

    /// <summary>
    /// 清理一次删除的条数越界。
    /// </summary>
    /// <remarks>
    /// 参数：<c>limit</c>、<c>max</c>。
    /// </remarks>
    public const string PurgeLimitInvalid = "workflow.purge.limit_invalid";

    /// <summary>
    /// 工作者没有持有外部任务的锁。
    /// </summary>
    /// <remarks>
    /// 参数：<c>task</c>、<c>worker</c>。
    /// </remarks>
    public const string ExternalTaskDenied = "workflow.external.denied";

    /// <summary>
    /// 外部任务由工作者报告失败。
    /// </summary>
    /// <remarks>
    /// 参数：<c>reason</c>。
    /// </remarks>
    public const string ExternalTaskFailed = "workflow.external.failed";

    /// <summary>
    /// HTTP 请求失败。
    /// </summary>
    /// <remarks>
    /// 参数：<c>status</c>、<c>reason</c>。
    /// </remarks>
    public const string HttpFailed = "workflow.http.failed";

    /// <summary>
    /// HTTP 请求超时。
    /// </summary>
    public const string HttpTimedOut = "workflow.http.timed_out";

    /// <summary>
    /// 故障与它的内层原因连成一句。
    /// </summary>
    /// <remarks>
    /// 参数：<c>message</c>（外层的文案）、<c>cause</c>（内层的文案）。
    /// </remarks>
    public const string FaultCausedBy = "workflow.fault.caused_by";

    /// <summary>
    /// 节点超时。
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>。
    /// </remarks>
    public const string NodeTimedOut = "workflow.node.timed_out";

    /// <summary>
    /// 活动没有可运行的调用。
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>。
    /// </remarks>
    public const string ActivityNoInvocation = "workflow.activity.no_invocation";

    /// <summary>
    /// <c>Fault</c> 节点让流程故障。
    /// </summary>
    /// <remarks>
    /// 参数：<c>message</c>（节点作者写的消息，原样）。
    /// </remarks>
    public const string ActivityFaultRaised = "workflow.activity.fault_raised";

    /// <summary>
    /// 活动属性必须是正的秒数。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string PropertyNotSeconds = "workflow.activity.property_not_seconds";

    /// <summary>
    /// 活动属性必须是日期时间。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string PropertyNotDateTime = "workflow.activity.property_not_datetime";

    /// <summary>
    /// 活动属性不能为空。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string PropertyEmpty = "workflow.activity.property_empty";

    /// <summary>
    /// 活动属性的值不是 cron 表达式。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>。
    /// </remarks>
    public const string PropertyNotCron = "workflow.activity.property_not_cron";

    /// <summary>
    /// 活动属性的值不是时区。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>。
    /// </remarks>
    public const string PropertyNotTimeZone = "workflow.activity.property_not_time_zone";

    /// <summary>
    /// cron 表达式五年内没有下一次触发。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>。
    /// </remarks>
    public const string PropertyCronExhausted = "workflow.activity.property_cron_exhausted";

    /// <summary>
    /// 活动属性至少需要一个信号名。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string PropertyNoSignal = "workflow.activity.property_no_signal";

    /// <summary>
    /// 实例没有关联 ID，又没有允许任意关联。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>（允许任意关联的属性）。
    /// </remarks>
    public const string PropertyNeedsCorrelation = "workflow.activity.property_needs_correlation";

    /// <summary>
    /// 活动属性的求值结果必须是数组。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string PropertyNotArray = "workflow.activity.property_not_array";

    /// <summary>
    /// 活动属性的值不是绝对 HTTP URL。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>。
    /// </remarks>
    public const string PropertyNotUrl = "workflow.activity.property_not_url";

    /// <summary>
    /// 活动属性的值不是支持的 HTTP 方法。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>。
    /// </remarks>
    public const string PropertyNotHttpMethod = "workflow.activity.property_not_http_method";

    /// <summary>
    /// 活动属性里的请求头不合法。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>（请求头名）。
    /// </remarks>
    public const string PropertyHeaderInvalid = "workflow.activity.property_header_invalid";

    /// <summary>
    /// 活动属性的值不是媒体类型。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>。
    /// </remarks>
    public const string PropertyNotMediaType = "workflow.activity.property_not_media_type";

    /// <summary>
    /// 活动属性里的项不是表达式。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>（项名）。
    /// </remarks>
    public const string PropertyNotExpression = "workflow.activity.property_not_expression";

    /// <summary>
    /// 活动属性的值不是工作流名称。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>。
    /// </remarks>
    public const string PropertyNotWorkflowName = "workflow.activity.property_not_workflow_name";

    /// <summary>
    /// 活动属性的值不在允许的取值里。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>。
    /// </remarks>
    public const string PropertyUnknownValue = "workflow.activity.property_unknown_value";

    /// <summary>
    /// 活动属性必须是正整数或百分比。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string PropertyNotThreshold = "workflow.activity.property_not_threshold";

    /// <summary>
    /// 活动属性必须是整数。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string PropertyNotInteger = "workflow.activity.property_not_integer";

    /// <summary>
    /// 指派规则必须是数组。
    /// </summary>
    public const string AssigneeRulesNotArray = "workflow.assignee.rules_not_array";

    /// <summary>
    /// 指派规则缺少类型。
    /// </summary>
    public const string AssigneeRuleTypeMissing = "workflow.assignee.rule_type_missing";

    /// <summary>
    /// 指派规则需要恰好一个表达式。
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>。
    /// </remarks>
    public const string AssigneeExpressionCount = "workflow.assignee.expression_count";

    /// <summary>
    /// 指派规则至少需要一个用户。
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>。
    /// </remarks>
    public const string AssigneeUsersMissing = "workflow.assignee.users_missing";

    /// <summary>
    /// 表达式的结果不是布尔值。
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>。
    /// </remarks>
    public const string ExpressionNotBoolean = "workflow.expression.not_boolean";

    /// <summary>
    /// 模板里有未闭合的占位符。
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>（模板）。
    /// </remarks>
    public const string ExpressionUnterminatedPlaceholder = "workflow.expression.unterminated_placeholder";

    /// <summary>
    /// 定义没有开始节点。
    /// </summary>
    public const string DefinitionStartMissing = "workflow.definition.start_missing";

    /// <summary>
    /// 工作流自己开的调用没有可加入的工作单元。
    /// </summary>
    /// <remarks>
    /// 装配错误：持久化没有在 Transaction 阶段开启工作单元。
    /// </remarks>
    public const string StoreUnitOfWorkMissing = "workflow.store.unit_of_work_missing";

    /// <summary>
    /// 待办查询既没给指派人也没给实例。
    /// </summary>
    public const string TaskQueryTargetMissing = "workflow.task.query_target_missing";

    /// <summary>
    /// 外部任务的锁定时长不是正数。
    /// </summary>
    public const string ExternalTaskLockDurationInvalid = "workflow.external.lock_duration_invalid";

    /// <summary>
    /// 一次拉取的外部任务数越界。
    /// </summary>
    /// <remarks>
    /// 参数：<c>count</c>（给出的个数）、<c>max</c>。
    /// </remarks>
    public const string ExternalTaskMaxTasksInvalid = "workflow.external.max_tasks_invalid";

    /// <summary>
    /// 外部任务的重试延迟为负。
    /// </summary>
    public const string ExternalTaskRetryAfterNegative = "workflow.external.retry_after_negative";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(WorkflowMessages));
}
