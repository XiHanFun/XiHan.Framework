# XiHan.Framework.Caching.Abstractions

缓存子系统的抽象包：字节存储 `ICacheStore`、类型化缓存 `ICache` / `ISharedCache`、序列化器、分布式锁、Idempotency 阶段中间件与 `IDistributedCache` 适配都在这里，能力 `caching.local@1` 与 `caching.distributed@1` 由 Provider 提供。写缓存 Provider、在特性里用缓存或锁时引用它。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Caching.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `caching.local@1`、`caching.distributed@1`（`CachingCapabilities`），本包不提供 |
| 特性入口 | — |
| 配置节 | `XiHan:Caching:Idempotency`（由提供 `caching.distributed` 的 Provider 读取） |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`；`Microsoft.Extensions.Caching.Abstractions` 10.0.13、`Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.13 |
| AOT | 兼容；`JsonCacheSerializer.CreateReflectionBased()` 标了 `RequiresUnreferencedCode` / `RequiresDynamicCode`，AOT 下不可用 |
| 稳定性 | Stable；`CacheStoreDistributedCache`、`DistributedCacheServiceCollectionExtensions`、键管理与变更通知类型为 Preview |

## 概述

子系统只有一个存储契约 `ICacheStore`：读、写、不存在才写（`TryAddAsync`）、删、比较删（`TryRemoveAsync`）五个原子操作，加上批量的 `GetManyAsync` / `SetManyAsync` / `RemoveManyAsync`。Provider 只实现这一个接口，类型化缓存、分布式锁与幂等去重都由本包建立在它之上：

- `SerializingCache` 把存储与 `ICacheSerializer` 组合成 `ISharedCache`，跨进程的 Provider（Redis、Remote）直接用它。
- `StoreDistributedLock` 用 `TryAddAsync` 写入 `lock:<resource>` 与唯一令牌、用 `TryRemoveAsync` 按令牌释放，任何存储都能复用。
- `IdempotencyMiddleware` 用 `TryAddAsync` 写「处理中」标记，用 `SetAsync` 写完成记录，用 `TryRemoveAsync` 清理。
- `CacheStoreDistributedCache` 把 .NET 的 `IDistributedCache` 架在存储上。

本包另有两个可选接口给管理界面（`ICacheStoreInspector`、`ICacheInspector`）和一个给两级缓存的变更通知接口（`ICacheChangeFeed`），存储或缓存能做就实现，业务代码不依赖它们。

## 何时使用

- 特性要用缓存或锁：引用本包，按需在清单里声明 `caching.local@1` 或 `caching.distributed@1`，从容器取 `ICache`、`ISharedCache` 或 `IDistributedLock`。
- 写新的缓存 Provider：实现 `ICacheStore`（可选再实现 `ICacheStoreInspector`、`ICacheChangeFeed`），用本包的 `SerializingCache`、`StoreDistributedLock`、`IdempotencyMiddleware`、`AddXiHanDistributedCache` 完成其余注册，并通过 [Caching.Conformance](./conformance) 的 `CacheStoreConformance`。
- 只需要一个能跑的缓存：直接用 [Caching.InMemory](./caching-inmemory)（随 `Profiles.Standard` 装配），不必单独引用本包。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Caching.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。本包没有特性入口，服务由 Provider 的特性注册。

## 配置

幂等选项 `IdempotencyOptions` 由 Provider 在装配时用 `IdempotencyOptions.FromConfiguration` 读取：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Caching:Idempotency:TimeToLiveSeconds` | 数字 | 86400 | 已完成调用结果的保留时间 |
| `XiHan:Caching:Idempotency:InProgressTimeToLiveSeconds` | 数字 | 60 | 「处理中」标记的保留时间，即处理器崩溃且未能清理时同一个键被封锁的最长时间 |

```json
{ "XiHan": { "Caching": { "Idempotency": { "TimeToLiveSeconds": 3600, "InProgressTimeToLiveSeconds": 30 } } } }
```

值按不变区域性解析，不是正数时抛 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `ICacheStore` | 字节级存储，Provider 实现；返回的字节不可修改 |
| `ICache` | 类型化缓存：`GetAsync<T>`、`SetAsync<T>`、`RemoveAsync`、`GetOrCreateAsync<T>` 与批量方法；`Store` 暴露底层存储 |
| `ISharedCache` | 跨进程共享的 `ICache`（`caching.distributed`） |
| `SerializingCache` | `ICacheStore` + `ICacheSerializer` 组成的 `ISharedCache` |
| `CacheKey` | 键：非空、无空白与控制字符、不超过 `MaxLength`（512）、区分大小写；可从字符串隐式转换；`IsValid`、`IsValidPrefix`、`Validate` |
| `CacheEntryOptions` | 过期：`Expires(ttl)`、`Sliding(window)`、`Default`（Provider 默认存活时间），二选一，`Validated()` 校验组合 |
| `CacheEntry` | `ICacheStore.SetManyAsync` 的一项：键、值、该项自己的过期选项 |
| `ICacheSerializer`、`JsonCacheSerializer`、`CacheSerializerRegistration` | 值序列化；`JsonCacheSerializer` 要求选项带 `TypeInfoResolver`；`TryAddJsonSerializer` 供 Provider 按已有登记选序列化器 |
| `IDistributedLock`、`LockHandle`、`StoreDistributedLock` | 租约锁：`TryAcquireAsync(resource, ttl)` 返回 `Maybe<LockHandle>`，句柄 `ReleaseAsync` / `DisposeAsync` 释放 |
| `IdempotencyMiddleware`、`IdempotencyOptions`、`IdempotencyRecord` | Idempotency 阶段：按 Baggage `xihan.idempotency-key` 去重；`WasReplayed(context)` 判断结果是否来自回放 |
| `CacheStoreDistributedCache`、`DistributedCacheServiceCollectionExtensions` | `IDistributedCache` 适配；`AddXiHanDistributedCache(services, store)` 幂等登记，已有登记不覆盖；`Translate` 换算过期选项 |
| `ICacheStoreInspector`、`CacheKeyQuery`、`CacheKeyPage`、`CacheEntryInfo` | 可选的字节条目键管理：按前缀分页列键、读字节数与剩余存活、按前缀删除 |
| `ICacheInspector`、`CachedObjectInfo` | 可选的对象条目键管理：进程内对象缓存的条目不在字节存储里，由它列出 |
| `ICacheChangeFeed`、`CacheChange` | 可选的变更通知：本实例与其他实例对共享存储的写入与删除，用来失效本地副本 |
| `CachingCapabilities`、`CachingErrors`、`CachingMessages` | 能力 ID 与版本、错误码、消息键与消息目录 |

## 使用示例

在 `ICacheStore` 的原子原语上手写一个只允许一个调用方通过的门闩，适合需要按值判断的场景（锁已经封装了这一模式，优先用 `IDistributedLock`）：

```csharp
using System.Text;
using XiHan.Framework.Caching.Abstractions.Stores;

