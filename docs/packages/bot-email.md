# XiHan.Framework.Bot.Email

> Bot 邮件通道：基于 MailKit 的 SMTP 发件提供者，把邮件接入统一的 `IBotClient` 调度。

- **NuGet**：`XiHan.Framework.Bot.Email`
- **模块类**：`XiHanBotEmailModule`
- **所在层**：基础设施层
- **关键依赖**：`MailKit`（4.17.0，SMTP 发信）；框架内部依赖 `XiHan.Framework.Bot`。

## 概述

本包是 [XiHan.Framework.Bot](./bot) 的**邮件通道子包**。它实现了 `IBotProvider`（名为 `Email`），内部用 `MailKit.Net.Smtp.SmtpClient` + `MimeKit` 连接 SMTP 服务器发信，从而让邮件成为 Bot 内核可调度的一个提供者：调用方仍只面对 `IBotClient`，用广播/主备/优先级等统一策略把消息发到邮件（以及其它通道）。

发件配置（SMTP 主机/端口/账号密码/收件人）通过 `EmailOptions` 表达，并经 `IEmailConfigStore` 提供当前生效配置——默认从 `IOptionsMonitor<EmailOptions>` 读取，应用层可注册自定义 store（如从数据库读取）覆盖。

## 何时使用

- 需要把「邮件」纳入 Bot 统一发送体系，与钉钉/飞书/短信等一起编排。
- 需要按运行时配置（而非硬编码 appsettings）动态切换 SMTP 账号——注册自定义 `IEmailConfigStore`。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Bot.Email
```

```csharp
[DependsOn(typeof(XiHanBotEmailModule))]
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Bot 构建器链式启用（UseEmail 的 configure 必填）
        context.Services.AddXiHanBot(bot => bot.UseEmail(o =>
        {
            o.From.SmtpHost = "smtp.example.com";
            o.From.SmtpPort = 587;
            o.From.FromMail = "noreply@example.com";
            o.From.FromPassword = "***";
            o.To.Add("ops@example.com");
        }));
    }
}
```

`XiHanBotEmailModule` 依赖 `XiHanBotModule`，其 `ConfigureServices` 调用 `AddXiHanBotEmail()`。注册内容：

- `IEmailConfigStore` → `DefaultEmailConfigStore`（`TryAddSingleton`）；
- `IBotProvider` → `EmailBotProvider`（`TryAddEnumerable` Singleton，加入调度提供者列表）。

`UseEmail(this BotBuilder, Action<EmailOptions> configure)` 做同样的注册并 `Configure(configure)` 绑定选项（`configure` 必填）。`AddXiHanBotEmail(this IServiceCollection, Action<EmailOptions>? configure = null)` 的 `configure` 可选。

## 核心能力

- **SMTP 发信**：`EmailBot.SendMail(EmailToModel, CancellationToken)` 用 MailKit 连接、（可选）认证、发送 `MimeMessage`，支持 HTML 正文、抄送/密送、附件（`EmailToModel.AttachmentsPath`）。
- **按条覆盖收件人 / 正文类型**：消息 `Data` 上写 `EmailMessageDataKeys` 键，可覆盖默认 `To/Cc/Bcc` 与 `IsBodyHtml`。
- **可插拔配置源**：`IEmailConfigStore` 抽象当前生效 `EmailOptions`。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `EmailBotProvider` | `IBotProvider` 实现，`Name => BotProviderNames.Email`（`"Email"`）。`Task<BotResult> SendAsync(BotMessage message, BotContext context)`：读配置 → 校验 → 组装 `EmailToModel` → `EmailBot.SendMail` |
| `EmailBot` | SMTP 发信封装。构造 `EmailBot(EmailFromModel)`；`Task<bool> SendMail(EmailToModel, CancellationToken)` |
| `IEmailConfigStore` | 配置源抽象：`Task<EmailOptions?> GetAsync(CancellationToken cancellationToken = default)`（返回 `null` 视为未配置） |
| `DefaultEmailConfigStore` | 默认实现，包 `IOptionsMonitor<EmailOptions>`，`GetAsync` 返回 `CurrentValue` |
| `EmailOptions` | 邮件选项（见配置节） |
| `EmailFromModel` | 发件人/SMTP 配置（见下表） |
| `EmailToModel` | 单封邮件：`Subject`、`Body`、`IsBodyHtml`、`ToMail`/`CcMail`/`BccMail`（`List<string>`）、`AttachmentsPath`（`List<Attachment>`） |
| `EmailMessageDataKeys` | 消息 `Data` 键常量（见下表） |

## 配置

`EmailOptions` 字段：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `true` | 是否启用该提供者（`false` 或未配置时 `SendAsync` 返回 `BadRequest`） |
| `From` | `EmailFromModel` | `new()` | 发件人/SMTP 服务器配置 |
| `To` | `List<string>` | `[]` | 默认收件人 |
| `Cc` | `List<string>` | `[]` | 默认抄送 |
| `Bcc` | `List<string>` | `[]` | 默认密送 |
| `IsBodyHtml` | `bool` | `true` | 是否 HTML 正文 |

`EmailFromModel` 字段：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `SmtpHost` | `string` | `""` | SMTP 服务器（必填） |
| `SmtpPort` | `int` | `587` | 端口 |
| `UseSsl` | `bool` | `true` | 是否 SSL |
| `FromMail` | `string` | `""` | 发件邮箱（必填） |
| `FromPassword` | `string` | `""` | 发件密码 |
| `FromUserName` | `string` | `""` | SMTP 认证登录名（为空则不认证；多数服务商即发件邮箱） |
| `FromName` | `string` | `""` | 发件人显示名（为空回退发件邮箱） |
| `Coding` | `Encoding` | `Encoding.UTF8` | 内容编码 |
| `AcceptInvalidCertificate` | `bool` | `false` | 是否接受无效/自签 TLS 证书（仅开发环境放开，生产务必 `false`） |

`EmailMessageDataKeys` 常量（写入 `BotMessage.Data` 覆盖默认）：

| 常量 | 值 | 含义 |
| --- | --- | --- |
| `EmailTo` | `"Email.To"` | 覆盖收件人 |
| `EmailCc` | `"Email.Cc"` | 覆盖抄送 |
| `EmailBcc` | `"Email.Bcc"` | 覆盖密送 |
| `EmailIsBodyHtml` | `"Email.IsBodyHtml"` | 覆盖是否 HTML 正文（`bool`） |

## 使用示例

```csharp
// 用默认收件人发一封 HTML 邮件
await bot.SendAsync(new BotMessage
{
    Title = "夜间巡检报告",     // → 邮件主题（为空时用 "Notification"）
    Content = "<h3>全部服务正常</h3>",
    Type = BotMessageType.Text
}, new[] { BotProviderNames.Email });

