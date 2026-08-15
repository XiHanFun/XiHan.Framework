# XiHan.Framework.Web.Api

> 动态 API + 完整 Web 中间件管道：把标注 `[DynamicApi]` 的应用服务自动暴露为 REST 接口，并一次性装配转发头还原、追踪、文化、异常/请求/API 日志、限流、熔断、CORS、认证、多租户解析、授权与 OpenAPI 安全一整条管线。

- **NuGet**：`XiHan.Framework.Web.Api`
- **模块类**：`XiHanWebApiModule`
- **所在层**：Web 层
- **关键依赖**：`Microsoft.AspNetCore.OpenApi`（OpenAPI 文档生成）；SDK 为 `Microsoft.NET.Sdk.Web`。

## 概述

XiHan.Framework.Web.Api 是构建 HTTP 后端的主力包，做两件核心事：

1. **动态 API**——你只写继承 `IApplicationService` 且（类或方法级）标注 `[DynamicApi]` 的应用服务，框架在启动时扫描程序集、为它们生成动态控制器与路由，你无需手写 Controller。默认约定「剥离动词」路由（如 `CreateXxxAsync` → `POST /api/.../Xxx`）。
2. **中间件管道**——模块在 `OnApplicationInitialization` 里按固定顺序装配好一整套 Web 中间件：转发头还原、TraceId、请求文化、请求上下文、异常/请求/API 日志、路由、限流、熔断、CORS、本地对象存储静态文件、OpenAPI 安全、认证、多租户解析、授权、端点映射。

新手可以把它理解为：引用它 + 写应用服务 = 一个带统一响应、日志、JWT 认证、授权、多租户、OpenAPI 的 REST API。

## 何时使用

