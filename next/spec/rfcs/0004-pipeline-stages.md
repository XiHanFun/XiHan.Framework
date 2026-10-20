# RFC-0004 管道阶段与排序

> 状态：草案 | 日期：2026-10-21 | 对应 ADR：0002

## 摘要

每次调用经过一条固定阶段的管道。特性按阶段贡献中间件，不需要知道其他中间件；排序确定、可打印、可测试。

## 阶段

顺序固定：

```text
ingress → observe → resilience → authenticate → tenant → authorize → validate → idempotency → transaction → handle → egress
```

| 阶段 | 职责 |
| --- | --- |
| `ingress` | 协议消息归一化、大小限制、构造上下文 |
| `observe` | Trace、日志、指标 |
| `resilience` | 超时、限流、自适应并发、熔断 |
| `authenticate` | 主体解析 |
| `tenant` | 租户解析与隔离上下文 |
| `authorize` | 策略判定 |
| `validate` | 契约校验 |
| `idempotency` | 幂等键去重 |
| `transaction` | 工作单元 |
| `handle` | 调用操作 |
| `egress` | 结果与错误翻译、响应信封、写回传播字段 |

## 排序规则

1. 先按阶段。
2. 阶段内按 `before` / `after` 约束做拓扑排序；约束成环报 `KERNEL_PIPELINE_001`。
3. 仍未定序的按包 ID 字典序。
4. 显式 `Use<T>()` 不带阶段时归入 `handle` 之前的位置，视为 `transaction` 阶段末尾。
5. 未知阶段名报 `KERNEL_PIPELINE_002`。

## 打印格式

`xihan pipeline dump` 与启动日志输出同一格式，一行一个中间件：

```text
ingress      XiHan.Framework.Kernel.Pipeline.RequestNormalizationMiddleware
observe      XiHan.Framework.Kernel.Pipeline.ObserveMiddleware
...
handle       XiHan.Framework.Kernel.Pipeline.OperationDispatchMiddleware
egress       XiHan.Framework.Protocols.Http.ProblemDetailsMiddleware
```

## .NET 形态

```csharp
public interface IPipelineMiddleware
{
    ValueTask InvokeAsync(InvocationContext context, PipelineDelegate next);
}

public delegate ValueTask PipelineDelegate(InvocationContext context);

public enum Stage { Ingress, Observe, Resilience, Authenticate, Tenant, Authorize, Validate, Idempotency, Transaction, Handle, Egress }
public enum Placement { Beginning, End }

public sealed class PipelineBuilder
{
    public PipelineBuilder Use<TMiddleware>() where TMiddleware : IPipelineMiddleware;
    public PipelineBuilder UseAt<TMiddleware>(Stage stage, Placement placement = Placement.End) where TMiddleware : IPipelineMiddleware;
    public PipelineBuilder Before<TMiddleware, TOther>() where TMiddleware : IPipelineMiddleware where TOther : IPipelineMiddleware;
    public PipelineBuilder After<TMiddleware, TOther>() where TMiddleware : IPipelineMiddleware where TOther : IPipelineMiddleware;
    public PipelineDelegate Build(IServiceProvider services);
    public IReadOnlyList<(Stage Stage, Type Middleware)> Describe();
}
```

管道的第二形态（阶段内并行分支）不在本 RFC 范围。

## 一致性向量

`spec/conformance/vectors/pipeline/*.json`：给定贡献集合的期望顺序、成环用例、未知阶段用例。
