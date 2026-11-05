# XiHan.Framework.Localization.Abstractions

## 关于

`XiHan.Framework.Localization.Abstractions` 定义本地化契约：区域名与内置的 10 种区域、按键与区域取模板的消息目录、显式区域的解析接口。不含本地化器实现，实现由 `Localization.InMemory` 或 `Localization.Database` 提供。

## 主要特性

* 内置 `en-US`（默认）、`zh-CN`、`zh-TW`、`ja-JP`、`ko-KR`、`de-DE`、`es-ES`、`fr-FR`、`pt-BR`、`ru-RU`
* `MessageCatalog` 装载程序集嵌入的 `Localization/<区域>.json`
* 回退链：精确区域、同语言、默认区域、`en-US`
* 从 Baggage 与 `Accept-Language` 协商调用方区域

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Localization.Abstractions --prerelease
```

### 使用

一般由实现包或组合包带入；应用或能力包有自己的文案时才直接引用。定义键、嵌入语言文件并注册目录：

```csharp
public static class OrdersMessages
{
    public const string NotFound = "orders.order.not_found";   // 文案在 Localization/<区域>.json
}

services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("orders", typeof(OrdersMessages)));

var text = OrdersMessages.NotFound.L(("id", id));   // 调用链里是调用方语言
```

```xml
<EmbeddedResource Include="Localization/*.json" LogicalName="Localization/%(Filename)%(Extension)" />
```

配置键 `XiHan:Localization:DefaultLocale` 与 `XiHan:Localization:Locales`（逗号分隔）。

## 主要类型

此库提供的主要类型：

* `SupportedLocales`、`LocaleName`：内置区域与区域名规范化
* `LocalizationOptions`：默认区域、启用列表、区域协商
* `MessageCatalog`：消息键、区域、模板的只读目录
* `IMessageLocalizer`、`LocalizedText`：按显式区域解析
* `AddMessageCatalog`：注册目录

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/localization-abstractions)

## 相关包

* 默认的内存实现：[XiHan.Framework.Localization.InMemory](https://www.nuget.org/packages/XiHan.Framework.Localization.InMemory)
* 数据库实现：[XiHan.Framework.Localization.Database](https://www.nuget.org/packages/XiHan.Framework.Localization.Database)
* 一致性套件：[XiHan.Framework.Localization.Conformance](https://www.nuget.org/packages/XiHan.Framework.Localization.Conformance)

## 反馈与贡献

`XiHan.Framework.Localization.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