- 想把应用服务（`AppService`）直接变成 REST 接口，不想手写控制器与路由。
- 需要一套现成的 Web 管道：统一异常/请求/API 访问日志、TraceId、请求文化、CORS、JWT 认证、授权、多租户解析。
- 需要入站保护：可按配置开关开启限流（`RateLimiter`）与服务端熔断（CircuitBreaking）。
- 处于反向代理（nginx/网关）之后，需要还原真实 scheme/host/客户端 IP（`UseForwardedHeaders`）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Web.Api
```

```csharp
[DependsOn(typeof(XiHanWebApiModule))]
public class MyModule : XiHanModule { }
```

模块声明 `[DependsOn(XiHanWebCoreModule, XiHanMultiTenancyModule, XiHanSerializationModule, XiHanAuditingModule)]`。`ConfigureServices` 里配置 `ForwardedHeadersOptions`（信任 `X-Forwarded-For/Proto/Host`）并调用 `AddXiHanWebApi` + `AddXiHanRateLimiting` + `AddXiHanCircuitBreaking`；`OnApplicationInitialization` 装配下述完整中间件管道。

`AddXiHanWebApi(configuration)` 内部依次装配：

- `AddXiHanWebApiSecurity`：`IRequestContextAccessor`（单例）、`ITraceIdProvider`（Scoped）、绑定 `XiHanOpenApiSecurityOptions`、`IOpenApiSecurityClientStore`（默认 `DefaultOpenApiSecurityClientStore`）。
- `AddXiHanWebApiCors`：绑定 `XiHanCorsOptions` 并按其构建默认 CORS 策略。
- `AddXiHanWebApiAuth`：按 `XiHan:Authentication:Jwt` 装配 JWT Bearer；按 `XiHan:Web:Api:Auth` 装配授权（可全局要求登录）；OAuth 启用时追加 `ExternalCookie` 临时 scheme。
- `AddXiHanWebApiLogging`：仅注册本包四个 MVC 过滤器（`XiHanActionLoggingFilter`、`XiHanApiResponseResultFilter`、`XiHanCacheFilter`、`XiHanUnitOfWorkFilter`）。五类日志的 `ILogQueue<>`（单例）+ 五个 `HostedService` Worker + 五个 Pipeline（Scoped）+ 五个 `Null*LogWriter` 默认写入器已下沉至 [XiHan.Framework.Auditing](./auditing) 包的 `AddXiHanAuditing`（随 `XiHanAuditingModule` 依赖自动装配），由上层应用覆盖写入器落库。
- `AddXiHanWebApiMvc`：`AddDynamicApi`（动态 API 发现与约定）+ `AddControllers`（按「日志 → 统一响应 → 缓存 → 工作单元」由外到内挂四个全局过滤器、统一 JSON、模型校验失败工厂）+ 两个租户贡献者 + `AddOpenApi`。

## 中间件管道顺序（`OnApplicationInitialization`）

框架按下列**固定顺序**装配（第 8、9 步按配置开关）。顺序是语义关键——转发头必须最前、限流/熔断在鉴权前、静态文件在鉴权前、租户解析在认证后授权前：

| # | 中间件 | 作用一句话 |
| --- | --- | --- |
| 1 | `UseForwardedHeaders` | 依据 `X-Forwarded-Proto/Host/For` 还原真实 scheme/host/客户端 IP；必须最前，否则后续读 scheme/host/IP 的中间件全错。 |
| 2 | `XiHanTraceIdMiddleware` | 取请求头 `X-Trace-Id`（无则用 `TraceIdentifier`），写入 `HttpContext.Items` 并回写响应头，贯穿全链路。 |
| 3 | `UseXiHanRequestCulture` | 解析请求文化（`X-Language` 等）并设置到当前线程，使后续管线（含控制器/响应过滤器）在请求文化下执行。 |
| 4 | `XiHanRequestContextMiddleware` | 汇聚 TraceId/文化/用户/租户/客户端 IP/UA/路径/方法/起始时间为 `RequestContext`，写入 `IRequestContextAccessor`。 |
| 5 | `XiHanExceptionLoggingMiddleware` | 管线级异常日志采集（异常队列/告警）。 |
| 6 | `XiHanRequestLoggingMiddleware` | 请求起止/访问日志采集。 |
| 7 | `UseRouting` | 端点路由匹配。 |
| 8 | `UseRateLimiter` | **仅当 `XiHan:Web:RateLimiting:IsEnabled=true`**：按客户端 IP 固定窗口限流，置于路由后、鉴权前，尽早拒绝超额。 |
| 9 | `XiHanCircuitBreakingMiddleware` | **仅当 `XiHan:Web:CircuitBreaking:IsEnabled=true`**：服务端过载熔断，置于限流后、鉴权前，过载时快速失败。 |
| 10 | `UseCors` | 应用默认 CORS 策略（来自 `XiHanCorsOptions`）。 |
| 11 | 本地对象存储静态文件 | 依 `XiHan:ObjectStorage:Local` 的 `RootPath`/`UrlPrefix` 提供静态文件；Web.Api 无配置时回退 `wwwroot/uploads` → `/uploads`，应用应显式配置路径；置于鉴权前，使头像等公开资源可匿名直链。 |
| 12 | `XiHanApiLoggingMiddleware` | 接口访问日志：仅当请求携带 OpenApi 安全头时记录（AccessKey/Signature）。 |
| 13 | `XiHanOpenApiSecurityMiddleware` | 对受保护路径做签名/内容签名/加密/防重放校验（默认关闭）。 |
| 14 | `UseAuthentication` | JWT Bearer 认证，填充 `HttpContext.User`。 |
| 15 | `XiHanTenantResolveMiddleware` | 按贡献者（Header/QueryString）解析租户并 `ICurrentTenant.Change`，置于认证后（可用用户信息）、授权前（授权可依租户）。 |
| 16 | `UseAuthorization` | 授权（可全局 `FallbackPolicy` 要求已认证用户）。 |
| 17 | `UseEndpoints` | `MapControllers()`（含动态控制器，走特性路由）+ `MapOpenApi().AllowAnonymous()`。 |

## 工作原理：动态 API

### 发现与注册

- **应用服务契约**：`IApplicationService`（在 [Application.Contracts](./application-contracts) 包，继承 `IRemoteService`）。实现它的非抽象类会被视为候选应用服务。
- **程序集发现**（`DynamicApiServiceCollectionExtensions.AddDynamicApi`）：遍历 `AppDomain.CurrentDomain.GetAssemblies()`，跳过系统程序集（`Microsoft.*`/`System.*`/`Serilog`/`Newtonsoft` 等），凡含 `IApplicationService` 实现类型的程序集，注册为 MVC `AssemblyPart`。
- **控制器生成**（`DynamicApiControllerFeatureProvider`）：作为 `IApplicationFeatureProvider<ControllerFeature>`，对每个应用服务类型经 `DynamicApiControllerFactory.CreateControllerType` 生成动态控制器类型，加入 MVC 控制器特性集合。生成成功/失败数会记入日志。
- **约定**（`IDynamicApiConvention` → `DefaultDynamicApiConvention`）：决定控制器名、动作名、HTTP 方法、路由模板、API 版本等。约定与选项以 `TryAddSingleton` 注册，可被外部自定义实现覆盖。

### 默认命名与路由约定

- **控制器名**：服务类型名去掉服务后缀（`ServiceSuffixes` 默认 `["ApplicationService", "AppService", "Service"]`）。如 `UserAppService` → `User`。
- **动作名 / HTTP 方法**：先去 `Async` 后缀；默认「剥离动词」（`PreserveRoutePredicate=false`）——即去掉方法名前缀动词得到资源段，动词映射为 HTTP 方法。前缀→方法映射（`HttpMethodConventions`）：
  - GET：`Get` `Retrieve` `Fetch` `Find` `Query` `List` `Search`
  - POST：`Create` `Add` `Insert`
  - PUT：`Update` `Edit` `Modify`
  - DELETE：`Delete` `Remove` `Destroy`
  - PATCH：`Patch` `PartialUpdate`
  - 无匹配前缀时默认 `POST`。
- **路由模板**：`{DefaultRoutePrefix}[/v{version}][/命名空间或模块段]/{Controller}/{Action}[/{id}]`。默认前缀 `api`，PascalCase、无分隔符。`GET`/`DELETE` 的 id 参数、`PUT`/`PATCH` 的首个 id 参数会拼进路由（`{id}`）；显式绑定（`[FromRoute]` 等）优先。
- 例：`UserAppService.CreateAsync(CreateUserDto)` → `POST /api/User`；`GetByIdAsync(long id)` → `GET /api/User/{id}`；`UpdateAsync(long id, UpdateUserDto)` → `PUT /api/User/{id}`。

> 注意「剥离动词」的历史陷阱：`DynamicApiAttribute` 的路由 bool（`PreserveRoutePredicate` 等）用可空后备字段区分「未设置」，只为 `Group`/`Tag` 标注特性时不会以编译期默认值覆盖全局配置（该陷阱曾导致整类路由 404）。全局约定在 `AddXiHanWebApiMvc` 中显式设 `PreserveRoutePredicate=false`，全部前端按此对接。

### `[DynamicApi]` 特性（定义在 [Application](./application) 包）

`[AttributeUsage(Class | Method, AllowMultiple = true)]`。合并优先级：**全局配置 < 类级特性 < 方法级特性**；同层级按 `Order` 升序合并（后写覆盖先写）。关键字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `IsEnabled` | `bool` | 是否启用；任一层级 `false` 即禁用对应 API。 |
| `RouteTemplate` | `string` | 自定义路由模板（方法级优先，类级兜底）。 |
| `Name` | `string` | 自定义 API 名（覆盖动作名）。 |
| `Version` | `string` | API 版本（可叠加，不带 `v` 前缀）。 |
| `Description` | `string` | API 描述。 |
| `Tag` / `Group` / `GroupName` | `string` | OpenAPI 文档标签 / 分组键 / 分组显示名。 |
| `Order` | `int` | 同层级合并顺序，越大优先级越高。 |
| `PreserveRoutePredicate` / `UsePascalCaseRoutes` / `UseLowercaseRoute` | `bool` | 路由格式开关；均以可空后备字段区分「未设置」（读取用对应 `*OrNull` 属性），未设置回退下一层级。 |
| `VisibleInApiExplorer` | `bool` | 是否在 API 浏览器/文档中显示。 |
| `CustomProperties` | `string` | 自定义键值属性（`key=value`，可叠加）。 |

### 参数来源推断与授权特性透传

`DynamicApiParameterAnalyzer`（`DynamicApi/ParameterAnalysis`）在生成控制器动作前逐参数分析，决定其绑定来源（`ParameterSource`：`Route`/`Query`/`Body`/`Form`/`Header`/`Services`），按以下优先级：

1. **显式绑定优先**：参数已标 `[FromRoute]`/`[FromQuery]`/`[FromBody]`/`[FromForm]`/`[FromHeader]`/`[FromServices]` 时直接采用，并保留其 `Name`。
2. **基础设施类型 → `Services`**：`CancellationToken`/`HttpContext`/`ClaimsPrincipal`/`IServiceProvider` 等特殊类型自动注入，不计入客户端必填参数。
3. **表单文件 → `Form`**：参数类型（含嵌套属性/集合）包含 `IFormFile`/`IFormFileCollection` 时判为表单来源，动作方法自动加 `[Consumes("multipart/form-data")]`，因此文件上传应用服务方法无需手写控制器即可工作。
4. **Id 形参 → `Route`**：参数名为 `id` 或以 `Id`/`ID` 结尾、类型为常见标识类型（`long`/`int`/`Guid`/`string` 等）或自定义 Id 值对象（类型名以 `Id` 结尾或实现 `IParsable<TSelf>`）时：`GET`/`DELETE`/`HEAD` 直接拼入路由；`PUT`/`PATCH` 仅第一个此类参数拼入路由，其余降级为 `Query`。
5. **唯一复杂类型 → `Body`**：非 `GET`/`DELETE`/`HEAD` 方法下，首个复杂类型（class/record）参数判为请求体；若已有一个 Body 参数，后续复杂类型参数降级为 `Query`（避免多 Body 冲突）。
6. **兜底 → `Query`**：其余简单类型/集合类型参数走查询字符串。

同一 HTTP 方法 + 路由模板出现重复映射（多为重载方法未显式区分路由）时，控制器生成阶段直接抛 `DynamicApiException` 报冲突，而非放任运行期路由歧义。

应用服务类/方法上的 `[Authorize]`（含 `Policy`/`Roles`/`AuthenticationSchemes`）与 `[AllowAnonymous]` 会被原样复制到生成的动态控制器/动作上，因此可直接在应用服务上做声明式鉴权，无需为需要特殊授权策略的接口改写手工控制器。

### `DynamicApiOptions` 全局配置

在 `AddXiHanWebApiMvc` 中以代码固定配置（非从 appsettings 读取）；如需覆盖可 `ConfigureDynamicApiConventions` / `ConfigureDynamicApiRoutes`。真实字段与框架默认：

| 字段 | 类型 | 框架默认 | 含义 |
| --- | --- | --- | --- |
| `IsEnabled` | `bool` | `true` | 是否启用动态 API。 |
| `DefaultRoutePrefix` | `string` | `"api"` | 默认路由前缀。 |
| `DefaultApiVersion` | `string?` | `null` | 默认 API 版本（字符串数字，不带 `v`）。 |
| `EnableApiVersioning` | `bool` | `true` | 是否启用版本控制（有版本时路由插 `v{version}`）。 |
| `EnableBatchOperations` | `bool` | `true` | 是否启用批量操作。 |
| `MaxBatchSize` | `int` | `100` | 批量操作上限。 |
| `RemoveServiceSuffix` | `bool` | `true` | 是否移除服务名后缀。 |
| `ServiceSuffixes` | `List<string>` | `["ApplicationService","AppService","Service"]` | 要移除的后缀列表。 |
| `Conventions` | `DynamicApiConventionOptions` | 见上「约定」 | 谓词/大小写/分隔符与 `HttpMethodConventions`。 |
| `Routes` | `DynamicApiRouteOptions` | `UseNamespaceAsRoute=false`、`UseModuleNameAsRoute=false` | 命名空间/模块是否入路由、排除前缀、模块名正则。 |

> `AddXiHanWebApiMvc` 里实际生效的约定：`PreserveRoutePredicate=false`、`UsePascalCaseRoutes=true`、`UseLowercaseRoutes=false`、`RouteSeparator=""`、命名空间与模块均不入路由。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanWebApiModule` | 模块类，装配服务与整条中间件管道。 |
| `[DynamicApi]`（Application 包） | 标注应用服务/方法以配置动态 API 行为。 |
| `IApplicationService`（Application.Contracts 包） | 应用服务标记接口，实现即被动态发现。 |
| `DynamicApiControllerFeatureProvider` / `DynamicApiControllerFactory` | 动态控制器发现与生成。 |
| `IDynamicApiConvention` / `DefaultDynamicApiConvention` | 动态路由/命名约定契约与默认实现。 |
| `XiHanController` | 控制器基类（继承 MVC `Controller`），提供 `Success/Success<T>/Fail/Fail<T>` 统一响应助手。 |
| `XiHanApiResponseResultFilter` | 统一响应过滤器：包装正常返回、把未处理异常映射为统一 `ApiResponse`（异常→状态码单一来源 `MapException`）。 |
| `XiHanActionLoggingFilter` | 动作级日志过滤器（操作日志/告警）。 |
| `XiHanCacheFilter` | 动作级缓存过滤器：按原始应用服务方法上的 `[Cacheable]` / `[CacheEvict]` 命中缓存或清除缓存，补上动态代理够不到的 HTTP 入口。 |
| `XiHanUnitOfWorkFilter` | 动作级工作单元过滤器：按原始应用服务方法上的 `[UnitOfWork]` 开启工作单元，动作抛异常时不提交、由释放时回滚。 |
| `OriginalMethodResolver` | 由动态控制器动作回查原始应用服务方法（供上述两个过滤器读取业务特性）。 |
| `[IgnoreApiResponse]` | 标注类/方法跳过统一响应包装（如自定义流式响应）。 |
| `IRequestContextAccessor` / `RequestContext` | 请求级上下文访问器与模型（TraceId/文化/用户/租户/IP/UA/路径/方法/起始时间）。 |
| `ITraceIdProvider` / `HttpTraceIdProvider` | TraceId 提供器。 |
| `XiHanTenantResolveMiddleware` + `HeaderTenantResolveContributor` / `QueryStringTenantResolveContributor` | 多租户解析中间件与两个内置贡献者。 |
| `XiHanOpenApiSecurityMiddleware` / `IOpenApiSecurityClientStore` / `OpenApiSecurityClient` | OpenAPI 安全中间件、客户端存储与客户端模型。 |
| `XiHanCircuitBreakerState` | 熔断器状态（滑动窗口统计、Closed/Open/HalfOpen 状态机，多实例各自独立）。 |

