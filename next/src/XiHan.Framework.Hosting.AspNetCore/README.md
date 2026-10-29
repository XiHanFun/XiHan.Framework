# XiHan.Framework.Hosting.AspNetCore

ASP.NET Core 宿主适配（Ring 4）。

## 责任边界

- `AddXiHan()`：由内核的 `IHostApplicationBuilder` 扩展提供，`WebApplicationBuilder` 直接可用。
- `UseXiHan()`：把每个 HTTP 请求翻译为 `InvocationContext`（`traceparent`、`baggage`、`xihan-deadline`、`Accept-Language`；调用来源 `InvocationOrigin` 记 `http`、方法与路径（含 PathBase，不含查询串）、连接对端地址、`User-Agent` 与 `Referer`、匹配到的端点的路由模板（`Route`）与名字（`EndpointName`，即 `WithName`；普通端点据此在 Observe 阶段被识别，ADR-0048），经反向代理时在 `UseXiHan()` 之前 `UseForwardedHeaders()` 即得到客户端地址，ADR-0043），让其余 ASP.NET Core 管道在内核管道的 Handle 阶段内执行；失败结果在响应未开始时以 RFC 9457 Problem Details 回写。
- `MapXiHanBlobs()` 与 `HostedBlobUrlFeature`（ADR-0049，`objectstorage.presign@1`）：给没有原生签名的对象存储（文件系统）签预签名地址并代为收发——`GET <BaseUrl>/{container}/{**key}` 下载（单个 `Range` 区间 206、不可满足 416、可带保存文件名），`PUT` 上传整个对象或分片的一片（响应 `ETag`）；签名是 HMAC-SHA256（配置 `XiHan:ObjectStorage:SignedUrls:Secret`，至少 32 字节，多实例须一致；`BaseUrl` 缺省 `/blobs`），签名不符或过期 403（`OBJECTSTORAGE_DENIED_001`）；端点经内核管道、不要求凭据，上传解除请求体大小上限（单对象上限由存储把关）。存储能自己签名（S3）时不要装。
- `MapXiHanHealth()`：`/health/startup`、`/health/ready`、`/health/live` 直接映射应用状态机；响应体含各特性状态与 `IHealthContributor` 条目（如每个进程外插件）的状态。
- `HttpContext.GetXiHanContext()`：在端点内取当前调用上下文。

不负责：操作契约与路由推导（`Protocols.Http`，P3）、认证与租户解析（安全与多租户子系统）。

`Accept-Language` 在调用方没有显式 `xihan.locale` Baggage 时按权重协商进该 Baggage（装配了本地化时协商到启用的区域，否则取第一个合法值）；装配了 `localization.resources` 时，Problem Details 的 `detail` 用 `IMessageLocalizer.Resolve` 翻译成调用方区域，`Content-Language` 是实际命中的区域（可能是回退链上的区域），`code` 不变；没有装配本地化或错误是自由文本时 `detail` 是 `Error.Message`（RFC-0010）。

Problem Details 的扩展成员（ADR-0024 补充）：VALIDATION 种类的错误带 `errors`——对象，键是字段键（`SchemaViolation.Field`：去掉根 `$` 的点分路径，如 `name`、`lines[0].sku`，根级为空串），值是该字段的消息数组；内核校验器的违规带 `kernel.validation.*` 消息键，经 `IMessageLocalizer.Resolve(key, locale, arguments)` 按与 `detail` 相同的区域翻译，应用自校验的文案（没有键）原样，没有装配本地化时用违规的原因文本。所有错误按白名单带 `details`：`Error.Details` 里的字符串、布尔、数值、`Guid`、时间等标量（如 `id`、`capacity`、`retryAfter`），集合与对象（含 `errors` / `violations`）不输出。`ProblemResponse.FromError` / `FieldErrorsOf` / `ScalarDetailsOf` / `WriteTo` 是这套规则的公开入口，响应体由 `WriteTo` 手写，不经源生成上下文。

`Authorization` 头原样进入上下文凭据（`SetCredential`），由安全子系统的 Authenticate 阶段解释；宿主不认证。端点用 `RequireXiHanPermission("orders.read")`、`RequireXiHanAuthentication()`、`RequireXiHanPolicy("name")` 挂授权要求，`UseXiHan()` 把它放进上下文，Authorize 阶段判定。

## 挂载的配置目录

`AddXiHan()` 先装载环境变量 `XIHAN_CONFIG_PATH` 指向的目录（k8s 里是 ConfigMap 的挂载点）：`appsettings.json` 最先，其余 `*.json` 按序数随后；都晚于内置配置因而优先，`reloadOnChange` 让 `IOptionsMonitor` 跟着文件变化。`xihan pack` 生成的清单同时设 `DOTNET_USE_POLLING_FILE_WATCHER=1`（ConfigMap 以符号链接切换更新）。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Security.Abstractions`、`XiHan.Framework.Localization.Abstractions` 与 `Microsoft.AspNetCore.App`。

## 状态

P1：请求桥接、探针、错误信封已实现。P2：凭据与授权要求进入上下文，`Accept-Language` 协商与 Problem Details 本地化。
