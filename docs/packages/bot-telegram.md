# XiHan.Framework.Bot.Telegram

> Bot Telegram 通道：两层能力——统一调度用的单发 `IBotProvider`，与一套可托管多机器人的交互平台（Webhook/长轮询双模、命令/回调/会话路由、发送门面、去重/审计）。

- **NuGet**：`XiHan.Framework.Bot.Telegram`
- **模块类**：`XiHanBotTelegramModule`
- **所在层**：基础设施层
- **关键依赖**：`Telegram.Bot`（22.10.1.1，官方 SDK）+ `Microsoft.AspNetCore.App`（`FrameworkReference`，Webhook 端点需要）；框架内部依赖 `XiHan.Framework.Bot`。

## 概述

本包是 [XiHan.Framework.Bot](./bot) 的**Telegram 通道子包**，也是 Bot 家族里**最厚**的一个。它有两层能力：

1. **单发层**：`TelegramBotProvider` 实现 `IBotProvider`（名为 `Telegram`），接入统一 Bot 调度，纯用于往某个 chat 发通知——调用方仍只面对 `IBotClient`。
2. **多机器人交互平台层**：一套完整的运行时（`TelegramBotManager` + `BotRegistry` + `TelegramBotHostedService`），支持**同时托管多个机器人**、**Webhook/长轮询双模**接入、**命令/回调/消息/内联查询/回复/会话状态**路由、`ITelegramNotifier` 主动发送门面（内建 429/5xx/超时重试退避 + 出站审计），以及会话状态、更新去重、消息审计三类可插拔 store。

平台层**默认不启用**：由配置节 `XiHan:Bot:Telegram:Platform` 的 `Settings.Enabled=true` 或应用层 store 开启；未启用时 manager 空转。所有处理器**显式注册、不做程序集扫描**。

## 何时使用