## 配置

### CORS：`XiHan:Web:Api:Cors`（`XiHanCorsOptions`）

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `AllowedOrigins` | `List<string>` | `[]` | 允许来源；`AllowAnyOrigin=true` 时忽略。 |
| `AllowAnyOrigin` | `bool` | `false` | 允许任意来源（与 `AllowCredentials` 互斥）。 |
| `AllowedMethods` / `AllowAnyMethod` | `List<string>` / `bool` | `[]` / `true` | 允许的方法。 |
| `AllowedHeaders` / `AllowAnyHeader` | `List<string>` / `bool` | `[]` / `true` | 允许的请求头。 |
| `AllowCredentials` | `bool` | `true` | 是否允许携带凭据（`AllowAnyOrigin` 时自动不启用）。 |
| `ExposedHeaders` | `List<string>` | `[]` | 暴露给客户端的响应头。 |
| `PreflightMaxAgeSeconds` | `int` | `0` | 预检缓存秒数，0 表示不设置。 |

### 认证授权：`XiHan:Web:Api:Auth`（`XiHanWebAuthOptions`）

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `RequireAuthenticatedUser` | `bool` | `false` | 为 `true` 时设置全局 `FallbackPolicy`，所有未标 `[AllowAnonymous]` 的端点均需登录；仅在 `XiHan:Authentication:Jwt:SecretKey` 有效时生效，否则抛异常。 |
| `SignalRHubPathPrefix` | `string` | `"/hubs"` | 该前缀下的请求允许从 query string `access_token` 提取 JWT（WebSocket/SSE 无法带 Authorization 头）。 |

