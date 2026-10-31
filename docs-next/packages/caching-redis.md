# XiHan.Framework.Caching.Redis

`caching.distributed@1` 的 Redis Provider，基于 StackExchange.Redis：装配后共享缓存、`ICacheStore`、分布式锁、`IDistributedCache` 与 Idempotency 阶段中间件改由 Redis 承担，适用于多实例部署。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Caching.Redis` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `caching.distributed@1`（`role: provider`）；依赖 `kernel.hosting@1` |
| 特性入口 | `RedisCachingFeature`（清单 ID `xihan.caching.redis`） |
| 配置节 | `XiHan:Caching:Redis`、`XiHan:Caching:Idempotency`；可选绑定 `cache`（类型 `cache:distributed`） |
| 依赖 | `XiHan.Framework.Caching.Abstractions`；`StackExchange.Redis` 3.4.5 |
| AOT | 不兼容（`IsAotCompatible=false`，StackExchange.Redis 未声明 AOT 兼容） |
| 稳定性 | Preview |

## 概述

`RedisCacheStore` 实现 `ICacheStore`、`ICacheStoreInspector` 与 `ICacheChangeFeed`，特性用它注册：

- `ISharedCache`：`SerializingCache` + `RedisCacheStore`，值经 `ICacheSerializer` 序列化。
- `ICacheStore`、`IDistributedLock`（`StoreDistributedLock`）、`IDistributedCache`（`CacheStoreDistributedCache`），以及 Idempotency 阶段的 `IdempotencyMiddleware`。

进程内的 `ICache` 不由本包提供，仍是 `Caching.InMemory`（或 `Caching.Hybrid`）。被本包顶替后，内存实现不再注册共享缓存、锁与幂等中间件。

存储的编码与命令：

- 键加前缀 `KeyPrefix`（默认 `xihan:`）。
- 值前有 1 字节头：`0` 表示绝对过期；`1` 表示滑动过期，后跟 8 字节窗口毫秒数。滑动条目在读取时用 `EXPIRE` 延长。
- 不存在才写用 `SET NX PX`；比较删用 Lua 脚本跳过头部比较载荷后 `DEL`，在服务端原子执行。
- 批量读用一次 `MGET`（命中的滑动条目再用一个批次统一延长）；批量写与删把全部命令放进一个批次，一次往返。

连接在第一次解析 `IConnectionMultiplexer` 时建立，`StartAsync` 触发并核对，连不上时启动失败；停止时关闭连接。

## 何时使用

- 多实例部署需要共享缓存、跨实例互斥的锁与幂等去重。
- 配合 [Caching.Hybrid](./caching-hybrid)：Redis 实现了变更通知，两级缓存的 L1 在别的实例写入后立即失效。
- 单实例、开发与测试用 [Caching.InMemory](./caching-inmemory) 即可；后端不是 Redis、或要用 .NET 以外的语言实现存储时用 [Caching.Remote](./caching-remote)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Caching.Redis" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 内存实现继续提供 caching.local，Redis 顶替 caching.distributed
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<RedisCachingFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Caching:Redis:Configuration` | 字符串 | — | StackExchange.Redis 连接字符串；优先于绑定 `cache` 的值，两者都没有时装配失败 |
| `XiHan:Caching:Redis:KeyPrefix` | 字符串 | `xihan:` | 键前缀；变更频道为 `<KeyPrefix>__changes` |
| `XiHan:Caching:Redis:DefaultTimeToLiveSeconds` | 正数 | 300 | 条目没有指定过期时的存活时间 |
| `XiHan:Caching:Redis:PublishChanges` | 布尔 | `true` | 写入与删除后是否向变更频道发布；关掉后本实例的同步通知不受影响 |
| `XiHan:Caching:Redis:HealthProbeIntervalSeconds` | 正数 | 15 | 健康项的重探间隔，也是单次探测的超时 |

```json
{
  "XiHan": {
    "Caching": {
      "Redis": {
        "Configuration": "localhost:6379",
        "KeyPrefix": "orders:",
        "DefaultTimeToLiveSeconds": 600
      }
    }
  }
}
```

