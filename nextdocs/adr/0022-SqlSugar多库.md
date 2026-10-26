# ADR-0022 SqlSugar 多库：实体级落点的多连接客户端与 `AsTenant()` 跨库事务

## 状态

已采纳（修订 ADR-0013 的分片连接选择，RFC-0005 / RFC-0007 修订）。

## 日期

2026-10-27

## 背景

ADR-0018 把租户分片做成"Tenant 阶段解析一次、持久化 Provider 在作用域内按分片选连接"，`Persistence.SqlSugar` 的实现是把**整个作用域**的 `ISqlSugarClient` 切到分片连接（`TenantShardBinding.Resolve` 有分片就只建分片连接）。BasicApp 6.0 按租户分库时，一次调用里既要写租户库（业务表）又要写平台库（租户目录、授予、字典、发件箱），整作用域切换做不到：平台目录表落进了分片库；工作单元也只对一个客户端 `BeginTran`，没有跨库事务。`SqlSugarRepository` 不调用 `SplitTable`，带 `[SplitTable]` 的实体走不了仓储（单独在 ADR-0023 处理）。用户于 2026-10-27 决定这些能力在框架里实现，BasicApp 不写绕过代码。

## 备选

1. 保持整作用域切换，BasicApp 自己为平台表另建客户端、另开事务。
2. 作用域内的客户端带多个连接（SqlSugar 多 `ConfigId`）：默认连接始终在，带分片的调用再加一个 `shard` 连接；每个实体按选择器决定落点；工作单元经 `AsTenant()` 在全部连接上开启、提交、回滚事务。
3. 同 2，但落点只认 SqlSugar 的 `[Tenant(configId)]` 特性，没有标注的实体一律默认连接。
4. 同 2，但在 `Persistence.Abstractions` 定义 Provider 无关的落点契约。

## 决策

采用备选 2，落点规则有默认实现也可替换：

- 新契约 `IEntityConnectionSelector`（`Persistence.SqlSugar` 包内，Preview）：`Select(Type entityType, ISqlSugarClient client)` 返回该实体应使用的连接。默认实现 `SqlSugarConnectionSelector` 的规则按顺序：实体带 SqlSugar `[Tenant(configId)]` → 客户端里该 `ConfigId` 的连接（不存在是配置错误，抛 `FrameworkException`）；实体实现 `IMultiTenant` 且客户端有 `shard` 连接 → `shard`；其余 → 客户端的根（默认）连接。应用用 `TryAdd` 之前注册自己的 `IEntityConnectionSelector` 即可替换。
- `SqlSugarConnections` 常量：`Default = "default"`、`Shard = "shard"`。方式一（特性按回调创建客户端）：默认连接的 `ConfigId` 为 `default`（回调可改），带分片的调用把分片绑定交给同一回调再建一个 `ConfigId = shard` 的连接，两者组成一个 `SqlSugarClient`；没有分片的调用只有默认连接。方式二（应用注册 `ISqlSugarClient`）：应用自己决定连接集合与 `ConfigId`，想让 `IMultiTenant` 实体落分片就提供名为 `shard` 的连接，`[Tenant]` 指向的 `ConfigId` 由应用保证存在。
- `SqlSugarUnitOfWork` 改为经 `ISqlSugarClient.AsTenant()` 的 `BeginTranAsync` / `CommitTranAsync` / `RollbackTranAsync`：首次写入时在当前已打开的连接上开事务，之后首次触及的连接由 SqlSugar 在取连接时补开事务；提交按连接顺序提交，回滚按连接顺序回滚。只有一个连接时与原来的 `Ado.BeginTran` 等价，不另留单连接路径。`ConnectionFor(Type)` 暴露选择结果，仓储每个操作用它取连接；行锁判定（SQLite 不支持 `TranLock`）看所选连接的数据库类型。
- `InitTables` 启动建表按选择器分组：方式一启动时没有分片，全部实体在默认连接建表（与原来相同）；方式二多连接时 `[Tenant]` 实体在各自连接建表。分片库的表结构仍由应用管理。
- 跨库事务是顺序提交，不是两阶段提交：第二个连接提交失败时第一个已经提交。README 与 RFC-0005 写明；需要严格原子性的写入放在同一个库。

## 理由

- 平台目录表与租户表同在一次调用里写是 BasicApp 的常态（下单时写订单也写发件箱与审计），整作用域切换没法表达；实体级落点让"平台表永远在平台库"成为默认行为而不是应用约定。
- 选择器用接口而不是只认 `[Tenant]`（备选 3）：BasicApp 的实体由 `IMultiTenant` 标识租户归属，不该再逐个标注；但 `[Tenant]` 是 SqlSugar 用户熟悉的写法、也是 `WithAttr` 系列 API 的依据，保留它作为显式覆盖。
- 不进抽象层（备选 4）：EF Core 一个 `DbContext` 就是一个连接，落点概念与它不同构；现在只有 SqlSugar 需要，放 Provider 包，等第二个 Provider 需要时再提升。
- `AsTenant()` 是 SqlSugar 多库事务的官方入口，比自己维护连接集合可靠；只有一个连接时它退化成单连接事务，所以不留两条路径。

## 影响

- `Persistence.SqlSugar` 公开面（Preview）：新增 `SqlSugarConnections`、`IEntityConnectionSelector`、`SqlSugarConnectionSelector`、`SqlSugarUnitOfWork.ConnectionFor(Type)`；`SqlSugarUnitOfWork` 构造函数多一个选择器参数；方式一创建的客户端默认连接 `ConfigId` 从空变为 `default`。
- `Persistence.SqlSugar.Tests`：分片用例改为断言所选连接；新增多连接用例（一次调用同时写默认库与分片库、失败一起回滚、`[Tenant]` 覆盖、方式二多连接）。
- RFC-0005 的 SqlSugar 行与 RFC-0007 的分片条目改为实体级落点；ADR-0013 状态注明被本决策修订。
