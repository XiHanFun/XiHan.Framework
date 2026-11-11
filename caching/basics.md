来源：https://framework.docs.xihanfun.com/caching/basics

# 缓存读写

这一页讲缓存子系统最常用的部分：它提供哪两个能力、字节存储与类型化缓存是什么关系、怎样启用内存与 Redis 实现、怎样读写、设置过期、批量操作、挑选序列化器，以及对应的配置、健康项与错误码。读完你能给自己的特性加上一层缓存，并在单实例与多实例部署之间切换 Provider 而不改业务代码。前置知识是[特性与清单](/startup/features)与[能力图谱与 Provider](/startup/capabilities)：缓存和别的子系统一样，按能力 ID 声明依赖、按清单选实现。

## 为什么需要

同一份数据被反复读取时，每次都查数据库或调用远端既慢又浪费。把结果缓存起来能把读路径缩短到一次内存或 Redis 访问，但缓存自己也带来问题：进程内的缓存在多实例部署下各实例互不可见；跨进程的缓存要序列化、要网络往返、后端可能不可达；过期、容量与批量读写各家实现语义不一。框架把这些收口成两个能力与一套契约：业务代码只面对 `ICache` / `ISharedCache` 读写对象，Provider 负责字节存储；默认的内存实现随 Standard 组合包装配、零配置可用，多实例时再装 Redis 顶替，调用方一行不改。存储不可达时统一抛出错误码，不会把故障伪装成未命中。

## 两个能力

缓存子系统提供两个能力（规范 RFC-0006《缓存、分布式锁与幂等》）：

| 能力 | 服务 | 默认实现 | 显式 Provider |
| --- | --- | --- | --- |
| `caching.local@1` | `ICache`：进程内缓存 | `Caching.InMemory` | `Caching.Hybrid`（两级缓存） |
| `caching.distributed@1` | `ISharedCache`、`ICacheStore`（字节存储）、`IDistributedLock`、`IDistributedCache`、Idempotency 阶段中间件 | `Caching.InMemory`（只在单实例下正确） | `Caching.Redis`、`Caching.Remote` |

`Caching.InMemory` 同时声明两个能力，[Profiles.Standard](/packages/profiles-standard) 已经装配它，所以不加任何 Provider 时两种缓存都能用。再 `UseFeature` 一个显式 Provider 就顶替对应能力：装配 `Caching.Redis` 后共享缓存、锁与幂等改由 Redis 承担，进程内的 `ICache` 仍由内存实现提供；被顶替的能力内存实现不再注册服务，也不登记重复的幂等中间件。

| 装配 | `ICache` | `ISharedCache` 与锁、幂等 |
| --- | --- | --- |
| `UseStandardProfile()` | `InMemoryObjectCache` | 同一个 `InMemoryObjectCache` |
| 再加 `RedisCachingFeature` | `InMemoryObjectCache` | `SerializingCache` + `RedisCacheStore` |
| 再加 `HybridCachingFeature` | `HybridObjectCache`（L1 + 内存 L2） | `InMemoryObjectCache` |
| 再加 `RedisCachingFeature` 与 `HybridCachingFeature` | `HybridObjectCache`（L1 + Redis L2） | Redis |
| 再加 `OutOfProcessPluginsFeature` 与 `RemoteCachingFeature` | `InMemoryObjectCache` | `SerializingCache` + `RemoteCacheStore` |

两个显式 Provider 同时提供 `caching.distributed`（如 Redis 与 Remote）时能力求解报 `KERNEL_CAPABILITY_004`，用配置键 `XiHan:Providers:caching.distributed` 写清单 ID（`xihan.caching.redis`、`xihan.caching.remote`）指定一个，规则见[能力图谱与 Provider](/startup/capabilities)。

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

缓存键是 `CacheKey`：非空、不含空白与控制字符、不超过 512 个字符（`CacheKey.MaxLength`）、区分大小写；从字符串创建用构造或显式转换（`new CacheKey(text)`、`(CacheKey)text`），不合法时抛 `ArgumentException`；`default(CacheKey)` 不是合法键，读 `Value` 抛 `InvalidOperationException`。缓存不自动按租户隔离键，多租户应用要把租户 ID 写进键里（幂等中间件是例外，见[分布式锁与幂等](/caching/locks)）。

## 启用

内存实现随 `UseStandardProfile()` 装配，不需要额外步骤。换成 Redis 分三步：

1. 引用 `XiHan.Framework.Caching.Redis`：`dotnet add package XiHan.Framework.Caching.Redis --prerelease`。
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

连接取宿主注入的 `cache` 绑定，没有绑定时取 `XiHan:Caching:Redis:Configuration`（两者都给时绑定优先，绑定的来源见[资源绑定](/configuration/bindings)）；两者都没有时装配直接失败；启动时连不上 Redis 同样让启动失败（`CACHING_UNAVAILABLE_001`），不降级为内存缓存。

## 最小示例

特性在清单里声明自己依赖的缓存能力，再把用缓存的服务登记进容器。下面的 `CatalogFeature` 依赖 `caching.local`，`ProductCache` 用 `GetOrCreateAsync` 做读穿透：命中直接返回，未命中执行工厂并写入，十分钟后过期。

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

在 `AddXiHan` 里 `UseFeature<CatalogFeature>()` 后，第一次调用 `GetAsync` 会执行 `load`，十分钟内的后续调用直接拿到同一个 `ProductView` 实例；调用 `InvalidateAsync` 返回 `true` 表示删到了，再读会重新加载。把清单里的 `CachingCapabilities.Local` 换成 `CachingCapabilities.Distributed`、把 `ICache` 换成 `ISharedCache`，这份代码就在多实例间共享缓存，前提是装配了 Redis。

