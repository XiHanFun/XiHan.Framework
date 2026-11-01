# XiHan.Framework.Persistence.InMemory

内存持久化。清单 `role: default`、`stability: preview`：没有显式 Provider 时被选中，只用于开发、原型与测试。

## 语义

- 每种实体一张表，容量上限 `XiHan:Persistence:InMemory:MaxEntitiesPerType`（默认 100000），超出返回 `PERSISTENCE_UNAVAILABLE_002`。
- 工作单元读己之写：新增、更新、删除先进覆盖层；回滚丢弃。
- 只读工作单元（只读操作或 `UnitOfWorkOptions(IsReadOnly: true)`）的写入在进覆盖层之前就返回 `PERSISTENCE_UNEXPECTED_001`（批量逐项都是它），并把工作单元记为失败，提交回滚并返回同一错误。
- 软删除、创建与修改时间、租户写入与过滤、并发戳刷新按 `PersistenceConventions` 执行。
- 读写都复制：查找、`FindManyAsync`、查询交出的是副本，写入存的是调用方实例的副本（`InMemoryTable.Find` / `Snapshot` 同样交出副本）。在读出的实例上的修改不经仓储写回就碰不到存储，回滚后不留痕迹。副本是字段逐一的浅复制：标量与协议字段互不影响，集合、嵌套对象等引用类型字段（含聚合的领域事件缓冲）仍与副本共享，不在隔离范围内。
- 提交时校验：覆盖层记下每个写过的标识首次读到的已提交状态，提交按建表先后给涉及的表依次加锁，在全部锁内逐表校验——新增的标识已被别的工作单元提交返回 `PERSISTENCE_CONFLICT_002`；更新或删除的行已被删除、带并发戳的行戳已变返回 `PERSISTENCE_CONFLICT_001`；之后是容量——全部通过才逐表应用，跨表原子，失败时任何一张表都不改。
- 更新沿用当前行的创建时间、租户与删除标记，软删除在当前行的副本上打标（调用方实例上的其他改动不写入），删除标记再同步回调用方实例。
- 命名连接（ADR-0084）不支持：内存实现没有连接。实体以 `AddRepository<TEntity, TKey>(connection)` 登记到命名连接在装配时、`UnitOfWorkOptions(Connection: …)` 在 `BeginAsync` 抛出携带 `PERSISTENCE_UNAVAILABLE_008` 的 `FrameworkException`，不静默落在唯一的存储上。
- 运行时数据源（ADR-0078）不支持：内存实现没有数据库。注册表 `DataSourceRegistry` 照样登记，取用与测试连接返回 `PERSISTENCE_UNAVAILABLE_008`。
- 实体目录（ADR-0078）：没有表，表名取实体类型名，列取有公开 setter 的非集合属性、列名即属性名（这是它的存储形态），主键是 `Id`。库结构读取不支持（`PERSISTENCE_UNAVAILABLE_008`）。
- 健康项 `persistence.inmemory.capacity`（存储自己实现，同步计算）：任一实体的表达到 `MaxEntitiesPerType` 即 Degraded（`PERSISTENCE_UNAVAILABLE_002`，`entity` 是按序数顺序的第一个满表），否则 Ready。

## 依赖方向

依赖 `XiHan.Framework.Persistence.Abstractions`。

## 状态

P2：可用，通过 `Persistence.Conformance` 套件。
