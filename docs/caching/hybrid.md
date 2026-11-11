# 两级缓存与键管理

这一页讲三件进阶的事：`Caching.Hybrid` 把进程内缓存与共享缓存叠成两级、读少一次网络往返；`ICacheStoreInspector` / `ICacheInspector` / `ICacheChangeFeed` 给管理界面列键、看元数据、按前缀删除与订阅变更；`Caching.Remote` 让任意语言写的进程外插件承担共享缓存。读完你能在多实例部署下压低缓存读延迟、写出缓存管理页面，并知道远程 Provider 的边界。前置知识是[缓存读写](/caching/basics)。

## 为什么需要

装配了 Redis 之后，每次 `ISharedCache` 读取都是一次网络往返，读多写少的热点数据（配置、字典、权限）为此付出的延迟远高于真正变化的频率。两级缓存在本进程放一层有界的 L1，命中就不出进程，未命中再读 L2 并回填；代价是 L1 可能短时间落后于 L2，所以框架用 L2 的变更通知跨实例失效 L1，并给 L1 一个存活上限兜底。另一方面，运维与管理界面需要「看看缓存里有什么、按前缀清掉一批」，这不该混进业务契约，框架把它们放进可选接口，由存储实现自愿支持。远程 Provider 则服务于已经有别的语言的缓存实现、或后端不是 Redis 的团队。

## 两级缓存的概念

`Caching.Hybrid` 提供 `caching.local@1` 并依赖 `caching.distributed@1`：装配后 `ICache` 变成 `HybridObjectCache`，`ISharedCache` 仍是 L2 本身。

| 术语 | 说明 |
| --- | --- |
| L1 | 进程内的对象缓存，有界 LRU，直接存对象引用 |
| L2 | 被选中提供 `caching.distributed` 的 `ISharedCache`（Redis、远程或内存实现） |
| `LocalCapacity` | L1 条目上限，缺省 10000，满时淘汰最久未访问的条目 |
| `LocalTimeToLive` | L1 存活上限，缺省 30 秒；条目在 L1 里的寿命是自身过期与它的较小者 |
| `ICacheChangeFeed` | L2 存储的变更通知；Redis 存储实现它，别的实例写入或删除后本实例 L1 立即失效 |
| `HybridObjectCache.InvalidatesAcrossInstances` | 是否订阅到了 L2 的变更通知 |

## 最小示例

Hybrid 顶替内存实现的 `caching.local`，L2 取 Redis 提供的 `caching.distributed`：

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

