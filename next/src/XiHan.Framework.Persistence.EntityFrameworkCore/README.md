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
        modelBuilder.ApplyPersistenceConventions(Database);   // 并发戳为并发令牌，租户与软删除建索引，[EntityIndex] 映射为 HasIndex
    }
}
```

## 语义

- 工作单元在首次写入时开启数据库事务（只读调用不开事务，SQLite 这类单写者数据库上读写调用能并存）；仓储的每次写入立即 `SaveChanges`（同一事务内读己之写），批量写入是一次 `IN` 查询取回目标行、逐项校验后一次 `AddRange` / 跟踪加一次 `SaveChanges`，提交即提交事务，回滚丢弃一切。
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
- 启动时在独立作用域里取一次模型：索引声明不合法、Provider 不支持部分唯一索引这类建模错误在启动时失败，不论是否开着 `EnsureCreated`。
- 命名连接（ADR-0084）不支持：一个 `DbContext` 一个连接，要分库另起 `DbContext`。实体以 `AddRepository<TEntity, TKey>(connection)` 登记到命名连接在装配时抛出携带 `PERSISTENCE_UNAVAILABLE_008` 的 `FrameworkException`（启动失败），`UnitOfWorkOptions(Connection: …)` 在 `BeginAsync` 抛出同码异常。所以 EF Core 在 SQLite 上跑数据库工作流时租约表放不到另一个库，调用方事务里的取锁会超时——用 PostgreSQL 等行锁数据库，或改用 SqlSugar。
- 运行时数据源（ADR-0078）不支持：EF Core 的数据库 Provider（`UseNpgsql`、`UseSqlServer` 等）由应用选、本包不引用，按数据库类型建连接要对应的 Provider 包。注册表 `DataSourceRegistry` 照样登记，取用与测试连接返回 `PERSISTENCE_UNAVAILABLE_008`，不返回空结果。
- 实体目录（ADR-0078）取 DbContext 模型：表名 `GetTableName()`（没有映射到表时取类型名），列名 `GetColumnName(表)`，影子属性与从属类型不列；登记了却不在模型里的实体抛 `InvalidOperationException`。库结构读取不支持（`PERSISTENCE_UNAVAILABLE_008`）：跨数据库读结构在 EF Core 里是设计期脚手架服务（`Microsoft.EntityFrameworkCore.Design` 的 `IDatabaseModelFactory`，按数据库 Provider 程序集反射发现），ADO.NET 的 `GetSchema` 各驱动返回的列不同、`Microsoft.Data.Sqlite` 不实现。
- `XiHan:Persistence:EntityFrameworkCore:EnsureCreated=true` 时启动即建表，只用于开发与测试；生产用迁移。
- 健康项 `persistence.entityframeworkcore.database`：启动时同步探一次，之后按 `XiHan:Persistence:EntityFrameworkCore:HealthProbeIntervalSeconds`（默认 15）在后台重探并缓存结果——在独立作用域里取默认连接的 DbContext `CanConnect`，不开工作单元、不经管道；连不上条目 Degraded（`PERSISTENCE_UNAVAILABLE_003`，消息键 `persistence.store.unavailable`），应用仍在服务。

## 依赖方向

依赖 `Persistence.Abstractions`、`MultiTenancy.Abstractions`（分片绑定，能力可选）与 `Microsoft.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.Relational`（索引名与表名是关系型元数据；各关系型数据库的 EF Core Provider 本来就依赖它）。数据库驱动包（Sqlite、Npgsql、SqlServer）由应用引用。

## 状态

P2：可用，通过 `Persistence.Conformance` 套件（SQLite）。
