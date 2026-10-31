# XiHan.Framework.Caching.Remote

`caching.distributed@1` 的远程 Provider 适配器：存储由任意语言写的进程外插件承担，插件实现固定的操作集 `cache-entries.*`，本包把它接成共享缓存、分布式锁、`IDistributedCache` 与幂等去重。适用于缓存后端要用 .NET 以外的语言实现、或要复用别的语言已有的存储客户端的场景。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Caching.Remote` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `caching.distributed@1`（`role: provider`）；依赖 `kernel.hosting@1` |
| 特性入口 | `RemoteCachingFeature`（清单 ID `xihan.caching.remote`） |
| 配置节 | `XiHan:Caching:Remote`、`XiHan:Caching:Idempotency` |
| 依赖 | `XiHan.Framework.Caching.Abstractions`；无第三方依赖；运行时需要插件宿主（如 `Plugins.OutOfProcess`）登记的代理操作 |
| AOT | 不兼容（`IsAotCompatible=false`：没有登记源生成序列化器时退回反射版 `ICacheSerializer`） |
| 稳定性 | Experimental |

## 概述

远程 Provider 的模式见 RFC-0019《远程 Provider》与 ADR-0011：能力的选择、依赖与注册都留在宿主，插件清单不写 `provides`，只在 `contributes.operations` 列出操作集；本包在清单里声明 `caching.distributed@1`，像 `Caching.Redis` 一样参与能力求解。

`RemoteCacheStore` 实现 `ICacheStore`，每个方法对应操作集里的一个操作（`provider/caching-distributed.md`）：

| `ICacheStore` 方法 | 操作 ID | 请求 → 响应 |
| --- | --- | --- |
| `GetAsync` | `cache-entries.get` | `{ key }` → `{ value }`（base64 或 `null`） |
| `SetAsync` | `cache-entries.set` | `{ key, value, ttlMs?, slidingMs? }` → `{}` |
| `TryAddAsync` | `cache-entries.try-add` | 同 `set` → `{ added }` |
| `RemoveAsync` | `cache-entries.remove` | `{ key }` → `{ removed }` |
| `TryRemoveAsync` | `cache-entries.try-remove` | `{ key, expected }` → `{ removed }` |
| `GetManyAsync` | `cache-entries.get-many` | `{ keys }` → `{ values }`，与 `keys` 等长 |
| `SetManyAsync` | `cache-entries.set-many` | `{ entries }` → `{}` |
| `RemoveManyAsync` | `cache-entries.remove-many` | `{ keys }` → `{ removed }`，与 `keys` 等长 |

值与期望值是标准 base64（带填充）；`ttlMs` 与 `slidingMs` 二选一，都不给时用插件的默认存活时间。

每个操作按 ID 从宿主的操作登记表取代理操作，经 `IOperationInvoker.ExecuteAsync` 直接执行，不经过管道：存储会被幂等中间件在管道里使用，走管道会递归，这些内部调用也不需要授权与幂等。键与过期选项在 .NET 侧先按 `CacheKey` / `CacheEntryOptions` 校验，再编码成操作集的 JSON。

在存储之上，特性注册 `ISharedCache`（`SerializingCache` + `RemoteCacheStore`）、`IDistributedLock`、`IDistributedCache` 与 Idempotency 阶段的 `IdempotencyMiddleware`。本包不登记 `ICacheStore` 接口，需要字节存储时从 `ISharedCache.Store` 取。

## 何时使用

- 缓存后端由别的语言实现，或团队已有 Go 等语言的存储客户端，想替换 [Caching.Redis](./caching-redis) 而不写 .NET 代码。
- 后端就是 Redis 时直接用 Caching.Redis：它多了键管理与变更通知，少一次进程间往返。
- 本包是 Experimental，契约可能在 5.0 正式版前调整。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Caching.Remote" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。本包只提供能力，不启动插件进程，插件由 [Plugins.OutOfProcess](./plugins-outofprocess) 按目录发现：

```csharp
using XiHan.Framework.Caching.Remote;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Plugins.OutOfProcess;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 插件宿主发现并登记 cache-entries.* 代理操作；Remote 顶替内存实现的 caching.distributed
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OutOfProcessPluginsFeature>()
    .UseFeature<RemoteCachingFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Caching:Remote:TimeoutMilliseconds` | 正整数 | 5000 | 每次操作的截止时间，超时按存储不可用处理；不是正整数时按缺省值 |

```json
{
  "XiHan": {
    "Plugins": { "OutOfProcess": { "Directories": ["plugins/cache-go"] } },
    "Caching": { "Remote": { "TimeoutMilliseconds": 5000 } }
  }
}
```

插件自己的配置由插件读取，例如 `next/samples/CacheGoPlugin` 读 `XiHan:Cache:DefaultTimeToLiveSeconds`（默认 300）与 `XiHan:Cache:MaxEntries`（默认 100000）。幂等选项见 [Caching.Abstractions](./caching-abstractions#配置)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `RemoteCachingFeature` | 特性入口 |
| `RemoteCachingOptions` | `Timeout`；`FromConfiguration` 读 `TimeoutMilliseconds` |
| `RemoteCacheStore` | 经操作集实现的 `ICacheStore` |
| `RemoteCacheOperations` | 八个操作 ID 常量与 `All`，以及请求、响应记录（`KeyRequest`、`GetResponse`、`SetRequest`、`AddedResponse`、`RemovedResponse`、`TryRemoveRequest`、`KeysRequest`、`GetManyResponse`、`SetManyRequest`、`RemovedManyResponse`、`EmptyResponse`） |
| `RemoteCachingMessages` | 插件侧错误的消息键 `caching.remote.*` 与消息目录（10 种区域的译文随本包嵌入） |

## 使用示例

插件可以用 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 的 `caching` 包实现（`Store` 接口、`MemoryStore` 与 `RegisterOperations`，示例在 SDK 的 `examples/cache`）。仓库里的 `next/samples/CacheGoPlugin` 是同一个示例的可执行入口，在该目录构建：

```bash
./build.ps1
```

产物在 `bin/`：可执行文件 `cache-go` 与 `xihan.manifest.json`，需要 Go 1.26 以上。把 `bin/` 的内容放到 `XiHan:Plugins:OutOfProcess:Directories` 指向的目录，宿主启动时加载插件，之后 `ISharedCache`、`IDistributedLock` 的调用都转到插件进程。插件清单的关键部分：

```json
{
  "kind": "plugin",
  "isolation": "out-of-process",
  "requires": [ { "id": "kernel.plugins", "version": "1" } ],
  "contributes": {
    "operations": [ "cache-entries.get", "cache-entries.set", "cache-entries.try-add", "cache-entries.remove", "cache-entries.try-remove", "cache-entries.get-many", "cache-entries.set-many", "cache-entries.remove-many" ]
  }
}
```

操作在握手里声明为 `stability: internal`、`expose` 为空，协议适配器不会把它们发布出去。协议层可以用规范仓库的运行器单独验证插件：

```bash
xihan-conformance plugin --dir bin --sample 'cache-entries.set={"key":"k","value":"aGk="}'
```

## 行为与限制

- 插件返回的缓存域错误码（`CACHING_*`）原样成为 `FrameworkException`；其他错误（插件未加载、操作未登记、进程不可达、超时）映射为 `CACHING_UNAVAILABLE_001`，消息里带原错误码，不伪装成未命中。
- 批量响应数组与请求长度不符、值不是合法 base64、载荷不是合法 JSON，都视为插件违反协议，抛 `CACHING_UNAVAILABLE_001`。
- 插件发现失败不阻止应用启动（插件以 `Failed` 留在宿主里），缓存在第一次使用时报 `CACHING_UNAVAILABLE_001`。
- 不实现键管理（`ICacheStoreInspector`）与变更通知（`ICacheChangeFeed`）：配合 [Caching.Hybrid](./caching-hybrid) 时 L1 的旧值窗口是 `LocalTimeToLive`。
- 原子性由插件在自己的串行化范围内保证；多个宿主共用一个后端时的一致性由插件的后端保证。
- 适配器 + 插件的组合必须通过 `CacheStoreConformance` 全部用例；`Caching.Remote.Tests` 用本机 `go` 构建 `CacheGoPlugin` 跑完整套件，没有 `go` 时跳过。

| 插件错误码 | 消息键 | 情况 |
| --- | --- | --- |
| `CACHING_VALIDATION_001` | `caching.remote.key_invalid` | 键不合法 |
| `CACHING_VALIDATION_002` | `caching.remote.options_invalid` | `ttlMs` / `slidingMs` 不是正整数或同时给出 |
| `CACHING_UNAVAILABLE_001` | `caching.remote.store_unavailable` | 后端不可用或失败 |
| `CACHING_UNAVAILABLE_001` | `caching.remote.capacity_reached` | 插件的存储已满（`CacheGoPlugin` 满了先清过期条目，仍满才返回） |

## 相关

- [缓存与分布式锁](../guide/caching#远程-provider)
- [插件](../guide/plugins)、[Plugins.OutOfProcess](./plugins-outofprocess)
- [Caching.Abstractions](./caching-abstractions)、[Caching.Redis](./caching-redis)
- 规范：RFC-0019《远程 Provider》、RFC-0006《缓存、分布式锁与幂等》；ADR-0011、ADR-0012
