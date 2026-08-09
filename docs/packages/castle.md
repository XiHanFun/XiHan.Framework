# XiHan.Framework.Castle

> AOP 动态代理底座：集成 Castle DynamicProxy，把框架的异步拦截器链 `IXiHanInterceptor[]` 适配到 Castle 的同步 `IInterceptor`，在服务注册完成后自动为登记了拦截器的接口服务生成代理。

- **NuGet**：`XiHan.Framework.Castle`
- **模块类**：`XiHanCastleModule`
- **所在层**：基础设施层
- **关键依赖**：**Castle.Core**（`Castle.DynamicProxy`）；框架内部仅依赖 Core

## 概述

XiHan.Framework.Castle 是框架的 AOP（面向切面）实现层。拦截器的抽象契约（`IXiHanInterceptor`、`IXiHanMethodInvocation`、抽象基类 `XiHanInterceptor`）定义在 [Core](./core)，本包只提供把这套「框架自有、天然异步」的拦截模型落到 Castle DynamicProxy 上的具体实现。

它做两件事：一是在服务注册全部完成后（`PostConfigureServices` 阶段），遍历 DI 容器，为满足条件的接口服务用「代理版本」替换原注册；二是在代理运行时，用 `CastleInterceptorAdapter` 把框架的异步拦截器链适配为 Castle 要求的同步 `IInterceptor`，并正确处理 `void` / `Task` / `Task<T>` 三种返回形态。工作单元（Uow）、缓存（Caching）等模块的方法级切面能力，正是建立在它之上。

## 何时使用

- 需要给服务方法透明地织入横切逻辑（事务、缓存、审计等），而不侵入业务代码。
- 你的模块要注册自定义拦截器，让被拦截的接口服务在启动时自动被代理。
- 依赖 Uow / Caching 等基于拦截器的能力时，需要引入这个 AOP 前置包（它们的模块通常已 `[DependsOn]` 它）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Castle
```

```csharp
[DependsOn(typeof(XiHanCastleModule))]
public class MyModule : XiHanModule { }
```

模块在 `PostConfigureServices` 阶段调用 `services.AddCastleDynamicProxy()`。选在 `PostConfigureServices` 是关键：此时所有模块的 `ConfigureServices` 都已跑完，容器里的服务描述符与各模块登记的拦截器回调都齐了，代理化才能一次性覆盖全部目标服务。

## 工作原理

拦截器的「登记」与「织入」是分离的两步：

1. **登记（各业务模块做）**：模块在 `ConfigureServices` 里调用 `services.OnRegistered(callback)` 注册一个回调。该回调对每个待检查的服务收到一个 `IOnServiceRegistredContext`（接口暴露 `ImplementationType`、可写的 `Interceptors` 列表；本包内部构造的具体实现类 `OnServiceRegistredContext` 还额外带一个 `ServiceType` 属性，但回调签名只声明为接口类型），按需 `context.Interceptors.TryAdd<TInterceptor>()`。例如 Uow 模块：`services.OnRegistered(UnitOfWorkInterceptorRegistrar.RegisterIfNeeded)`，其内部对带 `[UnitOfWork]` 的服务 `TryAdd<UnitOfWorkInterceptor>()`。
2. **织入（本包在 `AddCastleDynamicProxy()` 里做）**：
   - 若 `services.IsClassInterceptorsDisabled()` 为真，或没有任何登记回调，直接返回（不代理）。
   - 遍历所有服务描述符，筛选「`ServiceType` 是接口 + 有 `ImplementationType` + 不在 `DynamicProxyIgnoreTypes` 忽略名单」的项，对每项构造 `OnServiceRegistredContext` 并回放全部登记回调；只要最终 `Interceptors.Count > 0`，就记入待代理集合。
   - 对每个待代理项，用代理版描述符替换原描述符：解析器里先创建原始实例（走 `ImplementationInstance` / `ImplementationFactory` / `ActivatorUtilities.CreateInstance` 三种来源之一），解析出拦截器实例数组，包进 `CastleInterceptorAdapter`，再用共享的 `ProxyGenerator.CreateInterfaceProxyWithTarget(serviceType, target, adapter)` 生成接口代理；生命周期沿用原描述符的 `Lifetime`。

**运行时链式执行**（`CastleInterceptorAdapter.Intercept`）：

- 捕获 `IInvocation.CaptureProceedInfo()`，包成 `CastleXiHanMethodInvocation`。
- 按方法返回类型分派：`void` → 同步执行拦截链（内部对异步链 `GetAwaiter().GetResult()`）；`Task` → 返回执行链的 `Task`；`Task<T>` → 反射 `MakeGenericMethod` 走 `HandleAsyncWithResult<TResult>` 拿回结果。
- 拦截链按登记顺序递归执行：第 `i` 个拦截器拿到一个 `ChainedMethodInvocation`（其 `ProceedAsync()` 触发第 `i+1` 个），到链尾则调用 `CastleXiHanMethodInvocation.ProceedAsync()` → `IInvocationProceedInfo.Invoke()` 调真实目标方法，若返回 `Task` 则 `await`。

## 核心能力

- **Castle DynamicProxy 集成**：基于 `Castle.Core` 生成接口代理（`CreateInterfaceProxyWithTarget`），全局复用一个 `ProxyGenerator` 实例。
- **异步拦截器链适配**：`CastleInterceptorAdapter` 把框架的 `IXiHanInterceptor[]` 链适配为 Castle 同步 `IInterceptor`，正确处理 `void` / `Task` / `Task<T>`（含泛型结果）。
- **方法调用上下文包装**：`CastleXiHanMethodInvocation` 把 Castle 的 `IInvocation` 适配为框架的 `IXiHanMethodInvocation`（参数数组、参数名字典、泛型参数、目标对象、`MethodInfo`、可读写返回值、`ProceedAsync`）。
- **DI 自动代理化**：`AddCastleDynamicProxy()` 在注册阶段自动识别需代理的接口服务并原地替换描述符，无需逐个手工配置；支持全局开关与忽略名单。
- **拦截器登记回调机制**：各模块通过 `services.OnRegistered(...)` + `context.Interceptors.TryAdd<T>()` 把拦截器加入拦截链，按登记顺序执行。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanCastleModule` | 模块类，`PostConfigureServices` 阶段调用 `AddCastleDynamicProxy()` 启动代理化 |
| `ServiceCollectionCastleExtensions` | DI 扩展，含 `IServiceCollection AddCastleDynamicProxy(this IServiceCollection services)` |
| `CastleInterceptorAdapter` | 实现 Castle `IInterceptor`，构造参数 `IXiHanInterceptor[]`，适配异步拦截链 |
| `CastleXiHanMethodInvocation` | 实现 `IXiHanMethodInvocation`，适配 Castle 的 `IInvocation` + `IInvocationProceedInfo` |

