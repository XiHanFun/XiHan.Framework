# 消息通知

给「发一条通知」定义统一的模型和入口：业务代码只组装一个信封、指定通道，具体走邮件、短信还是站内信由路由决定。框架这一层**只做路由**，不含任何通道实现，也不替你排队和重试。

完整 API 与全部字段见 [Messaging 包](../packages/messaging)。

## 三个角色

| 角色 | 类型 | 职责 |
| --- | --- | --- |
| 信封 | `MessageEnvelope` | 一条消息的全部内容：通道、主题、正文、模板编码与参数、接收人集合、元数据 |
| 调度器 | `IMessageDispatcher` | 按信封的 `Channel` 挑一个发送器，**逐个接收人**调用它，汇总结果 |
| 发送器 | `IMessageSender` | 真正干活的一端：`CanHandle(channel)` 声明自己吃哪个通道，`SendAsync` 投递给单个接收人 |

调度器与发送器的分工是这一章的核心：

- 调度器**不知道**任何通道细节。它不认识 SMTP、不认识短信签名，只做「选谁」和「循环调」。
- 发送器**不关心**批量与容错策略。它只处理「这一条信封发给这一个人」，成败通过 `MessageSendResult` 返回。
- 所以框架里**没有内置任何真实发送器**。包里唯一的实现是兜底用的 `NotConfiguredMessageSender`，它对任何通道都返回「未配置发送器」的失败结果。

::: tip 选型：什么时候值得引入这一层
只有一个通道、且调用点很少时，直接注入你自己的服务更简单。当出现「同一段业务要按用户偏好走不同通道」或「通道实现将来会换」时，再让业务改为面向 `IMessageDispatcher` 编程。
:::

## 安装与启用

```bash
dotnet add package XiHan.Framework.Messaging
```

```csharp
[DependsOn(typeof(XiHanMessagingModule))]
public class MyModule : XiHanModule { }
```

模块的 `ConfigureServices` 只调用一次 `services.AddXiHanMessaging()`，注册两样东西：

| 注册 | 方式 | 说明 |
| --- | --- | --- |
| `IMessageDispatcher` → `DefaultMessageDispatcher` | `TryAddSingleton` | 默认路由实现 |
| `IMessageSender` 集合追加 `NotConfiguredMessageSender` | `TryAddEnumerable` 单例 | 兜底项，不会遮蔽你注册的真实发送器 |

::: warning 引入事件总线会顺带启用它
`XiHanEventBusModule` 依赖 `XiHanMessagingModule`，所以用了事件总线就一定装上了消息路由。但事件总线**不通过** `IMessageDispatcher` 投递事件，两条链路互不相干，别把它们混为一谈。
:::

## 写一个发送器

接通道 = 实现 `IMessageSender` + 注册进容器，没有别的接线点。

```csharp
public class SiteMessageSender : IMessageSender
{
    // 通道名建议用不区分大小写比较，调度器只帮你 Trim，不做大小写归一
    public bool CanHandle(string channel)
        => string.Equals(channel, "site", StringComparison.OrdinalIgnoreCase);

    public async Task<MessageSendResult> SendAsync(
        MessageEnvelope envelope,
        MessageRecipient recipient,
        CancellationToken cancellationToken = default)
    {
        // 站内信落库；envelope.TenantId / CorrelationId 由发送器自行取用
        await _repository.InsertAsync(...);

        return new MessageSendResult
        {
            MessageId = envelope.MessageId,
            Channel = envelope.Channel,
            RecipientAddress = recipient.Address,
            IsSuccess = true
        };
    }
}
```

注册时**追加**到集合，而不是覆盖：

```csharp
services.AddSingleton<IMessageSender, SiteMessageSender>();
```

## 发一条消息

```csharp
public class NoticeAppService
{
    private readonly IMessageDispatcher _dispatcher;

    public NoticeAppService(IMessageDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task NotifyAsync(IReadOnlyList<string> userIds, CancellationToken ct)
    {
        var envelope = new MessageEnvelope
        {
            Channel = "site",
            Subject = "系统通知",
            Content = "您有一条新消息",
            Recipients = userIds
                .Select(id => new MessageRecipient { ReceiverId = id, Address = id })
                .ToArray()
        };

        IReadOnlyList<MessageSendResult> results = await _dispatcher.DispatchAsync(envelope, ct);

        var failed = results.Where(item => !item.IsSuccess).ToArray();
        // failed[i].ErrorMessage 给出每个接收人的失败原因
    }
}
```

`MessageRecipient.Address` 是**通道相关**的地址：邮箱、手机号、用户 Id 都塞这里，怎么解释由发送器决定。`ReceiverId` 是业务侧的接收人主键，可选。

## 路由是怎么选的

`DefaultMessageDispatcher.DispatchAsync` 的流程：

