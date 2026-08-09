# 统一响应与异常

框架让所有接口回同一个 JSON 信封：成功的业务对象被自动包进去，抛出的异常也被翻译成同一个形状。本章讲这个信封长什么样、失败怎么表达、哪些地方会绕开它。

## 概述

两个组件协作完成这件事，都在 `XiHan.Framework.Web.Api` 包里：

| 组件 | 类型 | 职责 |
| --- | --- | --- |
| `XiHanApiResponseResultFilter` | MVC 过滤器（`IAsyncResultFilter` + `IAsyncExceptionFilter`） | 包装正常返回；把未处理异常转成同一个信封 |
| `XiHanExceptionLoggingMiddleware` | 中间件 | 只记日志、回填状态码，**不产出响应体** |

信封本身 `ApiResponse` / `ApiResponse<T>` 与业务码枚举 `ApiResponseCodes` 定义在 `XiHan.Framework.Application.Contracts`，不依赖 ASP.NET Core——微服务、消息队列、RPC 场景可以复用同一套结果表达。

一次成功响应：

```json
{
  "code": 200,
  "message": "请求成功",
  "data": { "basicId": "1863928374", "name": "示例商品" },
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "timestamp": "2026-08-05T10:12:33.482+08:00",
  "isSuccess": true
}
```

信封字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `code` | `int` | 业务码，**恒为数字**。见下方说明 |
| `message` | `string` | 面向用户的提示，默认取业务码的 `DescriptionAttribute` |
| `data` | `any` | 成功时是业务数据；失败时承载错误明细 |
| `traceId` | `string?` | 请求追踪 ID，与响应头 `X-Trace-Id` 同源 |
| `timestamp` | `DateTimeOffset` | 服务端时间，默认 `DateTimeOffset.UtcNow`，输出时按 `X-Timezone` 换算 |
| `isSuccess` | `bool` | 只读计算属性，`(int)Code is >= 200 and < 300` |

`ApiResponse<T>` 用 `new T? Data` 遮蔽父类的 `object? Data`，给客户端代码生成和 OpenAPI 一个精确的数据形状。

::: tip `code` 为什么标在属性上而不是枚举上
`ApiResponse.Code` **属性**上标了 `[JsonConverter(typeof(NumericEnumConverter<ApiResponseCodes>))]`，而不是只在 `ApiResponseCodes` 枚举类型上标。

原因是 System.Text.Json 的转换器优先级：

```text
属性特性  >  JsonSerializerOptions.Converters 集合  >  类型特性
```

Web 管道会把 `JsonStringEnumConverter` 加进 `Converters` 集合（全局枚举转成员名），**只标在枚举类型上会被集合压过**，`code` 就变成 `"Success"` 这样的字符串了。标在属性上优先级最高，才能保证无论管道怎么配 `code` 恒为数字。

自己定义信封、或在 DTO 里暴露枚举又希望它输出数字时，照这个位置标。
:::

::: warning 判定成功一律用 `isSuccess`
两条理由：

1. 不要用「有没有 `data` 字段」判断——序列化开了 `WhenWritingNull`，`data` 为 null 时字段整个不出现；
2. 成功不止 `200` 一个码，`201` / `202` / `204` 同样是成功（`isSuccess` 已经按 `[200, 300)` 算好）。

需要区分具体情形时再比 `code` 的数值。
:::

## 安装与启用

信封与业务码随契约包引入：

```bash
dotnet add package XiHan.Framework.Application.Contracts
```

自动包装与异常翻译随 Web API 模块生效，不需要额外注册：

```csharp
[DependsOn(typeof(XiHanWebApiModule))]
public class MyAppModule : XiHanModule { }
```

`AddXiHanWebApiLogging` 把 `XiHanApiResponseResultFilter` 注册为 Scoped 服务，`AddXiHanWebApiMvc` 通过 `options.Filters.AddService<XiHanApiResponseResultFilter>()` 挂到全局过滤器链上；`XiHanExceptionLoggingMiddleware` 由模块的 `OnApplicationInitialization` 装进管道。

## 核心用法

### 直接返回业务对象

最常见的写法——什么都不用做，返回你的 DTO 即可：

```csharp
using XiHan.Framework.Application.Services;

public class ProductAppService : ApplicationServiceBase, IProductAppService
{
    public async Task<ProductDto> GetByIdAsync(long id)
    {
        // 直接返回 DTO，过滤器会包成 { code: 200, message: "请求成功", data: {...} }
        return await _productRepository.GetDtoAsync(id);
    }
}
```

### 手动构造信封

