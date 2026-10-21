# XiHan.Framework.Hosting.AspNetCore

ASP.NET Core 宿主适配（Ring 4）。

## 责任边界

- `AddXiHan()`：由内核的 `IHostApplicationBuilder` 扩展提供，`WebApplicationBuilder` 直接可用。
- `UseXiHan()`：把每个 HTTP 请求翻译为 `InvocationContext`（`traceparent`、`baggage`、`xihan-deadline`），让其余 ASP.NET Core 管道在内核管道的 Handle 阶段内执行；失败结果在响应未开始时以 RFC 9457 Problem Details 回写。
- `MapXiHanHealth()`：`/health/startup`、`/health/ready`、`/health/live` 直接映射应用状态机。
- `HttpContext.GetXiHanContext()`：在端点内取当前调用上下文。

不负责：操作契约与路由推导（`Protocols.Http`，P3）、认证与租户解析（安全与多租户子系统）。

`Authorization` 头原样进入上下文凭据（`SetCredential`），由安全子系统的 Authenticate 阶段解释；宿主不认证。端点用 `RequireXiHanPermission("orders.read")`、`RequireXiHanAuthentication()`、`RequireXiHanPolicy("name")` 挂授权要求，`UseXiHan()` 把它放进上下文，Authorize 阶段判定。

## 依赖方向

依赖 `XiHan.Framework.Kernel` 与 `Microsoft.AspNetCore.App`。

## 状态

P1：请求桥接、探针、错误信封已实现。
