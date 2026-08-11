# XiHan.Framework.Bot.Lark

> Bot 飞书通道：飞书群「自定义机器人」Webhook + 图片上传提供者，纯 HTTP、无第三方 SDK。

- **NuGet**：`XiHan.Framework.Bot.Lark`
- **模块类**：`XiHanBotLarkModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（`XiHan.Framework.Bot`）；**无第三方 SDK**，直接走 HTTP。

## 概述

本包是 [XiHan.Framework.Bot](./bot) 的**飞书（Lark）通道子包**。它实现 `IBotProvider`（名为 `Lark`），通过飞书群「自定义机器人」Webhook（`https://open.feishu.cn/open-apis/bot/v2/hook`）发送消息，并提供图片上传接口（`https://open.feishu.cn/open-apis/im/v1/images`）。无第三方 SDK，用框架 `.AsHttp()` 直接 POST。

支持飞书自定义机器人的**加签**（`Secret`，HMAC-SHA256）与**安全关键字**（`KeyWord`，自动前缀）。连接配置由 `ILarkConfigStore` 提供，默认从 `IOptionsMonitor<LarkOptions>` 读取，应用层可覆盖为数据库来源。

## 何时使用

- 需要向飞书群推送通知（文本 / 富文本 Post / 卡片 InterActive / 图片）。
- 想让飞书与其它 IM、邮件、短信通道走同一套发送策略与管道。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Bot.Lark
```

```csharp
[DependsOn(typeof(XiHanBotLarkModule))]
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanBot(bot => bot.UseLark(o =>
        {
            o.AccessToken = "xxxxxxxx";   // 机器人 Webhook token（拼到 URL 末尾）
            o.Secret = "xxxxxxxx";        // 加签密钥（启用加签时）
            o.KeyWord = "[告警]";         // 安全关键字（启用关键字时）
        }));
    }
}
```

`XiHanBotLarkModule` 依赖 `XiHanBotModule`，其 `ConfigureServices` 调用 `AddXiHanBotLark()`。`AddXiHanBotLark(this IServiceCollection, Action<LarkOptions>? configure = null)` 与 `UseLark(this BotBuilder, Action<LarkOptions> configure)` 均注册：

- `ILarkConfigStore` → `DefaultLarkConfigStore`（`TryAddSingleton`）；
- `IBotProvider` → `LarkBotProvider`（`TryAddEnumerable` Singleton）。

`UseLark` 的 `configure` 必填；`AddXiHanBotLark` 的可选。

## 工作原理

`LarkBotProvider.SendAsync` 读配置、校验 `Enabled`/`AccessToken` 后实例化 `LarkBot`，按 `message.Type` 路由（`Card` → InterActive、`Image` → 图片、其余 → Post/Text）。`LarkBot` POST 到 `WebHookUrl + "/" + AccessToken`：

- **加签（可选）**：`Secret` 非空时带 `timestamp`（Unix 秒）与 `sign = Base64(HmacSha256(timestamp + "\n" + secret, 空内容))`。
- **关键字（可选）**：`KeyWord` 非空时自动前缀（`KeyWord + "\n"`）到文本内容 / Post 标题 / 卡片标题。
- **成功判定**：`Code == 0 || Msg == "success"`。

## 核心能力

- **群机器人发送**：`LarkBotProvider` 经 `LarkBot` 发送文本 / Post（富文本）/ InterActive（卡片）/ Image。
- **图片上传**：`LarkOptions.UploadUrl` 指向飞书图片上传接口（`im/v1/images`），换取 `image_key` 供图片消息使用。
- **加签与关键字**：`LarkOptions.Secret` / `KeyWord`。
- **可插拔配置**：`ILarkConfigStore` 抽象配置来源。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `LarkBotProvider` | `IBotProvider` 实现，`Name => BotProviderNames.Lark`（`"Lark"`）。`Task<BotResult> SendAsync(BotMessage, BotContext)` |
| `LarkBot` | Webhook 发送封装（`.AsHttp()`；可选加签/关键字） |
| `ILarkConfigStore` | `Task<LarkOptions?> GetAsync(CancellationToken cancellationToken = default)` |
| `DefaultLarkConfigStore` | 默认实现，包 `IOptionsMonitor<LarkOptions>`，`GetAsync` 返回 `CurrentValue` |
| `LarkOptions` | 飞书选项（见配置节） |
| `LarkText` / `LarkPost` / `LarkImage` / `LarkInterActive` | 各消息体模型；Post/卡片用 `IPostTag`（`TagText`/`TagA`/`TagAt`/`TagImg`）与 `IInterActiveTag`（`TagMarkdown`/`TagDiv`/`TagAction`/`TagButton` 等）组织 |
| `LarkMessageDataKeys` | 消息 `Data` 键常量（见下表） |
| `LarkResultInfoDto` | 响应 DTO：`Code`（`code`）/ `Msg`（`msg`）/ `Data`（`data`） |
| `LarkResultErrCodeEnum` | 飞书错误码枚举（如 `SignMatchFail = 19021`、`KeyWordsNotFound = 19024`）；成功以 `Code == 0` 或 `Msg == "success"` 判定 |

## 配置

`LarkOptions` 字段：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `true` | 是否启用该提供者 |
| `WebHookUrl` | `string` | `"https://open.feishu.cn/open-apis/bot/v2/hook"` | Webhook 基址（惰性初始化；实际 POST 到 `WebHookUrl + "/" + AccessToken`） |
| `UploadUrl` | `string` | `"https://open.feishu.cn/open-apis/im/v1/images"` | 图片上传接口（惰性初始化） |
| `AccessToken` | `string` | `""` | 机器人 Webhook token |
| `Secret` | `string` | `""` | 加签密钥（HMAC-SHA256；为空则不加签） |
| `KeyWord` | `string?` | `null` | 安全关键字（非空时自动前缀到内容/标题） |

`LarkMessageDataKeys` 常量（写入 `BotMessage.Data` 传富消息体）：

| 常量 | 值 | 含义 |
| --- | --- | --- |
| `LarkPost` | `"Lark.Post"` | 富文本 Post 消息体（`LarkPost`） |
| `LarkInterActive` | `"Lark.InterActive"` | 卡片 InterActive 消息体 |
| `LarkImage` | `"Lark.Image"` | 图片消息体（`LarkImage`，需先上传得到 `image_key`） |

## 使用示例

```csharp
// 纯文本告警
await bot.SendAsync(new BotMessage
{
    Content = "构建失败：release-2026.7",
    Type = BotMessageType.Text
}, new[] { BotProviderNames.Lark });