> JWT 参数来自 `XiHan:Authentication:Jwt`（`JwtOptions`，见 [Authentication](./authentication)）：`SecretKey`/`Issuer`/`Audience`/`ValidateIssuer`/`ValidateAudience`/`ValidateLifetime`/`ClockSkewMinutes`。JWT 过期时会回写响应头 `Token-Expired: true`。

### 限流：`XiHan:Web:RateLimiting`（`XiHanRateLimitingOptions`）

基于 ASP.NET Core 内置 `RateLimiter`，按客户端 IP 固定窗口全局限流；超额返回 429 + `Retry-After`。默认关闭。

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `IsEnabled` | `bool` | `false` | 是否启用（同时控制中间件是否接入）。 |
| `PermitLimit` | `int` | `300` | 每窗口允许请求数。 |
| `WindowSeconds` | `int` | `60` | 窗口秒数。 |
| `QueueLimit` | `int` | `0` | 排队上限（0=超额立即拒绝）。 |
| `ExemptPathPrefixes` | `string[]` | `["/health"]` | 豁免路径前缀。 |

### 熔断：`XiHan:Web:CircuitBreaking`（`XiHanCircuitBreakingOptions`）

服务端过载保护熔断器：滑动窗口统计 5xx/未处理异常占比，超阈值熔断（Open，返回 503 + `Retry-After`），到期转半开（HalfOpen）放行少量探测，探测全成功恢复闭合、任一失败重新熔断。默认关闭；多实例各自独立统计、无分布式协调。

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `IsEnabled` | `bool` | `false` | 是否启用（同时控制中间件是否接入）。 |
| `WindowSeconds` | `int` | `60` | 滑动统计窗口秒数。 |
| `MinimumRequests` | `int` | `50` | 窗口内样本下限，不足不评估（避免低流量误判）。 |
| `FailureRateThreshold` | `double` | `0.5` | 失败率阈值（失败=5xx 或未处理异常）。 |
| `BreakSeconds` | `int` | `30` | 熔断持续秒数，到期转半开。 |
| `HalfOpenMaxProbes` | `int` | `5` | 半开态放行的最大探测请求数。 |
| `ExemptPathPrefixes` | `string[]` | `["/health"]` | 豁免路径（既不被拦截也不参与统计）。 |

