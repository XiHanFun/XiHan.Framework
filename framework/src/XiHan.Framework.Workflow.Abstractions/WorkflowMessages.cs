// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 工作流子系统的消息键
/// </summary>
/// <remarks>
/// 各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>（RFC-0010）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowMessages
{
    /// <summary>
    /// 目录名
    /// </summary>
    public const string CatalogName = "workflow";

    /// <summary>
    /// 定义不合法
    /// </summary>
    /// <remarks>
    /// 参数：<c>count</c>（问题条数）；<c>details.errors</c> 是全部问题（<c>SchemaViolation</c> 的列表，每条有自己的消息键与参数，ADR-0106）
    /// </remarks>
    public const string DefinitionInvalid = "workflow.definition.invalid";

    /// <summary>
    /// 定义不存在或没有已发布版本
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>
    /// </remarks>
    public const string DefinitionNotFound = "workflow.definition.not_found";


    /// <summary>
    /// 实例不存在
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>
    /// </remarks>
    public const string InstanceNotFound = "workflow.instance.not_found";


    /// <summary>
    /// 子流程深度超限
    /// </summary>
    /// <remarks>
    /// 参数：<c>depth</c>、<c>max</c>
    /// </remarks>
    public const string InstanceDepth = "workflow.instance.depth";

    /// <summary>
    /// 一个波次执行的节点数超过上限
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>limit</c>
    /// </remarks>
    public const string InstanceRunaway = "workflow.instance.runaway";

    /// <summary>
    /// 汇合节点等待的分支已死
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>
    /// </remarks>
    public const string InstanceJoinDead = "workflow.instance.join_dead";

    /// <summary>
    /// 节点没有可走的出边
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>
    /// </remarks>
    public const string InstanceNoTransition = "workflow.instance.no_transition";

    /// <summary>
    /// 缺少必填变量
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>
    /// </remarks>
    public const string VariableRequired = "workflow.variable.required";

    /// <summary>
    /// 书签不存在
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>
    /// </remarks>
    public const string BookmarkNotFound = "workflow.bookmark.not_found";

    /// <summary>
    /// 书签不可恢复
    /// </summary>
    /// <remarks>
    /// 实例不在运行中。参数：<c>id</c>
    /// </remarks>
    public const string BookmarkNotResumable = "workflow.bookmark.not_resumable";

    /// <summary>
    /// 书签的键与期望不符
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>
    /// </remarks>
    public const string BookmarkKeyMismatch = "workflow.bookmark.key_mismatch";

    /// <summary>
    /// 活动类型未登记
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>
    /// </remarks>
    public const string ActivityNotRegistered = "workflow.activity.not_registered";

    /// <summary>
    /// 活动抛出未处理异常
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>、<c>reason</c>
    /// </remarks>
    public const string ActivityFailed = "workflow.activity.failed";

    /// <summary>
    /// 活动属性不合法
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>reason</c>
    /// </remarks>
    public const string ActivityPropertyInvalid = "workflow.activity.property_invalid";


    /// <summary>
    /// 操作者不是任务的指派人
    /// </summary>
    /// <remarks>
    /// 参数：<c>task</c>、<c>actor</c>
    /// </remarks>
    public const string TaskDenied = "workflow.task.denied";

    /// <summary>
    /// 任务结果不在允许范围
    /// </summary>
    /// <remarks>
    /// 参数：<c>outcome</c>
    /// </remarks>
    public const string TaskOutcomeInvalid = "workflow.task.outcome_invalid";

    /// <summary>
    /// 指派人已在任务上
    /// </summary>
    /// <remarks>
    /// 参数：<c>assignee</c>
    /// </remarks>
    public const string TaskAssigneeExists = "workflow.task.assignee_exists";

    /// <summary>
    /// 没有解析出任何指派人
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>
    /// </remarks>
    public const string TaskNoAssignee = "workflow.task.no_assignee";

    /// <summary>
    /// 指派规则类型未登记
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>
    /// </remarks>
    public const string AssigneeRuleUnknown = "workflow.assignee.rule_unknown";


    /// <summary>
    /// 实例锁等待超时
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>
    /// </remarks>
    public const string LockTimeout = "workflow.lock.timeout";

    /// <summary>
    /// 存储不可达
    /// </summary>
    /// <remarks>
    /// 参数：<c>reason</c>
    /// </remarks>
    public const string StoreUnavailable = "workflow.store.unavailable";

    /// <summary>
    /// 存储容量已满
    /// </summary>
    /// <remarks>
    /// 参数：<c>store</c>、<c>limit</c>
    /// </remarks>
    public const string StoreCapacity = "workflow.store.capacity";

    /// <summary>
    /// 子流程以意外的状态结束
    /// </summary>
    /// <remarks>
    /// 故障、取消、终止各有键（<see cref="ChildFaulted"/>、<see cref="ChildCanceled"/>、<see cref="ChildTerminated"/>）；这个键只用于手工恢复书签给了别的状态。参数：<c>id</c>（子实例）、<c>status</c>（原样）
    /// </remarks>
    public const string ChildFailed = "workflow.child.failed";

    /// <summary>
    /// 同一关联 ID 下已有未结束的实例
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>correlation</c>
    /// </remarks>
    public const string ActiveInstanceExists = "workflow.instance.active_exists";

    /// <summary>
    /// 跳转目标节点不存在
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>node</c>
    /// </remarks>
    public const string NodeNotFound = "workflow.instance.node_not_found";

    /// <summary>
    /// 迁移目标与未结束的节点不兼容
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>node</c>
    /// </remarks>
    public const string MigrationIncompatible = "workflow.instance.migration_incompatible";

    /// <summary>
    /// 实例级超时到期
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>
    /// </remarks>
    public const string InstanceTimedOut = "workflow.instance.timed_out";

    /// <summary>
    /// 任务已被别人认领
    /// </summary>
    /// <remarks>
    /// 参数：<c>task</c>、<c>actor</c>
    /// </remarks>
    public const string TaskClaimed = "workflow.task.claimed";

    /// <summary>
    /// 任务的完成策略不支持认领
    /// </summary>
    /// <remarks>
    /// 参数：<c>task</c>
    /// </remarks>
    public const string TaskNotClaimable = "workflow.task.not_claimable";

    /// <summary>
    /// 找不到可退回的节点
    /// </summary>
    /// <remarks>
    /// 参数：<c>task</c>
    /// </remarks>
    public const string TaskNoReturnTarget = "workflow.task.no_return_target";


    /// <summary>
    /// 待办查询的分页越界
    /// </summary>
    /// <remarks>
    /// 参数：<c>skip</c>、<c>limit</c>、<c>max</c>
    /// </remarks>
    public const string TaskQueryPageInvalid = "workflow.task.query_page_invalid";

    /// <summary>
    /// 实例查询的分页越界
    /// </summary>
    /// <remarks>
    /// 参数：<c>skip</c>、<c>limit</c>、<c>max</c>
    /// </remarks>
    public const string InstanceQueryPageInvalid = "workflow.instance.query_page_invalid";

    /// <summary>
    /// 实例查询的关联 ID 集合越界
    /// </summary>
    /// <remarks>
    /// 超过上限或有空白的 ID。参数：<c>count</c>（给出的个数）、<c>max</c>
    /// </remarks>
    public const string InstanceQueryCorrelationsInvalid = "workflow.instance.query_correlations_invalid";

    /// <summary>
    /// 定义查询的分页越界
    /// </summary>
    /// <remarks>
    /// 参数：<c>skip</c>、<c>limit</c>、<c>max</c>
    /// </remarks>
    public const string DefinitionQueryPageInvalid = "workflow.definition.query_page_invalid";

    /// <summary>
    /// 定义查询的排序字段不在白名单里
    /// </summary>
    /// <remarks>
    /// 参数：<c>sort</c>（给出的值）
    /// </remarks>
    public const string DefinitionQuerySortInvalid = "workflow.definition.query_sort_invalid";

    /// <summary>
    /// 定义查询的同一排序字段出现了两次
    /// </summary>
    /// <remarks>
    /// 参数：<c>sort</c>（重复的字段）
    /// </remarks>
    public const string DefinitionQuerySortDuplicate = "workflow.definition.query_sort_duplicate";

    /// <summary>
    /// 定义查询的排序项超过上限
    /// </summary>
    /// <remarks>
    /// 参数：<c>count</c>（给出的项数）、<c>max</c>
    /// </remarks>
    public const string DefinitionQuerySortsTooMany = "workflow.definition.query_sorts_too_many";

    /// <summary>
    /// 实例查询的排序字段不在白名单里
    /// </summary>
    /// <remarks>
    /// 参数：<c>sort</c>（给出的值）
    /// </remarks>
    public const string InstanceQuerySortInvalid = "workflow.instance.query_sort_invalid";

    /// <summary>
    /// 实例查询的同一排序字段出现了两次
    /// </summary>
    /// <remarks>
    /// 参数：<c>sort</c>（重复的字段）
    /// </remarks>
    public const string InstanceQuerySortDuplicate = "workflow.instance.query_sort_duplicate";

    /// <summary>
    /// 实例查询的排序项超过上限
    /// </summary>
    /// <remarks>
    /// 参数：<c>count</c>（给出的项数）、<c>max</c>
    /// </remarks>
    public const string InstanceQuerySortsTooMany = "workflow.instance.query_sorts_too_many";

    /// <summary>
    /// 清理一次删除的条数越界
    /// </summary>
    /// <remarks>
    /// 参数：<c>limit</c>、<c>max</c>
    /// </remarks>
    public const string PurgeLimitInvalid = "workflow.purge.limit_invalid";

    /// <summary>
    /// 工作者没有持有外部任务的锁
    /// </summary>
    /// <remarks>
    /// 参数：<c>task</c>、<c>worker</c>
    /// </remarks>
    public const string ExternalTaskDenied = "workflow.external.denied";

    /// <summary>
    /// 外部任务由工作者报告失败
    /// </summary>
    /// <remarks>
    /// 参数：<c>reason</c>
    /// </remarks>
    public const string ExternalTaskFailed = "workflow.external.failed";

    /// <summary>
    /// HTTP 请求失败
    /// </summary>
    /// <remarks>
    /// 参数：<c>status</c>、<c>reason</c>
    /// </remarks>
    public const string HttpFailed = "workflow.http.failed";

    /// <summary>
    /// HTTP 请求超时
    /// </summary>
    public const string HttpTimedOut = "workflow.http.timed_out";

    /// <summary>
    /// HTTP 请求的目标被出站地址策略拒绝
    /// </summary>
    /// <remarks>
    /// 参数：<c>host</c>（请求里的主机名）
    /// </remarks>
    public const string HttpAddressDenied = "workflow.http.address_denied";

    /// <summary>
    /// 故障与它的内层原因连成一句
    /// </summary>
    /// <remarks>
    /// 参数：<c>message</c>（外层的文案）、<c>cause</c>（内层的文案）
    /// </remarks>
    public const string FaultCausedBy = "workflow.fault.caused_by";

    /// <summary>
    /// 节点超时
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>
    /// </remarks>
    public const string NodeTimedOut = "workflow.node.timed_out";

    /// <summary>
    /// 活动没有可运行的调用
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>
    /// </remarks>
    public const string ActivityNoInvocation = "workflow.activity.no_invocation";

    /// <summary>
    /// <c>Fault</c> 节点让流程故障
    /// </summary>
    /// <remarks>
    /// 参数：<c>message</c>（节点作者写的消息，原样）
    /// </remarks>
    public const string ActivityFaultRaised = "workflow.activity.fault_raised";

    /// <summary>
    /// 活动属性必须是正的秒数
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>
    /// </remarks>
    public const string PropertyNotSeconds = "workflow.activity.property_not_seconds";

    /// <summary>
    /// 活动属性必须是日期时间
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>
    /// </remarks>
    public const string PropertyNotDateTime = "workflow.activity.property_not_datetime";

    /// <summary>
    /// 活动属性不能为空
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>
    /// </remarks>
    public const string PropertyEmpty = "workflow.activity.property_empty";

    /// <summary>
    /// 活动属性的值不是 cron 表达式
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>
    /// </remarks>
    public const string PropertyNotCron = "workflow.activity.property_not_cron";

    /// <summary>
    /// 活动属性的值不是时区
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>
    /// </remarks>
    public const string PropertyNotTimeZone = "workflow.activity.property_not_time_zone";

    /// <summary>
    /// cron 表达式五年内没有下一次触发
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>
    /// </remarks>
    public const string PropertyCronExhausted = "workflow.activity.property_cron_exhausted";

    /// <summary>
    /// 活动属性至少需要一个信号名
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>
    /// </remarks>
    public const string PropertyNoSignal = "workflow.activity.property_no_signal";

    /// <summary>
    /// 实例没有关联 ID，又没有允许任意关联
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>（允许任意关联的属性）
    /// </remarks>
    public const string PropertyNeedsCorrelation = "workflow.activity.property_needs_correlation";

    /// <summary>
    /// 活动属性的求值结果必须是数组
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>
    /// </remarks>
    public const string PropertyNotArray = "workflow.activity.property_not_array";

    /// <summary>
    /// 活动属性的值不是绝对 HTTP URL
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>
    /// </remarks>
    public const string PropertyNotUrl = "workflow.activity.property_not_url";

    /// <summary>
    /// 活动属性的值不是支持的 HTTP 方法
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>
    /// </remarks>
    public const string PropertyNotHttpMethod = "workflow.activity.property_not_http_method";

    /// <summary>
    /// 活动属性里的请求头不合法
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>（请求头名）
    /// </remarks>
    public const string PropertyHeaderInvalid = "workflow.activity.property_header_invalid";

    /// <summary>
    /// 活动属性的值不是媒体类型
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>
    /// </remarks>
    public const string PropertyNotMediaType = "workflow.activity.property_not_media_type";

    /// <summary>
    /// 活动属性里的项不是表达式
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>（项名）
    /// </remarks>
    public const string PropertyNotExpression = "workflow.activity.property_not_expression";

    /// <summary>
    /// 活动属性的值不是工作流名称
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>
    /// </remarks>
    public const string PropertyNotWorkflowName = "workflow.activity.property_not_workflow_name";

    /// <summary>
    /// 活动属性的值不在允许的取值里
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>value</c>
    /// </remarks>
    public const string PropertyUnknownValue = "workflow.activity.property_unknown_value";

    /// <summary>
    /// 活动属性必须是正整数或百分比
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>
    /// </remarks>
    public const string PropertyNotThreshold = "workflow.activity.property_not_threshold";

    /// <summary>
    /// 活动属性必须是整数
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>
    /// </remarks>
    public const string PropertyNotInteger = "workflow.activity.property_not_integer";

    /// <summary>
    /// 指派规则必须是数组
    /// </summary>
    public const string AssigneeRulesNotArray = "workflow.assignee.rules_not_array";

    /// <summary>
    /// 指派规则缺少类型
    /// </summary>
    public const string AssigneeRuleTypeMissing = "workflow.assignee.rule_type_missing";

    /// <summary>
    /// 指派规则需要恰好一个表达式
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>
    /// </remarks>
    public const string AssigneeExpressionCount = "workflow.assignee.expression_count";

    /// <summary>
    /// 指派规则至少需要一个用户
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>
    /// </remarks>
    public const string AssigneeUsersMissing = "workflow.assignee.users_missing";

    /// <summary>
    /// 表达式的结果不是布尔值
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>
    /// </remarks>
    public const string ExpressionNotBoolean = "workflow.expression.not_boolean";

    /// <summary>
    /// 模板里有未闭合的占位符
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>（模板）
    /// </remarks>
    public const string ExpressionUnterminatedPlaceholder = "workflow.expression.unterminated_placeholder";

    /// <summary>
    /// 定义没有开始节点
    /// </summary>
    public const string DefinitionStartMissing = "workflow.definition.start_missing";

    /// <summary>
    /// 工作流自己开的调用没有可加入的工作单元
    /// </summary>
    /// <remarks>
    /// 装配错误：持久化没有在 Transaction 阶段开启工作单元
    /// </remarks>
    public const string StoreUnitOfWorkMissing = "workflow.store.unit_of_work_missing";

    /// <summary>
    /// 待办查询既没给指派人也没给实例
    /// </summary>
    public const string TaskQueryTargetMissing = "workflow.task.query_target_missing";

    /// <summary>
    /// 外部任务的锁定时长不是正数
    /// </summary>
    public const string ExternalTaskLockDurationInvalid = "workflow.external.lock_duration_invalid";

    /// <summary>
    /// 一次拉取的外部任务数越界
    /// </summary>
    /// <remarks>
    /// 参数：<c>count</c>（给出的个数）、<c>max</c>
    /// </remarks>
    public const string ExternalTaskMaxTasksInvalid = "workflow.external.max_tasks_invalid";

    /// <summary>
    /// 外部任务的重试延迟为负
    /// </summary>
    public const string ExternalTaskRetryAfterNegative = "workflow.external.retry_after_negative";

    /// <summary>
    /// 定义没有节点
    /// </summary>
    public const string DefinitionNoNodes = "workflow.definition.no_nodes";

    /// <summary>
    /// 节点 ID 重复
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>
    /// </remarks>
    public const string DefinitionNodeDuplicate = "workflow.definition.node_duplicate";

    /// <summary>
    /// 节点的活动类型没有登记
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>、<c>type</c>
    /// </remarks>
    public const string DefinitionActivityUnknown = "workflow.definition.activity_unknown";

    /// <summary>
    /// 定义有不止一个开始节点
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>（多出来的开始节点）
    /// </remarks>
    public const string DefinitionStartDuplicate = "workflow.definition.start_duplicate";

    /// <summary>
    /// 输出映射的变量名为空
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>
    /// </remarks>
    public const string DefinitionMappingVariableEmpty = "workflow.definition.mapping_variable_empty";

    /// <summary>
    /// 转移 ID 重复
    /// </summary>
    /// <remarks>
    /// 参数：<c>transition</c>
    /// </remarks>
    public const string DefinitionTransitionDuplicate = "workflow.definition.transition_duplicate";

    /// <summary>
    /// 转移的源节点不存在
    /// </summary>
    /// <remarks>
    /// 参数：<c>transition</c>、<c>node</c>
    /// </remarks>
    public const string DefinitionTransitionSourceMissing = "workflow.definition.transition_source_missing";

    /// <summary>
    /// 转移的目标节点不存在
    /// </summary>
    /// <remarks>
    /// 参数：<c>transition</c>、<c>node</c>
    /// </remarks>
    public const string DefinitionTransitionTargetMissing = "workflow.definition.transition_target_missing";

    /// <summary>
    /// 转移指向开始节点
    /// </summary>
    /// <remarks>
    /// 参数：<c>transition</c>、<c>node</c>（开始节点）
    /// </remarks>
    public const string DefinitionStartIncoming = "workflow.definition.start_incoming";

    /// <summary>
    /// 节点有不止一条默认出边
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>
    /// </remarks>
    public const string DefinitionDefaultDuplicate = "workflow.definition.default_duplicate";

    /// <summary>
    /// 节点从开始节点走不到
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>
    /// </remarks>
    public const string DefinitionNodeUnreachable = "workflow.definition.node_unreachable";

    /// <summary>
    /// 用户任务没有任何指派来源
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>
    /// </remarks>
    public const string DefinitionUserTaskNoAssignees = "workflow.definition.user_task_no_assignees";

    /// <summary>
    /// 兜底策略缺兜底规则
    /// </summary>
    /// <remarks>
    /// 参数：<c>node</c>
    /// </remarks>
    public const string DefinitionFallbackRulesMissing = "workflow.definition.fallback_rules_missing";

    /// <summary>
    /// 定义 JSON 为空值
    /// </summary>
    public const string DefinitionJsonNull = "workflow.definition.json_null";

    /// <summary>
    /// 定义 JSON 语法或类型不合法
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>、<c>line</c>、<c>position</c>（行与字节位置从 1 起）
    /// </remarks>
    public const string DefinitionJsonMalformed = "workflow.definition.json_malformed";


    /// <summary>
    /// 表达式为空
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>
    /// </remarks>
    public const string ExpressionEmpty = "workflow.expression.empty";

    /// <summary>
    /// 表达式超过长度上限
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>（截断的）、<c>length</c>、<c>max</c>
    /// </remarks>
    public const string ExpressionTooLong = "workflow.expression.too_long";

    /// <summary>
    /// 表达式超过嵌套上限
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>（截断的）、<c>max</c>
    /// </remarks>
    public const string ExpressionTooDeep = "workflow.expression.too_deep";

    /// <summary>
    /// 表达式里出现意外的字符
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>character</c>、<c>position</c>（从 1 起）
    /// </remarks>
    public const string ExpressionUnexpectedCharacter = "workflow.expression.unexpected_character";

    /// <summary>
    /// 表达式除以零
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>
    /// </remarks>
    public const string ExpressionDivisionByZero = "workflow.expression.division_by_zero";

    /// <summary>
    /// 表达式在该处应有成员名
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>position</c>（从 1 起）
    /// </remarks>
    public const string ExpressionMemberExpected = "workflow.expression.member_expected";

    /// <summary>
    /// 表达式在该处缺少符号
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>token</c>、<c>position</c>（从 1 起）
    /// </remarks>
    public const string ExpressionTokenExpected = "workflow.expression.token_expected";

    /// <summary>
    /// 表达式意外结束
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>
    /// </remarks>
    public const string ExpressionUnexpectedEnd = "workflow.expression.unexpected_end";

    /// <summary>
    /// 表达式引用了未知变量
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>name</c>
    /// </remarks>
    public const string ExpressionUnknownVariable = "workflow.expression.unknown_variable";

    /// <summary>
    /// 函数的参数个数不对（固定个数）
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>function</c>、<c>count</c>
    /// </remarks>
    public const string ExpressionArgumentCount = "workflow.expression.argument_count";

    /// <summary>
    /// 函数的参数个数不对（范围）
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>function</c>、<c>min</c>、<c>max</c>
    /// </remarks>
    public const string ExpressionArgumentRange = "workflow.expression.argument_range";

    /// <summary>
    /// 值不是日期
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>value</c>
    /// </remarks>
    public const string ExpressionNotDate = "workflow.expression.not_date";

    /// <summary>
    /// 函数未知
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>function</c>
    /// </remarks>
    public const string ExpressionUnknownFunction = "workflow.expression.unknown_function";

    /// <summary>
    /// 从非对象值读成员
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>member</c>
    /// </remarks>
    public const string ExpressionMemberOfNonObject = "workflow.expression.member_of_non_object";

    /// <summary>
    /// 下标越界
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>index</c>、<c>length</c>
    /// </remarks>
    public const string ExpressionIndexOutOfRange = "workflow.expression.index_out_of_range";

    /// <summary>
    /// 对标量取下标
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>
    /// </remarks>
    public const string ExpressionIndexOfScalar = "workflow.expression.index_of_scalar";

    /// <summary>
    /// 运算符需要两个标量操作数
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>operator</c>
    /// </remarks>
    public const string ExpressionOperandsNotScalar = "workflow.expression.operands_not_scalar";

    /// <summary>
    /// 运算符需要布尔操作数
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>operator</c>
    /// </remarks>
    public const string ExpressionOperandsNotBoolean = "workflow.expression.operands_not_boolean";

    /// <summary>
    /// 运算符需要数值操作数
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>operator</c>
    /// </remarks>
    public const string ExpressionOperandsNotNumeric = "workflow.expression.operands_not_numeric";

    /// <summary>
    /// 值不是数字
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>、<c>value</c>
    /// </remarks>
    public const string ExpressionNotNumber = "workflow.expression.not_number";

    /// <summary>
    /// 字符串字面量没闭合
    /// </summary>
    /// <remarks>
    /// 参数：<c>expression</c>
    /// </remarks>
    public const string ExpressionUnterminatedString = "workflow.expression.unterminated_string";

    /// <summary>
    /// 子流程故障
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>（子实例）、<c>status</c>（枚举名）；子实例的故障是内层原因
    /// </remarks>
    public const string ChildFaulted = "workflow.child.faulted";

    /// <summary>
    /// 子流程被取消
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>（子实例）、<c>status</c>（枚举名）
    /// </remarks>
    public const string ChildCanceled = "workflow.child.canceled";

    /// <summary>
    /// 子流程被终止
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>（子实例）、<c>status</c>（枚举名）
    /// </remarks>
    public const string ChildTerminated = "workflow.child.terminated";

    /// <summary>
    /// 实例正在运行，不允许该操作
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>（枚举名）
    /// </remarks>
    public const string InstanceIsRunning = "workflow.instance.is_running";

    /// <summary>
    /// 实例已挂起，不允许该操作
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>（枚举名）
    /// </remarks>
    public const string InstanceIsSuspended = "workflow.instance.is_suspended";

    /// <summary>
    /// 实例已完成，不允许该操作
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>（枚举名）
    /// </remarks>
    public const string InstanceIsCompleted = "workflow.instance.is_completed";

    /// <summary>
    /// 实例已取消，不允许该操作
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>（枚举名）
    /// </remarks>
    public const string InstanceIsCanceled = "workflow.instance.is_canceled";

    /// <summary>
    /// 实例已故障，不允许该操作
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>（枚举名）
    /// </remarks>
    public const string InstanceIsFaulted = "workflow.instance.is_faulted";

    /// <summary>
    /// 实例已终止，不允许该操作
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>（枚举名）
    /// </remarks>
    public const string InstanceIsTerminated = "workflow.instance.is_terminated";

    /// <summary>
    /// 定义是草稿，不允许该操作
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>（枚举名）
    /// </remarks>
    public const string DefinitionIsDraft = "workflow.definition.is_draft";

    /// <summary>
    /// 定义已发布，不允许该操作
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>（枚举名）
    /// </remarks>
    public const string DefinitionIsPublished = "workflow.definition.is_published";

    /// <summary>
    /// 定义已停用，不允许该操作
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>（枚举名）
    /// </remarks>
    public const string DefinitionIsDisabled = "workflow.definition.is_disabled";

    /// <summary>
    /// 定义已归档，不允许该操作
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>status</c>（枚举名）
    /// </remarks>
    public const string DefinitionIsArchived = "workflow.definition.is_archived";

    /// <summary>
    /// 实例状态 Running 的展示名
    /// </summary>
    public const string InstanceStatusRunning = "workflow.instance_status.running";

    /// <summary>
    /// 实例状态 Suspended 的展示名
    /// </summary>
    public const string InstanceStatusSuspended = "workflow.instance_status.suspended";

    /// <summary>
    /// 实例状态 Completed 的展示名
    /// </summary>
    public const string InstanceStatusCompleted = "workflow.instance_status.completed";

    /// <summary>
    /// 实例状态 Canceled 的展示名
    /// </summary>
    public const string InstanceStatusCanceled = "workflow.instance_status.canceled";

    /// <summary>
    /// 实例状态 Faulted 的展示名
    /// </summary>
    public const string InstanceStatusFaulted = "workflow.instance_status.faulted";

    /// <summary>
    /// 实例状态 Terminated 的展示名
    /// </summary>
    public const string InstanceStatusTerminated = "workflow.instance_status.terminated";

    /// <summary>
    /// 节点实例状态 Running 的展示名
    /// </summary>
    public const string NodeStatusRunning = "workflow.node_status.running";

    /// <summary>
    /// 节点实例状态 Suspended 的展示名
    /// </summary>
    public const string NodeStatusSuspended = "workflow.node_status.suspended";

    /// <summary>
    /// 节点实例状态 Completed 的展示名
    /// </summary>
    public const string NodeStatusCompleted = "workflow.node_status.completed";

    /// <summary>
    /// 节点实例状态 Canceled 的展示名
    /// </summary>
    public const string NodeStatusCanceled = "workflow.node_status.canceled";

    /// <summary>
    /// 节点实例状态 Faulted 的展示名
    /// </summary>
    public const string NodeStatusFaulted = "workflow.node_status.faulted";

    /// <summary>
    /// 节点实例状态 Compensated 的展示名
    /// </summary>
    public const string NodeStatusCompensated = "workflow.node_status.compensated";

    /// <summary>
    /// 定义状态 Draft 的展示名
    /// </summary>
    public const string DefinitionStatusDraft = "workflow.definition_status.draft";

    /// <summary>
    /// 定义状态 Published 的展示名
    /// </summary>
    public const string DefinitionStatusPublished = "workflow.definition_status.published";

    /// <summary>
    /// 定义状态 Disabled 的展示名
    /// </summary>
    public const string DefinitionStatusDisabled = "workflow.definition_status.disabled";

    /// <summary>
    /// 定义状态 Archived 的展示名
    /// </summary>
    public const string DefinitionStatusArchived = "workflow.definition_status.archived";

    /// <summary>
    /// 活动分类 Control 的展示名
    /// </summary>
    public const string ActivityCategoryControl = "workflow.activity_category.control";

    /// <summary>
    /// 活动分类 Human 的展示名
    /// </summary>
    public const string ActivityCategoryHuman = "workflow.activity_category.human";

    /// <summary>
    /// 活动分类 Integration 的展示名
    /// </summary>
    public const string ActivityCategoryIntegration = "workflow.activity_category.integration";

    /// <summary>
    /// 活动分类 Data 的展示名
    /// </summary>
    public const string ActivityCategoryData = "workflow.activity_category.data";

    /// <summary>
    /// 活动 Start 的显示名
    /// </summary>
    public const string ActivityStartName = "workflow.activity_type.start.name";

    /// <summary>
    /// 活动 Start 的说明
    /// </summary>
    public const string ActivityStartDescription = "workflow.activity_type.start.description";

    /// <summary>
    /// 活动 End 的显示名
    /// </summary>
    public const string ActivityEndName = "workflow.activity_type.end.name";

    /// <summary>
    /// 活动 End 的说明
    /// </summary>
    public const string ActivityEndDescription = "workflow.activity_type.end.description";

    /// <summary>
    /// 活动 Terminate 的显示名
    /// </summary>
    public const string ActivityTerminateName = "workflow.activity_type.terminate.name";

    /// <summary>
    /// 活动 Terminate 的说明
    /// </summary>
    public const string ActivityTerminateDescription = "workflow.activity_type.terminate.description";

    /// <summary>
    /// 活动 SetVariable 的显示名
    /// </summary>
    public const string ActivitySetVariableName = "workflow.activity_type.set_variable.name";

    /// <summary>
    /// 活动 SetVariable 的说明
    /// </summary>
    public const string ActivitySetVariableDescription = "workflow.activity_type.set_variable.description";

    /// <summary>
    /// 活动 Decision 的显示名
    /// </summary>
    public const string ActivityDecisionName = "workflow.activity_type.decision.name";

    /// <summary>
    /// 活动 Decision 的说明
    /// </summary>
    public const string ActivityDecisionDescription = "workflow.activity_type.decision.description";

    /// <summary>
    /// 活动 Parallel 的显示名
    /// </summary>
    public const string ActivityParallelName = "workflow.activity_type.parallel.name";

    /// <summary>
    /// 活动 Parallel 的说明
    /// </summary>
    public const string ActivityParallelDescription = "workflow.activity_type.parallel.description";

    /// <summary>
    /// 活动 Join 的显示名
    /// </summary>
    public const string ActivityJoinName = "workflow.activity_type.join.name";

    /// <summary>
    /// 活动 Join 的说明
    /// </summary>
    public const string ActivityJoinDescription = "workflow.activity_type.join.description";

    /// <summary>
    /// 活动 Delay 的显示名
    /// </summary>
    public const string ActivityDelayName = "workflow.activity_type.delay.name";

    /// <summary>
    /// 活动 Delay 的说明
    /// </summary>
    public const string ActivityDelayDescription = "workflow.activity_type.delay.description";

    /// <summary>
    /// 活动 UserTask 的显示名
    /// </summary>
    public const string ActivityUserTaskName = "workflow.activity_type.user_task.name";

    /// <summary>
    /// 活动 UserTask 的说明
    /// </summary>
    public const string ActivityUserTaskDescription = "workflow.activity_type.user_task.description";

    /// <summary>
    /// 活动 PublishEvent 的显示名
    /// </summary>
    public const string ActivityPublishEventName = "workflow.activity_type.publish_event.name";

    /// <summary>
    /// 活动 PublishEvent 的说明
    /// </summary>
    public const string ActivityPublishEventDescription = "workflow.activity_type.publish_event.description";

    /// <summary>
    /// 活动 WaitSignal 的显示名
    /// </summary>
    public const string ActivityWaitSignalName = "workflow.activity_type.wait_signal.name";

    /// <summary>
    /// 活动 WaitSignal 的说明
    /// </summary>
    public const string ActivityWaitSignalDescription = "workflow.activity_type.wait_signal.description";

    /// <summary>
    /// 活动 EventGateway 的显示名
    /// </summary>
    public const string ActivityEventGatewayName = "workflow.activity_type.event_gateway.name";

    /// <summary>
    /// 活动 EventGateway 的说明
    /// </summary>
    public const string ActivityEventGatewayDescription = "workflow.activity_type.event_gateway.description";

    /// <summary>
    /// 活动 SubWorkflow 的显示名
    /// </summary>
    public const string ActivitySubWorkflowName = "workflow.activity_type.sub_workflow.name";

    /// <summary>
    /// 活动 SubWorkflow 的说明
    /// </summary>
    public const string ActivitySubWorkflowDescription = "workflow.activity_type.sub_workflow.description";

    /// <summary>
    /// 活动 ForEach 的显示名
    /// </summary>
    public const string ActivityForEachName = "workflow.activity_type.for_each.name";

    /// <summary>
    /// 活动 ForEach 的说明
    /// </summary>
    public const string ActivityForEachDescription = "workflow.activity_type.for_each.description";

    /// <summary>
    /// 活动 Log 的显示名
    /// </summary>
    public const string ActivityLogName = "workflow.activity_type.log.name";

    /// <summary>
    /// 活动 Log 的说明
    /// </summary>
    public const string ActivityLogDescription = "workflow.activity_type.log.description";

    /// <summary>
    /// 活动 Fault 的显示名
    /// </summary>
    public const string ActivityFaultName = "workflow.activity_type.fault.name";

    /// <summary>
    /// 活动 Fault 的说明
    /// </summary>
    public const string ActivityFaultDescription = "workflow.activity_type.fault.description";

    /// <summary>
    /// 活动 Compensate 的显示名
    /// </summary>
    public const string ActivityCompensateName = "workflow.activity_type.compensate.name";

    /// <summary>
    /// 活动 Compensate 的说明
    /// </summary>
    public const string ActivityCompensateDescription = "workflow.activity_type.compensate.description";

    /// <summary>
    /// 活动 Http 的显示名
    /// </summary>
    public const string ActivityHttpName = "workflow.activity_type.http.name";

    /// <summary>
    /// 活动 Http 的说明
    /// </summary>
    public const string ActivityHttpDescription = "workflow.activity_type.http.description";

    /// <summary>
    /// 活动 ExternalTask 的显示名
    /// </summary>
    public const string ActivityExternalTaskName = "workflow.activity_type.external_task.name";

    /// <summary>
    /// 活动 ExternalTask 的说明
    /// </summary>
    public const string ActivityExternalTaskDescription = "workflow.activity_type.external_task.description";

    /// <summary>
    /// 开始节点 SignalName 输入的说明
    /// </summary>
    public const string ActivityStartSignalNameInput = "workflow.activity_type.start.signal_name";

    /// <summary>
    /// 开始节点 Cron 输入的说明
    /// </summary>
    public const string ActivityStartCronInput = "workflow.activity_type.start.cron";

    /// <summary>
    /// 汇合节点 Mode 输入的说明
    /// </summary>
    public const string ActivityJoinModeInput = "workflow.activity_type.join.mode";

    /// <summary>
    /// 用户任务 ApprovalThreshold 输入的说明
    /// </summary>
    public const string ActivityUserTaskApprovalThresholdInput = "workflow.activity_type.user_task.approval_threshold";

    /// <summary>
    /// 用户任务 DueAt 输入的说明
    /// </summary>
    public const string ActivityUserTaskDueAtInput = "workflow.activity_type.user_task.due_at";

    /// <summary>
    /// 定义 ID 为空
    /// </summary>
    public const string DefinitionIdEmpty = "workflow.definition.id_empty";

    /// <summary>
    /// 工作流名称不合文法
    /// </summary>
    /// <remarks>
    /// 参数：<c>value</c>、<c>max</c>
    /// </remarks>
    public const string DefinitionNameInvalid = "workflow.definition.name_invalid";

    /// <summary>
    /// 版本号小于 1
    /// </summary>
    /// <remarks>
    /// 参数：<c>value</c>
    /// </remarks>
    public const string DefinitionVersionInvalid = "workflow.definition.version_invalid";

    /// <summary>
    /// 分类超长
    /// </summary>
    /// <remarks>
    /// 参数：<c>length</c>、<c>max</c>
    /// </remarks>
    public const string DefinitionCategoryTooLong = "workflow.definition.category_too_long";

    /// <summary>
    /// 超时不是正数
    /// </summary>
    /// <remarks>
    /// 参数：<c>value</c>（<c>c</c> 格式的时长）
    /// </remarks>
    public const string DefinitionTimeoutInvalid = "workflow.definition.timeout_invalid";

    /// <summary>
    /// 节点 ID 不合文法
    /// </summary>
    /// <remarks>
    /// 参数：<c>value</c>、<c>max</c>
    /// </remarks>
    public const string DefinitionNodeIdInvalid = "workflow.definition.node_id_invalid";

    /// <summary>
    /// 转移 ID 不合文法
    /// </summary>
    /// <remarks>
    /// 参数：<c>value</c>、<c>max</c>
    /// </remarks>
    public const string DefinitionTransitionIdInvalid = "workflow.definition.transition_id_invalid";

    /// <summary>
    /// 活动类型名不合文法
    /// </summary>
    /// <remarks>
    /// 参数：<c>value</c>、<c>max</c>
    /// </remarks>
    public const string DefinitionActivityTypeInvalid = "workflow.definition.activity_type_invalid";

    /// <summary>
    /// 变量名为空
    /// </summary>
    public const string DefinitionVariableNameEmpty = "workflow.definition.variable_name_empty";

    /// <summary>
    /// 重试的最多尝试次数小于 1
    /// </summary>
    /// <remarks>
    /// 参数：<c>value</c>
    /// </remarks>
    public const string DefinitionRetryAttemptsInvalid = "workflow.definition.retry_attempts_invalid";

    /// <summary>
    /// 首次重试延迟为负
    /// </summary>
    /// <remarks>
    /// 参数：<c>value</c>（<c>c</c> 格式的时长）
    /// </remarks>
    public const string DefinitionRetryDelayNegative = "workflow.definition.retry_delay_negative";

    /// <summary>
    /// 退避因子小于 1
    /// </summary>
    /// <remarks>
    /// 参数：<c>value</c>
    /// </remarks>
    public const string DefinitionRetryBackoffInvalid = "workflow.definition.retry_backoff_invalid";

    /// <summary>
    /// 缺少必填成员
    /// </summary>
    /// <remarks>
    /// 参数：<c>member</c>（JSON 成员名）
    /// </remarks>
    public const string DefinitionMemberMissing = "workflow.definition.member_missing";

    /// <summary>
    /// 本包的消息目录
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(WorkflowMessages));
}