// 按条覆盖收件人与正文类型
var msg = new BotMessage { Title = "对账单", Content = "纯文本正文" };
msg.Data[EmailMessageDataKeys.EmailTo] = new List<string> { "a@x.com", "b@x.com" };
msg.Data[EmailMessageDataKeys.EmailIsBodyHtml] = false;
await bot.SendAsync(msg, new[] { BotProviderNames.Email });
```

## 扩展点 / 自定义

- **自定义配置源**：实现 `IEmailConfigStore`（如从数据库读 SMTP 账号），在 `AddXiHanBotEmail`/`UseEmail` 之前 `services.AddSingleton<IEmailConfigStore, DbEmailConfigStore>()` 覆盖默认（默认以 `TryAddSingleton` 注册，先注册者生效）。

## 注意事项与最佳实践

- **收件人为空即失败**：`To/Cc/Bcc` 全空时 `SendAsync` 返回 `BadRequest`；主题取 `message.Title`，为空回退 `"Notification"`。
- **未配置 = 未启用**：`GetAsync` 返回 `null`、`Enabled=false`、或 `SmtpHost`/`FromMail` 为空，都会被判为未配置并返回 `BadRequest`（不抛异常，符合 fail-closed）。
- **认证可选**：`FromUserName` 为空时不做 SMTP 认证（适配允许匿名中继的内网 SMTP）。
- **证书校验默认严格**：`AcceptInvalidCertificate` 默认 `false`，生产勿放开。
- **发送异常不抛出、取消例外**：`EmailBot.SendMail` 内 SMTP 连接/认证/发送失败会被捕获并记录日志（`LogHelper.Error`），返回 `false`（由 `EmailBotProvider` 转为 `BotResult.Failed`），不会抛出异常；但调用方主动取消触发的 `OperationCanceledException` 会原样上抛，不会被吞成"发送失败"。

## 依赖模块

- [XiHan.Framework.Bot](./bot)（内核，`IBotProvider` / `BotResult` / `BotMessage`）
- `MailKit` 4.17.0（SMTP 发信）

## 相关模块

- [XiHan.Framework.Bot.Sms](./bot-sms) · [XiHan.Framework.Bot.Telegram](./bot-telegram)
- [XiHan.Framework.Bot.DingTalk](./bot-dingtalk) · [XiHan.Framework.Bot.Lark](./bot-lark) · [XiHan.Framework.Bot.WeCom](./bot-wecom)
