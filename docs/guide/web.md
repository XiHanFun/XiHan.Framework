# Web 应用开发

引用 `XiHan.Framework.Web.Api` 后你得到的是**一整条编排好的中间件管道**、统一响应信封、异常处理与 API 文档。本章讲这条管道长什么样、怎么往里插东西、响应契约是什么。

接口怎么从应用服务变出来见 [动态 API](./dynamic-api)。

## 一行获得整条管道

```csharp
[DependsOn(typeof(XiHanWebApiModule))]
public class MyAppModule : XiHanModule { }
```

`XiHanWebApiModule` 在 `OnApplicationInitialization` 里装配了这条管道，**顺序经过设计**：

```text
①  UseForwardedHeaders            反向代理转发头还原（必须最前）
②  XiHanTraceIdMiddleware         分配/透传 TraceId，写响应头 X-Trace-Id
③  UseXiHanRequestCulture         请求文化（按 X-Language）
④  XiHanRequestContextMiddleware  请求上下文
⑤  XiHanExceptionLoggingMiddleware 异常日志
⑥  XiHanRequestLoggingMiddleware  请求日志
⑦  UseRouting                     路由
⑧  UseRateLimiter                 限流（配置开关，默认关）
⑨  XiHanCircuitBreakingMiddleware 熔断（配置开关，默认关）
⑩  UseCors                        跨域
⑪  本地对象存储静态文件            公开资源匿名直链
⑫  XiHanApiLoggingMiddleware      API 日志
⑬  XiHanOpenApiSecurityMiddleware 开放接口签名/防重放/加解密
⑭  UseAuthentication              认证
⑮  XiHanTenantResolveMiddleware   租户解析
⑯  XiHanSessionStateMiddleware    会话闸门（401 / 423）
⑰  UseAuthorization               授权
⑱  UseEndpoints                   映射控制器 + OpenAPI
```

几条顺序上的讲究：

- **① 必须最前**——一切读取 scheme / host / 客户端 IP 的中间件（路由、鉴权、CORS、重定向生成）都依赖它还原后的值。
- **⑧⑨ 在路由后、鉴权前**——尽早拒绝超额请求，不浪费鉴权与数据库资源。
- **⑪ 在鉴权前**——本地存储的公开资源（头像等）要能匿名直链。
- **⑯ 在认证之后、授权之前**——要读 `session_id` claim，且 `423` / `401` 要先于权限评估短路，不与 `403` 混淆。

## 往管道里插东西

在你自己的模块里重写钩子：

```csharp
public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
{
    // 插在管道前段（如 Webhook 校验，要先于鉴权）
    context.GetApplicationBuilder().UseMiddleware<MyWebhookMiddleware>();
}

public override void OnApplicationInitialization(ApplicationInitializationContext context)
{
    var app = context.GetApplicationBuilder();
    app.UseMiddleware<MyMiddleware>();      // 常规位置
    app.UseEndpoints(e => e.MapHub<MyHub>("/hubs/my"));   // 映射端点
}
```

::: tip 钩子按模块拓扑序执行
底层模块先跑、你的应用模块最后跑。所以在 `OnApplicationInitialization` 里 `Use` 的中间件会**排在框架管道之后**；要插到框架管道前面，用 `OnPreApplicationInitialization`。
:::

## 统一响应信封

所有接口返回同一个信封 `ApiResponse` / `ApiResponse<T>`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `code` | `int` | 业务码，**恒为数字**（属性级 `NumericEnumConverter`，不受全局枚举转字符串影响） |
| `message` | `string` | 面向用户的提示，默认取业务码描述 |
| `data` | `any` | 成功时是业务数据；失败时承载错误明细 |
| `traceId` | `string?` | 请求追踪 ID |
| `timestamp` | `string` | 服务端时间（UTC） |
| `isSuccess` | `bool` | `code` 落在 `[200, 300)` 视为成功 |

::: warning 客户端判定成功用 `isSuccess`
不要靠「有没有 `data` 字段」——服务端 `WhenWritingNull` 会把 `data: null` 整个省略。错误提示优先读 `data`（具体错因），其次 `message`（业务码的通用描述）。
:::

### 业务码分两段

- **协议段 100–599**：与 HTTP 状态码同值同名。
- **业务段 10000+**：更细的语义，如 `10001` 登录已过期、`10003` 令牌已过期、`10004` 权限不足、`11000` 数据校验失败、`12000` 业务处理失败。

