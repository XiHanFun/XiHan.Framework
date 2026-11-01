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
- 多库（ADR-0022）：客户端可以带多个连接（`ConfigId`），每种实体落在哪个连接由 `IEntityConnectionSelector` 决定，默认实现 `SqlSugarConnectionSelector` 的规则按顺序：实体带 SqlSugar `[Tenant(configId)]` → 该连接（不存在是装配错误，抛携带 `PERSISTENCE_VALIDATION_004` 的 `FrameworkException`，不回退；缺省规则下已登记实体在启动时就校验，`shard` 除外）；实体实现 `IMultiTenant` 且客户端有 `SqlSugarConnections.Shard`（`shard`）连接 → 分片连接；其余 → 根（默认）连接。应用在装配前 `AddSingleton<IEntityConnectionSelector>` 可替换规则。`SqlSugarUnitOfWork.ConnectionFor(Type)` 暴露选择结果。
- 写入用 SqlSugar 的按对象入口 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject`（实体类型没有 `new()` 约束，用不了泛型构建器；开启差异日志的实体改走泛型构建器 `Insertable<T>` / `Updateable<T>` / `Deleteable<T>`）；批量方法一次主键 `IN` 查询取回目标行、逐项校验后把列表交给同一入口一次写入（删除按主键），不在循环里逐条执行。
- 软删除与租户过滤以表达式显式施加在每个查询上，不用全局查询过滤器；无租户的调用看不到任何多租户实体。
- 更新与删除先在事务内加行锁取回当前行（`TranLock`：`FOR UPDATE` / `UPDLOCK`；SQLite 不支持行锁，由其数据库级写锁保证），游离实例的并发戳与当前行不同返回 `PERSISTENCE_CONFLICT_001`；行被锁住，读取与写入之间别人改不了它。
- 更新把调用方实例整行写入，其中创建时间、租户与删除标记先换成当前行的值：游离实例或读出后改过的实例都不能借更新改租户、改创建时间或打删除标记。
- 软删除在取回的当前行上打标并整行写回，调用方实例随后拿到修改时间、并发戳与删除标记。
- 新增撞上主键约束（检查之后别的事务提交了同一标识，PostgreSQL 在插入时等对方提交后报错）：PostgreSQL 系语句失败后事务已中止，仓储另开一个不在事务里的连接（`CopyNew`，同一份连接配置）核对这些标识，确已存在就返回 `PERSISTENCE_CONFLICT_002`（批量时这次写入的项统一报告），否则异常原样抛出；之后工作单元只能回滚，提交回滚并返回同一错误，不会提交一个已中止的事务。
- `SqlSugarConventions.Apply(config)`：`Id` 为主键（是否自增沿用实体自己的 `[SugarColumn(IsIdentity)]`，默认不自增），`ConcurrencyStamp` 长度 32，字符串列的长度——`[StringLength(n)]`（或 `[MaxLength(n)]`）→ 长度 n，`Persistence.Abstractions` 的 `[LongText]`（或无参 `[MaxLength]`）→ 数据库的长文本类型（`StaticConfig.CodeFirst_BigString`），实体自己的 `[SugarColumn]` 已给长度或类型时不动；DataAnnotations 的 `[MaxLength]` 构造函数带 `RequiresUnreferencedCode`，AOT 分析器不放行，框架的记录只用前两种；没有注解的字符串列沿用 SqlSugar 默认（PostgreSQL 为 `varchar(255)`，超长写入整批失败），框架的数据库记录全部显式标注并有架构测试门禁，应用实体建议同样标注（ADR-0036）；`DateTimeOffset` 经转换器按 UTC 时刻往返（SqlSugar 默认按本地时间处理会改变时刻；读取按驱动交出的 `DateTime.Kind` 还原——Npgsql legacy 时间戳模式把 `timestamptz` 换算成本地时间、Kind=Local 交出，转换器换回 UTC，Utc / Unspecified 按 UTC，ADR-0037），可空值类型与可空引用类型的列可空，没有公开 setter 的属性与集合属性不映射。约定通过 `ConfigureExternalServices.EntityService` 施加，已有回调先执行。
- 索引（ADR-0072）：实体上的 `Persistence.Abstractions` `[EntityIndex]` 经 `ConfigureExternalServices.EntityNameService`（已有回调先执行，表名以它们改过的为准）转成 `SugarIndexAttribute` 加进实体信息，名字与列由 `EntityIndexes.Resolve(type, 表名)` 推出、全部升序；`InitTables` 建表后按名字查不到才 `CREATE [UNIQUE] INDEX`，已有的索引不改、不删，再次建表不重复建（PostgreSQL 把不加引号的名字折成小写，SqlSugar 按不区分大小写查，同样幂等）。表里已有重复数据时唯一索引建不出来，`InitTables` 失败、启动失败。实体自己的 `[SugarIndex]` 与之同名时以实体的为准。分表实体（`[SplitTable]`）声明 `[EntityIndex]` 抛 `NotSupportedException`：SqlSugar 按分表改写索引名时就地改动缓存的特性，名字随建表次数变化，分表实体直接用 `[SugarIndex]` 与 `{split_table}` 占位符。应用自己组合回调时用 `SqlSugarConventions.ConfigureIndexes(type, entityInfo)`。
- 唯一键重复（ADR-0072）：新增、更新、软删除的写入失败、又不是标识重复时，同样另开不在事务里的连接核对实体的唯一索引（实体信息里的唯一索引：`[EntityIndex(IsUnique = true)]` 转成的，以及实体自己的唯一 `[SugarIndex]`）——这次写入里两项同键，或库里已提交的另一行（标识不同）同键——撞上就返回 `PERSISTENCE_CONFLICT_003`（`index` 是第一个撞上的索引名），这次写入的项统一报告，工作单元标为失败、提交回滚并返回同一错误；核对不到的（与本工作单元先前未提交的行冲突、大小写不敏感排序规则下只差大小写、键含空值）异常原样抛出。分表实体不核对。
- 实体类型没有 `new()` 约束，写入经 SqlSugar 的 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject` 入口，语义与泛型构建器相同。
- 差异日志（ADR-0044）：`new SqlSugarPersistenceFeature(…) { DiffLog = type => … }` 选出的实体，仓储的插入、更新（含软删除）、物理删除与批量方法（分表实体同样）带 SqlSugar `EnableDiffLogEvent`，应用在连接配置的 `AopEvents.OnDiffLogEvent` 上收到操作类型、表、主键与前后值，`BusinessData` 是 `SqlSugarDiffLogData`（实体类型与仓储所在作用域的服务提供器）。缺省一个都不开：开启的实体每次写入多一次按主键的读取。回调在写入执行后同步触发、不等提交，调用方的 AsyncLocal 可读，当前调用经 `Services` 里的 `IInvocationAccessor` 取；事务回滚由应用按调用缓冲后再落库，缓冲时在回调里复制值（分表插入的各组复用同一个 `DiffLogModel`）。开启的实体走泛型构建器（按对象入口不能同时带差异日志与分表），须有公开无参构造函数；分表删除由框架按 `[SplitField]` 分组到表逐表删除，因为 SqlSugar 的分表删除会丢掉差异日志开关。
- 分表（ADR-0023）：带 SqlSugar `[SplitTable]` 的实体（表名含 `{year}{month}{day}` 占位符、`[SplitField]` 是按 UTC 写入的 `DateTime` 列）自动走分表入口——查询在全部过滤条件之后 `SplitTable(from, to)`（按 `EntityQuery<T>.Within` 的时间范围只扫范围内的表，没有提示扫全部分表），写入按 `[SplitField]` 的值落表（缺少的分表由 SqlSugar 建出），分表字段写入后不要再改；分表实体的写入路径不加行锁，靠并发戳；`InitTables` 对它们走 `CodeFirst.SplitTables()`。`Within` 只是扫描范围提示，不是过滤条件。
- 只读工作单元（只读操作或 `UnitOfWorkOptions(IsReadOnly: true)`）不开事务：仓储写入返回 `PERSISTENCE_UNEXPECTED_001` 并把工作单元记为失败（提交返回同一错误）；直接用客户端写入（`Insertable` / `Updateable` / `Deleteable`）前调用的 `EnsureTransactionAsync` 抛出携带它的异常，Transaction 阶段转为同码结果。不经 `EnsureTransactionAsync` 直接执行的写入不在守卫之内，也不在事务里。
- 绑定 `db`（`connection:relational`，可选）传给配置回调；缺失时回调自行决定连接。
- 租户分库：装配了 `multitenancy.sharding@1`（如 `MultiTenancy.InMemory`）时，方式一创建的客户端默认连接 `ConfigId` 为 `default`（回调里设置的 `ConfigId` 以特性为准），带分片的调用把分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）交给同一回调再建一个 `ConfigId = shard` 的连接，两者组成一个客户端：租户实体落分片库，平台目录表（租户、授予、翻译、发件箱等不带租户的实体）仍落默认库，同一调用里一起提交或回滚；其余调用只有默认连接。方式二由应用自己按 `context.GetTenantShard()` 组装连接集合，提供名为 `shard` 的连接即可让默认规则生效。分片库的表结构由应用管理。
- `XiHan:Persistence:SqlSugar:InitTables=true` 时启动按已登记的实体建表，按落点分组：启动时没有调用也就没有分片，方式一的全部实体在默认连接建表；方式二带 `[Tenant]` 的实体在各自连接建表。
- `InitTables` 加列（ADR-0040）：建缺表、加缺列，删列 / 改名 / 改类型不在契约内，宜只在开发与测试开启，生产库结构演进走迁移脚本。给已有行的表加非可空列时，PostgreSQL 上非可空的数值 / 枚举 / 布尔 / Guid 列由约定配 CLR 缺省值（`0` / `false` / 全零 Guid），SqlSugar 先加可空列、回填旧行、再设 NOT NULL；代价是此后每次 `InitTables` 它都对这些列重发一次 `SET DEFAULT`。SQLite 不配（SqlSugar 本来就加可空列、旧行读回 CLR 缺省值；配了反而走重建列流程、布尔列读回失败）。非可空字符串（SqlSugar 忽略空串缺省值）、日期时间列与其他数据库不受支持：新列声明成可空，或重建开发库 / 走迁移脚本。约定只管实体：声明在 BCL 类型（`System.*`）上的属性原样交还——SqlSugar 对承载字典更新的 `Dictionary<string, object>` 也调用实体服务，原先其 `Count` 被标成忽略列，字典更新里名为 `count` 的列被丢掉。
- 运行时数据源（ADR-0078）：特性登记 SqlSugar 的数据源 Provider，`DataSourceRegistry` 按应用记录里的定义建 `SqlSugarDataSource`，`Client`（或 `source.GetSqlSugarClient()`）是每个数据源一个的 `SqlSugarScope`（线程安全，`IsAutoCloseConnection`）。数据库类型是 SqlSugar `DbType` 的名字（不区分大小写，`Custom` 与数字不认）；登记时由驱动解析连接串，格式错即 `PERSISTENCE_VALIDATION_006`；探测与测试连接打开原生 `DbConnection` 再关闭，驱动消息脱敏后作原因。外部库不是本应用的实体：不施加 `SqlSugarConventions`、不挂过滤器与 AOP、不进工作单元，PostgreSQL 上不自动转小写（`PgSqlIsAutoToLower = false`，标识符按库里原样引用）。运行时数据源不加进作用域客户端，`default` / `shard` 与 `IEntityConnectionSelector` 不受影响。清单的配置节因此写 `XiHan:Persistence`（本包的 `XiHan:Persistence:SqlSugar` 与 `XiHan:Persistence:DataSources` 的最近公共节，ADR-0075）。
- 库结构读取与实体目录（ADR-0078）：`ISchemaReader` 读表 `DbMaintenance.GetTableInfoList`、读列 `GetColumnInfosByTableName`（同步 API，在调用线程上执行）。缺省连接在独立作用域里取客户端再 `CopyNew`，不在应用可能开着的事务里读；运行时数据源先按定义登记再另建客户端。分表按已登记实体的表名模板（`{year}` 四位，`{month}`、`{day}` 两位，不区分大小写）折叠成逻辑表。驱动与 SqlSugar 的异常翻译为 `PERSISTENCE_UNAVAILABLE_006`（消息键 `persistence.schema.read_failed`），其余异常原样抛出。`IEntityCatalog` 取 `EntityMaintenance.GetEntityInfo`（施加了约定的映射），不连库。SQLite 没有注释、`INTEGER PRIMARY KEY` 报告可空（读取时按主键改为不可空）；PostgreSQL 的注释、identity 与 serial 都能读到。
- 规约在交给 SqlSugar 前先改写（`NullableComparisonRewriter`，ADR-0035 与其修订）。
  - 可空值类型与 null 的 `==` 改写成 `SqlFunc.EqualsNull(x, null)`，译成 `(x is null)`；`!=` 改写成 `HasValue`。C# 把 `x.RevokedAt == null`（`DateTimeOffset?`、`int?` 等）编译成带运算符方法的提升比较，SqlSugar 据此生成 `WHERE revoked_at = NULL`——永假、静默返回空集，而 `string?` 的 `== null` 正确译成 `IS NULL`。改写覆盖字面量 null、闭包里求值为 null 的变量或静态成员、左右互换；与非 null 值的比较不动。
  - 逻辑非下推到叶子：双重否定消去，`!(p && q)` / `!(p || q)` 按德摩根律展开，`!(a == b)` / `!(a < b)` 换成反运算，`!x.HasValue` 换成判空。SqlSugar 在与 / 或条件右侧遇到取反作左操作数（如 `A && (!x.HasValue || B)`、`A && B && (x == null || …)`）时会漏写运算符，生成语法错误的 SQL。下推后的形式在 SQL 三值逻辑下等价。
  - 一致性套件 `List_NullableValueTypeComparedWithNull_FiltersByPresence` 与 `List_NullChecksAndNegationsNestedInAndOr_FilterAsWritten` 覆盖 SQLite，PostgreSQL 由同一套件按配置跑。