- 只想往 Telegram 发通知：通过统一 `IBotClient` 走 `TelegramBotProvider`（单发层）。
- 想构建可交互机器人：处理用户命令、内联按钮回调、多轮会话，并可同时托管多个机器人实例（平台层）。
- 需要 Webhook 接收更新（ASP.NET Core 端点，含 secret token 校验）或长轮询两种接入方式。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Bot.Telegram
```

```csharp
[DependsOn(typeof(XiHanBotTelegramModule))]
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 单发层：Bot 构建器上启用（configure 必填）
        context.Services.AddXiHanBot(bot => bot.UseTelegram(o =>
        {
            o.Token = "123456:ABC-DEF...";
            o.ChatId = "-1001234567890";
            o.ParseMode = "Markdown";
        }));

        // 平台层：注册业务处理器（显式，无扫描）
        context.Services.AddTelegramBotBuiltinHandlers();       // /start /help /myid
        context.Services.AddTelegramBotHandler<MyOrderCommandHandler>();
    }
}
```

`XiHanBotTelegramModule` 依赖 `XiHanBotModule`，其 `ConfigureServices` **同时**注册单发通道（`AddXiHanBotTelegram()`）与多机器人平台（绑定 `TelegramBotPlatformOptions`（配置节 `XiHan:Bot:Telegram:Platform`）后 `AddXiHanBotTelegramPlatform()`）。

Webhook 模式还需在应用管道里 `app.UseTelegramBotWebhook()`（映射 Webhook 中间件）。

## 工作原理

### 传输模式选择（Webhook vs 长轮询）

由 `TelegramBotSettings.WebhookBaseUrl` 决定：**非空 = Webhook**，**空 = 长轮询**。

- **Webhook**：`TelegramBotManager` 调 `ITelegramBotClient.SetWebhook(url, secretToken)`；`TelegramBotWebhookMiddleware` 在 `{WebhookRoutePrefix}/{botName}` 接收 POST，校验 `X-Telegram-Bot-Api-Secret-Token` 头（**fail-closed，secret 必须非空**，定长比较防时序攻击），反序列化为 `Update` 后**入队后台分发**并立即回 200（避免 Telegram 重试风暴）。
- **长轮询**：`TelegramBotManager` 调 `ITelegramBotClient.StartReceiving()` 拉取更新；遇 409（getUpdates 冲突）停轮询、下轮刷新重试。

### Manager 刷新循环

`TelegramBotManager` 按 `ManagerRefreshSeconds` 轮询 `ITelegramBotConfigStore` 与 `ITelegramBotSettingsStore`，diff 配置：新增机器人启动、变更重启、删除移除；传输模式切换或网络配置（代理/BaseUrl/超时）变更时重建实例；并把命令菜单同步到 Telegram（`SetMyCommands`/`DeleteMyCommands`）。`BotRegistry` 无锁热路径查找，替换/删除的旧实例**延迟 150 秒**再 `Dispose`（避免打断在途请求）。

### 更新分发管线

`TelegramUpdateDispatcher.DispatchAsync` 固定顺序：群/频道白名单守卫（fail-closed）→ 更新去重（幂等）→ 内联查询 → 会话状态 → 回调 → `/start` 深链 → 命令（token 或正则）→ 回复 → 消息 → 兜底回复。未捕获异常回 `InternalErrorReply`；处理被取消则回滚去重标记（at-least-once）。

## 核心能力

- **单发通道**：`TelegramBotProvider`（`Name = "Telegram"`）接入统一调度；chat/parseMode 可被消息 `Data` 覆盖。
- **主动发送门面**：`ITelegramNotifier` 按机器人名发文本/Markdown/图片/文件、编辑消息与内联键盘、`SendToAdminsAsync` 广播管理员；内建重试退避（429 用 `RetryAfter`，否则指数退避）与出站审计。
- **多机器人托管**：`TelegramBotManager` / `BotRegistry` / `TelegramBotHostedService`（`IHostedService`，启动/停止不阻塞应用）。
- **处理器扩展点**：`IBotCommandHandler` / `IBotCallbackHandler` / `IBotMessageHandler` / `IBotReplyHandler` / `IBotInlineQueryHandler` / `IBotStartPayloadHandler` / `IBotStateHandler` + `[BotCommand]` / `[BotCallback]` 特性。
- **会话状态/去重/审计**：`IConversationStateStore`（多轮会话，默认内存 TTL 10 分钟）、`ITelegramUpdateDeduplicator`（更新去重，默认内存 TTL 30 分钟）、`ITelegramMessageAuditStore`（默认 No-op），均可换 store。
- **内联键盘 DSL**：`TelegramKeyboardBuilder`（`AddButton` / `AddUrlButton` / `AddRow` / `Build`，静态 `ConfirmCancel` / `Single`）。

## 主要 API / 类型

### 单发层

| 类型 | 说明 |
| --- | --- |
| `TelegramBotProvider` | `IBotProvider` 实现，`Name => BotProviderNames.Telegram`（`"Telegram"`）。读配置→解析 ChatId/ParseMode→`TelegramBotClient.SendMessage` |
| `TelegramOptions` | 单发选项（见配置节） |
| `ITelegramConfigStore` / `DefaultTelegramConfigStore` | `Task<TelegramOptions?> GetAsync(CancellationToken)`；默认读 `IOptionsMonitor<TelegramOptions>` |
| `BotBuilderTelegramExtensions.UseTelegram` | `UseTelegram(this BotBuilder, Action<TelegramOptions> configure)` |

### 平台层 · 运行时

| 类型 | 说明 |
| --- | --- |
| `TelegramBotManager` | 多机器人运行时：`StartAsync` / `StopAsync` / `GetStatus` / `RefreshNowAsync` / `DispatchAsync` / `QueueDispatch` |
| `BotRegistry` | 无锁实例表：`AddOrUpdate` / `TryGet` / `GetRequired` / `GetAll` / `Remove`（旧实例延迟 150s 释放） |
| `BotInstance` | 单机器人实例（`Name` / `Token` / `Config` / `Client` / `BotId` / `Username`；`IsAdmin` / `IsGroupAllowed`） |
| `TelegramBotHostedService` | `IHostedService`，`StartAsync`/`StopAsync` 调 manager（异常吞并记日志，不阻塞启动） |
| `TelegramUpdateDispatcher` | 更新分发（固定管线） |
| `TelegramKeyboardBuilder` | 内联键盘构建器 |

### 平台层 · 发送门面（`ITelegramNotifier`）

| 方法 | 说明 |
| --- | --- |
| `SendTextAsync(botName, chatId, text, replyToMessageId?, replyMarkup?, ct)` | 发文本 |
| `SendMarkdownAsync(...)` / `SendByParseModeAsync(..., parseMode?, ...)` | 发 Markdown / 指定 parseMode（`None`/`Markdown`/`MarkdownV2`/`Html`，忽略大小写） |
| `SendPhotoAsync(..., imageBytes, caption?, ...)` / `SendDocumentAsync(..., fileBytes, fileName, ...)` | 发图片 / 文件 |
| `EditMessageTextAsync(...)` / `EditMessageReplyMarkupAsync(...)` | 编辑消息文本 / 内联键盘 |
| `SendToAdminsAsync(botName, text, parseMode?, ct)` | 广播机器人所有管理员（逐个发，单个失败不中断） |

默认实现 `TelegramNotifier` 所有发送包裹 `ExecuteWithRetryAsync`（429 用 `RetryAfter`，5xx/网络/超时指数退避，`MaxRetries` 默认 3，最终失败可选通知管理员），并经 `ITelegramMessageAuditStore.AppendAsync` 审计。

### 平台层 · 处理器扩展点

| 接口 | 关键方法 |
| --- | --- |
| `IBotCommandHandler` | `Task HandleAsync(TelegramBotContext, string[] args, CancellationToken)`；配 `[BotCommand]` |
| `IBotCallbackHandler` | `Task HandleAsync(TelegramBotContext, string data, CancellationToken)`；配 `[BotCallback]` |
| `IBotMessageHandler` / `IBotReplyHandler` | `int Order`、`bool CanHandle(ctx)`、`Task HandleAsync(ctx, ct)`（按 `Order` 排序，首个 `CanHandle` 命中即止） |
| `IBotInlineQueryHandler` | `bool CanHandle(ctx, query)`、`Task<IReadOnlyList<InlineQueryResult>> HandleAsync(ctx, query, ct)` |
| `IBotStartPayloadHandler` | `Task<bool> HandleAsync(ctx, string payload, ct)`（处理 `/start` 深链，返回 `true` 消费） |
| `IBotStateHandler` | `bool CanHandle(ctx, string stateStep)`、`Task HandleAsync(ctx, stateStep, statePayload?, ct)` |

`[BotCommand(command)]`：`Command` / `Description` / `AdminOnly` / `Aliases` / `Pattern`（正则匹配非命令文本，编译时带 100ms 超时防 ReDoS）。`[BotCallback(action)]`：`Action` / `AdminOnly`。`TelegramBotContext` 统一封装一次更新（`Bot` / `Update` / `Message` / `Callback` / `ChatId` / `UserId` / `Text` / `IsCommand` / `IsCallback` / `IsReply` / `IsGroup` / `IsChannel` / `IsAdmin` / `LanguageCode`（Telegram 客户端语言，未识别回退 `zh-CN`）等 + `GetCommandToken` / `GetCommandArgs` / `GetCallbackAction` / `SetCallbackAnswer` / `MarkCallbackAnswered`）。

### 平台层 · 可插拔 store

| 接口 | 默认实现 | 说明 |
| --- | --- | --- |
| `ITelegramBotConfigStore` | `DefaultTelegramBotConfigStore` | `Task<IReadOnlyList<TelegramBotConfig>> GetBotConfigsAsync(ct)`；默认读选项 `.Bots` |
| `ITelegramBotSettingsStore` | `DefaultTelegramBotSettingsStore` | `Task<TelegramBotSettings> GetSettingsAsync(ct)`；默认读选项 `.Settings` |
| `IConversationStateStore` | `InMemoryConversationStateStore` | 多轮会话状态（key `"{botName}:{chatId}:{userId}"`，默认 TTL 10 分钟） |
| `ITelegramUpdateDeduplicator` | `InMemoryTelegramUpdateDeduplicator` | 更新去重（`TryMarkProcessedAsync`/`TryUnmarkAsync`，TTL 30 分钟） |
| `ITelegramMessageAuditStore` | `NoOpTelegramMessageAuditStore` | 出站审计（`AppendAsync(TelegramMessageAuditRecord, ct)`） |

`ConversationState`：`Step`（当前步骤）/ `Payload`（上下文 JSON）/ `CreateTime`。

### DI 入口

| 方法 | 说明 |
| --- | --- |
| `AddXiHanBotTelegram(this IServiceCollection, Action<TelegramOptions>? configure = null)` | 注册单发层：`DefaultTelegramConfigStore`（TryAdd）+ `TelegramBotProvider`（TryAddEnumerable） |
| `AddXiHanBotTelegramPlatform(this IServiceCollection, Action<TelegramBotPlatformOptions>? configure = null)` | 注册平台层全套（stores/routers/catalog/dispatcher/notifier/registry/manager，均 TryAdd + `TelegramBotHostedService` 托管） |
| `AddTelegramBotHandler<THandler>(this IServiceCollection)` | 注册处理器（transient），并加入 `TelegramBotHandlerOptions.Handlers` |
| `AddTelegramBotBuiltinHandlers(this IServiceCollection)` | 注册内置 `/start` `/help` `/myid` 处理器 |
| `UseTelegramBotWebhook(this IApplicationBuilder)` | 挂载 `TelegramBotWebhookMiddleware` |

## 配置

### `TelegramOptions`（单发层）

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `true` | 是否启用该提供者 |
| `Token` | `string` | `""` | Bot 令牌（必填） |
| `ChatId` | `string` | `""` | 默认会话 ID 或用户名 |
| `ParseMode` | `string?` | `null` | 默认解析模式（`Html`/`Markdown`/`MarkdownV2`/`None`） |
| `DisableNotification` | `bool` | `false` | 是否静默（不提示音） |

`TelegramMessageDataKeys`：`TelegramChatId = "Telegram.ChatId"`、`TelegramParseMode = "Telegram.ParseMode"`（写入 `BotMessage.Data` 按条覆盖）。

### `TelegramBotPlatformOptions`（平台层，配置节 `XiHan:Bot:Telegram:Platform`）

由 `Settings`（`TelegramBotSettings`）、`Bots`（`List<TelegramBotConfig>`）、`Retry`（`TelegramBotRetryOptions`）、`Texts`（`TelegramBotTexts`）组成。

`TelegramBotSettings` 关键字段：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `false` | 是否启用整个平台（`false` 时 manager 空转） |
| `ManagerRefreshSeconds` | `int` | `5` | 配置/设置变更轮询间隔（≤0 关闭刷新） |
| `WebhookBaseUrl` | `string` | `""` | Webhook 基址（非空=Webhook；空=长轮询） |
| `WebhookRoutePrefix` | `string` | `"/api/telegram-bot/webhook"` | Webhook 路由前缀 |
| `WebhookSecretToken` | `string` | `""` | Webhook 校验 secret（Webhook 模式下 fail-closed，必须非空） |
| `ConfigCacheSeconds` | `int` | `5` | 配置列表缓存 TTL（应用层 DB store 用） |
| `EnableFallbackReply` | `bool` | `false` | 全局兜底回复（与单机器人设置 OR 组合） |
| `Network` | `TelegramBotNetworkOptions` | `new()` | 代理 / 自定义 BaseUrl / 超时 |

`TelegramBotConfig`（单机器人）：`Id`（数据库来源配置 Id，仅用于变更检测与审计定位）、`Name`（业务名，唯一，忽略大小写）、`Token`（必填）、`AdminUsers`（`long[]`）、`AllowedGroupChatIds`（`long[]`，**fail-closed：空=拒绝所有群与频道**；私聊不受限；白名单同时覆盖群组与频道，频道 ChatId 与超级群同为 -100 前缀）、`AllowedCommands`（`string[]`，空=不限）、`EnableFallbackReply`、`Remark`。`IsSameAs` 逐字段比对（含 Name/Token/Id/AdminUsers/AllowedGroupChatIds/AllowedCommands/EnableFallbackReply），任一变化 Manager 即重启该机器人。

`TelegramBotNetworkOptions`：`ProxyUrl`（空=直连，支持 `user:pass@host:port` 形式的代理凭证）、`BaseUrl`（空=官方 api.telegram.org，可指向自建 Bot API Server）、`TimeoutSeconds`（默认 100，`<=0` 按 100 处理）。
`TelegramBotRetryOptions`：`MaxRetries`（3，不含首次发送）、`BaseDelayMs`（500，首次重试延迟，之后翻倍）、`MaxDelayMs`（10000）、`NotifyAdminOnFinalFailure`（true）。
`TelegramBotPlatformConsts`：`SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token"`、`CallbackDataSeparator = ':'`、`StartCommand = "/start"`、`DefaultWebhookRoutePrefix = "/api/telegram-bot/webhook"`。

