# XiHan.Framework.Messaging.Conformance

消息一致性套件。任何提供 `messaging.queue@1` 的 Provider 都必须继承 `MessageBrokerConformance` 并通过全部用例。

```csharp
public sealed class InMemoryMessageBrokerConformanceTests : MessageBrokerConformance
{
    protected override IFeature CreateFeature() => new InMemoryMessagingFeature();
}
```

套件覆盖：信封属性、扩展与数据完整到达处理器；同一主题按发布顺序投递；主题互不串扰；处理器失败或抛异常时重试到上限后停止且不阻塞后续消息；租户与关联扩展进入 Baggage；非法主题被发布者拒绝。用例以 `Testing.TestHost` 走完整管道，等待投递有超时（`DeliveryTimeout`）。

## 依赖方向

依赖 `Messaging.Abstractions`、`Testing` 与 xUnit 核心包。工具链包。

## 状态

P2：可用。
