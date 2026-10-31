# 缓存与分布式锁

本章讲缓存子系统的用法：进程内与跨进程缓存、过期与批量、序列化、分布式锁、幂等键、`IDistributedCache` 适配、两级缓存、键管理与远程 Provider，适用于给特性加缓存或在多实例部署下选缓存 Provider 的开发者。完整 API 见 [Caching.Abstractions 包](../packages/caching-abstractions)。

## 两个能力

缓存子系统提供两个能力（RFC-0006《缓存、分布式锁与幂等》）：

| 能力 | 服务 | 默认实现 | 显式 Provider |
| --- | --- | --- | --- |
| `caching.local@1` | `ICache`：进程内缓存 | `Caching.InMemory` | `Caching.Hybrid`（两级缓存） |
| `caching.distributed@1` | `ISharedCache`、`IDistributedLock`、`IDistributedCache`、Idempotency 阶段中间件；内存与 Redis 实现另登记 `ICacheStore` | `Caching.InMemory`（只在单实例下正确） | `Caching.Redis`、`Caching.Remote` |

`Caching.InMemory` 同时声明两个能力，[`Profiles.Standard`](../packages/profiles-standard) 已经装配它，所以不加任何 Provider 时两种缓存都能用。再 `UseFeature` 一个显式 Provider 就顶替对应能力：装配 `Caching.Redis` 后共享缓存、锁与幂等改由 Redis 承担，进程内的 `ICache` 仍由内存实现提供；被顶替的能力内存实现不再注册服务，也不登记重复的幂等中间件。

| 装配 | `ICache` | `ISharedCache` 与锁、幂等 |
| --- | --- | --- |
| `UseStandardProfile()` | `InMemoryObjectCache` | 同一个 `InMemoryObjectCache` |
| 再加 `RedisCachingFeature` | `InMemoryObjectCache` | `SerializingCache` + `RedisCacheStore` |
| 再加 `HybridCachingFeature` | `HybridCache`（L1 + 内存 L2） | `InMemoryObjectCache` |
| 再加 `RedisCachingFeature` 与 `HybridCachingFeature` | `HybridCache`（L1 + Redis L2） | Redis |
| 再加 `OutOfProcessPluginsFeature` 与 `RemoteCachingFeature` | `InMemoryObjectCache` | `SerializingCache` + `RemoteCacheStore` |

两个显式 Provider 同时提供 `caching.distributed`（如 Redis 与 Remote）时能力求解报 `KERNEL_CAPABILITY_004`，用配置键 `XiHan:Providers:caching.distributed` 写清单 ID（`xihan.caching.redis`、`xihan.caching.remote`）指定一个，规则见[能力包与清单](./modularity)。

::: warning 内存实现的共享缓存只在单实例下正确
没有显式 Provider 时，`ISharedCache`、分布式锁与幂等去重都只在本进程内有效。部署多个实例前必须装配 `Caching.Redis` 或 `Caching.Remote`，否则锁不互斥、幂等键在实例之间互不可见。
:::

## 字节存储与类型化缓存

子系统只有一个存储契约 `ICacheStore`，按字节读写，Provider 实现它：

