# XiHan.Framework.Bot

> 机器人 / 消息通知内核：统一消息模型 + 多提供者调度，把「往哪些渠道发」与「用哪个厂商发」解耦。

- **NuGet**：`XiHan.Framework.Bot`
- **模块类**：`XiHanBotModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（`XiHan.Framework.Http`、`XiHan.Framework.Templating`）；核心包**不含任何第三方平台 SDK**，具体平台 SDK 由各通道子包各自引入。

## 概述

本包是「机器人 / 消息通知」的**核心抽象与调度内核**。它定义了统一的消息模型（`BotMessage`）、提供者抽象（`IBotProvider`）、调度器（`BotDispatcher`）、发送策略（`IBotStrategy`）与横切管道（`IBotPipeline`），让你用一致的方式往多个渠道（邮件、短信、Telegram、钉钉、飞书、企业微信……）发消息。

核心包本身**不包含任何具体通道**——每个通道是一个独立子包（`XiHan.Framework.Bot.Email` / `.Sms` / `.Telegram` / `.DingTalk` / `.Lark` / `.WeCom`），各自实现 `IBotProvider` 并以 `UseXxx` 扩展方法接入。本包只负责**编排**它们：解析渠道、选提供者、跑管道、执行策略、聚合结果。

设计上把两件事解耦：**渠道（Channel）** 是逻辑名（如 `"ops-alert"`），**提供者（Provider）** 是真正干活的厂商实现（如 `"DingTalk"`）。一个渠道可映射到一组提供者，调用方只按渠道名发送，底层用哪几个厂商由配置决定。

## 何时使用

- 需要向多个平台**统一发送通知**，且希望调用方只面对一个 `IBotClient`。
- 需要**广播 / 主备 / 优先级**等发送策略，或**重试 / 限流 / 环境过滤**等横切能力。
- 需要按**模板**渲染消息（`SendTemplateAsync`），或**延迟 / 批量**发送。
- 想让「往哪些渠道发」与「用哪个厂商发」解耦，通过渠道映射灵活配置、按环境灰度。

不该用它的场景：只发单一渠道、无需策略/管道编排时，可直接引用对应子包的 `XxxBotProvider`。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Bot
# 再按需添加通道子包，例如：
dotnet add package XiHan.Framework.Bot.DingTalk
```

```csharp
[DependsOn(
    typeof(XiHanBotModule),
    typeof(XiHanBotDingTalkModule) // 想启用哪个通道就依赖哪个子模块
)]
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanBot(bot =>
        {
            bot.Configure(o => o.DefaultStrategy = BotStrategyNames.Broadcast)
               .UseDingTalk()                       // 由子包提供的 Use* 扩展
               .AddChannel("ops-alert", "DingTalk"); // 逻辑渠道 → 提供者
        });
    }
}
```

`XiHanBotModule` 依赖 `XiHanHttpModule` 与 `XiHanTemplatingModule`，其 `ConfigureServices` 只调用一次 `AddXiHanBot()`。`AddXiHanBot` 注册（均为单例）：

- `BotProviderManager`、`BotDispatcher`、`IBotClient` → `BotClient`、`IBotTemplateEngine` → `BotTemplateEngine`（`TryAddSingleton`）；
- 三个策略 `IBotStrategy`：`BroadcastStrategy` / `FailoverStrategy` / `PriorityStrategy`（`TryAddEnumerable`）；
- 四个管道 `IBotPipeline`：`LoggingPipeline` / `EnvironmentFilterPipeline` / `RetryPipeline` / `RateLimitPipeline`（`TryAddEnumerable`，注册顺序即执行顺序）；
- 同时 `AddXiHanTemplating()` 与 `AddOptions<XiHanBotOptions>()`。

`AddXiHanBot(Action<BotBuilder>? configure)` 的委托拿到 `BotBuilder`，用它配置选项、登记渠道/模板，并调用各子包的 `UseXxx()`。

## 工作原理

一次发送的完整链路（`BotClient` → `BotDispatcher.DispatchAsync`）：

