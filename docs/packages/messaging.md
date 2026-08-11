# XiHan.Framework.Messaging

> 很薄的消息路由抽象层：定义"消息信封 → 按通道路由 → 交给发送器投递"的统一模型与接口，本身只负责路由，不含任何具体通道实现。

- **NuGet**：`XiHan.Framework.Messaging`
- **模块类**：`XiHanMessagingModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（仅 Core）——不引入任何第三方通道 SDK

## 概述

这个包是一层**很薄的消息发送抽象**。它定义"一条消息（信封 `MessageEnvelope`）→ 按 `Channel` 路由到某个发送器 `IMessageSender` → 逐个接收人投递"的统一模型与接口，本身**只负责路由**，不含任何具体通道实现（邮件、短信、站内信等）。真正的发送器由业务层或专用包（如 Bot 系列）提供并注册进来；本包内置的只有一个"未配置兜底发送器" `NotConfiguredMessageSender`，用于在没有可用发送器时给出明确失败结果。

## 何时使用

- 你要一套统一的消息模型（信封、接收人、发送结果），把不同通道的差异藏在同一个接口后
- 你要按 `Channel` 把消息路由到不同的发送器（email / sms / site 等），而调用方无需感知具体实现
- 你在实现自定义消息通道，需要一个标准的 `IMessageSender` 契约去接入路由
- 不该用它的场景：本包不做"后台异步发送/发件箱/重试"，那属于上层实现（业务层或 Bot 包）的职责

## 安装与启用

```bash
dotnet add package XiHan.Framework.Messaging
```

```csharp
[DependsOn(typeof(XiHanMessagingModule))]
public class MyModule : XiHanModule { }
```

`XiHanMessagingModule.ConfigureServices` 调用 `services.AddXiHanMessaging()`，启用后注册：

- `IMessageDispatcher` → `DefaultMessageDispatcher`（`TryAddSingleton`）
- `IMessageSender` 集合追加一个 `NotConfiguredMessageSender`（`TryAddEnumerable` 单例）——它是兜底项，不遮蔽你注册的真实发送器

## 工作原理

调度路由逻辑在 `DefaultMessageDispatcher.DispatchAsync` 中：

1. 校验：`envelope` 非空、`Channel` 非空白；接收人为空则直接返回空结果。
2. 选发送器：**优先选真实发送器**——先在所有 `IMessageSender` 里找"不是 `NotConfiguredMessageSender` 且 `CanHandle(channel)`"的，找不到再退回任意 `CanHandle(channel)` 的（即兜底）。因为 `NotConfiguredMessageSender.CanHandle` 恒为 `true`，这个两段式选择确保它只在"没有真实发送器"时才生效，不会遮蔽真实发送器。
3. 无发送器时：按 `ThrowWhenNoSender` 决定抛异常还是给每个接收人返回失败结果。
4. 逐接收人调用 `sender.SendAsync`，回填结果里缺省的 `MessageId/Channel/RecipientAddress`；单个失败或抛异常时，按 `ContinueOnError` 决定继续还是中断。

## 核心能力

- **消息信封模型** `MessageEnvelope`：承载 `MessageId`、`Channel`、`Subject`、`Content`、模板编码/参数、元数据、接收人集合、计划/过期时间、租户与关联追踪 Id 等
- **消息调度器** `IMessageDispatcher`：`DispatchAsync(envelope, ct)` 把消息路由到匹配通道并逐接收人投递，返回每个接收人的发送结果集合
- **消息发送器契约** `IMessageSender`：`CanHandle(channel)` 声明支持的通道 + `SendAsync(envelope, recipient, ct)` 投递单条消息
- **路由约定**：调度器按 `Channel` 选发送器，真实发送器优先、兜底发送器最后
- **可配置行为** `XiHanMessagingOptions`：`ContinueOnError`（单接收人失败是否继续）、`ThrowWhenNoSender`（无发送器时是否抛异常）

## 主要 API / 类型

| 类型 | 说明 / 关键方法 |
| --- | --- |
| `IMessageDispatcher` | `Task<IReadOnlyList<MessageSendResult>> DispatchAsync(MessageEnvelope envelope, CancellationToken ct = default)` |
| `IMessageSender` | `bool CanHandle(string channel)` + `Task<MessageSendResult> SendAsync(MessageEnvelope envelope, MessageRecipient recipient, CancellationToken ct = default)` |
| `MessageEnvelope` | 消息信封（消息主体与元数据），见下表字段 |
| `MessageRecipient` | 接收人：`ReceiverId?`、`Address`（邮箱/手机号/用户 Id）、`DisplayName?` |
| `MessageSendResult` | 单次发送结果：`MessageId`、`Channel`、`RecipientAddress`、`IsSuccess`、`ErrorMessage?`、`ProviderMessageId?`（第三方消息 Id）、`DispatchedAt`（默认 `DateTimeOffset.UtcNow`） |
| `XiHanMessagingOptions` | 消息模块行为配置 |
| `DefaultMessageDispatcher` | 默认调度器实现（`IMessageDispatcher`） |
| `NotConfiguredMessageSender` | 未配置通道时的兜底发送器（`CanHandle` 恒 `true`，`SendAsync` 恒返回失败结果） |

### `MessageEnvelope` 字段

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `MessageId` | `string` | `Guid.NewGuid().ToString("N")` | 消息唯一标识 |
| `Channel` | `string` | `"default"` | 消息通道（如 email/sms/site） |
| `TenantId` | `string?` | `null` | 租户 Id |
| `SenderId` | `string?` | `null` | 发送者 Id |
| `Subject` | `string` | `""` | 主题 |
| `Content` | `string?` | `null` | 内容 |
| `TemplateCode` | `string?` | `null` | 模板编码 |
| `TemplateParams` | `Dictionary<string, string?>` | `[]` | 模板参数 |
| `Metadata` | `Dictionary<string, string?>` | `[]` | 扩展元数据 |
| `Recipients` | `IReadOnlyList<MessageRecipient>` | `[]` | 接收人集合 |
| `ScheduledTime` | `DateTimeOffset?` | `null` | 计划发送时间 |
| `ExpireTime` | `DateTimeOffset?` | `null` | 过期时间 |
| `CorrelationId` | `string?` | `null` | 关联追踪 Id |

## 配置

`XiHanMessagingOptions`（无独立配置节，通过 `AddXiHanMessaging(configure)` 或 `services.Configure<XiHanMessagingOptions>(...)` 设置）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `ContinueOnError` | `bool` | `true` | 单个接收人失败后是否继续给后续接收人发送 |
| `ThrowWhenNoSender` | `bool` | `false` | 找不到发送器时是否抛异常（`false` 则返回失败结果集合） |

## 使用示例

### 1. 实现并注册一个自定义发送器

```csharp
public class SiteMessageSender : IMessageSender
{
    public bool CanHandle(string channel)
        => string.Equals(channel, "site", StringComparison.OrdinalIgnoreCase);

