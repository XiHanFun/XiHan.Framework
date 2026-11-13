# 分布式锁与幂等

这一页讲建立在共享缓存存储之上的两样东西：`IDistributedLock` 租约锁，以及按幂等键给调用去重、回放首次结果的 Idempotency 阶段；最后说明怎样让沿用 .NET `IDistributedCache` 的代码接到同一个存储上。读完你能让多实例中只有一个实例执行某项工作，让客户端重试同一个请求时拿回首次的响应。前置知识是[缓存读写](/caching/basics)（`ICacheStore` 的原子原语与 `caching.distributed` 能力）与[执行管道](/startup/pipeline)（Idempotency 阶段的位置）。

## 为什么需要

多实例部署下，定时报表、补偿任务这类工作如果每个实例都跑一遍，轻则浪费重则写出重复数据；客户端在网络抖动后重试一次「下单」，服务端若照常处理就会生成两个订单。解决这两类问题都需要一个所有实例可见的原子存储：锁用「不存在才写」互斥，幂等用同一原语登记「这个键已经处理过」。框架不另起一套基础设施，直接在 `caching.distributed` 的 `ICacheStore` 上实现租约锁与幂等中间件，装配了 Redis 之后两者自然跨实例，不装配时在单进程内同样可用于开发与测试。

## 锁的概念

`IDistributedLock` 是建立在存储原语上的租约锁：`TryAcquireAsync(resource, ttl)` 用 `TryAddAsync` 写入键 `lock:<resource>`，值是本次获取生成的唯一令牌；释放用 `TryRemoveAsync` 按令牌比较删除。

| 成员 | 说明 |
| --- | --- |
| `IDistributedLock.TryAcquireAsync(resource, timeToLive, ct)` | 返回 `Maybe<LockHandle>`；被占用时无值，不产生错误码 |
| `DistributedLockExtensions.AcquireAsync(resource, timeToLive, ct)` | 结果式版本，返回 `Result<LockHandle>`；被占用返回 `CACHING_CONFLICT_001` |
| `LockHandle.Resource` / `LockHandle.Token` | 锁定的资源键与本次获取的令牌 |
| `LockHandle.ReleaseAsync(ct)` / `DisposeAsync()` | 按令牌比较删除；只释放一次（并发释放也只执行一次），重复释放返回 `false` |
| 自己实现 `IDistributedLock` | 派生抽象类 `LockHandle`（受保护构造收资源与令牌），实现 `ReleaseCoreAsync`；基类保证释放只调用它一次 |

锁不可重入。持有期间可以用 `LockHandle.TryExtendAsync(ttl)` 续约：仍持有时把到期时刻重设为现在加 `ttl` 并返回 `true`，锁在续约期间一直被持有；已到期、被他人持有或已释放时返回 `false`（ADR-0215）。到期后别人可以获取，原持有者再释放返回 `false`，不会误删别人的锁。`IDistributedLock` 由被选中提供 `caching.distributed` 的 Provider（内存、Redis、远程）登记在容器里，用的 `ICacheStore` 与 `ISharedCache.Store` 是同一个实例。

## 幂等的概念

| 术语 | 说明 |
| --- | --- |
| 幂等键 | 客户端为「一次业务意图」生成的唯一字符串，经 Baggage `xihan.idempotency-key`（`BaggageKeys.IdempotencyKey`）随调用传入 |
| Idempotency 阶段 | 管道里 Authenticate、Tenant、Authorize、Validate 之后、Transaction 之前的阶段；装配了 `caching.distributed` 的 Provider 时这里有 `IdempotencyMiddleware` |
| 作用域 | 去重按（租户、主体、键）三元组，存储键 `idem:<租户>:<长度>.<主体>:<幂等键>`，没有租户或匿名时对应段为 `-`；主体带长度前缀，含冒号的主体 ID 不会与键拼出同一个存储键（ADR-0205）。匿名调用共用一个命名空间，键是唯一的保护，要用猜不到的随机键 |
| 记录 | 中间件内部的存储格式：处理中标记，或已完成调用的结果；操作调用另记操作 ID 与响应 |
| 回放 | 重复调用遇到已完成的记录时不再执行处理器，直接还原首次结果；`IdempotencyMiddleware.WasReplayed(context)` 为 `true` |

调用不带幂等键时中间件直接放行；消息派发也不写幂等键，处理器的调用不会被去重（原因见[发布与订阅](/messaging/pubsub)）。

## 最小示例

下面的 `DailyReport` 保证同一时刻只有一个实例在跑日报：拿到锁的实例执行工作并在结束时释放，拿不到的直接返回 `false`。

