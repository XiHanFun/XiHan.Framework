# XiHan.Framework.Caching

> 框架统一缓存层：基于 .NET `HybridCache` 的两级混合缓存 + 泛型分布式缓存 + AOP 声明式缓存 + 租户感知键，Redis 启用后额外提供分布式锁、延迟队列与 Stream 可靠队列。

- **NuGet**：`XiHan.Framework.Caching`
- **模块类**：`XiHanCachingModule`
- **所在层**：基础设施层
- **关键依赖**：**Microsoft.Extensions.Caching.Hybrid**（两级缓存）+ **Microsoft.Extensions.Caching.StackExchangeRedis** / **StackExchange.Redis**（Redis）

## 概述

本包在 .NET 内置的 `HybridCache` 与分布式缓存之上，封装出框架统一的缓存能力：

- 用泛型接口 `IHybridCache<TCacheItem>` / `IDistributedCache<TCacheItem>` 让你以类型安全的方式缓存业务对象，屏蔽序列化与键拼接细节。
- 键经 `IDistributedCacheKeyNormalizer` 规范化，默认按当前租户加前缀，天然实现多租户隔离。
- 未配置 Redis 时全部回落到进程内内存实现（内存缓存 + 内存分布式缓存 + 进程内锁），零外部依赖即可跑；配置 Redis 后自动 `Replace` 为 Redis 实现，并解锁分布式锁、延迟队列、Stream 队列等能力。
- 通过 AOP 拦截器支持 `[Cacheable]` 声明式缓存方法结果，并用 `[CacheEvict]` 在方法成功后清除缓存。

设计思路是"接口稳定、实现可切换"：业务只依赖泛型接口，本地/Redis 的差异由 DI 装配期决定。

## 何时使用

