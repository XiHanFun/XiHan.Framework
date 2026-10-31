# XiHan.Framework.Localization.InMemory

本地化的默认实现：汇集容器里注册的全部消息目录，按精确区域、同语言、默认区域、`en-US` 的回退链取模板并格式化，同时作为 `MessageScope` 的解析器让 `key.L()` 在任何地方可用。`Profiles.Minimal` 与 `Profiles.Standard` 都装配它。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Localization.InMemory` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `localization.resources@1`（`role: default`）；依赖 `kernel.hosting@1` |
| 特性入口 | `InMemoryLocalizationFeature` |
| 配置节 | `XiHan:Localization` |
| 依赖 | `XiHan.Framework.Localization.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

`InMemoryLocalizationFeature` 在装配时做三件事：从 `XiHan:Localization` 读 `LocalizationOptions`；注册原语层（`primitives`）与内核（`kernel`）的语言文件目录，这两层不能读嵌入资源，由本特性代为装载；登记 `MessageLocalizer`，同一个实例既是 `IMessageLocalizer`（显式区域）也是 `IMessageResolver`（环境作用域）。

启动时，特性把本地化器以默认区域设为进程默认的 `MessageScope`，于是启动日志、异常与 `Error.Message` 都有默认语言的文案；每次调用的入口由内核按 Baggage `xihan.locale` 另装调用方区域的作用域。停止时不清除默认作用域，停机日志仍需要文案，下一个启动的应用会覆盖它。

`MessageLocalizer` 在构造时取得容器里全部 `MessageCatalog` 的快照，之后不再变化；所有查找都在内存里完成，没有 IO。

## 何时使用

默认就用它：文案随程序集发布，改译文就重新发布。需要在运行期修改译文、由运营人员维护翻译时，换成 [Localization.Database](./localization-database)，它装配后顶替本包。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Localization.InMemory" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

用组合包时已经装配，不需要再写；只用内核时显式加入：

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryLocalizationFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Localization:DefaultLocale` | `string` | `en-US` | 默认区域 |
| `XiHan:Localization:Locales` | `string`（逗号分隔） | 内置 10 种 | 启用的区域，按优先级排列 |

默认区域不在启用列表里或区域名不合法时，装配时抛出 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `InMemoryLocalizationFeature` | 特性入口（`IHostedFeature`） |
| `MessageLocalizer` | 实现 `IMessageLocalizer` 与 `IMessageResolver`；构造参数是目录集合与 `LocalizationOptions`，可在测试或工具里直接创建 |

## 使用示例

在测试或命令行工具里不经宿主直接创建本地化器：

```csharp
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Primitives;

var orders = new MessageCatalog("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
{
    ["en-US"] = new Dictionary<string, string> { ["orders.order.not_found"] = "Order '{id}' was not found." },
    ["zh-CN"] = new Dictionary<string, string> { ["orders.order.not_found"] = "订单 '{id}' 不存在。" },
});
var localizer = new MessageLocalizer([orders], new LocalizationOptions());

Console.WriteLine(localizer.Localize("orders.order.not_found", "zh-TW", MessageArguments.Of(("id", 1))));
// zh-TW 没有这条，同语言回退到 zh-CN：订单 '1' 不存在。

// 让 key.L() 在这段代码里按 ja-JP 解析；ja-JP 没有，经默认区域回退到英文
using (MessageScope.Use(localizer, "ja-JP"))
{
    Console.WriteLine("orders.order.not_found".L(("id", 2)));   // Order '2' was not found.
}
```

## 行为与限制

- 回退链：精确区域 → 启用列表里同语言的其他区域 → 默认区域 → `en-US`。在链上的每个区域，按注册顺序查各目录，先注册的目录优先。
- 格式化用命中区域的文化：`{amount:N2}` 在 `de-DE` 得到 `1.234,50`。
- 找不到键，或模板的占位符缺值时整条模板不采用：`Resolve` 无值，`Localize` 与 `key.L()` 返回 `MessageTemplate.Describe` 的描述（`kernel.feature.start_failed (id=…, reason=…)`）。
- 自由文本错误（`Error.Literal`）没有键，`Resolve(error, locale)` 无值，`Localize(error, locale)` 返回原文。
- 容器建好后再注册的目录不会被已创建的本地化器看到，目录要在 `Build()` 之前注册。
- 通过 `Localization.Conformance` 的 `MessageLocalizerConformance`。

## 相关

- [国际化](../guide/localization)
- [Localization.Abstractions](./localization-abstractions)
- [Localization.Database](./localization-database)
- [Profiles.Minimal](./profiles-minimal)
- 规范：RFC-0010《本地化》
