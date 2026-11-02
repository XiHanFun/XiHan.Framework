// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 持久化子系统的消息键。
/// </summary>
/// <remarks>
/// 各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class PersistenceMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "persistence";

    /// <summary>
    /// 多租户实体在没有租户的调用里新增。
    /// </summary>
    /// <remarks>
    /// 参数：<c>entity</c>。
    /// </remarks>
    public const string TenantRequired = "persistence.entity.tenant_required";

    /// <summary>
    /// 新增的标识已存在。
    /// </summary>
    /// <remarks>
    /// 参数：<c>entity</c>、<c>id</c>。
    /// </remarks>
    public const string Duplicate = "persistence.entity.duplicate";

    /// <summary>
    /// 写入撞上唯一索引。
    /// </summary>
    /// <remarks>
    /// 参数：<c>entity</c>、<c>index</c>（索引名）。
    /// </remarks>
    public const string UniqueKeyConflict = "persistence.entity.unique_key_conflict";

    /// <summary>
    /// 并发戳过期。
    /// </summary>
    /// <remarks>
    /// 参数：<c>entity</c>、<c>id</c>。
    /// </remarks>
    public const string StaleStamp = "persistence.entity.stale_stamp";

    /// <summary>
    /// 实体不存在。
    /// </summary>
    /// <remarks>
    /// 参数：<c>entity</c>、<c>id</c>。
    /// </remarks>
    public const string NotFound = "persistence.entity.not_found";

    /// <summary>
    /// 只读工作单元拒绝写入。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="PersistenceErrors.ReadOnly"/> 搭配。
    /// </remarks>
    public const string ReadOnly = "persistence.unit_of_work.read_only";

    /// <summary>
    /// 查询的取出条数超过上限。
    /// </summary>
    /// <remarks>
    /// 参数：<c>take</c>（请求的条数）、<c>max</c>（上限）。与 <see cref="PersistenceErrors.TakeExceeded"/> 搭配。
    /// </remarks>
    public const string TakeExceeded = "persistence.query.take_exceeded";

    /// <summary>
    /// 同一服务作用域里再经管道发起的嵌套调用。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="PersistenceErrors.InvalidUnitOfWorkState"/> 搭配。
    /// </remarks>
    public const string NestedInvocation = "persistence.unit_of_work.nested";

    /// <summary>
    /// 聚合有未派发的领域事件但没有注册派发器。
    /// </summary>
    /// <remarks>
    /// 参数：<c>aggregate</c>、<c>count</c>。
    /// </remarks>
    public const string EventsUndispatched = "persistence.unit_of_work.events_undispatched";

    /// <summary>
    /// 实体上的乐观并发冲突。
    /// </summary>
    /// <remarks>
    /// 参数：<c>entity</c>、<c>reason</c>。
    /// </remarks>
    public const string ConcurrencyConflict = "persistence.entity.concurrency_conflict";

    /// <summary>
    /// 提交时的乐观并发冲突。
    /// </summary>
    /// <remarks>
    /// 参数：<c>reason</c>。
    /// </remarks>
    public const string CommitConflict = "persistence.unit_of_work.concurrency_conflict";

    /// <summary>
    /// 内存表容量已满。
    /// </summary>
    /// <remarks>
    /// 参数：<c>entity</c>、<c>capacity</c>。
    /// </remarks>
    public const string CapacityExceeded = "persistence.store.capacity_exceeded";

    /// <summary>
    /// 关系数据库连不上（健康探针）。
    /// </summary>
    public const string StoreUnavailable = "persistence.store.unavailable";

    /// <summary>
    /// 雪花标识的机器位租约尚未取得或已丢失。
    /// </summary>
    /// <remarks>
    /// 健康项与调用结果共用。
    /// </remarks>
    public const string WorkerIdUnavailable = "persistence.snowflake.worker_unavailable";

    /// <summary>
    /// 时钟回拨超过容忍阈值，暂不生成雪花标识。
    /// </summary>
    /// <remarks>
    /// 生成器以异常抛出，调用里由 <see cref="Identifiers.SnowflakeIdFeature"/> 的中间件转为结果时使用。
    /// </remarks>
    public const string ClockMovedBackwards = "persistence.snowflake.clock_moved_backwards";

    /// <summary>
    /// 运行时数据源没有登记。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。与 <see cref="PersistenceErrors.DataSourceNotFound"/> 搭配。
    /// </remarks>
    public const string DataSourceNotFound = "persistence.datasource.not_found";

    /// <summary>
    /// 数据库类型 Provider 不认识。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>databaseType</c>。与 <see cref="PersistenceErrors.DataSourceInvalid"/> 搭配。
    /// </remarks>
    public const string DataSourceTypeUnknown = "persistence.datasource.database_type_unknown";

    /// <summary>
    /// 数据源定义不合法（连接串格式错等）。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>reason</c>（已脱敏）。与 <see cref="PersistenceErrors.DataSourceInvalid"/> 搭配。
    /// </remarks>
    public const string DataSourceInvalid = "persistence.datasource.definition_invalid";

    /// <summary>
    /// 数据源连不上。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>reason</c>（已脱敏）。与 <see cref="PersistenceErrors.DataSourceUnavailable"/> 搭配。
    /// </remarks>
    public const string DataSourceUnavailable = "persistence.datasource.unavailable";

    /// <summary>
    /// 数据源登记已满。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>（要登记的名字）、<c>capacity</c>。与 <see cref="PersistenceErrors.DataSourceCapacityExceeded"/> 搭配。
    /// </remarks>
    public const string DataSourceCapacityExceeded = "persistence.datasource.capacity_exceeded";

    /// <summary>
    /// 当前的持久化 Provider 不支持运行时数据源。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="PersistenceErrors.NotSupported"/> 搭配。
    /// </remarks>
    public const string DataSourcesNotSupported = "persistence.datasource.not_supported";

    /// <summary>
    /// 库里没有要读结构的表。
    /// </summary>
    /// <remarks>
    /// 参数：<c>dataSource</c>、<c>table</c>。与 <see cref="PersistenceErrors.TableNotFound"/> 搭配。
    /// </remarks>
    public const string TableNotFound = "persistence.schema.table_not_found";

    /// <summary>
    /// 一次读结构的表数超过上限。
    /// </summary>
    /// <remarks>
    /// 参数：<c>count</c>、<c>max</c>。与 <see cref="PersistenceErrors.TakeExceeded"/> 搭配。
    /// </remarks>
    public const string TooManyTables = "persistence.schema.too_many_tables";

    /// <summary>
    /// 库结构表列表的分页越界。
    /// </summary>
    /// <remarks>
    /// 参数：<c>skip</c>、<c>take</c>。与 <see cref="PersistenceErrors.ListPageInvalid"/> 搭配。
    /// </remarks>
    public const string ListPageInvalid = "persistence.schema.list_page_invalid";

    /// <summary>
    /// 读库结构失败。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>（数据源名，缺省连接为 <c>default</c>）、<c>reason</c>（已脱敏）。与 <see cref="PersistenceErrors.DataSourceUnavailable"/> 搭配。
    /// </remarks>
    public const string SchemaReadFailed = "persistence.schema.read_failed";

    /// <summary>
    /// 当前的持久化 Provider 不能读库结构。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="PersistenceErrors.NotSupported"/> 搭配。
    /// </remarks>
    public const string SchemaNotSupported = "persistence.schema.not_supported";

    /// <summary>
    /// 租户分片库里的时间列还是旧的存储形式。
    /// </summary>
    /// <remarks>
    /// 参数：<c>tenant</c>（分片绑定的租户）、<c>columns</c>（同 <see cref="TimestampFormatMismatch"/>）。分片库首次绑定时核对（ADR-0097），与 <see cref="PersistenceErrors.StorageFormatMismatch"/> 搭配。
    /// </remarks>
    public const string ShardTimestampFormatMismatch = "persistence.schema.shard_timestamp_format_mismatch";

    /// <summary>
    /// 库里的时间列还是旧的存储形式。
    /// </summary>
    /// <remarks>
    /// 参数：<c>connection</c>（<c>default</c> 或命名连接名）、<c>columns</c>（<c>表.列 (声明类型)</c>，抽样不符的另带查到的存储类）。与 <see cref="PersistenceErrors.StorageFormatMismatch"/> 搭配。
    /// </remarks>
    public const string TimestampFormatMismatch = "persistence.schema.timestamp_format_mismatch";

    /// <summary>
    /// 延迟装载的目录。
    /// </summary>
    /// <remarks>
    /// 首次访问时读嵌入的语言文件，之后复用同一实例。
    /// </remarks>
    private static readonly Lazy<MessageCatalog> _catalog = new(static () => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(PersistenceMessages)));

    /// <summary>
    /// 目录实例。
    /// </summary>
    /// <remarks>
    /// 首次访问时从嵌入资源装载。
    /// </remarks>
    public static MessageCatalog Catalog => _catalog.Value;
}
