# 扩展与二次开发

本页讲**怎么在 XiHan.Framework 上写自己的东西**：新建一个模块、替换框架的默认实现、加自定义 AOP 拦截器、定制动态 API 约定。所有约定都对照框架源码核实，照抄即可。

> 先读 [模块系统](./modularity)（`[DependsOn]` 与拓扑排序）、[生命周期](./lifecycle)（七个钩子）、[依赖注入](./dependency-injection)（约定注册与选项模式）三页打底，本页只讲**怎么落地**。

## 先决定扩展粒度

| 你想做的事 | 用哪个配方 |
| --- | --- |
| 给应用加一块自成体系的能力（自己的服务 + 配置 + 中间件） | [配方 A：写一个模块](#配方-a-写一个自己的模块) |
| 框架某个默认实现不满足需求（存储、配置源、权限判定器…） | [配方 B：替换默认实现](#配方-b-替换框架的默认实现) |
| 想给一批方法加统一的横切逻辑（事务、缓存、审计之外的） | [配方 C：自定义 AOP 拦截器](#配方-c-自定义-aop-拦截器) |
| 想改接口路由的推导规则（前缀、谓词、版本段） | [配方 D：定制动态 API 约定](#配方-d-定制动态-api-约定) |
| 想做一个能发 NuGet 的通用框架包 | [配方 E：发一个框架级包](#配方-e-发一个框架级包) |

---

## 配方 A：写一个自己的模块

**模块 = 一个继承 `XiHanModule` 的类**。它声明自己依赖谁、注册自己的服务、在生命周期节点接中间件。这是扩展框架的默认姿势。

### 1. 建工程、装依赖

```bash
dotnet new classlib -n MyCompany.Billing
cd MyCompany.Billing
dotnet add package XiHan.Framework.Application   # 应用服务基类 + 动态 API 特性
dotnet add package XiHan.Framework.Data          # 用到数据访问时
```

只装你真的要用的包——框架是按模块安装的，传递依赖会自动带上底层库。

### 2. 写模块类

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;

namespace MyCompany.Billing;

[DependsOn(
    typeof(XiHanDataModule)      // 声明依赖：框架会保证它先于本模块装配
)]
public class MyCompanyBillingModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();

        // 绑定自己的配置节
        Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));

        // 注册自己的服务（把接线集中到扩展方法里，见下）
        services.AddBillingDomainServices();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseMiddleware<BillingQuotaMiddleware>();
    }
}
```

要点：

- **`[DependsOn]` 只写直接依赖**。间接依赖由被依赖模块自己声明，框架按依赖图拓扑排序，不需要你重复列举。
- 钩子选择：注册服务用 `ConfigureServices`；接中间件、映射端点用 `OnApplicationInitialization`；需要在管道最前面插东西（如某些 Webhook 校验）用 `OnPreApplicationInitialization`；要等一切就绪后再做的事（如把数据库里的任务同步进调度器）用 `OnPostApplicationInitialization`。
- 拿 `IApplicationBuilder` / `IConfiguration` / `IWebHostEnvironment` 的扩展方法（`GetApplicationBuilder()` 等）来自 `XiHan.Framework.Web.Core`，纯类库模块里没有。

### 3. 服务接线集中到扩展方法

框架内与 BasicApp 都遵循同一个约定：**模块类只调用扩展方法，具体注册写在 `Extensions/ServiceCollectionExtensions.cs`**。这样模块类始终一目了然，接线细节可独立测试与复用。

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBillingDomainServices(this IServiceCollection services)
    {
        // 领域服务接口不带 DI 标记接口，必须显式登记
        services.AddScoped<IInvoiceDomainService, InvoiceDomainService>();
        return services;
    }
}
```

::: warning 什么该手写、什么会自动注册
- **自动注册**：实现 `ITransientDependency` / `IScopedDependency` / `ISingletonDependency` 标记接口的类；应用服务（`ApplicationServiceBase` 已实现 `ITransientDependency`）；仓储基类。
- **必须手写**：**领域服务**——它们的接口通常不带任何 DI 标记接口，约定注册扫不到，漏了就是运行期 DI 解析异常。这是新增纵切片最常见的漏接线点。
:::

### 4. 选项与配置节命名

约定是把配置节名钉成常量放在 Options 类里，代码里一律引用常量而非内联字符串：

```csharp
public class BillingOptions
{
    public const string SectionName = "MyCompany:Billing";

    public int InvoiceRetentionDays { get; set; } = 365;
}
```

框架自身的配置节统一在 `XiHan:` 命名空间下（`XiHan:Data:SqlSugarCore`、`XiHan:Tasks:ScheduledJobs`、`XiHan:Web:Api:OpenApiSecurity` …），你的业务包建议用自己的顶层命名空间，避免和框架撞节。

消费时注入 `IOptions<BillingOptions>`；需要热更新用 `IOptionsMonitor<T>`。`XiHanModule` 上的 `PreConfigure<T>` / `Configure<T>` / `PostConfigure<T>` 分别对应「先于其他模块预设」「常规配置」「所有模块配置完后最终覆盖」。

### 5. 挂到宿主

在应用的启动模块上加一行 `[DependsOn]` 即可：

```csharp
[DependsOn(
    typeof(XiHanWebApiModule),
    typeof(MyCompanyBillingModule)   // ← 新模块
)]
public class MyAppModule : XiHanModule { }
```

**不要**在宿主里重复调用 `services.AddBilling(...)`——模块自己的 `ConfigureServices` 会被框架在依赖图里调用一次，重复调用只会重复注册。

### 6. 暴露 REST 接口

不用写 Controller。应用服务打 `[DynamicApi]` 就是接口：

```csharp
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Services;

[DynamicApi(Group = "MyCompany.Billing", GroupName = "计费服务", Tag = "发票")]
public class InvoiceAppService(IInvoiceDomainService invoices) : ApplicationServiceBase
{
    public Task<InvoiceDto> GetInvoiceDetailAsync(long id) => invoices.GetAsync(id);
    //  → GET /api/Invoice/InvoiceDetail?id=...

    public Task<InvoiceDto> CreateInvoiceAsync(InvoiceCreateDto input) => invoices.CreateAsync(input);
    //  → POST /api/Invoice/Invoice
}
```

路由推导规则（动词前缀剥离、默认 POST、路由段只由 `[FromRoute]` 产生）见 [动态 API](./dynamic-api)。

---

## 配方 B：替换框架的默认实现

框架的可替换点几乎都遵循同一个模式：**接口 + `TryAdd` 注册的默认实现**。常见的有 `IJobStore`、`IBackgroundJobStore`、`IPermissionChecker`、`IUserStore`、`IAiProviderConfigStore`、各 Bot 的 `*ConfigStore` 等。

::: danger 必须用 `Replace`，不能用 `TryAdd`
框架模块**先于**你的模块注册了默认实现（`TryAdd` 语义）。你再 `TryAdd` / `AddSingleton` 一个同接口实现，`TryAdd` 会被**静默忽略**，你的实现永不生效，且没有任何异常和日志。
:::

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

public static IServiceCollection AddBillingStores(this IServiceCollection services)
{
    // ✅ 覆盖框架默认：Replace
    services.Replace(ServiceDescriptor.Singleton<IAiProviderConfigStore, DbAiProviderConfigStore>());

    // ❌ 会被静默忽略
    // services.TryAddSingleton<IAiProviderConfigStore, DbAiProviderConfigStore>();
    return services;
}
```

也可以用特性走约定注册：

```csharp
[Dependency(ServiceLifetime.Scoped, ReplaceServices = true)]
public class DbPermissionChecker : IPermissionChecker { }
```

替换时**生命周期要和被替换者一致**——把 Singleton 的默认实现换成 Scoped 会在解析时抛作用域校验异常。

---

## 配方 C：自定义 AOP 拦截器

框架的 `[UnitOfWork]`、`[Cacheable]`、审计等横切能力都建立在同一套机制上：**Castle 动态代理 + `IXiHanInterceptor`**。你的拦截器接同一套。

### 1. 写拦截器

```csharp
using XiHan.Framework.Core.DynamicProxy;

public class BillingAuditInterceptor(ILogger<BillingAuditInterceptor> logger) : IXiHanInterceptor
{
    public async Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        var sw = Stopwatch.StartNew();
        await invocation.ProceedAsync();          // ← 必须调用，否则目标方法不会执行
        logger.LogInformation("{Method} 耗时 {Ms}ms", invocation.Method.Name, sw.ElapsedMilliseconds);
    }
}
```

### 2. 写注册器：决定哪些服务要被拦截

```csharp
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DynamicProxy;

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

