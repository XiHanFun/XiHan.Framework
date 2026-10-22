// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions;
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

    /// <summary>领导租约被占用。参数：<c>resource</c>。</summary>
    public const string LeaseHeld = "scheduling.lease.held";

    /// <summary>调度器未运行。</summary>
    public const string SchedulerUnavailable = "scheduling.scheduler.unavailable";

    /// <summary>领导选举的存储不可达。参数：<c>resource</c>、<c>reason</c>。</summary>
    public const string LeaseUnavailable = "scheduling.lease.unavailable";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(SchedulingMessages));
}