| 操作 | 语义 |
| --- | --- |
| `GetAsync` | 不存在或已过期为无值；滑动过期的条目在读取时延长 |
| `SetAsync` | 写入或覆盖 |
| `TryAddAsync` | 不存在（或已过期）才写入，原子 |
| `RemoveAsync` | 删除，返回删除前是否存在 |
| `TryRemoveAsync` | 当前值等于期望值才删除，原子 |
| `GetManyAsync` / `SetManyAsync` / `RemoveManyAsync` | 批量版本，见[批量方法](#批量方法) |

类型化缓存 `ICache`（`ISharedCache` 是它的跨进程版本）在存储之上提供 `GetAsync<T>`、`SetAsync<T>`、`RemoveAsync`、`GetOrCreateAsync<T>` 与批量方法。两类实现的存法不同：

- 进程内实现 `InMemoryObjectCache` 直接存对象引用，不序列化。取回的对象与写入的是同一个实例，调用方不得修改它。
- 跨进程实现 `SerializingCache` 把 `ICacheStore` 与 `ICacheSerializer` 组合起来，写入时序列化，读取时反序列化。

`ICache.Store` 暴露底层字节存储，锁与幂等用它的原子原语。存储不可达时所有方法抛出携带 `CACHING_UNAVAILABLE_001` 的 `FrameworkException`，不会当成未命中返回。

缓存键是 `CacheKey`：非空、不含空白与控制字符、不超过 512 个字符（`CacheKey.MaxLength`）、区分大小写；字符串可以隐式转换成它，不合法时抛 `ArgumentException`。缓存不自动按租户隔离键，多租户应用要把租户 ID 写进键里（幂等中间件是例外，见[幂等键](#幂等键)）。

## 启用

内存实现随 `UseStandardProfile()` 装配，不需要额外步骤。换成 Redis：

1. 引用 `XiHan.Framework.Caching.Redis`，安装方式见[包页](../packages/caching-redis)。
2. 在 `AddXiHan` 里 `UseFeature<RedisCachingFeature>()`。
3. 在配置里给出连接字符串。

```csharp
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// Standard 里的内存实现继续提供 caching.local；Redis 顶替 caching.distributed
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RedisCachingFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

```json
{
  "XiHan": {
    "Caching": {
      "Redis": {
        "Configuration": "localhost:6379",
        "KeyPrefix": "orders:"
      }
    }
  }
}
```

没有 `XiHan:Caching:Redis:Configuration`，也没有宿主注入的 `cache` 绑定时，装配直接失败；启动时连不上 Redis 同样让启动失败（`CACHING_UNAVAILABLE_001`），不降级为内存缓存。

## 读写缓存

特性在清单里声明自己依赖的缓存能力，再把用缓存的服务登记进容器。下面的 `CatalogFeature` 依赖 `caching.local`，`ProductCache` 用 `GetOrCreateAsync` 做读穿透：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

public sealed record ProductView(Guid Id, string Name, int Price);

public sealed class ProductCache(ICache cache)
{
    public ValueTask<ProductView> GetAsync(Guid id, Func<CancellationToken, ValueTask<ProductView>> load, CancellationToken cancellationToken)
        => cache.GetOrCreateAsync(Key(id), load, CacheEntryOptions.Expires(TimeSpan.FromMinutes(10)), cancellationToken);

    // 商品变更后删掉缓存，下一次读取重新加载
    public ValueTask<bool> InvalidateAsync(Guid id, CancellationToken cancellationToken)
        => cache.RemoveAsync(Key(id), cancellationToken);

    private static CacheKey Key(Guid id) => new($"product:{id:N}");
}

public sealed class CatalogFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "acme.catalog",
        Package = "Acme.Catalog",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(CachingCapabilities.Local, CachingCapabilities.Version),
        ],
        Entry = typeof(CatalogFeature).FullName,
    };

    public void Configure(FeatureContext context) => context.Services.AddSingleton<ProductCache>();
}
```

`GetOrCreateAsync` 不合并并发：多个调用同时未命中时各自执行工厂，后写入的覆盖先写入的。工厂代价高、需要只算一次时，先用[分布式锁](#分布式锁)串行化。

读单个值用 `GetAsync<T>`，返回 `Maybe<T>`：

```csharp
var cached = await cache.GetAsync<ProductView>(new CacheKey("product:42"), cancellationToken);
if (cached.TryGetValue(out var product))
{
    // 命中
}
```

内存实现在同一个键存的对象类型与 `T` 不符时当作未命中；序列化实现反序列化失败时抛出 `InvalidOperationException`。值不能是 `null`，`SetAsync<T>` 对 `null` 抛 `ArgumentNullException`。

## 过期

每次写入带一个 `CacheEntryOptions`：

| 写法 | 含义 |
| --- | --- |
| `CacheEntryOptions.Expires(ttl)` | 绝对过期：写入后固定存活 `ttl` |
| `CacheEntryOptions.Sliding(window)` | 滑动过期：每次读取把到期时刻推到「现在 + `window`」 |
| `CacheEntryOptions.Default` | 两者都不设，用 Provider 的默认存活时间 |

两种过期只能二选一，同时设置时写入抛 `ArgumentException`；时长必须为正。Provider 的默认存活时间都是 300 秒，分别由 `XiHan:Caching:InMemory:DefaultTimeToLiveSeconds` 与 `XiHan:Caching:Redis:DefaultTimeToLiveSeconds` 调整，远程插件自行决定（`samples/CacheGoPlugin` 读 `XiHan:Cache:DefaultTimeToLiveSeconds`，默认 300）。经框架写入的条目总有过期时间。

## 批量方法

存储与类型化缓存都提供批量方法（`conventions/batch-first.md`，ADR-0012），一次调用、一次往返，结果与入参顺序一一对应：

| 方法 | 语义 |
| --- | --- |
| `GetManyAsync<T>(keys)` | 每项是 `Maybe<T>`；命中的滑动过期条目逐个延长 |
| `SetManyAsync<T>(entries, options)` | 全部条目用同一过期选项；同一个键出现多次后者覆盖 |
| `RemoveManyAsync(keys)` | 每项报告是否删到；同一个键出现多次第二次为 `false` |

空集合不触后端，直接返回空结果；存储不可达时整批抛出 `CACHING_UNAVAILABLE_001`。存储层的 `ICacheStore.SetManyAsync` 接受 `CacheEntry` 列表，每项可以带自己的过期选项，任一项选项不合法时整批不写。`TryAddAsync` 与 `TryRemoveAsync` 是锁与幂等的原子原语，只有单键版本。

先批量读、再只为未命中的键加载并批量回填：

```csharp
var keys = ids.Select(static id => new CacheKey($"product:{id:N}")).ToList();
var hits = await cache.GetManyAsync<ProductView>(keys, cancellationToken);

var misses = new List<Guid>();
for (var index = 0; index < keys.Count; index++)
{
    if (!hits[index].HasValue)
    {
        misses.Add(ids[index]);
    }
}

// loadMany 是应用自己的批量查询
var loaded = await loadMany(misses, cancellationToken);
await cache.SetManyAsync(
    loaded.Select(static p => new KeyValuePair<CacheKey, ProductView>(new CacheKey($"product:{p.Id:N}"), p)).ToList(),
    CacheEntryOptions.Expires(TimeSpan.FromMinutes(10)),
    cancellationToken);
```

Redis 用 `MGET` 读、把 `SET` 与 `DEL` 放进一个批次写删；内存实现一次加锁逐项处理；远程 Provider 调用对应的 `cache-entries.get-many / set-many / remove-many` 操作。

## 序列化器

跨进程缓存（Redis、Remote）把值序列化后存进字节存储。序列化器按下面的顺序决定，在特性装配时（`AddXiHan` 内部）检查服务集合：

1. 已登记的 `ICacheSerializer`。
2. 已登记的 `JsonSerializerOptions`：用它构造 `JsonCacheSerializer`。
3. 两者都没有：退回 `JsonCacheSerializer.CreateReflectionBased()`（Web 默认的 camelCase、按反射解析类型）。

`JsonCacheSerializer` 要求选项带 `TypeInfoResolver`（源生成上下文），没有时构造即抛 `ArgumentException`；类型信息经 `JsonSerializerOptions.GetTypeInfo` 取得，上下文里没有登记的类型抛 `NotSupportedException`，不悄悄退回反射。缓存的每种类型都要写进上下文：

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Caching.Abstractions.Serialization;
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 必须在 AddXiHan 之前登记：Provider 装配时按已有登记选序列化器
builder.Services.AddSingleton<ICacheSerializer>(new JsonCacheSerializer(new JsonSerializerOptions { TypeInfoResolver = CacheJsonContext.Default }));

builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<RedisCachingFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();

public sealed record ProductView(Guid Id, string Name, int Price);

[JsonSerializable(typeof(ProductView))]
internal sealed partial class CacheJsonContext : JsonSerializerContext;
```

内存实现不序列化，所以只在内存缓存下跑过的代码换成 Redis 后，值类型第一次需要能被序列化；不在上下文里的类型会在那时抛出。

## 分布式锁

`IDistributedLock` 是建立在存储原语上的租约锁：`TryAcquireAsync(resource, ttl)` 用 `TryAddAsync` 写入键 `lock:<resource>`，值是本次获取生成的唯一令牌；释放用 `TryRemoveAsync` 按令牌比较删除。锁不可重入、不续约，到期后别人可以获取，原持有者再释放返回 `false`，不会误删别人的锁。

```csharp
using XiHan.Framework.Caching.Abstractions.Locking;

public sealed class DailyReport(IDistributedLock locks)
{
    public async ValueTask<bool> TryRunAsync(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken)
    {
        var acquired = await locks.TryAcquireAsync("report:daily", TimeSpan.FromMinutes(5), cancellationToken);
        if (!acquired.TryGetValue(out var handle))
        {
            // 别的实例正持有锁
            return false;
        }

        // 释放即按令牌比较删除；工作超过五分钟时锁已到期，别的实例可能已经进来
        await using (handle)
        {
            await work(cancellationToken);
        }

        return true;
    }
}
```

被占用时返回无值，不产生错误码；`CACHING_CONFLICT_001` 已登记，但框架不产出它，应用要把占用报告成失败时可以用。`LockHandle` 只释放一次，重复释放返回 `false`。存活时间要大于工作的最长耗时，需要续约的场景在 `ICacheStore` 的「不存在才写 + 比较删」之上自己实现，`ICacheStore` 由内存与 Redis Provider 登记在容器里（远程 Provider 只能经 `ISharedCache.Store` 取到）。

雪花 ID 的机器位租约就用这把锁，见[标识生成](./distributed-ids)。

## 幂等键

装配了 `caching.distributed` 的 Provider 时，管道的 Idempotency 阶段有 `IdempotencyMiddleware`。调用的 Baggage 带 `xihan.idempotency-key`（`BaggageKeys.IdempotencyKey`）时它按（租户、主体、键）去重，不带时直接放行：

1. 首次调用用 `TryAddAsync` 写入「处理中」标记，存活 `InProgressTimeToLive`（默认 60 秒），然后执行后续阶段。
2. 执行完用调用结果替换标记，保留 `TimeToLive`（默认 24 小时）。成功与失败的结果都会记录。
3. 处理器抛出异常时按值比较删除自己的标记，允许重试。
4. 重复调用遇到「处理中」返回 `CACHING_CONFLICT_002`；遇到已完成的记录直接回放结果，不再执行处理器，`IdempotencyMiddleware.WasReplayed(context)` 为 `true`。

HTTP 上幂等键走 W3C `baggage` 头（gRPC 是同名的 `baggage` 元数据，`conventions/baggage-keys.md`）：

```bash
curl -X POST http://localhost:5000/orders \
  -H "Authorization: ApiKey dev-alice-key" \
  -H "baggage: xihan.idempotency-key=order-20261031-0001" \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","amount":2}'
```

存储键是 `idem:<租户>:<主体>:<幂等键>`（没有租户或匿名时对应段为 `-`），所以不同租户、不同用户提交同一个键互不回放。拼出的键不满足 `CacheKey` 文法（含空白、超过 512 字符）时调用以 `CACHING_VALIDATION_001` 结束。

::: warning 失败的结果同样会被回放
处理器返回的失败（如业务冲突）与成功一样被记录 24 小时，同一幂等键重试只会拿回同一个错误。客户端修正请求后要换一个新键；只有处理器抛出异常时标记会被清除。
:::

回放只还原调用结果，不还原响应体：重复的成功请求不会再执行操作，得到的是没有正文的成功结果。Idempotency 阶段在 Authenticate、Tenant、Authorize、Validate 之后，被这些阶段拒绝的调用不会写入记录；在 Transaction 之前，所以记录的是提交之后的最终结果。阶段顺序见[执行管道](./pipeline)。

## IDistributedCache 适配

沿用 .NET `IDistributedCache` 的代码（ASP.NET Core 会话、第三方库）不需要改调用点：被选中提供 `caching.distributed` 的 Provider 用 `AddXiHanDistributedCache` 把 `CacheStoreDistributedCache` 登记为 `IDistributedCache`，架在同一个字节存储上。应用自己登记的 `IDistributedCache` 优先。

| `DistributedCacheEntryOptions` | 换算 |
| --- | --- |
| `AbsoluteExpiration` / `AbsoluteExpirationRelativeToNow` | 取较早者换算成 `TimeToLive`；已经过去的时刻抛 `ArgumentOutOfRangeException` |
| `SlidingExpiration` | 没有绝对过期时对应 `SlidingExpiration`；与绝对过期同时给时绝对优先 |
| 都不给 | `CacheEntryOptions.Default`，即 Provider 的默认存活时间（300 秒） |

键必须满足 `CacheKey` 文法，否则抛 `ArgumentException`。`Refresh` 是一次读取（读取即续期滑动过期）；同步成员在适配器边界阻塞等待异步存储，调用方应优先用异步成员。

## 两级缓存

`Caching.Hybrid` 提供 `caching.local@1` 并依赖 `caching.distributed@1`：装配后 `ICache` 变成 `HybridCache`，L1 是进程内的对象缓存（有界 LRU），L2 是被选中的 `ISharedCache`；`ISharedCache` 仍是 L2 本身。它省掉读路径上的一次网络往返，代价是 L1 可能短时间内落后于 L2（ADR-0019）。

```csharp
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// Hybrid 顶替内存实现的 caching.local，L2 取 Redis 提供的 caching.distributed
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RedisCachingFeature>()
    .UseFeature<HybridCachingFeature>());
```

```json
{ "XiHan": { "Caching": { "Hybrid": { "LocalCapacity": 10000, "LocalTimeToLiveSeconds": 30 } } } }
```

- 读：L1 命中直接返回，未命中读 L2，命中后回填 L1；批量读只把 L1 未命中的键一次交给 L2。
- 写与删：先 L2 再 L1，L2 失败时 L1 不留下别的实例看不到的值；`GetOrCreateAsync` 两级都未命中才执行工厂。
- L1 的存活时间是条目自己的过期时长与 `LocalTimeToLive`（默认 30 秒）的较小者，滑动过期在 L1 里按绝对时间处理；`LocalCapacity`（默认 10000）满时淘汰最久未访问的条目。

跨实例失效（ADR-0055）依赖 L2 存储的变更通知 `ICacheChangeFeed`。Redis 存储实现了它：每次写入与删除后同步通知本实例的订阅者，并以发后不等的方式向频道 `<KeyPrefix>__changes` 发布，其他实例收到后删掉 L1 里的这些键或前缀。`HybridCache.InvalidatesAcrossInstances` 表示是否订阅到了。通知是尽力而为的，断线期间可能丢失。

L2 的存储没有变更通知时（远程 Provider、内存实现），绕过本实例 `HybridCache` 的写入（别的实例的写入，本实例直接经 `ISharedCache` 或管理界面的写入）不会失效 L1，读到旧值的时间最长为 `LocalTimeToLive`。需要强一致的键直接用 `ISharedCache`，不经 L1。

## 键管理

管理界面需要按前缀列键、看条目元数据、按前缀删除，这些放在两个可选接口里，业务代码拿不到，管理界面显式转型使用（ADR-0055、ADR-0058）：

| 接口 | 从哪里转 | 实现者 | 内容 |
| --- | --- | --- | --- |
| `ICacheStoreInspector` | `ICache.Store` / `ISharedCache.Store` | `InMemoryCacheStore`、`RedisCacheStore` | 字节条目：载荷字节数、剩余存活、滑动窗口 |
| `ICacheInspector` | `ICache` / `ISharedCache` 本身 | `InMemoryObjectCache` | 对象条目：类型全名、剩余存活、滑动窗口 |

内存实现的对象条目存在对象缓存自己的表里，不在字节存储里，所以要两份合起来才是一个缓存的全部条目；Redis 的条目都序列化进存储，只有 `ICacheStoreInspector`。远程插件两个都不实现。

```csharp
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;

public sealed class CacheAdmin(ISharedCache cache)
{
    public async ValueTask<List<string>> ListAsync(string prefix, CancellationToken cancellationToken)
    {
        var keys = new List<string>();
        // 对象条目（内存实现）与字节条目各列一遍
        if (cache is ICacheInspector objects)
        {
            await CollectAsync(query => objects.ListKeysAsync(query, cancellationToken), prefix, keys);
        }

        if (cache.Store is ICacheStoreInspector bytes)
        {
            await CollectAsync(query => bytes.ListKeysAsync(query, cancellationToken), prefix, keys);
        }

        return keys;
    }

    private static async ValueTask CollectAsync(Func<CacheKeyQuery, ValueTask<CacheKeyPage>> list, string prefix, List<string> keys)
    {
        string? continuation = null;
        do
        {
            var page = await list(new CacheKeyQuery { Prefix = prefix, Limit = 200, Continuation = continuation });
            keys.AddRange(page.Keys.Select(static k => k.Value));
            continuation = page.Continuation;
        }
        while (continuation is not null);
    }
}
```

- `ListKeysAsync`：`Prefix` 与键同文法，空为全部；`Limit` 为 1 到 1000（默认 100）；没有更多时 `Continuation` 为 `null`。已过期的键不列出。
- `InspectAsync`：不存在或已过期为无值；读元数据不延长滑动过期，对象条目也不读出值。
- `RemoveByPrefixAsync`：返回删除数；前缀不能为空，空前缀抛 `ArgumentException`，没有「清空全部」。
- 内存实现按键的序数顺序分页，续页标记是上一页最后一个键。Redis 用单个端点的 `SCAN`：续页标记是游标，`Limit` 只是提示值，同一个键可能出现在两页；前缀里的 glob 字符会被转义，按前缀匹配而不是按模式。

读值、改值、删单个键仍走 `ICache` / `ICacheStore`。经 Redis 存储删除的键同样触发变更通知，各实例两级缓存的 L1 随之失效。

## 远程 Provider

`Caching.Remote` 让任意语言写的进程外插件承担 `caching.distributed`（RFC-0019《远程 Provider》，ADR-0011）。插件实现固定的操作集 `cache-entries.get / set / try-add / remove / try-remove / get-many / set-many / remove-many`（`provider/caching-distributed.md`），由 `Plugins.OutOfProcess` 按目录发现并登记代理操作；`Caching.Remote` 按操作 ID 直接执行这些代理操作（不经过管道，否则幂等中间件会递归），把它接成 `ISharedCache`、`IDistributedLock`、`IDistributedCache` 与幂等中间件。

```csharp
using XiHan.Framework.Caching.Remote;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Plugins.OutOfProcess;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OutOfProcessPluginsFeature>()
    .UseFeature<RemoteCachingFeature>());
```

```json
{
  "XiHan": {
    "Plugins": { "OutOfProcess": { "Directories": ["plugins/cache-go"] } },
    "Caching": { "Remote": { "TimeoutMilliseconds": 5000 } }
  }
}
```

每次操作带 `TimeoutMilliseconds`（默认 5000）的截止时间。插件返回的缓存域错误（`CACHING_VALIDATION_00x`）原样成为 `FrameworkException`；插件未加载、操作未登记、进程不可达、超时都映射为 `CACHING_UNAVAILABLE_001`。Go 实现见 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 的 `caching` 包，示例插件在 `next/samples/CacheGoPlugin`。远程 Provider 是 Experimental，没有键管理与变更通知；插件机制见[插件](./plugins)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Caching:InMemory:MaxEntries` | 整数 | 10000 | 内存实现每张表（字节存储、对象缓存各一张）的条目上限，满时淘汰最久未访问的条目 |
| `XiHan:Caching:InMemory:DefaultTimeToLiveSeconds` | 数字 | 300 | 内存实现未指定过期时的存活时间 |
| `XiHan:Caching:Redis:Configuration` | 字符串 | — | StackExchange.Redis 连接字符串；优先于 `cache` 绑定 |
| `XiHan:Caching:Redis:KeyPrefix` | 字符串 | `xihan:` | Redis 键前缀，也决定变更频道 `<KeyPrefix>__changes` |
| `XiHan:Caching:Redis:DefaultTimeToLiveSeconds` | 数字 | 300 | Redis 未指定过期时的存活时间 |
| `XiHan:Caching:Redis:PublishChanges` | 布尔 | `true` | 写入与删除后是否向变更频道发布 |
| `XiHan:Caching:Redis:HealthProbeIntervalSeconds` | 数字 | 15 | 健康项 `caching.redis.connection` 的重探间隔 |
| `XiHan:Caching:Hybrid:LocalCapacity` | 整数 | 10000 | 两级缓存 L1 的条目上限 |
| `XiHan:Caching:Hybrid:LocalTimeToLiveSeconds` | 数字 | 30 | L1 存活时间上限 |
| `XiHan:Caching:Remote:TimeoutMilliseconds` | 整数 | 5000 | 远程 Provider 每次操作的截止时间 |
| `XiHan:Caching:Idempotency:TimeToLiveSeconds` | 数字 | 86400 | 已完成调用结果的保留时间 |
| `XiHan:Caching:Idempotency:InProgressTimeToLiveSeconds` | 数字 | 60 | 「处理中」标记的保留时间，也是处理器崩溃后同一个键被封锁的最长时间 |

数值项不是正数、布尔项不是 `true` / `false` 时装配抛出 `ArgumentException`；`TimeoutMilliseconds` 例外，非法值按 5000 处理。

## 健康项与错误码

| 健康项 | 来源 | Degraded 的条件 |
| --- | --- | --- |
| `caching.inmemory.capacity` | `Caching.InMemory` | 两张表任一条目数达到 `MaxEntries`（`CACHING_UNAVAILABLE_002`）；缓存仍可用，但每次写入都在淘汰 |
| `caching.redis.connection` | `Caching.Redis` | 后台 PING 失败或超时（`KERNEL_UNAVAILABLE_005`） |

| 错误码 | 含义与出现位置 |
| --- | --- |
| `CACHING_VALIDATION_001` | 幂等键拼出的存储键不合法，Idempotency 阶段以校验错误结束调用；远程插件报告键不合法 |
| `CACHING_VALIDATION_002` | 远程插件报告过期选项不合法；.NET 侧的非法选项直接抛 `ArgumentException` |
| `CACHING_UNAVAILABLE_001` | 存储不可达或操作失败，以 `FrameworkException` 抛出 |
| `CACHING_UNAVAILABLE_002` | 内存缓存已达容量上限，只作为健康项的原因，不拒绝写入 |
| `CACHING_CONFLICT_001` | 分布式锁被占用；已登记，框架不产出 |
| `CACHING_CONFLICT_002` | 同一幂等键的调用仍在处理中 |

非法的 `CacheKey`、同时设置两种过期、非正的存活时间都是编程错误，抛 `ArgumentException`，不转成 `Result`。

## 相关

- [Caching.Abstractions](../packages/caching-abstractions)、[Caching.InMemory](../packages/caching-inmemory)、[Caching.Redis](../packages/caching-redis)、[Caching.Hybrid](../packages/caching-hybrid)、[Caching.Remote](../packages/caching-remote)
- [执行管道](./pipeline)：Idempotency 阶段的位置
- [标识生成](./distributed-ids)：用分布式锁租约机器位
- [插件](./plugins)：进程外插件与远程 Provider
- [测试与一致性套件](./testing)：Provider 必须通过 `CacheStoreConformance`
- 规范：RFC-0006《缓存、分布式锁与幂等》、RFC-0019《远程 Provider》；ADR-0011、ADR-0012、ADR-0019、ADR-0055、ADR-0058
