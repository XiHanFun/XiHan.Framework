# XiHan.Framework.Persistence.Abstractions

持久化契约（Ring 2）。能力 `persistence.relational@1`、`persistence.document@1` 由 Provider 提供。

| 契约 | 用途 |
| --- | --- |
| `IRepository<TEntity, TKey>` | 查找、查询、计数、新增、更新、删除，以及按标识集合查找与批量新增 / 更新 / 删除（`FindManyAsync`、`AddRangeAsync` / `UpdateRangeAsync` / `RemoveRangeAsync`：逐项 `Result` 与入参一一对应、通过校验的项一次写入）；可恢复失败用 `Result` 返回 |
| `EntityQuery<T>` | 过滤（规约）、排序、分页、是否包含已删除、时间范围提示（`Within`：只影响分表 / 分区 Provider 扫描的表，不过滤行） |
| `IUnitOfWork` | 事务边界与当前租户；由 Transaction 阶段的 `UnitOfWorkMiddleware` 开启、提交或回滚；`DomainEvents`（`DomainEventTracker`）登记本工作单元写过的聚合 |
| `IDomainEventDispatcher` | 领域事件的派发端口：处理器成功后、提交前，中间件把带有未派发事件的聚合交给它（Messaging 子系统实现，发件箱时与业务写入同事务），成功即清空缓冲、失败即回滚；有事件却没有派发器返回 `PERSISTENCE_VALIDATION_003`，不静默丢弃 |
| `IIdGenerator<TKey>`、`GuidV7IdGenerator` | 标识生成端口；Provider 注册 `IIdGenerator<Guid>`（UUID 版本 7，时间有序） |
| `SnowflakeIdFeature`、`SnowflakeIdGenerator`、`SnowflakeOptions`、`SnowflakeId`、`IWorkerIdSource`（`FixedWorkerIdSource` / `LeasedWorkerIdSource`） | 雪花 `long` 标识（ADR-0033）：应用显式装配特性后得到 `IIdGenerator<long>`，41 位毫秒 + 10 位机器位 + 12 位序列；机器位显式配置或经 `IDistributedLock` 租约分配；`SnowflakeId.Parse` 还原时间戳、机器位与序列，见下文 |
| `PersistenceConventions` | Provider 共用的协议约定：创建时间、修改时间、租户、并发戳、软删除 |
| `IRepositoryRegistrar`、`AddRepository<TEntity, TKey>()` | 应用显式登记实体，Provider 据此注册闭合泛型的仓储；换 Provider 不改业务代码 |

```csharp
builder.Services.AddRepository<Order, Guid>();          // 应用登记实体
builder.AddXiHan(x => x.UseFeature<InMemoryPersistenceFeature>());   // 或任一 Provider 的特性
```

## 雪花标识

Profile 缺省只有 GuidV7；要 `long` 标识的应用显式装配 `SnowflakeIdFeature`：

```csharp
// 机器位经分布式锁租约分配：先装配任一 caching.distributed 的 Provider
builder.AddXiHan(x => x.UseFeature<InMemoryCachingFeature>().UseFeature<SnowflakeIdFeature>());
// 或配置 XiHan:Persistence:Snowflake:WorkerId=7 固定机器位，不需要缓存 Provider
```

| 配置键（`XiHan:Persistence:Snowflake`） | 缺省 | 说明 |
| --- | --- | --- |
| `Epoch` | `2026-01-01T00:00:00Z` | 时间戳起点（ISO 8601，无时区按 UTC）；同一套数据一经写入不得再改 |
| `WorkerId` | 无 | 0–1023；指定即固定机器位，运维保证集群内不重复（如 StatefulSet 序号） |
| `ClockDriftToleranceMilliseconds` | 100 | 时钟回拨不超过它时等待追平 |
| `LeaseTimeToLiveSeconds` | 30 | 租约分配时每个机器位租约的存活时间，每三分之一续约一次 |

- 位布局：符号位 0，41 位自 epoch 起的毫秒（约 69.7 年）、10 位机器位、12 位序列；单实例每毫秒最多 4096 个，用尽等下一毫秒；线程安全。
- 机器位租约：没有配置 `WorkerId` 时启动期从 0 起逐个尝试锁 `persistence:snowflake:worker:<n>`，取第一个空位；锁不续约，续约 = 释放再获取同一机器位，空位被别的实例拿走或锁存储不可达都按丢失处理并重新分配；本地按获取时刻 + 存活时间判定有效，到期未续上即没有机器位。容器里没有 `IDistributedLock`、或 1024 个机器位都被占用时启动失败，不做随机机器位。健康项 `persistence.snowflake.worker`：持有 Ready，没有 Degraded（`PERSISTENCE_UNAVAILABLE_005`）。
- 失败：`NewId()` 返回 `long`，失败只能抛出 `FrameworkException`——回拨超过阈值 `PERSISTENCE_UNAVAILABLE_004`（等时钟追上后自动恢复），没有机器位 `PERSISTENCE_UNAVAILABLE_005`；两者都是 UNAVAILABLE，协议适配器映射为可重试的 503。时钟早于 epoch 是配置错误，启动即失败。
- 排障：`generator.Parse(id)` 或 `SnowflakeId.Parse(id, epoch)` 还原生成时刻、机器位与序列。
- 线格式与前端精度：标识在 JSON 里保持 number（后端标准类型，不加字符串转换器）。超过 2^53（约 9.007 × 10^15）的整数在 JavaScript 的 `number` 里会丢精度，而雪花标识从 epoch 之后约 25 天起就全部超过 2^53——前端必须用能保留大整数的方式解析（`JSON.parse` 的 BigInt reviver、按字符串读取原文）并以字符串或 `BigInt` 保存与回传，不能 `Number()` 换算。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Domain` 与 `XiHan.Framework.Caching.Abstractions`（雪花机器位租约用它的 `IDistributedLock`）。被 `Persistence.InMemory`、`Persistence.EntityFrameworkCore`、`Persistence.SqlSugar` 等实现包引用。

## 状态

P2：可用。
