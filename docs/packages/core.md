# XiHan.Framework.Core

> 框架的模块化引擎与运行时内核：定义 `XiHanModule` 基类、`[DependsOn]` 依赖声明、模块描述符与拓扑排序加载、7 阶段生命周期钩子、应用引导工厂、约定式依赖注入、选项模式与统一异常体系。上层所有模块都直接或间接依赖它。

- **NuGet**：`XiHan.Framework.Core`
- **模块类**：—（它本身提供 `XiHanModule` 基类与整套模块系统，无独立模块类）
- **所在层**：核心层
- **关键依赖**：`Microsoft.Extensions.Hosting`、`Microsoft.Extensions.Localization`（其余为 .NET 原生 `Microsoft.Extensions.DependencyInjection/Options/Configuration/Logging` + 框架内部 `XiHan.Framework.Metadata` / `XiHan.Framework.Utils`）

## 概述

XiHan.Framework.Core 是整个框架的引擎。它把"应用"抽象为一组**模块**：每个功能包都是一个继承 `XiHanModule` 的类，用 `[DependsOn(typeof(...))]` 声明它依赖哪些模块。框架从**启动模块**出发递归发现所有依赖，做拓扑排序，然后按序回调每个模块的生命周期钩子——先是服务注册三阶段（Pre / Configure / Post），再是（应用初始化时）初始化三阶段与关机钩子。

在这套模块流程之上，Core 还提供：约定式依赖注入（实现 `ITransientDependency` / `IScopedDependency` / `ISingletonDependency` 标记接口即自动注册）、`AddApplication<TStartupModule>()` 引导入口、选项模式增强（`PreConfigure` / 动态选项管理器）、统一异常体系（`XiHanException` / `BusinessException` / `UserFriendlyException`），以及反射、状态检查、动态代理抽象、追踪等基础设施抽象。

Core 只依赖 `Metadata` 与 `Utils`，不引用任何 Web / 数据 / 领域包，因此可以独立引用于任何 .NET Host（控制台、Worker、ASP.NET Core）。

## 何时使用

- 编写自己的功能模块：继承 `XiHanModule`，用 `[DependsOn]` 声明依赖，在 `ConfigureServices` 里注册服务、在 `OnApplicationInitialization` 里挂载中间件。
- 需要在应用启动、初始化、关机的特定阶段挂载逻辑（7 阶段钩子）。
- 需要约定式服务注册（标记接口/特性），或 `IServiceCollection.AddApplication` 引导整个应用。
- 需要选项模式的预配置（`PreConfigure`）或运行时可覆盖的动态选项。
- 需要框架统一的业务异常 / 用户友好异常体系。

> 说明：`ApplicationInitializationContext` 在 Core 里**只暴露 `ServiceProvider`**。常用的 `GetApplicationBuilder()` / `GetConfiguration()` / `GetEnvironment()` 是 **Web 层扩展方法**（定义在 `XiHan.Framework.Web.Core` 的 `ApplicationInitializationContextExtensions`），仅在 Web 应用中可用；纯 Core 场景请从 `context.ServiceProvider` 自行解析。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Core
```

Core 提供 `XiHanModule` 基类本身。业务模块通过继承它并用 `[DependsOn]` 声明依赖来接入模块系统：

```csharp
[DependsOn(
    typeof(XiHanAuthorizationModule),
    typeof(XiHanCachingModule))]
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 注册服务；context.Services 即 IServiceCollection
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 应用初始化时执行（Web 下通常在这里挂中间件）
    }
}
```

启用后（即被 `AddApplication` 引导时）Core 会自动注册以下核心服务（见 `InternalServiceCollectionExtensions.AddCoreServices`）：

- `AddOptions()` / `AddLogging()` / `AddLocalization()`（.NET 原生基础设施）。
- `IModuleLoader`（默认 `ModuleLoader`）、`IAssemblyFinder`（`AssemblyFinder`）、`ITypeFinder`（`TypeFinder`）、`IInitLoggerFactory`（`DefaultInitLoggerFactory`）—— 均以 `TryAddSingleton` 注册，可替换。
- `IXiHanApplication` / `IApplicationInfoAccessor` / `IModuleContainer` / `IXiHanHostEnvironment` 指向应用实例本身。
- `AutowiredServiceHandler`（属性/字段注入处理器）。
- `ISimpleStateCheckerManager<>`（简单状态检查，Transient）。
- 四个默认模块生命周期贡献者（`XiHanModuleLifecycleOptions.Contributors`，见下文"工作原理"）。
- 若未注册 `IConfiguration`，则用 `XiHanConfigurationBuilderOptions` 构建一份并 `ReplaceConfiguration`。

## 工作原理

### 应用引导流程

以 Web Host 为典型（`AddApplicationAsync` → `InitializeApplicationAsync`）：

```text
1. Services.AddApplication<TStartupModule>(options)
   └─ XiHanApplicationFactory.Create → new XiHanApplicationWithExternalServiceProvider
      ├─ 注册自身为 IXiHanApplication / IApplicationInfoAccessor / IModuleContainer / IXiHanHostEnvironment
      ├─ AddCoreServices()：Options/Logging/Localization + 模块系统服务 + 生命周期贡献者
      ├─ LoadModules()：IModuleLoader.LoadModules → 发现全部模块 + 拓扑排序
      └─ ConfigureServices()（同步；异步入口会 SkipConfigureServices=true 延后）
         ├─ PreConfigureServices  （每个模块，按依赖序）
         ├─ ConfigureServices     （AddAssembly 约定式注册 + 每模块 ConfigureServices）
         └─ PostConfigureServices （每个模块）
