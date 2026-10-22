# XiHan.Framework.Hosting.AspNetCore

ASP.NET Core 宿主适配（Ring 4）。

## 责任边界

- `AddXiHan()`：由内核的 `IHostApplicationBuilder` 扩展提供，`WebApplicationBuilder` 直接可用。
- `UseXiHan()`：把每个 HTTP 请求翻译为 `InvocationContext`（`traceparent`、`baggage`、`xihan-deadline`、`Accept-Language`），让其余 ASP.NET Core 管道在内核管道的 Handle 阶段内执行；失败结果在响应未开始时以 RFC 9457 Problem Details 回写。
- `MapXiHanHealth()`：`/health/startup`、`/health/ready`、`/health/live` 直接映射应用状态机。
- `HttpContext.GetXiHanContext()`：在端点内取当前调用上下文。

不负责：操作契约与路由推导（`Protocols.Http`，P3）、认证与租户解析（安全与多租户子系统）。

`Accept-Language` 在调用方没有显式 `xihan.locale` Baggage 时按权重协商进该 Baggage（装配了本地化时协商到启用的区域，否则取第一个合法值）；装配了 `localization.resources` 时，Problem Details 的 `detail` 用 `IMessageLocalizer.Resolve` 翻译成调用方区域，`Content-Language` 是实际命中的区域（可能是回退链上的区域），`code` 不变；没有装配本地化或错误是自由文本时 `detail` 是 `Error.Message`（RFC-0010）。

`Authorization` 头原样进入上下文凭据（`SetCredential`），由安全子系统的 Authenticate 阶段解释；宿主不认证。端点用 `RequireXiHanPermission("orders.read")`、`RequireXiHanAuthentication()`、`RequireXiHanPolicy("name")` 挂授权要求，`UseXiHan()` 把它放进上下文，Authorize 阶段判定。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Security.Abstractions`、`XiHan.Framework.Localization.Abstractions` 与 `Microsoft.AspNetCore.App`。

## 状态

P1：请求桥接、探针、错误信封已实现。P2：凭据与授权要求进入上下文，`Accept-Language` 协商与 Problem Details 本地化。