```
BotClient.SendAsync(message, channels, ct)
      │
      ▼
BotDispatcher.DispatchAsync
  1. 规范化 channels（去空白/去空项）
  2. new BotContext(message, channels, ct)，StrategyName ← 消息 Data["Strategy"] 或 null
  3. BotProviderManager.ResolveProviders(channels)  →  选中的 IBotProvider 列表
        • channels 为空/null → 返回全部提供者（广播）
        • 命中 Options.Channels 里的渠道 → 展开为其 Providers 名单
        • 未命中 → 按提供者名直接匹配
  4. 无提供者 →（ThrowWhenNoProvider ? 抛异常 : 返回 BotDispatchResult.NoProvider）
  5. ResolveStrategy(StrategyName ?? DefaultStrategy)，找不到回退 Broadcast
  6. 管道包裹策略：pipelines.Reverse().Aggregate(finalStep, ...)（洋葱模型）
        Logging → EnvironmentFilter → Retry → RateLimit → [策略执行]
  7. 策略对每个 provider 调 SendAsync，结果写入 context.Results
      │
      ▼
BotDispatchResult.From(context.Results, context.IsSkipped)  →  聚合成败 + 各提供者明细
```

约定与要点：

- **管道是洋葱式中间件**：`IBotPipeline.InvokeAsync(context, next)`，通过 `_pipelines.Reverse().Aggregate` 组成责任链；每个管道读 `XiHanBotOptions` 上自己的开关，关闭时直接 `await next()` 透传。
- **提供者异常不外抛**（策略内 `SafeSendAsync` 捕获）：转成 `BotResult.Failed(ex.Message, provider.Name)` 记入结果，保证一个提供者挂掉不影响其它（除非 `ContinueOnError=false`）。
- **RetryPipeline 只重试失败的提供者**：每轮 `ClearResults` 后仅对上一轮失败的提供者重发；`RetryCount<=1` 或关闭时跳过；耗尽后若有 `LastException` 则抛出。
- **模板渲染走 `ITemplateService`**：`BotTemplateEngine` 在独立 scope 内取 `ITemplateService.RenderAsync(content, model)` 渲染 `Content`/`Title`。
- **fail-closed 判定看 `BotDispatchResult.IsSuccess`**：被环境过滤跳过（`IsSkipped=true`）、无提供者、任一提供者失败，`IsSuccess` 均为 `false`——调用方据此决定是否兜底。

## 核心能力

- **统一客户端入口**：`IBotClient` 提供发送 / 指定渠道发送 / 模板发送 / 批量发送 / 延迟发送，全部返回 `BotDispatchResult`（整体成败 + 各提供者明细）。
- **提供者抽象**：`IBotProvider` 定义「一个通道怎么发一条消息」，各子包实现它并以 `TryAddEnumerable` 注册，可多提供者共存。
- **调度器**：`BotDispatcher` 解析渠道 → 选提供者 → 应用管道 → 执行策略，统一聚合成 `BotDispatchResult`。
- **发送策略**：`Broadcast`（广播全部）、`Failover`（主备，成功即止）、`Priority`（仅发第一个），名称常量见 `BotStrategyNames`；可在消息 `Data["Strategy"]` 上按条覆盖默认策略。
- **横切管道**：日志（`LoggingPipeline`）、环境过滤（`EnvironmentFilterPipeline`）、重试（`RetryPipeline`）、限流（`RateLimitPipeline`），经 `XiHanBotOptions` 开关配置。
- **渠道映射**：`BotChannel` 把逻辑渠道名映射到一组提供者，`BotBuilder.AddChannel(name, ...providers)` 登记。
- **消息模板**：`IBotTemplateEngine` + `BotTemplate`，按模板名渲染出 `BotMessage`。
- **流式告警构建器**：`IBotClient.Alert()` 扩展返回 `BotAlertBuilder`，链式设置标题/内容/类型/@ 提及/渠道后 `SendAsync`。

## 主要 API / 类型

### 客户端与调度