2. app.InitializeApplicationAsync()  →  application.InitializeAsync(app.ApplicationServices)
   ├─ SetServiceProvider(sp)：把真实根容器写回 ObjectAccessor<IServiceProvider>
   └─ IModuleManager.InitializeModulesAsync()：在一个新 scope 内依次跑生命周期贡献者
      ├─ OnPreApplicationInitialization  （每个模块）
      ├─ OnApplicationInitialization     （每个模块）
      └─ OnPostApplicationInitialization （每个模块）
3. 应用关闭：IModuleManager.ShutdownModulesAsync() → OnApplicationShutdown（模块逆序）
```

要点：

- **拓扑排序**：`ModuleLoader.SortByDependency` 用 `SortByDependencies(m => m.Dependencies)` 排序，保证被依赖模块先于依赖方；随后把**启动模块移到最后**（`MoveItem(... , count-1)`），确保它的钩子最后执行。
- **服务配置阶段** vs **初始化阶段**：前者在容器构建**之前**（只能操作 `IServiceCollection`），后者在容器构建**之后**（可以从 `ServiceProvider` 解析服务、挂中间件）。
- **每个模块类是单例**：`ModuleLoader.CreateAndRegisterModule` 用 `Activator.CreateInstance` 建实例并 `AddSingleton(moduleType, instance)`，同一 `IXiHanModule` 实例贯穿服务配置与初始化。
- **异常隔离**：任一模块在服务配置阶段抛错，包装为 `InitializationException`；初始化/关闭阶段抛错，分别包装为 `InitializationException` / `ShutdownException`，消息含出错模块的 `AssemblyQualifiedName`。

### 生命周期贡献者机制

模块的初始化/关闭并非直接调用，而是通过**贡献者**（`IModuleLifecycleContributor`）分派。`AddCoreServices` 默认注册四个贡献者到 `XiHanModuleLifecycleOptions.Contributors`：

| 贡献者 | 对应钩子接口 | 阶段 |
| --- | --- | --- |
| `OnPreApplicationInitializationModuleLifecycleContributor` | `IOnPreApplicationInitialization` | 初始化前 |
| `OnApplicationInitializationModuleLifecycleContributor` | `IOnApplicationInitialization` | 初始化 |
| `OnPostApplicationInitializationModuleLifecycleContributor` | `IOnPostApplicationInitialization` | 初始化后 |
| `OnApplicationShutdownModuleLifecycleContributor` | `IOnApplicationShutdown` | 关闭 |

`ModuleManager` 对每个贡献者、每个模块做双重循环：初始化时按模块拓扑序、关闭时**逆序**（`Modules.Reverse()`）。你可以向 `Contributors` 追加自定义贡献者来插入新的生命周期阶段。

### 约定式依赖注入

`ConfigureServices` 阶段，Core 对每个模块的 `AllAssemblies` 调用 `Services.AddAssembly(assembly)`，由 `DefaultConventionalRegistrar` 扫描其中的具体类并自动注册。生命周期解析优先级（`ConventionalRegistrarBase.GetLifeTimeOrNull`）：

1. `[Dependency(ServiceLifetime.X)]` 特性显式指定；
2. 类层次实现的标记接口：`ITransientDependency` → Transient，`ISingletonDependency` → Singleton，`IScopedDependency` → Scoped；
3. 默认返回 `null` → **不注册**（既没特性也没标记接口的类会被跳过）。

暴露的服务类型由 `[ExposeServices]` / `[ExposeKeyedService]` 或默认约定（接口名去掉前导 `I` 后是类名后缀，如 `FooService : IFooService`）决定。`[Dependency]` 上的 `TryRegister` / `ReplaceServices` 决定用 `TryAdd` 还是 `Replace`（默认 `Add`）。用 `[DisableConventionalRegistration]` 可让某类退出约定扫描。

## 核心能力

- **模块化系统**：`XiHanModule` 基类 + `[DependsOn]`，`XiHanModuleHelper` 递归发现模块（并打印目录树日志），`ModuleLoader` 建描述符并拓扑排序，`ModuleManager` 驱动生命周期。详见 [模块化](../guide/modularity)。
- **7 阶段生命周期**：服务注册三阶段 + 初始化三阶段 + 关机，每阶段同步/异步双版本。详见 [生命周期](../guide/lifecycle)。
- **应用引导**：`IServiceCollection.AddApplication<TStartupModule>()` / `AddApplicationAsync<TStartupModule>()` 从启动模块引导；`XiHanApplicationFactory` 支持"外部容器"（Web/Host）与"内部容器"（独立自建 `ServiceCollection`）两种模式。
- **远程/集成服务标记**：`IRemoteService` 空标记接口——`XiHan.Framework.Application.Contracts` 的 `IApplicationService` 即继承自它，是动态 API 自动暴露为 REST 的判定基础（`XiHan.Framework.Web.Api` 的 `TypeHelper` 用 `IApplicationService.IsAssignableFrom(type)` 识别应用服务类型）；配套的细粒度控制特性 `RemoteServiceAttribute`（`IsEnabled` / `IsMetadataEnabled` / `Name` 分组名 + `IsExplicitlyEnabledFor` 等静态判定）、`IntegrationServiceAttribute`（标记集成服务，`IsDefinedOrInherited` 支持接口继承判定）、`ApplicationServiceTypes`（`[Flags]` 枚举：`ApplicationServices` / `IntegrationServices` / `All`）、`DisableXiHanFeaturesAttribute`（按类禁用拦截器 / 中间件 / MVC 过滤器）均定义在 Core，供上层按需读取。
- **约定式依赖注入**：标记接口 / `[Dependency]` / `[ExposeServices]` / `[ExposeKeyedService]` / `[DisableConventionalRegistration]`，可插拔 `IConventionalRegistrar`。详见 [依赖注入](../guide/dependency-injection)。
- **缓存式服务提供器**：`ICachedServiceProvider`（scoped，缓存含瞬态在内的所有已解析服务）/ `ITransientCachedServiceProvider` / `IRootServiceProvider`。
- **属性/字段注入**：`[AutowiredService]` + `AutowiredServiceHandler.Autowired(this)`（推荐构造函数注入，此为特殊情况兜底）。
- **选项模式增强**：`PreConfigure<TOptions>` 预配置、`XiHanDynamicOptionsManager<T>`（运行时可覆盖选项）、`XiHanOptionsFactory`。
- **异常体系**：`XiHanException`、`BusinessException`、`UserFriendlyException`、`InitializationException` / `ShutdownException`，异常语义接口 `IHasErrorCode` / `IHasErrorDetails` / `IHasHttpStatusCode` / `IHasLogLevel` / `ILocalizeErrorMessage`，及 `IExceptionNotifier` / `IExceptionSubscriber` 通知链。
- **反射与类型发现**：`IAssemblyFinder` / `ITypeFinder`。
- **简单状态检查**：`ISimpleStateChecker<TState>` / `ISimpleStateCheckerManager<TState>`（权限/特性开关等布尔判定的通用框架）。
- **动态代理抽象**：`IXiHanInterceptor` / `IXiHanMethodInvocation` / `ProxyHelper`（Castle 代理的 `UnProxy` / `GetUnProxiedType`）。
- **追踪**：`ICorrelationIdProvider` / `DefaultCorrelationIdProvider`（分布式关联唯一标识）；`XiHanActivitySources`（框架共享 `ActivitySource` 名与实例：`App`/`Data`/`EventBus`/`Grpc`/`Cache`/`Ai` 六个源常量 + 对应实例，`All` 数组供 `XiHan.Framework.Observability` 一次性 `AddSource` 批量注册）。
- **横切关注点去重**：`XiHanCrossCuttingConcerns`（静态门面：`Applying`/`AddApplied`/`RemoveApplied`/`IsApplied`/`GetApplieds`）+ `IAvoidDuplicateCrossCuttingConcerns`（`AppliedCrossCuttingConcerns` 列表），用于标记某对象已应用过某横切逻辑，避免嵌套调用中重复执行（如拦截器判定"是否已处理过"）；`XiHan.Framework.Web.Api` 的 `XiHanController` 已实现该接口，为其派生控制器提供去重挂载点。
- **异步与锁工具**：`AsyncHelper`（`RunSync` 同步等待异步方法、`UnwrapTask` 拆 `Task`/`Task<T>` 真实返回类型）、`AsyncLock`（信号量实现的异步友好独占锁，`LockAsync()`/`LockAsync(timeout)`/`Lock()`）、`SemaphoreSlimExtensions`（`SemaphoreSlim.LockAsync()`/`Lock()` 系列，返回 `IDisposable` 自动释放）、`LockExtensions`（`object.Locking(...)` 简化 `lock` 语句）、`AsyncExtensions`（`MethodInfo.IsAsync()` / `Type.IsTaskOrTaskOfT()` / `IsTaskOfT()` 反射判定）。
- **插件源**：`FolderPlugInSource` / `FilePlugInSource` / `TypePlugInSource`，从目录、程序集或类型加载额外模块。

## 主要 API / 类型

### 模块系统

| 类型 | 说明 |
| --- | --- |
| `XiHanModule`（abstract） | 所有模块基类，实现全部钩子接口的虚方法；提供 `Configure/PreConfigure/PostConfigure<TOptions>`；`SkipAutoServiceRegistration` 可关闭本模块的约定扫描 |
| `IXiHanModule` | 最小契约：`ConfigureServices` / `ConfigureServicesAsync` |
| `DependsOnAttribute`（`[AttributeUsage(Class, AllowMultiple=true)]`） | `DependsOn(params Type[])`，实现 `IDependedTypesProvider.GetDependedTypes()` 驱动依赖发现 |
| `AdditionalAssemblyAttribute` | 声明模块的附加程序集（`IAdditionalModuleAssemblyProvider`），一并纳入约定扫描 |
| `IModuleDescriptor` / `XiHanModuleDescriptor` | 模块描述符：`Type` / `Assembly` / `AllAssemblies` / `Instance`（单例）/ `IsLoadedAsPlugIn` / `Dependencies` |
| `IModuleLoader` / `ModuleLoader` | `IModuleDescriptor[] LoadModules(IServiceCollection, Type startupModuleType, PlugInSourceList)` |
| `IModuleManager` / `ModuleManager` | `InitializeModules(Async)(ApplicationInitializationContext)` / `ShutdownModules(Async)(ApplicationShutdownContext)` |
| `IModuleContainer` | `IReadOnlyList<IModuleDescriptor> Modules` |
| `XiHanModuleHelper`（static） | `FindAllModuleTypes` / `FindDependedModuleTypes` / `GetAllAssemblies` / `IsXiHanModule` |
| `IModuleLifecycleContributor` / `ModuleLifecycleContributorBase` | 生命周期分派器基类 |
| `XiHanModuleLifecycleOptions` | `ITypeList<IModuleLifecycleContributor> Contributors` |

### 生命周期钩子接口（均含同步 + 异步双方法）

| 阶段 | 接口 | 方法 |
| --- | --- | --- |
| 服务注册前 | `IPreConfigureServices` | `PreConfigureServices(Async)(ServiceConfigurationContext)` |
| 服务注册 | `IXiHanModule` | `ConfigureServices(Async)(ServiceConfigurationContext)` |
| 服务注册后 | `IPostConfigureServices` | `PostConfigureServices(Async)(ServiceConfigurationContext)` |
| 初始化前 | `IOnPreApplicationInitialization` | `OnPreApplicationInitialization(Async)(ApplicationInitializationContext)` |
| 初始化 | `IOnApplicationInitialization` | `OnApplicationInitialization(Async)(ApplicationInitializationContext)` |
| 初始化后 | `IOnPostApplicationInitialization` | `OnPostApplicationInitialization(Async)(ApplicationInitializationContext)` |
| 关闭 | `IOnApplicationShutdown` | `OnApplicationShutdown(Async)(ApplicationShutdownContext)` |

`XiHanModule` 已实现全部接口的空虚方法：默认异步版本调用同步版本（`XxxAsync → Xxx; return Task.CompletedTask`），你只覆盖需要的方法即可；覆盖异步版本时不必再调基类同步版本。

### 应用引导

| 类型 | 说明 |
| --- | --- |
| `ServiceCollectionApplicationExtensions`（static） | `AddApplication<TStartupModule>(this IServiceCollection, Action<XiHanApplicationCreationOptions>?)` 及 `Type` 重载、`...Async` 重载；辅助 `GetApplicationName` / `GetApplicationInstanceId` / `GetXiHanHostEnvironment` |
| `XiHanApplicationFactory`（static） | `Create/CreateAsync<TStartupModule>(...)`：无 `services` 参数 → 内部容器（`IXiHanApplicationWithInternalServiceProvider`）；带 `services` → 外部容器（`IXiHanApplicationWithExternalServiceProvider`）。`Async` 版本会先置 `SkipConfigureServices=true` 再 `await ConfigureServicesAsync()` |
| `IXiHanApplication` | 应用契约：`StartupModuleType` / `Services` / `ServiceProvider` / `ConfigureServicesAsync()` / `Shutdown(Async)()`；继承 `IModuleContainer` / `IApplicationInfoAccessor` / `IDisposable` |
| `IXiHanApplicationWithExternalServiceProvider` | 由外部（Host）构建容器：`SetServiceProvider(sp)` / `Initialize(Async)(IServiceProvider)`（Web 场景用） |
| `IXiHanApplicationWithInternalServiceProvider` | 自建容器：`CreateServiceProvider()` / `Initialize(Async)()`（控制台/测试用） |
| `XiHanApplicationBase` | 上述实现基类，含 `ConfigureServices(Async)` 三阶段编排与 `Shutdown(Async)` |
| `XiHanApplicationCreationOptions` | 见下"配置" |
| `IApplicationInfoAccessor` | `ApplicationName` / `InstanceId`（`InstanceId` 为每次启动生成的 Guid） |
| `IXiHanHostEnvironment` / `XiHanHostEnvironment` | `EnvironmentName`（未设置时默认回落 `Production`） |
| `IRemoteService` | 空标记接口：标识某类型可作为远程服务 |
| `RemoteServiceAttribute` | `[AttributeUsage(Interface\|Class\|Method)]`；`IsEnabled` / `IsMetadataEnabled` / `Name`（分组名）；`IsExplicitlyEnabledFor(Type/MethodInfo)` 等静态判定方法 |
| `IntegrationServiceAttribute` | `[AttributeUsage(Class\|Interface)]`；`IsDefinedOrInherited<T>()` 判定类型或其接口是否标注 |
| `ApplicationServiceTypes`（`[Flags]` 枚举） | `ApplicationServices` / `IntegrationServices` / `All` |
| `DisableXiHanFeaturesAttribute` | `[AttributeUsage(Class)]`；`DisableInterceptors` / `DisableMiddleware` / `DisableMvcFilters`（均默认 `true`） |

### 上下文

| 类型 | 关键成员 |
| --- | --- |
| `ServiceConfigurationContext` | `IServiceCollection Services`；`IDictionary<string, object?> Items` + 索引器 `this[key]`（模块间在服务注册阶段共享数据） |
| `ApplicationInitializationContext` | `IServiceProvider ServiceProvider`（实现 `IServiceProviderAccessor`）。`GetApplicationBuilder()`/`GetConfiguration()`/`GetEnvironment()` 属 **Web.Core 扩展**，非本包 |
| `ApplicationShutdownContext` | `IServiceProvider ServiceProvider` |
| `LocalizationContext` | `ServiceProvider` / `IStringLocalizerFactory LocalizerFactory` |

### 依赖注入

| 类型 | 说明 |
| --- | --- |
| `ITransientDependency` / `IScopedDependency` / `ISingletonDependency` | 标记接口，实现即按对应生命周期自动注册（命名空间 `...DependencyInjection.ServiceLifetimes`） |
| `DependencyAttribute` | `[Dependency(ServiceLifetime)]`：`Lifetime` / `TryRegister` / `ReplaceServices` |
| `ExposeServicesAttribute` | `[ExposeServices(params Type[])]`：`IncludeDefaults` / `IncludeSelf`；控制暴露哪些服务类型 |
| `ExposeKeyedServiceAttribute` | 键控服务暴露 |
| `DisableConventionalRegistrationAttribute` | 让类退出约定扫描 |
| `DisablePropertyInjectionAttribute` | 禁用属性注入 |
| `IConventionalRegistrar` / `ConventionalRegistrarBase` / `DefaultConventionalRegistrar` | 约定注册器，可 `services.AddConventionalRegistrar(...)` 追加自定义 |
| `ICachedServiceProvider` / `ITransientCachedServiceProvider` | 缓存已解析服务（含瞬态）；`GetService<T>(defaultValue)` / `GetService<T>(factory)` 等 |
| `IRootServiceProvider` / `RootServiceProvider` | 访问根容器（避开 scope 生命周期） |
| `IObjectAccessor<T>` / `ObjectAccessor<T>` | 延迟对象持有者（引导期占位、后填值），见 `IServiceCollection.TryAddObjectAccessor` |
| `AutowiredServiceAttribute` / `AutowiredServiceHandler` | 属性/字段注入 |
| `IServiceProviderAccessor` / `IClientScopeServiceProviderAccessor` | 服务提供器访问抽象 |
| `IOnServiceRegistredContext` / `IOnServiceExposingContext` / `IOnServiceActivatedContext` | 注册/暴露/激活拦截扩展点 |

### 反射 / 集合 / 状态检查 / 代理 / 追踪 / 横切关注点 / 异步锁 / 异常

| 类型 | 说明 |
| --- | --- |
| `IAssemblyFinder` / `AssemblyFinder`、`ITypeFinder` / `TypeFinder` | 从已加载模块发现程序集与类型 |
| `ITypeList` / `ITypeList<TBaseType>` / `TypeList` | 类型安全的 `IList<Type>`（生命周期贡献者列表等用它） |
| `ISimpleStateChecker<TState>` / `ISimpleBatchStateChecker<TState>` / `ISimpleStateCheckerManager<TState>` / `SimpleStateCheckerContext<TState>` / `SimpleStateCheckerResult<TState>` | 布尔状态判定框架（权限、特性开关等） |
| `IHasSimpleStateCheckers<TState>` / `SimpleStateCheckerOptions<TState>` | 状态检查器聚合与配置 |
| `IXiHanInterceptor` / `IXiHanMethodInvocation` / `XiHanInterceptor` | 框架级拦截器抽象（由 `XiHan.Framework.Castle` 落地到 Castle） |
| `ProxyHelper`（static） | `IsProxy(obj)` / `UnProxy(obj)` / `GetUnProxiedType(obj)`；识别 `Castle.Proxies` 命名空间 |
| `DynamicProxyIgnoreTypes` | 不做动态代理的类型登记 |
| `ICorrelationIdProvider` / `DefaultCorrelationIdProvider` | `string? Get()` / `IDisposable Change(string?)`（作用域内临时切换关联唯一标识） |
| `XiHanActivitySources`（static） | 框架共享 `ActivitySource`：`App`/`Data`/`EventBus`/`Grpc`/`Cache`/`Ai` 六个源名常量 + `AppSource`/`DataSource`/`EventBusSource`/`GrpcSource`/`CacheSource` 五个实例（`Ai` 无独立实例字段，其值须与 AI 管道的 `AiPipelineOptions.TelemetrySourceName` 一致）；`All` 汇总全部六个源名供 OTel 一次性批量 `AddSource`；置于 Core.Tracing 而非 Observability，避免 Data/EventBus/Http/Web 反向依赖 Observability |
| `XiHanCrossCuttingConcerns`（static） | `Applying(obj, concerns)` 返回 `IDisposable`（作用域内标记+自动移除）、`AddApplied` / `RemoveApplied` / `IsApplied` / `GetApplieds` |
| `IAvoidDuplicateCrossCuttingConcerns` | `List<string> AppliedCrossCuttingConcerns { get; }`；`XiHan.Framework.Web.Api` 的 `XiHanController` 实现此接口 |
| `AsyncHelper`（static） | `RunSync<TResult>(Func<Task<TResult>>)` / `RunSync(Func<Task>)`（同步阻塞等待）、`UnwrapTask(Type)` |
| `AsyncLock` | `LockAsync()` / `LockAsync(TimeSpan)`（超时抛 `TimeoutException`）/ `Lock()`，均返回释放用的 `IDisposable` |
| `SemaphoreSlimExtensions`（static） | `SemaphoreSlim` 的 `LockAsync(...)` / `Lock(...)` 系列重载（支持超时、`CancellationToken`），返回 `IDisposable` 自动 `Release` |
| `LockExtensions`（static） | `object.Locking(Action)` / `Locking<T>(Action<T>)` / `Locking<TResult>(Func<TResult>)` 等，封装 `lock` 语句 |
| `AsyncExtensions`（static） | `MethodInfo.IsAsync()`、`Type.IsTaskOrTaskOfT()` / `IsTaskOfT()` |
| `NameValue` / `NameValue<T>` | 通用名称/值对承载类型（`Timing`、`Settings` 等包用于承载时区名、设置值） |
| `NamedTypeSelector` | `Name` + `Predicate<Type>` 组成的命名类型断言选择器 |
| `XiHanException` | 框架根异常；构造时把消息以 `Error` 级写入 `LogHelper` |
| `BusinessException` | 实现 `IBusinessException` / `IHasErrorCode` / `IHasErrorDetails` / `IHasLogLevel`；`Code` / `Details` / `LogLevel`（默认 `Warning`）/ `LocalizableMessage`（弱类型承载可本地化消息）/ `WithData(name, value)` |
| `UserFriendlyException` | `BusinessException` 子类，可直接呈现给终端用户；支持 `(message, code, details, ...)` 与 `(localizableMessage, fallbackMessage, ...)` 两种构造 |
| `InitializationException` / `ShutdownException` | 生命周期错误包装（消息前缀"程序初始化异常/关闭过程异常"，构造时记 `Error` 日志） |
| `IExceptionNotifier` / `ExceptionNotifier` / `IExceptionSubscriber` / `ExceptionNotificationContext` | 异常通知链：`NotifyAsync` 遍历所有 `IExceptionSubscriber.HandleAsync`，单个订阅者抛错只记警告不中断 |
| `ConnectionStrings` | `Dictionary<string,string>` 子类，`const DefaultConnectionStringName = "Default"` + `Default` 属性 |

### 插件源

| 类型 | 说明 |
| --- | --- |
| `IPlugInSource` / `PlugInSourceList` | 插件源集合，`GetAllModules(logger)` 汇总所有插件模块类型 |
| `FolderPlugInSource` | 从目录扫描程序集加载模块 |
| `FilePlugInSource` | 从指定程序集文件加载 |
| `TypePlugInSource` | 直接以类型登记插件模块 |

插件模块通过 `XiHanApplicationCreationOptions.PlugInSources` 配置，`ModuleLoader.FillModules` 会把它们并入模块图（`IsLoadedAsPlugIn = true`），与依赖声明的模块一同排序、执行生命周期。

## 配置

`XiHanApplicationCreationOptions`（通过 `AddApplication(options => ...)` 配置）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Services` | `IServiceCollection` | 传入 | 服务容器（只读） |
| `PlugInSources` | `PlugInSourceList` | 空 | 插件源列表 |
| `Configuration` | `XiHanConfigurationBuilderOptions` | 新建 | 仅当未注册 `IConfiguration` 时用于构建配置 |
| `SkipConfigureServices` | `bool` | `false` | 是否跳过服务配置三阶段（异步引导路径会置为 `true`，随后显式 `await ConfigureServicesAsync()`） |
| `ApplicationName` | `string?` | `null` | 应用名（未设则读配置 `ApplicationName`，再回落入口程序集名） |
| `Environment` | `string?` | `null` | 环境名（未设则回落 `Production`） |

