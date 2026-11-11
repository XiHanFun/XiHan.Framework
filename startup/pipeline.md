来源：https://framework.docs.xihanfun.com/startup/pipeline

# 执行管道

这一页讲每次调用经过的十一个固定阶段、各阶段由谁提供中间件、中间件的写法与登记方式、同一阶段内的排序规则、应用构建之后成组改动管道的办法，以及怎样打印最终顺序。读完这页，你能在认证、租户、授权、事务等环节前后插入自己的逻辑，并确定它一定排在你期望的位置。前置知识是[依赖注入](/startup/dependency-injection)。

## 为什么需要

认证、限流、校验、事务这些横切逻辑如果靠动态代理拦截器或 AOP 特性实现，顺序由反射与注册细节决定，Native AOT 下还要额外的生成器支撑；靠 ASP.NET Core 中间件实现，又只对 HTTP 有效，gRPC、消息、作业各走一套。框架把横切逻辑统一成一条与协议无关的管道：阶段的名字与顺序由规范规定，不可增删；每个子系统把自己的中间件挂到对应阶段；同一份管道服务于 HTTP、gRPC、MCP、命令行、消息与作业。最终顺序可打印、可复现。

## 十一个阶段

阶段的名字与顺序由 XiHan 规范规定（RFC-0004），定义在原语层的 `PipelineStage` 枚举里，不能增删或调换：

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
5. 管道返回后，宿主读取 `context.FindOutcome()`，把 `Result` 与 `Error` 翻译回协议：HTTP 为 RFC 9457 Problem Details，gRPC 为状态码与 `xihan-error-code` 尾部元数据，命令行为退出码与 stderr。

`ObserveMiddleware` 为每次调用开启名为 `xihan.invoke` 的 Activity，记录 `xihan.invocations` 计数与 `xihan.invocation.duration` 耗时（毫秒），并把未处理异常转成 `KERNEL_UNEXPECTED_001` 结果；取消不在这里吞掉，由 `DeadlineMiddleware` 区分超时（`KERNEL_TIMEOUT_001`）与外部取消（`KERNEL_CANCELLED_001`）。采样规则见[日志与可观测性](/common/observability)。

## 最小示例

一个「维护模式」中间件：开关打开时拒绝所有调用，宿主把它翻译成 503。

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

在特性的 `Configure` 里登记，阶段与 `Placement` 二选一地放在阶段开头或末尾（缺省末尾）：

```csharp
public void Configure(FeatureContext context)
{
    context.Services.AddSingleton(new MaintenanceOptions { ReadOnly = true });
    // 归属本特性的清单 ID，内核同时以单例注册中间件类型
    context.AddMiddleware<MaintenanceModeMiddleware>(PipelineStage.Resilience, Placement.End);
}
```

把这个特性加进任一宿主，启动日志的 `resilience` 阶段末尾多出一行 `XiHan.Framework...MaintenanceModeMiddleware` 所在的类型全名；任何请求都得到：

```bash
curl -i -X POST localhost:5000/orders -H "Content-Type: application/json" -d '{"name":"widget","amount":1}'
```

```text
HTTP/1.1 503 Service Unavailable
Content-Type: application/problem+json

{"type":"urn:xihan:error:SHOP_UNAVAILABLE_001","title":"UNAVAILABLE","status":503,
 "detail":"...","instance":"/orders","code":"SHOP_UNAVAILABLE_001","traceId":"..."}
```

`detail` 的文案来自消息键 `shop.maintenance.read_only`，放在应用消息目录的 `Localization/<区域>.json` 里，见[全球化和本地化](/common/localization)。

写中间件的规则：

- 中间件在每次调用时从 `context.Services` 解析，不缓存调用级状态；需要作用域服务时在 `InvokeAsync` 里从 `context.Services` 取。
- 失败用 `Error` 表达并写进上下文，不抛异常；错误码按 `<域>_<KIND>_<NNN>` 文法，文案以消息键引用，见[结果与错误处理](/web/errors)。
- `nextStep` 之后的代码在下游全部完成后执行，可以读 `context.FindOutcome()` 观察结果，但不要改写别的阶段已经写入的结果。
- 调用上下文的安全信息（`context.GetSecurity()`）在 Authenticate 与 Tenant 阶段之后才有值，之前读到的是匿名上下文。

