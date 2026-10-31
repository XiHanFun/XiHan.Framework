# XiHan.Framework.Localization.Abstractions

本地化的抽象包：区域名与内置 10 种区域、按键与区域取模板的消息目录 `MessageCatalog`、显式区域的解析接口 `IMessageLocalizer` 与目录注册。各子系统的抽象包、宿主与应用都经它注册语言文件、取调用方语言的文案。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Localization.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `localization.resources@1`，由 [Localization.InMemory](./localization-inmemory)（默认实现）或 [Localization.Database](./localization-database) 提供 |
| 特性入口 | — |
| 配置节 | `XiHan:Localization` |
| 依赖 | `XiHan.Framework.Kernel`；`Microsoft.Extensions.Configuration.Abstractions` 10.0.13、`Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.13 |
| AOT | 兼容（语言文件经源生成的 JSON 上下文解析） |
| 稳定性 | Stable |

## 概述

消息键文法、模板语法、`key.L()` 与 `MessageScope` 在原语层（`XiHan.Framework.Primitives`），本包在其上补齐区域与目录：

- `SupportedLocales` 列出内置的 10 种区域（与 XiHan.UI 相同），`Default` 是 `en-US`。
- `LocaleName` 定义区域名文法 `^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$`（最长 35 个字符）与大小写规范化。
- `MessageCatalog` 是消息键 → 区域 → 模板的只读目录，`FromEmbeddedResources` 装载一个程序集嵌入的全部 `Localization/<区域>.json`。目录只做精确查找，回退由 `IMessageLocalizer` 负责。
- `LocalizationOptions` 持有默认区域与启用列表，给出回退链 `FallbackChain`，并从候选区域协商出启用的区域（`Negotiate`、`Resolve`）。
- `IMessageLocalizer` 按显式区域解析并给出实际命中的区域（`LocalizedText`），宿主据此设置 `Content-Language`。

本包没有特性，也不注册本地化器；装配 `localization.resources@1` 的 Provider 才登记 `IMessageLocalizer` 与 `IMessageResolver`。

## 何时使用

- 应用或能力包有自己的文案：定义 `<领域>Messages` 键常量、写语言文件、用 `AddMessageCatalog` 注册目录。
- 需要在调用链之外按指定区域取文案，或要知道命中的区域：注入 `IMessageLocalizer`。
- 实现新的本地化 Provider：实现 `IMessageLocalizer` 与 `IMessageResolver`，并通过 `Localization.Conformance`。

用法与回退规则的完整讲解见[国际化](../guide/localization)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Localization.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Minimal`（以及包含它的 `Standard`、`Full`）已引用本包与默认实现。

应用的语言文件以固定逻辑名嵌入：

```xml
<ItemGroup>
  <EmbeddedResource Include="Localization/*.json" LogicalName="Localization/%(Filename)%(Extension)" />
</ItemGroup>
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `DefaultLocale` | `string` | `en-US` | 协商不到时的区域；进程默认作用域用它 |
| `Locales` | `string`（逗号分隔） | 内置 10 种 | 启用的区域，按优先级排列；去重并规范化 |

```json
{ "XiHan": { "Localization": { "DefaultLocale": "zh-CN", "Locales": "zh-CN,zh-TW,en-US" } } }
```

`LocalizationOptions.FromConfiguration` 读取这两个键并调用 `Validated()`：默认区域必须在启用列表里，区域名必须合法，启用列表不能为空，否则抛出 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `Locales.SupportedLocales` | `All` 与各区域常量（`EnglishUnitedStates`、`ChineseSimplified`、`ChineseTraditional`、`Japanese`、`Korean`、`German`、`Spanish`、`French`、`PortugueseBrazil`、`Russian`），`Default` = `en-US` |
| `Locales.LocaleName` | `IsValid`、`Validate`、`Normalize`（`zh-cn` → `zh-CN`）、`LanguageOf`（`zh-CN` → `zh`）、`CultureOf`、`AreEqual`；`MaxLength` = 35 |
| `LocalizationOptions` | `DefaultLocale`、`Locales`、`FallbackChain(locale)`、`Negotiate(candidates)`、`Resolve(context)`、`FromConfiguration`、`Validated()` |
| `Messages.MessageCatalog` | 构造（目录名 + 区域 → 键 → 模板）、`FromEmbeddedResources(name, anchor, prefix)`、`ParseLocaleFile`、`Find(key, locale)`（精确匹配）、`Keys`、`Locales`、`LocalesOf(key)`；`DefaultResourcePrefix` = `Localization/` |
| `Messages.IMessageLocalizer` | `Find`、`Resolve(key, locale, arguments)`、`Localize(...)`、`Resolve(error, locale)`、`Localize(error, ...)`、`Options` |
| `Messages.LocalizedText` | 一次解析的 `Text` 与实际命中的 `Locale` |
| `Messages.InvocationContextLocalizationExtensions` | `context.Localize(key, arguments)`、`context.Localize(error)`；没有装配本地化时退回描述与 `Error.Message` |
| `LocalizationServiceCollectionExtensions` | `AddMessageCatalog(catalog)`，同名目录只保留第一次注册 |
| `LocalizationCapabilities` | 能力 ID `Resources` 与接口版本 `Version` |
| `LocalizationErrors` | 错误码常量 |

## 使用示例

定义键、注册目录、取文案：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace Orders;

// 只定义键，文案在 Localization/<区域>.json
public static class OrdersMessages
{
    /// <summary>订单不存在。占位符：<c>id</c>。</summary>
    public const string NotFound = "orders.order.not_found";
}

public static class OrdersLocalization
{
    public static IServiceCollection AddOrdersMessages(this IServiceCollection services)
        => services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("orders", typeof(OrdersMessages)));

    public static Error OrderNotFound(Guid id)
        // 只带键与参数，宿主在 Egress 按调用方语言翻译
        => Error.NotFound("ORDERS_NOTFOUND_001", OrdersMessages.NotFound, MessageArguments.Of(("id", id)));
}
```