public sealed class OneShotGate(ICacheStore store)
{
    // 只有第一次写入成功的调用方拿到 true；ttl 到期后门闩重新打开
    public ValueTask<bool> TryEnterAsync(string name, string owner, TimeSpan ttl, CancellationToken cancellationToken)
        => store.TryAddAsync(new CacheKey($"gate:{name}"), Encoding.UTF8.GetBytes(owner), CacheEntryOptions.Expires(ttl), cancellationToken);

    // 只有当前持有者能提前打开门闩
    public ValueTask<bool> LeaveAsync(string name, string owner, CancellationToken cancellationToken)
        => store.TryRemoveAsync(new CacheKey($"gate:{name}"), Encoding.UTF8.GetBytes(owner), cancellationToken);
}
```

`ICacheStore` 由内存与 Redis Provider 登记在容器里；用远程 Provider 时从 `ISharedCache.Store` 取。缓存读写、批量、锁与幂等键的完整示例见[缓存与分布式锁](../guide/caching)。

## 行为与限制

- 基础设施失败（存储不可达、超时）抛出携带 `CACHING_UNAVAILABLE_001` 的 `FrameworkException`，不伪装成未命中。
- 批量方法一次往返、结果与入参顺序一一对应、空集合不触后端；`SetManyAsync` 任一项选项不合法时整批不写；`RemoveManyAsync` 同一个键出现多次第二次为 `false`。
- `GetOrCreateAsync` 不合并并发：多个未命中的调用方各自执行工厂，后写覆盖先写。
- `LockHandle` 只释放一次；锁到期后被他人获取会换成新令牌，原句柄释放返回 `false`。锁不可重入、不续约。
- 幂等记录的作用域键是 `idem:<租户>:<主体>:<键>`，不满足 `CacheKey` 文法时以 `CACHING_VALIDATION_001` 结束调用；处理中返回 `CACHING_CONFLICT_002`；成功与失败的结果都会回放，回放不还原响应体。
- `CacheStoreDistributedCache`：绝对过期两种写法取较早者，已过去的时刻抛 `ArgumentOutOfRangeException`；与滑动过期同时给时绝对优先；都不给时用 Provider 的默认存活时间；同步成员阻塞等待异步存储。
- `ICacheStoreInspector.RemoveByPrefixAsync` 与 `ICacheInspector.RemoveByPrefixAsync` 拒绝空前缀；`CacheKeyQuery.Limit` 为 1 到 `MaxLimit`（1000），默认 100。
- `ICacheChangeFeed` 的处理器在写入的调用里同步执行，不应阻塞或抛出；跨实例通知可能丢失。

| 错误码 | 含义 |
| --- | --- |
| `CACHING_VALIDATION_001` | 缓存键或幂等键不合法 |
| `CACHING_VALIDATION_002` | 条目选项不合法（远程插件返回；.NET 侧抛 `ArgumentException`） |
| `CACHING_UNAVAILABLE_001` | 存储不可达或操作失败 |
| `CACHING_UNAVAILABLE_002` | 内存缓存已达容量上限（健康项 Degraded 的原因） |
| `CACHING_CONFLICT_001` | 分布式锁被占用（已登记，框架不产出） |
| `CACHING_CONFLICT_002` | 同一幂等键的调用仍在处理中 |

## 相关

- [缓存与分布式锁](../guide/caching)
- [Caching.InMemory](./caching-inmemory)、[Caching.Redis](./caching-redis)、[Caching.Hybrid](./caching-hybrid)、[Caching.Remote](./caching-remote)
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0006《缓存、分布式锁与幂等》；ADR-0012、ADR-0055、ADR-0058