// 富文本 Post：把 Post 体放进 Data
var post = new BotMessage { Type = BotMessageType.Markdown };
post.Data[LarkMessageDataKeys.LarkPost] = new LarkPost
{
    Title = "发布公告",
    Content = new()
    {
        new() { new TagText { Text = "版本 " }, new TagA { Text = "release-2026.7", Href = "https://example.com" } }
    }
};
await bot.SendAsync(post, new[] { BotProviderNames.Lark });
```

## 扩展点 / 自定义

- **自定义配置源**：实现 `ILarkConfigStore`（如从数据库读多机器人配置），在启用之前 `services.AddSingleton<ILarkConfigStore, ...>()` 覆盖默认（`TryAddSingleton`，先注册者生效）。

## 注意事项与最佳实践

- **图片消息需先上传**：图片类消息要先经 `UploadUrl` 上传得到 `image_key`，再放进 `LarkImage`。
- **加签时间敏感**：飞书校验 `timestamp` 与服务器时间偏差（错误码 `19021`），确保系统时间同步。
- **未配置 / 未启用返回 `BadRequest`**：`GetAsync` 返回 `null`、`Enabled=false` 或 `AccessToken` 为空时不发送。

## 依赖模块

- [XiHan.Framework.Bot](./bot)（内核，`IBotProvider` / `BotResult` / `BotMessage`）
- 无第三方 SDK，经 `XiHan.Framework.Http` 直接 HTTP 调用飞书开放接口。

## 相关模块

- [XiHan.Framework.Bot.DingTalk](./bot-dingtalk) · [XiHan.Framework.Bot.WeCom](./bot-wecom)
- [XiHan.Framework.Bot.Email](./bot-email) · [XiHan.Framework.Bot.Sms](./bot-sms) · [XiHan.Framework.Bot.Telegram](./bot-telegram)
