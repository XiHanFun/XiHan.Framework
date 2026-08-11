# XiHan.Framework.Bot.DingTalk

> Bot 钉钉通道：钉钉群「自定义机器人」Webhook 提供者，纯 HTTP、无第三方 SDK。

- **NuGet**：`XiHan.Framework.Bot.DingTalk`
- **模块类**：`XiHanBotDingTalkModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（`XiHan.Framework.Bot`）；**无第三方 SDK**，直接走 HTTP。

## 概述

本包是 [XiHan.Framework.Bot](./bot) 的**钉钉通道子包**。它实现 `IBotProvider`（名为 `DingTalk`），通过钉钉群「自定义机器人」的 Webhook（`https://oapi.dingtalk.com/robot/send`）发送消息，把钉钉封装为 Bot 内核可调度的一个提供者。无第三方 SDK，用框架 `XiHan.Framework.Http` 的 `.AsHttp()` 直接 POST。

支持钉钉自定义机器人的三种安全设置：**加签**（`Secret`，HMAC-SHA256）、**安全关键字**（`KeyWord`，自动前缀）、**IP 白名单**（由钉钉侧配置）。连接配置由 `IDingTalkConfigStore` 提供，默认从 `IOptionsMonitor<DingTalkOptions>` 读取，应用层可覆盖为数据库来源。

## 何时使用

- 需要向钉钉群推送通知（文本 / Markdown / ActionCard / FeedCard / Link）。
- 想让钉钉与其它 IM、邮件、短信通道走同一套发送策略与管道。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Bot.DingTalk
```

```csharp
[DependsOn(typeof(XiHanBotDingTalkModule))]
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanBot(bot => bot.UseDingTalk(o =>
        {
            o.AccessToken = "xxxxxxxx";   // 机器人 Webhook 的 access_token
            o.Secret = "SECxxxxxxxx";     // 加签密钥（启用加签时）
            o.KeyWord = "[告警]";         // 安全关键字（启用关键字时）
        }));
    }
}
```

`XiHanBotDingTalkModule` 依赖 `XiHanBotModule`，其 `ConfigureServices` 调用 `AddXiHanBotDingTalk()`。`AddXiHanBotDingTalk(this IServiceCollection, Action<DingTalkOptions>? configure = null)` 与 `UseDingTalk(this BotBuilder, Action<DingTalkOptions> configure)` 均注册：

- `IDingTalkConfigStore` → `DefaultDingTalkConfigStore`（`TryAddSingleton`）；
- `IBotProvider` → `DingTalkBotProvider`（`TryAddEnumerable` Singleton）。

`UseDingTalk` 的 `configure` 必填；`AddXiHanBotDingTalk` 的可选。

## 工作原理

`DingTalkBot` 构造时拼出请求 URL 与关键字前缀：

```
_url = WebHookUrl + "?access_token=" + AccessToken
_keyWord = KeyWord == null ? null : KeyWord + "\n"   // 自动前缀到标题/正文
```

发送时（`Send`）**无条件**计算签名并追加到 URL：`timestamp = 毫秒时间戳`，`sign = UrlEncode(HmacSha256(Secret, timestamp + "\n" + Secret))`，`url` 都会追加 `&timestamp={ts}&sign={sign}`——即使未配置 `Secret`（此时以空字符串作密钥参与运算）也照样带上这两个查询参数；钉钉服务端是否据此校验，取决于该机器人是否启用了「加签」安全设置。然后 `url.AsHttp().SetJsonBody(payload).PostAsync<DingTalkResultInfoDto>()`，按 `ErrCode == 0 || ErrMsg == "ok"` 判成功。

`DingTalkBotProvider.SendAsync` 按 `message.Type` 路由：`Markdown` 走 `MarkdownMessage`；`Link` 从 `message.Data` 按 `DingTalkMessageDataKeys.DingTalkLink` 取出 `DingTalkLink` 走 `LinkMessage`；`Card` 依次尝试从 `Data` 取 `DingTalkActionCard`（键 `DingTalkMessageDataKeys.DingTalkActionCard`）走 `ActionCardMessage`，取不到再尝试 `DingTalkFeedCard`（键 `DingTalkMessageDataKeys.DingTalkFeedCard`）走 `FeedCardMessage`；以上分支若对应 `Data` 取不到值，或消息类型不属于以上三种，统一跌落到默认分支发送纯文本 `TextMessage`。@ 提及由 `message.Mentions` 构建 `DingTalkAt`（含 `"@all"` → `IsAtAll`）。

## 核心能力

- **群机器人发送**：`DingTalkBot` 提供 `TextMessage` / `MarkdownMessage` / `LinkMessage` / `ActionCardMessage` / `FeedCardMessage`，均返回 `Task<BotResult>`。
- **三种安全设置**：加签（`Secret`）、关键字（`KeyWord`）、IP 白名单（钉钉侧）。
- **@ 提及**：`DingTalkAt` 支持 `AtMobiles` / `AtUserIds` / `IsAtAll`。
- **可插拔配置**：`IDingTalkConfigStore` 抽象配置来源。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `DingTalkBotProvider` | `IBotProvider` 实现，`Name => BotProviderNames.DingTalk`（`"DingTalk"`）。`Task<BotResult> SendAsync(BotMessage, BotContext)` |
| `DingTalkBot` | Webhook 发送封装。构造 `DingTalkBot(DingTalkOptions)`；各消息类型方法 + 私有 `Send(object, CancellationToken)` |
| `IDingTalkConfigStore` | `Task<DingTalkOptions?> GetAsync(CancellationToken cancellationToken = default)` |
| `DefaultDingTalkConfigStore` | 默认实现，包 `IOptionsMonitor<DingTalkOptions>`，`GetAsync` 返回 `CurrentValue` |
| `DingTalkOptions` | 钉钉选项（见配置节） |
| `DingTalkText` / `DingTalkMarkdown` / `DingTalkLink` / `DingTalkActionCard` / `DingTalkFeedCard` / `DingTalkAt` | 各消息体模型（`JsonPropertyName` 映射钉钉字段） |
| `DingTalkMessageDataKeys` | 消息 `Data` 键常量（见下表） |
| `DingTalkResultInfoDto` | 响应 DTO：`ErrCode`（`errcode`）/ `ErrMsg`（`errmsg`） |
| `DingTalkResultErrCodeEnum` | 钉钉错误码枚举（如 `AccessTokenNotExist = 400101`、`SendingSpeedTooFast = 410100`）；成功以 `ErrCode == 0` 判定，无独立成功枚举值 |

## 配置

`DingTalkOptions` 字段：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `true` | 是否启用该提供者 |
| `WebHookUrl` | `string` | `"https://oapi.dingtalk.com/robot/send"` | Webhook 地址（惰性初始化） |
| `AccessToken` | `string` | `""` | 机器人 access_token（拼到 URL） |
| `Secret` | `string` | `""` | 加签密钥（HMAC-SHA256；为空则不加签） |
| `KeyWord` | `string?` | `null` | 安全关键字（非空时自动前缀 `KeyWord + "\n"` 到标题/正文） |

`DingTalkMessageDataKeys` 常量（写入 `BotMessage.Data` 传富消息体）：

| 常量 | 值 | 含义 |
| --- | --- | --- |
| `DingTalkLink` | `"DingTalk.Link"` | Link 消息体（`DingTalkLink`） |
| `DingTalkActionCard` | `"DingTalk.ActionCard"` | ActionCard 消息体 |
| `DingTalkFeedCard` | `"DingTalk.FeedCard"` | FeedCard 消息体 |

## 使用示例

```csharp
// Markdown 告警，@ 指定手机号
var msg = new BotMessage
{
    Title = "服务告警",
    Content = "### node-01 CPU 95%\n> 持续 5 分钟",
    Type = BotMessageType.Markdown,
    Mentions = { "13800000000" }   // "@all" 则 @ 全员
};
await bot.SendAsync(msg, new[] { BotProviderNames.DingTalk });

