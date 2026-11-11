# XiHan.Framework.Hosting.AspNetCore

ASP.NET Core 宿主适配：把 HTTP 请求翻译成调用上下文并经内核管道执行，映射探针端点，用 RFC 9457 Problem Details 回写失败结果。所有 Web 形态的 XiHan 应用都用它承载，HTTP、gRPC、MCP、CloudEvents 协议适配器都建在它上面。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Hosting.AspNetCore` |
| 环层 | Ring 4（宿主） |
| 能力 | 宿主本身不提供能力；可选的 `HostedBlobUrlFeature` 提供 `objectstorage.presign@1`，依赖 `kernel.hosting@1`、`objectstorage.blob@1` |
| 特性入口 | —（可选 `HostedBlobUrlFeature`） |
| 配置节 | 环境变量 `XIHAN_CONFIG_PATH`；`XiHan:ObjectStorage:SignedUrls`（预签名地址特性）；方案 `XiHan` 读 `XiHan:Security:RoleClaim` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Security.Abstractions`、`XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.ObjectStorage.Abstractions`；共享框架 `Microsoft.AspNetCore.App` |
| AOT | 兼容（`IsAotCompatible`） |
| 稳定性 | `WebApplicationBuilder.AddXiHan`、`UseXiHan`、`MapXiHanHealth`、`HttpInvocation`、`ProblemResponse`、端点约定为 Stable；认证方案 `XiHan`、`OperationPayloadBinder`、`OperationResponseWriter`、预签名地址为 Preview |

## 概述

宿主不认证、不解析租户、不判定授权，只做协议到内核的翻译。`UseXiHan()` 是一个 ASP.NET Core 中间件：对每个请求，它从 `traceparent`、`baggage`、`xihan-deadline`（或 `grpc-timeout`）、`Accept-Language`、`Authorization` 建调用上下文，记下调用来源，把端点元数据上的授权要求与操作描述放进上下文，然后让其余的 ASP.NET Core 管道作为内核管道的 Handle 阶段执行；调用失败且响应尚未开始时写 Problem Details；调用成功而端点没有执行（幂等回放）时，用端点元数据上的 `OperationResponseWriter` 写出上下文里的响应。应用未就绪时直接回 503，带 `PipelineBypassMetadata` 的端点（探针、文档、gRPC 方法、SignalR Hub）不经内核管道。装配了授权能力（容器里有 `PolicyCatalog`）时，`UseXiHan()` 在宿主建请求管道时核对全部端点元数据上的授权要求：`RequireXiHanPermission` 的权限码未登记、`RequireXiHanPolicy` 的策略未注册，即抛出 `SECURITY_VALIDATION_004`（一次列出全部），`app.StartAsync()` / `app.Run()` 失败（ADR-0080）。

包里还有三块面向特定场景的能力：`AddAuthentication().AddXiHan(...)` 登记的 ASP.NET Core 认证方案 `XiHan`，让绕过管道的端点也用内核的认证处理器（ADR-0051）；`MapXiHanHealth()` 的三个探针；`HostedBlobUrlFeature` 与 `MapXiHanBlobs()`，给没有原生签名的对象存储（文件系统）签预签名地址并代为收发（ADR-0049）。

## 何时使用

用 `WebApplication` 承载 XiHan 应用时就需要它，`Profiles.Minimal`、`Profiles.Standard`、`Profiles.Full` 都已引用。只在命令行里运行的工具用 [Hosting.CommandLine](./hosting-commandline)。要把操作契约发布成 REST 端点，再加 [Protocols.Http](./protocols-http)。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Hosting.AspNetCore --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();          // 放在路由之后、端点之前
app.MapXiHanHealth();
app.Run();
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| 环境变量 `XIHAN_CONFIG_PATH` | 目录 | 无 | 挂载的配置目录：`appsettings.json` 最先，其余顶层 `*.json` 按文件名序数随后，紧接 `appsettings.{环境}.json` 插入（覆盖 appsettings，环境变量与命令行参数覆盖它），可重载 |
| `XiHan:Security:RoleClaim` | string | `role` | 方案 `XiHan` 转出的 `ClaimsPrincipal` 的角色声明类型 |
| `XiHan:ObjectStorage:SignedUrls:Secret` | string | 无，必填 | 预签名地址的 HMAC-SHA256 密钥，至少 32 字节 UTF-8，多实例须一致 |
| `XiHan:ObjectStorage:SignedUrls:BaseUrl` | string | `/blobs` | 预签名地址的基址 |