`XiHanConfigurationBuilderOptions`（`Configuration` 子对象，节选真实字段——用于在未提供 `IConfiguration` 时构建）：控制 `appsettings.json` / 用户机密 / 环境变量 / 命令行参数等来源；`HostingHostBuilderExtensions.AddAppSettingsSecretsJson()` 额外支持 `appsettings.secrets.json`（`const AppSettingsSecretJsonPath`）。

> Core 本身不定义业务配置节；配置项以模块各自的 `Options` 为准。

## 使用示例

### 编写并声明一个模块

```csharp
[DependsOn(typeof(XiHanCachingModule))]
public class MyFeatureModule : XiHanModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // 预配置：为后续模块提供可被 PostConfigure 覆盖的基线
        context.Services.PreConfigure<MyOptions>(o => o.Enabled = true);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<MyOptions>(o => o.Timeout = TimeSpan.FromSeconds(30));
        // 具体类实现 ITransientDependency/IScopedDependency/ISingletonDependency 即自动注册，
        // 这里通常只写需要显式配置或第三方库的注册。
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var svc = context.ServiceProvider.GetRequiredService<IMyService>();
        await svc.WarmupAsync();
    }
}
```

### 约定式注册

```csharp
// 无需在模块里手写注册，实现标记接口即可：
public class OrderService : IOrderService, IScopedDependency { /* ... */ }

// 或用特性精确控制暴露类型与语义：
[Dependency(ServiceLifetime.Singleton, ReplaceServices = true)]
[ExposeServices(typeof(ICacheProvider))]
public class RedisCacheProvider : ICacheProvider { /* ... */ }
```

