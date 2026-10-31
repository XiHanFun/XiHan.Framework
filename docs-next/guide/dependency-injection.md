# 依赖注入

本章说明 5.0 如何使用 .NET 原生的依赖注入：服务在哪里注册、框架登记的类型是什么生命周期、调用作用域如何划分，以及调用链深处如何取得当前调用的上下文。适用于所有写特性、操作与中间件的开发者。

## 原生容器，显式注册

5.0 直接使用 `Microsoft.Extensions.DependencyInjection`，不包装容器，不扫描程序集，没有按接口或特性自动注册的约定，也没有属性注入与拦截器。服务在两个位置注册：

| 位置 | 适用 |
| --- | --- |
| 特性的 `Configure(FeatureContext context)` 里的 `context.Services` | 属于某个能力包的服务：仓储、处理器、领域服务、选项 |
| 应用入口的 `builder.Services` | 应用级的宿主设置：消息目录、JSON 选项、ASP.NET Core 服务 |

`AddXiHan` 的回调结束时内核立即按能力图谱的拓扑顺序调用各特性的 `Configure`，因此被依赖的特性先完成注册。

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace Shop.Pricing;

public sealed class PricingFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.shop.pricing",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(PricingFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        context.Services.AddSingleton<PriceList>();
        context.Services.AddScoped<DiscountCalculator>();
    }
}

public sealed class PriceList;

public sealed class DiscountCalculator(PriceList prices, TimeProvider clock);
```

源生成器产出的登记代码同样要显式调用：`Kernel.Generators` 生成 `context.AddGeneratedOperations()`，`Persistence.Generators` 生成 `context.Services.AddGeneratedRepositories()`。生成器只省去手写登记，不改变「显式调用才生效」的规则。

替换某个能力的实现不靠覆盖容器注册，而是加入另一个 Provider 特性，由能力图谱选择，见[能力包与清单](./modularity#选择-provider)。

## 框架登记的生命周期

| 类型 | 生命周期 | 登记方 |
| --- | --- | --- |
| 操作（`IOperation<TRequest, TResponse>` 的实现） | 作用域 | `FeatureContext.AddOperation`，每次调用解析一个新实例 |
| 管道中间件 | 单例 | `FeatureContext.AddMiddleware<T>` 以 `TryAddSingleton` 登记 |
| `IInvocationAccessor` | 作用域 | 内核 |
| `App`、`ResolvedGraph`、`KernelOptions`、`OperationRegistry`、`PermissionRegistry` | 单例 | 内核 |
| `TimeProvider` | 单例 | 内核在宿主没有注册时补 `TimeProvider.System` |
| `ILoggerFactory`、`IAuditSink` | 单例 | 内核在宿主没有注册时补空实现 |

中间件是单例，构造函数只能依赖单例服务；需要作用域服务时，在 `InvokeAsync` 里从 `context.Services`（本次调用的作用域服务提供器）取得：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace Shop.Pricing;

public sealed class PriceAuditMiddleware(TimeProvider clock) : IPipelineMiddleware
{
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        // 作用域服务按调用解析，不进构造函数
        var calculator = context.Services.GetRequiredService<DiscountCalculator>();
        var startedAt = clock.GetUtcNow();
        await nextStep(context);
    }
}
```

应用自己用 `AppBuilder.ConfigurePipeline` 或 `AppBuilder.Pipeline.UseAt<T>` 登记中间件时，内核不会替它注册类型，要同时在容器里注册（通常是 `AddSingleton<T>()`），否则第一次调用时解析失败。用 `FeatureContext.AddMiddleware<T>` 则由内核一并注册。

## 调用作用域

每次调用对应一个服务作用域：ASP.NET Core 宿主沿用请求作用域，命令行宿主、消息派发、作业派发与测试基座为每次调用创建一个作用域。`InvocationContext.Services` 就是这个作用域的服务提供器，同一次调用里的操作、仓储、工作单元共享它。

在调用之外运行的代码（`IHostedFeature.StartAsync`、后台循环、单例服务里的定时任务）没有调用作用域，需要作用域服务时自己创建：

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Shop.Pricing;

public sealed class PriceListRefresher(IServiceScopeFactory scopes)
{
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var calculator = scope.ServiceProvider.GetRequiredService<DiscountCalculator>();
        await Task.CompletedTask;
    }
}
```

在这样的作用域里使用仓储，还要自己开启工作单元，写法见[工作单元与事务](./uow)。

## 取得当前调用的上下文

操作与中间件通过参数拿到 `InvocationContext`。调用链深处不方便传参的服务（领域服务、仓储装饰器、当前用户的包装）注入作用域服务 `IInvocationAccessor`：

```csharp
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;

namespace Shop.Pricing;

public sealed class CurrentShopper(IInvocationAccessor accessor)
{
    // 不在调用内（后台任务、作用域刚创建、调用已结束）时为无值
    public string? Id => accessor.Current.TryGetValue(out var context)
        ? context.GetSecurity().Principal.Id
        : null;

    public string? TenantId => accessor.Current.TryGetValue(out var context)
        ? context.GetSecurity().Tenant?.Id
        : null;
}
```

内核在调用进入管道前设置 `Current`，离开时恢复上一层；同一作用域内的嵌套调用各自看到自己的上下文。`CurrentShopper` 要注册为作用域服务（`AddScoped`）。访问器不是传播载体：跨进程传递租户、主体等信息由调用上下文的 Baggage 负责。

## 时间与测试替身

需要当前时间的代码注入 `TimeProvider`，不直接读 `DateTimeOffset.UtcNow`。截止时间、缓存过期、调度与租约都基于容器里的 `TimeProvider`，测试里注册 `Microsoft.Extensions.TimeProvider.Testing` 的 `FakeTimeProvider` 就能控制时间推进。测试宿主与特性桩见[测试与一致性套件](./testing)。

## 相关

- [能力包与清单](./modularity)
- [执行管道](./pipeline)
- [工作单元与事务](./uow)
- 规范：RFC-0003《调用上下文与传播》