## 登记中间件

应用也可以不经特性直接登记，归属缺省为 `app`。按类型登记的中间件与特性登记的一样，构建应用时内核以单例补登记进容器；应用自己注册过就沿用它的注册：

```csharp
builder.Services.AddSingleton(new MaintenanceOptions { ReadOnly = false });
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .ConfigurePipeline(pipeline => pipeline.UseAt<MaintenanceModeMiddleware>(PipelineStage.Resilience)));
```

`PipelineBuilder` 的登记方法：

| 方法 | 用途 |
| --- | --- |
| `UseAt<T>(stage, placement, owner)` | 按类型登记到指定阶段，`owner` 缺省 `"app"` |
| `Use<T>()` | 不指定阶段，归入 `Transaction` 阶段末尾 |
| `UseInstance<T>(instance, stage, placement, owner)` | 登记一个已创建的实例，不进容器 |
| `UseNamedInstance(name, instance, stage, placement, owner)` | 同一类型登记多个实例，按名字区分，打印为 `类型名[名字]` |
| `Before<T, TOther>()`、`After<T, TOther>()` | 同阶段内的次序约束 |
| `Before(名字, 目标)`、`After(名字, 目标)` | 按名字的次序约束：名字是 `UseNamedInstance` 的实例名，或只登记一次的中间件类型全名 |
| `UseOwned(owner, configure)` | 一次登记同一归属的一组中间件与它们的约束，全进或全不进、立即核对；应用构建之后改管道只能用它 |
| `RemoveOwnedBy(owner)` | 按归属撤回登记的全部中间件，返回个数；会留下悬空约束时什么都不撤、报 `KERNEL_PIPELINE_003` |

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

同一类型默认只能登记一次，重复登记报 `KERNEL_PIPELINE_003`；需要多个实例时用 `UseNamedInstance`，此时不能再用按类型的 `Before` / `After` 约束它，改用按名字的 `Before(名字, 目标)` / `After(名字, 目标)`：约束主体在声明时须已登记，目标在排序时解析，未登记、同一个名字对应多个中间件或跨阶段同样报 `KERNEL_PIPELINE_003`。Wasm 插件清单里的 `before` / `after` 就是这样落到管道上的，见 [Wasm 插件](/plugins/wasm)。

## 运行期改动

应用构建之后，管道可以按归属成组地加进或撤出中间件（RFC-0004「运行期改动」）。Wasm 插件的运行期加载与卸载就是这样把它贡献的中间件接进、撤出管道的；应用一般用不到这两个方法。

```csharp
// pipeline 是装配时拿到的 PipelineBuilder（FeatureContext.Pipeline 或 AppBuilder.Pipeline），应用构建之后仍可以成组改动
// 一组中间件归属同一个清单 ID：组内只能登记这个归属的中间件，约束只能挂在组内的中间件上（目标可以在组外）
pipeline.UseOwned("xihan.plugins.audit", group => group
    .UseNamedInstance("xihan.plugins.audit:trail", new AuditTrailMiddleware(), PipelineStage.Observe, Placement.End, owner: "xihan.plugins.audit")
    .After("xihan.plugins.audit:trail", typeof(ObserveMiddleware).FullName!));

// 撤回同一归属的全部中间件，返回撤回的个数
var removed = pipeline.RemoveOwnedBy("xihan.plugins.audit");
```

