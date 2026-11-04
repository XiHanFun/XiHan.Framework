# XiHan.Framework.Persistence.EntityFrameworkCore

`persistence.relational@1` 的 Entity Framework Core Provider（Ring 3，`role: provider`）。应用持有自己的 `DbContext`，框架只提供仓储、工作单元与协议约定。

```csharp
builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x.UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(
    static (binding, options) => options.UseNpgsql(binding.ValueOrThrow().Value))));

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();
        modelBuilder.ApplyPersistenceConventions(Database);   // 并发戳为并发令牌，租户与软删除建索引，[EntityIndex] 映射为 HasIndex，SQLite 上 DateTimeOffset 存成 UTC ticks
    }
}
```

## 语义

- 工作单元在首次写入时开启数据库事务（只读调用不开事务，SQLite 这类单写者数据库上读写调用能并存）；仓储的每次写入立即 `SaveChanges`（同一事务内读己之写），批量写入是一次 `IN` 查询取回目标行、逐项校验后一次 `AddRange` / 跟踪加一次 `SaveChanges`，提交即提交事务，回滚丢弃一切。开没开由 `HasTransaction` 报告（ADR-0109）：第一次 `EnsureTransactionAsync`（仓储每次写入前、提交时保存残余跟踪变更前都经它）开事务即为真，提交、回滚成功后立即释放事务对象、复位为假。
- 软删除与租户过滤在仓储查询里显式施加（`EF.Property`），不用全局查询过滤器，`Find` 与 LINQ 行为一致。
- 更新游离实体时先比对并发戳，不符返回 `PERSISTENCE_CONFLICT_001`；跟踪实体由 EF 的并发令牌保护。
- 更新与删除时创建时间、租户与删除标记回到跟踪实例的原值（`OriginalValues`），游离实例复制过来的值、直接改在跟踪实例上的值都不生效，写入路径也按原值判定可见性；提交时保存的残余变更（绕过仓储改了跟踪实体）同样不改创建时间与租户。保存成功后协议字段同步回游离实例。
- 新增撞上主键约束（检查之后别的事务提交了同一标识）：`SaveChanges` 已回到自动建的保存点，仓储把这次新增的实体移出跟踪，在同一事务里核对这些标识，确已存在就返回 `PERSISTENCE_CONFLICT_002`（批量时这次写入的项统一报告），否则异常原样抛出；之后工作单元只能回滚，提交回滚并返回同一错误。
- 只读工作单元（只读操作或 `UnitOfWorkOptions(IsReadOnly: true)`）不开事务：仓储写入返回 `PERSISTENCE_UNEXPECTED_001` 并把工作单元记为失败（提交返回同一错误）；直接用 `DbContext` 写入前调用的 `EnsureTransactionAsync` 抛出携带它的异常，Transaction 阶段转为同码结果；绕过仓储改了跟踪实体而未保存的，提交时以它回滚。读出的实体在只读操作里不要当作草稿修改。
- 唯一键重复（ADR-0072）：新增、更新、删除的 `SaveChanges` 失败、又不是标识重复时，在保存点之后的同一事务里核对模型里的唯一索引（`[EntityIndex(IsUnique = true)]` 映射的与应用自己 `HasIndex(…).IsUnique()` 的；约定建的部分唯一索引只比较未删除的写入项与未删除的行）——这次保存里两项同键，或库里另一行（标识不同）同键——撞上就把这些实体移出跟踪，返回 `PERSISTENCE_CONFLICT_003`（`index` 是第一个撞上的索引名），工作单元标为失败、提交回滚并返回同一错误；核对不到的（大小写不敏感排序规则下只差大小写、键含空值、影子属性的索引）异常原样抛出。
- 绑定 `db`（`connection:relational`，可选）传给配置回调；缺失时回调自行决定连接。
- 租户分库：装配了 `multitenancy.sharding@1`（如 `MultiTenancy.InMemory`）时，DbContext 选项按作用域构建，带分片的调用把分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）交给配置回调，其余调用用应用默认连接。分片库的表结构由应用管理，`EnsureCreated` 只作用于默认连接。
- 索引（ADR-0072）：`ApplyPersistenceConventions(Database)` 把实体上的 `Persistence.Abstractions` `[EntityIndex]` 映射为 `HasIndex(列).IsUnique(…).HasDatabaseName(名字)`，名字与列由 `EntityIndexes.Resolve(type, 表名)` 推出，与 SqlSugar 建出的同名；表名取调用时的 `GetTableName()`，`ToTable` 要在它之前配置。从属类型与共享类型实体跳过。框架的数据库记录（授予、翻译、审计、调度、工作流、发件箱）在 `modelBuilder.Entity<T>()` 之后调用它即得到索引，不用再手写 `HasIndex`。
- 软删除实体的唯一索引（ADR-0081）：只约束未删除的行，另加 `HasFilter`，条件按 `Database.ProviderName` 写方言——Npgsql `"IsDeleted" = false`、SQLite `"IsDeleted" = 0`、SQL Server `[IsDeleted] = 0`（键含可空列时另加 `IS NOT NULL`，显式条件会顶掉 SQL Server 对可空唯一索引的缺省过滤），列名取调用时的映射（`HasColumnName` 与命名约定在它之前生效）。所以 `ApplyPersistenceConventions` 要收本 DbContext 的 `Database`。其余关系型 Provider（Pomelo MySQL、Oracle 等）不支持部分唯一索引，建模时抛出（`PERSISTENCE_UNAVAILABLE_008`，一次列出全部实体），不退化成普通唯一索引；要在那些数据库上唯一约束，把 `IsDeleted` 显式写进列。非关系型 Provider（InMemory、Cosmos）本来不建索引，不加条件。
- SQLite 上 `ApplyPersistenceConventions(Database)` 把模型里全部 `DateTimeOffset` 与可空 `DateTimeOffset` 属性（含从属类型与复杂类型里的，框架表与应用实体一样）存成 UTC ticks 整数（ADR-0091）：EF Core 的 SQLite Provider 把 `DateTimeOffset` 存成文本，翻译不了大小比较与排序（`SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY clauses`），到期书签、按时间过滤与分页都要用到。写入 `UtcTicks`、读回偏移为零的同一时刻，100 纳秒精度与 SqlSugar 在 SQLite 上的 UTC 日期时间文本一致，整数的比较与排序就是时刻的比较与排序。应用已配了值转换或存储类型的属性（`HasConversion`、`ConfigureConventions` 的 `HaveConversion`）不动；其余 Provider 不动。已有的 SQLite 库：原来没配转换的列存的是 EF Core 的 `DateTimeOffset` 文本，照读会被**静默读成错误的时刻**（如 `0001-01-01T00:00:00.0002026`，SQLite 把文本开头的年份当整数）。特性启动时因此核对（ADR-0094）：默认连接一次、登记了实体的每个命名连接各一次，落在这个连接上的、由约定映射成 UTC ticks 的列，声明类型不是 INTEGER（旧库的 `TEXT`，空表也算）或抽样最早写入的 1000 行查到非整数的值（重建表时没换算）即启动失败，`PERSISTENCE_VALIDATION_008`，消息列出 `表.列 (声明类型)`。只读不改库；库文件还不存在、库里没有的表或列、应用自己配了转换的列不核对。租户分片库启动时枚举不到，在**首次绑定**时核对（ADR-0097）：工作单元开始时 DbContext 建在分片上、这个分片库还没有核对通过，就先按同样的范围与判定核对，不符即抛出携带同码的异常、工作单元保持未开始（经管道是 `KERNEL_UNEXPECTED_001`，HTTP 500，原因 `PERSISTENCE_VALIDATION_008`，消息键 `persistence.schema.shard_timestamp_format_mismatch`，参数 `tenant`、`columns`；500 而不是 400 是已定的，服务端的数据问题不回给调用方当输入错误，ADR-0099），别的租户不受影响；核对通过的分片按连接串记在进程内，之后不再查，同一分片的并发首绑只查一次；记录至多 `XiHan:Persistence:EntityFrameworkCore:VerifiedShardCapacity` 条（默认 1024，值不合法启动失败），记满淘汰最早记下的，被淘汰的分片下次绑定重新核对一次；不符、库文件还不存在、核对抛出异常都不记下，修好或建出之后下一次绑定再查，不用重启。不经工作单元直接用 DbContext 的读写不在范围内；开销每个连接一次读表结构、每张表至多 1000 行抽样（200 张表约 100 毫秒）。修法：开发库删了重建；要留数据的重建表、列建成 `INTEGER`、搬数据时用换算式把文本换成 UTC ticks（换算式与重建语句见 docs 的 EF Core 包页「已有 SQLite 库的时间列」，出处 ADR-0091 第三节）——EF Core 迁移改列类型虽然也是重建表，但原样搬数据，要换成带换算式的语句。原来自己配了转换（如 `DateTimeOffsetToBinaryConverter`）的照旧，库不用动。
- SQLite 上字符串的 `Contains` / `StartsWith` / `EndsWith` 一律译成 `LIKE … ESCAPE '\'`（ADR-0101）：EF Core 的 SQLite Provider 原来把 `Contains` 译成区分大小写的 `instr`，`StartsWith` / `EndsWith` 的实参是值时译成只对 ASCII 字母不区分大小写的 `LIKE`、是列或表达式时译成区分的 `substr` 比较，同一个库上三种规则。特性在 `DbContext` 选项上加一个查询表达式拦截器（`IQueryExpressionInterceptor`，查询编译之前把这三个方法换成本包的标记方法，只在 SQLite 上改写）与一个方法翻译插件（`IMethodCallTranslatorPlugin`，经选项扩展登记到 EF Core 内部服务容器，把标记方法译成 `LIKE`）：常量实参在翻译时转义并拼上 `%`；参数、列或表达式在 SQL 里以三层 `replace` 转义（反斜杠、`%`、`_`）、以 `||` 拼上 `%`，并要求实参不为 NULL（NULL 实参不匹配，与原来一样）。结果只对 ASCII 字母不区分大小写、其余字母区分，`%`、`_`、反斜杠按字面，与 SqlSugar 在 SQLite 上同一套规则；字符实参与字符串一样。范围是特性建出的 `DbContext` 上的全部 LINQ（仓储的规约、应用直接写的查询、投影、子查询、参数化集合），投影里落到客户端求值的部分按 .NET 原方法计算；应用自己的全局查询过滤器（`HasQueryFilter`）、不经特性建出的 `DbContext` 不经过改写，应用用 `UseInternalServiceProvider` 自给 EF Core 内部服务容器的不受支持。SQLite 的 `LIKE` 模式最长 50000 字节，`Contains` 的实参超过即报错（原来的 `instr` 没有这个限制）。PostgreSQL、SQL Server 等其余数据库照 EF Core 数据库 Provider 的翻译，本来就一套规则、按字面，不改写。
- 启动时在独立作用域里取一次模型：索引声明不合法、Provider 不支持部分唯一索引这类建模错误在启动时失败，不论是否开着 `EnsureCreated`。
- 命名连接（ADR-0087）：在 `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` 声明，值是连接串（名字合 `^[a-z0-9][a-z0-9-]{0,63}$`，不能是 `default` 或 `shard`，值不能空白、不能写成子节，否则装配即 `ArgumentException`）。一个 `DbContext` 一个连接、建出后不换：`UnitOfWorkOptions(Connection: name)` 开启的工作单元先记下连接、再建本作用域的 `DbContext`，回调收到的绑定主值是那个连接串、属性沿用 `db` 绑定，事务只开在那个库上。所以登记到命名连接的实体（`AddRepository<TEntity, TKey>(connection)`）只能在绑定那个连接的工作单元里读写：普通工作单元里取用它的仓储抛出携带 `PERSISTENCE_UNAVAILABLE_008` 的 `FrameworkException`，绑定连接的工作单元里取用落在别处的实体抛 `PERSISTENCE_VALIDATION_004`。这一半是已定的（ADR-0091）：EF Core 不做「每个连接一个 `DbContext`、跨上下文顺序提交」的多上下文工作单元，普通工作单元里取用命名连接上的实体维持明确报错；要在业务事务里一起读写命名连接上的实体，用 `Persistence.SqlSugar`。登记到没有声明的连接装配即失败、绑定没有声明的连接 `BeginAsync` 抛出，都是 `PERSISTENCE_VALIDATION_004`；作用域里已经按别的连接建出 `DbContext`（先取用了它）再绑定，`BeginAsync` 抛 `PERSISTENCE_VALIDATION_002`，在新作用域里绑定即可。`EnsureCreated` 对登记了实体的每个命名连接各执行一次，按整个模型建表（那个库里多出的空表不用）。命名连接与默认连接用同一个配置回调，是同一种数据库 Provider。工作流的租约表就这样放到另一个库：SQLite 上 `XiHan:Workflow:Database:LockConnection` 指向这里声明的连接，调用方事务里取锁不再等自己的事务。
- 运行时数据源（ADR-0078）不支持：EF Core 的数据库 Provider（`UseNpgsql`、`UseSqlServer` 等）由应用选、本包不引用，按数据库类型建连接要对应的 Provider 包。注册表 `DataSourceRegistry` 照样登记，取用与测试连接返回 `PERSISTENCE_UNAVAILABLE_008`，不返回空结果。
- 运行时数据源与库结构读取不计划支持，已定（ADR-0084）：需要代码生成的读结构、外部数据源或零代码运行时的应用用 `Persistence.SqlSugar`。
- 实体目录（ADR-0078）取 DbContext 模型：表名 `GetTableName()`（没有映射到表时取类型名），列名 `GetColumnName(表)`，影子属性与从属类型不列；登记了却不在模型里的实体抛 `InvalidOperationException`。库结构读取不支持（`PERSISTENCE_UNAVAILABLE_008`）：跨数据库读结构在 EF Core 里是设计期脚手架服务（`Microsoft.EntityFrameworkCore.Design` 的 `IDatabaseModelFactory`，按数据库 Provider 程序集反射发现），ADO.NET 的 `GetSchema` 各驱动返回的列不同、`Microsoft.Data.Sqlite` 不实现。
- `XiHan:Persistence:EntityFrameworkCore:EnsureCreated=true` 时启动即建表，只用于开发与测试；生产用迁移。
- 健康项 `persistence.entityframeworkcore.database`：启动时同步探一次，之后按 `XiHan:Persistence:EntityFrameworkCore:HealthProbeIntervalSeconds`（默认 15）在后台重探并缓存结果——在独立作用域里取默认连接的 DbContext `CanConnect`，不开工作单元、不经管道；连不上条目 Degraded（`PERSISTENCE_UNAVAILABLE_003`，消息键 `persistence.store.unavailable`），应用仍在服务。

## 依赖方向

依赖 `Persistence.Abstractions`、`MultiTenancy.Abstractions`（分片绑定，能力可选）与 `Microsoft.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.Relational`（索引名与表名是关系型元数据；各关系型数据库的 EF Core Provider 本来就依赖它）。数据库驱动包（Sqlite、Npgsql、SqlServer）由应用引用；SQLite 用 `Microsoft.EntityFrameworkCore.Sqlite` 10.0.13 或更高——它带来的 `Microsoft.Data.Sqlite.Core` 10.0.13 修复了连接池激活的竞态（dotnet/efcore#39008），更低的版本带着它。本包不依赖任何 SQLite 包，不替应用钉这个版本（ADR-0097）。

## 状态

P2：可用，通过 `Persistence.Conformance` 套件（SQLite）。
