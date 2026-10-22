# XiHan.Framework.Auditing.Abstractions

审计契约（Ring 2，RFC-0015）。内核已有 `AuditEvent` 与 `IAuditSink`（默认丢弃）；本包补上产生事件的中间件、调用期间的补充信息与查询契约。能力 `auditing.trail@1` 由 Provider 提供（同时实现 `IAuditSink` 与 `IAuditTrail`）；默认实现 `Auditing.InMemory`。

| 契约 | 用途 |
| --- | --- |
| `AuditMiddleware`、`AuditingOptions` | Observe 阶段末尾：按显式动作或 `Selection`（`Protected` 默认只记声明了权限的操作 / `All` / `None`）决定是否记录；结果取管道 outcome，异常按 `KERNEL_UNEXPECTED_001` 记失败；`IncludeSuccesses` 为假只记失败；接收器故障只记日志 |
| `SetAuditAction` / `SetAuditResource` / `AddAuditDetail` | 调用期间补充动作（让非操作调用也被记录）、资源名（`xrn:`）与细节 |
| `IAuditTrail`、`AuditQuery`、`AuditPage` | 按时间、动作、主体、租户、trace-id、成败过滤，按写入顺序升序分页（`PageSize` 1 到 1000，不透明续页令牌） |
| `AddAuditing(this FeatureContext)` | Provider 注册接收器后调用：注册消息目录与选项、挂中间件 |

```csharp
public ValueTask<Result<ExportResponse>> HandleAsync(ExportRequest request, InvocationContext context)
{
    context.SetAuditAction("orders.export");
    context.SetAuditResource(ResourceName.Parse("xrn:global:orders:acme:report/2026-10"));
    context.AddAuditDetail("rows", 1200);
    ...
}
```

## 依赖方向

依赖 `XiHan.Framework.Kernel` 与 `XiHan.Framework.Localization.Abstractions`。被 `Auditing.InMemory`、`Auditing.Conformance` 引用。

## 状态

P3：可用。
