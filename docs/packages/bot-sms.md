# XiHan.Framework.Bot.Sms

> Bot 短信通道：阿里云 / 腾讯云双网关短信提供者，内部模板码经映射转服务商模板码后发送。

- **NuGet**：`XiHan.Framework.Bot.Sms`
- **模块类**：`XiHanBotSmsModule`
- **所在层**：基础设施层
- **关键依赖**：`AlibabaCloud.SDK.Dysmsapi20170525`（4.4.0）、`TencentCloudSDK.Sms`（3.0.1435）；框架内部依赖 `XiHan.Framework.Bot`。

## 概述

本包是 [XiHan.Framework.Bot](./bot) 的**短信通道子包**。它实现 `IBotProvider`（名为 `Sms`），内置**阿里云**与**腾讯云**两家云短信网关，把短信封装为 Bot 内核可调度的一个提供者。

短信按云厂商**模板**发送，本包的关键设计是**内部模板码映射**：业务方在消息里带一个「内部模板码」（如 `auth-sms-login-code`），网关客户端按配置的 `TemplateMap` 把它转成服务商真实模板码（如阿里云 `SMS_123456` / 腾讯云 `TemplateId`），并按厂商差异组织参数（阿里云用命名 JSON 参数，腾讯云用 `ParamOrder` 排出的位置数组）。

短信凭证（云厂商 AccessKeySecret）不宜放配置文件，因此 `ISmsConfigStore` 默认实现**恒返回 `null`**，须由应用层注册数据库 store 实现覆盖（并在其中完成凭证解密）才能真正发信。

## 何时使用

- 需要通过阿里云 / 腾讯云发送模板短信（验证码、通知等），并与邮件/IM 通道走同一套策略与管道。
- 需要在阿里云与腾讯云之间按配置热切换（改数据库配置即换网关，无需重启）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Bot.Sms
```

```csharp
[DependsOn(typeof(XiHanBotSmsModule))]
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanBot(bot => bot.UseSms());
        // 关键：注册应用层配置 store（内含凭证解密）覆盖默认空实现
        context.Services.AddSingleton<ISmsConfigStore, DbSmsConfigStore>();
    }
}
```

`XiHanBotSmsModule` 依赖 `XiHanBotModule`，其 `ConfigureServices` 调用 `AddXiHanBotSms()`。`AddXiHanBotSms()` 与 `UseSms(this BotBuilder)` 均注册（`TryAdd` 语义）：

- `ISmsConfigStore` → `DefaultSmsConfigStore`（`TryAddSingleton`，恒返回 `null`）；
- `ISmsGatewayResolver` → `SmsGatewayResolver`（`TryAddSingleton`）；
- `IBotProvider` → `SmsBotProvider`（`TryAddEnumerable` Singleton）。

`UseSms()` **不接收 configure 委托**（凭证不走选项绑定）；凭证由 `ISmsConfigStore` 应用层实现提供。

## 工作原理

```
SmsBotProvider.SendAsync(message, context)
  1. 从 message.Data 取手机号（Sms.PhoneNumbers）、内部模板码（Sms.TemplateCode）、参数 JSON（Sms.TemplateParams）
  2. ISmsGatewayResolver.ResolveAsync() → 当前生效的 ISmsGatewayClient（无启用配置返回 null）
  3. gateway.SendAsync(SmsGatewayRequest) → 客户端内按 TemplateMap 映射模板码、组织参数、调 SDK
