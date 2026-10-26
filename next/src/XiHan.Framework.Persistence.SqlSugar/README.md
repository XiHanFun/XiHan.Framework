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
- `SqlSugarConventions.Apply(config)`：`Id` 为主键（是否自增沿用实体自己的 `[SugarColumn(IsIdentity)]`，默认不自增），`ConcurrencyStamp` 长度 32，`DateTimeOffset` 经转换器按 UTC 时刻往返（SqlSugar 默认按本地时间处理会改变时刻），可空值类型与可空引用类型的列可空，没有公开 setter 的属性与集合属性不映射。约定通过 `ConfigureExternalServices.EntityService` 施加，已有回调先执行。
- 实体类型没有 `new()` 约束，写入经 SqlSugar 的 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject` 入口，语义与泛型构建器相同。
- 只读工作单元在写入时就返回 `PERSISTENCE_VALIDATION_002`。
- 绑定 `db`（`connection:relational`，可选）传给配置回调；缺失时回调自行决定连接。
- 租户分库：装配了 `multitenancy.sharding@1`（如 `MultiTenancy.InMemory`）时，方式一创建的客户端默认连接 `ConfigId` 为 `default`（回调里设置的 `ConfigId` 以特性为准），带分片的调用把分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）交给同一回调再建一个 `ConfigId = shard` 的连接，两者组成一个客户端：租户实体落分片库，平台目录表（租户、授予、翻译、发件箱等不带租户的实体）仍落默认库，同一调用里一起提交或回滚；其余调用只有默认连接。方式二由应用自己按 `context.GetTenantShard()` 组装连接集合，提供名为 `shard` 的连接即可让默认规则生效。分片库的表结构由应用管理。
- `XiHan:Persistence:SqlSugar:InitTables=true` 时启动按已登记的实体建表，按落点分组：启动时没有调用也就没有分片，方式一的全部实体在默认连接建表；方式二带 `[Tenant]` 的实体在各自连接建表。
- 不提供按查询条件的批量更新 / 删除（`Updateable().SetColumns().Where()`）：它们绕过审计、并发戳与软删除协议；需要时应用直接用客户端。

## 依赖方向

依赖 `Persistence.Abstractions`、`MultiTenancy.Abstractions`（分片绑定，能力可选）与 `SqlSugarCore`（自带各数据库驱动）。应用从 `SqlSugarCore` 换成 `SqlSugarCoreNoDrive` + 单个驱动时须整体替换，两者不能并存。

## 状态

P5：可用，通过 `Persistence.Conformance` 套件（SQLite）；多库用两个 SQLite 文件验证（平台表落默认库、租户表落分片库、失败一起回滚、`[Tenant]` 显式落点）。
