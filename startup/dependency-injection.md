来源：https://framework.docs.xihanfun.com/startup/dependency-injection

# 依赖注入

这一页讲框架怎样使用 .NET 原生的依赖注入：服务在哪两个位置注册、框架自己登记的类型是什么生命周期、每次调用对应的服务作用域从哪里来，以及在调用之外运行的代码怎样取得作用域服务。读完这页，你能为一个能力包正确地注册单例、作用域服务与中间件，不再在根容器里误解析作用域服务。前置知识是[特性与清单](/startup/features)。

## 为什么需要

很多框架为了「省事」包装容器：按接口名自动注册、按特性标记扫描、靠属性注入与拦截器实现横切逻辑。代价是注册关系不可见、启动慢、Native AOT 下难以裁剪，排查「这个实现是谁注册的」要翻约定文档。框架只用 `Microsoft.Extensions.DependencyInjection`，所有注册都是显式写出的一行代码；换实现靠能力图谱选 Provider，横切逻辑走管道中间件。容器只做容器该做的事。

## 原生容器，显式注册

框架不包装容器，不扫描程序集，没有按接口或特性自动注册的约定，也没有属性注入与拦截器。服务在两个位置注册：

| 位置 | 适用 |
| --- | --- |
| 特性的 `Configure(FeatureContext context)` 里的 `context.Services` | 属于某个能力包的服务：仓储、处理器、领域服务、选项 |
| 应用入口的 `builder.Services` | 应用级的宿主设置：消息目录、JSON 选项、ASP.NET Core 服务 |

两者是同一个 `IServiceCollection`。`AddXiHan` 的回调结束时内核立即按能力图谱的拓扑顺序调用各特性的 `Configure`，因此被依赖的特性先完成注册；`builder.Services` 上的注册在 `AddXiHan` 之前或之后写都可以，容器在宿主 `Build()` 时才构建。

源生成器产出的登记代码同样要显式调用：`Kernel.Generators` 生成 `context.AddGeneratedOperations()`，`Persistence.Generators` 生成 `context.Services.AddGeneratedRepositories()`。生成器只省去手写登记，不改变「显式调用才生效」的规则。

替换某个能力的实现不靠覆盖容器注册，而是加入另一个 Provider 特性，由能力图谱选择，见[能力图谱与 Provider](/startup/capabilities)。

## 框架登记的生命周期

| 类型 | 生命周期 | 登记方 |
| --- | --- | --- |
| 操作（`IOperation<TRequest, TResponse>` 的实现） | 作用域 | `FeatureContext.AddOperation`，每次调用解析一个新实例 |
| 管道中间件 | 单例 | `FeatureContext.AddMiddleware<T>` 以 `TryAddSingleton` 登记 |
| `IInvocationAccessor` | 作用域 | 内核 |
| `App`、`ResolvedGraph`、`KernelOptions`、`OperationRegistry`、`PermissionRegistry` | 单例 | 内核 |
| `TimeProvider` | 单例 | 内核在宿主没有注册时补 `TimeProvider.System` |
| `ILoggerFactory`、`IAuditSink` | 单例 | 内核在宿主没有注册时补空实现 |

操作是作用域服务，构造函数可以直接注入仓储、工作单元、`IMessagePublisher` 这类作用域服务；中间件是单例，构造函数只能依赖单例服务，作用域服务要在 `InvokeAsync` 里取，见下文。

## 最小示例

一个定价能力包：单例的价目表、作用域的折扣计算器，以及一个在构造函数里注入计算器的操作。

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
        // 价目表全局一份；折扣计算器每次调用一份，可以安全地依赖其他作用域服务
        context.Services.AddSingleton<PriceList>();
        context.Services.AddScoped<DiscountCalculator>();
        context.AddGeneratedOperations();
    }
}

public sealed class PriceList
{
    public decimal UnitPrice(string sku) => 10m;
}

public sealed class DiscountCalculator(PriceList prices, TimeProvider clock)
{
    // 周末九折：时间来自容器里的 TimeProvider，测试可替换
    public decimal Quote(string sku, int quantity)
    {
        var total = prices.UnitPrice(sku) * quantity;
        var day = clock.GetUtcNow().DayOfWeek;
        return day is DayOfWeek.Saturday or DayOfWeek.Sunday ? total * 0.9m : total;
    }
}
```

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace Shop.Pricing;

[Operation(Id, Stability = Stability.Preview, Expose = Expose.Http)]
public sealed class CreateQuote(DiscountCalculator calculator) : IOperation<QuoteRequest, QuoteResponse>
{
    public const string Id = "quotes.create";

    public ValueTask<Result<QuoteResponse>> HandleAsync(QuoteRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(new QuoteResponse(request.Sku, calculator.Quote(request.Sku, request.Quantity))));
}

public sealed record QuoteRequest(string Sku, int Quantity);

public sealed record QuoteResponse(string Sku, decimal Total);
```