```json
{ "orders.order.not_found": "Order '{id}' was not found." }
```

```json
{ "orders.order.not_found": "订单 '{id}' 不存在。" }
```

按显式区域解析并取命中的区域：

```csharp
var resolved = localizer.Resolve(OrdersMessages.NotFound, "zh-HK", MessageArguments.Of(("id", 42)));
var header = resolved.TryGetValue(out var text) ? text.Locale : localizer.Options.DefaultLocale;   // "zh-CN"
```

从 `Accept-Language` 一类的候选里协商区域：

```csharp
var options = LocalizationOptions.FromConfiguration(configuration);
var locale = options.Negotiate(["fr-CA", "de-DE"]);   // 精确命中优先："de-DE"
var chain = options.FallbackChain("zh-HK");          // zh-HK, zh-CN, zh-TW, en-US
```

## 行为与限制

- 目录装载：`FromEmbeddedResources` 只认逻辑名为 `<前缀><区域>.json` 的资源，文件名必须是合法区域名；每个文件是键 → 模板的平面 JSON 对象。JSON 不合法、键不符合消息键文法、模板为 `null`、程序集里一个语言文件都没有时，抛出 `FrameworkException`（`LOCALIZATION_VALIDATION_002`）。
- 区域名在装载时规范化，查找不区分大小写；同一目录里一个区域只能出现一次。
- `AddMessageCatalog` 按目录名去重，重复注册静默跳过。
- `Negotiate` 先找任一候选的精确命中，再按语言子标签命中启用列表里第一个同语言区域（`zh` 或 `zh-HK` 命中 `zh-CN`），都没有时返回默认区域；不合法的候选忽略。
- `FallbackChain` 的顺序是请求区域 → 启用列表里同语言的区域 → 默认区域 → `en-US`，去重。

| 错误码 | 常量 | 含义 |
| --- | --- | --- |
| `LOCALIZATION_VALIDATION_002` | `InvalidCatalog` | 消息目录不合法（JSON、键或区域名错误） |
| `LOCALIZATION_NOTFOUND_001` | `TranslationNotFound` | 数据库里没有这条翻译（`Localization.Database` 使用） |
| `LOCALIZATION_VALIDATION_001` | `InvalidLocale` | 区域名不合法（已登记；代码里区域名不合法时抛出 `ArgumentException`） |
| `LOCALIZATION_CONFLICT_001` | `DuplicateCatalog` | 同名消息目录重复注册（已登记；`AddMessageCatalog` 对重复注册静默跳过） |

## 相关

- [国际化](../guide/localization)
- [Localization.InMemory](./localization-inmemory)
- [Localization.Database](./localization-database)
- [Primitives 原语层](./primitives)
- 规范：RFC-0010《本地化》