    public async Task<MessageSendResult> SendAsync(
        MessageEnvelope envelope, MessageRecipient recipient, CancellationToken ct = default)
    {
        // 站内信落库逻辑……
        return new MessageSendResult
        {
            MessageId = envelope.MessageId,
            Channel = envelope.Channel,
            RecipientAddress = recipient.Address,
            IsSuccess = true
        };
    }
}

// 注册（追加到 IMessageSender 集合，与兜底发送器共存，路由时优先命中）
services.AddSingleton<IMessageSender, SiteMessageSender>();
```

### 2. 通过调度器发送

```csharp
public class NoticeService
{
    private readonly IMessageDispatcher _dispatcher;
    public NoticeService(IMessageDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task NotifyAsync(string userId)
    {
        var envelope = new MessageEnvelope
        {
            Channel = "site",
            Subject = "系统通知",
            Content = "您有一条新消息",
            Recipients = new[] { new MessageRecipient { ReceiverId = userId, Address = userId } }
        };

        IReadOnlyList<MessageSendResult> results = await _dispatcher.DispatchAsync(envelope);
        // 逐接收人查看 results[i].IsSuccess / ErrorMessage
    }
}
```

## 扩展点 / 自定义

- **接入新通道**：实现 `IMessageSender`（`CanHandle` 声明通道、`SendAsync` 投递），注册到 `IMessageSender` 集合即可被路由；真实发送器总是优先于兜底发送器。
- **替换调度器**：`DefaultMessageDispatcher` 以 `TryAddSingleton` 注册，先注册你自己的 `IMessageDispatcher` 即可覆盖默认路由策略。
- **后台异步/发件箱**：本包不做异步发送，"提交后入队、后台拉取发送"等能力由业务层实现（参考项目内基于 Redis 延迟队列的发件箱做法）。

## 注意事项与最佳实践

- **本包只提供路由骨架**：要真正发某个通道的消息，先提供并注册对应 `IMessageSender`，否则会命中 `NotConfiguredMessageSender` 得到"未配置发送器"的失败结果（`ThrowWhenNoSender = true` 时改为抛异常）。
- **兜底发送器不会遮蔽真实发送器**：`NotConfiguredMessageSender.CanHandle` 恒为 `true`，但调度器做了两段式选择——真实发送器优先、它只在没有真实发送器时兜底。
- **同步、无重试**：`DispatchAsync` 是即时逐接收人投递，不含重试/持久化；可靠性（重试、发件箱、去重）需在上层实现。
- **`Channel` 大小写**：`DefaultMessageDispatcher` 会 `Trim()` 通道名后匹配，但大小写敏感与否取决于各 `IMessageSender.CanHandle` 的实现（建议自实现里用不区分大小写比较）。

## 依赖模块

- [XiHan.Framework.Core](./core)
- 仅依赖 Core 与 BCL，不引入任何第三方通道 SDK。

## 相关模块

- [XiHan.Framework.EventBus](./eventbus)（事件总线依赖本包）
- [XiHan.Framework.Bot](./bot)（消息通道实现方向）
