# XiHan.Framework.Messaging.Conformance

## 关于

`XiHan.Framework.Messaging.Conformance` 是消息的一致性测试套件，给写消息代理提供程序和发件箱存储的人验证实现用，不是业务代码的依赖。

## 主要特性

* `MessageBrokerConformance`：队列语义（顺序、重试、死信、竞争与广播订阅）
* `MessageStreamConformance`：在队列用例之上验证同键有序与按时刻重放
* `OutboxConformance`：发件箱的退避、死信与取消语义

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Messaging.Conformance --prerelease
```

### 使用

继承对应基类并实现 `CreateFeature()`：

```csharp
public sealed class InMemoryMessageBrokerConformanceTests : MessageBrokerConformance
{
    protected override IFeature CreateFeature() => new InMemoryMessagingFeature();
}
```

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