### OpenAPI 安全：`XiHan:Web:Api:OpenApiSecurity`（`XiHanOpenApiSecurityOptions`）

对开放接口做签名（HMAC/RSA/SM2）、内容签名（SHA256/512）、请求/响应加密（AES-CBC）与防重放（时间戳窗 + Nonce，分布式缓存或本地内存兜底）。默认关闭（`IsEnabled=false`）。主要字段：

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `IsEnabled` | `bool` | `false` | 是否启用安全中间件。 |
| `AllowUnsignedRequests` | `bool` | `false` | 灰度开关：无安全头请求直接放行。 |
| `RequireContentSignature` | `bool` | `true` | 是否必须校验内容签名。 |
| `EnableReplayProtection` | `bool` | `true` | 是否启用防重放（Nonce）。 |
| `TimestampToleranceSeconds` / `NonceExpireSeconds` | `int` | `300` / `300` | 时间戳误差 / Nonce 存活秒数。 |
| `MaxRequestBodySize` | `int` | `2*1024*1024` | 可读取的最大请求体字节数。 |
| `EnableResponseEncryption` / `EncryptResponseByDefaultWhenRequestEncrypted` | `bool` | `true` / `false` | 是否允许响应加密 / 请求加密时是否默认加密响应。 |
| `DefaultSignatureAlgorithm` | `string` | `"HMACSHA256"` | 默认签名算法（支持 HMACSHA256/512、RSASHA256、SM2）。 |
| `DefaultContentSignatureAlgorithm` | `string` | `"SHA256"` | 默认内容签名算法。 |
| `DefaultEncryptionAlgorithm` | `string` | `"AES-CBC"` | 默认加密算法。 |
| `AllowLegacy*Algorithms` | `bool` | `false` | 是否允许旧算法（HMACSHA1 / MD5 / BLOWFISH）。 |
| `ProtectedPathPrefixes` | `List<string>` | `["/api"]` | 受保护路径（空=全路径）。 |
| `IgnoredPathPrefixes` | `List<string>` | `["/openapi","/swagger","/health"]` | 忽略路径。 |
| `Clients` | `List<OpenApiSecurityClientOptions>` | `[]` | 配置内客户端（AccessKey/SecretKey/公钥/IP 白名单等），默认存储可直接读取。 |