- 需要"内存 + Redis"两级缓存，降低热点数据的 Redis 往返。
- 想在多租户环境下缓存，且要求缓存自动按租户隔离、避免跨租户串数据。
- 想用特性声明式地缓存方法结果（`[Cacheable]`）。
- 需要基于 Redis 的分布式锁（`IDistributedLock`）、延迟队列（`IRedisDelayQueue<T>`）或 Stream 可靠消息队列（`IRedisStreamQueue<T>`）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Caching
```

```csharp
[DependsOn(typeof(XiHanCachingModule))]
public class MyModule : XiHanModule;
```

模块在 `ConfigureServices` 里调用 `AddXiHanCaching(config)`，一次性注册：

- `AddMemoryCache()` + `AddDistributedMemoryCache()`（内存两级基座）。
- `IDistributedCacheKeyNormalizer` → `DefaultDistributedCacheKeyNormalizer`（租户感知键）。
- `IDistributedCacheSerializer` → `JsonDistributedCacheSerializer`（JSON 序列化）。
- 开放泛型 `IDistributedCache<>` / `IDistributedCache<,>` → `DistributedCache<>` / `DistributedCache<,>`。
- `AddHybridCache()` + 开放泛型 `IHybridCache<>` / `IHybridCache<,>` → `XiHanHybridCache<>` / `XiHanHybridCache<,>`。
- `IDistributedLock` → `InMemoryDistributedLock`（`TryAdd`，进程内回退）。
- `XiHanDistributedCacheOptions`（全局条目默认 `SlidingExpiration = 20min`）与 `XiHanRedisCacheOptions`（绑定配置节）。

随后 `OnRegistered(CacheInterceptorRegistrar.RegisterIfNeeded)` 挂上缓存拦截器（为带 `[Cacheable]` 的类型织入 `CacheInterceptor`）。

动态代理只包裹接口注册，HTTP 请求进来的控制器实例不经过它。[XiHan.Framework.Web.Api](./web-api) 的 `XiHanCacheFilter` 补上这条路径：MVC 动作外层按同一套 `CacheAspect` 语义处理 `[Cacheable]` / `[CacheEvict]`，动态 API 的动作先经 `OriginalMethodAttribute` 回查原始应用服务方法再读取特性。它排在工作单元过滤器之外，命中缓存不开启事务、清除缓存发生在事务提交之后。

若 `XiHan:Caching:Redis:IsEnabled = true`，`AddXiHanCaching` 继续：调用 `AddStackExchangeRedisCache`，把 `IDistributedCache` `Replace` 为 `XiHanRedisCache`，并在有连接串时 `TryAdd` 出 `IConnectionMultiplexer`、`IRedisStreamQueue<>`、`IRedisDelayQueue<>`，同时把 `IDistributedLock` `Replace` 为 `RedisDistributedLock`。

## 工作原理

**两级混合缓存**：`IHybridCache<TCacheItem>` 是对 .NET `HybridCache` 的泛型薄封装。读取时优先命中本地内存（L1），未命中回落分布式层（L2，内存或 Redis），再未命中执行工厂并回填两级；两级一致失效。`GetOrCreateAsync` 的 `optionsFactory` 可按调用定制 `HybridCacheEntryOptions`（本地/分布式过期时间）。

**键规范化（租户隔离）**：所有分布式缓存键先经 `DefaultDistributedCacheKeyNormalizer.NormalizeKey` 处理，产出形如 `{tenantId}:{cacheName}:{key}`。租户段取当前 `ICurrentTenantAccessor.Current.TenantId`；无租户（宿主）时用 `0`。`cacheName` 由 `CacheNameAttribute.GetCacheName<TCacheItem>()` 推断（有 `[CacheName]` 用其值，否则用类型全名去掉 `CacheItem` 后缀）。若某条查询要跨租户共享，可通过 `DistributedCacheKeyNormalizeArgs.IgnoreMultiTenancy` 跳过租户段。

**Redis 装配切换**：`IDistributedCache<>` 的模式匹配 / Lua 等高级能力，依赖底层 `IDistributedCache` 是否实现三个"能力"标记接口 —— `ICacheSupportsMultipleItems`、`ICacheSupportsKeyPattern`、`ICacheSupportsLuaScript`。`XiHanRedisCache`（继承自框架 `RedisCache`）实现了全部三者，因此启用 Redis 后 `GetKeys` / `RemoveByPattern` / `ScriptEvaluate` 才真正生效；内存分布式缓存不实现这些接口，相应方法在纯内存模式下不可用（会退化/抛错）。

**`considerUow` 与工作单元**：多数方法带 `bool considerUow = false`。传 `true` 时缓存写入会挂到当前工作单元，UoW 提交后才实际生效，读时也会考虑 UoW 内的暂存变更，保证事务内读到自己写的缓存。

## 核心能力

- **两级混合缓存**：`IHybridCache<TCacheItem>` / `IHybridCache<TCacheItem, TCacheKey>`，泛型封装 .NET `HybridCache`。
- **泛型分布式缓存**：`IDistributedCache<TCacheItem>` / `IDistributedCache<TCacheItem, TCacheKey>`，同步/异步、单条/批量、GetOrAdd、Refresh、Exists、按模式查/删、Lua 脚本一应俱全。
- **租户感知键**：`DefaultDistributedCacheKeyNormalizer`，`TenantId` 前缀（宿主用 `0`）。
- **AOP 声明式缓存**：`[Cacheable]` 缓存方法结果；`[CacheEvict]` 标注失效意图；`[CacheName]` 指定缓存名。
- **Redis 分布式锁**：`IDistributedLock`（`SET NX PX` + 释放校验持有者），带 `WithLockAsync` / `AcquireAsync`（等待重试）便捷扩展。
- **Redis 延迟队列**：`IRedisDelayQueue<T>`，基于 Sorted Set，到期原子领取。
- **Redis Stream 可靠队列**：`IRedisStreamQueue<T>`，消费组 + ACK + 崩溃重投（at-least-once）。
- **原生连接暴露**：Redis 启用后可直接注入 `IConnectionMultiplexer` 做定制操作，另有 `XiHanRedisExtensions` 提供批量 Hash 读取扩展。

## 主要 API / 类型

### 混合缓存 `IHybridCache<TCacheItem>`（键为 `string`）/ `IHybridCache<TCacheItem, TCacheKey>`

`TCacheItem` 需为 `class`。核心方法（均异步）：

| 方法 | 说明 |
| --- | --- |
| `Task<TCacheItem?> GetOrCreateAsync(key, Func<Task<TCacheItem>> factory, Func<HybridCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)` | 命中返回；未命中执行工厂并回填两级 |
| `Task SetAsync(key, TCacheItem value, HybridCacheEntryOptions? options = null, ...)` | 写入缓存 |
| `Task RemoveAsync(key, ...)` | 移除单条 |
| `Task RemoveManyAsync(IEnumerable<TCacheKey> keys, ...)` | 批量移除 |

`IHybridCache<TCacheItem>` 额外暴露 `InternalCache`（底层 `IHybridCache<TCacheItem, string>`）。

### 分布式缓存 `IDistributedCache<TCacheItem>` / `IDistributedCache<TCacheItem, TCacheKey>`

`TCacheItem` 需为 `class`，方法几乎都有同步 + `...Async` 两版，且带 `hideErrors` / `considerUow` 参数。关键方法：

| 分组 | 方法 |
| --- | --- |
| 读取 | `TCacheItem? Get(key, ...)` · `Task<TCacheItem?> GetAsync(...)` · `GetMany(keys, ...)` · `GetManyAsync(...)` |
| 读或建 | `TCacheItem? GetOrAdd(key, Func<TCacheItem> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, ...)` · `GetOrAddAsync(...)` · `GetOrAddMany(...)` · `GetOrAddManyAsync(...)` |
| 写入 | `void Set(key, value, DistributedCacheEntryOptions? options = null, ...)` · `SetAsync(...)` · `SetMany(...)` · `SetManyAsync(...)` |
| 刷新 | `void Refresh(key, ...)` · `RefreshAsync(...)` · `RefreshMany(...)` · `RefreshManyAsync(...)` |
| 移除 | `void Remove(key, ...)` · `RemoveAsync(...)` · `RemoveMany(...)` · `RemoveManyAsync(...)` |
| 存在性 | `bool Exists(key, ...)` · `Task<bool> ExistsAsync(...)` |
| 模式操作（仅字符串键 + 支持的实现） | `TCacheKey[] GetKeys(string pattern = "*", ...)` · `GetKeysAsync(...)` · `long RemoveByPattern(string pattern = "*", ...)` · `RemoveByPatternAsync(...)` |
| Lua 脚本（仅支持的实现） | `RedisResult? ScriptEvaluate(string script, IEnumerable<TCacheKey>? keys = null, IEnumerable<RedisValue>? values = null, ...)` · `ScriptEvaluateAsync(...)` |

### 分布式锁 `IDistributedLock`

```csharp
Task<IDistributedLockHandle?> TryAcquireAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default);
```

单次尝试，拿不到返回 `null`（不阻塞、不重试）。`IDistributedLockHandle`（`IDisposable` + `IAsyncDisposable`）暴露 `ResourceKey` / `LockId` / `IsReleased`，方法 `Task ReleaseAsync()`（幂等，仅删自己持有的）与 `Task<bool> ExtendAsync(TimeSpan expiry, ...)`（续期）。便捷扩展 `DistributedLockExtensions`：

- `Task<bool> WithLockAsync(this IDistributedLock, resourceKey, expiry, Func<CancellationToken, Task> action, ...)` —— 拿到就执行并自动释放，拿不到返回 `false`。
- `Task<(bool Acquired, TResult? Result)> WithLockAsync<TResult>(..., Func<CancellationToken, Task<TResult>> func, ...)` —— 带返回值版本。
- `Task<IDistributedLockHandle?> AcquireAsync(..., TimeSpan wait, TimeSpan? pollInterval = null, ...)` —— 等待重试直到拿到或超时（默认轮询 100ms）。

### Redis 延迟队列 `IRedisDelayQueue<T>`

基于 Sorted Set，`score = 到期时间戳(ms)`，每个消息类型 `T` 独占一个 ZSET（键由类型派生）。单例，一封闭类型一实例。

| 方法 | 说明 |
| --- | --- |
| `Task EnqueueAsync(T item, TimeSpan delay, ...)` | 延迟 `delay` 后可被取出 |
| `Task EnqueueAtAsync(T item, DateTimeOffset dueTime, ...)` | 到 `dueTime` 后可被取出 |
| `Task<IReadOnlyList<T>> DequeueDueAsync(int count, ...)` | 原子领取已到期消息（多消费者不重复），领取即移除 |
| `Task<long> CountAsync(...)` | 队列总数（含未到期） |

延迟精度 ≈ 消费者轮询周期（到期项不主动唤醒，需定期 `DequeueDueAsync`）。

### Redis Stream 可靠队列 `IRedisStreamQueue<T>`

基于 Redis Streams + 消费组，at-least-once。每个 `T` 独占一个 Stream。

| 方法 | 说明 |
| --- | --- |
| `Task<string> EnqueueAsync(T item, ...)` | 入队并广播唤醒，返回条目 ID |
| `Task<IReadOnlyList<RedisStreamMessage<T>>> ReadAsync(string consumer, int count, ...)` | 消费组读一批新消息（进入 PEL 待确认） |
| `Task AckAsync(IEnumerable<string> messageIds, ...)` | 确认处理完成（移出 PEL） |
| `Task<IReadOnlyList<RedisStreamMessage<T>>> ClaimStaleAsync(string consumer, TimeSpan minIdle, int count, ...)` | 认领空闲超阈值的待确认消息（崩溃重投），用于重试 |
| `Task<long> CountAsync(...)` | Stream 当前长度（积压估算） |
| `Task WaitForSignalAsync(TimeSpan timeout, ...)` | 阻塞等待"有新消息"，被入队唤醒或超时返回；替代空转轮询 |

`RedisStreamMessage<T>` 为 `readonly record struct`，含 `Id` / `Value` / `DeliveryCount`（首次为 1，每次认领重投 +1，可据此转死信）。

### AOP 特性

| 特性 | 作用域 | 可配置项 |
| --- | --- | --- |
| `[Cacheable]` | 方法（`AllowMultiple = false`） | `string Key`（键模板，支持 `{paramName}` 占位）、`int ExpireSeconds`（默认 300；本地过期取 `min(ExpireSeconds, 60)`） |
| `[CacheEvict]` | 方法（`AllowMultiple = true`） | `string Key`（键模板） |
| `[CacheName]` | 类 / 接口 / 结构 | 构造参数 `name`，指定缓存名（覆盖类型名推断） |

键模板由 `CacheKeyBuilder.Build(template, invocation)` 渲染：把 `{参数名}` 替换为对应实参 `ToString()`（`null` → `"null"`）。占位符正则为 `\{[a-zA-Z_]\w*\}`。

读写语义由 `CacheAspect` 承载，`CacheInterceptor`（进程内）与 `XiHanCacheFilter`（HTTP）共用它，两条入口的键构建、过期时间与命中判定一致。目标方法成功执行后渲染每个 `[CacheEvict]` 的键模板，并调用底层 `HybridCache.RemoveAsync`，同时清除 L1 与 L2。目标方法抛异常时既不写入缓存也不执行清理；同一方法可声明多个 `[CacheEvict]`。

## 配置

配置节：`XiHan:Caching:Redis`（`XiHanRedisCacheOptions.SectionName`）。

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `IsEnabled` | `bool` | `false` | 是否启用 Redis（关闭时全部回落内存实现） |
| `Configuration` | `string` | `""` | StackExchange.Redis 连接串，如 `127.0.0.1:6379,password=...,defaultDatabase=0` |
| `InstanceName` | `string?` | `null` | 键前缀（实例名） |
| `ConnectTimeout` | `int` | `5000` | 连接超时（ms） |
| `SyncTimeout` | `int` | `5000` | 同步超时（ms） |
| `AsyncTimeout` | `int` | `5000` | 异步超时（ms） |
| `AllowAdmin` | `bool` | `false` | 是否允许管理员操作 |
| `UseSsl` | `bool` | `false` | 是否使用 SSL |
| `AbortOnConnectFail` | `bool` | `false` | 连接失败是否中止 |

> 注意：当前 `AddXiHanCaching` 在装配 `AddStackExchangeRedisCache` 时只读取 `Configuration` 与 `InstanceName`；`ConnectTimeout` / `SyncTimeout` / `AllowAdmin` 等字段应写在连接串里生效，或供上层自行读取，装配代码本身未单独消费它们。全局条目默认滑动过期为 20 分钟（来自 `XiHanDistributedCacheOptions.GlobalCacheEntryOptions`）。

示例 `appsettings.json`：

```json
{
  "XiHan": {
    "Caching": {
      "Redis": {
        "IsEnabled": true,
        "Configuration": "127.0.0.1:6379,password=mypassword,defaultDatabase=0,abortConnect=false",
        "InstanceName": "Default:"
      }
    }
  }
}
```

## 使用示例

### 泛型混合缓存：读或建

```csharp
public class ProfileService(IHybridCache<UserProfile> cache)
{
    public Task<UserProfile?> GetProfileAsync(string userId)
    {
        // 命中即返回；未命中执行工厂并写入两级缓存（键已按当前租户隔离）
        return cache.GetOrCreateAsync(
            userId,
            () => LoadFromDbAsync(userId),
            optionsFactory: () => new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(1)
            });
    }
}
```

### 声明式缓存

```csharp
public interface IConfigAppService
{
    [Cacheable(Key = "config:{tenantId}:{key}", ExpireSeconds = 300)]
    Task<string?> GetAsync(string tenantId, string key);