`GetOrCreateAsync` 不合并并发：多个调用同时未命中时各自执行工厂，后写入的覆盖先写入的。工厂代价高、需要只算一次时，先用[分布式锁](/caching/locks)串行化。

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

两种过期只能二选一，同时设置时写入抛 `ArgumentException`；时长必须为正。Provider 的默认存活时间都是 300 秒，分别由 `XiHan:Caching:InMemory:DefaultTimeToLiveSeconds` 与 `XiHan:Caching:Redis:DefaultTimeToLiveSeconds` 调整，远程插件自行决定。经框架写入的条目总有过期时间，没有「永不过期」。

## 批量方法

存储与类型化缓存都提供批量方法（规范的批量优先约定），一次调用、一次往返，结果与入参顺序一一对应：

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

内存实现不序列化，所以只在内存缓存下跑过的代码换成 Redis 后，值类型第一次需要能被序列化；不在上下文里的类型会在那时抛出。源生成上下文的写法见[JSON 序列化与 Native AOT](/common/serialization)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Caching:InMemory:MaxEntries` | 整数 | 10000 | 内存实现每张表（字节存储、对象缓存各一张）的条目上限，满时淘汰最久未访问的条目 |
| `XiHan:Caching:InMemory:DefaultTimeToLiveSeconds` | 数字 | 300 | 内存实现未指定过期时的存活时间 |
| `XiHan:Caching:Redis:Configuration` | 字符串 | — | StackExchange.Redis 连接字符串；有 `cache` 绑定时被绑定的主值覆盖，绑定的属性 `username` / `password` 覆盖其中的 `user` / `password` |
| `XiHan:Caching:Redis:KeyPrefix` | 字符串 | `xihan:` | Redis 键前缀，也决定变更频道 `<KeyPrefix>__changes` |
| `XiHan:Caching:Redis:DefaultTimeToLiveSeconds` | 数字 | 300 | Redis 未指定过期时的存活时间 |
| `XiHan:Caching:Redis:PublishChanges` | 布尔 | `true` | 写入与删除后是否向变更频道发布，供[两级缓存](/caching/hybrid)失效 L1 |
| `XiHan:Caching:Redis:HealthProbeIntervalSeconds` | 数字 | 15 | 健康项 `caching.redis.connection` 的重探间隔 |

数值项不是正数、布尔项不是 `true` / `false` 时装配抛出 `ArgumentException`。两级缓存、远程 Provider 与幂等的配置键见对应页面。

## 健康项与错误码

| 健康项 | 来源 | Degraded 的条件 |
| --- | --- | --- |
| `caching.inmemory.capacity` | `Caching.InMemory` | 两张表任一条目数达到 `MaxEntries`（`CACHING_UNAVAILABLE_002`）；缓存仍可用，但每次写入都在淘汰 |
| `caching.redis.connection` | `Caching.Redis` | 后台 PING 失败或超时（`KERNEL_UNAVAILABLE_005`） |

| 错误码 | 含义与出现位置 |
| --- | --- |
| `CACHING_UNAVAILABLE_001` | 存储不可达或操作失败，以 `FrameworkException` 抛出 |
| `CACHING_UNAVAILABLE_002` | 内存缓存已达容量上限，只作为健康项的原因，不拒绝写入 |
| `CACHING_VALIDATION_001` / `002` | 幂等键拼出的存储键不合法；远程插件报告键或过期选项不合法，见[分布式锁与幂等](/caching/locks)与[两级缓存与键管理](/caching/hybrid) |
| `CACHING_VALIDATION_003` | 键列表的每页上限不在 1～1000，见[两级缓存与键管理](/caching/hybrid) |
| `CACHING_CONFLICT_001` / `002` / `003` | 锁被占用、幂等调用处理中、幂等记录不能回放，见[分布式锁与幂等](/caching/locks) |

非法的 `CacheKey`、同时设置两种过期、非正的存活时间都是编程错误，抛 `ArgumentException`，不转成 `Result`。健康项怎样汇总到就绪探针见[生命周期与健康](/startup/lifecycle)。

## 常见问题

### 缓存会按租户自动隔离吗

不会。`ICache` / `ISharedCache` 的键就是你给的字符串，多租户应用要自己把租户 ID 拼进键里，例如 `$"{tenantId}:product:{id:N}"`。只有幂等中间件例外，它的存储键自带租户与主体段。

### 从内存缓存取回的对象能直接修改吗

不能。进程内实现存的是对象引用，改了它等于改了缓存里的值，其他调用方会读到被改过的状态。要修改就先复制，或把缓存值设计成不可变的 `record`。

### 为什么换成 Redis 后抛出 NotSupportedException

跨进程缓存要序列化值，`JsonCacheSerializer` 只认源生成上下文里登记的类型。在内存缓存下跑得好好的代码换 Redis 后第一次写入某个类型时就会暴露。把类型加进 `[JsonSerializable]` 上下文，或在 `AddXiHan` 之前登记自己的 `ICacheSerializer`。

### 两个调用同时未命中，工厂会跑两次吗

会。`GetOrCreateAsync` 不合并并发，各自执行工厂、后写入的覆盖先写入的，对幂等的读操作通常无害。工厂很贵又必须只算一次时，用 `IDistributedLock` 把它串行化，见[分布式锁与幂等](/caching/locks)。

## 相关包与下一步

- 包参考：[Caching.Abstractions](/packages/caching-abstractions)、[Caching.InMemory](/packages/caching-inmemory)、[Caching.Redis](/packages/caching-redis)
- Provider 要通过的一致性套件见[一致性套件与门禁](/testing/conformance)
- 下一步：[分布式锁与幂等](/caching/locks)，在同一个字节存储上做互斥与请求去重
