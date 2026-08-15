# AOP 与拦截器

`[UnitOfWork]`、`[Cacheable]`、审计留痕这些横切能力，底层都是同一套机制：**Castle 动态代理 + `IXiHanInterceptor`**。理解它能解释一大类「特性标了没生效」的问题，也是写自己横切逻辑的入口。

## 一条必须先记住的限制

::: danger 拦截只对「以接口为服务类型」的注册生效
`AddCastleDynamicProxy` 只处理 `ServiceType.IsInterface` 为真的服务描述器（用的是 `CreateInterfaceProxyWithTarget`）。

```csharp
services.AddScoped<MyService>();               // ❌ 注册为自身类型 → 不会被代理
services.AddScoped<IMyService, MyService>();   // ✅ 接口 → 会被代理
```

把类注册为它自己，自定义拦截器**全部静默失效**——不报错、不记日志。要用 AOP 就走「接口 + 实现」。
:::

其次检查：类型有没有被列进 `DynamicProxyIgnoreTypes`。

::: tip HTTP 入口不受这条限制
控制器由 MVC 自行激活、动态 API 控制器又直接注入应用服务的具体类，两者都不走代理。框架内置的两个横切能力另有 MVC 过滤器覆盖这条路径：`XiHanUnitOfWorkFilter` 处理 `[UnitOfWork]`、`XiHanCacheFilter` 处理 `[Cacheable]` / `[CacheEvict]`，都经动作上的 `OriginalMethodAttribute` 回查应用服务的原始方法读取特性。

所以这两个特性在 HTTP 请求上照常生效；**自定义拦截器没有这层兜底**，只能靠接口代理。
:::

## 写一个自己的拦截器

### 1. 拦截器

```csharp
using XiHan.Framework.Core.DynamicProxy;

public class BillingAuditInterceptor(ILogger<BillingAuditInterceptor> logger) : IXiHanInterceptor
{
    public async Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        var sw = Stopwatch.StartNew();
        await invocation.ProceedAsync();     // ← 必须调用，否则目标方法不会执行
        logger.LogInformation("{Method} 耗时 {Ms}ms", invocation.Method.Name, sw.ElapsedMilliseconds);
    }
}
```

`IXiHanMethodInvocation` 能拿到什么：

| 成员 | 说明 |
| --- | --- |
| `Method` | `MethodInfo`，可读特性 |
| `Arguments` / `ArgumentsDictionary` | 入参 |
| `GenericArguments` | 泛型实参 |
| `TargetObject` | 真实目标实例 |
| `ReturnValue` | 返回值，**可读可写** |
| `ProceedAsync()` | 调用下一环 |

### 2. 注册器：决定拦谁

```csharp
public static class BillingAuditInterceptorRegistrar
{
    public static void RegisterIfNeeded(IOnServiceRegistredContext context)
    {
        if (!DynamicProxyIgnoreTypes.Contains(context.ImplementationType)
            && context.ImplementationType.IsDefined(typeof(BillingAuditAttribute), inherit: true))
        {
            context.Interceptors.TryAdd<BillingAuditInterceptor>();
        }
    }
}
```

### 3. 挂上

```csharp
public override void PreConfigureServices(ServiceConfigurationContext context)
{
    context.Services.OnRegistered(BillingAuditInterceptorRegistrar.RegisterIfNeeded);
}
```

`OnRegistered` 登记的回调会在每个服务描述器上跑一遍；只要往 `context.Interceptors` 塞了拦截器，该服务就会被包成代理。框架自己的 `XiHanUowModule`、`XiHanCachingModule` 用的就是这个 API。

::: tip 挂在 `PreConfigureServices`
注册器要在其他模块注册服务**之前**登记好，否则先注册的服务不会被扫到。
:::

## 拿到未被代理的真实实例

某些场景下代理会坏事——最典型的是**匿名端点**：

::: danger 匿名端点调被代理的服务会永久挂起
匿名端点（Minimal API、OAuth 回调等）不经过工作单元中间件，而服务被代理包着，UoW 拦截器会急切开事务从而死锁。

解法是绕开代理：
- 注入真正的依赖直连，或
- 用 `ProxyHelper.UnProxy(service)` 取真实目标实例再调。

框架的 OAuth 回调端点就是这么处理的。
:::

## 执行顺序

多个拦截器按登记顺序串成管道：

```text
拦截器 A 前段 → 拦截器 B 前段 → 目标方法 → B 后段 → A 后段
```

先登记的在外层。要控制顺序就控制 `OnRegistered` 的调用顺序（即模块的拓扑序）。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 特性标了没生效 | 服务注册类型不是接口（最常见）；或类型在 `DynamicProxyIgnoreTypes` 里。`[UnitOfWork]` / `[Cacheable]` 走 HTTP 时不受此限，见上方提示 |
| 目标方法没执行 | 拦截器忘了调 `ProceedAsync()` |
| 匿名端点调用挂起 | 走了代理，用 `ProxyHelper.UnProxy` |
| 自定义拦截器不生效 | `OnRegistered` 挂在了 `ConfigureServices`（太晚），改到 `PreConfigureServices` |

## 下一步

- [依赖注入](./dependency-injection)：服务注册与暴露类型
- [工作单元与事务](./uow)：最重要的一个拦截器
- [Castle 包](../packages/castle)：动态代理集成细节
