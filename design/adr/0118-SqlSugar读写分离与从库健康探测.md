# ADR-0118 SqlSugar 读写分离与从库健康探测

## 状态

已采纳（`spec/rfcs/0005-persistence.md`、`spec/conventions/error-codes.md` 随之修订）。

## 日期

2026-11-04

本 ADR 记录第三十批回灌（`aj-batch-backlog.md`，N5 矩阵发现）的决定。用户定在框架实现。

## 背景

框架只在 `SqlSugarConventions` 里处理了从库连接串的启动参数，没有从库权重归一化与健康探针。

补的时候核对 SqlSugar 5.1.4.221 的读路由（`AdoProvider.SetConnectionStart`、`SetConnectionEnd`），发现配从库本来就不安全，都用真实连接复现过：

1. **写入会落到从库上。** SqlSugar 读从库时把 `CurrentConnectionConfig.ConnectionString` 改成从库的、读完再改回来。特性把默认连接的 `ConnectionConfig` 建一份供所有作用域共用，这期间别的作用域建的连接就连到了从库。用两个 SQLite 文件复现：写入落在从库文件上，主库没有那一行。
2. **会写的工作单元读从库。** SqlSugar 只在有事务时才不读从库，而事务要到第一次写入才开（延迟开启）。写入之前的判重、取并发戳都读从库；提交之后同一作用域的读也读从库，读不到刚写的。
3. **冲突判断读从库。** 写入撞上约束后，仓储 `CopyNew` 另开连接查是谁撞了，新连接也读从库，从库上还没有那一行时判断错。

## 决策

1. **每个作用域一份连接配置。** 配了从库的连接，作用域取 `UtilMethods.CopyConfig` 的副本。不探测也这样做，这是路由正确的前提。`SqlSugarScope` 自己按上下文复制，没有这个问题。
2. **摘除靠替换副本上的列表，不改共享对象。** 监视器维护每个连接此刻能读的从库列表，状态变化时整体替换，作用域取副本时带上它。一个都没有时为空，SqlSugar 不再做读写分离，读回到主库。这比共享 `SlaveConnectionConfig` 实例简单：共享时只能原地改权重，要维持「不能全零」的不变量，全灭时还要把槽位的连接串改写成主库。
3. **会写的工作单元读写都走主库。** `SqlSugarUnitOfWork` 对不是只读的工作单元设 `Ado.IsDisableMasterSlaveSeparation`，作用域结束前不复位。只读工作单元读从库。仓储的冲突判断连接与库结构读取也读主库。
4. **探针执行一条语句，不只打开连接。** 连接池把空闲连接直接交出来，从库宕了也打得开。去掉语句后用真实主从验证过：从库停了健康项仍是 Ready。单次超时取 5 秒与间隔一半的较小者，首探同步完成，不把启动拖到超时。
5. **`reason` 只写异常类型，完整异常进日志**，同一种失败第一次 Warning、之后 Debug（ADR-0115）。全部不可读时记一条 Error。
6. **探测缺省打开。** 配了从库才有探测，没配时没有任何开销，而不探测时从库宕机，落到它的读就失败。键是 `XiHan:Persistence:SqlSugar:Replicas` 下的 `DefaultHitRate`（缺省 10）、`HealthCheck`（缺省 true）、`HealthCheckIntervalSeconds`（缺省 30）、`FailureCooldownSeconds`（缺省 120）。
7. **主库不同的连接共用从库即失败**（`PERSISTENCE_VALIDATION_009`）。回调对每个连接都调用，不看 `ConfigId` 就把默认连接的从库挂上去，租户的读就读到平台库。默认连接与命名连接启动时核对，分片连接在作用域取客户端时核对。
8. **分片连接可以有自己的从库，但不探测。** 配置每个作用域现建，回调按分片绑定给出，权重归一化、路由规则相同。运行时动态注册的租户连接不在探测范围内，这里不为它做有界的动态注册表。

## 备选

1. **原地改共享的权重与连接串**：解决不了决策 1 的问题，还要维持全零不变量。否决。
2. **每个作用域重跑配置回调**（分片连接就是这样）：不用复制，但回调多跑一次，而且列表还要另外过滤。`CopyConfig` 是 SqlSugar 自己的公开方法，`CopyNew` 也用它。否决。
3. **监视器也探测分片连接**：要做有界的动态注册表和淘汰，目前没有使用者。否决，有需要时再说。
4. **写过之后才钉到主库**：写入之前的读（判重）仍然读从库。否决。
5. **把从库列表也做成框架配置**：入口留给应用的回调，N5 在 `SqlSugarRuntime.Configure` 里读自己的配置。框架不重复一份。否决。

## 影响

- 没配从库的应用没有变化：作用域仍用共享配置，没有探测与健康条目。
- 配了从库的应用，会写的工作单元不再分担读流量到从库，这是正确性的代价。只读调用（动词 `get`、`list`）照常读从库。
- 跨调用读己之写不保证：写完马上发起的只读调用可能读到落后的数据。框架自己的只读工作单元也走从库：`Security.Database` 的授予、`MultiTenancy.Database` 的租户目录、`Scheduling.Database` 与 `Workflow.Database` 的租约探测。
- 应用自己注册 `ISqlSugarClient` 并配从库时，要自己按作用域复制配置，框架不探测它们。
- 公开 API 新增 `SqlSugarReplicaOptions`、`SqlSugarPersistenceOptions.Replicas`、`SqlSugarPersistenceFeature.ReplicaHealthName`、`PersistenceErrors.ReplicaUnavailable`、`PersistenceErrors.ReplicaMisplaced`、`PersistenceMessages.ReplicaUnavailable`。

### 已知限制

- 只探连通，不看复制延迟。一个落后很多但连得上的从库照常接读流量。
- 已经开着的作用域保留自己的快照，从库宕机时这个作用域里落到它的读会失败。作用域是一次调用，很快结束。
- 不串库的核对比较连接串原文，写法不同的同一个串查不出来。它防的是回调不看 `ConfigId` 这个常见错误，不是任意配置。

### 探针之外发现的同类问题

默认连接的健康项 `persistence.sqlsugar.database` 用 `IsValidConnection`、运行时数据源的探测用 `OpenAsync`，都只打开连接。按决策 4 的实验，连接池里有空闲连接时它们同样可能在数据库宕了之后报 Ready。没有在本批改，第三十一批改了（ADR-0123）。