值的序列化器按「已登记的 `ICacheSerializer` → 已登记的 `JsonSerializerOptions`（须带 `TypeInfoResolver`）→ 反射版 `JsonCacheSerializer`」的顺序选，在 `AddXiHan` 装配时决定，所以要在 `AddXiHan` 之前登记，见[序列化器](../guide/caching#序列化器)。幂等选项见 [Caching.Abstractions](./caching-abstractions#配置)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `RedisCachingFeature` | 特性入口（`IHostedFeature`）；常量 `ConnectionBinding`（`cache`）、`HealthName`（`caching.redis.connection`） |
| `RedisCachingOptions` | `Configuration`、`KeyPrefix`、`DefaultTimeToLive`、`PublishChanges`、`HealthProbeInterval`；`FromConfiguration(configuration, binding)` |
| `RedisCacheStore` | 字节存储，实现 `ICacheStore`、`ICacheStoreInspector`、`ICacheChangeFeed`、`IDisposable` |

## 使用示例

订阅存储的变更，在别的实例写入或删除时清掉自己维护的本地副本（两级缓存就是这样做的）：

```csharp
using System.Collections.Concurrent;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;

public sealed class LocalMirror : IDisposable
{
    private readonly ConcurrentDictionary<string, string> _copies = new(StringComparer.Ordinal);
    private readonly IDisposable? _subscription;

    public LocalMirror(ISharedCache cache)
    {
        // 只有实现了变更通知的存储（Redis）才订阅得到
        _subscription = cache.Store is ICacheChangeFeed feed ? feed.Subscribe(OnChanged) : null;
    }

    public bool InvalidatesAcrossInstances => _subscription is not null;

    // 处理器在写入的调用里同步执行，只做内存操作
    private void OnChanged(CacheChange change)
    {
        if (change.Prefix is { } prefix)
        {
            foreach (var key in _copies.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
            {
                _copies.TryRemove(key, out _);
            }

            return;
        }

        foreach (var key in change.Keys)
        {
            _copies.TryRemove(key.Value, out _);
        }
    }

    public void Dispose() => _subscription?.Dispose();
}
```

## 行为与限制

- 连接或超时失败抛出携带 `CACHING_UNAVAILABLE_001` 的 `FrameworkException`，不当成未命中。
- 键管理 `ICacheStoreInspector` 用单个端点的 `SCAN`：续页标记是游标，`CacheKeyQuery.Limit` 只是提示值，同一个键可能出现在两页；前缀里的 glob 字符被转义，只按前缀匹配。读元数据把 `PTTL`、`STRLEN`、`GETRANGE` 放进一个批次，不延长滑动过期；按前缀删除逐页 `SCAN` 后 `DEL`。
- 变更通知：每次写入与删除（真的删到了才算，按前缀删除总是）后同步通知本实例的订阅者，再发后不等地向 `<KeyPrefix>__changes` 发布；消息带来源标识，不回送给发出者；第一次有人订阅时才订阅频道，释放存储即退订。发布失败不让写入失败，断线期间的通知会丢失。
- 多个应用共用一个 Redis 时用不同的 `KeyPrefix` 隔开键与变更频道。
- 测试：一致性套件需要真实 Redis，设置环境变量 `XIHAN_TEST_REDIS`（如 `localhost:6379`）后运行，未设置时跳过。

| 健康项 | 状态 |
| --- | --- |
| `caching.redis.connection` | 启动时同步 PING 一次，之后按 `HealthProbeIntervalSeconds` 在后台重探并缓存结果；PING 失败或超时时 Degraded（`KERNEL_UNAVAILABLE_005`，`reason` 是异常消息），应用仍在服务 |

## 相关

- [缓存与分布式锁](../guide/caching)
- [Caching.Abstractions](./caching-abstractions)、[Caching.InMemory](./caching-inmemory)、[Caching.Hybrid](./caching-hybrid)
- [部署与打包](../guide/deployment)：绑定 `cache` 的来源
- 规范：RFC-0006《缓存、分布式锁与幂等》；ADR-0055