| 类型 | 说明 |
| --- | --- |
| `IBotClient` | 客户端入口。`Task<BotDispatchResult> SendAsync(BotMessage, CancellationToken)`；`SendAsync(BotMessage, IReadOnlyList<string>? channels, CancellationToken)`；`SendTemplateAsync(string templateName, object? model, IReadOnlyList<string>? channels, CancellationToken)`；`SendBatchAsync(IEnumerable<BotMessage>, ...)` → `IReadOnlyList<BotDispatchResult>`；`SendDelayedAsync(BotMessage, TimeSpan delay, ...)` |
| `BotClient` | `IBotClient` 默认实现，委托 `BotDispatcher`；模板方法先经 `IBotTemplateEngine` 渲染 |
| `BotDispatcher` | 调度器。`Task<BotDispatchResult> DispatchAsync(BotMessage, IReadOnlyList<string>? channels, CancellationToken)` |
| `BotProviderManager` | 提供者管理。`IReadOnlyList<IBotProvider> GetAllProviders()`；`ResolveProviders(IReadOnlyList<string>? channels)` |
| `BotContext` | 单次调度上下文：`Message` / `Channels` / `CancellationToken` / `Items` / `Providers` / `Results` / `StrategyName` / `LastException` / `IsSkipped` / `IsSuccess` / `HasFailures`；`SetProviders(...)` / `AddResult(providerName, result)` / `ClearResults()` |

### 提供者、策略、管道

| 类型 | 说明 |
| --- | --- |
| `IBotProvider` | 通道提供者抽象：`string Name { get; }`、`Task<BotResult> SendAsync(BotMessage message, BotContext context)`。各子包实现 |
| `IBotStrategy` | 发送策略：`string Name { get; }`、`Task ExecuteAsync(BotContext context, IReadOnlyList<IBotProvider> providers)` |
| `BroadcastStrategy` / `FailoverStrategy` / `PriorityStrategy` | 内置三策略实现 |
| `BotStrategyNames` | 策略名常量：`Broadcast` / `Failover` / `Priority`（值同名字符串） |
| `IBotPipeline` | 横切管道：`Task InvokeAsync(BotContext context, Func<Task> next)` |
| `LoggingPipeline` / `EnvironmentFilterPipeline` / `RetryPipeline` / `RateLimitPipeline` | 内置四管道 |

### 消息与结果模型

| 类型 | 说明 |
| --- | --- |
| `BotMessage` | 统一消息：`string? Title`、`string Content`、`BotMessageType Type`（默认 `Text`）、`List<string> Mentions`、`Dictionary<string, object?> Data`（大小写不敏感，承载提供者扩展数据/策略覆盖等） |
| `BotMessageType` | 消息类型枚举：`Text` / `Markdown` / `Card` / `Image` / `File` / `Link` |
| `BotResult` | 单提供者结果：`BotResultCodes Code`（默认 `Success`）、`string? Message`、`object? Data`、`bool IsSuccess`（`Code==Success`）、`string? Provider`；静态工厂 `Success` / `BadRequest` / `Failed` / `From` |
| `BotResultCodes` | `Success=200` / `BadRequest=400` / `Failed=500` |
| `BotDispatchResult` | 聚合结果（`sealed`）：`bool IsSuccess`、`bool IsSkipped`、`string? ErrorMessage`、`IReadOnlyList<BotResult> Results`；静态工厂 `NoProvider(errorMessage)` / `From(results, isSkipped)` |
| `BotChannel` | 渠道定义：`string Name`、`List<string> Providers`、`string? Description` |

### 配置、构建器、模板、辅助

| 类型 | 说明 |
| --- | --- |
| `XiHanBotOptions` | Bot 配置项（见下节）；`AddChannel(BotChannel)` / `AddTemplate(BotTemplate)` |
| `BotBuilder` | 构建器（`sealed`）：`IServiceCollection Services`、`Configure(Action<XiHanBotOptions>)`、`AddChannel(string name, params string[] providers)`、`AddTemplate(BotTemplate)`。子包在其上以扩展方法加 `UseXxx()` |
| `BotAlertBuilder` | 流式告警构建器（`sealed`）：`Title` / `Content` / `Type` / `Mention(params string[])` / `SendTo(params string[])` / `SendAsync(CancellationToken)` |
| `BotClientExtensions` | `BotAlertBuilder Alert(this IBotClient)` |
| `IBotTemplateEngine` | 模板引擎：`Task<BotMessage> RenderAsync(string templateName, object? model)`、`RenderAsync(BotTemplate template, object? model)` |
| `BotTemplate` | 模板定义：`string Name`、`string? Title`、`string Content`、`BotMessageType Type`（默认 `Markdown`）、`Dictionary<string, object?> Data` |
| `BotMessageHelper` | `static bool TryGetData<T>(BotMessage message, string key, out T? value)` |
| `BotMessageDataKeys` | 内核通用 Data 键常量：`Strategy = "Strategy"`（专属键由各子包的 `{X}MessageDataKeys` 提供） |
| `BotProviderNames` | 提供者名常量：`DingTalk` / `Lark` / `WeCom` / `Telegram` / `Email` / `Sms` |

