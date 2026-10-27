// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 用户任务书签载荷、恢复输入与节点状态里的键。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class UserTaskPayloadKeys
{
    /// <summary>
    /// 载荷：标题。
    /// </summary>
    public const string Title = "title";

    /// <summary>
    /// 载荷：表单数据。
    /// </summary>
    public const string FormData = "formData";

    /// <summary>
    /// 载荷：抄送人。
    /// </summary>
    public const string CcUserIds = "ccUserIds";

    /// <summary>
    /// 恢复输入：操作者。
    /// </summary>
    public const string ActorId = "actorId";

    /// <summary>
    /// 恢复输入：结果。
    /// </summary>
    public const string Outcome = "outcome";

    /// <summary>
    /// 恢复输入：意见。
    /// </summary>
    public const string Comment = "comment";

    /// <summary>
    /// 节点状态：指派人列表。
    /// </summary>
    public const string Assignees = "assignees";

    /// <summary>
    /// 节点状态：已通过的操作者。
    /// </summary>
    public const string Approved = "approved";

    /// <summary>
    /// 节点状态：完成策略。
    /// </summary>
    public const string Policy = "policy";

    /// <summary>
    /// 节点状态：历史（操作者、结果、意见、时间）。
    /// </summary>
    public const string History = "history";

    /// <summary>
    /// 节点状态：已拒绝的指派人列表。
    /// </summary>
    public const string Rejected = "rejected";

    /// <summary>
    /// 节点状态：认领人。
    /// </summary>
    public const string ClaimedBy = "claimedBy";

    /// <summary>
    /// 载荷：优先级。
    /// </summary>
    public const string Priority = "priority";

    /// <summary>
    /// 载荷：截止时刻。
    /// </summary>
    public const string DueAt = "dueAt";

    /// <summary>
    /// 载荷：表单键。
    /// </summary>
    public const string FormKey = "formKey";

    /// <summary>
    /// 提醒书签载荷：提醒间隔秒数。
    /// </summary>
    public const string ReminderIntervalSeconds = "intervalSeconds";
}