`TelegramBotTexts`（平台文案，带中文默认值，应用层可整体覆盖）：

| 字段 | 默认值 | 用途 |
| --- | --- | --- |
| `InternalErrorReply` | "系统处理异常，已记录日志，请稍后重试或联系管理员。" | 分发异常兜底回复 |
| `CommandDisabledReply` | "该命令未开启。" | 命令不在白名单 |
| `AdminOnlyCommandReply` | "该命令仅管理员可用。" | 非管理员执行管理员命令 |
| `AdminOnlyCallbackReply` | "该操作仅管理员可用。" | 非管理员点击管理员按钮 |
| `UnhandledMessageReply` | "暂不支持处理该消息，发送 /help 查看可用命令。" | 无处理器命中且兜底回复开启 |
| `SendFailureAdminNotifyTitle` | "消息发送失败告警" | 发送最终失败通知管理员的标题 |
| `StartReply` | "你好，欢迎使用 {botUsername}！发送 /help 查看可用命令。" | 内置 `/start` 欢迎语（支持 `{botUsername}` 占位符） |
| `HelpHeader` | "可用命令：" | 内置 `/help` 列表头部 |
| `MyIdReply` | "ChatId：{chatId}\nUserId：{userId}" | 内置 `/myid` 回复（支持 `{chatId}` / `{userId}` 占位符） |