## 配置

`XiHanBotOptions` 通过 `AddOptions<XiHanBotOptions>()` 注册，一般用 `BotBuilder.Configure(...)` 或 `Configure<XiHanBotOptions>(...)` 代码配置（渠道/模板为运行时字典，通常代码登记）。

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `DefaultStrategy` | `string` | `BotStrategyNames.Broadcast`（`"Broadcast"`） | 默认发送策略名 |
| `ContinueOnError` | `bool` | `true` | 广播时某提供者失败后是否继续发其它 |
| `ThrowWhenNoProvider` | `bool` | `false` | 无可用提供者时是否抛异常（否则返回 `NoProvider` 结果） |
| `RetryCount` | `int` | `3` | 重试次数（`<=1` 时重试管道不生效） |
| `RetryDelay` | `TimeSpan` | `1s` | 重试间隔 |
| `RateLimitPerSecond` | `int` | `5` | 每秒最多发送条数（`<=0` 关闭限流） |
| `EnableLoggingPipeline` | `bool` | `true` | 是否启用日志管道 |
| `EnableRetryPipeline` | `bool` | `true` | 是否启用重试管道 |
| `EnableRateLimitPipeline` | `bool` | `true` | 是否启用限流管道 |
| `EnableEnvironmentFilter` | `bool` | `false` | 是否启用环境过滤 |
| `AllowedEnvironments` | `List<string>` | `[]` | 允许发送的环境名列表（启用过滤且非空时，`IHostEnvironment.EnvironmentName` 不在其中则整次调度被跳过） |
| `Channels` | `Dictionary<string, BotChannel>` | 空（大小写不敏感） | 渠道映射（只读字典，用 `AddChannel` 登记） |
| `Templates` | `Dictionary<string, BotTemplate>` | 空（大小写不敏感） | 模板映射（只读字典，用 `AddTemplate` 登记） |

## 使用示例

### 1. 注册通道 + 发送消息

```csharp
// 模块里注册内核 + 通道 + 渠道映射
context.Services.AddXiHanBot(bot =>
{
    bot.UseDingTalk()   // 由 XiHan.Framework.Bot.DingTalk 提供
       .UseEmail()      // 由 XiHan.Framework.Bot.Email 提供
       // 逻辑渠道 "ops-alert" 同时映射到钉钉与邮件两个提供者
       .AddChannel("ops-alert", BotProviderNames.DingTalk, BotProviderNames.Email);
});

// 业务里注入 IBotClient 发送
public class AlarmService(IBotClient bot)
{
    public async Task NotifyAsync(CancellationToken ct)
    {
        var message = new BotMessage
        {
            Title = "磁盘告警",
            Content = "node-01 磁盘使用率 92%",
            Type = BotMessageType.Markdown
        };

        // 只发 "ops-alert" 渠道（展开为钉钉 + 邮件）；渠道传 null 则广播全部提供者
        var result = await bot.SendAsync(message, new[] { "ops-alert" }, ct);
        if (!result.IsSuccess)
        {
            // fail-closed：逐提供者查看失败明细
            foreach (var r in result.Results.Where(x => !x.IsSuccess))
            {
                // r.Provider / r.Message
            }
        }
    }
}
```

### 2. 流式告警构建器

