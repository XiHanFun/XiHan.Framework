# XiHan.Framework.Profiles.Standard

Standard 组合包（Ring 6）：Minimal（含本地化）+ `Domain` + 五个子系统的抽象与内存默认实现。大多数业务服务从这里开始，零外部基础设施即可运行。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()                 // Minimal + 五个子系统的内存默认实现（role: default）
    .UseFeature<OutboxFeature>()          // 可选：事务性发件箱
    .UseFeature<OrdersFeature>());        // 业务能力包
```

`UseStandardProfile()` 装配：Minimal 的全部（含 `InMemoryLocalizationFeature`）、`InMemoryPersistenceFeature`、`InMemoryCachingFeature`、`InMemoryMultiTenancyFeature`、`InMemorySecurityFeature`、`InMemoryMessagingFeature`。它们都是 `role: default`：再 `UseFeature` 一个显式 Provider（`EntityFrameworkCorePersistenceFeature`、`RedisCachingFeature`、`JwtSecurityFeature`、`RabbitMQMessagingFeature`）即顶替对应能力，被顶替的默认实现不装配、不启动（RFC-0002 第 4 条）。

管道阶段由这些特性填满：Authenticate（ApiKey / JWT）→ Tenant（声明或 Baggage）→ Authorize（策略与权限）→ Idempotency（幂等键）→ Transaction（工作单元）。本地化不占阶段：宿主在 Ingress 把 `Accept-Language` 协商进 Baggage `xihan.locale`，在 Egress 用 `IMessageLocalizer` 把失败结果翻译成 Problem Details 的 `detail`（默认英文，内置 10 种语言，RFC-0010）。

## 依赖方向

组合包可引用任何环；被应用引用。`Profiles.Full` 在此之上加 ObjectStorage、Scheduling、Auditing 的默认实现、OpenTelemetry 导出与四种协议适配器。

## 状态

P2：可用。
