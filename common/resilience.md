来源：https://framework.docs.xihanfun.com/common/resilience

# 弹性与限流

这一章讲 Resilience 阶段的四件事：截止时间、熔断、固定窗口限流与并发上限，以及怎样按操作配置它们。读完你能给一个昂贵的操作限制并发与频率、给不稳定的下游加熔断，并分得清 504 与 408 各代表什么。前置知识是[执行管道](/startup/pipeline)与[结果与错误处理](/web/errors)。

## 为什么需要

一个操作调用慢了、下游挂了、流量突然翻倍，如果不加任何限制，线程与连接会被耗尽，一个坏操作拖垮整个进程。调用方等不到回应就重试，雪崩随之而来。解决这些问题的手段是成熟的：给每次调用一个截止时间、对失败的下游熔断、对昂贵操作限流与限并发。难点在于把它们统一放在正确的位置，并且在每种协议入口上行为一致。

框架把它们放进内核管道的 Resilience 阶段：`DeadlineMiddleware` 按调用上下文的绝对截止时间取消下游，`ResilienceMiddleware` 按操作 ID 执行熔断、限流与并发上限，拒绝都以 `UNAVAILABLE` 结果返回，带 `retryAfter` 提示。配置写在 `XiHan:Kernel:Resilience`，不改代码。框架不提供网关与流量治理模块：按操作 ID 的限制由内核完成，反向代理与 API 网关交给 YARP、Nginx 等专门的组件。

## 截止时间

调用上下文带一个绝对截止时间 `InvocationContext.Deadline`（UTC，基于 `TimeProvider`）。内核的 `DeadlineMiddleware` 位于 Resilience 阶段开头，到点即取消下游：

| 结果 | 错误码 | HTTP | gRPC | 命令行退出码 |
| --- | --- | --- | --- | --- |
| 到达截止时间 | `KERNEL_TIMEOUT_001` | 504 | `DeadlineExceeded` | 7 |
| 截止时间到达之前调用方取消（连接断开等） | `KERNEL_CANCELLED_001` | 408 | `Cancelled` | 8 |

截止时间随调用传播：HTTP 用请求头 `xihan-deadline`（RFC 3339，UTC），gRPC 用 `grpc-timeout`，命令行用 `--timeout <秒>`；下游调用的截止时间不晚于上游。操作与仓储只需要把 `context.CancellationToken` 传下去，不必自己计时；`context.RemainingTime(now)` 与 `context.IsExpired(now)` 可读剩余时间。

### 超时与取消的归因

截止时间到了一律报超时，调用方同时取消也一样。`DeadlineMiddleware` 只看令牌归因：截止令牌已触发即 `KERNEL_TIMEOUT_001`，截止令牌没有触发、调用方已取消才是 `KERNEL_CANCELLED_001`。下游抛出的取消，与下游接住取消、自己写下 `KERNEL_CANCELLED_001` 结果正常返回的，按同一规则归因。

不要在下游用 `context.IsExpired(now)` 区分超时与取消：定时器按系统节拍触发，可能早于时钟读数到达截止时间，几毫秒的差就会把超时判成取消；写下取消结果，交给 `DeadlineMiddleware` 归因。`DeadlineMiddleware` 自己也不看时钟：截止令牌还没触发时，即使时钟读数已过截止时间，调用方的取消仍是 `KERNEL_CANCELLED_001`。换来的是归因只有令牌一个来源，在注入的 `TimeProvider` 下可以确定地复现。

::: warning 按超时处理重试
按状态码或错误码决定重试的调用方要把 504 / `KERNEL_TIMEOUT_001` 按超时处理：截止时间已经用尽，被调方可能已经完成了副作用，非幂等操作重试前带上幂等键（Baggage `xihan.idempotency-key`，见[分布式锁与幂等](/caching/locks)）。408 / `KERNEL_CANCELLED_001` 只表示调用方自己取消了。熔断按原结果计数：`ResilienceMiddleware` 在 `DeadlineMiddleware` 之内，看到的是下游写下的原结果；审计、追踪与访问日志看到的是归因后的超时。
:::

## 按操作的限制