```csharp
using XiHan.Framework.Caching.Abstractions.Locking;

public sealed class DailyReport(IDistributedLock locks)
{
    public async ValueTask<bool> TryRunAsync(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken)
    {
        var acquired = await locks.TryAcquireAsync("report:daily", TimeSpan.FromMinutes(5), cancellationToken);
        if (!acquired.TryGetValue(out var handle))
        {
            // 别的实例正持有锁
            return false;
        }

        // 释放即按令牌比较删除；工作超过五分钟时锁已到期，别的实例可能已经进来
        await using (handle)
        {
            await work(cancellationToken);
        }

        return true;
    }
}
```

把它登记成单例（`context.Services.AddSingleton<DailyReport>()`），所在特性的清单声明依赖 `caching.distributed`（`CachingCapabilities.Distributed`）。装配了 Redis 时，在两个实例上同时调用 `TryRunAsync`，只有一个返回 `true`；另一个在前者释放或五分钟到期之后才可能拿到锁。只用 Standard 组合包时锁在单进程内照样互斥，便于本地验证。

要把占用当作调用失败返回给调用方，用结果式的 `AcquireAsync`，被占用返回 `CACHING_CONFLICT_001`（HTTP 409，消息键 `caching.lock.held`，参数 `resource`，按调用方区域翻译）：

```csharp
var acquired = await locks.AcquireAsync($"orders:{orderId:N}", TimeSpan.FromSeconds(30), cancellationToken);
if (!acquired.TryGetValue(out var handle))
{
    return acquired.Error!;
}

await using (handle)
{
    await work(cancellationToken);
}
```

存活时间要大于工作的最长耗时。需要续约或可重入的场景在 `ICacheStore` 的「不存在才写 + 比较删」之上自己实现。雪花 ID 的机器位租约就用这把锁，见[标识生成](/persistence/identifiers)。

## 幂等键的处理流程

调用的 Baggage 带 `xihan.idempotency-key` 时，`IdempotencyMiddleware` 按（租户、主体、键）去重：

1. 首次调用用 `TryAddAsync` 写入「处理中」标记，存活 `InProgressTimeToLive`（默认 60 秒），然后执行后续阶段。
2. 执行完用调用结果替换标记，保留 `TimeToLive`（默认 24 小时）。成功与失败的结果都会记录；操作调用另记操作 ID，成功时连同响应一起记。
3. 处理器抛出异常时按值比较删除自己的标记，允许重试。
4. 重复调用遇到「处理中」返回 `CACHING_CONFLICT_002`；遇到已完成的记录直接回放，不再执行处理器。

HTTP 上幂等键走 W3C `baggage` 头（gRPC 是同名的 `baggage` 元数据）。向示例应用重复提交同一个下单请求：

```bash
curl -X POST http://localhost:5000/orders \
  -H "Authorization: ApiKey dev-alice-key" \
  -H "baggage: xihan.idempotency-key=order-20261031-0001" \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","amount":2}'
```

第一次返回 201 与新订单；原样再发一次，拿回同一个 201 与同一份正文，`/orders` 的处理器没有再执行，订单也没有多出一个。不同租户、不同用户提交同一个键互不回放。拼出的键不满足 `CacheKey` 文法（含空白、超过 512 字符）时调用以 `CACHING_VALIDATION_001` 结束。

::: warning 失败的结果同样会被回放
处理器返回的失败（如业务冲突）与成功一样被记录 24 小时，同一幂等键重试只会拿回同一个错误。客户端修正请求后要换一个新键；只有处理器抛出异常时标记会被清除。
:::

## 响应回放

回放还原首次调用的结果，操作调用还还原响应：

| 入口 | 回放得到 |
| --- | --- |
| HTTP 端点 | 与首次一样的状态码（`create` 为 201）与正文；端点不执行，由 `UseXiHan()` 写出 |
| XGI（`IOperationInvoker.InvokeAsync(context, request)`，gRPC、MCP 走它） | 与首次逐字节一致的载荷 |
| 类型化（`InvokeAsync<TRequest, TResponse>`） | 按同一份序列化元数据解码出的等值响应对象 |
| 非操作调用（消息以外的 `App.InvokeAsync`） | 只有调用结果 |

回放不比对请求内容，同一个键配不同的请求体照样拿回首次的响应；键要按「一次业务意图一个键」生成。记录不能回放给本次调用时返回 `CACHING_CONFLICT_003`（HTTP 409）：

- 同一个键被另一个操作用过，或在操作与非操作调用之间复用（消息键 `caching.idempotency.key_reused`，参数 `operation`、`recorded`）。
- 记录里没有响应，或响应按当前的响应类型解码不了（`caching.idempotency.response_unavailable`），换一个键即可。

