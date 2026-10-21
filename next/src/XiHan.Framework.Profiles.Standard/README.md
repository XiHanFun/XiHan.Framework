# XiHan.Framework.Profiles.Standard

Standard 组合包（Ring 6）：Minimal + `Domain` + 五个子系统的抽象与内存默认实现。大多数业务服务从这里开始，零外部基础设施即可运行。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()                 // 五个内存默认实现（role: default）
    .UseFeature<OutboxFeature>()          // 可选：事务性发件箱
    .UseFeature<OrdersFeature>());        // 业务能力包
```

`UseStandardProfile()` 装配：`InMemoryPersistenceFeature`、`InMemoryCachingFeature`、`InMemoryMultiTenancyFeature`、`InMemorySecurityFeature`、`InMemoryMessagingFeature`。它们都是 `role: default`：再 `UseFeature` 一个显式 Provider（`EntityFrameworkCorePersistenceFeature`、`RedisCachingFeature`、`JwtSecurityFeature`、`RabbitMQMessagingFeature`）即顶替对应能力，被顶替的默认实现不装配、不启动（RFC-0002 第 4 条）。

管道阶段由这些特性填满：Authenticate（ApiKey / JWT）→ Tenant（声明或 Baggage）→ Authorize（策略与权限）→ Idempotency（幂等键）→ Transaction（工作单元）。

## 依赖方向

组合包可引用任何环；被应用引用。`Profiles.Full`（ObjectStorage、Scheduling、Configuration、Auditing、Localization 与协议适配器）在 P3 之后提供。

## 状态

P2：可用。
