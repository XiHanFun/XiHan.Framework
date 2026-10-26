// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 工作流子系统的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>（RFC-0010）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "workflow";

    /// <summary>定义不合法。参数：<c>errors</c>。</summary>
    public const string DefinitionInvalid = "workflow.definition.invalid";

    /// <summary>定义不存在或没有已发布版本。参数：<c>name</c>。</summary>
    public const string DefinitionNotFound = "workflow.definition.not_found";

    /// <summary>定义状态不允许该操作。参数：<c>id</c>、<c>status</c>。</summary>
    public const string DefinitionStatus = "workflow.definition.status";

    /// <summary>实例不存在。参数：<c>id</c>。</summary>
    public const string InstanceNotFound = "workflow.instance.not_found";

    /// <summary>实例状态不允许该操作。参数：<c>id</c>、<c>status</c>。</summary>
    public const string InstanceStatus = "workflow.instance.status";

    /// <summary>子流程深度超限。参数：<c>depth</c>、<c>max</c>。</summary>
    public const string InstanceDepth = "workflow.instance.depth";

    /// <summary>一个波次执行的节点数超过上限。参数：<c>id</c>、<c>limit</c>。</summary>
    public const string InstanceRunaway = "workflow.instance.runaway";

    /// <summary>汇合节点等待的分支已死。参数：<c>id</c>。</summary>
    public const string InstanceJoinDead = "workflow.instance.join_dead";

    /// <summary>节点没有可走的出边。参数：<c>node</c>。</summary>
    public const string InstanceNoTransition = "workflow.instance.no_transition";

    /// <summary>缺少必填变量。参数：<c>name</c>。</summary>
    public const string VariableRequired = "workflow.variable.required";

    /// <summary>书签不存在。参数：<c>id</c>。</summary>
    public const string BookmarkNotFound = "workflow.bookmark.not_found";

    /// <summary>书签不可恢复：实例不在运行中。参数：<c>id</c>。</summary>
    public const string BookmarkNotResumable = "workflow.bookmark.not_resumable";

    /// <summary>书签的键与期望不符。参数：<c>id</c>。</summary>
    public const string BookmarkKeyMismatch = "workflow.bookmark.key_mismatch";

    /// <summary>活动类型未登记。参数：<c>type</c>。</summary>
    public const string ActivityNotRegistered = "workflow.activity.not_registered";

    /// <summary>活动抛出未处理异常。参数：<c>type</c>、<c>reason</c>。</summary>
    public const string ActivityFailed = "workflow.activity.failed";

    /// <summary>活动属性不合法。参数：<c>name</c>、<c>reason</c>。</summary>
    public const string ActivityPropertyInvalid = "workflow.activity.property_invalid";

    /// <summary>表达式语法不合法或求值失败。参数：<c>expression</c>、<c>reason</c>。</summary>
    public const string ExpressionInvalid = "workflow.expression.invalid";

    /// <summary>操作者不是任务的指派人。参数：<c>task</c>、<c>actor</c>。</summary>
    public const string TaskDenied = "workflow.task.denied";

    /// <summary>任务结果不在允许范围。参数：<c>outcome</c>。</summary>
    public const string TaskOutcomeInvalid = "workflow.task.outcome_invalid";

    /// <summary>指派人已在任务上。参数：<c>assignee</c>。</summary>
    public const string TaskAssigneeExists = "workflow.task.assignee_exists";

    /// <summary>没有解析出任何指派人。参数：<c>node</c>。</summary>
    public const string TaskNoAssignee = "workflow.task.no_assignee";

    /// <summary>指派规则类型未登记。参数：<c>type</c>。</summary>
    public const string AssigneeRuleUnknown = "workflow.assignee.rule_unknown";

    /// <summary>指派规则不合法。参数：<c>type</c>、<c>reason</c>。</summary>
    public const string AssigneeRuleInvalid = "workflow.assignee.rule_invalid";

    /// <summary>实例锁等待超时。参数：<c>id</c>。</summary>
    public const string LockTimeout = "workflow.lock.timeout";

    /// <summary>存储不可达。参数：<c>reason</c>。</summary>
    public const string StoreUnavailable = "workflow.store.unavailable";

    /// <summary>存储容量已满。参数：<c>store</c>、<c>limit</c>。</summary>
    public const string StoreCapacity = "workflow.store.capacity";

    /// <summary>子流程启动失败。参数：<c>id</c>、<c>reason</c>。</summary>
    public const string ChildFailed = "workflow.child.failed";

    /// <summary>同一关联 ID 下已有未结束的实例。参数：<c>name</c>、<c>correlation</c>。</summary>
    public const string ActiveInstanceExists = "workflow.instance.active_exists";

    /// <summary>跳转目标节点不存在。参数：<c>id</c>、<c>node</c>。</summary>
    public const string NodeNotFound = "workflow.instance.node_not_found";

    /// <summary>迁移目标与未结束的节点不兼容。参数：<c>id</c>、<c>node</c>。</summary>
    public const string MigrationIncompatible = "workflow.instance.migration_incompatible";

    /// <summary>实例级超时到期。参数：<c>id</c>。</summary>
    public const string InstanceTimedOut = "workflow.instance.timed_out";

    /// <summary>任务已被别人认领。参数：<c>task</c>、<c>actor</c>。</summary>
    public const string TaskClaimed = "workflow.task.claimed";

    /// <summary>任务的完成策略不支持认领。参数：<c>task</c>。</summary>
    public const string TaskNotClaimable = "workflow.task.not_claimable";

    /// <summary>找不到可退回的节点。参数：<c>task</c>。</summary>
    public const string TaskNoReturnTarget = "workflow.task.no_return_target";

    /// <summary>任务查询不合法。参数：<c>reason</c>。</summary>
    public const string TaskQueryInvalid = "workflow.task.query_invalid";

    /// <summary>工作者没有持有外部任务的锁。参数：<c>task</c>、<c>worker</c>。</summary>
    public const string ExternalTaskDenied = "workflow.external.denied";

    /// <summary>外部任务由工作者报告失败。参数：<c>reason</c>。</summary>
    public const string ExternalTaskFailed = "workflow.external.failed";

    /// <summary>HTTP 请求失败。参数：<c>status</c>、<c>reason</c>。</summary>
    public const string HttpFailed = "workflow.http.failed";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(WorkflowMessages));
}
