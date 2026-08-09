# 机器人

把「往哪些渠道发通知」和「用哪个厂商发」拆开：业务只面对一个 `IBotClient`，邮件 / 短信 / Telegram / 钉钉 / 飞书 / 企业微信各是一个可插拔的通道子包，凭证由数据库供给而不是写死在 `appsettings`。

完整 API 与配置全表见 [Bot 包](../packages/bot) 及各通道包文档。

## 一套抽象，六个通道

核心包 `XiHan.Framework.Bot` 只做编排——它不含任何平台 SDK。每个通道是独立子包，各自实现 `IBotProvider` 并引入自己的第三方 SDK。

| 通道 | 包 | 模块类 | 提供者名 | 第三方 SDK |
| --- | --- | --- | --- | --- |
| 邮件 | `XiHan.Framework.Bot.Email` | `XiHanBotEmailModule` | `Email` | MailKit |
| 短信 | `XiHan.Framework.Bot.Sms` | `XiHanBotSmsModule` | `Sms` | 阿里云 / 腾讯云 SDK |
| Telegram | `XiHan.Framework.Bot.Telegram` | `XiHanBotTelegramModule` | `Telegram` | Telegram.Bot |
| 钉钉 | `XiHan.Framework.Bot.DingTalk` | `XiHanBotDingTalkModule` | `DingTalk` | 无（HTTP Webhook） |
| 飞书 | `XiHan.Framework.Bot.Lark` | `XiHanBotLarkModule` | `Lark` | 无（HTTP Webhook） |
| 企业微信 | `XiHan.Framework.Bot.WeCom` | `XiHanBotWeComModule` | `WeCom` | 无（HTTP Webhook） |

提供者名常量集中在 `BotProviderNames`，别手写字符串。

**渠道（Channel）不是提供者（Provider）**：渠道是逻辑名（如 `"ops-alert"`），映射到一组提供者。业务按渠道名发送，底层用哪几个厂商由配置决定。`BotProviderManager.ResolveProviders` 先查渠道表，没命中才按提供者名直接匹配——所以两者可以混着传。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Bot
dotnet add package XiHan.Framework.Bot.DingTalk   # 想要哪个通道就装哪个
dotnet add package XiHan.Framework.Bot.Email
```

在模块上依赖内核模块与需要的通道子模块，再用 `AddXiHanBot` 组装：

```csharp
[DependsOn(
    typeof(XiHanBotModule),
    typeof(XiHanBotDingTalkModule),
    typeof(XiHanBotEmailModule)
)]
public class MyNotificationModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanBot(bot =>
        {
            bot.Configure(options =>
               {
                   options.DefaultStrategy = BotStrategyNames.Failover;
                   options.RateLimitPerSecond = 5;
               })
               .AddChannel("ops-alert", BotProviderNames.DingTalk, BotProviderNames.Email);
        });
    }
}
```

`AddXiHanBot` 注册的都是单例：`BotProviderManager`、`BotDispatcher`、`IBotClient`、`IBotTemplateEngine`，三个策略（`Broadcast` / `Failover` / `Priority`），四个管道（`LoggingPipeline` → `EnvironmentFilterPipeline` → `RetryPipeline` → `RateLimitPipeline`，注册顺序即洋葱顺序）。

::: tip 子模块已经把提供者注册好了
`XiHanBotEmailModule` 之类的通道模块在 `ConfigureServices` 里各自调用了 `AddXiHanBotEmail()`，提供者与默认配置存储已经进 DI。`BotBuilder` 上的 `UseEmail(...)` / `UseDingTalk(...)` / `UseLark(...)` / `UseWeCom(...)` / `UseTelegram(...)` 是**顺带写选项**的另一条路——它们的 `configure` 参数是必填的，不能像 `UseSms()` 那样空调用。
:::

## 发一条消息

```csharp
public class AlarmService(IBotClient bot)
{
    public async Task NotifyAsync(CancellationToken ct)
    {
        var message = new BotMessage
        {
            Title = "磁盘告警",
            Content = "node-01 磁盘使用率 92%",
            Type = BotMessageType.Markdown,
            Mentions = { "13800000000" }
        };

        // 只发 "ops-alert" 渠道；channels 传 null 或空 = 广播给全部已注册提供者
        var result = await bot.SendAsync(message, ["ops-alert"], ct);

        if (!result.IsSuccess)
        {
            foreach (var item in result.Results.Where(x => !x.IsSuccess))
            {
                // item.Provider / item.Message 定位是哪个通道挂了
            }
        }
    }
}
```

链式写法：

```csharp
await bot.Alert()
    .Title("部署完成")
    .Content("**release-2026.8** 已上线")
    .Type(BotMessageType.Markdown)
    .Mention("@all")
    .SendTo("ops-alert")
    .SendAsync(ct);
