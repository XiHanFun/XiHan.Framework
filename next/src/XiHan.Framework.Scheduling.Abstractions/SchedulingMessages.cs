// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 调度子系统的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchedulingMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "scheduling";

    /// <summary>作业未登记。参数：<c>name</c>。</summary>
    public const string JobNotFound = "scheduling.job.not_found";

    /// <summary>作业正在运行。参数：<c>name</c>。</summary>
    public const string JobRunning = "scheduling.job.running";

    /// <summary>作业抛出异常。参数：<c>name</c>、<c>reason</c>。</summary>
    public const string JobFailed = "scheduling.job.failed";

    /// <summary>作业类型未登记。参数：<c>type</c>。</summary>
    public const string JobTypeNotRegistered = "scheduling.job.type_not_registered";

    /// <summary>作业定义不合法。参数：<c>name</c>、<c>reason</c>。</summary>
    public const string DefinitionInvalid = "scheduling.definition.invalid";

    /// <summary>触发器不存在。参数：<c>job</c>、<c>trigger</c>。</summary>
    public const string TriggerNotFound = "scheduling.trigger.not_found";

    /// <summary>运行不存在或已结束。参数：<c>id</c>。</summary>
    public const string RunNotFound = "scheduling.run.not_found";

    /// <summary>运行被取消。参数：<c>id</c>。</summary>
    public const string RunCanceled = "scheduling.run.canceled";

    /// <summary>领导租约被占用。参数：<c>resource</c>。</summary>
    public const string LeaseHeld = "scheduling.lease.held";

    /// <summary>调度器未运行。</summary>
    public const string SchedulerUnavailable = "scheduling.scheduler.unavailable";

    /// <summary>领导选举的存储不可达。参数：<c>resource</c>、<c>reason</c>。</summary>
    public const string LeaseUnavailable = "scheduling.lease.unavailable";

    /// <summary>作业存储不可达。参数：<c>reason</c>。</summary>
    public const string StoreUnavailable = "scheduling.store.unavailable";

    /// <summary>作业存储容量已满。参数：<c>store</c>、<c>limit</c>。</summary>
    public const string StoreCapacity = "scheduling.store.capacity";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(SchedulingMessages));
}