var app = builder.Build();
app.UseXiHan();
app.Run();
```

```json
{
  "XiHan": {
    "Caching": {
      "Redis": { "Configuration": "localhost:6379" },
      "Hybrid": { "LocalCapacity": 10000, "LocalTimeToLiveSeconds": 30 }
    }
  }
}
```

业务代码不变：[缓存读写](/caching/basics)里的 `ProductCache` 注入的还是 `ICache`，只是现在第一次 `GetOrCreateAsync` 两级都未命中才执行工厂，写入先到 Redis 再进 L1；同一实例 30 秒内的再次读取不出进程，别的实例第一次读取从 Redis 取到后回填自己的 L1。在别的实例上调用 `InvalidateAsync` 删掉这个键，Redis 发出变更通知，本实例的 L1 随之删掉，下一次读取回到 Redis。单实例开发时 L2 可以就是内存实现，行为一样，只是没有跨实例失效可谈。

## 读写规则

- 读：L1 命中直接返回，未命中读 L2，命中后回填 L1；批量读只把 L1 未命中的键一次交给 L2。
- 写与删：先 L2 再 L1，L2 失败时 L1 不留下别的实例看不到的值；`GetOrCreateAsync` 两级都未命中才执行工厂。
- L1 的存活时间是条目自己的过期时长与 `LocalTimeToLive` 的较小者，滑动过期在 L1 里按绝对时间处理；`LocalCapacity` 满时淘汰最久未访问的条目。
- L1 存对象引用，取回的对象不得修改，与内存实现的约定一致。

## 跨实例失效

跨实例失效依赖 L2 存储的变更通知 `ICacheChangeFeed`。Redis 存储实现了它：每次写入与删除后同步通知本实例的订阅者，并以发后不等的方式向频道 `<KeyPrefix>__changes` 发布，其他实例收到后删掉 L1 里的这些键或前缀（`CacheChange.Keys` / `CacheChange.Prefix`）。`HybridObjectCache.InvalidatesAcrossInstances` 表示是否订阅到了。通知是尽力而为的，断线期间可能丢失；`XiHan:Caching:Redis:PublishChanges` 设为 `false` 可以关掉发布。

L2 的存储没有变更通知时（远程 Provider、内存实现），绕过本实例 `HybridObjectCache` 的写入（别的实例的写入，本实例直接经 `ISharedCache` 或管理界面的写入）不会失效 L1，读到旧值的时间最长为 `LocalTimeToLive`。需要强一致的键直接用 `ISharedCache`，不经 L1。

## 键管理

管理界面需要按前缀列键、看条目元数据、按前缀删除，这些放在两个可选接口里，业务代码拿不到，管理界面显式转型使用：

| 接口 | 从哪里转 | 实现者 | 内容 |
| --- | --- | --- | --- |
| `ICacheStoreInspector` | `ICache.Store` / `ISharedCache.Store` | `InMemoryCacheStore`、`RedisCacheStore` | 字节条目 `CacheEntryInfo`：载荷字节数、剩余存活、滑动窗口 |
| `ICacheInspector` | `ICache` / `ISharedCache` 本身 | `InMemoryObjectCache` | 对象条目 `CachedObjectInfo`：类型全名、剩余存活、滑动窗口 |

内存实现的对象条目存在对象缓存自己的表里，不在字节存储里，所以要两份合起来才是一个缓存的全部条目；Redis 的条目都序列化进存储，只有 `ICacheStoreInspector`。远程插件两个都不实现。

```csharp
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

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

    private static async ValueTask CollectAsync(Func<CacheKeyQuery, ValueTask<Result<CacheKeyPage>>> list, string prefix, List<string> keys)
    {
        string? continuation = null;
        do
        {
            // 每页条数来自请求时越界是 CACHING_VALIDATION_003（400）；这里是常量，直接取值
            var page = (await list(new CacheKeyQuery { Prefix = prefix, PageSize = 200, ContinuationToken = continuation })).ValueOrThrow();
            keys.AddRange(page.Items.Select(static k => k.Value));
            continuation = page.ContinuationToken;
        }
        while (continuation is not null);
    }
}
```

三个方法的语义：

- `ListKeysAsync`：返回 `Result<CacheKeyPage>`。`Prefix` 与键同文法，空为全部（不合法在构造查询时抛 `ArgumentException`）；`PageSize` 为 1 到 1000（`CacheKeyQuery.MaxPageSize`，缺省 `DefaultPageSize` 100），越界不在构造时抛出，存储先经 `CacheKeyQuery.Validate()` 核对、返回 `CACHING_VALIDATION_003`（HTTP 400，参数 `pageSize`、`max`），不访问后端；本页的键在 `Items`，没有更多时 `ContinuationToken` 为 `null`。已过期的键不列出。
- `InspectAsync`：不存在或已过期为无值；读元数据不延长滑动过期，对象条目也不读出值。
- `RemoveByPrefixAsync`：返回删除数；前缀不能为空，空前缀抛 `ArgumentException`，没有「清空全部」。

内存实现按键的序数顺序分页，续页标记是上一页最后一个键。Redis 用单个端点的 `SCAN`：续页标记是游标，`PageSize` 只是提示值，同一个键可能出现在两页；前缀里的 glob 字符会被转义，按前缀匹配而不是按模式。读值、改值、删单个键仍走 `ICache` / `ICacheStore`。经 Redis 存储删除的键同样触发变更通知，各实例两级缓存的 L1 随之失效。

## 远程 Provider

`Caching.Remote` 让任意语言写的进程外插件承担 `caching.distributed`（规范 RFC-0019《远程 Provider》）。插件实现固定的操作集 `cache-entries.get / set / try-add / remove / try-remove / get-many / set-many / remove-many`（常量在 `RemoteCacheOperations`），由 `Plugins.OutOfProcess` 按目录发现并登记代理操作；`Caching.Remote` 按操作 ID 直接执行这些代理操作（不经过管道，否则幂等中间件会递归），把它接成 `ISharedCache`、`IDistributedLock`、`IDistributedCache` 与幂等中间件。

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

每次操作带 `TimeoutMilliseconds`（默认 5000）的截止时间。插件返回的缓存域错误（`CACHING_VALIDATION_001` 键不合法、`CACHING_VALIDATION_002` 过期选项不合法）原样成为 `FrameworkException`；插件未加载、操作未登记、进程不可达、超时都映射为 `CACHING_UNAVAILABLE_001`。Go 实现见 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 的 `caching` 包，示例插件在 `framework/samples/CacheGoPlugin`，它读 `XiHan:Cache:DefaultTimeToLiveSeconds` 作默认存活时间（缺省 300）。远程 Provider 是 Experimental，没有键管理与变更通知；插件机制见[进程外插件](/plugins/out-of-process)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Caching:Hybrid:LocalCapacity` | 整数 | 10000 | 两级缓存 L1 的条目上限 |
| `XiHan:Caching:Hybrid:LocalTimeToLiveSeconds` | 数字 | 30 | L1 存活时间上限 |
| `XiHan:Caching:Redis:PublishChanges` | 布尔 | `true` | Redis 写入与删除后是否向变更频道发布 |
| `XiHan:Caching:Remote:TimeoutMilliseconds` | 正整数 | 5000 | 远程 Provider 每次操作的截止时间 |

