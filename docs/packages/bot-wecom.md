# XiHan.Framework.Bot.WeCom

> Bot 企业微信通道：企业微信群机器人 Webhook + 媒体上传提供者，纯 HTTP、无第三方 SDK。

- **NuGet**：`XiHan.Framework.Bot.WeCom`
- **模块类**：`XiHanBotWeComModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（`XiHan.Framework.Bot`）；**无第三方 SDK**，直接走 HTTP。

## 概述

本包是 [XiHan.Framework.Bot](./bot) 的**企业微信（WeCom）通道子包**。它实现 `IBotProvider`（名为 `WeCom`），通过企业微信群机器人 Webhook（`https://qyapi.weixin.qq.com/cgi-bin/webhook/send`）发送消息，并提供媒体上传接口（`https://qyapi.weixin.qq.com/cgi-bin/webhook/upload_media`）。无第三方 SDK，用框架 `.AsHttp()` 直接 POST。

企业微信群机器人的鉴权只需一个 `Key`（机器人 Webhook key）。连接配置由 `IWeComConfigStore` 提供，默认从 `IOptionsMonitor<WeComOptions>` 读取，应用层可覆盖为数据库来源。

## 何时使用

- 需要向企业微信群推送通知（文本 / Markdown / 图片 / 文件 / 图文 / 模板卡片）。
- 想让企业微信与其它 IM、邮件、短信通道走同一套发送策略与管道。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Bot.WeCom
```

```csharp
[DependsOn(typeof(XiHanBotWeComModule))]
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanBot(bot => bot.UseWeCom(o =>
        {
            o.Key = "693axxxx-7aoc-4bc4-97a0-0ec2sifa5aaa";  // 机器人 Webhook key
        }));
    }
}
```

`XiHanBotWeComModule` 依赖 `XiHanBotModule`，其 `ConfigureServices` 调用 `AddXiHanBotWeCom()`。`AddXiHanBotWeCom(this IServiceCollection, Action<WeComOptions>? configure = null)` 与 `UseWeCom(this BotBuilder, Action<WeComOptions> configure)` 均注册：

- `IWeComConfigStore` → `DefaultWeComConfigStore`（`TryAddSingleton`）；
- `IBotProvider` → `WeComBotProvider`（`TryAddEnumerable` Singleton）。

`UseWeCom` 的 `configure` 必填；`AddXiHanBotWeCom` 的可选。

## 工作原理

`WeComBot` 构造时拼出发送与上传 URL：

```
_messageUrl = WebHookUrl + "?key=" + Key
_uploadUrl  = UploadUrl  + "?key=" + Key   // 发送时再追加 &type=file|voice
```

`WeComBotProvider.SendAsync` 读配置、校验 `Enabled` 与 `Key` 后按 `message.Type` 路由：`Markdown`、`Image`（`Data[WeCom.Image]`）、`File`（`Data[WeCom.File]`）、`Card`（模板卡片 text_notice / news_notice）、`Link`（图文 `Data[WeCom.News]`），其余按 `Text`（应用 `Mentions`）。所有发送 POST 到 `_messageUrl`，按 `ErrCode == 0 || ErrMsg == "ok"` 判成功。媒体上传（`UploadFile`）POST 到 `_uploadUrl + "&type=" + uploadType`，成功返回 `media_id`（有效期 3 天）。

## 核心能力

- **群机器人发送**：`WeComBot` 提供 `TextMessage` / `MarkdownMessage` / `ImageMessage` / `NewsMessage` / `FileMessage` / `VoiceMessage` / `TextNoticeMessage` / `NewsNoticeMessage`，均返回 `Task<BotResult>`。
- **媒体上传**：`UploadFile(FileStream, WeComUploadType, CancellationToken)` 上传 file / voice，得 `media_id`（3 天有效）。
- **@ 提及**：`WeComText`（继承 `WeComAt`）支持 `Mentions` / `MentionedMobiles`。
- **可插拔配置**：`IWeComConfigStore` 抽象配置来源。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `WeComBotProvider` | `IBotProvider` 实现，`Name => BotProviderNames.WeCom`（`"WeCom"`）。`Task<BotResult> SendAsync(BotMessage, BotContext)` |
| `WeComBot` | Webhook 发送 + 媒体上传封装。构造 `WeComBot(WeComOptions)` |
| `IWeComConfigStore` | `Task<WeComOptions?> GetAsync(CancellationToken cancellationToken = default)` |
| `DefaultWeComConfigStore` | 默认实现，包 `IOptionsMonitor<WeComOptions>`，`GetAsync` 返回 `CurrentValue` |
| `WeComOptions` | 企业微信选项（见配置节） |
| `WeComText` / `WeComMarkdown` / `WeComImage` / `WeComNews` / `WeComFile` / `WeComVoice` / `WeComTemplateCardTextNotice` / `WeComTemplateCardNewsNotice` | 各消息体模型 |
| `WeComMsgTypeEnum` / `WeComTemplateCardType` / `WeComUploadType` | 消息类型 / 卡片类型 / 上传类型（`File` / `Voice`）枚举 |
| `WeComMessageDataKeys` | 消息 `Data` 键常量（见下表） |
| `WeComResultInfoDto` | 响应 DTO：`ErrCode`（`errcode`）/ `ErrMsg`（`errmsg`）/ `Type` / `MediaId` / `CreatedAt`；成功以 `ErrCode == 0 || ErrMsg == "ok"` 判定 |
| `WeComUploadResultDto` | 上传结果：`Message` / `MediaId` / `Type` |

## 配置

`WeComOptions` 字段：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `true` | 是否启用该提供者 |
| `WebHookUrl` | `string` | `"https://qyapi.weixin.qq.com/cgi-bin/webhook/send"` | 发送 Webhook 地址（惰性初始化） |
| `UploadUrl` | `string` | `"https://qyapi.weixin.qq.com/cgi-bin/webhook/upload_media"` | 媒体上传接口（惰性初始化） |
| `Key` | `string` | `""` | 机器人 Webhook key（拼到 URL；必填） |

`WeComMessageDataKeys` 常量（写入 `BotMessage.Data` 传富消息体）：

| 常量 | 值 | 含义 |
| --- | --- | --- |
| `WeComNews` | `"WeCom.News"` | 图文消息体（`WeComNews`） |
| `WeComImage` | `"WeCom.Image"` | 图片消息体（`WeComImage`，含 md5/base64） |
| `WeComFile` | `"WeCom.File"` | 文件消息体（`WeComFile`，含 media_id） |
| `WeComVoice` | `"WeCom.Voice"` | 语音消息体（`WeComVoice`，含 media_id） |
| `WeComTemplateCardTextNotice` | `"WeCom.TemplateCardTextNotice"` | 文本通知模板卡片 |
| `WeComTemplateCardNewsNotice` | `"WeCom.TemplateCardNewsNotice"` | 图文展示模板卡片 |

## 使用示例

```csharp
// Markdown 通知
await bot.SendAsync(new BotMessage
{
    Content = "**发布成功** release-2026.7 已上线",
    Type = BotMessageType.Markdown
}, new[] { BotProviderNames.WeCom });