### 多租户解析：`XiHan:MultiTenancy:Resolve`（`XiHanTenantResolveOptions`，来自 MultiTenancy 包）

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `EnableHeaderResolve` | `bool` | `true` | 启用 Header 解析。 |
| `HeaderKeys` | `string[]` | `["X-Tenant-Id","x-tenant-id","TenantId"]` | 租户 Header 键。 |
| `EnableQueryStringResolve` | `bool` | `true` | 启用 QueryString 解析。 |
| `QueryStringKeys` | `string[]` | `["tenantId","tenant"]` | 租户 QueryString 键。 |
| `FallbackTenant` | `string?` | `null` | 未解析出租户时的兜底。 |

### 异步日志队列：`XiHan:Auditing:LogQueue`（`XiHanAuditingLogQueueOptions`，来自 [Auditing](./auditing) 包）

访问/操作/异常/API/登录五类日志经 `Channel` 队列 + `HostedService` Worker 异步落库；队列/Worker/Pipeline/Options 均由 `XiHanAuditingModule` 装配（Web.Api 依赖它自动引入），默认写入器为 `Null*LogWriter`（不落库），上层应用注册真实实现覆盖（后注册者胜出）。主要字段：`IgnoredPathPrefixes`（默认 `["/hubs"]`）、`EnableAccessLogQueue`/`EnableOperationLogQueue`/`EnableExceptionLogQueue`/`EnableApiLogQueue`/`EnableLoginLogQueue`（均默认 `false`）、`QueueCapacity`(10000)、`DropOnFull`(false)、`BatchSize`(100)、`BatchDelayMilliseconds`(200)。