其中 **`423 Locked`** 值得单独记：它表示**会话已锁定但身份仍有效**，客户端应引导解锁而**不是**跳登录页。框架不假设锁定原因（锁屏、风控挂起、强制改密都可能），原因与解锁方式由应用侧定义。

### 不想要信封

个别端点（如文件下载、第三方回调）需要裸返回，给方法打 `[IgnoreApiResponse]`。

## JSON 序列化约定

由 `XiHanWebCoreMvcOptions.ConfigureJsonOptionsDefault` 统一设定，对接时最容易踩的五条：

| 约定 | 表现 | 注意 |
| --- | --- | --- |
| **camelCase 命名** | `UserName` → `userName` | `OAuthProviders` → **`oAuthProviders`**（只有首字符变小写） |
| **null 省略** | `WhenWritingNull` | 字段可能整个不出现，客户端按可选处理 |
| **`long` → 字符串** | `12345` → `"12345"` | 避免 JS Number 精度溢出；反序列化时数字与字符串都接受 |
| **枚举 → 成员名** | `Status.Enabled` → `"Enabled"` | 唯一例外是 `ApiResponse.code`，恒为 int |
| **时间按 `X-Timezone` 换算** | 存储 UTC，输出按请求头时区 | `DateTimeOffset` 走 ISO 8601 带偏移 |

## 请求头约定

| 请求头 | 作用 |
| --- | --- |
| `Authorization: Bearer <token>` | 身份认证 |
| `X-Language` | 请求文化，决定本地化文案与枚举标签 |
| `X-Timezone` | IANA 时区，决定时间输出 |
| `X-Trace-Id` | 入站链路 ID（未启用 OpenTelemetry 时作为 TraceId 回退来源）；响应头必回 |

## 异常处理

`XiHanExceptionLoggingMiddleware` 统一兜底：记录异常日志、转成 `ApiResponse` 失败信封返回。

::: warning 别把内部细节回传给客户端
`500` 的 `data` 建议留空。`503` 允许回传「哪个依赖不可达」这类排查线索，但**不要写主机、端口、凭据等拓扑信息**。
:::

## 跨域

配置节 `XiHan:Web:Api:Cors`：

| 键 | 说明 |
| --- | --- |
| `AllowedOrigins[]` | 允许的来源。**携带凭证时不能用 `*`，必须显式列出** |
| `AllowAnyOrigin` | 与 `AllowCredentials` **互斥** |
| `AllowCredentials` | 是否允许携带 Cookie / Authorization |
| `ExposedHeaders[]` | 额外暴露给前端 JS 读取的响应头 |
| `PreflightMaxAgeSeconds` | 预检结果缓存秒数 |

开发期更推荐让前端 dev server 反向代理到后端（同源，天然无 CORS），而不是放开 CORS。

## 限流与熔断

两者默认关闭，由配置开关打开：

| 能力 | 配置 | 位置 |
| --- | --- | --- |
| 限流 | `XiHan:Web:RateLimiting:IsEnabled` | 路由后、鉴权前 |
| 熔断 | `XiHan:Web:CircuitBreaking:IsEnabled` | 限流后、鉴权前 |

更细的流量治理（灰度路由、按百分比/用户/租户/请求头分流）见 [Web.Gateway](../packages/web-gateway) 与 [Traffic](../packages/traffic)。

## API 文档

引用 `XiHan.Framework.Web.Docs` 后自动提供 Scalar 与 Swagger UI，动态 API 的分组会自动发现。开发期访问 `/scalar`。

::: tip 生产建议关闭文档端点
或至少加访问控制——接口文档会完整暴露你的 API 面。
:::

## 相关能力

| 想做 | 去哪 |
| --- | --- |
| 实时通信（SignalR） | [Web.RealTime](../packages/web-realtime) |
| gRPC | [Web.Grpc](../packages/web-grpc) |
| MCP Server | [Web.Mcp](../packages/web-mcp) |
| 网关与灰度 | [Web.Gateway](../packages/web-gateway) |
| 开放接口签名 | [Web.Api](../packages/web-api) 的 OpenApiSecurity 段 |

## 下一步

- [动态 API](./dynamic-api)：应用服务怎么变成 REST 接口
- [数据访问](./data)：仓储与查询
- [认证与授权](./authentication)：管道里的认证/授权两段
- [Web.Api 包](../packages/web-api)：完整配置项与 API
