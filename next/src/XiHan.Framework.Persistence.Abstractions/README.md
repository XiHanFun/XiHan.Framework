# XiHan.Framework.Persistence.Abstractions

持久化契约（Ring 2）。能力 `persistence.relational@1`、`persistence.document@1` 由 Provider 提供。

| 契约 | 用途 |
| --- | --- |
| `IRepository<TEntity, TKey>` | 查找、查询、计数、新增、更新、删除，以及按标识集合查找与批量新增 / 更新 / 删除（`FindManyAsync`、`AddRangeAsync` / `UpdateRangeAsync` / `RemoveRangeAsync`：逐项 `Result` 与入参一一对应、通过校验的项一次写入）；可恢复失败用 `Result` 返回 |
| `EntityQuery<T>` | 过滤（规约）、排序、分页、是否包含已删除、时间范围提示（`Within`：只影响分表 / 分区 Provider 扫描的表，不过滤行） |
| `IUnitOfWork` | 事务边界与当前租户；由 Transaction 阶段的 `UnitOfWorkMiddleware` 开启、提交或回滚 |
| `IIdGenerator<TKey>`、`GuidV7IdGenerator` | 标识生成 |
| `PersistenceConventions` | Provider 共用的协议约定：创建时间、修改时间、租户、并发戳、软删除 |
| `IRepositoryRegistrar`、`AddRepository<TEntity, TKey>()` | 应用显式登记实体，Provider 据此注册闭合泛型的仓储；换 Provider 不改业务代码 |

```csharp
builder.Services.AddRepository<Order, Guid>();          // 应用登记实体
builder.AddXiHan(x => x.UseFeature<InMemoryPersistenceFeature>());   // 或任一 Provider 的特性
```

## 依赖方向

依赖 `XiHan.Framework.Kernel` 与 `XiHan.Framework.Domain`。被 `Persistence.InMemory`、`Persistence.EntityFrameworkCore`、`Persistence.SqlSugar` 等实现包引用。

## 状态

P2：可用。
