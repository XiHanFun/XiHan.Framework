# 弹性与限流

本章说明 Resilience 阶段的截止时间、熔断、固定窗口限流与并发上限，以及如何按操作配置它们。适用于需要保护下游、限制昂贵操作或控制超时的开发者。

5.0 不再提供 4.x 的网关与流量治理模块：按操作 ID 的限制由内核完成，反向代理与 API 网关交给 YARP、Nginx 等专门的组件。

完整 API 见 [Kernel 包](../packages/kernel)。

## 截止时间

调用上下文带一个绝对截止时间 `InvocationContext.Deadline`（UTC，基于 `TimeProvider`）。内核的 `DeadlineMiddleware` 位于 Resilience 阶段开头，到点即取消下游：

| 结果 | 错误码 | HTTP |
| --- | --- | --- |
| 到达截止时间 | `KERNEL_TIMEOUT_001` | 504 |
| 调用方取消（连接断开等） | `KERNEL_CANCELLED_001` | 408 |

截止时间随调用传播：HTTP 用请求头 `xihan-deadline`（RFC 3339，UTC），gRPC 用 `grpc-timeout`，命令行用 `--timeout`；下游调用的截止时间不晚于上游。操作与仓储只需要把 `context.CancellationToken` 传下去，不必自己计时。`context.RemainingTime(now)` 与 `context.IsExpired(now)` 可读剩余时间。

## 按操作的限制

内核的 `ResilienceMiddleware` 紧随 `DeadlineMiddleware`，只作用于带操作描述的调用；宿主自定义端点、消息派发与作业派发不受它约束。每个操作 ID 各持一份限制实例，判定顺序固定为熔断 → 限流 → 并发：

| 限制 | 语义 | 拒绝码 |
| --- | --- | --- |
| 熔断 | 连续 `FailureThreshold` 次失败即打开 `BreakSeconds` 秒；到期半开，只放一次试探，成功关闭、失败重新打开 | `KERNEL_UNAVAILABLE_004` |
| 限流 | 固定窗口：每 `WindowSeconds` 秒最多 `PermitLimit` 次，超出的最多 `QueueLimit` 个排队到下一窗口，其余拒绝 | `KERNEL_UNAVAILABLE_002` |
| 并发 | 同时执行最多 `ConcurrencyLimit` 次，超出的最多 `ConcurrencyQueueLimit` 个排队，其余拒绝 | `KERNEL_UNAVAILABLE_003` |

只有 KIND 为 `UNAVAILABLE`、`TIMEOUT`、`UNEXPECTED` 的结果与未处理异常算熔断意义上的失败；校验不通过、找不到、无权限这类调用方错误不算。熔断打开期间仍在执行的调用结束时不改变熔断器状态。

拒绝都以 UNAVAILABLE 种类结束调用（HTTP 503），不进入后续阶段，`details` 带 `id`（操作 ID）；限流与熔断另带 `retryAfter`（秒，向上取整，至少 1）。排队等待受调用的取消令牌约束，截止时间到了按 `KERNEL_TIMEOUT_001` 结束，不会无限等待。限流与并发使用 BCL 的 `System.Threading.RateLimiting`，熔断是内核自己的三态状态机。

## 配置

配置节 `XiHan:Kernel:Resilience`：节本身的键是所有操作的默认策略，`Operations:<操作 ID>` 下的同名键覆盖该操作，缺省的键沿用默认策略。

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `PermitLimit` | 正整数 | 不限流 | 每个窗口允许的调用数 |
| `WindowSeconds` | 正数 | `1` | 限流窗口 |
| `QueueLimit` | 非负整数 | `0` | 窗口用尽时排队等待的调用数 |
| `ConcurrencyLimit` | 正整数 | 不限并发 | 同时执行的调用数上限 |
| `ConcurrencyQueueLimit` | 非负整数 | `0` | 并发用尽时排队等待的调用数 |
| `FailureThreshold` | 正整数 | 不熔断 | 连续失败多少次后打开熔断器 |
| `BreakSeconds` | 正数 | `30` | 熔断打开的时长 |

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

一个限制都没配置的操作直接透传。非法值（非正数、`Operations` 下不合文法的操作 ID）在构建应用时抛出 `ArgumentException`。`XiHan:Kernel:UseBuiltInMiddleware=false` 时两个内置中间件都不登记，配置节不再读取。

## 内核范围之外

按调用方（主体、租户、IP）分桶的限流与自适应并发不在内核范围。需要时在 Resilience 阶段末尾登记应用自己的中间件，用 `System.Threading.RateLimiting` 的 `PartitionedRateLimiter` 按 `context.GetSecurity()` 或调用来源分区：

```csharp
using System.Threading.RateLimiting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
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

HTTP 层面的连接数、请求体大小与反向代理限流仍用 ASP.NET Core 与网关自身的设置。

## 相关

- [执行管道](./pipeline)
- [结果与错误](./errors)
- 规范：RFC-0004《管道阶段与排序》的「Resilience 阶段」一节