内核的 `ResilienceMiddleware` 紧随 `DeadlineMiddleware`，只作用于带操作描述的调用；宿主自定义端点、消息派发与作业派发不受它约束。每个操作 ID 各持一份限制实例，判定顺序固定为熔断、限流、并发：

| 限制 | 语义 | 拒绝码 |
| --- | --- | --- |
| 熔断 | 连续 `FailureThreshold` 次失败即打开 `BreakSeconds` 秒；到期半开，只放一次试探，成功关闭、失败重新打开 | `KERNEL_UNAVAILABLE_004` |
| 限流 | 固定窗口：每 `WindowSeconds` 秒最多 `PermitLimit` 次，超出的最多 `QueueLimit` 个排队到下一窗口，其余拒绝 | `KERNEL_UNAVAILABLE_002` |
| 并发 | 同时执行最多 `ConcurrencyLimit` 次，超出的最多 `ConcurrencyQueueLimit` 个排队，其余拒绝 | `KERNEL_UNAVAILABLE_003` |

只有 KIND 为 `UNAVAILABLE`、`TIMEOUT`、`UNEXPECTED` 的结果与未处理异常算熔断意义上的失败；校验不通过、找不到、无权限这类调用方错误不算。熔断打开期间仍在执行的调用结束时不改变熔断器状态。

拒绝都以 `UNAVAILABLE` 种类结束调用（HTTP 503），不进入后续阶段，`details` 带 `id`（操作 ID）；限流与熔断另带 `retryAfter`（秒，向上取整，至少 1）。排队等待受调用的取消令牌约束，截止时间到了按 `KERNEL_TIMEOUT_001` 结束，不会无限等待。限流与并发使用 BCL 的 `System.Threading.RateLimiting`，熔断是内核自己的三态状态机。

## 最小示例

我们给[操作契约](/web/operations)里的 `orders.create` 加限制：每 60 秒最多 2 次、同时只执行 1 个，其他操作每秒最多 1000 次。不改代码，只写配置：

```json
{
  "XiHan": {
    "Kernel": {
      "Resilience": {
        "PermitLimit": 1000,
        "WindowSeconds": 1,
        "Operations": {
          "orders.create": { "PermitLimit": 2, "WindowSeconds": 60, "ConcurrencyLimit": 1 }
        }
      }
    }
  }
}
```

运行应用后连续下三次单：

```bash
for i in 1 2 3; do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST localhost:5000/orders \
    -H "Content-Type: application/json" -d '{"name":"widget","amount":1}'
done
```

前两次 201，第三次 503，响应体是 Problem Details：

```json
{
  "type": "urn:xihan:error:KERNEL_UNAVAILABLE_002",
  "title": "UNAVAILABLE",
  "status": 503,
  "detail": "Operation 'orders.create' is rate limited; retry after 60 seconds.",
  "instance": "/orders",
  "code": "KERNEL_UNAVAILABLE_002",
  "traceId": "5978653a746fa20f0dce9f4a2f954bac",
  "details": { "id": "orders.create", "retryAfter": 60 }
}
```

再给请求头加 `xihan-deadline`，值是一个已经过去的 UTC 时刻，响应变成 504、`code` 为 `KERNEL_TIMEOUT_001`：截止时间在进入 Handle 之前就到了。

## 配置

配置节 `XiHan:Kernel:Resilience`：节本身的键是所有操作的默认策略，`Operations:<操作 ID>` 下的同名键覆盖该操作，缺省的键沿用默认策略。对应的类型是 `ResilienceOptions`（`Defaults` 与 `Operations`，`PolicyFor(operationId)` 取合并后的策略）与 `ResiliencePolicy`。

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `PermitLimit` | 正整数 | 不限流 | 每个窗口允许的调用数 |
| `WindowSeconds` | 正数 | `1` | 限流窗口 |
| `QueueLimit` | 非负整数 | `0` | 窗口用尽时排队等待的调用数 |
| `ConcurrencyLimit` | 正整数 | 不限并发 | 同时执行的调用数上限 |
| `ConcurrencyQueueLimit` | 非负整数 | `0` | 并发用尽时排队等待的调用数 |
| `FailureThreshold` | 正整数 | 不熔断 | 连续失败多少次后打开熔断器 |
| `BreakSeconds` | 正数 | `30` | 熔断打开的时长 |