// ActionCard（带跳转）：把卡片体放进 Data
var card = new BotMessage { Type = BotMessageType.Card };
card.Data[DingTalkMessageDataKeys.DingTalkActionCard] = new DingTalkActionCard
{
    Title = "发布确认",
    Text = "点击查看详情",
    SingleTitle = "查看",
    SingleUrl = "https://example.com/release"
};
await bot.SendAsync(card, new[] { BotProviderNames.DingTalk });
```

## 扩展点 / 自定义

- **自定义配置源**：实现 `IDingTalkConfigStore`（如从数据库读多机器人配置），在启用之前 `services.AddSingleton<IDingTalkConfigStore, ...>()` 覆盖默认（`TryAddSingleton`，先注册者生效）。

## 注意事项与最佳实践

- **加签与关键字可二选一或并用**：与钉钉机器人安全设置一致；`KeyWord` 非空则自动前缀（注意别让关键字被 Markdown 语法吞掉）。`timestamp`/`sign` 签名参数每次请求都会计算并追加到 URL，未配置 `Secret` 时以空字符串参与运算，钉钉服务端是否校验取决于该机器人是否启用了「加签」。
- **未配置 / 未启用返回 `BadRequest`**：`GetAsync` 返回 `null`、`Enabled=false` 或 `AccessToken` 为空时不发送。
- **限流**：钉钉自定义机器人有每分钟条数限制（错误码 `410100`），高频场景配合内核 `RateLimitPipeline` 使用。

## 依赖模块

- [XiHan.Framework.Bot](./bot)（内核，`IBotProvider` / `BotResult` / `BotMessage`）
- 无第三方 SDK，经 `XiHan.Framework.Http` 直接 HTTP 调用钉钉开放接口。

## 相关模块

- [XiHan.Framework.Bot.Lark](./bot-lark) · [XiHan.Framework.Bot.WeCom](./bot-wecom)
- [XiHan.Framework.Bot.Email](./bot-email) · [XiHan.Framework.Bot.Sms](./bot-sms) · [XiHan.Framework.Bot.Telegram](./bot-telegram)
