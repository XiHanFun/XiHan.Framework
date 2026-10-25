# XiHan.Framework.Caching.Abstractions

缓存契约（Ring 2）。能力 `caching.local@1`（进程内）与 `caching.distributed@1`（跨进程共享）由 Provider 提供。

| 契约 | 用途 |
| --- | --- |
| `ICacheStore` | 字节级存储：读、写、不存在才写、删、比较删，以及批量读 / 写 / 删（`GetManyAsync` / `SetManyAsync` / `RemoveManyAsync`：一次往返，结果与入参顺序一一对应）；Provider 实现它 |
| `ICache` / `ISharedCache` | 类型化门面：`GetAsync<T>`、`SetAsync<T>`、`GetOrCreateAsync<T>` 与批量的 `GetManyAsync<T>`、`SetManyAsync<T>`、`RemoveManyAsync`；`ISharedCache` 是跨进程的那一个 |
| `ICacheSerializer`、`JsonCacheSerializer`、`CacheSerializerRegistration` | 共享缓存的值序列化；`JsonCacheSerializer` 要求 `JsonSerializerOptions` 带 `TypeInfoResolver`（源生成上下文），`CreateReflectionBased()` 只用于允许反射的运行时；Provider 按"已注册的 `ICacheSerializer` → 已注册的 `JsonSerializerOptions`"顺序取序列化器 |
| `CacheKey`、`CacheEntryOptions`、`CacheEntry` | 键（非空、无空白与控制字符、≤ 512）、过期（绝对或滑动，二选一）与批量写入的一项 |
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