```

`SmsGatewayResolver` 每次 `ResolveAsync` 读 `ISmsConfigStore.GetAsync()` 当前配置，以 `ConfigId` 为键在 `ConcurrentDictionary` 中缓存已构建的客户端，并用**配置指纹**（ConfigId | Provider | AccessKeyId | AccessKeySecret | SdkAppId | SignName | Region | TemplateMap | IsEnabled）判断是否需要重建；指纹变化即重建客户端，实现**热切换**。按 `config.Provider` 分发：`Aliyun` → `AliyunSmsGatewayClient`，`TencentCloud` → `TencentCloudSmsGatewayClient`（`AccessKeySecret` 为空直接抛异常；腾讯云还须 `SdkAppId`/`Region`，缺任一同样抛异常）。

## 核心能力

- **多云网关**：`SmsProviderType` 覆盖阿里云 / 腾讯云；`AliyunSmsGatewayClient`（`dysmsapi.aliyuncs.com`）/ `TencentCloudSmsGatewayClient`（`sms.tencentcloudapi.com`，V20210111）分别对接对应 SDK。
- **网关解析与缓存**：`ISmsGatewayResolver` 按配置指纹构建并缓存 `ISmsGatewayClient`，配置变即重建。
- **模板码映射**：`SmsChannelConfig.TemplateMap`（JSON）把内部模板码映射为服务商模板码 + 参数序。
- **可插拔配置**：`ISmsConfigStore` 抽象渠道配置来源（默认空实现，应用层须提供 DB 实现）。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `SmsBotProvider` | `IBotProvider` 实现，`Name => BotProviderNames.Sms`（`"Sms"`）。`Task<BotResult> SendAsync(BotMessage, BotContext)` |
| `ISmsConfigStore` | `Task<SmsChannelConfig?> GetAsync(CancellationToken cancellationToken = default)`（返回 `null` 视为未配置，fail-closed） |
| `DefaultSmsConfigStore` | 默认实现，`GetAsync` 恒返回 `null`（须应用层覆盖） |
| `ISmsGatewayResolver` | `Task<ISmsGatewayClient?> ResolveAsync(CancellationToken cancellationToken = default)`（无启用配置返回 `null`） |
| `ISmsGatewayClient` | `SmsProviderType Provider { get; }`、`Task<SmsGatewaySendResult> SendAsync(SmsGatewayRequest request, CancellationToken)` |
| `SmsGatewayClientBase` | 网关客户端基类（模板映射解析 `ResolveMapping` / 参数解析 `ParseParams`） |
| `AliyunSmsGatewayClient` / `TencentCloudSmsGatewayClient` | 两家网关实现（`Provider` 分别为 `Aliyun` / `TencentCloud`） |
| `SmsProviderType` | `Aliyun = 0` / `TencentCloud = 1` |
| `SmsChannelConfig` | 渠道配置（见下表） |
| `SmsMessageDataKeys` | 消息 `Data` 键常量（见下表） |

关键 record：

- `SmsGatewayRequest(IReadOnlyList<string> PhoneNumbers, string? TemplateCode, string? TemplateParamsJson, string Content)`
- `SmsGatewaySendResult(bool IsSuccess, string? ProviderMessageId, string? ErrorMessage)`
- `SmsTemplateMapping(string TemplateCode, string[]? ParamOrder)`

## 配置

`SmsChannelConfig` 字段（由应用层 `ISmsConfigStore` 提供，通常来自数据库）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `ConfigId` | `long` | `0` | 配置标识（解析器按此键缓存网关客户端） |
| `Provider` | `SmsProviderType` | `Aliyun` | 短信服务商 |
| `AccessKeyId` | `string` | `""` | 阿里云 AccessKeyId / 腾讯云 SecretId |
| `AccessKeySecret` | `string` | `""` | 阿里云 AccessKeySecret / 腾讯云 SecretKey（已解密明文，由 store 负责解密） |
| `SdkAppId` | `string?` | `null` | 腾讯云 SmsSdkAppId（腾讯云必填；阿里云不用） |
| `SignName` | `string` | `""` | 短信签名（控制台审核通过的签名名称） |
| `Region` | `string?` | `null` | 腾讯云地域（如 `ap-guangzhou`，必填；阿里云不用） |
| `TemplateMap` | `string?` | `null` | 模板映射 JSON：内部模板码 → 服务商模板码 + 参数序 |
| `IsEnabled` | `bool` | `true` | 是否启用 |

`TemplateMap` 形如：`{"auth-sms-login-code":{"templateCode":"SMS_123456","paramOrder":["code","minutes"]}}`。阿里云按命名 JSON 参数发送（参数名须与服务商模板变量名一致），`paramOrder` 供腾讯云位置参数数组使用。

`SmsMessageDataKeys` 常量（写入 `BotMessage.Data`）：

| 常量 | 值 | 含义 |
| --- | --- | --- |
| `PhoneNumbers` | `"Sms.PhoneNumbers"` | 接收手机号（逗号分隔 `string` 或 `IEnumerable<string>`） |
| `TemplateCode` | `"Sms.TemplateCode"` | 内部模板码（经 `TemplateMap` 映射为服务商模板码） |
| `TemplateParams` | `"Sms.TemplateParams"` | 模板参数 JSON（键须与服务商模板变量名一致） |

## 使用示例

```csharp
var msg = new BotMessage { Content = "登录验证码" };
msg.Data[SmsMessageDataKeys.PhoneNumbers] = "13800000000,13900000000";
msg.Data[SmsMessageDataKeys.TemplateCode] = "auth-sms-login-code";   // 内部模板码
msg.Data[SmsMessageDataKeys.TemplateParams] = "{\"code\":\"8421\",\"minutes\":\"5\"}";
await bot.SendAsync(msg, new[] { BotProviderNames.Sms });
```

## 扩展点 / 自定义

- **必做：注册应用层 `ISmsConfigStore`**。默认 `DefaultSmsConfigStore` 恒返回 `null`（fail-closed，不会误发），须实现从数据库读取 `SmsChannelConfig`（并解密 `AccessKeySecret`）的 store，`services.AddSingleton<ISmsConfigStore, ...>()` 覆盖默认。
- 腾讯云手机号会自动规范化为 E.164：以 `+` 开头保持原样，否则前置 `+86`。

## 注意事项与最佳实践

- **未配置即不发**：`ISmsConfigStore` 默认空实现导致 `ResolveAsync` 返回 `null`，`SendAsync` 返回 `BadRequest`——这是刻意的 fail-closed，防止凭证缺失时静默误发。
- **密钥不可为空**：`AccessKeySecret` 为空会在构建网关客户端时直接抛异常（两家服务商均如此），被 `SmsBotProvider` 折叠为 `Failed`。
- **腾讯云需完整配置**：缺 `SdkAppId` 或 `Region` 会在解析时抛异常。
- **模板码必须映射**：`TemplateCode` 找不到对应 `TemplateMap` 条目会抛异常（`ResolveMapping`）；腾讯云还要求 `ParamOrder` 里每个键都能在 `TemplateParams` 找到值。
- **失败不外抛**：SDK 层错误在 provider 内转成 `BotResult.Failed`，不影响其它提供者。

## 依赖模块

- [XiHan.Framework.Bot](./bot)（内核，`IBotProvider` / `BotResult` / `BotMessage`）
- `AlibabaCloud.SDK.Dysmsapi20170525` 4.4.0、`TencentCloudSDK.Sms` 3.0.1435

## 相关模块

- [XiHan.Framework.Bot.Email](./bot-email) · [XiHan.Framework.Bot.Telegram](./bot-telegram)
- [XiHan.Framework.Bot.DingTalk](./bot-dingtalk) · [XiHan.Framework.Bot.Lark](./bot-lark) · [XiHan.Framework.Bot.WeCom](./bot-wecom)