> 拦截器抽象本身定义在 [XiHan.Framework.Core](./core)：`IXiHanInterceptor`（`Task InterceptAsync(IXiHanMethodInvocation)`）、`IXiHanMethodInvocation`、抽象基类 `XiHanInterceptor`；以及登记侧的 `IOnServiceRegistredContext`、`OnRegistered(...)`、`DynamicProxyIgnoreTypes`、`IsClassInterceptorsDisabled()`。本包只提供 Castle 实现，不重复定义这些抽象。

## 使用示例

写一个自定义拦截器（继承 Core 的抽象基类），在方法前后打点：

```csharp
public class TimingInterceptor : XiHanInterceptor
{
    private readonly ILogger<TimingInterceptor> _logger;

    public TimingInterceptor(ILogger<TimingInterceptor> logger) => _logger = logger;

    public override async Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        var sw = Stopwatch.StartNew();
        await invocation.ProceedAsync();   // 继续链 / 调真实方法
        sw.Stop();
        _logger.LogInformation("{Method} 耗时 {Ms}ms", invocation.Method.Name, sw.ElapsedMilliseconds);
    }
}
```

在模块里登记：注册拦截器本身，并用 `OnRegistered` 决定哪些服务被它拦截：

```csharp
[DependsOn(typeof(XiHanCastleModule))]
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 拦截器需可被 DI 解析（适配器运行时用 GetRequiredService 取实例）
        services.AddTransient<TimingInterceptor>();

        // 对满足条件的接口服务登记该拦截器
        services.OnRegistered(ctx =>
        {
            if (typeof(IMyService).IsAssignableFrom(ctx.ImplementationType))
            {
                ctx.Interceptors.TryAdd<TimingInterceptor>();
            }
        });
    }
}
```

`XiHanCastleModule` 的 `PostConfigureServices` 会在最后把 `IMyService` 的注册替换成带 `TimingInterceptor` 的代理，无需其它手工代码。

## 扩展点 / 自定义

- **自定义拦截器**：继承 `XiHanInterceptor`（或实现 `IXiHanInterceptor`），实现 `InterceptAsync`；务必调用 `invocation.ProceedAsync()` 才会继续后续拦截器与真实方法。
- **控制拦截范围**：全靠 `OnRegistered` 回调里的判断（按接口、`ImplementationType`、特性等）决定 `TryAdd` 哪个拦截器；顺序即执行顺序。
- **忽略某些实现**：把类型加入 Core 的 `DynamicProxyIgnoreTypes`，代理化会跳过它。
- **全局关闭类拦截**：`services.DisableClassInterceptors()`（Core）会让 `AddCastleDynamicProxy()` 直接短路。

## 注意事项与最佳实践

- **只代理接口服务**：仅对「`ServiceType` 是接口且有具体 `ImplementationType`」的注册生效；用 `ImplementationInstance`/工厂注册的仍会被包装原实例，但纯类型（非接口）服务不被代理。
- **拦截器必须可解析**：适配器在运行时用 `GetRequiredService(interceptorType)` 取拦截器，所以拦截器类型必须已注册到 DI，否则解析代理实例时抛异常。
- **时机固定在 `PostConfigureServices`**：代理化是一次性、在所有 `ConfigureServices` 之后完成；运行时再往容器加服务不会被自动代理。
- **异步优先**：拦截器天然是 `Task` 语义；对返回 `void` 的方法，适配器会同步阻塞等待异步链完成（`GetAwaiter().GetResult()`），横切逻辑里避免长阻塞/死锁风险。
- **顺序敏感**：多个拦截器按 `OnRegistered` 登记（`TryAdd`）顺序形成链，前者先进后出地包裹后者。

## 依赖模块

- 内部依赖：仅 [XiHan.Framework.Core](./core)（提供拦截器抽象、`IOnServiceRegistredContext`、`OnRegistered`、忽略名单等契约）。
- 第三方核心：**Castle.Core**（`Castle.DynamicProxy`）。

## 相关模块

- [XiHan.Framework.Uow](./uow) — `UnitOfWorkInterceptor` 依赖本包织入 `[UnitOfWork]` 事务切面（`XiHanUowModule` 用 `OnRegistered(UnitOfWorkInterceptorRegistrar.RegisterIfNeeded)` 登记）。
- [XiHan.Framework.Caching](./caching) — 缓存拦截器依赖本包实现方法级缓存切面。
