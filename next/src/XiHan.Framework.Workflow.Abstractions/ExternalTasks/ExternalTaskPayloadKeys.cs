// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.ExternalTasks;

/// <summary>
/// 外部任务书签载荷与恢复输入里的键。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ExternalTaskPayloadKeys
{
    /// <summary>
    /// 载荷：主题。
    /// </summary>
    public const string Topic = "topic";

    /// <summary>
    /// 载荷：活动声明的载荷。
    /// </summary>
    public const string Payload = "payload";

    /// <summary>
    /// 载荷：持有锁的工作者。
    /// </summary>
    public const string LockedBy = "lockedBy";

    /// <summary>
    /// 载荷：锁到期时刻。
    /// </summary>
    public const string LockedUntil = "lockedUntil";

    /// <summary>
    /// 载荷：在此之前不可领取。
    /// </summary>
    public const string NotBefore = "notBefore";

    /// <summary>
    /// 载荷：尝试次数。
    /// </summary>
    public const string Attempt = "attempt";

    /// <summary>
    /// 恢复输入：工作者的输出。
    /// </summary>
    public const string Outputs = "outputs";

    /// <summary>
    /// 恢复输入：工作者报告的错误。
    /// </summary>
    public const string Error = "error";

    /// <summary>
    /// 恢复输入：工作者 ID。
    /// </summary>
    public const string WorkerId = "workerId";
}