一个限制都没配置的操作直接透传。非法值（非正数、`Operations` 下不合文法的操作 ID）在构建应用时抛出 `ArgumentException`。`XiHan:Kernel:UseBuiltInMiddleware=false` 时两个内置中间件都不登记，配置节不再读取。

一份带熔断的完整配置：

```json
{
  "XiHan": {
    "Kernel": {
      "Resilience": {
        "PermitLimit": 1000,
        "WindowSeconds": 1,
        "ConcurrencyLimit": 64,
        "FailureThreshold": 5,
        "BreakSeconds": 30,
        "Operations": {
          "reports.export": { "ConcurrencyLimit": 2, "PermitLimit": 10, "WindowSeconds": 60 }
        }
      }
    }
  }
}
```

## 内核范围之外

按调用方（主体、租户、IP）分桶的限流与自适应并发不在内核范围。需要时在 Resilience 阶段末尾登记应用自己的中间件，用 `System.Threading.RateLimiting` 的 `PartitionedRateLimiter` 按 Baggage 或调用来源分区：

```csharp
using System.Threading.RateLimiting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace Shop.Throttling;

public sealed class TenantRateLimitMiddleware : IPipelineMiddleware
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(
        tenant => RateLimitPartition.GetFixedWindowLimiter(tenant, static _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromSeconds(1),
        }));

    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        // 租户在 Tenant 阶段之后才解析，这里按调用方自报的 Baggage 分区，只用于限流、不用于授权
        var tenant = context.TryGetBaggage(BaggageKeys.Tenant, out var id) ? id : "anonymous";
        using var lease = await _limiter.AcquireAsync(tenant, 1, context.CancellationToken);
        if (!lease.IsAcquired)
        {
            context.SetOutcome(Error.Unavailable("SHOP_UNAVAILABLE_002", "shop.throttling.tenant_limited"));
            return;
        }

        await nextStep(context);
    }
}
```

登记到 Resilience 阶段末尾（内置中间件在阶段开头，应用的排在它们之后）：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .ConfigurePipeline(pipeline => pipeline.UseAt<TenantRateLimitMiddleware>(PipelineStage.Resilience)));
```

HTTP 层面的连接数、请求体大小与反向代理限流仍用 ASP.NET Core 与网关自身的设置。

## 常见问题

### 怎样区分 504 与 408

看谁先放弃。截止时间到了是 504 / `KERNEL_TIMEOUT_001`，不管调用方是否也取消了；调用方在截止时间之前自己断开或取消是 408 / `KERNEL_CANCELLED_001`。重试逻辑只需要关心 504：副作用可能已发生。

### 为什么我的端点没有被限流

`ResilienceMiddleware` 只作用于带操作描述的调用，即经 `[Operation]` 登记并由协议适配器或调用器发起的调用。`MapGet` 这类宿主自定义端点、消息处理器与作业不经它。给端点限流用 ASP.NET Core 自己的 `AddRateLimiter`。

### 限流是按实例还是按集群

按进程实例：每个操作 ID 在每个实例里各持一份限制器。多副本部署时实际上限是单实例上限乘以副本数。需要集群级配额用网关或共享存储实现。

### 熔断为什么没有打开

只有 `UNAVAILABLE`、`TIMEOUT`、`UNEXPECTED` 与未处理异常计入失败，而且要连续达到 `FailureThreshold` 次。校验失败、找不到、无权限是调用方的问题，不触发熔断。

### 怎样给操作加重试

内核不做重试：重试要由了解幂等性的一方决定。调用方按 503 的 `retryAfter` 与 504 的幂等键重试；操作内部调用下游时用下游客户端自己的重试策略，并把 `context.CancellationToken` 传下去，让截止时间约束整个重试过程。

## 相关包与下一步

- [Kernel](/packages/kernel)：`DeadlineMiddleware`、`ResilienceMiddleware`、`ResilienceOptions`
- [Primitives](/packages/primitives)：`InvocationContext.Deadline` 与 `BaggageKeys`
- [Hosting.AspNetCore](/packages/hosting-aspnetcore)：`xihan-deadline` 请求头与状态映射
- 规范：RFC-0004《管道阶段与排序》的「Resilience 阶段」一节
- 下一页：[JSON 序列化与 Native AOT](/common/serialization)
