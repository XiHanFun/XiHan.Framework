# XiHan.Framework.Auditing.Abstractions

审计的抽象包：Observe 阶段的审计中间件、调用上下文上补充动作 / 资源 / 细节的扩展、查询契约 `IAuditTrail` 与选项。能力 `auditing.trail@1` 由引用它的 Provider 提供。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Auditing.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `auditing.trail@1`（`AuditingCapabilities.Trail`），本包不提供；Provider 是 `Auditing.InMemory`、`Auditing.Database` |
| 特性入口 | —（Provider 在装配时调用 `AddAuditing()`） |
| 配置节 | `XiHan:Auditing` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`；`Microsoft.Extensions.Configuration.Abstractions` 10.0.13 |
| AOT | 兼容 |
| 稳定性 | Preview（`AuditEvent`、`IAuditSink` 在内核，Stable） |

## 概述

内核定义审计事件 `AuditEvent` 与接收器 `IAuditSink`，默认的 `NullAuditSink` 丢弃全部事件。本包补上产生事件与读回事件的部分：

- `AuditMiddleware` 挂在 Observe 阶段末尾，调用结束后按显式动作或 `AuditSelection` 决定是否记录；结果优先取管道写入的 outcome，主体与租户先取安全上下文、再回退 Baggage（ADR-0036）；接收器故障只记日志。
- `AuditInvocationExtensions` 让处理器在调用期间写入动作、资源名与细节。
- `IAuditTrail` 按时间、动作、主体、租户、trace-id、成败过滤，按写入顺序升序分页。

本包不存储任何东西，存储由 Provider 实现。用法与行为细节见[审计](../guide/auditing)。

## 何时使用

应用代码只引用本包的扩展与查询契约，装配时再选存储 Provider：开发与单实例用 [Auditing.InMemory](./auditing-inmemory)，要持久化与按租户、主体、时间查询时用 [Auditing.Database](./auditing-database)。自写 Provider 时实现 `IAuditSink` 与 `IAuditTrail`，被选中后调用 `AddAuditing()`，并通过 `Auditing.Conformance` 的 `AuditTrailConformance`。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Auditing.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。两个 Provider 都依赖本包，装配 Provider 即可；`Profiles.Full` 已包含 `Auditing.InMemory`。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Auditing:Selection` | `Protected` / `All` / `None`（不区分大小写） | `Protected` | 没有显式动作时记哪些操作 |
| `XiHan:Auditing:IncludeSuccesses` | bool | `true` | 为 `false` 时只记失败 |

```json
{ "XiHan": { "Auditing": { "Selection": "All", "IncludeSuccesses": false } } }
```

值不合法时 `AuditingOptions.FromConfiguration` 抛出 `ArgumentException`，应用在装配阶段失败。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `AuditingCapabilities` | 能力 ID `auditing.trail` 与接口版本 1 |
| `AuditingOptions` | `Selection`、`IncludeSuccesses`；`ConfigurationSection` 为 `XiHan:Auditing` |
| `AuditSelection` | `Protected`（只记契约声明了 `Permission` 的操作）、`All`、`None`（只记显式动作） |
| `Pipeline.AuditMiddleware` | Observe 阶段末尾的审计中间件 |
| `Pipeline.AuditInvocationExtensions` | `SetAuditAction`、`SetAuditResource`、`AddAuditDetail`，以及对应的 `GetAuditAction`、`GetAuditResource`、`GetAuditDetails` |
| `Trail.IAuditTrail` | `QueryAsync(AuditQuery, CancellationToken)` → `Result<AuditPage>` |
| `Trail.AuditQuery` | 过滤项 `From`、`To`、`Action`、`PrincipalId`、`TenantId`、`TraceId`、`Succeeded`，分页 `PageSize`、`ContinuationToken`；`Validate()`、`Matches(AuditEvent)` |
| `Trail.AuditPage` | `Events`（按写入顺序升序）与 `ContinuationToken`（没有下一页为 `null`） |
| `AuditingFeatureContextExtensions` | `AddAuditing(this FeatureContext)`：注册消息目录 `auditing` 与选项，把中间件挂到 Observe 末尾 |
| `AuditingErrors`、`AuditingMessages` | 错误码与消息键 |