## 使用示例

### 1. 单发通知（走统一 `IBotClient`）

```csharp
var msg = new BotMessage { Content = "*部署完成*", Type = BotMessageType.Markdown };
// 可按条覆盖目标 chat 与解析模式
msg.Data[TelegramMessageDataKeys.TelegramChatId] = "-1009876543210";
msg.Data[TelegramMessageDataKeys.TelegramParseMode] = "MarkdownV2";
await bot.SendAsync(msg, new[] { BotProviderNames.Telegram });
```

### 2. 多机器人平台：命令处理器 + 主动发送

```csharp
// 声明命令处理器（显式注册，无扫描）
[BotCommand("/ping", Description = "健康检查")]
public sealed class PingCommandHandler : IBotCommandHandler
{
    public async Task HandleAsync(TelegramBotContext ctx, string[] args, CancellationToken ct)
        => await ctx.Client.SendMessage(ctx.ChatId, "pong", cancellationToken: ct);
}

// 注册：services.AddTelegramBotHandler<PingCommandHandler>();

// 业务里用 ITelegramNotifier 主动推送（带重试与审计）
public class ReleaseNotifier(ITelegramNotifier notifier)
{
    public Task NotifyAsync(CancellationToken ct)
        => notifier.SendMarkdownAsync("ops-bot", -1001234567890, "*release-2026.7* 已上线", cancellationToken: ct);
}
```