### 在 ASP.NET Core Host 引导（Web 场景）

```csharp
var builder = WebApplication.CreateBuilder(args);
// Web.Core 扩展，内部调用 Core 的 AddApplicationAsync（外部容器模式）
await builder.AddApplicationAsync<MyStartupModule>();

var app = builder.Build();
// Web.Core 扩展，内部调用 application.InitializeAsync(app.Services) → 跑初始化三阶段
await app.InitializeApplicationAsync();
app.Run();
```

### 独立 Host / 测试（内部容器模式）

```csharp
using var app = await XiHanApplicationFactory.CreateAsync<MyStartupModule>();
app.Initialize();                       // 建容器 + 跑初始化钩子
var sp = app.ServiceProvider;
// ... 使用服务 ...
app.Shutdown();                         // 逆序跑关机钩子
```

## 扩展点 / 自定义

- **替换核心基础服务**：`IModuleLoader` / `IAssemblyFinder` / `ITypeFinder` / `IInitLoggerFactory` 都以 `TryAddSingleton` 注册，在更早注册同类型即可覆盖默认实现。
- **自定义约定注册器**：实现 `IConventionalRegistrar`（或继承 `ConventionalRegistrarBase`），`services.AddConventionalRegistrar(new MyRegistrar())`。
- **新增生命周期阶段**：实现 `IModuleLifecycleContributor`，在模块的 `PreConfigureServices` 里 `PreConfigure<XiHanModuleLifecycleOptions>(o => o.Contributors.Add<MyContributor>())`。
- **插件模块**：通过 `options.PlugInSources.AddFolder(path)` / `AddFile(...)` / `AddTypes(...)` 引入编译期未引用的模块。
- **注册/暴露/激活拦截**：通过 `IOnServiceRegistredContext` / `IOnServiceExposingContext` / `IOnServiceActivatedContext` 相关的 action list 扩展（如统一为服务加拦截器）。
- **异常订阅**：实现 `IExceptionSubscriber` 并注册，即可接入 `IExceptionNotifier.NotifyAsync` 通知链（做告警/上报）。

