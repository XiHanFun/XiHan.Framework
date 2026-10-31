# 执行管道

本章说明每次调用经过的十一个固定阶段、各阶段由谁提供中间件、中间件的写法与排序规则，以及如何打印最终顺序。适用于要在认证、租户、授权、事务等环节前后插入自己逻辑的开发者。

5.0 用管道中间件代替 4.x 的 Castle 拦截器与 AOP 特性。

完整 API 见 [Kernel 包](../packages/kernel)。

## 十一个阶段

阶段的名字与顺序由 XiHan Spec 规定（RFC-0004），定义在原语层的 `PipelineStage` 枚举里，不能增删或调换：

| 阶段 | 职责 | 提供中间件的包 |
| --- | --- | --- |
| `Ingress` | 协议消息归一化、构造调用上下文 | 协议适配器在进入管道前完成，框架不在本阶段登记中间件 |
| `Observe` | 追踪、指标、日志、审计 | 内核 `ObserveMiddleware`；`Auditing.*` 的审计中间件 |
| `Resilience` | 截止时间、熔断、限流、并发上限 | 内核 `DeadlineMiddleware`、`ResilienceMiddleware` |
| `Authenticate` | 解析主体 | `Security.*` 的 `AuthenticationMiddleware` |
| `Tenant` | 解析租户与隔离上下文 | `MultiTenancy.*` 的 `TenantResolutionMiddleware` |
| `Authorize` | 按权限或策略判定 | `Security.*` 的 `AuthorizationMiddleware` |
| `Validate` | 按操作的 JSON Schema 与 `IValidatableObject` 校验请求 | 内核 `ValidationMiddleware` |
| `Idempotency` | 按幂等键去重 | `Caching.*` 的 `IdempotencyMiddleware` |
| `Transaction` | 开启与提交工作单元，提交前派发领域事件 | `Persistence.*` 的 `UnitOfWorkMiddleware` |
| `Handle` | 执行操作或端点处理器 | 处理器是管道的终点，在本阶段的中间件之后执行 |
| `Egress` | 把结果与错误翻译回协议 | 宿主与协议适配器在管道返回后完成 |

Standard 组合加 HTTP 适配器时，启动日志打印的最终顺序是：

```text
observe      XiHan.Framework.Kernel.Diagnostics.ObserveMiddleware
resilience   XiHan.Framework.Kernel.Pipeline.DeadlineMiddleware
resilience   XiHan.Framework.Kernel.Resilience.ResilienceMiddleware
authenticate XiHan.Framework.Security.Abstractions.Authentication.AuthenticationMiddleware
tenant       XiHan.Framework.MultiTenancy.Abstractions.Resolution.TenantResolutionMiddleware
authorize    XiHan.Framework.Security.Abstractions.Authorization.AuthorizationMiddleware
validate     XiHan.Framework.Kernel.Validation.ValidationMiddleware
idempotency  XiHan.Framework.Caching.Abstractions.Idempotency.IdempotencyMiddleware
transaction  XiHan.Framework.Persistence.Abstractions.UnitOfWork.UnitOfWorkMiddleware
```

中间件属于哪个阶段只取决于登记时给的阶段，与 `UseFeature` 的书写顺序无关。没有装配某个子系统时，对应阶段就是空的：Minimal 组合只有 Observe、Resilience、Validate 三个阶段有中间件。

## 一次调用怎样走完管道

1. 协议适配器或派发器为本次调用创建服务作用域与 `InvocationContext`，写入截止时间、Trace、Baggage 与调用来源，操作调用还把操作描述与载荷放进上下文。
2. 调用经 `App.InvokeAsync` 进入管道。内核先设置 `IInvocationAccessor.Current`，并按 Baggage 里的区域安装消息作用域，让调用链里的 `key.L()` 取到调用方语言。
3. 中间件依次执行，每个中间件决定是否调用下一步。拒绝调用的中间件把失败结果写进上下文（`context.SetOutcome(error)`）并直接返回，后续阶段不再执行。
4. 处理器在所有中间件之内执行，结果写进上下文。
5. 管道返回后，宿主读取 `context.GetOutcome()`，把 `Result` 与 `Error` 翻译回协议：HTTP 为 RFC 9457 Problem Details，gRPC 为状态码与 `xihan-error-code` 尾部元数据，命令行为退出码与 stderr。

`ObserveMiddleware` 为每次调用开启名为 `xihan.invoke` 的 Activity，记录 `xihan.invocations` 计数与 `xihan.invocation.duration` 耗时（毫秒），并把未处理异常转成 `KERNEL_UNEXPECTED_001` 结果；取消不在这里吞掉，由 `DeadlineMiddleware` 区分超时（`KERNEL_TIMEOUT_001`）与外部取消（`KERNEL_CANCELLED_001`）。

## 写一个中间件

中间件实现 `IPipelineMiddleware`，只有一个方法：

```csharp
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace Shop.Maintenance;

public sealed class MaintenanceOptions
{
    public bool ReadOnly { get; init; }
}

public sealed class MaintenanceModeMiddleware(MaintenanceOptions options) : IPipelineMiddleware
{
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        if (options.ReadOnly)
        {
            // 写进结果后直接返回：后续阶段与处理器都不执行，宿主把它翻译成 503
            context.SetOutcome(Error.Unavailable("SHOP_UNAVAILABLE_001", ShopMessages.MaintenanceMode));
            return;
        }

        await nextStep(context);
    }
}

public static class ShopMessages
{
    public const string MaintenanceMode = "shop.maintenance.read_only";
}
```