```

`IBotClient` 另有 `SendTemplateAsync`（按 `BotTemplate` 名渲染后发）、`SendBatchAsync`（逐条发、逐条返回）、`SendDelayedAsync`（`Task.Delay` 后发，**不落盘**，进程重启即丢）。

### 富消息走 `Data` 扩展键

`BotMessage.Content` 是所有通道的公共部分。平台特有的结构（钉钉 ActionCard、飞书富文本、企业微信图文……）放进 `Data` 字典，键名由各子包的 `{X}MessageDataKeys` 提供，大小写不敏感：

```csharp
var message = new BotMessage { Type = BotMessageType.Card, Content = "订单待审批" };
message.Data[DingTalkMessageDataKeys.DingTalkActionCard] = new DingTalkActionCard { /* … */ };
message.Data[EmailMessageDataKeys.EmailTo] = new[] { "ops@example.com" };
message.Data[SmsMessageDataKeys.PhoneNumbers] = "13800000000,13900000000";
```

提供者按 `Type` 选分支，取不到对应的 `Data` 就**回落成纯文本**发出去——不会报错，所以键名拼错的表现是「消息发出去了但样式不对」。

`BotMessageType` 取值：`Text` / `Markdown` / `Card` / `Image` / `File` / `Link`。

## 关键机制

### 一次调度的完整链路

```
IBotClient.SendAsync
   └─ BotDispatcher.DispatchAsync
        1. 规范化 channels
        2. BotProviderManager.ResolveProviders → 选中的提供者列表
        3. 无提供者 → ThrowWhenNoProvider ? 抛异常 : BotDispatchResult.NoProvider
        4. 解析策略：消息 Data["Strategy"] ?? DefaultStrategy，找不到回退 Broadcast
        5. 管道洋葱包裹策略：Logging → EnvironmentFilter → Retry → RateLimit → 策略
        6. 策略逐个调 IBotProvider.SendAsync，结果写入 BotContext.Results
   └─ BotDispatchResult.From(results, isSkipped)
