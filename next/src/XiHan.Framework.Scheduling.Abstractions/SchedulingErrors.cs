// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 调度子系统的错误码；登记在 <c>spec/conventions/error-codes.md</c>。名字与 cron 表达式不合法是编程错误，在构造时抛出。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchedulingErrors
{
    /// <summary>作业未登记。</summary>
    public const string JobNotFound = "SCHEDULING_NOTFOUND_001";

    /// <summary>作业正在运行且不允许并发。</summary>
    public const string JobRunning = "SCHEDULING_CONFLICT_001";

    /// <summary>领导租约被其他持有者占用。</summary>
    public const string LeaseHeld = "SCHEDULING_CONFLICT_002";

    /// <summary>调度器未运行。</summary>
    public const string SchedulerUnavailable = "SCHEDULING_UNAVAILABLE_001";

    /// <summary>领导选举的存储（如 Kubernetes API Server）不可达或超时。</summary>
    public const string LeaseUnavailable = "SCHEDULING_UNAVAILABLE_002";

    /// <summary>作业抛出未处理异常。</summary>
    public const string JobFailed = "SCHEDULING_UNEXPECTED_001";
}
