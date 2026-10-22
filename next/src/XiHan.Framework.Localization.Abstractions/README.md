# XiHan.Framework.Localization.Abstractions

本地化契约（Ring 2）。能力 `localization.resources@1` 由 Provider 提供。默认语言英文（`en-US`），内置与 XiHan.UI 相同的 10 种区域（RFC-0010）。

| 契约 | 用途 |
| --- | --- |
| `SupportedLocales` | 10 种内置区域：`en-US`（默认）、`zh-CN`、`zh-TW`、`ja-JP`、`ko-KR`、`de-DE`、`es-ES`、`fr-FR`、`pt-BR`、`ru-RU` |
| `LocaleName` | BCP 47 形态的区域名文法与规范化（`zh-cn` → `zh-CN`） |
| `LocalizationOptions` | 默认区域、启用的区域列表、从候选（Baggage `xihan.locale`、`Accept-Language`）协商区域 |
| `MessageCatalog` | 消息键 → 区域 → 模板的只读目录；`FromEmbeddedResources` 装载一个程序集嵌入的全部 `Localization/<区域>.json` |
| `IMessageLocalizer` | 显式区域的解析入口：`Resolve` 给出文案与实际命中的区域，`Localize` 退回键与参数的描述；回退链：精确区域 → 同语言 → 默认区域 → `en-US` |
| `LocalizedText` | 一次解析的文案与命中区域，宿主据此设置 `Content-Language` |
| `context.Localize(key, arguments)` | 在调用上下文上按其区域解析（没有装配本地化时返回描述） |
| `AddMessageCatalog` | 注册目录，同名只注册一次 |

消息键、模板语法、`key.L()` 与 `MessageScope` 在原语层：

```csharp
public static class OrdersMessages                     // 只定义键，文案在 Localization/<区域>.json
{
    public const string NotFound = "orders.order.not_found";
}

// Localization/en-US.json: { "orders.order.not_found": "Order '{id}' was not found." }
// Localization/zh-CN.json: { "orders.order.not_found": "订单 '{id}' 不存在。" }

services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("orders", typeof(OrdersMessages)));

var text = OrdersMessages.NotFound.L(("id", id));      // 调用链里是调用方语言，其他地方是默认语言
return Error.NotFound("ORDERS_NOTFOUND_001", OrdersMessages.NotFound, MessageArguments.Of(("id", id)));  // 宿主在 Egress 翻译
```

## 依赖方向

依赖 `XiHan.Framework.Kernel`。被各子系统的抽象包、`Hosting.AspNetCore` 与 `Profiles.Minimal` 引用。

## 状态

P2：可用。
