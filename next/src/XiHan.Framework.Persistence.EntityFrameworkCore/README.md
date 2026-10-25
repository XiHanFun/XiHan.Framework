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
        modelBuilder.ApplyPersistenceConventions();   // 并发戳为并发令牌，租户与软删除建索引
    }
}
```

## 语义

- 工作单元在首次写入时开启数据库事务（只读调用不开事务，SQLite 这类单写者数据库上读写调用能并存）；仓储的每次写入立即 `SaveChanges`（同一事务内读己之写），批量写入是一次 `IN` 查询取回目标行、逐项校验后一次 `AddRange` / 跟踪加一次 `SaveChanges`，提交即提交事务，回滚丢弃一切。
- 软删除与租户过滤在仓储查询里显式施加（`EF.Property`），不用全局查询过滤器，`Find` 与 LINQ 行为一致。
- 更新游离实体时先比对并发戳，不符返回 `PERSISTENCE_CONFLICT_001`；跟踪实体由 EF 的并发令牌保护。
- 只读工作单元在写入时就返回 `PERSISTENCE_VALIDATION_002`。
- 绑定 `db`（`connection:relational`，可选）传给配置回调；缺失时回调自行决定连接。
- 租户分库：装配了 `multitenancy.sharding@1`（如 `MultiTenancy.InMemory`）时，DbContext 选项按作用域构建，带分片的调用把分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）交给配置回调，其余调用用应用默认连接。分片库的表结构由应用管理，`EnsureCreated` 只作用于默认连接。
- `XiHan:Persistence:EntityFrameworkCore:EnsureCreated=true` 时启动即建表，只用于开发与测试；生产用迁移。

## 依赖方向

依赖 `Persistence.Abstractions`、`MultiTenancy.Abstractions`（分片绑定，能力可选）与 `Microsoft.EntityFrameworkCore`。数据库驱动包（Sqlite、Npgsql、SqlServer）由应用引用。

## 状态

P2：可用，通过 `Persistence.Conformance` 套件（SQLite）。
