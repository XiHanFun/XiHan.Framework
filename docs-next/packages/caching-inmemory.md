# XiHan.Framework.Caching.InMemory

缓存的默认实现：有界 LRU 的字节存储与对象缓存，同时提供 `caching.local@1` 与 `caching.distributed@1`。没有显式 Provider 时进程内缓存、共享缓存、分布式锁与幂等去重都由它承担，适用于单实例部署、开发与测试。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Caching.InMemory` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `caching.local@1`、`caching.distributed@1`（`role: default`）；依赖 `kernel.hosting@1` |
| 特性入口 | `InMemoryCachingFeature`（清单 ID `xihan.caching.inmemory`） |
| 配置节 | `XiHan:Caching:InMemory`；被选中提供 `caching.distributed` 时另读 `XiHan:Caching:Idempotency` |
| 依赖 | `XiHan.Framework.Caching.Abstractions`；无第三方依赖 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

包里有两张互相独立的有界 LRU 表：

- `InMemoryCacheStore`：字节存储，实现 `ICacheStore` 与 `ICacheStoreInspector`。写入复制字节，读取返回不可修改的视图。
- `InMemoryObjectCache`：类型化缓存，实现 `ISharedCache`（因而也是 `ICache`）与 `ICacheInspector`。直接保存对象引用、不序列化；它的 `Store` 指向同一进程的 `InMemoryCacheStore`，供锁与幂等使用。

每张表的全部操作在一把锁内完成，批量方法一次加锁逐项处理；`TryAddAsync` 与比较删的检查和写入在同一把锁里，因而是原子的。过期条目在访问时清理，表满时淘汰最久未访问的条目。

特性只为被能力求解选中的能力注册契约接口：

| 被选中的能力 | 注册 |
| --- | --- |
| `caching.local` | `ICache` → `InMemoryObjectCache` |
| `caching.distributed` | `ISharedCache` → 同一个 `InMemoryObjectCache`；`ICacheStore` → `InMemoryCacheStore`；`IDistributedLock`（`StoreDistributedLock`）；`IDistributedCache`；Idempotency 阶段的 `IdempotencyMiddleware` |

两个都选中时 `ICache` 与 `ISharedCache` 是同一个实例，经一个写入的值从另一个读得到。具体类型 `InMemoryCacheStore`、`InMemoryObjectCache` 与健康项总是注册。

## 何时使用

- 单实例部署、开发环境、测试：零外部基础设施，`Profiles.Standard` 已经包含它。
- 多实例部署：换 [Caching.Redis](./caching-redis) 或 [Caching.Remote](./caching-remote) 提供 `caching.distributed`，本包继续提供 `caching.local`；需要读路径少一次往返时再加 [Caching.Hybrid](./caching-hybrid) 顶替 `caching.local`。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Caching.InMemory" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Standard` 已经装配本包；不用组合包时单独装配：

```csharp
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Hosting.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryCachingFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Caching:InMemory:MaxEntries` | 正整数 | 10000 | 每张表（字节存储、对象缓存各一张）的条目上限 |
| `XiHan:Caching:InMemory:DefaultTimeToLiveSeconds` | 正数 | 300 | 条目没有指定过期时的存活时间 |

```json
{ "XiHan": { "Caching": { "InMemory": { "MaxEntries": 50000, "DefaultTimeToLiveSeconds": 120 } } } }
```

幂等选项见 [Caching.Abstractions](./caching-abstractions#配置)。非法值在装配时抛 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `InMemoryCachingFeature` | 特性入口；常量 `HealthName` 为 `caching.inmemory.capacity` |
| `InMemoryCachingOptions` | `MaxEntries`、`DefaultTimeToLive`；`FromConfiguration` 从配置节读取 |
| `InMemoryCacheStore` | 有界 LRU 字节存储；`Count`（含尚未清理的过期条目）、`Clear()` |
| `InMemoryObjectCache` | 有界 LRU 对象缓存；`Count` |
| `InMemoryCacheHealthContributor` | 健康项：任一张表满即 Degraded |

## 使用示例

管理界面列出对象条目并查看类型与剩余存活（对象条目不在字节存储里，`ICacheStoreInspector` 看不到它们）：

```csharp
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;

public sealed class ObjectEntries(ICache cache)
{
    public async ValueTask<List<CachedObjectInfo>> ListAsync(string prefix, CancellationToken cancellationToken)
    {
        var result = new List<CachedObjectInfo>();
        if (cache is not ICacheInspector inspector)
        {
            return result;
        }

        var page = await inspector.ListKeysAsync(new CacheKeyQuery { Prefix = prefix }, cancellationToken);
        foreach (var key in page.Keys)
        {
            // 查看不读出值、不延长滑动过期；两次调用之间条目可能已过期
            var info = await inspector.InspectAsync(key, cancellationToken);
            if (info.TryGetValue(out var entry))
            {
                result.Add(entry);
            }
        }

        return result;
    }
}
```

## 行为与限制

- 共享缓存、锁与幂等只在本进程内有效：多实例部署下锁不互斥、幂等键互不可见。
- 对象缓存返回写入时的同一个对象，调用方不得修改取回的对象。同一个键被写成别的类型时，按 `T` 读取当作未命中。
- `MaxEntries` 对两张表分别计数；满时写入不被拒绝，而是淘汰最久未访问的条目。
- 过期条目只在访问时清理，`Count` 包含尚未清理的过期条目；列键与查看会跳过它们。
- 键管理按键的序数顺序分页，续页标记是上一页最后一个键；`RemoveByPrefixAsync` 返回删除的条目中仍存活的条数。
- 字节存储与对象缓存都没有变更通知（不实现 `ICacheChangeFeed`），作为两级缓存的 L2 时，直接写 L2 不会失效 L1。

| 健康项 | 状态 |
| --- | --- |
| `caching.inmemory.capacity` | 两张表任一条目数达到 `MaxEntries` 时 Degraded（`CACHING_UNAVAILABLE_002`，数据 `storeEntries`、`cacheEntries`、`capacity`），否则 Ready；同步计算，不起后台循环 |

## 相关

- [缓存与分布式锁](../guide/caching)
- [Caching.Abstractions](./caching-abstractions)、[Caching.Redis](./caching-redis)、[Caching.Hybrid](./caching-hybrid)
- [Profiles.Standard](./profiles-standard)
- 规范：RFC-0006《缓存、分布式锁与幂等》；ADR-0055、ADR-0058