## 注意事项与最佳实践

- **`ServiceConfigurationContext` 只在服务配置三阶段可用**：`XiHanModule.ServiceConfigurationContext` 在配置完成后会被置空，初始化阶段访问会抛 `XiHanException`。初始化阶段请用 `ApplicationInitializationContext.ServiceProvider`。
- **服务注册阶段不要解析服务**：此时容器尚未构建，只能操作 `IServiceCollection`；需要实例的逻辑放到 `OnApplicationInitialization`。
- **异步引导必须成对**：调用 `ConfigureServicesAsync` 前必须先把 `SkipConfigureServices` 置 `true`（`XiHanApplicationFactory` 的 `Async` 工厂已自动处理），否则会因重复配置抛 `InitializationException`。不要在同一应用上既走同步又走异步配置。
- **约定式注册的隐性规则**：既无 `[Dependency]` 也未实现三种标记接口的具体类**不会被注册**（默认生命周期为 `null`）。若第三方类型需要注册，用特性或手写注册。
- **`SkipAutoServiceRegistration`**：模块置此为 `true` 会跳过它自身程序集的约定扫描（适合纯抽象/占位模块），但仍会执行其生命周期钩子。
- **`GetApplicationBuilder/GetConfiguration/GetEnvironment` 属 Web 层**：纯 Core 应用没有这些便捷方法，别按 Web 文档习惯直接调用。
- **`[DependsOn]` 只需声明直接依赖**：传递依赖由框架递归发现；重复声明由 `AddIfNotContains` 去重，`XiHanModuleHelper` 日志会标注 `[已跳过-重复加载]`。
- **关机为逆序**：`OnApplicationShutdown` 按模块**逆拓扑序**执行，释放资源时可依赖"我依赖的模块尚未关闭"这一顺序保证。

## 依赖模块

- [XiHan.Framework.Metadata](./metadata) — 框架元数据（启动 Logo / 版本摘要）。
- [XiHan.Framework.Utils](./utils) — 通用工具库（`Guard`、集合扩展、`LogHelper` 等）。
- 第三方：`Microsoft.Extensions.Hosting`（主机集成）、`Microsoft.Extensions.Localization`（本地化基座）；隐式使用 `Microsoft.Extensions.DependencyInjection/Options/Configuration/Logging`。

## 相关模块

- [模块化概念](../guide/modularity) · [生命周期概念](../guide/lifecycle) · [依赖注入概念](../guide/dependency-injection)
- [XiHan.Framework.Castle](./castle) — 基于 Castle 的动态代理落地（实现本包的 `IXiHanInterceptor`）。
- [XiHan.Framework.Web.Core](./web-core) — 提供 Web Host 引导（`AddApplicationAsync` / `InitializeApplicationAsync`）与 `ApplicationInitializationContext` 的 `GetApplicationBuilder/GetConfiguration/GetEnvironment` 扩展。
- [XiHan.Framework.Utils](./utils) · [XiHan.Framework.Metadata](./metadata)
