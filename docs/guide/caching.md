# 缓存与分布式锁

框架的缓存抽象、分布式锁、以及**失效时序**这个最容易出错的点。

完整 API 见 [Caching 包](../packages/caching)。

## 三层能力

| 能力 | 接口 | 说明 |
| --- | --- | --- |
| 分布式缓存 | `IDistributedCache` / `IDistributedCache<T>` | 有 Redis 走 Redis，否则退化为进程内内存 |
| 分布式锁 | `IDistributedLock` | 同上，**未接 Redis 时退化为进程内锁** |
| 延迟队列 / 流式队列 | `IRedisDelayQueue<T>` / `IRedisStreamQueue<T>` | 名字里就带 Redis，是**诚实的专用抽象**，需要 Redis |

配置节 `XiHan:Caching:Redis`：

| 键 | 说明 |
| --- | --- |
| `IsEnabled` | **关闭则退化为进程内内存缓存** |
| `Configuration` | 连接串 `host:port,user=,password=,defaultDatabase=` |
| `InstanceName` | Key 统一前缀，隔离不同应用/环境 |
| `AbortOnConnectFail` | `false` = 后台持续重连，更适合生产 |
| `AllowAdmin` | 允许 FLUSHDB / CONFIG 等管理命令，**生产慎开** |

::: danger 关掉 Redis 的连锁反应
`IsEnabled=false` 时**分布式锁退化为进程内锁**——多实例部署会各跑各的：定时任务重复执行、后台 Worker 不再单活、工作流定时器多实例并发。

单机开发无所谓，**生产必须开**。
:::

## 声明式缓存

给方法打特性：

```csharp
[Cacheable(Key = "user:{userId}", ExpirationSeconds = 300)]
public Task<UserDto> GetUserAsync(long userId) { … }

[CacheEvict(Key = "user:{userId}")]
public Task UpdateUserAsync(long userId, UserUpdateDto input) { … }
```

::: warning 非 HTTP 入口的前提是服务注册为接口
声明式缓存有两条生效路径：HTTP 请求由 `XiHanCacheFilter` 在 MVC 动作外层处理（动态 API 控制器注入具体类也生效），进程内互调靠 AOP 代理，**此时服务的注册类型必须是接口**，否则特性静默失效。见 [AOP 与拦截器](./aop)。
:::

## 缓存条目模式

复杂场景推荐「一个热点读定义一个条目类」，把「键怎么拼、值是什么、多久过期」封装在一起，而不是到处 `cache.GetAsync("字符串")`。

好处：

- 键名集中，不会拼错；
- 失效时能按模式批量清理；
- 值的结构有类型约束。

::: tip 键名一律用常量
把缓存键前缀定义成 `const` 集中管理，禁止内联字符串——否则失效逻辑找不到你的键，表现是「改完不生效」。
:::

## 失效时序：最容易出错的地方

::: danger 写路径失效必须排队到事务提交之后
```csharp
await cache.RemoveByPatternAsync(pattern, hideErrors: true, considerUow: true, token: ct);
```

`considerUow: true` 让失效动作**排队到工作单元提交之后**执行。

如果在事务提交**前**就清了缓存，并发请求会立刻回源查库，读到的是**尚未提交的旧值**，然后把旧值重新写回缓存——事务提交后缓存反而是错的，**且不会自愈**。

这类问题极难复现（要恰好有并发读落在提交前的窗口里），一旦发生只能靠手工清缓存恢复。**务必保持 `considerUow: true`。**
:::

完整的提交时序见 [工作单元与事务](./uow#提交时序)。

## 分布式锁

```csharp
await using var handle = await distributedLock.TryAcquireAsync("resource-key", TimeSpan.FromMinutes(5));
if (handle is null) return;   // 没抢到，本轮跳过
// 临界区
```

用途：多实例下的单活 Worker、防止定时任务重入、跨实例的资源互斥。

::: warning 锁 TTL 要大于临界区耗时
TTL 是崩溃安全网——进程挂了锁能自动释放。但如果临界区跑得比 TTL 久，锁会在执行中途过期，另一个实例就进来了。
:::

## Lua 脚本

Lua 执行**不在 `IDistributedCache` 这个通用契约上**，而是可选能力接口 `ICacheSupportsLuaScript`——通用缓存抽象不应该焊上某个客户端的类型，否则换实现就要改所有调用方。

用法是先做类型判断：

```csharp
if (cache is ICacheSupportsLuaScript lua)
{
    CacheScriptResult result = await lua.ScriptEvaluateAsync(script, keys, args);
}
```

签名是中立的：入参 `object?[]`，返回 `CacheScriptResult`（承载标量、整数、布尔与嵌套数组）。具体实现负责把自己的原生返回值映射成这个中立结构。

`IRedisDelayQueue` / `IRedisStreamQueue` 则是名字里就带 Redis 的专用抽象，直接用即可。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 改了数据但读到旧值 | 写侧漏调失效；或键名内联字符串拼错 |
| 偶发读到旧值且不自愈 | 失效没走 `considerUow: true` |
| `[Cacheable]` 没生效 | 非 HTTP 入口（后台作业、事件处理器、Minimal API）且服务注册类型不是接口 |
| 多实例定时任务重复执行 | Redis 没开，分布式锁退化成进程内锁 |
| 想执行 Lua 脚本 | 用能力接口 `ICacheSupportsLuaScript`，见上节 |

## 下一步

- [工作单元与事务](./uow)：失效为什么要排队
- [AOP 与拦截器](./aop)：声明式缓存的实现机制
- [Caching 包](../packages/caching)：完整 API 与配置
