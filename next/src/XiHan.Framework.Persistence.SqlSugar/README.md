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

// 方式二：应用自己注册客户端（SqlSugarScope、多库、分表等），创建前先应用约定
builder.Services.AddSingleton<ISqlSugarClient>(_ => new SqlSugarScope(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.MySql, ConnectionString = "…" })));
builder.AddXiHan(x => x.UseFeature(new SqlSugarPersistenceFeature()));
```

## 语义

- 工作单元在首次写入时在客户端连接上 `BeginTran`（只读调用不开事务），提交即 `CommitTran`，回滚即 `RollbackTran`；SqlSugar 没有变更跟踪，仓储的每次写入立即执行。
- 写入用 `Insertable` / `Updateable` / `Deleteable`；批量方法一次主键 `IN` 查询取回目标行、逐项校验后一次 `Insertable(list)` / `Updateable(list)` / `Deleteable().In(ids)`，不在循环里逐条执行。
- 软删除与租户过滤以表达式显式施加在每个查询上，不用全局查询过滤器；无租户的调用看不到任何多租户实体。
- 更新与删除先在事务内加行锁取回当前行（`TranLock`：`FOR UPDATE` / `UPDLOCK`；SQLite 不支持行锁，由其数据库级写锁保证），游离实例的并发戳与当前行不同返回 `PERSISTENCE_CONFLICT_001`；行被锁住，读取与写入之间别人改不了它。
- 软删除在取回的当前行上打标并整行写回，调用方实例随后拿到修改时间、并发戳与删除标记。
- `SqlSugarConventions.Apply(config)`：`Id` 为主键（是否自增沿用实体自己的 `[SugarColumn(IsIdentity)]`，默认不自增），`ConcurrencyStamp` 长度 32，`DateTimeOffset` 经转换器按 UTC 时刻往返（SqlSugar 默认按本地时间处理会改变时刻），可空值类型与可空引用类型的列可空，没有公开 setter 的属性与集合属性不映射。约定通过 `ConfigureExternalServices.EntityService` 施加，已有回调先执行。
- 实体类型没有 `new()` 约束，写入经 SqlSugar 的 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject` 入口，语义与泛型构建器相同。
- 只读工作单元在写入时就返回 `PERSISTENCE_VALIDATION_002`。
- 绑定 `db`（`connection:relational`，可选）传给配置回调；缺失时回调自行决定连接。
- 租户分库：装配了 `multitenancy.sharding@1`（如 `MultiTenancy.InMemory`）时，带分片的调用在作用域内按分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）另建连接配置并创建客户端（约定同样施加），其余调用共用应用默认连接；只对由特性创建客户端的方式生效，应用自己注册客户端时自行按 `context.GetTenantShard()` 处理。分片库的表结构由应用管理，`InitTables` 只作用于默认连接。
- `XiHan:Persistence:SqlSugar:InitTables=true` 时启动按已登记的实体 `CodeFirst.InitTables` 建表，只用于开发与测试；生产由应用管理表结构。
- 不提供按查询条件的批量更新 / 删除（`Updateable().SetColumns().Where()`）：它们绕过审计、并发戳与软删除协议；需要时应用直接用客户端。

## 依赖方向

依赖 `Persistence.Abstractions`、`MultiTenancy.Abstractions`（分片绑定，能力可选）与 `SqlSugarCore`（自带各数据库驱动）。应用从 `SqlSugarCore` 换成 `SqlSugarCoreNoDrive` + 单个驱动时须整体替换，两者不能并存。

## 状态

P5：可用，通过 `Persistence.Conformance` 套件（SQLite）。