### 3. 在模块里挂上

```csharp
public override void PreConfigureServices(ServiceConfigurationContext context)
{
    context.Services.OnRegistered(BillingAuditInterceptorRegistrar.RegisterIfNeeded);
}
```

`OnRegistered` 登记的回调会在每个服务描述器上跑一遍；只要回调往 `context.Interceptors` 里塞了拦截器，该服务就会被包成代理。框架自己的 `XiHanUowModule`、`XiHanCachingModule` 用的就是这个 API。

::: warning 拦截只对「以接口为服务类型」的注册生效
`AddCastleDynamicProxy` 只处理 `ServiceType.IsInterface` 为真的描述器（用的是 `CreateInterfaceProxyWithTarget`）。把类注册为它自己（`services.AddScoped<MyService>()`）**不会**被拦截——`[UnitOfWork]` / `[Cacheable]` 都会静默失效。要用 AOP 就走「接口 + 实现」。
:::

::: tip 拿到未被代理的真实实例
匿名端点等没有 UoW 中间件的场景里，调用被代理的服务会让拦截器急切开事务而死锁。用 `ProxyHelper.UnProxy(service)` 取出真实目标实例即可绕开。
:::

---

## 配方 D：定制动态 API 约定

动态 API 的选项**不走配置文件**，是一个代码方式配置的单例：`XiHanWebApiModule` 在装配时 `TryAddSingleton` 了一个 `DynamicApiOptions` 实例并把它交给约定实现持有。你的模块（依赖它，因而在它之后执行）在 `ConfigureServices` 里就地改这个实例即可：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.ConfigureDynamicApiConventions(conventions =>
    {
        conventions.HttpMethodConventions["Import"] = "POST";
        conventions.HttpMethodConventions["Export"] = "POST";
    });

    context.Services.ConfigureDynamicApiRoutes(routes =>
    {
        routes.UseModuleNameAsRoute = true;
    });
}
```

常用旋钮：

| 选项 | 默认值 | 作用 |
| --- | --- | --- |
| `IsEnabled` | `true` | 动态 API 总开关 |
| `DefaultRoutePrefix` | `"api"` | 全局路由前缀 |
| `RemoveServiceSuffix` | `true` | 是否剥掉服务类名后缀 |
| `ServiceSuffixes` | `["ApplicationService", "AppService", "Service"]` | 要剥掉的后缀列表 |
| `EnableApiVersioning` | `true` | 是否在路由里插 `v{version}` 段 |
| `DefaultApiVersion` | `null` | 默认版本号（不带 `v` 前缀） |
| `ThrowOnGenerationFailure` | `true` | **控制器生成失败时抛异常中断装配**；设为 `false` 会只记日志并跳过该服务的全部端点（接口静默消失，不建议） |
| `EnableBatchOperations` / `MaxBatchSize` | `true` / `100` | 批量操作开关与上限 |
| `Conventions.HttpMethodConventions` | 见 [动态 API](./dynamic-api) | 动词前缀 → HTTP 谓词映射表，可增删 |
| `Conventions.PreserveRoutePredicate` | `false` | 设为 `true` 则路由保留动词前缀（`GetUsers` → `/GetUsers`） |
| `Conventions.UseLowercaseRoutes` | `false` | 路由全小写 |
| `Routes.UseNamespaceAsRoute` | `false` | 把命名空间片段作为路由段 |
| `Routes.UseModuleNameAsRoute` | `false` | 把模块名作为路由段 |

需要**完全自定义推导逻辑**时，实现 `IDynamicApiConvention` 并在框架之前把它注册进容器——`AddDynamicApi` 用的是 `TryAddSingleton`，且会优先复用已注册的实例，所以在 `PreConfigureServices` 里 `services.AddSingleton<IDynamicApiConvention>(new MyConvention(...))` 即可接管，`DefaultDynamicApiConvention` 不会再被注册。

::: warning 改约定会改路由
动词表、`PreserveRoutePredicate`、`UseLowercaseRoutes` 任一改动都会让**已有接口的 URL 变化**，前端得同步改。上线后再动这些开关等于一次破坏性变更。
:::

---

## 配方 E：发一个框架级包

想做一个可被别人 `dotnet add package` 的通用能力包时，跟着框架自身的约定走：

| 约定 | 规则 |
| --- | --- |
| **包名** | 通用类库 `XiHan.Framework.[模块名]`；Web 相关 `XiHan.Framework.Web.[模块名]`。第三方作者请用自己的命名空间 |
| **SDK** | 通用类库用 `Microsoft.NET.Sdk`；Web 相关用 `Microsoft.NET.Sdk.Web` |
| **模块类名** | `XiHan[模块名]Module`，继承 `XiHanModule`。纯工具库（无需注册服务）可以不带模块类，直接引用即可 |
| **抽象/实现分包** | 契约放 `*.Abstractions`（零/极少依赖），实现另起一包。框架里 `EventBus`/`MultiTenancy`/`Validation`/`Localization`/`SearchEngines`/`Workflow`/`AI` 都是这个结构 |
| **分层** | 只能依赖比自己低的层（公共 → 元数据 → 核心 → 领域 → 应用 → 基础设施 → Web），**绝不反向**，保证依赖图无环、可裁剪 |
| **默认实现用 `TryAdd`** | 这样使用方能用 `Replace` 覆盖你的默认实现 |
| **配置节** | 常量 `SectionName` 钉在 Options 类上 |

分层图与命名细节见 [框架概述 · 分层架构](../overview#分层架构)。

---

## 约定速查

| 场景 | 做法 | 漏了会怎样 |
| --- | --- | --- |
| 常规服务注册 | 实现 `ITransientDependency` / `IScopedDependency` / `ISingletonDependency` | — |
| **领域服务注册** | **手写 `services.AddScoped<IFoo, Foo>()`** | 运行期 DI 解析异常 |
| **覆盖框架默认实现** | **`services.Replace(...)` 或 `[Dependency(ReplaceServices = true)]`** | 被静默忽略，默认实现继续生效 |
| 一个实现暴露多个接口 | `[ExposeServices(typeof(IA), IncludeDefaults = true)]` | 只注册默认推断出的那个接口 |
| 同接口多实现按 key 区分 | `[ExposeKeyedService<IFoo>("key")]` + `[FromKeyedServices("key")]` | — |
| 退出约定扫描 | `[DisableConventionalRegistration]` | 被自动注册一遍 |
| 要 AOP 拦截 | 服务类型必须是**接口** | 拦截器静默不生效 |
| 自定义拦截器 | `services.OnRegistered(注册器)` | 代理不会创建 |
| **本地事件处理器** | 显式加入 `XiHanLocalEventBusOptions.Handlers` | 裸 `AddTransient<具体类>` **不会被订阅**，静默失败 |
| 分页方法 | 显式标 `[HttpPost]` | 被推导成 GET，请求体绑不上 |
| 想要 `/{id}` 路径段 | 参数标 `[FromRoute]` | 参数落到查询串，路由段不会生成 |

## 下一步

- [常见问题](./faq)：模块不生效、拦截器失效、事务没提交等排查
- [模块系统](./modularity)：`[DependsOn]` 与拓扑排序机制
- [生命周期](./lifecycle)：七个钩子分别在什么时候跑
- [动态 API](./dynamic-api)：路由推导的完整规则
- [BasicApp 二次开发](https://basicapp.docs.xihanfun.com/backend/development)：在完整业务系统上扩展的实战清单