- 不提供按查询条件的批量更新 / 删除（`Updateable().SetColumns().Where()`）：它们绕过审计、并发戳与软删除协议；需要时应用直接用客户端。
- 健康项 `persistence.sqlsugar.database`：启动时同步探一次，之后按 `XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds`（默认 15）在后台重探并缓存结果——在独立作用域里取客户端对默认连接 `Ado.IsValidConnection()`，不开工作单元、不经管道；连不上条目 Degraded（`PERSISTENCE_UNAVAILABLE_003`，消息键 `persistence.store.unavailable`），应用仍在服务。

## 依赖方向

依赖 `Persistence.Abstractions`、`MultiTenancy.Abstractions`（分片绑定，能力可选）与 `SqlSugarCore`（自带各数据库驱动）。应用从 `SqlSugarCore` 换成 `SqlSugarCoreNoDrive` + 单个驱动时须整体替换，两者不能并存。

## 状态

P5：可用，通过 `Persistence.Conformance` 套件（SQLite）；多库用两个 SQLite 文件验证（平台表落默认库、租户表落分片库、失败一起回滚、`[Tenant]` 显式落点）；分表用按月分表的实体验证（落表、跨表查找、范围提示、更新 / 删除与批量）；索引在 SQLite 与 PostgreSQL（`XIHAN_TEST_POSTGRES`）上验证建出、再次建表不重复、唯一索引拒绝重复。