需要自己决定业务码时，用静态工厂——它保证 `Code` 与 `Message` 语义一致，不会出现「code 是 403、message 写着请求成功」这种搭配：

| 工厂 | 业务码 | 明细去向 |
| --- | --- | --- |
| `ApiResponse.Success(data, traceId)` | 200 | `Data` = 业务数据 |
| `ApiResponse<T>.Success(data, traceId = null)` | 200 | 强类型 `Data` |
| `ApiResponse.Created(data = null, traceId = null)` | 201 | `Data` |
| `ApiResponse.Continue()` | 100 | — |
| `ApiResponse.BadRequest(errorMessage = null, traceId = null)` | 400 | `Data` = 错误明细 |
| `ApiResponse.Unauthorized(errorMessage = null)` | 401 | `Data` |
| `ApiResponse.Forbidden()` | 403 | — |
| `ApiResponse.NotFound()` | 404 | — |
| `ApiResponse.UnprocessableEntity(errorMessage = null)` | 422 | `Data` |
| `ApiResponse.TooManyRequests()` | 429 | — |
| `ApiResponse.InternalServerError(errorMessage = null, traceId = null)` | 500 | `Data`（建议留空） |
| `ApiResponse.ServiceUnavailable(errorMessage = null)` | 503 | `Data` = 排查线索 |
| `ApiResponse.Failure(code, errorMessage = null, traceId = null)` | 任意 | `Data` |

`Failure` 是没有专用工厂时的通用入口，业务段的 10000+ 码都走它：

```csharp
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;

var response = ApiResponse.Failure(
    ApiResponseCodes.PermissionDenied,
    "缺少 product:delete 权限",
    traceId);
```

::: tip `Failure` 接受未定义的枚举值
`Message` 取自 `Enum.IsDefined(code) ? code.GetDescription() : "请求失败"`。传一个自定义的整数码不会抛异常，只是拿不到描述文案。
:::

控制器基类 `XiHanController` 提供了四个短写法：`Success<T>(data)`、`Success()`、`Fail<T>(message)`、`Fail()`。后两个固定产出 `400` + `ApiResponse.BadRequest`。

### 用异常表达失败

业务代码里更推荐抛异常，而不是层层往上传 `ApiResponse`——异常会被过滤器统一翻译，并且顺带记进异常日志：

```csharp
using XiHan.Framework.Core.Exceptions;

// 400：调用方输入或业务状态有问题，重试同样的请求仍会失败
throw new UserFriendlyException("商品已下架，无法加入购物车");

// 503：外部依赖不可达，依赖恢复后同样的请求即可成功
throw new ServiceUnavailableException(
    "向量库连接失败，请检查 Qdrant 服务状态",
    innerException: ex);
```

`UserFriendlyException` 与 `ServiceUnavailableException` 都派生自 `BusinessException`，构造参数除消息外还有 `code`、`details`、`innerException`、`logLevel`（前者默认 `Warning`，后者默认 `Error`）。原始基础设施异常放进 `innerException`，它会进日志但不会回给调用方。

### 不要信封

个别端点（文件下载、第三方回调、需要裸 JSON 的开放接口）打 `[IgnoreApiResponse]`：

```csharp
using XiHan.Framework.Web.Api.Filters;

[HttpGet("callback")]
[IgnoreApiResponse]
public IActionResult Callback() => Content("success", "text/plain");
```

特性可以打在方法或类上（`AllowMultiple = false`、`Inherited = true`），过滤器同时检查过滤器链与端点元数据。

::: warning `[IgnoreApiResponse]` 只免成功包装
异常路径不看这个特性。打了它的端点抛出未处理异常，仍然返回标准 `ApiResponse` JSON。
:::

## 关键机制

### 哪些返回值会被包装

`OnResultExecutionAsync` 按顺序判断，命中即停：

| 情形 | 处理 |
| --- | --- |
| 过滤器链或端点元数据上有 `IgnoreApiResponseAttribute` | 原样放行 |
| 结果是 `FileResult` | 原样放行 |
| 结果是 `ObjectResult` 且 `Value` 是 `Stream` | 转成 `FileStreamResult`（`application/octet-stream`），不包装 |
| `ObjectResult` / `JsonResult` 且 `Value` 已经是 `ApiResponse` | 不重复包装 |
| `ObjectResult` / `JsonResult` / `ContentResult` / `StatusCodeResult` / `EmptyResult` | 包装 |

::: tip 裸 `Stream` 必须被特判
`Stream.Handle` 是 `IntPtr`，直接 JSON 序列化会炸。所以返回 `Stream` 的下载接口不需要额外标注，过滤器自动转成文件流响应。
:::

