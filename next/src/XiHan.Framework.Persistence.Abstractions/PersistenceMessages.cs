// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 持久化子系统的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class PersistenceMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "persistence";

    /// <summary>
    /// 多租户实体在没有租户的调用里新增。参数：<c>entity</c>。
    /// </summary>
    public const string TenantRequired = "persistence.entity.tenant_required";

    /// <summary>
    /// 新增的标识已存在。参数：<c>entity</c>、<c>id</c>。
    /// </summary>
    public const string Duplicate = "persistence.entity.duplicate";

    /// <summary>
    /// 并发戳过期。参数：<c>entity</c>、<c>id</c>。
    /// </summary>
    public const string StaleStamp = "persistence.entity.stale_stamp";

    /// <summary>
    /// 实体不存在。参数：<c>entity</c>、<c>id</c>。
    /// </summary>
    public const string NotFound = "persistence.entity.not_found";

    /// <summary>
    /// 只读工作单元拒绝写入。
    /// </summary>
    public const string ReadOnly = "persistence.unit_of_work.read_only";

    /// <summary>
    /// 聚合有未派发的领域事件但没有注册派发器。参数：<c>aggregate</c>、<c>count</c>。
    /// </summary>
    public const string EventsUndispatched = "persistence.unit_of_work.events_undispatched";

    /// <summary>
    /// 实体上的乐观并发冲突。参数：<c>entity</c>、<c>reason</c>。
    /// </summary>
    public const string ConcurrencyConflict = "persistence.entity.concurrency_conflict";

    /// <summary>
    /// 提交时的乐观并发冲突。参数：<c>reason</c>。
    /// </summary>
    public const string CommitConflict = "persistence.unit_of_work.concurrency_conflict";

    /// <summary>
    /// 内存表容量已满。参数：<c>entity</c>、<c>capacity</c>。
    /// </summary>
    public const string CapacityExceeded = "persistence.store.capacity_exceeded";

    /// <summary>
    /// 关系数据库连不上（健康探针）。
    /// </summary>
    public const string StoreUnavailable = "persistence.store.unavailable";

    /// <summary>
    /// 雪花标识的机器位租约尚未取得或已丢失（健康项）。
    /// </summary>
    public const string WorkerIdUnavailable = "persistence.snowflake.worker_unavailable";

    /// <summary>
    /// 延迟装载的目录；首次访问时读嵌入的语言文件，之后复用同一实例。
    /// </summary>
    private static readonly Lazy<MessageCatalog> _catalog = new(static () => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(PersistenceMessages)));

    /// <summary>
    /// 目录实例；首次访问时从嵌入资源装载。
    /// </summary>
    public static MessageCatalog Catalog => _catalog.Value;
}
