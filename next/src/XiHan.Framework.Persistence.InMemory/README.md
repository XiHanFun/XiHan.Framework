# XiHan.Framework.Persistence.InMemory

内存持久化。清单 `role: default`、`stability: preview`：没有显式 Provider 时被选中，只用于开发、原型与测试。

## 语义

- 每种实体一张表，容量上限 `XiHan:Persistence:InMemory:MaxEntitiesPerType`（默认 100000），超出返回 `PERSISTENCE_UNAVAILABLE_002`。
- 工作单元读己之写：新增、更新、删除先进覆盖层，提交时原子写入表；回滚丢弃。
- 软删除、创建与修改时间、租户写入与过滤、并发戳刷新按 `PersistenceConventions` 执行。
- 存储的是对象引用，不做深拷贝；不检测跨工作单元的乐观并发冲突。

## 依赖方向

依赖 `XiHan.Framework.Persistence.Abstractions`。

## 状态

P2：可用，通过 `Persistence.Conformance` 套件。