规则：

- 中间件在每次调用时从 `context.Services` 解析，不缓存调用级状态；需要作用域服务时在 `InvokeAsync` 里从 `context.Services` 取。
- 失败用 `Error` 表达并写进上下文，不抛异常；错误码按 `<域>_<KIND>_<NNN>` 文法，文案以消息键引用，见[结果与错误](./errors)。
- `nextStep` 之后的代码在下游全部完成后执行，可以读 `context.GetOutcome()` 观察结果，但不要改写别的阶段已经写入的结果。
- 调用上下文的安全信息（`context.GetSecurity()`）在 Authenticate 与 Tenant 阶段之后才有值。

## 登记中间件

特性在 `Configure` 里登记，阶段与 `Placement` 二选一地放在阶段开头或末尾（缺省末尾）：

```csharp
public void Configure(FeatureContext context)
{
    context.Services.AddSingleton(new MaintenanceOptions { ReadOnly = false });
    // 归属本特性的清单 ID，内核同时以单例注册中间件类型
    context.AddMiddleware<MaintenanceModeMiddleware>(PipelineStage.Resilience, Placement.End);
}
```

应用也可以不经特性直接登记，此时要自己在容器里注册中间件类型：

```csharp
builder.Services.AddSingleton(new MaintenanceOptions { ReadOnly = false });
builder.Services.AddSingleton<MaintenanceModeMiddleware>();
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .ConfigurePipeline(pipeline => pipeline.UseAt<MaintenanceModeMiddleware>(PipelineStage.Resilience)));
```

`PipelineBuilder` 的登记方法：

| 方法 | 用途 |
| --- | --- |
| `UseAt<T>(stage, placement, owner)` | 按类型登记到指定阶段，`owner` 缺省 `"app"` |
| `Use<T>()` | 不指定阶段，归入 `Transaction` 阶段末尾 |
| `UseInstance<T>(instance, stage, placement, owner)` | 登记一个已创建的实例 |
| `UseNamedInstance(name, instance, stage, placement, owner)` | 同一类型登记多个实例，按名字区分，打印为 `类型名[名字]` |
| `Before<T, TOther>()` / `After<T, TOther>()` | 同阶段内的次序约束 |

清单里的 `Contributes.Pipeline` 用 `PipelineContribution` 声明中间件归属的阶段，供 `graph` 命令与插件宿主读取；真正生效的仍是 `Configure` 里的登记，两者应保持一致。

## 排序规则

同一阶段内的中间件按以下顺序排定，结果确定、可复现：

1. `Placement`：`Beginning` 在前，`End` 在后。内核的内置中间件都登记在各自阶段的开头。
2. 归属：按清单 ID 的字典序。
3. 登记顺序。
4. 最后应用 `Before` / `After` 约束做稳定拓扑排序；约束成环报 `KERNEL_PIPELINE_001`，约束引用了没有登记或不在同一阶段的中间件报 `KERNEL_PIPELINE_003`。

::: warning 跨包依赖必须写约束
归属字典序只保证结果可复现，不表达依赖：`xihan.basicapp.identity` 会排在 `xihan.security.jwt` 之前。一个中间件要用到另一个包中间件的产出（认证后的主体、解析后的租户、开启的工作单元）时，必须用 `After` / `Before` 声明，例如 `pipeline.After<SessionGateMiddleware, AuthenticationMiddleware>()`。内核不按清单的 `Requires` 推导中间件次序。
:::

同一类型默认只能登记一次，重复登记报 `KERNEL_PIPELINE_003`；需要多个实例时用 `UseNamedInstance`，此时不能再用按类型的 `Before` / `After` 约束它。

## 打印与检查

| 方式 | 输出 |
| --- | --- |
| 启动日志（Information 级别） | `Pipeline:` 后一行一个中间件：阶段名与类型全名 |
| 命令行宿主 `pipeline` 命令 | 与启动日志相同 |
| `App.DescribePipeline()` / `PipelineBuilder.Dump()` | 同上，返回字符串 |
| `PipelineBuilder.Describe()` | `MiddlewareDescriptor` 列表：类型、阶段、`Placement`、归属、名字与约束 |

`XiHan:Kernel:UseBuiltInMiddleware=false` 时内核不登记任何内置中间件，适用于完全自定义 Observe、Resilience 与 Validate 的宿主；同时 `XiHan:Kernel:Resilience` 配置节不再读取。

## 绕过管道的端点

ASP.NET Core 宿主里，`app.UseXiHan()` 之后映射的最小 API 端点在 Handle 阶段内执行，同样经过认证、租户、授权与事务。探针端点（`MapXiHanHealth`）与标记为绕过管道的端点不经过管道；这类端点需要认证时使用 ASP.NET Core 方案 `XiHan`，见[认证](./authentication)。

## 相关

- [操作契约](./operations)
- [数据校验](./validation)：Validate 阶段
- [弹性与限流](./resilience)：Resilience 阶段
- [工作单元与事务](./uow)：Transaction 阶段
- 规范：RFC-0004《管道阶段与排序》