| 步骤 | 动作 | 异常路径 |
| --- | --- | --- |
| 1 | 校验信封非空、`Channel` 非空白 | 分别抛 `ArgumentNullException` / `InvalidOperationException` |
| 2 | 接收人为空则直接返回空集合 | —— |
| 3 | `Channel.Trim()` 后先找**非兜底**且 `CanHandle` 的发送器 | —— |
| 4 | 找不到再回退到任意 `CanHandle` 的（即兜底） | —— |
| 5 | 仍然没有 | 按 `ThrowWhenNoSender` 抛异常，或给每个接收人返回失败结果。默认注册下兜底发送器恒匹配，这一步只有把 `NotConfiguredMessageSender` 从集合里移除后才可达 |
| 6 | 逐接收人 `SendAsync`，回填结果里缺省的字段 | 失败或抛异常时按 `ContinueOnError` 决定继续还是中断 |

几个必须知道的细节：

| 细节 | 行为 |
| --- | --- |
| 兜底优先级 | `NotConfiguredMessageSender.CanHandle` 恒为 `true`，调度器做**两段式选择**：先找非兜底的，找不到才回退，所以它永远不会挡住真实发送器 |
| 同通道多个发送器 | 只命中**第一个**匹配的（按注册顺序），不会依次尝试、也不会广播 |
| 结果回填 | 发送器返回的 `MessageId` / `Channel` / `RecipientAddress` 为空时，调度器用信封和接收人的值补上；你在发送器里可以只填 `IsSuccess` 和 `ErrorMessage` |
| 异常收敛 | 发送器抛出的异常会被捕获、记 `LogError`，转成一条 `IsSuccess = false` 的结果，**不向外抛** |
| 通道名匹配 | 调度器只对 `Channel` 做 `Trim()`，大小写是否敏感取决于各发送器的 `CanHandle` 实现 |

::: warning 循环中的取消不会抛出
`DispatchAsync` 只在进入循环**前**做一次 `ThrowIfCancellationRequested()`。循环里发送器因取消抛出的 `OperationCanceledException` 会被同一个 `catch` 吞掉、变成失败结果。判定「是否被取消」要看结果里的 `ErrorMessage`，别指望 catch 到取消异常。
:::

## 框架不做后台异步发送

这是最容易误解的一点：`DispatchAsync` 是**同步语义的即时投递**——在你的调用线程上逐个接收人跑完才返回。框架这一层**不提供**：

| 不提供的能力 | 后果 | 该由谁做 |
| --- | --- | --- |
| 入队 / 后台发送 | 接收人多时会拖长当前请求 | 业务层：提交后入队，后台服务拉取消费 |
| 重试 | 一次失败就是失败 | 发送器内部，或上层队列的重试机制 |
| 持久化 / 发件箱 | 进程退出未发的消息就没了 | 业务层落库 + 补偿 |
| 去重 | 同一条可能被重复投递 | 业务层按 `MessageId` 幂等 |
| 定时发送 | `ScheduledTime` / `ExpireTime` 调度器**根本不读** | 上层队列或调度器自行解释 |

::: danger 信封上的字段不等于框架会处理
`ScheduledTime`、`ExpireTime`、`TenantId`、`CorrelationId`、`TemplateCode`、`TemplateParams` 都只是**随信封携带的数据**。`DefaultMessageDispatcher` 一个都不解释——模板不会被渲染、过期时间不会被检查。要生效就得在你的 `IMessageSender` 里自己读。
:::

正确的异步姿势是：业务侧把待发消息入队（框架提供了 `IRedisDelayQueue<T>` 这类基础设施），后台服务拉取后再调 `IMessageDispatcher`。见 [缓存与分布式锁](./caching)。

## 与 Bot 各通道的关系

框架里有两套并行的发送栈，**没有默认打通**：

| | Messaging | Bot |
| --- | --- | --- |
| 入口 | `IMessageDispatcher` | `IBotClient` |
| 扩展点 | `IMessageSender` | `IBotProvider` |
| 目标形态 | 面向「接收人」的一对一投递 | 面向「渠道 / 群机器人」的推送 |
| 内置实现 | 无（只有兜底） | 钉钉、飞书、企业微信、Telegram、邮件、短信 |
| 附加能力 | 无 | 策略（广播 / 故障转移 / 优先级）、管道（日志 / 重试 / 限流 / 环境过滤）、模板引擎 |

Bot 的六个提供者包实现的是 `IBotProvider`，**不是** `IMessageSender`——所以装了 Bot 并不会让 `Channel = "email"` 的信封自动走 Bot 的邮件提供者。要打通就自己写一个桥接发送器：