请求与响应类型登记进项目的 `JsonSerializerContext`（写法与[特性与清单](/startup/features)的示例相同），`Program.cs` 里 `UseFeature<PricingFeature>()` 并 `MapXiHanOperations()`。运行后：

```bash
curl -s -X POST localhost:5000/quotes -H "Content-Type: application/json" -d '{"sku":"widget","quantity":3}'
```

```json
{"sku":"widget","total":30}
```

每次请求解析一个新的 `CreateQuote` 与 `DiscountCalculator`，`PriceList` 全程只有一个实例。

## 中间件只能依赖单例

中间件以单例登记，构造函数只能依赖单例服务；需要作用域服务时，在 `InvokeAsync` 里从 `context.Services`（本次调用的作用域服务提供器）取得：

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

应用自己用 `AppBuilder.ConfigurePipeline` 或 `AppBuilder.Pipeline.UseAt<T>` 登记中间件时，内核会在构建应用时以单例补登记中间件类型；应用已经注册过（比如按作用域注册）就沿用它的注册。用 `FeatureContext.AddMiddleware<T>` 则由内核一并注册。中间件的写法与阶段见[执行管道](/startup/pipeline)。

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

在这样的作用域里使用仓储，还要自己开启工作单元，写法见[工作单元与事务](/persistence/unit-of-work)；要让这段代码经过完整管道（认证、租户、事务都生效），改为建调用上下文并调用 `App.InvokeAsync`，见[调用上下文](/startup/invocation-context)。

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

内核在调用进入管道前设置 `Current`，离开时恢复上一层；同一作用域内的嵌套调用各自看到自己的上下文。`CurrentShopper` 要注册为作用域服务（`AddScoped`）。访问器不是传播载体：跨进程传递租户、主体等信息由调用上下文的 Baggage 负责，见[调用上下文](/startup/invocation-context)。

## 时间与测试替身

需要当前时间的代码注入 `TimeProvider`，不直接读 `DateTimeOffset.UtcNow`。截止时间、缓存过期、调度与租约都基于容器里的 `TimeProvider`，测试里注册 `Microsoft.Extensions.TimeProvider.Testing` 的 `FakeTimeProvider` 就能控制时间推进；上面的 `DiscountCalculator` 因此可以在测试里把时间拨到周末验证折扣。测试宿主与特性桩见[单元与集成测试](/testing/basics)。

## 常见问题

### 我习惯用 Autofac 或别的容器，能换吗

框架只依赖 `IServiceCollection` 与 `IServiceProvider` 抽象，宿主换容器工厂在理论上可行，但框架不测试、不保证第三方容器的作用域与释放语义。建议保持原生容器；原生容器缺少的能力（如装饰器）用显式的工厂委托或管道中间件实现。

### 为什么我的服务在 `StartAsync` 里解析失败

`StartAsync` 收到的是根容器。作用域服务在根容器里解析，开了作用域校验时直接抛出，没开时会变成事实上的单例并在应用停止前不释放。按「调用作用域」一节自建作用域，或把该服务改成单例。

### 同一个接口注册了两次，哪一个生效

与 .NET 容器一致：解析单个服务取最后注册的，解析 `IEnumerable<T>` 得到全部。框架自己的补登记用 `TryAdd*`，不会覆盖应用先注册的实现；但框架不建议用「后注册覆盖」替换能力实现，那是 Provider 的工作。

### 操作里能注入 `HttpContext` 吗

不建议。操作是协议无关的契约，同一份代码也会从 gRPC、MCP、命令行、消息被调用。请求信息已经翻译进 `InvocationContext`（追踪、截止时间、Baggage、调用来源、安全上下文），从参数里读即可；确实只在 HTTP 下才有的信息放在端点里处理，见[端点里使用框架](/web/endpoints)。

### 怎样在单元测试里拿到一个独立的 `InvocationContext`

`XiHan.Framework.Testing` 的 `TestInvocationContext.Create(...)` 给不需要宿主的单元测试一个独立的调用上下文；要走完整管道则用 `TestHost.StartAsync(...)` 装配并调用，见[单元与集成测试](/testing/basics)。

## 相关包与下一步

- 包参考：[Kernel](/packages/kernel)、[Primitives](/packages/primitives)、[Testing](/packages/testing)
- 下一页：[执行管道](/startup/pipeline)，看中间件怎样按阶段排序并取用作用域服务
- 延伸：[工作单元与事务](/persistence/unit-of-work)、[单元与集成测试](/testing/basics)
