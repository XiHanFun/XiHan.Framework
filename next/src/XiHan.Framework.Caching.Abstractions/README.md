# XiHan.Framework.Caching.Abstractions

缓存契约（Ring 2）。能力 `caching.local@1`（进程内）与 `caching.distributed@1`（跨进程共享）由 Provider 提供。

| 契约 | 用途 |
| --- | --- |
| `ICacheStore` | 字节级存储：读、写、不存在才写、删、比较删，以及批量读 / 写 / 删（`GetManyAsync` / `SetManyAsync` / `RemoveManyAsync`：一次往返，结果与入参顺序一一对应）；Provider 实现它，被选中提供 `caching.distributed` 的 Provider 把它登记为接口，应用可在这些原语上搭自己的租约与续约 |
| `ICache` / `ISharedCache` | 类型化门面：`GetAsync<T>`、`SetAsync<T>`、`GetOrCreateAsync<T>` 与批量的 `GetManyAsync<T>`、`SetManyAsync<T>`、`RemoveManyAsync`；`ISharedCache` 是跨进程的那一个 |
| `ICacheSerializer`、`JsonCacheSerializer`、`CacheSerializerRegistration` | 共享缓存的值序列化；`JsonCacheSerializer` 要求 `JsonSerializerOptions` 带 `TypeInfoResolver`（源生成上下文），`CreateReflectionBased()` 只用于允许反射的运行时；Provider 按"已注册的 `ICacheSerializer` → 已注册的 `JsonSerializerOptions`"顺序取序列化器 |
| `CacheKey`、`CacheEntryOptions`、`CacheEntry` | 键（非空、无空白与控制字符、≤ 512；`IsValidPrefix` 校验前缀，可以为空）、过期（绝对或滑动，二选一）与批量写入的一项 |
| `ICacheStoreInspector`、`CacheKeyQuery`、`CacheKeyPage`、`CacheEntryInfo` | 可选的键管理（ADR-0055）：按前缀分页列键（`Limit` 1–1000，`Continuation` 续页）、读条目的载荷字节数 / 剩余存活 / 滑动窗口、按前缀删除（前缀不能为空）；管理界面把 `ICacheStore` 转成它使用，内存与 Redis 存储实现，远程插件不实现 |
| `ICacheChangeFeed`、`CacheChange` | 可选的变更通知（ADR-0055）：本实例与其他实例对共享存储的写入与删除（键列表，或按前缀删除时的前缀），用来失效本地副本；本实例同步通知、其他实例经后端转发且不回送，尽力而为 |
| `IDistributedLock`、`StoreDistributedLock`、`LockHandle` | 建立在"不存在才写 + 比较删"之上的租约锁 |
| `IdempotencyMiddleware`、`IdempotencyOptions` | Idempotency 阶段：按 Baggage `xihan.idempotency-key` 去重，处理中返回冲突，完成后回放结果 |
| `CacheStoreDistributedCache`、`AddXiHanDistributedCache` | .NET `IDistributedCache` 架在 `ICacheStore` 上：键须满足 `CacheKey` 文法，绝对过期换算成 TTL（与滑动同时给时绝对优先），`Refresh` 是一次读取，同步成员在边界阻塞；提供 `caching.distributed` 的 Provider 被选中时登记，应用自己的登记优先 |

```csharp
var cache = context.Services.GetRequiredService<ISharedCache>();
var profile = await cache.GetOrCreateAsync(new CacheKey($"profile:{id}"), ct => LoadAsync(id, ct), CacheEntryOptions.Expires(TimeSpan.FromMinutes(5)), ct);
```

基础设施失败（存储不可达）抛出携带 `CACHING_UNAVAILABLE_001` 的 `FrameworkException`，不伪装成未命中。

## 依赖方向

依赖 `XiHan.Framework.Kernel` 与 `Microsoft.Extensions.Caching.Abstractions`。被 `Caching.InMemory`、`Caching.Redis`、`Caching.Remote` 引用。

## 状态

P2：可用。