```csharp
public class BotMessageSender : IMessageSender
{
    private readonly IBotClient _botClient;

    public BotMessageSender(IBotClient botClient) => _botClient = botClient;

    public bool CanHandle(string channel)
        => string.Equals(channel, "bot", StringComparison.OrdinalIgnoreCase);

    public async Task<MessageSendResult> SendAsync(
        MessageEnvelope envelope,
        MessageRecipient recipient,
        CancellationToken cancellationToken = default)
    {
        var message = new BotMessage
        {
            Title = envelope.Subject,
            Content = envelope.Content ?? string.Empty
        };

        // 这里把 Address 约定为 Bot 渠道名（对应 XiHanBotOptions.Channels 的键）
        var dispatch = await _botClient.SendAsync(message, [recipient.Address], cancellationToken);

        return new MessageSendResult
        {
            MessageId = envelope.MessageId,
            Channel = envelope.Channel,
            RecipientAddress = recipient.Address,
            IsSuccess = dispatch.IsSuccess,
            ErrorMessage = dispatch.ErrorMessage
        };
    }
}
```

::: tip 桥接时注意语义差
`BotDispatchResult.IsSuccess` 的含义是「至少有一个提供者结果且全部成功」，被管道跳过（`IsSkipped`）算作**不成功**。如果你的业务把「环境过滤跳过」视为正常，桥接时要把 `IsSkipped` 单独拆出来处理，别一律当失败。
:::

Bot 自身的渠道映射、策略与管道配置见 [Bot 包](../packages/bot)。

## 配置

`XiHanMessagingOptions` **没有配置节**，不能从 `appsettings.json` 读，只能用代码配置：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.Configure<XiHanMessagingOptions>(options =>
    {
        options.ContinueOnError = false;
        options.ThrowWhenNoSender = true;
    });
}
```

| 选项 | 默认值 | 含义 |
| --- | --- | --- |
| `ContinueOnError` | `true` | 单个接收人失败后是否继续发给后面的人。设为 `false` 时遇到第一个失败就中断，已产生的结果照常返回 |
| `ThrowWhenNoSender` | `false` | 一个发送器都匹配不到时是否抛 `InvalidOperationException`。`false` 则给每个接收人返回失败结果 |

::: warning `ThrowWhenNoSender` 在默认注册下不会触发
`AddXiHanMessaging()` 注册的 `NotConfiguredMessageSender` 的 `CanHandle` 恒为 `true`，调度器总能选到它，`sender is null` 这个分支永远走不到。所以打开 `ThrowWhenNoSender` 并不会让「通道没接好」抛异常——它仍然表现为兜底发送器返回的「消息通道 'xxx' 未配置发送器」失败结果。想让它生效，得先把兜底项从 `IMessageSender` 集合里移除。
:::

::: tip 开关的取舍
批量通知类场景保持默认（`ContinueOnError = true`），别让一个坏邮箱堵住整批。要在联调期让「通道没接好」立刻可见，靠的是检查每条结果的 `IsSuccess` / `ErrorMessage`，而不是这两个开关。
:::

## 常见问题

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 结果里全是「消息通道 'xxx' 未配置发送器」 | 没有任何真实发送器的 `CanHandle` 认这个通道，命中了兜底 | 检查发送器是否注册、`CanHandle` 的比较是否大小写敏感 |
| 注册了发送器但没被调用 | 同通道有多个匹配的发送器，只命中注册顺序里的第一个 | 一个通道只留一个发送器，或在 `CanHandle` 里收窄条件 |
| `DispatchAsync` 返回空集合 | `Recipients` 为空时直接返回 `[]`，不算失败 | 调用前校验接收人；别用 `results.All(r => r.IsSuccess)` 判成功——空集合恒为 `true` |
| 抛 `InvalidOperationException: 消息通道不能为空` | `Channel` 为 `null` / 空白。这个校验与 `ThrowWhenNoSender` 无关，一定抛 | 显式设置 `Channel`（默认值是 `"default"`，通常没有发送器认它） |
| 模板没渲染、`ScheduledTime` 没生效 | 调度器不解释这些字段 | 在发送器里读取并自行处理 |
| 发送器抛的异常在上层 catch 不到 | 调度器已捕获并转为失败结果 | 看 `MessageSendResult.ErrorMessage`，或看发送器打的 `LogError` |
| 换了自己的 `IMessageDispatcher` 不生效 | 被依赖模块先执行，`TryAddSingleton` 已占位 | 用 `services.Replace(ServiceDescriptor.Singleton<IMessageDispatcher, MyDispatcher>())` |
| 大批量接收人时接口变慢 | 逐接收人串行投递，且在调用线程上完成 | 改为入队 + 后台消费，见上文「框架不做后台异步发送」 |

## 下一步

- [缓存与分布式锁](./caching)——延迟队列，做后台异步发送的基础设施
- [事件总线](../packages/eventbus)——用领域事件解耦「业务动作」与「发通知」
- [多租户](./multi-tenancy)——发送器里读 `TenantId` 前先弄清当前租户上下文
- [扩展与二次开发](./extending)——替换框架默认实现的通用做法
- [Messaging 包](../packages/messaging)——完整 API 清单与 `MessageEnvelope` 全字段表
- [Bot 包](../packages/bot)——机器人推送栈的渠道、策略与管道