```

三个策略的区别：

| 策略 | 行为 |
| --- | --- |
| `Broadcast`（默认） | 发给全部选中提供者；某个失败后是否继续由 `ContinueOnError` 决定 |
| `Failover` | 按顺序发，**第一个成功就停** |
| `Priority` | 只发列表里的第一个提供者 |

单条消息想临时换策略，写 `message.Data[BotMessageDataKeys.Strategy] = BotStrategyNames.Failover;`。

### 成败判定：只看 `BotDispatchResult.IsSuccess`

::: danger 提供者异常不会冒泡
策略内部 `SafeSendAsync` 捕获了所有异常，转成 `BotResult.Failed(ex.Message, provider.Name)` 记进结果列表。所以 `SendAsync` 不抛异常**不代表发成功**。

只有两种情况会抛：`ThrowWhenNoProvider = true` 且无提供者；重试耗尽且 `context.LastException` 非空。
:::

`IsSuccess` 为 `true` 的条件是「至少有一个结果，且全部成功」。以下都是 `false`：

- 没有任何提供者被选中（`ErrorMessage = "No bot provider configured."`）；
- 被环境过滤跳过（`IsSkipped = true`）；
- 任意一个提供者失败（`ErrorMessage` 把各失败项拼成 `提供者:原因`）。

单项结果 `BotResult.Code` 是 `BotResultCodes`：`Success = 200` / `BadRequest = 400`（配置缺失、收件人为空这类调用方问题）/ `Failed = 500`。

### 重试只重发失败的提供者

`RetryPipeline` 每轮先 `ClearResults()`，然后把提供者列表收缩为上一轮失败的那几个再跑一遍。`RetryCount <= 1` 或开关关闭时整个管道透传。这意味着广播场景下**已成功的通道不会被重复打扰**。

### 限流是进程内的

`RateLimitPipeline` 用一个进程内滑动窗口队列限制每秒条数。多实例部署时每个实例各限各的，不是全局配额。

### 环境过滤

`EnableEnvironmentFilter = true` 且 `AllowedEnvironments` 非空时，`IHostEnvironment.EnvironmentName` 不在列表里就把整次调度标记 `IsSkipped` 并直接返回。默认关闭。开发环境防误发告警很有用，但别忘了它会让 `IsSuccess` 变成 `false`。

## 配置存储：把凭证搬进数据库

每个通道都有一个 `*ConfigStore` 接口，提供者**每次发送前**都会调它拿当前生效配置。默认实现从 `IOptionsMonitor` 读（也就是 `appsettings` 那套），生产环境应该换成数据库实现——这样运维在后台改完 Webhook 地址、换完密钥立刻生效，不用重启也不用把密钥写进配置文件。

| 通道 | 接口 | 默认实现读什么 |
| --- | --- | --- |
| 邮件 | `IEmailConfigStore` | `EmailOptions` |
| 短信 | `ISmsConfigStore` | **恒返回 `null`**（凭证不入配置文件） |
| Telegram 单发 | `ITelegramConfigStore` | `TelegramOptions` |
| 钉钉 | `IDingTalkConfigStore` | `DingTalkOptions` |
| 飞书 | `ILarkConfigStore` | `LarkOptions` |
| 企业微信 | `IWeComConfigStore` | `WeComOptions` |
| Telegram 机器人列表 | `ITelegramBotConfigStore` | `TelegramBotPlatformOptions.Bots` |
| Telegram 平台设置 | `ITelegramBotSettingsStore` | `TelegramBotPlatformOptions.Settings` |

前六个单配置 store 都只有一个 `GetAsync(CancellationToken)`，返回 `null` 表示未配置，提供者会返回 `BadRequest` 而不是静默成功。末两个 Telegram 平台 store 方法名不同也不返回 `null`：`ITelegramBotConfigStore.GetBotConfigsAsync` 返回机器人列表（无配置即空列表），`ITelegramBotSettingsStore.GetSettingsAsync` 返回全局设置对象。

### 用 `Replace` 覆盖，不要用 `TryAdd`

::: danger 顺序决定了必须用 Replace
框架的默认实现是以 `TryAddSingleton` 注册的，而被 `[DependsOn]` 依赖的模块**先于**你的模块执行 `ConfigureServices`——等你的代码跑到时，坑位已经被默认实现占了。此时再调 `TryAddSingleton` 是**静默 no-op**，表现为「我明明写了数据库 store，读到的还是 appsettings」。
:::

```csharp
[DependsOn(typeof(XiHanBotDingTalkModule))]
public class MyBotConfigModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 必须 Replace
        context.Services.Replace(
            ServiceDescriptor.Singleton<IDingTalkConfigStore, DbDingTalkConfigStore>());
    }
}

public class DbDingTalkConfigStore(IMyBotConfigRepository repository) : IDingTalkConfigStore
{
    public async Task<DingTalkOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetEnabledAsync("DingTalk", cancellationToken);
        if (entity is null)
        {
            return null;   // 未配置 → 提供者 fail-closed
        }