> 注意：此配置节从早期版本的 `XiHan:Web:Api:LogQueue`（`XiHanWebApiLogQueueOptions`）迁移为 `XiHan:Auditing:LogQueue`（`XiHanAuditingLogQueueOptions`），随审计基础设施一并下沉至 Auditing 包；Web.Api 本身不再持有该 Options 类型。

### 示例 `appsettings.json`

```json
{
  "XiHan": {
    "Authentication": {
      "Jwt": {
        "SecretKey": "please-change-me-to-a-long-random-secret",
        "Issuer": "XiHan",
        "Audience": "XiHan.Client",
        "ClockSkewMinutes": 5
      }
    },
    "Web": {
      "Api": {
        "Cors": {
          "AllowedOrigins": ["https://app.example.com"],
          "AllowCredentials": true
        },
        "Auth": {
          "RequireAuthenticatedUser": true,
          "SignalRHubPathPrefix": "/hubs"
        }
      },
      "RateLimiting": { "IsEnabled": false, "PermitLimit": 300, "WindowSeconds": 60 },
      "CircuitBreaking": { "IsEnabled": false, "FailureRateThreshold": 0.5, "BreakSeconds": 30 }
    }
  }
}
```

## 使用示例

### Program.cs 最小启动

```csharp
var builder = WebApplication.CreateBuilder(args);

// 装配模块化应用（启动模块依赖 XiHanWebApiModule）
await builder.AddApplicationAsync<MyStartupModule>();

var app = builder.Build();

// 驱动模块 OnApplicationInitialization，装配完整中间件管道
await app.InitializeApplicationAsync();

await app.RunAsync();
```

```csharp
[DependsOn(typeof(XiHanWebApiModule))]
public class MyStartupModule : XiHanModule { }
```

### 一个动态 API 应用服务

```csharp
using Microsoft.AspNetCore.Mvc;
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Contracts.Services;

// 类级 [DynamicApi] 提供 OpenAPI 分组/标签；路由约定走全局默认（剥离动词）
[DynamicApi(Group = "Demo", GroupName = "示例", Tag = "用户")]
public sealed class UserAppService : IApplicationService
{
    // POST /api/User
    public Task<UserDto> CreateAsync(CreateUserDto input) => /* ... */;

    // GET /api/User/{id}
    public Task<UserDto> GetByIdAsync(long id) => /* ... */;

    // PUT /api/User/{id}
    public Task UpdateAsync(long id, UpdateUserDto input) => /* ... */;

    // DELETE /api/User/{id}
    public Task DeleteAsync(long id) => /* ... */;
}
```

无需写 Controller、无需手动路由；返回值经 `XiHanApiResponseResultFilter` 自动包装为统一 `ApiResponse`。需要覆盖单个方法的动词/路由时用方法级 `[DynamicApi(RouteTemplate = "...")]` 或 MVC 的 `[HttpGet]` 等；需要跳过统一包装（如流式下载）时标 `[IgnoreApiResponse]`。

### 手写控制器（可选）

需要完全掌控时，继承 `XiHanController` 使用统一响应助手：

