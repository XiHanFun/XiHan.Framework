# 缓存读写

这一页讲缓存子系统最常用的部分：它提供哪两个能力、字节存储与类型化缓存是什么关系、怎样启用内存与 Redis 实现、怎样读写、设置过期、批量操作、用按范围失效的缓存区缓存会被写入改变的数据、挑选序列化器，以及对应的配置、健康项与错误码。读完你能给自己的特性加上一层缓存，并在单实例与多实例部署之间切换 Provider 而不改业务代码。前置知识是[特性与清单](/startup/features)与[能力图谱与 Provider](/startup/capabilities)：缓存和别的子系统一样，按能力 ID 声明依赖、按清单选实现。

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

框架自己的几处缓存也走 `ICache`：`Security.Database` 的授予缓存、`MultiTenancy.Database` 的租户目录缓存、`Localization.Database` 的翻译版本戳，三个包的清单都依赖 `caching.local@1`。它们在写入后让所有共用缓存的实例立即失效，前提是 `ICache` 本身跨实例失效：多实例部署装 `Caching.Redis` 与 `Caching.Hybrid`。只装 Redis 时 `ICache` 仍是进程内的内存实现，这些缓存跨实例只靠存活时间收敛。

## 字节存储与类型化缓存

子系统只有一个存储契约 `ICacheStore`，按字节读写，Provider 实现它：

| 操作 | 语义 |
| --- | --- |
| `GetAsync` | 不存在或已过期为无值；滑动过期的条目在读取时延长 |
| `SetAsync` | 写入或覆盖 |
| `TryAddAsync` | 不存在（或已过期）才写入，原子 |
| `RemoveAsync` | 删除，返回删除前是否存在 |
| `TryRemoveAsync` | 当前值等于期望值才删除，原子 |
| `TryRefreshAsync` | 当前值等于期望值才把到期时刻重设为现在加给定的存活时间，原子；不改值，锁续约用它 |
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

## 按范围失效的缓存区

上面的读穿透有两个缺口：写入要失效一批条目时，必须知道缓存里有哪些键（`ICache` 没有按前缀删除）；一次读取在写入之前查了源、在失效之后才写回，旧值就留到过期。`GenerationCache<T>`（Preview）架在 `ICache` 上补这两处，框架的授予缓存（[RBAC 授予与条件](/security/grants)）与租户目录缓存（[数据库租户目录](/multi-tenancy/database)）就用它。

条目按两段定位：范围（如主体、租户）与范围下的条目（如租户、商品类别），写成 `GenerationCacheKey(scope, entry)`，只有范围时用 `GenerationCacheKey.ForScope(scope)`（条目为空字符串）。

| 成员 | 语义 |
| --- | --- |
| `GenerationCache(cache, region, timeToLive, logger)` | `region` 是所有键的前缀，须是合法的缓存键文本、不超过 128 个字符（`MaxRegionLength`）；`timeToLive` 必须为正 |
| `GetOrLoadManyAsync(keys, load, ct)` | 批量读，返回 `Result<IReadOnlyList<T>>`，与 `keys` 顺序一一对应；重复的位置只读一次、只查一次源，空集合不触缓存。未命中的位置去重后一次交给 `load`，它按同样的顺序返回同样多的值（条数不符抛 `InvalidOperationException`）；`load` 失败原样返回、不写缓存 |
| `InvalidateAsync(scopes, ct)` | 让这些范围下的全部条目失效，一次批量写入；空集合什么也不做 |
| `InvalidateAllAsync(ct)` | 让整个区失效 |
| `TimeToLive` | 条目的存活时间 |

```csharp
using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Primitives;

public sealed record PriceList(string TenantId, string Category, IReadOnlyDictionary<string, decimal> Prices);

// 应用自己的批量查询：按入参顺序返回，查不到的给空价目表；读库失败返回失败
public interface IPriceSource
{
    ValueTask<Result<IReadOnlyList<PriceList>>> LoadManyAsync(IReadOnlyList<GenerationCacheKey> keys, CancellationToken cancellationToken);
}

// 单例：缓存区记着故障日志的节流状态
public sealed class TenantPriceCache(ICache cache, IPriceSource source, ILogger<TenantPriceCache> logger)
{
    // 范围是租户，条目是商品类别
    private readonly GenerationCache<PriceList> _prices = new(cache, "acme:prices", TimeSpan.FromMinutes(5), logger);

    public ValueTask<Result<IReadOnlyList<PriceList>>> GetAsync(string tenantId, IReadOnlyList<string> categories, CancellationToken cancellationToken)
        => _prices.GetOrLoadManyAsync([.. categories.Select(category => new GenerationCacheKey(tenantId, category))], source.LoadManyAsync, cancellationToken);

    // 租户调价：它下面所有类别的条目一起作废，所有共用这个缓存的实例都看得到
    public ValueTask InvalidateAsync(string tenantId, CancellationToken cancellationToken)
        => _prices.InvalidateAsync([tenantId], cancellationToken);
}
```

