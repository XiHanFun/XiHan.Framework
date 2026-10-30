// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 调度子系统的错误码。
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>。名字与 cron 表达式不合法是编程错误，在构造时抛出。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchedulingErrors
{
    /// <summary>
    /// 作业未登记。
    /// </summary>
    public const string JobNotFound = "SCHEDULING_NOTFOUND_001";

    /// <summary>
    /// 作业类型未登记。
    /// </summary>
    public const string JobTypeNotRegistered = "SCHEDULING_NOTFOUND_002";

    /// <summary>
    /// 触发器不存在。
    /// </summary>
    public const string TriggerNotFound = "SCHEDULING_NOTFOUND_003";

    /// <summary>
    /// 运行不存在或已结束。
    /// </summary>
    public const string RunNotFound = "SCHEDULING_NOTFOUND_004";

    /// <summary>
    /// 作业没有代码定义。
    /// </summary>
    /// <remarks>
    /// 不能恢复为代码定义（ADR-0053）。
    /// </remarks>
    public const string CodeDefinitionNotFound = "SCHEDULING_NOTFOUND_005";

    /// <summary>
    /// 作业定义不合法（触发器名重复、日历未登记、计划不合法）。
    /// </summary>
    public const string DefinitionInvalid = "SCHEDULING_VALIDATION_001";

    /// <summary>
    /// 作业正在运行且不允许并发。
    /// </summary>
    public const string JobRunning = "SCHEDULING_CONFLICT_001";

    /// <summary>
    /// 领导租约被其他持有者占用。
    /// </summary>
    public const string LeaseHeld = "SCHEDULING_CONFLICT_002";

    /// <summary>
    /// 运行被取消。
    /// </summary>
    public const string RunCanceled = "SCHEDULING_CONFLICT_003";

    /// <summary>
    /// 调度器未运行。
    /// </summary>
    public const string SchedulerUnavailable = "SCHEDULING_UNAVAILABLE_001";

    /// <summary>
    /// 领导选举的存储不可达或超时。
    /// </summary>
    public const string LeaseUnavailable = "SCHEDULING_UNAVAILABLE_002";

    /// <summary>
    /// 作业存储不可达。
    /// </summary>
    public const string StoreUnavailable = "SCHEDULING_UNAVAILABLE_003";

    /// <summary>
    /// 作业存储容量已满。
    /// </summary>
    public const string StoreCapacity = "SCHEDULING_UNAVAILABLE_004";

    /// <summary>
    /// 作业抛出未处理异常。
    /// </summary>
    public const string JobFailed = "SCHEDULING_UNEXPECTED_001";
}