状态码决定包成功还是失败：`>= 400` 走错误分支，其余走 `ApiResponse.Success`。另外 **`204` 会被归一成 `200`**（`NormalizeStatusCode`）——`204` 按 HTTP 规范不能带响应体，而信封本身就是响应体。

### 错误明细放哪

这是最容易搞混的一点：**同样是 400，明细的落点取决于响应是谁构造的。**

| 来源 | `message` | `data` |
| --- | --- | --- |
| `ApiResponse.BadRequest("库存不足")` 等工厂 | 业务码的通用描述（「请求错误」） | `"库存不足"` |
| 过滤器包装一个 `ObjectResult` + 字符串值 | 该字符串 | 省略 |
| 过滤器包装 `ProblemDetails` / `ValidationProblemDetails` | `Detail` → `Title` → 业务码描述 | 整个 `ProblemDetails` 对象 |
| 过滤器包装其它对象 | 业务码描述 | 该对象 |

异常路径走的是工厂，所以 `UserFriendlyException("库存不足")` 的文案在 `data` 里，`message` 是「请求错误」。

::: warning 客户端取错误提示的顺序
先读 `data`（具体错因），再回退 `message`（业务码的通用描述）。反过来会把所有 400 都显示成「请求错误」。
:::

模型校验失败同理：`InvalidModelStateResponseFactory` 把 `ModelState` 里的错误消息去重后用 `"; "` 拼接（一条都没有时用 `"请求参数校验失败"`），交给 `ApiResponse.BadRequest(message, traceId)`——所以校验明细也在 `data`。

### 异常怎么变成状态码

`XiHanApiResponseResultFilter.MapException` 是唯一的映射表，公开静态供中间件复用：

| 异常类型 | 状态码 | 构造 |
| --- | --- | --- |
| `ServiceUnavailableException` | 503 | `ServiceUnavailable(ex.Message)` |
| `UserFriendlyException` | 400 | `BadRequest(ex.Message)` |
| `BusinessException` | 400 | `BadRequest(ex.Message)` |
| `UnauthorizedAccessException` | 401 | `Unauthorized("未授权访问")` |
| `KeyNotFoundException` | 404 | `NotFound()` |
| `ArgumentException` | 400 | `BadRequest(ex.Message)` |
| `InvalidOperationException` | 422 | `UnprocessableEntity(ex.Message)` |
| 其它 | 500 | `InternalServerError()` |

::: danger 分支顺序有语义
`ServiceUnavailableException` 与 `UserFriendlyException` 都派生自 `BusinessException`，所以它们必须排在 `BusinessException` 前面。自定义异常派生自这三者之一时，同样要意识到 `switch` 是自上而下第一个匹配生效。
:::

`ArgumentNullException` / `ArgumentOutOfRangeException` 派生自 `ArgumentException`，一并落 400。

### 500 和 503 的区别

两者都是 5xx，但语义、`data` 策略、日志级别都不同：

| | 500 `InternalServerError` | 503 `ServiceUnavailable` |
| --- | --- | --- |
| 触发 | 未匹配任何分支的异常 | 显式抛 `ServiceUnavailableException` |
| 含义 | 服务端实现出了预期外的问题 | 外部依赖暂时不可用 |
| 重试 | 重试大概率还是失败 | 依赖恢复后同样的请求即可成功 |
| `data` | 工厂调用时不传参，**留空** | 允许带排查线索（是哪个依赖不可达） |
| 默认日志级别 | 中间件路径记 `LogError` | 异常自带 `LogLevel.Error` |

::: danger 503 的 data 也不是什么都能写
可以写「向量库连接失败」，不要写主机、端口、连接串、凭据等拓扑信息。500 的 `data` 一律留空——堆栈只进异常日志表，不回给调用方。
:::

区分这两者的实际收益在监控侧：告警规则可以据此把「用户操作错误」和「基础设施故障」分开，所以不要用 `UserFriendlyException` 代替 `ServiceUnavailableException` 去描述依赖故障。

### 响应与日志是两条独立的路

理解这一点，才能解释「为什么有的异常返回了 JSON，有的只有一个状态码」：

| 异常发生位置 | 谁产出响应 | 谁记日志 |
| --- | --- | --- |
| MVC 管线内（控制器、应用服务、仓储） | `XiHanApiResponseResultFilter.OnExceptionAsync` → 完整 `ApiResponse` JSON | `XiHanActionLoggingFilter` 记 `ILogger` 告警；过滤器调 `ExceptionLogReporter` 落异常日志表 |
| MVC 管线外、且位于异常日志中间件之下游（请求日志 / 限流熔断 / 认证 / 租户解析 / 会话闸门等） | 无响应体，`XiHanExceptionLoggingMiddleware` 仅在 `!Response.HasStarted` 时回填状态码 | 中间件按状态码分级：`>= 500` 记 `LogError`，否则 `LogWarning`；同样调 `ExceptionLogReporter` |

