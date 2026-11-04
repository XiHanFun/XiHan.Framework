# XiHan.Framework.Persistence.SqlSugar

`persistence.relational@1` 的 SqlSugar Provider（Ring 3，`role: provider`）。面向已有的 SqlSugar 代码：实体不需要继承 SqlSugar 基类或标注 `[SugarColumn]`，框架按实体协议（`IEntity<TKey>`、审计接口、租户、软删除）配置映射；应用保留自己的 `ISqlSugarClient` 用法。

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

- 工作单元在首次写入时经 `AsTenant()` 在客户端的全部连接上 `BeginTran`（只读调用不开事务），提交即逐连接 `CommitTran`，回滚即逐连接 `RollbackTran`；之后首次触及的连接由 SqlSugar 在取连接时补开事务。开没开由 `HasTransaction` 报告（ADR-0109）：第一次 `EnsureTransactionAsync`（仓储每次写入前都经它）开事务即为真，多连接时一个值，提交、回滚或作用域释放时复位；绑定命名连接的工作单元只报告那个连接。跨连接是顺序提交，不是两阶段提交：后一个连接提交失败时前一个已经提交，需要严格原子性的写入放在同一个库。SqlSugar 没有变更跟踪，仓储的每次写入立即执行。
- 多库（ADR-0022）：客户端可以带多个连接（`ConfigId`），每种实体落在哪个连接由 `IEntityConnectionSelector` 决定，默认实现 `SqlSugarConnectionSelector`（构造收 `RepositoryRegistrations`）的规则按顺序：实体带 SqlSugar `[Tenant(configId)]` → 该连接；以 `AddRepository<TEntity, TKey>(connection)` 登记到命名连接 → 同名连接（两者都有而不同、或连接不存在是装配错误，抛携带 `PERSISTENCE_VALIDATION_004` 的 `FrameworkException`，不回退；缺省规则下已登记实体在启动时就校验，`shard` 除外）；实体实现 `IMultiTenant` 且客户端有 `SqlSugarConnections.Shard`（`shard`）连接 → 分片连接；其余 → 根（默认）连接。应用在装配前 `AddSingleton<IEntityConnectionSelector>` 可替换规则。`SqlSugarUnitOfWork.ConnectionFor(Type)` 暴露选择结果。
- 命名连接（ADR-0084）：方式一在 `XiHan:Persistence:SqlSugar:Connections:<name>` 声明，值是连接串（名字合 `^[a-z0-9][a-z0-9-]{0,63}$`，不能是 `default` 或 `shard`，值不能空白；不合法装配即抛 `ArgumentException`，消息不带连接串）；每个作用域的客户端都带上它们，排在默认连接之后，回调对每个各调用一次——绑定主值是该连接串、属性沿用 `db` 绑定，`config.ConfigId` 是连接名。方式二写了这一节装配即失败（应用自己把同名 `ConfigId` 的连接加进客户端）。工作单元首次写入时只在默认连接与已经取用过的连接上开事务，业务调用不碰命名连接时那个库不被业务事务锁住；`UnitOfWorkOptions(Connection: name)` 开启的工作单元只用那个连接（`GetConnection(name)` 上 `Ado.BeginTran` / `CommitTran` / `RollbackTran`，不经 `AsTenant()`），操作落在别处的实体抛 `PERSISTENCE_VALIDATION_004`，连接不在客户端里 `BeginAsync` 抛同码异常。SQLite 上的工作流租约表靠它放到另一个库文件（`XiHan:Workflow:Database:LockConnection`）。
- 写入用 SqlSugar 的按对象入口 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject`（实体类型没有 `new()` 约束，用不了泛型构建器；开启差异日志的实体改走泛型构建器 `Insertable<T>` / `Updateable<T>` / `Deleteable<T>`）；批量方法一次主键 `IN` 查询取回目标行、逐项校验后把列表交给同一入口一次写入（删除按主键），不在循环里逐条执行。
- 软删除与租户过滤以表达式显式施加在每个查询上，不用全局查询过滤器；无租户的调用看不到任何多租户实体。
- 更新与删除先在事务内加行锁取回当前行（`TranLock`：`FOR UPDATE` / `UPDLOCK`；SQLite 不支持行锁，由其数据库级写锁保证），游离实例的并发戳与当前行不同返回 `PERSISTENCE_CONFLICT_001`；行被锁住，读取与写入之间别人改不了它。
- 更新把调用方实例整行写入，其中创建时间、租户与删除标记先换成当前行的值：游离实例或读出后改过的实例都不能借更新改租户、改创建时间或打删除标记。
- 软删除在取回的当前行上打标并整行写回，调用方实例随后拿到修改时间、并发戳与删除标记。
- 新增撞上主键约束（检查之后别的事务提交了同一标识，PostgreSQL 在插入时等对方提交后报错）：PostgreSQL 系语句失败后事务已中止，仓储另开一个不在事务里的连接（`CopyNew`，同一份连接配置）核对这些标识，确已存在就返回 `PERSISTENCE_CONFLICT_002`（批量时这次写入的项统一报告），否则异常原样抛出；之后工作单元只能回滚，提交回滚并返回同一错误，不会提交一个已中止的事务。
- `SqlSugarConventions.Apply(config)`：`Id` 为主键（是否自增沿用实体自己的 `[SugarColumn(IsIdentity)]`，默认不自增），`ConcurrencyStamp` 长度 32，字符串列的长度——`[StringLength(n)]`（或 `[MaxLength(n)]`）→ 长度 n，`Persistence.Abstractions` 的 `[LongText]`（或无参 `[MaxLength]`）→ 数据库的长文本类型（`StaticConfig.CodeFirst_BigString`），实体自己的 `[SugarColumn]` 已给长度或类型时不动；DataAnnotations 的 `[MaxLength]` 构造函数带 `RequiresUnreferencedCode`，AOT 分析器不放行，框架的记录只用前两种；没有注解的字符串列沿用 SqlSugar 默认（PostgreSQL 为 `varchar(255)`，超长写入整批失败），框架的数据库记录全部显式标注并有架构测试门禁，应用实体建议同样标注（ADR-0036）；`DateTimeOffset` 经转换器按 UTC 时刻往返（SqlSugar 默认按本地时间处理会改变时刻；读取按驱动交出的 `DateTime.Kind` 还原——Npgsql legacy 时间戳模式把 `timestamptz` 换算成本地时间、Kind=Local 交出，转换器换回 UTC，Utc / Unspecified 按 UTC，ADR-0037），可空值类型与可空引用类型的列可空，没有公开 setter 的属性与集合属性不映射。约定通过 `ConfigureExternalServices.EntityService` 施加，已有回调先执行。
- 索引（ADR-0072）：实体上的 `Persistence.Abstractions` `[EntityIndex]` 经 `ConfigureExternalServices.EntityNameService`（已有回调先执行，表名以它们改过的为准）转成 `SugarIndexAttribute` 加进实体信息，名字与列由 `EntityIndexes.Resolve(type, 表名)` 推出、全部升序；`InitTables` 建表后按名字查不到才 `CREATE [UNIQUE] INDEX`，已有的索引不改、不删，再次建表不重复建（PostgreSQL 把不加引号的名字折成小写，SqlSugar 按不区分大小写查，同样幂等）。表里已有重复数据时唯一索引建不出来，`InitTables` 失败、启动失败。实体自己的 `[SugarIndex]` 与之同名时以实体的为准。分表实体（`[SplitTable]`）声明 `[EntityIndex]` 抛 `NotSupportedException`：SqlSugar 按分表改写索引名时就地改动缓存的特性，名字随建表次数变化，分表实体直接用 `[SugarIndex]` 与 `{split_table}` 占位符。应用自己组合回调时用 `SqlSugarConventions.ConfigureIndexes(type, entityInfo)`。
- 软删除实体的唯一索引（ADR-0081）：只约束未删除的行，是部分唯一索引。SqlSugar 的索引入口写不出条件，它不交给 `SugarIndexAttribute`，由 `SqlSugarConventions.InitTables(client, entityTypes)` 在建表之后按方言补建——PostgreSQL、SQLite 用 `CREATE UNIQUE INDEX IF NOT EXISTS … WHERE "IsDeleted" = false / 0`（PostgreSQL 上名字同样是小写），SQL Server 查 `sys.indexes` 后建筛选索引 `WHERE [IsDeleted] = 0`（键含可空列时另加 `IS NOT NULL`）；已有同名的不改、不删。特性开着 `InitTables` 时经它建表；应用自己建表（租户独立库、迁移工具）也要调它，直接调 `CodeFirst.InitTables` 建不出部分唯一索引。支持的数据库只有 PostgreSQL、SQLite、SQL Server：MySQL 系（含 TiDB、OceanBase、Doris、GoldenDB、TDSQL）、Oracle 系（含达梦、OceanBase Oracle 模式）、ClickHouse、QuestDB、TDengine、DuckDB、Access、DB2、HANA 等没有部分索引，源自 PostgreSQL 的人大金仓、openGauss、GaussDB、瀚高、Vastbase、PolarDB 没有验证过，都按不支持处理（已定，ADR-0084：在真实库上验证后才加进名单）——已登记的软删除实体声明了唯一索引（列里没有 `IsDeleted`）而它落的连接是这些数据库时，启动即失败（`PERSISTENCE_UNAVAILABLE_008`，一次列出全部实体），不论是否开着 `InitTables`；`InitTables(client, …)` 在建任何表之前同样抛出。要在这些数据库上唯一约束，把 `IsDeleted` 显式写进列（普通唯一索引，同一业务键最多一行已删除）。库里已有同名的带 `IsDeleted` 列的唯一索引时，`IF NOT EXISTS` 会跳过，要先手动删掉那个索引。
- 唯一键重复（ADR-0072）：新增、更新、软删除的写入失败、又不是标识重复时，同样另开不在事务里的连接核对实体的唯一索引（实体信息里的唯一索引：`[EntityIndex(IsUnique = true)]` 转成的，以及实体自己的唯一 `[SugarIndex]`；加上软删除实体的部分唯一索引，只比较未删除的写入项与未删除的行）——这次写入里两项同键，或库里已提交的另一行（标识不同）同键——撞上就返回 `PERSISTENCE_CONFLICT_003`（`index` 是第一个撞上的索引名），这次写入的项统一报告，工作单元标为失败、提交回滚并返回同一错误；核对不到的（与本工作单元先前未提交的行冲突、大小写不敏感排序规则下只差大小写、键含空值）异常原样抛出。分表实体不核对。
- 实体类型没有 `new()` 约束，写入经 SqlSugar 的 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject` 入口，语义与泛型构建器相同。
- 差异日志（ADR-0044）：`new SqlSugarPersistenceFeature(…) { DiffLog = type => … }` 选出的实体，仓储的插入、更新（含软删除）、物理删除与批量方法（分表实体同样）带 SqlSugar `EnableDiffLogEvent`，应用在连接配置的 `AopEvents.OnDiffLogEvent` 上收到操作类型、表、主键与前后值，`BusinessData` 是 `SqlSugarDiffLogData`（实体类型与仓储所在作用域的服务提供器）。缺省一个都不开：开启的实体每次写入多一次按主键的读取。回调在写入执行后同步触发、不等提交，调用方的 AsyncLocal 可读，当前调用经 `Services` 里的 `IInvocationAccessor` 取；事务回滚由应用按调用缓冲后再落库，缓冲时在回调里复制值（分表插入的各组复用同一个 `DiffLogModel`）。开启的实体走泛型构建器（按对象入口不能同时带差异日志与分表），须有公开无参构造函数；分表删除由框架按 `[SplitField]` 分组到表逐表删除，因为 SqlSugar 的分表删除会丢掉差异日志开关。
- 分表（ADR-0023）：带 SqlSugar `[SplitTable]` 的实体（表名含 `{year}{month}{day}` 占位符、`[SplitField]` 是按 UTC 写入的 `DateTime` 列）自动走分表入口——查询在全部过滤条件之后 `SplitTable(from, to)`（按 `EntityQuery<T>.Within` 的时间范围只扫范围内的表，没有提示扫全部分表），写入按 `[SplitField]` 的值落表（缺少的分表由 SqlSugar 建出），分表字段写入后不要再改；分表实体的写入路径不加行锁，靠并发戳；`InitTables` 对它们走 `CodeFirst.SplitTables()`。`Within` 只是扫描范围提示，不是过滤条件。
- 只读工作单元（只读操作或 `UnitOfWorkOptions(IsReadOnly: true)`）不开事务：仓储写入返回 `PERSISTENCE_UNEXPECTED_001` 并把工作单元记为失败（提交返回同一错误）；直接用客户端写入（`Insertable` / `Updateable` / `Deleteable`）前调用的 `EnsureTransactionAsync` 抛出携带它的异常，Transaction 阶段转为同码结果。不经 `EnsureTransactionAsync` 直接执行的写入不在守卫之内，也不在事务里。
- 绑定 `db`（`connection:relational`，可选）传给配置回调；缺失时回调自行决定连接。
- 租户分库：装配了 `multitenancy.sharding@1`（如 `MultiTenancy.InMemory`）时，方式一创建的客户端默认连接 `ConfigId` 为 `default`（回调里设置的 `ConfigId` 以特性为准），带分片的调用把分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）交给同一回调再建一个 `ConfigId = shard` 的连接，两者组成一个客户端：租户实体落分片库，平台目录表（租户、授予、翻译、发件箱等不带租户的实体）仍落默认库，同一调用里一起提交或回滚；其余调用只有默认连接。方式二由应用自己按 `context.GetTenantShard()` 组装连接集合，提供名为 `shard` 的连接即可让默认规则生效。分片库的表结构由应用管理。
- `XiHan:Persistence:SqlSugar:InitTables=true` 时启动按已登记的实体建表，按落点分组：启动时没有调用也就没有分片，租户实体在默认连接建表；带 `[Tenant]` 或登记到命名连接的实体在各自连接建表。每个连接经 `SqlSugarConventions.InitTables` 建表，软删除实体的部分唯一索引随之建出。
- `InitTables` 加列（ADR-0040）：建缺表、加缺列，删列 / 改名 / 改类型不在契约内，宜只在开发与测试开启，生产库结构演进走迁移脚本。给已有行的表加非可空列时，PostgreSQL 上非可空的数值 / 枚举 / 布尔 / Guid 列由约定配 CLR 缺省值（`0` / `false` / 全零 Guid），SqlSugar 先加可空列、回填旧行、再设 NOT NULL；代价是此后每次 `InitTables` 它都对这些列重发一次 `SET DEFAULT`。SQLite 不配（SqlSugar 本来就加可空列、旧行读回 CLR 缺省值；配了反而走重建列流程、布尔列读回失败）。非可空字符串（SqlSugar 忽略空串缺省值）、日期时间列与其他数据库不受支持：新列声明成可空，或重建开发库 / 走迁移脚本。约定只管实体：声明在 BCL 类型（`System.*`）上的属性原样交还——SqlSugar 对承载字典更新的 `Dictionary<string, object>` 也调用实体服务，否则其 `Count` 被标成忽略列，字典更新里名为 `count` 的列被丢掉。
- 命令超时（RFC-0005「命令超时」，ADR-0119）：`XiHan:Persistence:SqlSugar:CommandTimeoutSeconds`，缺省 300，与 SqlSugar 的缺省值相同。设在特性建的客户端的每个连接上；不是正整数即启动失败，0 不接受（ADO.NET 的 0 是无限等待）；应用自己注册客户端时配了与缺省不同的值也启动失败。个别长任务在调用点用 `Ado.CommandTimeOut` 临时调大。
- 读写分离（RFC-0005「读写分离」，ADR-0118）：回调在 `ConnectionConfig.SlaveConnectionConfigs` 上给出从库，只给它所属的连接（看 `ConfigId`）。配了从库的连接每个作用域一份配置副本：SqlSugar 读从库时会改配置上的连接串，共用时别的作用域的写入会落到从库上。只读工作单元读从库，设了 `UnitOfWorkOptions.ReadsPrimary` 的除外（ADR-0122），其余工作单元读写都走主库，提交之后同一作用域的读也在主库上。默认连接与命名连接的从库周期探测（`XiHan:Persistence:SqlSugar:Replicas`，缺省打开），不可读的摘除，全部不可读时读回到主库，健康项 `persistence.sqlsugar.replicas`；探通之后要过了冷却窗口才恢复。权重不大于 0 的归一化。分片连接的从库由回调按分片绑定给出，不探测；主库不同的连接共用从库是 `PERSISTENCE_VALIDATION_009`。没配从库时没有任何额外开销。
- 运行时数据源（ADR-0078）：特性登记 SqlSugar 的数据源 Provider，`DataSourceRegistry` 按应用记录里的定义建 `SqlSugarDataSource`，`Client`（或 `source.GetSqlSugarClient()`）是每个数据源一个的 `SqlSugarScope`（线程安全，`IsAutoCloseConnection`）。数据库类型是 SqlSugar `DbType` 的名字（不区分大小写，`Custom` 与数字不认）；登记时由驱动解析连接串，格式错即 `PERSISTENCE_VALIDATION_006`；探测与测试连接打开原生 `DbConnection` 再关闭，驱动消息脱敏后作原因。外部库不是本应用的实体：不施加 `SqlSugarConventions`、不挂过滤器与 AOP、不进工作单元，PostgreSQL 上不自动转小写（`PgSqlIsAutoToLower = false`，标识符按库里原样引用）。运行时数据源不加进作用域客户端，`default` / `shard` 与 `IEntityConnectionSelector` 不受影响。清单的配置节因此写 `XiHan:Persistence`（本包的 `XiHan:Persistence:SqlSugar` 与 `XiHan:Persistence:DataSources` 的最近公共节，ADR-0075）。
- 库结构读取与实体目录（ADR-0078）：`ISchemaReader` 读表 `DbMaintenance.GetTableInfoList`、读列 `GetColumnInfosByTableName`（同步 API，在调用线程上执行）。缺省连接在独立作用域里取客户端再 `CopyNew`，不在应用可能开着的事务里读；运行时数据源先按定义登记再另建客户端。分表按已登记实体的表名模板（`{year}` 四位，`{month}`、`{day}` 两位，不区分大小写）折叠成逻辑表。驱动与 SqlSugar 的异常翻译为 `PERSISTENCE_UNAVAILABLE_006`（消息键 `persistence.schema.read_failed`），其余异常原样抛出。`IEntityCatalog` 取 `EntityMaintenance.GetEntityInfo`（施加了约定的映射），不连库。SQLite 没有注释、`INTEGER PRIMARY KEY` 报告可空（读取时按主键改为不可空）；PostgreSQL 的注释、identity 与 serial 都能读到。MySQL、SQL Server、Oracle 在真实库上实测（ADR-0084，`XIHAN_TEST_MYSQL`、`XIHAN_TEST_SQLSERVER`、`XIHAN_TEST_ORACLE`）：类型名、主键、自增、可空、注释、缺省值原文（MySQL `x`、SQL Server `('x')`、Oracle `'x'`）、大小写混合的表名、分表折叠与分页总数都对；长度只给字符与二进制类型的声明长度，显示宽度（`tinyint(1)`）、小数秒位数（`datetime(6)`）、SQL Server 报告的 `int` 10 / `float` 53 不算长度。Oracle 的读列不用 `GetColumnInfosByTableName`（`GetSchemaTable` 给 CLR 类型名、读不到 identity、缺省值错位、大小写混合的表名被转成大写而查不到），改由 `OracleColumns` 查连接用户自己的 `USER_TAB_COLUMNS`、`USER_COL_COMMENTS`、`USER_CONSTRAINTS` / `USER_CONS_COLUMNS`（要 12c 及以上，字符类型按字符数）；运行时数据源在 Oracle 上 `IsAutoToUpper = false`。不支持（已定，ADR-0087）：Oracle 11g 及更早的版本读列以 `PERSISTENCE_UNAVAILABLE_006` 失败（数据字典没有 `IDENTITY_COLUMN`），不做 11g 的分支；达梦、OceanBase Oracle 模式等 Oracle 兼容库走 SqlSugar 原来的读列（类型名可能是 CLR 类型名，identity、缺省值、大小写混合的表名可能读不对），没有验证，读到的结构不作保证。
- PostgreSQL 的连接固定 `standard_conforming_strings = on`（ADR-0100）：SqlSugar 在多行插入、批量更新（`UPDATE … FROM (VALUES …)`）、按实体删除（按主键 `IN`，单个实体也是）、`In(…)`、集合 `Contains`、`Storageable` 里把字符串内联成 `'…'` 字面量、只把单引号写成两个，这只在 on（PostgreSQL 9.1 起的缺省）时正确；库、角色或服务器设成 off 时，含 `\'` 的值报 42601，`a\b`、`line\n` 被静默写成退格与换行、主键随之变成别的值，批量更新、删除与 `In` 静默漏行。所以 `SqlSugarConventions.Apply` 在数据库类型为 PostgreSQL 的连接串与从库连接串（`SlaveConnectionConfigs`）的启动参数 `Options` 里追加 `-c standard_conforming_strings=on`（已有的参数保留）：服务器在建连时施加，优先于库与角色上的缺省值，连接池复用时的重置也回到它。连接串已显式写成 on 以外的值时 `Apply` 抛 `ArgumentException`（方式一装配即失败），不静默改写。连接串经 `DbConnectionStringBuilder` 重新序列化（键变成小写、含分号的值加引号），Npgsql 解析结果相同。方式二要在 `Apply` 之前设好数据库类型与连接串。影响：经这个配置建出的全部连接（含应用自己的原生 SQL）都是 on，原来设成 off 的库上，原生 SQL 普通字面量里的反斜杠不再是转义符（要转义用 `E'…'`）；会话里自己 `SET standard_conforming_strings = off` 的不在支持之内；经 PgBouncer 连接要 1.20 及以上（按其文档 1.20 起接受启动参数 `options`，没有实测），更早的版本拒绝这个启动参数。PostgreSQL 系其他 `DbType`（人大金仓、openGauss 等）与运行时数据源不加。参数化的路径（单行插入与更新、`UseParameter()`、`Fastest` 的 `COPY`、规约里的比较与字符串匹配）本来就不受影响。
- MySQL 上的已知限制（已定，ADR-0100 修订；SqlSugar 上游缺陷，已报上游 [DotNetNext/SqlSugar#1460](https://github.com/DotNetNext/SqlSugar/issues/1460)，待上游修复）：SqlSugar 在两个及以上值的 `In(…)`、按实体删除（`Deleteable(entity)` / `DeleteableByObject(list)`，单个实体也是）与 `Deleteable<T>().In(…)` 里把字符串内联成只把单引号写成两个的字面量，在集合 `Contains`（`Where(x => list.Contains(x.Col))`）里则先把反斜杠写成两个。MySQL 缺省 sql_mode 下反斜杠是字面量里的转义符：前一组对含反斜杠的值静默漏行（`a\b` 被读成 a 加退格），值里有 `\'` 时报语法错误；开了 `NO_BACKSLASH_ESCAPES` 时反过来，后一组静默漏行。参数化的路径——逐个 `==`、只有一个值的 `In`（`WHERE col = @InPara0`）、单行写入、多行插入（MySQL 上字符串走参数）——两种模式下都对。本机 `mysql:9.7` 实测：缺省模式下 `In` 与三种按主键删除 4 个值只命中 2 个、集合 `Contains` 4 个都对；`NO_BACKSLASH_ESCAPES` 下反过来。
  - 仓储里走前一组路径的：`FindManyAsync`（两个及以上标识）、`AddRangeAsync` 的标识预查、`UpdateRangeAsync` 与 `RemoveRangeAsync` 的加锁取回、物理删除的 `RemoveAsync` / `RemoveRangeAsync`；走后一组的：规约里的集合 `Contains`。软删除实体的 `RemoveAsync`、`FindAsync`、`AddAsync`、`UpdateAsync` 按单个标识用参数，不受影响。
  - 框架自己的字符串主键——租户（`TenantIdentifier`）、调度（`JobName` 等）、工作流定义——是不含反斜杠与引号的受限标识符，不受影响；Guid 与整数主键不受影响。框架不在仓储里绕开（用户已定），等上游修复。
  - 应用在 MySQL 上用字符串主键时，首选在写入前校验、不让主键含反斜杠（例如沿用框架标识符的 `^[A-Za-z0-9][A-Za-z0-9._-]*$`）；做不到时改用参数化的写法：
  ```csharp
  // db：本作用域的 ISqlSugarClient；documents：IRepository<Doc, string>；unitOfWork：本作用域的 SqlSugarUnitOfWork
  // 多个标识的查找：缺省 sql_mode 下用集合 Contains（SqlSugar 对 MySQL 把反斜杠写成两个），不用 In / FindManyAsync
  var docs = await db.Queryable<Doc>().Where(d => ids.Contains(d.Id)).ToListAsync();
  var viaRepository = await documents.ListAsync(EntityQuery.All<Doc>().Where(new Specification<Doc>(d => ids.Contains(d.Id))), ct);
  // 开了 NO_BACKSLASH_ESCAPES 的反过来：用 db.Queryable<Doc>().In(ids)，不用集合 Contains

  // 两种模式都对的写法：逐个按 == 比较（参数）
  var one = await documents.FindAsync(id, ct);
  await unitOfWork.EnsureTransactionAsync(ct); // 在调用的工作单元里直接用客户端写入之前
  await db.Deleteable<Doc>().Where(d => d.Id == id).ExecuteCommandAsync(); // 代替 RemoveAsync 的物理删除与 Deleteable(entity)
  ```
  - 开了 `NO_BACKSLASH_ESCAPES` 的服务器还要在连接串里写 `NoBackslashEscapes=true`：MySqlConnector 不探测服务器模式，不写时它按缺省模式给所有参数里的反斜杠加引号（ADR-0098）。
- 规约在交给 SqlSugar 前先改写（`NullableComparisonRewriter`，ADR-0035 与其修订）。
  - 可空值类型与 null 的 `==` 改写成 `SqlFunc.EqualsNull(x, null)`，译成 `(x is null)`；`!=` 改写成 `HasValue`。C# 把 `x.RevokedAt == null`（`DateTimeOffset?`、`int?` 等）编译成带运算符方法的提升比较，SqlSugar 据此生成 `WHERE revoked_at = NULL`——永假、静默返回空集，而 `string?` 的 `== null` 正确译成 `IS NULL`。改写覆盖字面量 null、闭包里求值为 null 的变量或静态成员、左右互换；与非 null 值的比较不动。
  - 逻辑非下推到叶子：双重否定消去，`!(p && q)` / `!(p || q)` 按德摩根律展开，`!(a == b)` / `!(a < b)` 换成反运算，`!x.HasValue` 换成判空。SqlSugar 在与 / 或条件右侧遇到取反作左操作数（如 `A && (!x.HasValue || B)`、`A && B && (x == null || …)`）时会漏写运算符，生成语法错误的 SQL。下推后的形式在 SQL 三值逻辑下等价。
  - 一致性套件 `List_NullableValueTypeComparedWithNull_FiltersByPresence` 与 `List_NullChecksAndNegationsNestedInAndOr_FilterAsWritten` 覆盖 SQLite，PostgreSQL 由同一套件按配置跑。
- 字符串匹配按字面（ADR-0098）：`string.Contains` / `StartsWith` / `EndsWith` 由 `SqlSugarConventions.Apply` 登记的同名外部函数（`ConfigureExternalServices.SqlFuncServices`）接管翻译——SqlSugar 解析方法调用时先按名字查外部函数，所以经仓储的规约、直接用施加了约定的客户端的查询、删除条件、投影与子查询都一样；集合的 `Contains`（`IN`）不受影响。SqlSugar 原来译成不转义的 `LIKE`：`t_2` 命中 `budgetx2026`、`0%` 命中 `500 off`，SQL Server 上 `[a]` 是字符类，PostgreSQL 与 MySQL 的 `LIKE` 缺省以反斜杠转义（`a\b` 命中 `ab`，PostgreSQL 上以反斜杠结尾的实参报 22025）。
  - 在 SQLite、PostgreSQL、MySQL、SQL Server、Oracle 上（按连接配置的 `DbType`）译成 `LIKE … ESCAPE '!'`：实参是值（常量、闭包变量、字符）时，SqlSugar 为它建的参数值先转义、再拼上通配符，SQL 里只有参数名；实参是列或表达式时在 SQL 里以 `REPLACE` 转义，拼接写法与原来相同（PostgreSQL / MySQL `concat`，SQL Server `+`，SQLite / Oracle `||`），实参为 NULL 的结果不变。转义 `%`、`_` 与 `!`，SQL Server 另转义 `[`；Oracle 的转义符后面只能跟 `%`、`_` 或它自己（ORA-01424），其余库不转义 `[`。转义符不用反斜杠，SQL 里不出现反斜杠，结果与 MySQL 的 `NO_BACKSLASH_ESCAPES`、PostgreSQL 的 `standard_conforming_strings` 无关（MySqlConnector 不探测服务器模式，开了 `NO_BACKSLASH_ESCAPES` 的服务器要在连接串里写 `NoBackslashEscapes=true`，否则驱动给所有参数里的反斜杠加错引号）。连接配置开了 `MoreSettings.EnableILike` 时照 SqlSugar 原样用 `ilike`。
  - 大小写与重音照各库，不统一：SQLite 只对 ASCII 字母不区分大小写，PostgreSQL 与 Oracle 区分，MySQL 与 SQL Server 按列的排序规则（缺省 `utf8mb4_0900_ai_ci` 不区分大小写与重音、`SQL_Latin1_General_CP1_CI_AS` 不区分大小写）。带 `StringComparison` 的重载照 SqlSugar 原样忽略比较方式。
  - 需要通配的查询用 `SqlFunc.Like(x.Name, pattern)`，模式由调用方给出、不转义。SQLite 上 SqlSugar 原有的启发式（值里有 `\%` 或 `\_` 时加 `escape '\'`）不再生效，`\%` 按字面是两个字符。
  - 其余数据库（MySQL、PostgreSQL、Oracle 的兼容库——TiDB、OceanBase、openGauss、GaussDB、人大金仓、达梦等——与 ClickHouse 等）没有在真实库上验证，维持 SqlSugar 原来的翻译，按通配匹配（`%`、`_` 是通配符，PostgreSQL 系的兼容库上以反斜杠结尾的实参可能报错）；某个库在真实库上验证了 `ESCAPE '!'` 与转义规则之后再加进名单，与部分唯一索引的名单同一做法。运行时数据源不施加约定，同样按通配匹配。这两条是已定的（ADR-0101）。应用在调用 `Apply` 之前自己登记了同名外部函数的，以应用的为准；之后整体替换 `SqlFuncServices` 列表会丢掉这三个翻译。
  - 一致性套件 `List_StringContainsStartsWithEndsWith_MatchWildcardCharactersLiterally` 覆盖 SQLite 与 PostgreSQL；本包测试在 SQLite（不设门控）与 PostgreSQL、MySQL、SQL Server、Oracle（`XIHAN_TEST_*` 门控）上跑经仓储与经客户端的同一组用例，另验证 PostgreSQL 的 `standard_conforming_strings = off` 与 MySQL 的 `NO_BACKSLASH_ESCAPES` 会话。
- 不提供按查询条件的批量更新 / 删除（`Updateable().SetColumns().Where()`）：它们绕过审计、并发戳与软删除协议；需要时应用直接用客户端。
- 健康项 `persistence.sqlsugar.database`：启动时同步探一次，之后按 `XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds`（默认 15）在后台重探并缓存结果——对默认连接走一次数据库往返（打开连接并执行一条语句，ADR-0123），不开工作单元、不经管道，单次上限就是这个间隔；连不上或超时条目 Degraded（`KERNEL_UNAVAILABLE_005`，`reason` 写异常类型，完整异常进日志），应用仍在服务。

## 依赖方向

依赖 `Persistence.Abstractions`、`MultiTenancy.Abstractions`（分片绑定，能力可选）与 `SqlSugarCore`（自带各数据库驱动）。应用从 `SqlSugarCore` 换成 `SqlSugarCoreNoDrive` + 单个驱动时须整体替换，两者不能并存。

另显式依赖 `Microsoft.Data.Sqlite` 10.0.14（ADR-0097）：`SqlSugarCore` 5.1.4.221 声明的是 `>= 10.0.11`，NuGet 按下限解析，SQLite 上就带着连接池激活的竞态（dotnet/efcore#39008，10.0.13 修复）——并发打开连接时两个连接可能共用一个原生连接（"cannot start a transaction within a transaction"），清池时正在打开的连接被释放（`ObjectDisposedException`）。显式依赖写进本包的 nuspec，引用本包的应用也解析到 10.0.13（连带 `Microsoft.Data.Sqlite.Core` 10.0.13、`SQLitePCLRaw` 2.1.13）；应用自己直接引用了更低版本时 NuGet 报降级 `NU1605`，删掉那个引用或抬到 10.0.13。不用 SQLite 的应用不多出依赖：`SqlSugarCore` 本来就带着它。

## 状态

P5：可用，通过 `Persistence.Conformance` 套件（SQLite）；多库用两个 SQLite 文件验证（平台表落默认库、租户表落分片库、失败一起回滚、`[Tenant]` 显式落点）；分表用按月分表的实体验证（落表、跨表查找、范围提示、更新 / 删除与批量）；索引在 SQLite 与 PostgreSQL（`XIHAN_TEST_POSTGRES`）上验证建出、再次建表不重复、唯一索引拒绝重复；字符串按字面匹配在 SQLite、PostgreSQL 17、MySQL 9.7、SQL Server 2022、Oracle Free 23 上验证（ADR-0098）；PostgreSQL 库与整个服务器设成 `standard_conforming_strings = off` 时批量写入、按主键的查找与删除照样保留反斜杠、单引号与换行（ADR-0100，`XIHAN_TEST_POSTGRES`）。
