// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 工作流子系统的错误码（<c>conventions/error-codes.md</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowErrors
{
    /// <summary>工作流定义不合法（<c>details.errors</c> 列出全部问题）。</summary>
    public const string DefinitionInvalid = "WORKFLOW_VALIDATION_001";

    /// <summary>启动或恢复的输入不合法（缺必填变量、子流程深度超限、任务结果不在允许范围）。</summary>
    public const string InputInvalid = "WORKFLOW_VALIDATION_002";

    /// <summary>表达式语法不合法或求值失败。</summary>
    public const string ExpressionInvalid = "WORKFLOW_VALIDATION_003";

    /// <summary>工作流定义不存在或没有已发布版本。</summary>
    public const string DefinitionNotFound = "WORKFLOW_NOTFOUND_001";

    /// <summary>工作流实例不存在。</summary>
    public const string InstanceNotFound = "WORKFLOW_NOTFOUND_002";

    /// <summary>书签（用户任务）不存在。</summary>
    public const string BookmarkNotFound = "WORKFLOW_NOTFOUND_003";

    /// <summary>活动类型或指派规则类型未登记。</summary>
    public const string TypeNotRegistered = "WORKFLOW_NOTFOUND_004";

    /// <summary>实例状态不允许该操作。</summary>
    public const string InstanceStatus = "WORKFLOW_CONFLICT_001";

    /// <summary>定义状态不允许该操作。</summary>
    public const string DefinitionStatus = "WORKFLOW_CONFLICT_002";

    /// <summary>书签不可恢复（实例不在运行中、键不匹配）。</summary>
    public const string BookmarkNotResumable = "WORKFLOW_CONFLICT_003";

    /// <summary>指派人已在任务上。</summary>
    public const string AssigneeExists = "WORKFLOW_CONFLICT_004";

    /// <summary>操作者不是任务的指派人。</summary>
    public const string TaskDenied = "WORKFLOW_DENIED_001";

    /// <summary>实例锁等待超时。</summary>
    public const string LockTimeout = "WORKFLOW_TIMEOUT_001";

    /// <summary>工作流存储不可达。</summary>
    public const string StoreUnavailable = "WORKFLOW_UNAVAILABLE_001";

    /// <summary>工作流存储容量已满。</summary>
    public const string StoreCapacity = "WORKFLOW_UNAVAILABLE_002";

    /// <summary>活动抛出未处理异常或引擎内部失败。</summary>
    public const string ActivityFailed = "WORKFLOW_UNEXPECTED_001";
}
