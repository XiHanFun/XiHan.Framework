# 模块生命周期

每个模块在框架启动到关闭的过程中，会依次经过若干**生命周期钩子**。理解它们，你就知道"该把代码写在哪个方法里"。

## 两个阶段

框架启动分为两大阶段，钩子分布在其中：

```text
① 服务注册阶段（构建 DI 容器）          ② 应用初始化阶段（装配中间件管道）
┌──────────────────────────┐          ┌──────────────────────────────────┐
│ PreConfigureServices     │          │ OnPreApplicationInitialization   │
│ ConfigureServices        │   ──►     │ OnApplicationInitialization      │
│ PostConfigureServices    │          │ OnPostApplicationInitialization  │
└──────────────────────────┘          └──────────────────────────────────┘
                                                      │
                                       应用运行……     ▼
                                       ┌──────────────────────────────────┐
                                       │ OnApplicationShutdown（关闭时）  │
                                       └──────────────────────────────────┘
```

- **阶段 ①** 在 `AddApplicationAsync<T>()` 时执行 —— 这时还没有 `ServiceProvider`，只能往 `IServiceCollection` 里**注册**服务。
- **阶段 ②** 在 `InitializeApplicationAsync()` 时执行 —— 这时容器已建好，可以**解析服务、接入中间件**。

> 每个钩子都有**同步**和**异步**两个版本（如 `ConfigureServices` / `ConfigureServicesAsync`）。重写任一个都可以，默认同步版本会被异步版本调用。

## 钩子说明

| 钩子 | 阶段 | 典型用途 |
| --- | --- | --- |
| `PreConfigureServices` | ① | 在正式注册前预配置，供后续模块读取（`PreConfigure<T>`） |
| `ConfigureServices` | ① | **最常用**。注册自己的服务、配置选项 |
| `PostConfigureServices` | ① | 所有模块注册完后做收尾/覆盖（`PostConfigure<T>`） |
| `OnPreApplicationInitialization` | ② | 中间件管道装配前的准备 |
| `OnApplicationInitialization` | ② | **最常用**。接入中间件、映射端点 |
| `OnPostApplicationInitialization` | ② | 初始化完成后的收尾工作 |
| `OnApplicationShutdown` | ② | 应用关闭时释放资源、刷写缓冲 |

**执行顺序**：同一个钩子会按[模块拓扑排序](./modularity)的顺序，在所有模块上依次调用——底层模块先执行，你的应用模块最后执行。`OnApplicationShutdown` 是唯一的例外：`ModuleManager` 关闭时按**逆序**遍历模块列表，你的应用模块先关闭、底层模块最后关闭，与其余六个钩子的正向顺序相反，使资源释放顺序与初始化顺序对称。

## 注册服务：`ConfigureServices`

在阶段 ① 里，通过 `context.Services`（标准 `IServiceCollection`）注册服务：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.AddSingleton<IClock, SystemClock>();

    // 配置选项：XiHanModule 提供了便捷的 Configure<T>
    Configure<MyOptions>(options =>
    {
        options.Timeout = TimeSpan.FromSeconds(30);
    });
}
```

## 接入中间件：`OnApplicationInitialization`

在阶段 ② 里，通过 `context.GetApplicationBuilder()` 拿到 `IApplicationBuilder`，接入中间件：

```csharp
public override void OnApplicationInitialization(ApplicationInitializationContext context)
{
    var app = context.GetApplicationBuilder();

    app.UseMyMiddleware();
    // 也可以读配置、解析服务：
    // var opt = context.ServiceProvider.GetRequiredService<IOptions<MyOptions>>().Value;
}
```

`ApplicationInitializationContext`（`XiHan.Framework.Core`）自身只暴露一个 `ServiceProvider` 属性；`GetApplicationBuilder()`、`GetConfiguration()`、`GetEnvironment()`、`GetLoggerFactory()` 都是 `XiHan.Framework.Web.Core` 追加的扩展方法（`ApplicationInitializationContextExtensions`），随 Web 相关模块（如 `XiHan.Framework.Web.Api`）一并引入，方便你按环境/配置决定行为。

## 真实示例：Web.Api 的中间件管道

`XiHanWebApiModule` 就是在 `OnApplicationInitialization` 里装配了整条 Web 中间件管道，顺序经过精心编排：

```text
UseForwardedHeaders            反向代理转发头还原（必须最前）
  → TraceId                    分配请求追踪 ID
  → RequestCulture             请求文化（i18n）
  → RequestContext             请求上下文
  → ExceptionLogging           异常日志
  → RequestLogging             请求日志
  → UseRouting                 路由
  → RateLimiter / CircuitBreak 限流 / 熔断（按配置开关）
  → UseCors                    跨域
  → 本地对象存储静态文件         公开资源匿名直链
  → ApiLogging                 API 日志
  → OpenApiSecurity            OpenAPI 安全
  → UseAuthentication          认证
  → TenantResolve              租户解析
  → UseAuthorization           授权
  → UseEndpoints               映射控制器 + OpenAPI
```

这条管道你**不需要自己写**——引用 `XiHan.Framework.Web.Api` 后它就自动生效了。这正是模块化的价值：一整套编排好的基础设施，一行 `[DependsOn]` 即可获得。

## 下一步

- [依赖注入](./dependency-injection)：约定式注册与选项模式
- [动态 API](./dynamic-api)：应用服务如何变成 REST 接口
- [模块系统](./modularity)：回顾 `[DependsOn]` 与拓扑排序