响应随记录保存 `TimeToLive`，响应很大的操作带幂等键时要考虑共享缓存的容量。Idempotency 阶段在 Authenticate、Tenant、Authorize、Validate 之后，被这些阶段拒绝的调用不会写入记录；在 Transaction 之前，所以记录的是提交之后的最终结果。中间件里可以用 `IdempotencyMiddleware.WasReplayed(context)` 判断本次是否是回放。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Caching:Idempotency:TimeToLiveSeconds` | 数字 | 86400 | 已完成调用结果的保留时间 |
| `XiHan:Caching:Idempotency:InProgressTimeToLiveSeconds` | 数字 | 60 | 「处理中」标记的保留时间，也是处理器崩溃后同一个键被封锁的最长时间 |

两个值都必须是正数，否则装配抛出 `ArgumentException`。锁没有自己的配置，存活时间由每次 `TryAcquireAsync` 的参数决定。

## IDistributedCache 适配

沿用 .NET `IDistributedCache` 的代码（ASP.NET Core 会话、第三方库）不需要改调用点：被选中提供 `caching.distributed` 的 Provider 用 `AddXiHanDistributedCache` 把 `CacheStoreDistributedCache` 登记为 `IDistributedCache`，架在同一个字节存储上。应用自己登记的 `IDistributedCache` 优先。

| `DistributedCacheEntryOptions` | 换算 |
| --- | --- |
| `AbsoluteExpiration` / `AbsoluteExpirationRelativeToNow` | 取较早者换算成 `TimeToLive`；已经过去的时刻抛 `ArgumentOutOfRangeException` |
| `SlidingExpiration` | 没有绝对过期时对应 `SlidingExpiration`；与绝对过期同时给时绝对优先 |
| 都不给 | `CacheEntryOptions.Default`，即存储的默认存活时间：内存与 Redis 实现缺省 300 秒，远程插件用自己的默认；不是不过期 |

键必须满足 `CacheKey` 文法，否则抛 `ArgumentException`。`Refresh` 是一次读取（读取即续期滑动过期）；同步成员在适配器边界阻塞等待异步存储，调用方应优先用异步成员。

## 错误码

| 错误码 | 含义 |
| --- | --- |
| `CACHING_CONFLICT_001` | 分布式锁被占用，`AcquireAsync` 返回（`TryAcquireAsync` 只返回无值） |
| `CACHING_CONFLICT_002` | 同一幂等键的调用仍在处理中 |
| `CACHING_CONFLICT_003` | 幂等记录不能回放给本次调用：键被另一个调用用过，或记录里的响应无法还原 |
| `CACHING_VALIDATION_001` | 幂等键拼出的存储键不合法，Idempotency 阶段以校验错误结束调用 |
| `CACHING_UNAVAILABLE_001` | 存储不可达；锁与幂等中间件都以 `FrameworkException` 抛出，不会当作「没拿到锁」或「没有记录」 |

错误怎样变成 HTTP 状态码与 Problem Details 见[结果与错误处理](/web/errors)。

## 常见问题

### 工作还没做完锁就到期了怎么办

到期后别的实例可以拿到，原持有者释放时按令牌比较删除会返回 `false`，不会删掉别人的锁。需要长时间持有时，按存活时间的几分之一定时调用 `TryExtendAsync` 续约；它返回 `false` 说明锁已经丢了，立即停下手里的工作。不要用「释放再获取」续约：释放的那一刻别人就能拿走。

### 同一个实例能重复获取同一把锁吗

不能，锁不可重入。第二次 `TryAcquireAsync` 在第一把释放或到期之前返回无值。需要嵌套时把锁句柄沿调用链传下去，而不是再获取一次。

### 幂等键在 gRPC 与消息上怎么传

gRPC 用同名的 `baggage` 元数据，格式与 HTTP 头一致。消息不传幂等键：派发不写它，带上也会被忽略，因为同一请求发出的几条消息与同一条消息的重试会被当成同一次调用。消息的去重由处理器按信封的 `Id` + `Source` 自理。

### 为什么业务失败也会被回放

因为「同一个键」表示「同一次业务意图」，首次的结论就是这次意图的结论，重试拿回同一个错误才与首次一致。客户端修正了请求就应当换一个新键。只有处理器抛出异常（服务端缺陷或暂时故障）时标记才被清除、允许重试。

### 同一个键用在了两个不同的操作上

返回 `CACHING_CONFLICT_003`，消息键 `caching.idempotency.key_reused`。幂等键在（租户、主体）作用域内要对操作唯一，建议把操作名或业务对象编号拼进键里，例如 `orders.create:<客户端请求号>`。

## 相关包与下一步

- 包参考：[Caching.Abstractions](/packages/caching-abstractions)、[Caching.InMemory](/packages/caching-inmemory)、[Caching.Redis](/packages/caching-redis)
- Idempotency 阶段在管道里的位置与中间件的登记方式见[执行管道](/startup/pipeline)；Baggage 的传递见[调用上下文](/startup/invocation-context)
- 下一步：[两级缓存与键管理](/caching/hybrid)，减少读路径的网络往返并给管理界面提供键列举