它怎样做到：

- 区与每个范围各有一个代数（随机令牌），也存在 `ICache` 里。条目写回时记下读取开始时的区代数与范围代数，读取时两个代数都还是当前值才算命中。失效就是换一个新代数，不需要知道范围下有哪些条目。
- 读取在查源之前取代数，缺代数（从未写过、已过期或被淘汰）时先写下新代数再查源。失效之前开始的读取写回的条目带着旧代数，不会被当作命中，失效之后才开始的读取看到新代数。
- 代数的存活时间是条目的 10 倍；代数丢了只会让旧条目作废、多查一次源，不会把旧条目当作命中。
- 键是 `<区>:gen`、`<区>:gen:<范围>`、`<区>:entry:<范围>:<条目>`。范围与条目写成 `<长度>.<文本>`；含空白、控制字符或超过 128 个字符的换成 `#<SHA-256 十六进制>`。共用一个 Redis 的多个应用要用不同的 `KeyPrefix`，否则同名的区会互相覆盖。
- 跨进程缓存里存的是 `GenerationCacheEntry<T>`（区代数、范围代数、值）与 `string`（代数），序列化器要认识这两种类型，见下一节。

缓存后端故障（`CACHING_UNAVAILABLE_001`）时，`GetOrLoadManyAsync` 绕过缓存直接查源、不写回，结果仍然正确，每个区每 10 秒最多记一条警告；`InvalidateAsync` 与 `InvalidateAllAsync` 把异常抛给调用方，由它决定拒绝写入还是记日志。写入方的常见做法与框架相同：在写入的事务里失效一次，失败就让调用失败、事务回滚；提交之后再失效一次，挡住在提交之前读到旧数据的读取，这次失败只记错误日志，靠存活时间收敛。

多实例一致的前提是 `ICache` 跨实例失效：装 `Caching.Redis` 与 `Caching.Hybrid` 后，代数与条目都在本机一级缓存里命中、读取不出进程，一个实例换了代数，Redis 的变更通知让其他实例的一级缓存丢掉旧代数（[两级缓存与键管理](/caching/hybrid)）。只装 Redis 时 `ICache` 仍是进程内的，其他实例只能等存活时间。

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

组件放进缓存的类型不必写进应用的上下文：组件在特性的 `Configure` 里用 `CacheSerializerRegistration.AddTypeInfo(services, resolver)` 登记自己的源生成上下文（容器里是 `CacheTypeInfoSource`，同一个解析器只登记一次）。三种情况的处理不同：

- 应用登记了 `JsonSerializerOptions`（第 2 种）：组件登记的上下文并在应用的解析器之后，同一类型两边都有时按应用的。
- 退回反射（第 3 种）：本来就认识全部类型，不需要处理。
- 应用自己登记 `ICacheSerializer`（第 1 种）：要自己处理。框架的 `Security.Database`、`MultiTenancy.Database`、`Localization.Database` 会往 `ICache` 里放 `GenerationCacheEntry<GrantSetEntry>`、`GenerationCacheEntry<TenantCacheValue>` 与 `string`，前两个值类型是包内部的，用工厂登记、从容器取出组件的上下文并进来：

```csharp
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Caching.Abstractions.Serialization;

// 工厂在容器建好后才执行，那时各特性的 AddTypeInfo 都已登记
builder.Services.AddSingleton<ICacheSerializer>(static sp => new JsonCacheSerializer(new JsonSerializerOptions
{
    TypeInfoResolver = JsonTypeInfoResolver.Combine([CacheJsonContext.Default, .. sp.GetServices<CacheTypeInfoSource>().Select(static source => source.Resolver)]),
}));
```

自己的组件把 `GenerationCache<T>` 放进跨进程缓存时，上下文里写 `[JsonSerializable(typeof(GenerationCacheEntry<T>))]` 与 `[JsonSerializable(typeof(string))]`，再用 `AddTypeInfo` 登记。

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