中间件里还会把异常挂到 `Activity.Current` 上（`AddException` + `SetStatus(ActivityStatusCode.Error, ...)`），OpenTelemetry 未激活时 `Activity.Current` 为 null，安全跳过。

`ExceptionLogReporter.ReportAsync` 是异常日志表的单一入口，组装 `ExceptionLogRecord` 交给 `IExceptionLogPipeline`：

- 拿不到 `IExceptionLogPipeline` 就静默返回；写入失败只记一条 `LogWarning`，绝不影响主流程。
- 请求头经 `LogSanitizer.MaskHeaders` 按头名整体掩码（`Authorization` / `Cookie` 等），路由与查询参数经 `LogSanitizer.MaskSensitiveData` 脱敏，请求体取的是中间件放进 `HttpContext.Items` 的已脱敏副本。
- 序列化结果超过 4000 字符会被截断。

::: warning 异常日志默认不落库
`IExceptionLogWriter` 的框架默认实现是 `NullExceptionLogWriter`。要真正入库，应用侧必须注册自己的实现覆盖它（`TryAddScoped` 不覆盖已注册项，所以在应用模块里直接注册即可）。
:::

### 消息本地化

`message` 会按请求文化（`X-Language`）解析，资源名固定为 `ApiResponse`，键是业务码的**枚举成员名**：

```json
{
  "resource": "ApiResponse",
  "culture": "en-US",
  "texts": {
    "Success": "Success",
    "BadRequest": "Bad request",
    "ServerError": "Internal server error"
  }
}
```

三条规则：

1. 正常响应：键 = `Code.ToString()`，缺工厂或缺键时回退 `DescriptionAttribute` 的中文描述。
2. 异常且携带 `BusinessException.LocalizableMessage`：本地化值优先覆盖 `Data`（当 `Data` 是字符串时），否则覆盖 `Message`——因为 4xx 工厂把用户消息放在 `Data`。
3. 异常且未携带本地化消息、状态码 `>= 500`：用固定键 `ServerError` 本地化通用提示。

带本地化消息的异常这样抛（第一个参数声明为 `object`，传 `ILocalizableString` 实例；第二个 `fallbackMessage` 是键缺失时的回退文案）：

```csharp
throw new UserFriendlyException(
    localizableMessage,
    fallbackMessage: "商品已下架");
```

::: tip 本地化查找永不让响应崩溃
`LocalizeApiResponseMessage` 对每个响应都会调用，内部 `try/catch` 全兜住，任何本地化异常都回退原文。
:::

## 业务码

`ApiResponseCodes` 分两段。**协议段 100–599** 与 HTTP 状态码同值同名，非 HTTP 场景（消息队列、RPC）也能复用；**业务段 10000+** 按千位分类留段扩展。

协议段：

| 段 | 成员 = 值 |
| --- | --- |
| 1xx | `Continue` = 100、`SwitchingProtocols` = 101 |
| 2xx | `Success` = 200、`Created` = 201、`Accepted` = 202、`NoContent` = 204 |
| 3xx | `MultipleChoices` = 300、`MovedPermanently` = 301、`Found` = 302、`NotModified` = 304 |
| 4xx | `BadRequest` = 400、`Unauthorized` = 401、`Forbidden` = 403、`NotFound` = 404、`MethodNotAllowed` = 405、`RequestTimeout` = 408、`Conflict` = 409、`Gone` = 410、`UnsupportedMediaType` = 415、`UnprocessableEntity` = 422、`Locked` = 423、`TooManyRequests` = 429 |
| 5xx | `InternalServerError` = 500、`NotImplemented` = 501、`BadGateway` = 502、`ServiceUnavailable` = 503、`GatewayTimeout` = 504 |

业务段：

| 段 | 成员 = 值 | 用途 |
| --- | --- | --- |
| 10xxx 认证与授权 | `LoginExpired` = 10001、`TokenInvalid` = 10002、`TokenExpired` = 10003、`PermissionDenied` = 10004 | 比 401/403 更细的语义 |
| 11xxx 数据校验 | `ValidationFailed` = 11000 | 明细逐项放 `Data` |
| 12xxx 业务处理 | `BusinessFailed` = 12000 | 业务规则/状态机不允许 |
| 13xxx 数据访问 | `DatabaseError` = 13000 | 持久化层错误 |
| 14xxx 外部依赖 | `ThirdPartyServiceError` = 14000 | 第三方接口不可用/超时 |