数值项不是正数、布尔项不是 `true` / `false` 时装配抛出 `ArgumentException`。

## 常见问题

### L1 最多会落后 L2 多久

L2 带变更通知（Redis）且通知没有丢时，别的实例一写入本实例的 L1 就失效，落后只有网络传播的时间。通知丢失或 L2 没有变更通知时，上限是 `LocalTimeToLive`（缺省 30 秒）。对一致性要求高的键直接用 `ISharedCache`，不经 L1。

### 单实例要不要装两级缓存

不必要。单实例下 `ICache` 本来就是内存实现，没有网络往返可省。两级缓存的价值在于 L2 是 Redis 或远程存储、而读又很频繁的多实例部署。

### 装了 Hybrid 之后 ISharedCache 也走 L1 吗

不走。`ISharedCache` 始终直接指向 L2，所以经它的写入与读取总是看到共享状态。只有 `ICache` 变成了 `HybridObjectCache`。

### 怎样清空整个缓存

没有「清空全部」的接口：`RemoveByPrefixAsync` 要求非空前缀。给应用的键统一加一个前缀（如 `catalog:`），需要整体失效时按这个前缀删除；Redis 上还可以换一个 `KeyPrefix` 让旧条目自然过期。

### Redis 列键时同一个键出现了两次

`SCAN` 的语义就是这样，`PageSize` 只是提示、游标分页不保证不重复。管理界面在客户端去重即可；内存实现按序数顺序分页，没有这个问题。

### 远程 Provider 能当生产方案吗

它是 Experimental，没有键管理与变更通知，错误只有不可用与校验两类。适合已有别的语言实现的缓存服务、或要验证进程外插件机制的场景；生产上多实例共享缓存优先用 `Caching.Redis`。

## 相关包与下一步

- 包参考：[Caching.Hybrid](/packages/caching-hybrid)、[Caching.Remote](/packages/caching-remote)、[Caching.Redis](/packages/caching-redis)、[Caching.InMemory](/packages/caching-inmemory)、[Caching.Abstractions](/packages/caching-abstractions)
- 进程外插件的发现、握手与操作代理见[进程外插件](/plugins/out-of-process)
- 下一步：[发布与订阅](/messaging/pubsub)，进入消息子系统
