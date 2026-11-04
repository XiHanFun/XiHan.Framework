// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 持久化错误码
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class PersistenceErrors
{
    /// <summary>
    /// 实体不存在
    /// </summary>
    public const string NotFound = "PERSISTENCE_NOTFOUND_001";

    /// <summary>
    /// 运行时数据源没有在本进程登记
    /// </summary>
    /// <remarks>
    /// <see cref="DataSources.DataSourceRegistry.GetAsync"/> 按名字取，名字没有登记过或已移除
    /// </remarks>
    public const string DataSourceNotFound = "PERSISTENCE_NOTFOUND_002";

    /// <summary>
    /// 库里没有要读结构的表
    /// </summary>
    /// <remarks>
    /// <see cref="Schema.ISchemaReader.GetTablesAsync"/> 的逐项结果；参数 <c>dataSource</c>、<c>table</c>
    /// </remarks>
    public const string TableNotFound = "PERSISTENCE_NOTFOUND_003";

    /// <summary>
    /// 乐观并发冲突
    /// </summary>
    public const string Concurrency = "PERSISTENCE_CONFLICT_001";

    /// <summary>
    /// 标识重复
    /// </summary>
    public const string Duplicate = "PERSISTENCE_CONFLICT_002";

    /// <summary>
    /// 唯一键重复：写入撞上实体声明的唯一索引
    /// </summary>
    /// <remarks>
    /// 关系型 Provider 在写入失败后核对出来（并发写入同一业务键、同一批里两项同键、更新把键改成已有的值）；之后工作单元只能回滚。软删除实体的唯一索引只约束未删除的行，删除不会撞上
    /// </remarks>
    public const string UniqueKeyConflict = "PERSISTENCE_CONFLICT_003";

    /// <summary>
    /// 多租户实体缺少当前租户
    /// </summary>
    public const string TenantRequired = "PERSISTENCE_VALIDATION_001";

    /// <summary>
    /// 工作单元状态不允许该操作
    /// </summary>
    /// <remarks>
    /// 重复开始、未开始就提交以异常抛出；同一服务作用域里再经管道发起的嵌套调用由 Transaction 阶段以结果结束（不执行处理器）
    /// </remarks>
    public const string InvalidUnitOfWorkState = "PERSISTENCE_VALIDATION_002";

    /// <summary>
    /// 工作单元里的聚合有未派发的领域事件，但没有注册领域事件派发器
    /// </summary>
    public const string EventsUndispatched = "PERSISTENCE_VALIDATION_003";

    /// <summary>
    /// 实体落点不成立
    /// </summary>
    /// <remarks>
    /// 装配错误，以异常抛出：SqlSugar 实体的 <c>[Tenant(configId)]</c> 或登记时指定的命名连接不在客户端里、两者指向不同的连接（缺省落点规则下在启动时校验）；工作单元绑定的连接不在客户端里，或绑定连接的工作单元里操作的实体不落在那个连接上
    /// </remarks>
    public const string UnknownConnection = "PERSISTENCE_VALIDATION_004";

    /// <summary>
    /// 查询的取出条数超过上限
    /// </summary>
    /// <remarks>
    /// <see cref="Queries.EntityQuery{T}.Take"/> 大于 <see cref="Queries.EntityQuery.DefaultMaxTake"/>：构造查询时以异常抛出，调用里由 Transaction 阶段转为同码的校验结果（HTTP 400）。<see cref="Schema.TableListQuery.Take"/> 超过上限、一次读结构的表数超过 <see cref="Schema.SchemaLimits.MaxTablesPerRead"/> 由读结构的 Provider 以结果返回
    /// </remarks>
    public const string TakeExceeded = "PERSISTENCE_VALIDATION_005";

    /// <summary>
    /// 运行时数据源的定义不合法
    /// </summary>
    /// <remarks>
    /// 数据库类型 Provider 不认识、连接串格式错；同名的已登记实例不受影响
    /// </remarks>
    public const string DataSourceInvalid = "PERSISTENCE_VALIDATION_006";

    /// <summary>
    /// 库结构表列表的分页越界
    /// </summary>
    /// <remarks>
    /// <see cref="Schema.TableListQuery.Skip"/> 为负或 <see cref="Schema.TableListQuery.Take"/> 小于 1：不在构造时抛出，读结构的 Provider 以结果返回（HTTP 400）
    /// </remarks>
    public const string ListPageInvalid = "PERSISTENCE_VALIDATION_007";

    /// <summary>
    /// 库里已有数据的存储形式与模型不符
    /// </summary>
    /// <remarks>
    /// EF Core 在 SQLite 上由约定映射成 UTC ticks 整数的时间列，库里还是旧的文本形式（列声明不是 INTEGER，或抽样到非整数的值）：照读会静默读成错误的时刻。特性启动时以异常抛出、启动失败；参数 <c>connection</c>、<c>columns</c>（ADR-0094）
    /// </remarks>
    public const string StorageFormatMismatch = "PERSISTENCE_VALIDATION_008";

    /// <summary>
    /// 连接用了别的库的从库
    /// </summary>
    /// <remarks>
    /// 装配错误，以异常抛出：主库不同的连接共用同一个从库，读会读到别的库的数据。固定连接在启动时核对，分片连接在作用域取客户端时核对
    /// </remarks>
    public const string ReplicaMisplaced = "PERSISTENCE_VALIDATION_009";

    /// <summary>
    /// 没有活动的工作单元
    /// </summary>
    public const string NoActiveUnitOfWork = "PERSISTENCE_UNAVAILABLE_001";

    /// <summary>
    /// 存储容量已满
    /// </summary>
    public const string CapacityExceeded = "PERSISTENCE_UNAVAILABLE_002";

    /// <summary>
    /// 存储不可用
    /// </summary>
    public const string StoreUnavailable = "PERSISTENCE_UNAVAILABLE_003";

    /// <summary>
    /// 雪花标识生成器的时钟回拨超过容忍阈值（以异常抛出）
    /// </summary>
    public const string ClockMovedBackwards = "PERSISTENCE_UNAVAILABLE_004";

    /// <summary>
    /// 雪花标识生成器没有可用的机器位
    /// </summary>
    /// <remarks>
    /// 未配置且没有分布式锁、机器位已满、租约尚未取得或已丢失
    /// </remarks>
    public const string WorkerIdUnavailable = "PERSISTENCE_UNAVAILABLE_005";

    /// <summary>
    /// 数据源连不上或读取失败
    /// </summary>
    /// <remarks>
    /// 运行时数据源的测试连接与健康探测，缺省连接与运行时数据源的库结构读取；参数 <c>name</c>、<c>reason</c>（驱动消息，连接串与口令已脱敏）
    /// </remarks>
    public const string DataSourceUnavailable = "PERSISTENCE_UNAVAILABLE_006";

    /// <summary>
    /// 运行时数据源登记已满
    /// </summary>
    /// <remarks>
    /// <see cref="DataSources.DataSourceOptions.MaxCount"/>；不淘汰已有的，替换同名的不占名额
    /// </remarks>
    public const string DataSourceCapacityExceeded = "PERSISTENCE_UNAVAILABLE_007";

    /// <summary>
    /// 当前的持久化 Provider 不支持该能力
    /// </summary>
    /// <remarks>
    /// 运行时数据源与库结构读取、命名连接（实体登记到命名连接、绑定连接的工作单元）：EF Core 与内存实现。软删除实体声明了唯一索引（部分唯一索引）而所连数据库不支持部分索引：Provider 在启动时以异常抛出（SqlSugar 只支持 PostgreSQL、SQLite、SQL Server，EF Core 只支持 Npgsql、SQLite、SQL Server 的 Provider）
    /// </remarks>
    public const string NotSupported = "PERSISTENCE_UNAVAILABLE_008";

    /// <summary>
    /// 从库不可读
    /// </summary>
    /// <remarks>
    /// 只出现在健康条目里，读落到别的从库或主库上，调用不受影响；参数 <c>connection</c>、<c>index</c>、<c>reason</c>（异常类型）
    /// </remarks>
    public const string ReplicaUnavailable = "PERSISTENCE_UNAVAILABLE_009";

    /// <summary>
    /// 只读工作单元拒绝写入：操作实现的缺陷
    /// </summary>
    /// <remarks>
    /// 只读操作（<c>OperationContract.IsReadOnly</c>）或以 <see cref="UnitOfWork.UnitOfWorkOptions.IsReadOnly"/> 开启的工作单元里新增、更新、删除（含批量）。仓储写入返回结果并把工作单元记为失败，提交返回同一错误；Provider 的直接写入入口（<c>EnsureTransactionAsync</c>）以异常抛出，Transaction 阶段转为同码结果。只读操作写数据是代码缺陷，不是调用方的输入或权限：HTTP 500，按操作计入熔断（ADR-0081）
    /// </remarks>
    public const string ReadOnly = "PERSISTENCE_UNEXPECTED_001";
}