选码的判断顺序：

- 客户端要**区分动作**（跳登录页 / 刷新令牌 / 提示缺权限）时用业务段。`401` 只说「没认证」，`LoginExpired` 才说「曾登录、现已过期」；`403` 只说「无权限」，`PermissionDenied` 面向按钮级、字段级的权限点。
- 客户端不需要区分、只要展示消息时，协议段够用。
- **`Locked` = 423 单独记**：会话已锁定但**身份仍然有效**，客户端应引导解锁而不是跳登录页。框架不假设锁定原因（锁屏、风控挂起、强制改密、二次验证未完成都可能），原因由应用侧经 `ISessionStateGate` 定义。

`XiHanSessionStateMiddleware` 是少数直接写信封的中间件——它在授权之前拦截失效会话（`401` + 「会话已失效，请重新登录」）与锁定会话（`423` + `data` 带 `reason` / `displayName` / `avatarUrl`），因为解锁页要显示「锁的是谁」，而用户信息接口此时已被挡住。

## 配置

信封与包装本身没有配置项，行为是固定约定。相关的可配项在异常日志一侧，配置节 `XiHan:Auditing:LogQueue`：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `EnableExceptionLogQueue` | `false` | `false` 时同步调 `IExceptionLogWriter`；`true` 时入队后由后台 Worker 批量写 |
| `QueueCapacity` | `10000` | 有界队列容量 |
| `DropOnFull` | `false` | `true` 满时丢弃并记警告；`false` 满时等待，反压到请求线程 |
| `BatchSize` | `100` | 批量落库条数 |
| `BatchDelayMilliseconds` | `200` | 批量落库间隔 |
| `IgnoredPathPrefixes` | `["/hubs"]` | 不记请求日志的路径前缀 |

本地化资源按 `ApiResponse.{culture}.json` 命名，键用枚举成员名加固定键 `ServerError`，接入方式见[国际化](../packages/localization)。

## 常见问题

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 前端拿到的错误提示全是「请求错误」 | 只读了 `message`，工厂把明细放在 `data` | 取值顺序改为 `data` → `message` |
| `data` 字段在响应里整个消失 | JSON 序列化 `WhenWritingNull` | 客户端按可选字段处理，判成功用 `isSuccess` |
| `code` 输出成字符串 | 不会发生——`NumericEnumConverter` 强制 int | 若自定义了信封，记得给枚举加同样的转换器 |
| 返回 `NoContent()` 却收到 `200` | `204` 被 `NormalizeStatusCode` 归一为 `200` | 这是有意的，信封本身就是响应体 |
| 下载接口报 `System.IntPtr ... is not supported` | 返回值不是裸 `Stream` 也不是 `FileResult`，被包装后序列化 | 直接返回 `Stream` 或 `FileResult`，或打 `[IgnoreApiResponse]` |
| 打了 `[IgnoreApiResponse]` 仍收到信封 | 该特性只免成功包装，异常路径不看它 | 在端点内部自行捕获异常 |
| 异常日志表一条不写 | `IExceptionLogWriter` 停留在 `NullExceptionLogWriter` | 应用侧注册真实现 |
| 认证中间件抛异常后拿到空响应体 | 异常在 MVC 管线外，中间件只回填状态码 | 按状态码 + 响应头 `X-Trace-Id` 排查 |
| 熔断触发时收到的不是标准信封 | `XiHanCircuitBreakingMiddleware` 在 MVC 之前短路，写的是极简 JSON | 客户端对 `503` 做兜底处理，别假设一定有 `code` 字段 |
| 校验失败的 `traceId` 与响应头 `X-Trace-Id` 不一致 | `InvalidModelStateResponseFactory` 与 `XiHanController` 的助手方法取的是 `HttpContext.TraceIdentifier` | 排查以响应头 `X-Trace-Id` 为准；应用代码取 TraceId 用 `ITraceIdProvider.GetCurrentTraceId()` |

## 下一步

- [Web 应用开发](./web)：整条中间件管道、JSON 序列化约定与请求头约定
- [动态 API](./dynamic-api)：应用服务怎么变成 REST 接口
- [认证与授权](./authentication)：`401` / `403` / `423` 分别由谁产出
- [常见问题](./faq)：跨能力域的排查清单
- [Application.Contracts 包](../packages/application-contracts)：信封与业务码的完整 API 清单
- [Web.Api 包](../packages/web-api)：过滤器、中间件与全部配置项
- [Auditing 包](../packages/auditing)：异常日志的记录模型与写入器契约