```csharp
await bot.Alert()
    .Title("部署完成")
    .Content("**release-2026.7** 已上线")
    .Type(BotMessageType.Markdown)
    .Mention("13800000000")
    .SendTo("ops-alert")   // 不调 SendTo 则广播全部提供者
    .SendAsync(ct);
```

### 3. 模板发送与按条覆盖策略

```csharp
// 登记模板
context.Services.AddXiHanBot(bot =>
{
    bot.AddTemplate(new BotTemplate
    {
        Name = "order-paid",
        Title = "订单已支付",
        Content = "订单 {{OrderNo}} 金额 {{Amount}} 已支付",  // 走 ITemplateService 渲染
        Type = BotMessageType.Markdown
    });
});

// 按模板名 + 模型渲染并发送
await bot.SendTemplateAsync("order-paid",
    model: new { OrderNo = "SO-1001", Amount = 99.00m },
    channels: new[] { "ops-alert" },
    cancellationToken: ct);

// 单条覆盖默认策略：在消息 Data 上写 BotMessageDataKeys.Strategy
var msg = new BotMessage { Content = "主备发送" };
msg.Data[BotMessageDataKeys.Strategy] = BotStrategyNames.Failover;
await bot.SendAsync(msg, ct);
```

## 扩展点 / 自定义

- **新增通道提供者**：实现 `IBotProvider`（`Name` + `SendAsync`），以 `services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, MyProvider>())` 注册即被 `BotProviderManager` 纳入调度；建议同时提供 `UseMyChannel()` 的 `BotBuilder` 扩展方法。参考六个官方通道子包的实现。
- **自定义策略 / 管道**：分别 `TryAddEnumerable` 注册 `IBotStrategy` / `IBotPipeline`。策略按 `Name` 匹配（消息可用 `Data["Strategy"]` 指定）；管道按注册顺序组成洋葱链，务必自行处理开关与 `next()` 透传。
- **替换核心服务**：`IBotClient` / `BotDispatcher` / `IBotTemplateEngine` 均以 `TryAddSingleton` 注册，可在 `AddXiHanBot` 之前注册自定义实现覆盖默认。
- **渠道 vs 提供者**：`ResolveProviders` 先查 `Options.Channels`（逻辑渠道 → 提供者名单），未命中再当作提供者名直接匹配——因此渠道名与提供者名可混用于 `channels` 参数。

## 注意事项与最佳实践

- **成败判定统一看 `BotDispatchResult.IsSuccess`**：无提供者、被环境过滤跳过、任一提供者失败都为 `false`；需要精确定位时遍历 `Results`（每项 `BotResult.Provider` 标识来源）。
- **提供者异常不会冒泡**：被策略内 `SafeSendAsync` 捕获成 `Failed` 结果，因此别指望 `SendAsync` 抛异常来感知失败——只有「耗尽重试且有 `LastException`」或 `ThrowWhenNoProvider=true` 无提供者时才抛。
- **限流是进程内单例**：`RateLimitPipeline` 用进程内滑动窗口，多实例部署时按实例各自限流，非全局。
- **环境过滤依赖 `IHostEnvironment`**：`EnableEnvironmentFilter=true` 且 `AllowedEnvironments` 非空时才生效；命中不允许的环境会将整次调度标记 `IsSkipped` 并直接返回（不发送、不算成功）。
- **凭据不放 `appsettings`**：各通道子包的 `IXxxConfigStore` 默认是空实现，凭据/Webhook 由应用层从数据库覆盖（详见各子包文档）。

## 依赖模块

- [XiHan.Framework.Http](./http)（通道适配的 HTTP 调用基础）
- [XiHan.Framework.Templating](./templating)（消息模板渲染，`ITemplateService`）

无第三方 SDK 依赖；具体平台 SDK 由各通道子包引入。

## 相关模块

- [XiHan.Framework.Bot.Email](./bot-email)
- [XiHan.Framework.Bot.Sms](./bot-sms)
- [XiHan.Framework.Bot.Telegram](./bot-telegram)
- [XiHan.Framework.Bot.DingTalk](./bot-dingtalk) · [XiHan.Framework.Bot.Lark](./bot-lark) · [XiHan.Framework.Bot.WeCom](./bot-wecom)