        return new DingTalkOptions
        {
            Enabled = entity.IsEnabled,
            WebHookUrl = entity.WebHookUrl,
            AccessToken = entity.AccessToken,
            Secret = DecryptSecret(entity.Secret),   // 解密责任在 store 实现内
            KeyWord = entity.KeyWord
        };
    }
}
```

::: warning 解密在 store 里做完
框架侧完全不感知加密方案。`SmsChannelConfig.AccessKeySecret` 的注释写得很直白：拿到手必须是明文。store 实现负责解密、负责租户过滤、负责缓存——出了 store 的边界，框架按明文用。
:::

### 短信通道多一层模板映射

云厂商短信必须按模板发，所以 `SmsChannelConfig` 里有一份 `TemplateMap`（JSON），把业务内部模板码翻译成服务商模板码：

```json
{
  "auth-sms-login-code": { "templateCode": "SMS_123456", "paramOrder": ["code", "minutes"] }
}
```

发送时在 `Data` 里给三个键：

```csharp
var message = new BotMessage();
message.Data[SmsMessageDataKeys.PhoneNumbers] = "13800000000,13900000000";
message.Data[SmsMessageDataKeys.TemplateCode] = "auth-sms-login-code";
message.Data[SmsMessageDataKeys.TemplateParams] = """{"code":"482913","minutes":"5"}""";
await bot.SendAsync(message, [BotProviderNames.Sms], ct);
```

阿里云按命名 JSON 参数发送（键名必须和控制台模板变量名一致），腾讯云用位置参数数组，顺序取自 `paramOrder`。`SmsProviderType` 只有 `Aliyun` 和 `TencentCloud` 两个取值；腾讯云还必须填 `SdkAppId` 与 `Region`。

`ISmsGatewayResolver` 每次解析都重读 store，按配置指纹（含密钥、签名、模板映射等字段）缓存已构建的网关客户端——**改配置即热生效**，不需要额外的缓存失效通知。无可用配置时返回 `null`，`SmsBotProvider` 直接判 `BadRequest`。

## Telegram：不止是一个发送通道

Telegram 子包里其实有两套东西，`XiHanBotTelegramModule` 会把两套都注册上：

| | 单发提供者 | 多机器人平台 |
| --- | --- | --- |
| 入口 | `AddXiHanBotTelegram()` | `AddXiHanBotTelegramPlatform()` |
| 用途 | 作为 `IBotClient` 的一个通道往固定 ChatId 推消息 | 托管多个机器人，**接收**并处理用户消息 |
| 配置 | `TelegramOptions`（Token / ChatId / ParseMode） | `TelegramBotPlatformOptions`，配置节 `XiHan:Bot:Telegram:Platform` |
| 默认状态 | `Enabled = true` | `Settings.Enabled = false`，宿主服务空转 |

只想发通知的话，看到这里就够了。要做交互机器人（命令、按钮、多步会话）才需要往下读。

### 平台怎么跑起来

`TelegramBotHostedService` 拉起 `TelegramBotManager`，管理器按 `ManagerRefreshSeconds`（默认 5 秒）轮询 `ITelegramBotConfigStore`，对比出新增 / 变更 / 删除，分别启动、重启、停止对应机器人。改数据库里的机器人配置**不用重启应用**。

传输模式由 `Settings.WebhookBaseUrl` 决定：

- **非空 → Webhook**：管理器调 `SetWebhook`，地址是 `{WebhookBaseUrl}{WebhookRoutePrefix}/{botName}`，同时带上 `secret_token`。
- **为空 → 长轮询**：管理器先 `DeleteWebhook`，再 `StartReceiving`。

`Settings.Network` 变更（代理、自建 Bot API Server、超时）会导致全部机器人客户端重建。

### Webhook 接入

除了填 `WebhookBaseUrl`，还要在管道里挂中间件：

```csharp
public override void OnApplicationInitialization(ApplicationInitializationContext context)
{
    var app = context.GetApplicationBuilder();

    app.UseTelegramBotWebhook();   // 匹配 POST {WebhookRoutePrefix}/{botName}

    app.UseAuthentication();
    app.UseAuthorization();
    // …
}
```

::: tip 挂在鉴权之前
中间件自己完成鉴权（下面的 secret_token 校验），路径不匹配就直接 `next()` 放行。把它放在会拒绝匿名请求的鉴权中间件之前，Telegram 的回调才进得来。
:::

路由前缀默认 `/api/telegram-bot/webhook`，可用 `Settings.WebhookRoutePrefix` 改（会被归一化：补前导 `/`、去尾部 `/`）。中间件只认 `POST` 且路径正好是「前缀 + 一段机器人名」，多一层路径段不匹配。

### secret_token 校验是强制的

::: danger Webhook 模式下 `WebhookSecretToken` 必填
请求体里的字段可以伪造，不能作为鉴权依据，所以框架 fail-closed：

- `WebhookSecretToken` 为空时，管理器**拒绝注册 Webhook**（记 Error 日志），中间件对所有 Webhook 请求一律返回 **401**；
- 配置了以后，中间件强制校验 `X-Telegram-Bot-Api-Secret-Token` 请求头，用固定时间比较（`CryptographicOperations.FixedTimeEquals`），不匹配返回 **401**。

「留空 = 不校验」在这里不成立。
:::

校验通过后中间件把 `Update` 反序列化、按机器人名从注册表取实例、`QueueDispatch` 排入后台分发，然后**立即返回 200**。处理生命周期与 HTTP 请求解耦：慢处理器不会被客户端断连半途取消，也不会因为 Telegram 超时重发而被幂等标记误丢。反序列化失败、机器人没找到，同样返回 200——避免 Telegram 对失败响应发起重发风暴。

请求头名与默认前缀都在 `TelegramBotPlatformConsts` 里：`SecretTokenHeaderName`、`DefaultWebhookRoutePrefix`。

### 处理器要显式注册

平台**不做程序集扫描**，没登记的处理器不会被路由：

```csharp
context.Services
    .AddTelegramBotBuiltinHandlers()              // /start /help /myid
    .AddTelegramBotHandler<OrderCommandHandler>()
    .AddTelegramBotHandler<ConfirmCallbackHandler>();