// 图文（News）：把图文体放进 Data
var news = new BotMessage { Type = BotMessageType.Link };
news.Data[WeComMessageDataKeys.WeComNews] = new WeComNews
{
    Articles = new()
    {
        new WeComArticle { Title = "版本发布", Description = "点击查看", Url = "https://example.com", PicUrl = "https://example.com/1.png" }
    }
};
await bot.SendAsync(news, new[] { BotProviderNames.WeCom });
```

## 扩展点 / 自定义

- **自定义配置源**：实现 `IWeComConfigStore`（如从数据库读多机器人配置），在启用之前 `services.AddSingleton<IWeComConfigStore, ...>()` 覆盖默认（`TryAddSingleton`，先注册者生效）。
- **发文件/语音前先上传**：文件/语音消息用 `media_id`，先 `WeComBot.UploadFile(...)` 取 `media_id`（3 天有效），再发 `WeComFile`/`WeComVoice`。

## 注意事项与最佳实践

- **`Key` 必填**：`GetAsync` 返回 `null`、`Enabled=false` 或 `Key` 为空时 `SendAsync` 返回 `BadRequest`，不发送。
- **媒体 3 天有效**：上传得到的 `media_id` 仅 3 天有效，别缓存过久。
- **图片走 base64+md5**：`WeComImage` 需 `md5` 与 `base64`（非 media_id），与文件/语音的 media_id 机制不同。

## 依赖模块

- [XiHan.Framework.Bot](./bot)（内核，`IBotProvider` / `BotResult` / `BotMessage`）
- 无第三方 SDK，经 `XiHan.Framework.Http` 直接 HTTP 调用企业微信开放接口。

## 相关模块

- [XiHan.Framework.Bot.DingTalk](./bot-dingtalk) · [XiHan.Framework.Bot.Lark](./bot-lark)
- [XiHan.Framework.Bot.Email](./bot-email) · [XiHan.Framework.Bot.Sms](./bot-sms) · [XiHan.Framework.Bot.Telegram](./bot-telegram)