- 全进或全不进：回调返回后、撤回之后立即排一次整条管道，约束目标未登记、有歧义、跨阶段（`KERNEL_PIPELINE_003`）或成环（`KERNEL_PIPELINE_001`）就整体还原、抛出，管道不变。
- 撤回不留悬空约束：别的归属的中间件以约束引用了要撤回的中间件时，`RemoveOwnedBy` 什么都不撤、报 `KERNEL_PIPELINE_003`，先撤回引用方。
- 应用构建之后，`Use*`、`Before` / `After` 单独调用报 `KERNEL_PIPELINE_003`（它们不立即核对，构建之后这样改会让下一次调用才失败）；组内也只能按实例登记，按类型登记的进不了已经建好的容器。
- 管道按版本构建：每次改动版本前进，改动后的第一次调用重建委托。每次调用在进入管道时取一次委托，已经在执行的调用按进入时的管道走完，改动返回之后开始的调用一定经过改动后的管道。
- 同一归属撤回后再成组登记同样的中间件，按排序规则回到原来的位置。

## 打印与检查

| 方式 | 输出 |
| --- | --- |
| 启动日志（Information 级别） | `Pipeline:` 后一行一个中间件：阶段名与类型全名 |
| 命令行宿主 `pipeline` 命令 | 与启动日志相同 |
| `App.DescribePipeline()`、`PipelineBuilder.Dump()` | 同上，返回字符串 |
| `PipelineBuilder.Describe()` | `MiddlewareDescriptor` 列表：类型、阶段、`Placement`、归属、名字与约束 |

`XiHan:Kernel:UseBuiltInMiddleware=false` 时内核不登记任何内置中间件，适用于完全自定义 Observe、Resilience 与 Validate 的宿主；同时 `XiHan:Kernel:Resilience` 配置节不再读取。

## 绕过管道的端点

ASP.NET Core 宿主里，`app.UseXiHan()` 之后映射的最小 API 端点在 Handle 阶段内执行，同样经过认证、租户、授权与事务。探针端点（`MapXiHanHealth`）与标记为绕过管道的端点（`PipelineBypassMetadata`）不经过管道；这类端点需要认证时使用 ASP.NET Core 方案 `XiHan`，见[端点里使用框架](/web/endpoints)与[认证](/security/authentication)。

## 常见问题

### 我想在授权之后、事务之前做一件事，挂到哪个阶段

挂到 `Validate` 或 `Idempotency` 阶段，用 `Placement` 决定在阶段内的开头或末尾。阶段之间的顺序是固定的，所以挂在 `Authorize` 之后的任何阶段都保证能读到已判定的主体与租户。

### 中间件抛了异常会怎样

`ObserveMiddleware` 把未处理异常转成 `KERNEL_UNEXPECTED_001`，调用方得到 500（HTTP）或退出码 1（命令行），异常本身记进日志并带 trace-id。但这是兜底：可恢复的失败应当用 `Error` 写进上下文，让调用方拿到有意义的错误码。

### 能不能只对某些操作生效

管道对每次调用都执行，中间件自己决定是否放行：读 `context` 里的操作描述或 `FindOrigin()` 判断来源与目标，不相关的调用直接 `await nextStep(context)`。框架不提供按操作挂中间件的注册方式，这样做保证顺序只由阶段决定。

### 为什么我的中间件排在别人之前，却读不到对方写的值

归属字典序不是依赖。用 `After<我的, 对方的>()` 声明次序，或者把它挂到更靠后的阶段。约束只能在同一阶段内生效，跨阶段的次序本来就由阶段保证。

### 怎样在测试里验证中间件的短路行为

用 `XiHan.Framework.Testing` 的 `TestHost.StartAsync` 装配只含你的特性的应用，再 `InvokeAsync` 传一个标记「是否到达」的处理器，断言结果与标记，见[单元与集成测试](/testing/basics)。

## 相关包与下一步

- 包参考：[Kernel](/packages/kernel)、[Primitives](/packages/primitives)
- 下一页：[调用上下文](/startup/invocation-context)，看中间件与处理器共享的那个对象里有什么
- 各阶段的细节：[数据校验](/web/validation)（Validate）、[弹性与限流](/common/resilience)（Resilience）、[工作单元与事务](/persistence/unit-of-work)（Transaction）、[分布式锁与幂等](/caching/locks)（Idempotency）、[审计](/common/auditing)（Observe）