```

```csharp
[BotCommand("/order", Description = "查询订单", Aliases = ["/o"], AdminOnly = false)]
public sealed class OrderCommandHandler(ITelegramNotifier notifier) : IBotCommandHandler
{
    public async Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        await notifier.SendTextAsync(context.Bot.Name, context.ChatId, $"订单号：{args.FirstOrDefault()}",
            context.TriggerMessageId, replyMarkup: null, cancellationToken);
    }
}
```

七种处理器接口：

| 接口 | 触发时机 | 配套特性 |
| --- | --- | --- |
| `IBotCommandHandler` | `/命令`，或 `Pattern` 正则命中的普通文本 | `[BotCommand]` |
| `IBotCallbackHandler` | 内联键盘回调，按 `action:id` 的 `action` 路由 | `[BotCallback]` |
| `IBotInlineQueryHandler` | 内联查询 `@bot query`，返回 `InlineQueryResult` 列表 | `CanHandle` |
| `IBotStartPayloadHandler` | `/start payload` 深链，返回 `true` 即消费 | 按 `Order` 排序 |
| `IBotStateHandler` | 会话存在活跃 `ConversationState` 时的非命令消息 | `CanHandle` + `Order` |
| `IBotReplyHandler` | 用户回复了某条消息 | `CanHandle` + `Order` |
| `IBotMessageHandler` | 兜底的普通消息 | `CanHandle` + `Order` |

分发次序是固定的：群组/频道白名单守卫 → `update_id` 幂等 → 内联查询 → 会话状态机 → 回调路由 → `/start` 深链 → 命令路由 → 回复路由 → 消息路由 → 兜底回复。

::: warning 群组白名单为空 = 全部拒收
`TelegramBotConfig.AllowedGroupChatIds` 是 fail-closed 的：**空数组表示拒收所有群组与频道消息**，跟「空 = 不限制」的直觉正好相反。私聊不受影响；`/start`、`/myid`、`/id`、`/help`、`/h` 这几个命令永久放行（只豁免这一层守卫，命令白名单和 `AdminOnly` 仍然生效）。

而 `AllowedCommands` 是另一套语义：空数组才表示不限制。
:::

主动发消息用 `ITelegramNotifier`（按机器人名定位实例），内建 429 按 `RetryAfter` 精确等待、5xx / 超时指数退避的重试环，最终失败可按 `Retry.NotifyAdminOnFinalFailure` 通知管理员，每次发送都会写 `ITelegramMessageAuditStore`。

### 默认存储都可以换

平台把这些也做成了 `TryAdd` 的可替换实现，多实例部署时需要换成分布式版本：

| 接口 | 默认实现 | 多实例下的问题 |
| --- | --- | --- |
| `ITelegramUpdateDeduplicator` | `InMemoryTelegramUpdateDeduplicator` | 进程内 TTL 字典，**多实例失效** |
| `IConversationStateStore` | `InMemoryConversationStateStore` | 同上，会话状态不跨实例 |
| `ITelegramMessageAuditStore` | `NoOpTelegramMessageAuditStore` | 不落任何审计 |

替换方式同样是 `Replace`。

## 配置

内核的 `XiHanBotOptions` 是代码配置（渠道表与模板表是运行时字典），常用几项：

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `DefaultStrategy` | `"Broadcast"` | 默认策略，可被消息 `Data["Strategy"]` 覆盖 |
| `ContinueOnError` | `true` | 广播时某提供者失败后是否继续 |
| `ThrowWhenNoProvider` | `false` | 无提供者时抛异常还是返回失败结果 |
| `RetryCount` / `RetryDelay` | `3` / `1s` | `<= 1` 时重试管道不生效 |
| `RateLimitPerSecond` | `5` | `<= 0` 关闭限流 |
| `EnableEnvironmentFilter` | `false` | 配合 `AllowedEnvironments` 使用 |

Telegram 平台走配置节 `XiHan:Bot:Telegram:Platform`（仅作兜底，生产由 store 覆盖）：

```json
{
  "XiHan": {
    "Bot": {
      "Telegram": {
        "Platform": {
          "Settings": {
            "Enabled": true,
            "ManagerRefreshSeconds": 5,
            "WebhookBaseUrl": "https://example.com",
            "WebhookRoutePrefix": "/api/telegram-bot/webhook",
            "WebhookSecretToken": "",
            "EnableFallbackReply": false,
            "Network": { "ProxyUrl": "", "BaseUrl": "", "TimeoutSeconds": 100 }
          },
          "Bots": [
            {
              "Name": "ops",
              "Token": "",
              "AdminUsers": [ 10001 ],
              "AllowedGroupChatIds": [ -1001234567890 ],
              "AllowedCommands": []
            }
          ],
          "Retry": { "MaxRetries": 3, "BaseDelayMs": 500, "MaxDelayMs": 10000, "NotifyAdminOnFinalFailure": true }
        }
      }
    }
  }
}
```

::: warning 别把 Token 和 SecretToken 留在配置文件里
上面这份只适合本地调试。生产环境把 `ITelegramBotConfigStore` 和 `ITelegramBotSettingsStore` 换成数据库实现，Token、`WebhookSecretToken` 一并落库加密。
:::

`TelegramBotTexts` 里的全部回复文案（`InternalErrorReply`、`CommandDisabledReply`、`AdminOnlyCommandReply`、`UnhandledMessageReply`、`StartReply`、`HelpHeader`、`MyIdReply` 等）都带中文默认值，可整体覆盖。

## 常见问题

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 广播时莫名多出失败项 | `channels` 传空 = 发给**全部**已注册提供者，包括没配凭证的那些 | 显式传渠道名，或把没用的通道子模块摘掉 |
| 数据库 store 不生效，读到的还是 `appsettings` | 用了 `TryAdd`，坑位已被框架默认实现占掉 | 改用 `services.Replace(...)` |
| `SendAsync` 没抛异常但对方没收到 | 提供者异常被策略吞成 `Failed` 结果 | 判 `BotDispatchResult.IsSuccess`，遍历 `Results` 看明细 |
| 消息发出去了但样式不对 | `Data` 扩展键拼错或类型不匹配，提供者回落成纯文本 | 用 `{X}MessageDataKeys` 常量，别手写字符串 |
| Telegram Webhook 全部 401 | Webhook 模式下 `WebhookSecretToken` 为空，或请求头对不上 | 配置密钥并确保 `SetWebhook` 用的是同一个值 |
| Telegram Webhook 请求根本没进来 | 中间件没挂，或挂在鉴权之后被拦 | `app.UseTelegramBotWebhook()` 前置 |
| 群里发命令机器人不理 | `AllowedGroupChatIds` 为空 = 拒收全部群组消息 | 把群 ChatId 加进白名单（`/myid` 可查） |
| 命令回「该命令未开启」 | 命中了 `AllowedCommands` 白名单过滤 | 补进白名单，或清空该数组表示不限制 |
| 短信报「缺少模板码」 | 云厂商必须按模板发，`Sms.TemplateCode` 没给或 `TemplateMap` 没配 | 补 `TemplateMap` 映射与 `Data` 键 |
| 多实例部署 Telegram 消息被处理两次 | 幂等去重器是进程内字典 | 换成分布式 `ITelegramUpdateDeduplicator` 实现 |
| 限流没起到全局作用 | `RateLimitPipeline` 是进程内滑动窗口 | 需要全局配额得自己做分布式限流 |

## 下一步

- [配置与选项](./configuration) —— 选项绑定与配置节约定
- [依赖注入](./dependency-injection) —— `TryAdd` / `Replace` 的覆盖语义
- [Web 应用开发](./web) —— 中间件注册次序
- [Bot 包](../packages/bot) —— 内核完整 API 与配置全表
- [Bot.Email](../packages/bot-email) · [Bot.Sms](../packages/bot-sms) · [Bot.Telegram](../packages/bot-telegram)
- [Bot.DingTalk](../packages/bot-dingtalk) · [Bot.Lark](../packages/bot-lark) · [Bot.WeCom](../packages/bot-wecom)