    [CacheEvict(Key = "config:{tenantId}:{key}")]
    Task UpdateAsync(string tenantId, string key, string value);
}
```

### 分布式锁（拿到才执行）

```csharp
public class ReportJob(IDistributedLock distributedLock)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var ran = await distributedLock.WithLockAsync(
            resourceKey: "report:daily",
            expiry: TimeSpan.FromMinutes(5),
            action: async token => await GenerateReportAsync(token),
            cancellationToken: ct);

        if (!ran)
        {
            // 未抢到锁，说明已有实例在跑，跳过
        }
    }
}
```

### 延迟队列（提交即入队、后台拉取消费）

```csharp
// 生产：30 分钟后再处理
await delayQueue.EnqueueAsync(new OrderTimeoutMessage(orderId), TimeSpan.FromMinutes(30), ct);

// 消费（后台服务循环）：取出已到期的
var due = await delayQueue.DequeueDueAsync(count: 50, ct);
```

## 扩展点 / 自定义

- **替换键规范化策略**：`IDistributedCacheKeyNormalizer` 用 `AddSingleton`（非 `TryAdd`）注册默认实现；如需改键格式，在你自己的模块里再次注册覆盖即可。
- **替换分布式锁**：`IDistributedLock` 默认用 `TryAddSingleton` 注册进程内实现，Redis 启用时框架用 `Replace` 升级为 Redis 实现；你也可自行 `Replace` 成自定义实现。
- **自定义序列化**：`IDistributedCacheSerializer` 默认 `JsonDistributedCacheSerializer`，可覆盖。
- **能力标记接口**：自定义 `IDistributedCache` 实现若想支持按模式/Lua，需实现 `ICacheSupportsMultipleItems` / `ICacheSupportsKeyPattern` / `ICacheSupportsLuaScript`。
- **原生 Redis**：Redis 启用后可直接注入 `IConnectionMultiplexer` 做框架未封装的操作。

## 注意事项与最佳实践

- **纯内存模式的能力边界**：未启用 Redis 时，`GetKeys` / `RemoveByPattern` / `ScriptEvaluate` 等依赖能力接口的方法不可用；分布式锁仅进程内有效（多实例部署无跨实例互斥）。这些能力务必在启用 Redis 后使用。
- **`[CacheEvict]` 只在方法成功后清理**：键模板必须与读取侧一致；事务写入若要求“提交后再失效”，应继续使用带 `considerUow` 的缓存接口或应用侧提交后失效机制，避免数据库回滚后缓存已被提前清除。
- **租户隔离是默认行为**：键自动带租户前缀；要跨租户共享缓存需显式走 `IgnoreMultiTenancy`，否则不同租户天然隔离。
- **`hideErrors`**：多数方法支持隐藏分布式缓存异常（缓存故障不影响主流程）；对一致性敏感的场景应显式传 `false` 让异常冒泡。
- **后台队列消费**：延迟/Stream 队列建议配合框架后台服务基类拉取消费（提交后入队、原子领取、启动恢复），避免自行轮询。

## 依赖模块

- [XiHan.Framework.Core](./core)
- [XiHan.Framework.MultiTenancy.Abstractions](./multitenancy-abstractions)（租户感知键）
- [XiHan.Framework.Serialization](./serialization)（JSON 序列化）
- [XiHan.Framework.Threading](./threading)
- [XiHan.Framework.Uow](./uow)（工作单元感知 `considerUow`）
- 第三方核心：`Microsoft.Extensions.Caching.Hybrid`、`Microsoft.Extensions.Caching.StackExchangeRedis`、`StackExchange.Redis`

## 相关模块

- [XiHan.Framework.Uow](./uow)
- [XiHan.Framework.Data](./data)
- [XiHan.Framework.MultiTenancy.Abstractions](./multitenancy-abstractions)
