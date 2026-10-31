// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 调度子系统的消息键。
/// </summary>
/// <remarks>
/// 各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchedulingMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "scheduling";

    /// <summary>
    /// 作业未登记。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string JobNotFound = "scheduling.job.not_found";

    /// <summary>
    /// 作业正在运行。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string JobRunning = "scheduling.job.running";

    /// <summary>
    /// 作业抛出异常。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>reason</c>。
    /// </remarks>
    public const string JobFailed = "scheduling.job.failed";

    /// <summary>
    /// 作业类型未登记。
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>。
    /// </remarks>
    public const string JobTypeNotRegistered = "scheduling.job.type_not_registered";

    /// <summary>
    /// 作业定义不合法。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>reason</c>。
    /// </remarks>
    public const string DefinitionInvalid = "scheduling.definition.invalid";

    /// <summary>
    /// 触发器不存在。
    /// </summary>
    /// <remarks>
    /// 参数：<c>job</c>、<c>trigger</c>。
    /// </remarks>
    public const string TriggerNotFound = "scheduling.trigger.not_found";

    /// <summary>
    /// 运行不存在或已结束。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string RunNotFound = "scheduling.run.not_found";

    /// <summary>
    /// 作业没有代码定义。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string CodeDefinitionNotFound = "scheduling.job.code_definition_not_found";

    /// <summary>
    /// 运行被取消。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string RunCanceled = "scheduling.run.canceled";

    /// <summary>
    /// 运行超过执行时限。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>、<c>timeout</c>。
    /// </remarks>
    public const string RunTimedOut = "scheduling.run.timed_out";

    /// <summary>
    /// 领导租约被占用。
    /// </summary>
    /// <remarks>
    /// 参数：<c>resource</c>。
    /// </remarks>
    public const string LeaseHeld = "scheduling.lease.held";

    /// <summary>
    /// 调度器未运行。
    /// </summary>
    public const string SchedulerUnavailable = "scheduling.scheduler.unavailable";

    /// <summary>
    /// 领导选举的存储不可达。
    /// </summary>
    /// <remarks>
    /// 参数：<c>resource</c>、<c>reason</c>。
    /// </remarks>
    public const string LeaseUnavailable = "scheduling.lease.unavailable";

    /// <summary>
    /// 作业存储不可达。
    /// </summary>
    /// <remarks>
    /// 参数：<c>reason</c>。
    /// </remarks>
    public const string StoreUnavailable = "scheduling.store.unavailable";

    /// <summary>
    /// 作业存储容量已满。
    /// </summary>
    /// <remarks>
    /// 参数：<c>store</c>、<c>limit</c>。
    /// </remarks>
    public const string StoreCapacity = "scheduling.store.capacity";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(SchedulingMessages));
}
