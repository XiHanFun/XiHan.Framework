# XiHan.Framework.Persistence.SqlSugar

`persistence.relational@1` 的 SqlSugar Provider（Ring 3，`role: provider`）。面向从 4.x / BasicApp 迁移的 SqlSugar 存量代码：实体不需要继承 SqlSugar 基类或标注 `[SugarColumn]`，框架按实体协议（`IEntity<TKey>`、审计接口、租户、软删除）配置映射；应用保留自己的 `ISqlSugarClient` 用法。

```csharp
builder.Services.AddRepository<Order, Guid>();

// 方式一：由特性创建作用域内的 SqlSugarClient，自动应用协议约定
builder.AddXiHan(x => x.UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
{
    config.DbType = DbType.PostgreSQL;
    config.ConnectionString = binding.ValueOrThrow().Value;
})));

// 方式二：应用自己注册客户端（SqlSugarScope、多库、分表等），创建前先应用约定；多连接时自己给 ConfigId
builder.Services.AddSingleton<ISqlSugarClient>(_ => new SqlSugarScope(
[
    SqlSugarConventions.Apply(new ConnectionConfig { ConfigId = SqlSugarConnections.Default, DbType = DbType.MySql, ConnectionString = "…" }),
    SqlSugarConventions.Apply(new ConnectionConfig { ConfigId = "audit", DbType = DbType.MySql, ConnectionString = "…" }),
]));
builder.AddXiHan(x => x.UseFeature(new SqlSugarPersistenceFeature()));

// 实体级落点：[Tenant] 指定连接；实现 IMultiTenant 的实体在带分片的调用里落 shard 连接；其余落默认连接
[Tenant("audit")]
public sealed class AuditRecord : IEntity<Guid> { … }
```

## 语义

