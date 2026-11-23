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

接在[第一个应用](/getting-started/first-app)的 `Program.cs` 上，在[缓存读写](/caching/basics)装配的 Redis 之后再多一行：Hybrid 顶替内存实现的 `caching.local`，L2 取 Redis 提供的 `caching.distributed`。

```csharp
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.Redis;

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RedisCachingFeature>()
    .UseFeature<HybridCachingFeature>());
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
- 回填：读 L2 之前记下这个键的失效轮次，回填前核对；读的途中收到过这个键的失效（别的实例刚写了它），读回的值可能早于那次写入，不回填 L1，下一次读取再去 L2。`GetAsync`、`GetManyAsync`、`GetOrCreateAsync` 都按这条规则（ADR-0229）。
- 写与删：先 L2 再 L1，L2 失败时 L1 不留下别的实例看不到的值；写入后照旧写进 L1；`GetOrCreateAsync` 两级都未命中才执行工厂。
- L1 的存活时间是条目自己的过期时长与 `LocalTimeToLive` 的较小者，滑动过期在 L1 里按绝对时间处理；`LocalCapacity` 满时淘汰最久未访问的条目。
- L1 存对象引用，取回的对象不得修改，与内存实现的约定一致。

## 跨实例失效

跨实例失效依赖 L2 存储的变更通知 `ICacheChangeFeed`。Redis 存储实现了它：每次写入与删除后同步通知本实例的订阅者，并以发后不等的方式向频道 `<KeyPrefix>__changes` 发布，其他实例收到后删掉 L1 里的这些键或前缀（`CacheChange.Keys` / `CacheChange.Prefix`）。`HybridObjectCache.InvalidatesAcrossInstances` 表示是否订阅到了。通知是尽力而为的，断线期间可能丢失；`XiHan:Caching:Redis:PublishChanges` 设为 `false` 可以关掉发布。

L2 的存储没有变更通知时（远程 Provider、内存实现），绕过本实例 `HybridObjectCache` 的写入（别的实例的写入，本实例直接经 `ISharedCache` 或管理界面的写入）不会失效 L1，读到旧值的时间最长为 `LocalTimeToLive`。需要强一致的键直接用 `ISharedCache`，不经 L1。

通知和读取会赛跑：L1 未命中时去读 L2，读的途中另一个实例写了这个键，变更通知先到（L1 里本来没有，删了个空），读回的旧值后到。读写规则里「读的途中失效过就不回填」挡的就是这种情况；失效轮次按键的散列分条带记录，细节见 [Caching.Hybrid](/packages/caching-hybrid)。

框架自己的授予缓存、租户目录缓存与翻译版本戳都走 `ICache`（[按范围失效的缓存区](/caching/basics#按范围失效的缓存区)），多实例部署装上 Redis 与两级缓存后，它们的命中都在 L1、判定与租户解析不出进程，一个实例写入后其他实例经变更通知立即失效。只装 Redis 时 `ICache` 仍是每个实例各自的内存实现，授予撤销、租户停用与新翻译在其他实例上要等存活时间（授予与租户目录缺省 30 秒，翻译等周期刷新，缺省 5 分钟）。

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

- `ListKeysAsync`：返回 `Result<CacheKeyPage>`。`Prefix` 与键同文法，空为全部（不合法不在构造时抛出，返回 `CACHING_VALIDATION_004`，参数 `prefix`、`max`）；`PageSize` 为 1 到 1000（`CacheKeyQuery.MaxPageSize`，缺省 `DefaultPageSize` 100），越界不在构造时抛出，存储先经 `CacheKeyQuery.Validate(CacheKeyQuery.Revision)` 核对、返回 `CACHING_VALIDATION_003`（HTTP 400，参数 `pageSize`、`max`），不访问后端；本页的键在 `Items`，没有更多时 `ContinuationToken` 为 `null`。已过期的键不列出。
- `InspectAsync`：不存在或已过期为无值；读元数据不延长滑动过期，对象条目也不读出值。
- `RemoveByPrefixAsync`：返回 `Result<long>`（删除数）；前缀为空或不合法返回 `CACHING_VALIDATION_004`（HTTP 400）、不访问后端，没有「清空全部」。要整体失效就给应用的键统一加一个前缀（如 `catalog:`）、按它删除；Redis 上还可以换一个 `KeyPrefix` 让旧条目自然过期。

内存实现按键的序数顺序分页，续页标记是上一页最后一个键。Redis 用单个端点的 `SCAN`：续页标记是游标，`PageSize` 只是提示值，同一个键可能出现在两页（管理界面自己去重）；前缀里的 glob 字符会被转义，按前缀匹配而不是按模式。读值、改值、删单个键仍走 `ICache` / `ICacheStore`。经 Redis 存储删除的键同样触发变更通知，各实例两级缓存的 L1 随之失效。

## 远程 Provider

`Caching.Remote` 让任意语言写的进程外插件承担 `caching.distributed`（规范 RFC-0019《远程 Provider》）。插件实现固定的操作集 `cache-entries.get / set / try-add / remove / try-remove / try-refresh / get-many / set-many / remove-many`（常量在 `RemoteCacheOperations`），由 `Plugins.OutOfProcess` 按目录发现并登记代理操作；`Caching.Remote` 按操作 ID 直接执行这些代理操作（不经过管道，否则幂等中间件会递归），把它接成 `ISharedCache`、`IDistributedLock`、`IDistributedCache` 与幂等中间件。装配时在 `AddXiHan` 里加插件宿主 `OutOfProcessPluginsFeature` 与本 Provider 的 `RemoteCachingFeature` 两个特性（写法见 [Caching.Remote](/packages/caching-remote)），再给出插件目录与超时：

```json
{
  "XiHan": {
    "Plugins": { "OutOfProcess": { "Directories": ["plugins/cache-go"] } },
    "Caching": { "Remote": { "TimeoutMilliseconds": 5000 } }
  }
}
```

每次操作带 `TimeoutMilliseconds`（默认 5000）的截止时间。插件返回的缓存域错误（`CACHING_VALIDATION_001` 键不合法、`CACHING_VALIDATION_002` 过期选项不合法）原样成为 `FrameworkException`；插件未加载、操作未登记、进程不可达、超时都映射为 `CACHING_UNAVAILABLE_001`。Go 实现见 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 的 `caching` 包，示例插件在 `framework/samples/CacheGoPlugin`，它读 `XiHan:Cache:DefaultTimeToLiveSeconds` 作默认存活时间（缺省 300）。远程 Provider 是 Experimental，没有键管理与变更通知，适合已有别的语言实现的缓存服务、或要验证进程外插件机制的场景；生产上多实例共享缓存优先用 `Caching.Redis`。插件机制见[进程外插件](/plugins/out-of-process)。

## 配置

本页用到四个键：`XiHan:Caching:Hybrid:LocalCapacity` 与 `LocalTimeToLiveSeconds`（[Caching.Hybrid](/packages/caching-hybrid#配置)）、`XiHan:Caching:Redis:PublishChanges`（[Caching.Redis](/packages/caching-redis#配置)）、`XiHan:Caching:Remote:TimeoutMilliseconds`（[Caching.Remote](/packages/caching-remote#配置)）。类型、缺省值与非法值的处理见各包页。

## 相关包与下一步

- 包参考：[Caching.Hybrid](/packages/caching-hybrid)、[Caching.Remote](/packages/caching-remote)、[Caching.Redis](/packages/caching-redis)、[Caching.InMemory](/packages/caching-inmemory)、[Caching.Abstractions](/packages/caching-abstractions)
- 进程外插件的发现、握手与操作代理见[进程外插件](/plugins/out-of-process)
- 下一步：[发布与订阅](/messaging/pubsub)，进入消息子系统
