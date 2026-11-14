# XiHan.Framework.Caching.Hybrid

两级缓存 Provider：提供 `caching.local@1`，把 `ICache` 换成「L1 进程内对象缓存 + L2 被选中的共享缓存」，读路径少一次网络往返，适用于多实例部署下读多写少的数据。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Caching.Hybrid` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `caching.local@1`（`role: provider`）；依赖 `kernel.hosting@1`、`caching.distributed@1` |
| 特性入口 | `HybridCachingFeature`（清单 ID `xihan.caching.hybrid`） |
| 配置节 | `XiHan:Caching:Hybrid` |
| 依赖 | `XiHan.Framework.Caching.Abstractions`；无第三方依赖 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

`HybridObjectCache` 实现 `ICache`：L1 是有界 LRU 的对象引用表，L2 是容器里的 `ISharedCache`（Redis、Remote，或单实例下内存实现提供的共享缓存）。`ISharedCache` 本身不变，仍直接指向 L2。

| 操作 | 行为 |
| --- | --- |
| `GetAsync<T>` | L1 命中直接返回；未命中读 L2，命中后回填 L1（读的途中这个键失效过就不回填） |
| `GetManyAsync<T>` | 先在 L1 取全部，只把未命中的键一次交给 L2；读的途中失效过的键不回填 |
| `SetAsync<T>` / `SetManyAsync<T>` | 先写 L2 再写 L1；L2 失败时 L1 不留下别的实例看不到的值 |
| `RemoveAsync` / `RemoveManyAsync` | 先删 L2 再删 L1；两级任一存在过即报告删到 |
| `GetOrCreateAsync<T>` | L1 未命中时交给 L2 的 `GetOrCreateAsync`（两级都未命中才执行工厂），结果回填 L1；途中有失效（含工厂写入 L2 引起的本实例通知）就不回填，下一次读取再去 L2 |

L1 里每个条目的存活时间是它自己的过期时长与 `LocalTimeToLive` 的较小者，滑动过期在 L1 里按绝对时间处理；从 L2 回填的条目存活 `LocalTimeToLive`。L1 存对象引用，调用方不得修改取回的对象；`ICache.Store` 是 L2 的字节存储。

跨实例失效（ADR-0055）：L2 的存储实现了 `ICacheChangeFeed`（Redis）时 `HybridObjectCache` 订阅它，本实例与其他实例的写入、删除、按前缀删除都让 L1 里对应的键立即失效；`InvalidatesAcrossInstances` 表示是否订阅到了，`HybridObjectCache` 释放即退订。

回填与失效的竞争（ADR-0229）：L1 按键的散列分 64 个条带记失效轮次，删除一个键让它所在条带的轮次加一，按前缀删除让全部条带加一。读 L2 之前记下轮次，回填前核对，读的途中有过失效就不回填：否则别的实例写入的通知先到、读回的旧值后到，旧值会在 L1 里留到 `LocalTimeToLive`。条带数固定，内存有界；同条带的无关失效也会挡掉一次回填，只多读一次 L2。

框架的授予缓存（`Security.Database`）、租户目录缓存（`MultiTenancy.Database`）与翻译版本戳（`Localization.Database`）都走 `ICache`，多实例部署装本包与 `Caching.Redis` 后，它们的命中在 L1、写入后经变更通知在所有实例上立即失效。

## 何时使用

- 多实例部署，热点数据读多写少，可以接受最长 `LocalTimeToLive` 的旧值窗口（L2 是 Redis 时通常只在通知丢失时出现）。
- 多实例部署装了 `Security.Database`、`MultiTenancy.Database` 或 `Localization.Database`：只装 Redis 时 `ICache` 仍是进程内的，授予撤销、租户停用与新翻译要等存活时间才传到其他实例。
- 需要强一致的键直接用 `ISharedCache`，不经 L1。
- 不需要 L1：不装配本包，`ICache` 由 [Caching.InMemory](./caching-inmemory) 提供。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Caching.Hybrid --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。本包需要一个 `caching.distributed` 的提供者作 L2：

```csharp
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// Hybrid 顶替内存实现的 caching.local；L2 是 Redis
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RedisCachingFeature>()
    .UseFeature<HybridCachingFeature>());
```

单实例开发环境不装配 Redis 时，L2 是 `Caching.InMemory` 的共享缓存，测试零外部基础设施。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Caching:Hybrid:LocalCapacity` | 正整数 | 10000 | L1 条目上限，满时淘汰最久未访问的条目 |
| `XiHan:Caching:Hybrid:LocalTimeToLiveSeconds` | 正数 | 30 | L1 存活时间上限，也是别的实例写入后最长的不可见窗口 |

```json
{ "XiHan": { "Caching": { "Hybrid": { "LocalCapacity": 10000, "LocalTimeToLiveSeconds": 30 } } } }
```

非法值在装配时抛 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `HybridCachingFeature` | 特性入口；被选中提供 `caching.local` 时把 `ICache` 登记为 `HybridObjectCache` |
| `HybridCachingOptions` | `LocalCapacity`、`LocalTimeToLive`；`FromConfiguration` 从配置节读取 |
| `HybridObjectCache` | 两级缓存，实现 `ICache` 与 `IDisposable`；`InvalidatesAcrossInstances`、`LocalCount` |

## 使用示例

启动后检查两级缓存是否订阅到了跨实例失效，没有时记一条警告，提醒 L1 的旧值窗口是 `LocalTimeToLive`：

```csharp
using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Hybrid;

public sealed class HybridCacheCheck(ICache cache, ILogger<HybridCacheCheck> logger)
{
    public void Report()
    {
        if (cache is HybridObjectCache hybrid && !hybrid.InvalidatesAcrossInstances)
        {
            logger.LogWarning("L2 has no change feed; L1 entries may be stale for up to the local time to live.");
        }
    }
}
```

读写本身与任何 `ICache` 相同，见[缓存与分布式锁](/caching/basics)。

## 行为与限制

- L2 没有变更通知时（远程 Provider、内存实现），绕过本实例 `HybridObjectCache` 的写入不会失效 L1，读到旧值的时间最长为 `LocalTimeToLive`；通知丢失时同样以此为上限。
- L2 是内存实现时，经 `ICacheInspector` 删除的对象条目在 L1 里最长 `LocalTimeToLive` 后才消失（ADR-0058）。
- L1 命中不触达 L2，因此不会延长 L2 里的滑动过期；滑动过期的条目经两级缓存读取时实际按绝对时间到期。
- 同一个键被写成别的类型时，L1 按 `T` 读取当作未命中，交给 L2 判定。
- L1 不实现键管理接口；管理界面列键用 L2 的 `Store`（`ICacheStoreInspector`）。

## 相关

- [缓存与分布式锁](/caching/hybrid)
- [Caching.Abstractions](./caching-abstractions)、[Caching.Redis](./caching-redis)、[Caching.InMemory](./caching-inmemory)
- 规范：RFC-0006《缓存、分布式锁与幂等》「两级缓存」；ADR-0019、ADR-0055、ADR-0058、ADR-0229