- 工作单元在首次写入时经 `AsTenant()` 在客户端的全部连接上 `BeginTran`（只读调用不开事务），提交即逐连接 `CommitTran`，回滚即逐连接 `RollbackTran`；之后首次触及的连接由 SqlSugar 在取连接时补开事务。跨连接是顺序提交，不是两阶段提交：后一个连接提交失败时前一个已经提交，需要严格原子性的写入放在同一个库。SqlSugar 没有变更跟踪，仓储的每次写入立即执行。
- 多库（ADR-0022）：客户端可以带多个连接（`ConfigId`），每种实体落在哪个连接由 `IEntityConnectionSelector` 决定，默认实现 `SqlSugarConnectionSelector` 的规则按顺序：实体带 SqlSugar `[Tenant(configId)]` → 该连接（不存在是配置错误，抛 `FrameworkException`，不回退）；实体实现 `IMultiTenant` 且客户端有 `SqlSugarConnections.Shard`（`shard`）连接 → 分片连接；其余 → 根（默认）连接。应用在装配前 `AddSingleton<IEntityConnectionSelector>` 可替换规则。`SqlSugarUnitOfWork.ConnectionFor(Type)` 暴露选择结果。
- 写入用 `Insertable` / `Updateable` / `Deleteable`；批量方法一次主键 `IN` 查询取回目标行、逐项校验后一次 `Insertable(list)` / `Updateable(list)` / `Deleteable().In(ids)`，不在循环里逐条执行。
- 软删除与租户过滤以表达式显式施加在每个查询上，不用全局查询过滤器；无租户的调用看不到任何多租户实体。
- 更新与删除先在事务内加行锁取回当前行（`TranLock`：`FOR UPDATE` / `UPDLOCK`；SQLite 不支持行锁，由其数据库级写锁保证），游离实例的并发戳与当前行不同返回 `PERSISTENCE_CONFLICT_001`；行被锁住，读取与写入之间别人改不了它。
- 软删除在取回的当前行上打标并整行写回，调用方实例随后拿到修改时间、并发戳与删除标记。
- `SqlSugarConventions.Apply(config)`：`Id` 为主键（是否自增沿用实体自己的 `[SugarColumn(IsIdentity)]`，默认不自增），`ConcurrencyStamp` 长度 32，字符串列的长度——`[StringLength(n)]`（或 `[MaxLength(n)]`）→ 长度 n，`Persistence.Abstractions` 的 `[LongText]`（或无参 `[MaxLength]`）→ 数据库的长文本类型（`StaticConfig.CodeFirst_BigString`），实体自己的 `[SugarColumn]` 已给长度或类型时不动；DataAnnotations 的 `[MaxLength]` 构造函数带 `RequiresUnreferencedCode`，AOT 分析器不放行，框架的记录只用前两种；没有注解的字符串列沿用 SqlSugar 默认（PostgreSQL 为 `varchar(255)`，超长写入整批失败），框架的数据库记录全部显式标注并有架构测试门禁，应用实体建议同样标注（ADR-0036）；`DateTimeOffset` 经转换器按 UTC 时刻往返（SqlSugar 默认按本地时间处理会改变时刻；读取按驱动交出的 `DateTime.Kind` 还原——Npgsql legacy 时间戳模式把 `timestamptz` 换算成本地时间、Kind=Local 交出，转换器换回 UTC，Utc / Unspecified 按 UTC，ADR-0037），可空值类型与可空引用类型的列可空，没有公开 setter 的属性与集合属性不映射。约定通过 `ConfigureExternalServices.EntityService` 施加，已有回调先执行。
- 实体类型没有 `new()` 约束，写入经 SqlSugar 的 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject` 入口，语义与泛型构建器相同。
- 差异日志（ADR-0044）：`new SqlSugarPersistenceFeature(…) { DiffLog = type => … }` 选出的实体，仓储的插入、更新（含软删除）、物理删除与批量方法（分表实体同样）带 SqlSugar `EnableDiffLogEvent`，应用在连接配置的 `AopEvents.OnDiffLogEvent` 上收到操作类型、表、主键与前后值，`BusinessData` 是 `SqlSugarDiffLogData`（实体类型与仓储所在作用域的服务提供器）。缺省一个都不开：开启的实体每次写入多一次按主键的读取。回调在写入执行后同步触发、不等提交，调用方的 AsyncLocal 可读，当前调用经 `Services` 里的 `IInvocationAccessor` 取；事务回滚由应用按调用缓冲后再落库，缓冲时在回调里复制值（分表插入的各组复用同一个 `DiffLogModel`）。开启的实体走泛型构建器（按对象入口不能同时带差异日志与分表），须有公开无参构造函数；分表删除由框架按 `[SplitField]` 分组到表逐表删除，因为 SqlSugar 的分表删除会丢掉差异日志开关。
- 分表（ADR-0023）：带 SqlSugar `[SplitTable]` 的实体（表名含 `{year}{month}{day}` 占位符、`[SplitField]` 是按 UTC 写入的 `DateTime` 列）自动走分表入口——查询在全部过滤条件之后 `SplitTable(from, to)`（按 `EntityQuery<T>.Within` 的时间范围只扫范围内的表，没有提示扫全部分表），写入按 `[SplitField]` 的值落表（缺少的分表由 SqlSugar 建出），分表字段写入后不要再改；分表实体的写入路径不加行锁，靠并发戳；`InitTables` 对它们走 `CodeFirst.SplitTables()`。`Within` 只是扫描范围提示，不是过滤条件。
- 只读工作单元在写入时就返回 `PERSISTENCE_VALIDATION_002`。
- 绑定 `db`（`connection:relational`，可选）传给配置回调；缺失时回调自行决定连接。
- 租户分库：装配了 `multitenancy.sharding@1`（如 `MultiTenancy.InMemory`）时，方式一创建的客户端默认连接 `ConfigId` 为 `default`（回调里设置的 `ConfigId` 以特性为准），带分片的调用把分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）交给同一回调再建一个 `ConfigId = shard` 的连接，两者组成一个客户端：租户实体落分片库，平台目录表（租户、授予、翻译、发件箱等不带租户的实体）仍落默认库，同一调用里一起提交或回滚；其余调用只有默认连接。方式二由应用自己按 `context.GetTenantShard()` 组装连接集合，提供名为 `shard` 的连接即可让默认规则生效。分片库的表结构由应用管理。
- `XiHan:Persistence:SqlSugar:InitTables=true` 时启动按已登记的实体建表，按落点分组：启动时没有调用也就没有分片，方式一的全部实体在默认连接建表；方式二带 `[Tenant]` 的实体在各自连接建表。
- `InitTables` 加列（ADR-0040）：建缺表、加缺列，删列 / 改名 / 改类型不在契约内，宜只在开发与测试开启，生产库结构演进走迁移脚本。给已有行的表加非可空列时，PostgreSQL 上非可空的数值 / 枚举 / 布尔 / Guid 列由约定配 CLR 缺省值（`0` / `false` / 全零 Guid），SqlSugar 先加可空列、回填旧行、再设 NOT NULL；代价是此后每次 `InitTables` 它都对这些列重发一次 `SET DEFAULT`。SQLite 不配（SqlSugar 本来就加可空列、旧行读回 CLR 缺省值；配了反而走重建列流程、布尔列读回失败）。非可空字符串（SqlSugar 忽略空串缺省值）、日期时间列与其他数据库不受支持：新列声明成可空，或重建开发库 / 走迁移脚本。约定只管实体：声明在 BCL 类型（`System.*`）上的属性原样交还——SqlSugar 对承载字典更新的 `Dictionary<string, object>` 也调用实体服务，原先其 `Count` 被标成忽略列，字典更新里名为 `count` 的列被丢掉。
- 规约里可空值类型与 null 的 `==` / `!=` 在交给 SqlSugar 前改写成 `HasValue` 判定（`NullableComparisonRewriter`，ADR-0035）：C# 把 `x.RevokedAt == null`（`DateTimeOffset?`、`int?` 等）编译成带运算符方法的提升比较，SqlSugar 据此生成 `WHERE revoked_at = NULL`——永假、静默返回空集，而 `string?` 的 `== null` 正确译成 `IS NULL`。改写覆盖字面量 null、闭包里求值为 null 的变量或静态成员、左右互换；与非 null 值的比较不动。一致性套件 `List_NullableValueTypeComparedWithNull_FiltersByPresence` 覆盖 SQLite，PostgreSQL 由同一套件按配置跑。
- 不提供按查询条件的批量更新 / 删除（`Updateable().SetColumns().Where()`）：它们绕过审计、并发戳与软删除协议；需要时应用直接用客户端。
- 健康项 `persistence.sqlsugar.database`：启动时同步探一次，之后按 `XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds`（默认 15）在后台重探并缓存结果——在独立作用域里取客户端对默认连接 `Ado.IsValidConnection()`，不开工作单元、不经管道；连不上条目 Degraded（`PERSISTENCE_UNAVAILABLE_003`，消息键 `persistence.store.unavailable`），应用仍在服务。

## 依赖方向

依赖 `Persistence.Abstractions`、`MultiTenancy.Abstractions`（分片绑定，能力可选）与 `SqlSugarCore`（自带各数据库驱动）。应用从 `SqlSugarCore` 换成 `SqlSugarCoreNoDrive` + 单个驱动时须整体替换，两者不能并存。

## 状态

P5：可用，通过 `Persistence.Conformance` 套件（SQLite）；多库用两个 SQLite 文件验证（平台表落默认库、租户表落分片库、失败一起回滚、`[Tenant]` 显式落点）；分表用按月分表的实体验证（落表、跨表查找、范围提示、更新 / 删除与批量）。