Webhook 模式下别忘了在管道里 `app.UseTelegramBotWebhook();`。

## 扩展点 / 自定义

- **业务处理器**：实现相应 `IBot*Handler` 接口并加特性，用 `AddTelegramBotHandler<T>()` 显式注册（**不做程序集扫描**；`TelegramBotHandlerCatalog` 在构建路由表时对缺特性、重复命令/动作、未实现接口**快速失败**）。
- **换 store**：会话状态/去重/审计/配置四类 store 均以 `TryAdd` 注册，多实例部署应把内存实现换成分布式实现（如 Redis），配置/设置换成数据库 store。
- **单发 vs 平台**：只发通知用单发层即可；要交互/多机器人再启用平台层（`Settings.Enabled=true`）。

## 注意事项与最佳实践

- **Webhook secret 必须配**：Webhook 模式下 `WebhookSecretToken` 为空会拒绝请求（fail-closed，401）。
- **群/频道白名单 fail-closed**：`AllowedGroupChatIds` 为空时拒绝所有群组与频道消息（私聊与内置命令 `/start` `/myid` `/id` `/help` `/h` 例外，但仍受命令白名单/AdminOnly 约束）。
- **平台默认关**：不设 `Settings.Enabled=true` 平台层不工作。
- **多实例慎用内存 store**：内存去重/会话/审计仅进程内有效，横向扩展需换分布式实现。
- **旧实例延迟释放**：配置热更后旧 `BotInstance` 保活 150 秒再释放，避免打断在途 Webhook 分发/重试。

## 依赖模块

- [XiHan.Framework.Bot](./bot)（内核，`IBotProvider` / `BotResult` / `BotMessage`）
- `Telegram.Bot` 22.10.1.1（官方 SDK）、`Microsoft.AspNetCore.App`（Webhook 端点）

## 相关模块

- [XiHan.Framework.Bot.Email](./bot-email) · [XiHan.Framework.Bot.Sms](./bot-sms)
- [XiHan.Framework.Bot.DingTalk](./bot-dingtalk) · [XiHan.Framework.Bot.Lark](./bot-lark) · [XiHan.Framework.Bot.WeCom](./bot-wecom)