```csharp
[ApiController]
[Route("api/[controller]")]
public class PingController : XiHanController
{
    [HttpGet]
    public IActionResult Get() => Success(new { pong = true });
}
```

## 扩展点 / 自定义

- **自定义动态路由约定**：实现 `IDynamicApiConvention` 并在动态 API 注册前放入 DI（`TryAddSingleton` 语义，先注册者胜出），可整体接管命名/路由生成。
- **覆盖日志落库**：以 `AddScoped` 覆盖 `IAccessLogWriter`/`IOperationLogWriter`/`IExceptionLogWriter`/`IApiLogWriter`/`ILoginLogWriter`（契约定义在 [Auditing](./auditing) 包，默认 `Null*` 由该包 `TryAddScoped` 注册，后注册的实现胜出）——这是把审计日志接入数据库的标准做法。
- **自定义租户来源**：向 `XiHanTenantResolveOptions.TenantResolvers` 追加实现 `TenantResolveContributorBase` 的贡献者（默认已含 Header、QueryString）。
- **OpenApi 安全客户端存储**：替换 `IOpenApiSecurityClientStore`（默认 `DefaultOpenApiSecurityClientStore` 读 `Options.Clients`），改为数据库/远程加载客户端凭据。
- **反向代理拓扑**：默认 `ForwardedHeadersOptions` 仅信任本机回环代理；反代在其它主机时，应用侧追加 `KnownProxies`/`KnownNetworks`。

## 注意事项与最佳实践

- **`UseForwardedHeaders` 必须最前**：一切读取 scheme/host/客户端 IP 的逻辑（路由、CORS、鉴权、限流、OAuth 回调绝对地址生成）都依赖它先执行；顺序不能调整。
- **限流/熔断在鉴权前**：故意如此——过载/超额时在进入昂贵的认证授权前就快速失败。二者默认关闭，生产开启前应按真实流量基线调参（熔断阈值过低或样本下限过小会误熔断）。
- **本地存储静态文件匿名可达**：静态文件中间件在鉴权前注册，`XiHan:ObjectStorage:Local` 下的资源可匿名直链——只放公开资源（头像等）。
- **模型校验失败为 400**：校验错误经 `InvalidModelStateResponseFactory` 返回 `ApiResponse.BadRequest`，具体错误放 `Data`（前端优先读 `Data`），不再写死 500。
- **异常映射单一来源**：业务/输入类异常归 4xx（`UserFriendlyException`/`BusinessException`/`ArgumentException`→400、`UnauthorizedAccessException`→401、`KeyNotFoundException`→404、`InvalidOperationException`→422），仅真正未预期异常归 500（不泄露内部细节）；映射由 `XiHanApiResponseResultFilter.MapException` 统一提供，异常中间件复用。
- **裸 `Stream` 返回值**：会被转成 `FileStreamResult`，避免被包装进 `ApiResponse` 后 JSON 序列化崩溃（`Stream.Handle` 为 `IntPtr` 不可序列化）；文件下载直接返回 `Stream` 或标 `[IgnoreApiResponse]`。

## 依赖模块

- 内部依赖：[XiHan.Framework.Web.Core](./web-core)、[XiHan.Framework.Application](./application)、[XiHan.Framework.MultiTenancy](./multitenancy)、[XiHan.Framework.Serialization](./serialization)、[XiHan.Framework.Localization](./localization)、[XiHan.Framework.Logging](./logging)、[XiHan.Framework.Auditing](./auditing)（`[DependsOn]` 声明的模块依赖；提供异步日志队列/管道/写入器基础设施）。
- 第三方核心：`Microsoft.AspNetCore.OpenApi`。

## 相关模块

- [XiHan.Framework.Web.Core](./web-core) — 本包的底座（HttpContext / 当前主体 / 客户端信息 / 托管环境）。
- [XiHan.Framework.Auditing](./auditing) — 访问/操作/异常/接口/登录五类日志与实体变更审计的采集队列、管道、写入器契约（本包中间件/过滤器是其主要生产方之一）。
- [XiHan.Framework.Web.Docs](./web-docs) — 在动态 API 之上提供 Scalar/Swagger UI 文档界面。
- [动态 API](../guide/dynamic-api) — 动态 API 的设计与约定说明。
- [模块生命周期](../guide/lifecycle) — `ConfigureServices` / `OnApplicationInitialization` 等生命周期钩子。
