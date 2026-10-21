# RFC-0005 领域协议与持久化

> 状态：已采纳 | 日期：2026-10-21 | 对应 ADR：0005 | 参考实现：`XiHan.Framework.Domain`、`XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.Persistence.InMemory` | 一致性：`XiHan.Framework.Persistence.Conformance`

## 摘要

领域层只定义协议，不提供基类；持久化层定义仓储、工作单元与查询的语义，Provider 按实体实现的协议自动维护审计、租户、并发与软删除字段。换 Provider 只改配置与包引用，业务代码不变。

## 领域协议

| 协议 | 字段 | Provider 行为 |
| --- | --- | --- |
| `IEntity<TKey>` | `Id` | 标识；`TKey` 必须可比较相等 |
| `IAggregateRoot<TKey>` | `DomainEvents`、`ClearDomainEvents()` | 提交后派发并清空（派发由 Messaging 子系统承担） |
| `IHasCreationTime` | `CreatedAt` | 新增时写入当前 UTC 时间 |
| `IHasModificationTime` | `ModifiedAt` | 更新时写入当前 UTC 时间 |
| `IHasConcurrencyStamp` | `ConcurrencyStamp` | 新增与更新时刷新；更新时比对，不符返回 `PERSISTENCE_CONFLICT_001` |
| `ISoftDeletable` | `IsDeleted`、`DeletedAt` | 删除改为打标；查询默认过滤 |
| `IMultiTenant` | `TenantId` | 新增时写入当前租户；查询只返回当前租户的实体；无当前租户时新增返回 `PERSISTENCE_VALIDATION_001` |
| `IValueObject` | — | 标记；用 `record` 取得值相等 |
| `ISpecification<T>` | `Criteria`、`IsSatisfiedBy` | 同一条件既能内存判定也能交给查询提供器；`And` / `Or` / `Not` 组合必须共享同一参数 |

领域事件集合有界（默认 256），超出抛出 `InvalidOperationException`：那是忘记提交的编程错误。

## 工作单元

- 作用域生命周期，一次调用一个；由 Transaction 阶段的 `UnitOfWorkMiddleware` 开启，用安全上下文中的租户作为当前租户。
- 状态机：`NotStarted → Active → Committed | RolledBack`。违反顺序抛出携带 `PERSISTENCE_VALIDATION_002` 的 `FrameworkException`。
- 调用结果为成功则提交；结果为失败或抛出异常则回滚。提交失败（并发冲突、标识重复、容量）作为调用结果返回。
- 只读工作单元在提交时发现写入返回 `PERSISTENCE_VALIDATION_002`。
- 读己之写：同一工作单元内新增、更新、删除对随后的查找与查询可见；提交前对其他工作单元不可见。

## 仓储

| 操作 | 语义 | 可恢复失败 |
| --- | --- | --- |
| `FindAsync(id)` | 已软删除或不属于当前租户视为不存在 | — |
| `ListAsync(query)` | 过滤、排序、分页；`Take` 上限默认 1000，Provider 不得超出 | — |
| `CountAsync(query)` | 忽略排序与分页 | — |
| `AddAsync` | 写入创建时间、租户、并发戳 | `PERSISTENCE_CONFLICT_002` 标识重复、`PERSISTENCE_VALIDATION_001` 缺租户 |
| `UpdateAsync` | 写入修改时间、刷新并发戳 | `PERSISTENCE_NOTFOUND_001`、`PERSISTENCE_CONFLICT_001` |
| `RemoveAsync` | 软删除协议打标，否则物理删除 | `PERSISTENCE_NOTFOUND_001` |

在 Transaction 阶段之外使用仓储是装配错误，每个操作抛出携带 `PERSISTENCE_UNAVAILABLE_001` 的 `FrameworkException`，不返回 `Result`。

## 实体登记

应用用 `AddRepository<TEntity, TKey>()` 登记实体；Provider 的特性在配置阶段通过 `IRepositoryRegistrar` 为每个登记注册闭合泛型的 `IRepository<TEntity, TKey>`。不用开放泛型注册，不用反射扫描；AOT 友好。

## 能力与配置

| 项 | 值 |
| --- | --- |
| 能力 | `persistence.relational@1`；`persistence.document@1` 预留 |
| 配置节 | `XiHan:Persistence`；内存实现 `XiHan:Persistence:InMemory:MaxEntitiesPerType`（默认 100000） |
| 默认实现 | `XiHan.Framework.Persistence.InMemory`，`role: default`，`stability: preview`，只用于开发与测试 |

## 一致性

Provider 必须继承 `RepositoryConformance` 并通过全部用例。套件以 `Testing.TestHost` 走完整管道，用自带的 Tenant 阶段中间件从 Baggage 的 `xihan.tenant` 解析租户，不依赖 MultiTenancy 子系统。

## 错误码

见 `conventions/error-codes.md` 中 `PERSISTENCE_*`。

## 不做什么

- 不提供实体基类、审计基类或"全能仓储"。
- 不在抽象层暴露 `IQueryable`；复杂查询由 Provider 专属包（如 `Persistence.EntityFrameworkCore` 的 `DbContext`）承担。
- 不提供跨 Provider 的分布式事务。