## 使用示例

在处理器里补充信息：

```csharp
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace MyApp.Orders;

public sealed record CancelOrderRequest(string Id, string Reason);

public sealed record CancelOrderResponse(string Id);

[Operation("orders.cancel", Expose = Expose.Http, Permission = "orders.write")]
public sealed class CancelOrder : IOperation<CancelOrderRequest, CancelOrderResponse>
{
    public ValueTask<Result<CancelOrderResponse>> HandleAsync(CancelOrderRequest request, InvocationContext context)
    {
        // 动作取操作 ID（orders.cancel），这里只补资源与原因
        context.SetAuditResource(new ResourceName("global", "orders", context.GetSecurity().Tenant?.Id ?? string.Empty, "order", request.Id));
        context.AddAuditDetail("reason", request.Reason);
        return ValueTask.FromResult(Result.Success(new CancelOrderResponse(request.Id)));
    }
}
```

自写存储 Provider 的装配步骤，与 `InMemoryAuditingFeature` 相同：

1. 清单声明 `Provides = [new CapabilityDeclaration(AuditingCapabilities.Trail, AuditingCapabilities.Version)]`，`Role = ManifestRole.Provider`，写法见[能力包与清单](../guide/modularity)。
2. `Configure` 里把存储登记为单例；`context.Node.IsSelectedFor(AuditingCapabilities.Trail)` 为真时，用 `TryAddSingleton` 把同一个实例登记为 `IAuditSink` 与 `IAuditTrail`。特性先于内核的默认注册配置，所以 `TryAdd` 能先于 `NullAuditSink` 占住接收器。
3. 同一分支里调用 `context.AddAuditing()`，注册消息目录与选项并挂上中间件；没被选中时不要调用，否则中间件会挂两次。
4. `WriteAsync` 必须有界、不阻塞调用路径；`QueryAsync` 先调用 `query.Validate()`，续页令牌不可识别时返回 `AUDITING_VALIDATION_001`。
5. 测试项目继承 `Auditing.Conformance` 的 `AuditTrailConformance` 并通过全部用例。

## 行为与限制

- 记录条件：有显式动作的调用总是记录；没有时只看操作调用并按 `Selection` 判定，非操作调用不记录。`IncludeSuccesses` 为假时成功的调用不记录。
- 结果：管道写入的 outcome 优先；没写入而抛出了异常时，取消记 `KERNEL_CANCELLED_001`，其他异常记 `KERNEL_UNEXPECTED_001`，异常照常向外抛；正常返回却没写结果视为成功。
- 接收器的写入不带调用的取消令牌，被取消的调用同样记录；接收器抛出的异常以 Error 级记一条日志，不影响调用结果。
- 细节只允许 JSON 可表示的值，同名覆盖；资源名以 `xrn:` 文本保存。
- 事件不带调用来源 `InvocationOrigin`，需要时由应用写进细节，见[审计](../guide/auditing#记录调用来源)。
- 查询页大小 1 到 1000，缺省 100；续页令牌由实现自行校验。

| 错误码 | 消息键 | 含义 |
| --- | --- | --- |
| `AUDITING_VALIDATION_001` | `auditing.query.invalid` | 查询不合法：页大小越界、起止颠倒或续页令牌不可识别 |
| `AUDITING_UNAVAILABLE_001` | `auditing.trail.unavailable` | 审计存储不可达 |

## 相关

- [审计](../guide/auditing)
- [Auditing.InMemory](./auditing-inmemory)
- [Auditing.Database](./auditing-database)
- [Kernel 内核](./kernel)
- 规范：RFC-0015《审计》
