# XiHan.Framework.Messaging.Conformance

## 关于

`XiHan.Framework.Messaging.Conformance` 是消息的一致性测试套件，给写消息代理提供程序和发件箱存储的人验证实现用，不是业务代码的依赖。

## 主要特性

* `MessageBrokerConformance`：队列语义（顺序、重试、死信、竞争与广播订阅，停止打断的投递不进死信、重新开始消费后重新投递，赶上最后一次尝试时也一样）
* `MessageStreamConformance`：在队列用例之上验证同键有序与按时刻重放
* `OutboxConformance`：发件箱的退避、死信、取消语义与死信重投

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Messaging.Conformance --prerelease
```

### 使用

继承对应基类并实现 `CreateFeature()`；代理的套件还要实现 `ReadDeadLettersAsync`，按自己的介质读出一个主题此刻的死信（死信列表、死信队列、死信主题或死信流），套件靠它核对哪些消息进了死信：

```csharp
public sealed class InMemoryMessageBrokerConformanceTests : MessageBrokerConformance
{
    protected override IFeature CreateFeature() => new InMemoryMessagingFeature();

    protected override ValueTask<IReadOnlyList<MessageEnvelope>> ReadDeadLettersAsync(TestHost host, string topic, CancellationToken cancellationToken)
    {
        var broker = host.Services.GetRequiredService<InMemoryMessageBroker>();
        return ValueTask.FromResult<IReadOnlyList<MessageEnvelope>>([.. broker.DeadLetters(topic).Select(static delivery => delivery.Envelope)]);
    }
}
```

`ReadDeadLettersAsync` 不取走死信、可以反复调用，还没有死信时返回空列表。没有确认的消息要等超时才重新可见的代理，覆写 `RedeliveryTimeout` 或在 `Configuration` 里把那个超时调小。

提供流能力的提供程序继承 `MessageStreamConformance`，不必再继承队列套件；持久化提供程序继承 `OutboxConformance`。

## 主要类型

此库提供的主要类型：

* `MessageBrokerConformance`
* `MessageStreamConformance`
* `OutboxConformance`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 消息契约：[XiHan.Framework.Messaging.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Messaging.Abstractions)
* 测试宿主：[XiHan.Framework.Testing](https://www.nuget.org/packages/XiHan.Framework.Testing)

## 反馈与贡献

`XiHan.Framework.Messaging.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