后两项只在装配 `HostedBlobUrlFeature` 时读取。`AddXiHan` 还按 `XiHan:Kernel:Banner:Enabled` 登记[启动 Banner](/startup/bootstrap#启动-banner) 的选项，应用版本取自入口程序集的 `AssemblyInformationalVersion`（去掉 `+` 之后的提交哈希）。

```json
{
  "XiHan": {
    "ObjectStorage": {
      "SignedUrls": {
        "Secret": "dev-signing-key-at-least-32-bytes-long",
        "BaseUrl": "/blobs"
      }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `WebApplicationBuilderExtensions` | `AddXiHan(configure)`：装载挂载的配置目录，再交给内核装配应用 |
| `ApplicationBuilderExtensions` | `UseXiHan()`：请求桥接进内核管道；`MapXiHanHealth(prefix)`：`{prefix}/startup`、`/ready`、`/live` |
| `Endpoints.HttpInvocation` | `FindXiHanContext()` 取当前调用上下文；`CreateContext(httpContext)` / `CreateContext(httpContext, services)` 按 HTTP 载体映射建上下文；`AcceptedLocales`、`ParseBaggage`、`FormatBaggage`；常量 `DeadlineHeader`、`GrpcTimeoutHeader` |
| `Endpoints.EndpointConventionBuilderExtensions` | `RequireXiHanAuthentication()`、`RequireXiHanPermission(code, resource)`、`RequireXiHanPolicy(name, resource)`；引用的权限码与策略在宿主建请求管道时核对，不存在即启动失败 |
| `Endpoints.PipelineBypassMetadata` | 端点元数据：带它的端点不经内核管道；`BypassXiHanPipeline(builder)` 标注 |
| `Endpoints.OperationPayloadBinder` | 协议适配器挂在端点上的载荷绑定委托，`UseXiHan()` 在进管道前调用 |
| `Endpoints.OperationResponseWriter` | 协议适配器挂在端点上的成功响应写出委托（状态码、内容类型与正文）；幂等回放时端点不执行，`UseXiHan()` 在管道成功结束、响应未开始而上下文里有响应对象时用它写出 |
| `ProblemResponse` | Problem Details 响应体；`FromError`、`StatusCodeFor`、`IsAuthenticationFailure`、`FieldErrorsOf`、`ScalarDetailsOf`、`WriteTo` |
| `Health.HealthResponse`、`Health.FeatureHealth` | 探针响应体 |
| `Authentication.InvocationAuthenticationBuilderExtensions` | `AddXiHan(configure)`：登记 ASP.NET Core 认证方案 `XiHan` |
| `Authentication.InvocationAuthenticationOptions` | `QueryTokenPaths`：接受查询串 `access_token` 的路径前缀 |
| `Authentication.IInvocationAuthenticationFilter` | 方案 `XiHan` 认证成功后的挂点（会话门禁） |
| `Authentication.InvocationAuthenticationDefaults` | 常量：方案名 `XiHan`、参数名 `access_token`、令牌方案 `Bearer` |
| `Blobs.HostedBlobUrlFeature`、`Blobs.HostedBlobUrlSigner`、`Blobs.BlobEndpointRouteBuilderExtensions` | 托管的预签名地址：特性、签名器（`ForStore(name)` 给命名实例签）、`MapXiHanBlobs()`（响应带 `nosniff`，内容除 PDF 外进沙箱，ADR-0125） |

## 使用示例

端点里读安全上下文、挂授权要求：

```csharp
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

// 已认证才进得来：Authenticate 与 Tenant 阶段已写好安全上下文
app.MapGet("/me", (HttpContext http) =>
{
    var security = http.FindXiHanContext().ValueOrThrow().GetSecurity();
    return $"{security.Principal.Id}@{security.Tenant?.Id ?? "-"}";
}).RequireXiHanAuthentication();
```

静态资源等不需要内核管道、应用未就绪时也要能访问的端点：

```csharp
using XiHan.Framework.Hosting.AspNetCore.Endpoints;

PipelineBypassMetadata.BypassXiHanPipeline(app.MapGet("/version", () => "1.0.0"));
```

SignalR Hub 的认证与在 Hub 里调用操作见[实时通信](/web/realtime)。

## 行为与限制

- `UseXiHan()` 要在路由之后、端点之前；`WebApplication` 自动加的路由在管道开头，直接调用即可。需要真实客户端地址时，在它之前调用 `UseForwardedHeaders()`。
- 成功的调用不改动响应；失败时只有响应尚未开始写出才写 Problem Details，端点已写正文时保留端点的内容。
- `baggage` 只保留已登记的键，其中 `xihan.tenant` 与 `xihan.principal` 不收（ADR-0121），`HttpInvocation.FormatBaggage` 出站也只写已登记的键（RFC-0003 规则 3）；`traceparent` 的采样标志进 `TraceFlags`，缺失或不合法时并入 ASP.NET Core 当前的请求 Activity（同一 trace-id），也没有时生成新的 trace-id；`xihan-deadline` 解析失败视为没有截止时间。
- 查询串令牌只在 `QueryTokenPaths` 登记的前缀下接受，前缀不能为空或 `/`；方案 `XiHan` 只认带 `PipelineBypassMetadata` 的端点。
- 调用来源 `InvocationOrigin` 记 `http`、方法与路径（含 PathBase，不含查询串）、连接对端地址、`User-Agent` 与 `Referer`，以及匹配到的端点的路由模板（`Route`）与名字（`EndpointName`，即 `WithName`），普通端点据此在 Observe 阶段被识别（ADR-0048）。
- 健康探针的响应体含各特性状态与 `IHealthContributor` 条目（如每个进程外插件）的状态；条目另带 `description`、`checkedAt`、`durationMs` 与 `data`，有才写（ADR-0054）。
- 预签名端点 `GET <BaseUrl>/{container}/{**key}` 下载（单个 `Range` 区间回 206，不可满足回 416，可带保存文件名），`PUT` 上传整个对象或分片的一片（响应 `ETag`）；上传解除请求体大小上限，单对象上限由存储把关；端点经内核管道、不要求凭据。命名实例用 `HostedBlobUrlSigner.ForStore(name)` 签，地址带 `x-store=<name>`，实例名在签名里，实例不存在回 404（`OBJECTSTORAGE_NOTFOUND_003`，ADR-0050）。存储能自己签名（S3）时不要装本特性。
- 在绕过管道的端点（SignalR Hub、Server-Sent Events）里发起内核调用用 `HttpInvocation.CreateContext(httpContext, services)`：`Authorization` 头经 `SetCredential` 带上，经管道时重新认证。
- `Accept-Language` 在调用方没有显式 `xihan.locale` Baggage 时按权重协商进该 Baggage（装配了本地化时协商到启用的区域，否则取第一个合法值）；Problem Details 的 `Content-Language` 是实际命中的区域（可能是回退链上的区域），`code` 不变。
- VALIDATION 种类的 Problem Details 带 `errors`（字段键 → 消息数组），所有错误按白名单带 `details`：`Error.Details` 里的字符串、布尔、数值、`Guid`、时间等标量，集合与对象不输出（ADR-0024）。
- 挂载目录与用户机密的次序：配置源从低到高依次是 `appsettings.json`、`appsettings.{环境}.json`、`XIHAN_CONFIG_PATH` 挂载目录、用户机密（仅开发环境）、环境变量、命令行参数、应用自己添加的配置源；构建器没有 appsettings 文件源时挂载目录追加在末尾。`xihan pack` 生成的清单同时设 `DOTNET_USE_POLLING_FILE_WATCHER=1`（ConfigMap 以符号链接切换更新）。
- Problem Details 由 `ProblemResponse.WriteTo` 手写、探针由源生成上下文序列化，不经反射。

| 情形 | 状态码 | 错误码 |
| --- | --- | --- |
| 应用未就绪或正在停止 | 503 | `KERNEL_UNAVAILABLE_001` |
| 进入管道时截止时间已过 | 504 | `KERNEL_TIMEOUT_001` |
| 端点抛出未处理异常 | 500 | `KERNEL_UNEXPECTED_001` |
| 未认证 / 凭据无效 | 401，带 `WWW-Authenticate` | `SECURITY_DENIED_001` / `SECURITY_DENIED_003` |
| 策略拒绝 / 缺少权限 | 403 | `SECURITY_DENIED_002` / `SECURITY_DENIED_004` |
| 预签名地址无效或已过期 | 403 | `OBJECTSTORAGE_DENIED_001` |

状态码按错误码 KIND 的完整映射见 [HTTP 与 OpenAPI](/web/routing)。

## 相关

- [宿主与 Web 应用](/web/hosting)、[实时通信](/web/realtime)、[对象存储](/object-storage/basics)
- [Protocols.Http](./protocols-http)、[Protocols.Grpc](./protocols-grpc)、[Protocols.Mcp](./protocols-mcp)、[Protocols.CloudEvents](./protocols-cloudevents)
- [Hosting.CommandLine](./hosting-commandline)、[Kernel](./kernel)
- 规范：`spec/protocol/http.md`《HTTP 载体》、RFC-0003《调用上下文与传播》、RFC-0008《认证与授权》、RFC-0010《本地化》
