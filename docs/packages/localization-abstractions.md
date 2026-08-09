# XiHan.Framework.Localization.Abstractions

> 国际化抽象层：可本地化字符串与枚举本地化的接口契约，实现由 XiHan.Framework.Localization 提供。

- **NuGet**：`XiHan.Framework.Localization.Abstractions`
- **模块类**：`XiHanLocalizationAbstractionsModule`
- **所在层**：基础设施层
- **关键依赖**：`Microsoft.Extensions.Localization.Abstractions`（复用其 `IStringLocalizerFactory`、`IStringLocalizer`、`LocalizedString` 标准抽象）+ 框架内部依赖 `XiHan.Framework.Core`

## 概述

本包是国际化能力的**薄抽象层**：只定义接口和数据模型（可本地化字符串、枚举本地化查询与结果），不含任何资源加载、文化解析、JSON 解析等具体逻辑。它建立在微软标准的 `Microsoft.Extensions.Localization.Abstractions` 之上——`ILocalizableString.Localize` 直接接受微软的 `IStringLocalizerFactory` 并返回 `LocalizedString`，因此与 ASP.NET Core 原生本地化基础设施天然互通。真正的实现（JSON 优先加载、请求文化中间件、枚举本地化服务）由 [XiHan.Framework.Localization](./localization) 提供。上层模块（如 [XiHan.Framework.ObjectMapping](./objectmapping)）只依赖这个薄抽象包，即可在契约层描述"可本地化文本"而不牵连完整实现。

## 何时使用

- 你的模块只想引用本地化的**接口契约**，而不想直接依赖具体实现包。
- 需要用一个可能带多语言的文本占位，用 `ILocalizableString` 表达（延迟到有 `IStringLocalizerFactory` 时再解析）。
- 定义"标识名 + 可本地化显示名"分离的对象（`IHasNameWithLocalizableDisplayName`），例如权限、菜单、功能项。
- 需要枚举字段级本地化的**查询契约**（`IEnumLocalizationService`），而实现放在下游。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Localization.Abstractions
```

```csharp
[DependsOn(typeof(XiHanLocalizationAbstractionsModule))]
public class MyModule : XiHanModule { }
```

`XiHanLocalizationAbstractionsModule.ConfigureServices` **不注册任何具体服务**（仅作为纯抽象声明入口存在）。要实际取值，必须引入实现包 [XiHan.Framework.Localization](./localization)——它提供 `IEnumLocalizationService` 与 `IStringLocalizerFactory` 的真实实现。

## 核心能力

- **可本地化字符串抽象**：`ILocalizableString` 定义单一契约 `Localize(IStringLocalizerFactory) -> LocalizedString`，把"如何取值"延迟到运行期。
- **两种内置实现**：`ResourceLocalizableString`（按资源类型/资源名 + 键解析，支持格式化参数）与 `FixedLocalizableString`（固定文本，直接原样返回，`ResourceNotFound=false`）。
- **名称与显示名分离**：`IHasNameWithLocalizableDisplayName` 约定不本地化的标识名 `Name` 与可本地化的显示名 `DisplayName`。
- **取值/回退扩展**：`LocalizableStringExtensions` 提供缺失资源时的降级取值与显示名便捷方法。
- **枚举本地化契约与模型**：`IEnumLocalizationService` 及 `EnumLocalizationQuery` / `LocalizedEnumItem` / `LocalizedEnumDefinition` 描述枚举字段级本地化的查询参数与结果结构。

## 主要 API / 类型

### 可本地化字符串

| 类型 | 说明 |
| --- | --- |
| `ILocalizableString` | 核心契约。方法 `LocalizedString Localize(IStringLocalizerFactory stringLocalizerFactory)` |
| `ResourceLocalizableString` | 按资源解析。两种构造：`(Type resourceType, string name, params object[] arguments)` 与 `(string resourceName, string name, params object[] arguments)`；属性 `ResourceType`、`ResourceName`、`Name`、`Arguments` |
| `FixedLocalizableString` | 固定文本实现。构造 `(string value)`，属性 `Value`；`Localize` 返回 `new LocalizedString(Value, Value, resourceNotFound: false)` |
| `IHasNameWithLocalizableDisplayName` | 分离契约：`string Name { get; }`、`ILocalizableString? DisplayName { get; }` |

`LocalizableStringExtensions`（静态扩展）：

| 方法 | 签名与说明 |
| --- | --- |
| `LocalizeOrFallback` | `string LocalizeOrFallback(this ILocalizableString? localizableString, IStringLocalizerFactory factory, string? fallback = null)`。源为 `null` 返回 `fallback ?? string.Empty`；命中但 `ResourceNotFound` 且有 `fallback` 则用 `fallback`，否则返回本地化值 |
| `GetLocalizedDisplayName` | `string GetLocalizedDisplayName(this IHasNameWithLocalizableDisplayName source, IStringLocalizerFactory factory)`。优先本地化 `DisplayName`，缺失时回退到 `source.Name` |
| `ToFixedLocalizableString` | `ILocalizableString ToFixedLocalizableString(this string value)`。把普通字符串包装为固定文本本地化字符串 |

### 枚举本地化契约

`IEnumLocalizationService`（实现见 [localization](./localization) 的 `EnumLocalizationService`）：

| 方法 | 说明 |
| --- | --- |
| `LocalizedEnumDefinition Get(Type enumType, EnumLocalizationQuery? query = null)` | 按类型读取 |
| `LocalizedEnumDefinition Get(string enumTypeName, EnumLocalizationQuery? query = null)` | 按类型名读取（支持短名或完整名） |
| `IReadOnlyDictionary<string, LocalizedEnumDefinition> GetMany(IEnumerable<string> enumTypeNames, EnumLocalizationQuery? query = null)` | 批量读取，键为传入的类型名 |
| `bool TryGet(string enumTypeName, out LocalizedEnumDefinition? result, EnumLocalizationQuery? query = null)` | 容错读取，未找到返回 `false` |

枚举本地化数据模型（命名空间 `XiHan.Framework.Localization.Abstractions.Enums`）：

| 类型 | 关键字段 |
| --- | --- |
| `EnumLocalizationQuery` | `string? CultureName`（未指定用当前 UI 文化）、`bool IncludeHidden`、`bool Ordered = true` |
| `LocalizedEnumItem` | `Name`、`Value`（`object`）、`ValueText`、`Label`（优先本地化）、`Description`、`Theme?`、`Icon?`、`Order`、`Hidden`、`Disabled`、`ResourceName?`、`LocalizationKey?`、`Extra`（`IReadOnlyDictionary<string, object>?`） |
| `LocalizedEnumDefinition` | `EnumName`、`FullName`、`DisplayName`、`CultureName`、`IsFlags`、`UnderlyingTypeName`、`ResourceName?`、`Items`（`IReadOnlyList<LocalizedEnumItem>`） |

## 使用示例

用 `ILocalizableString` 描述一个"标识名 + 可本地化显示名"的对象，取值时再传入工厂：

```csharp
using XiHan.Framework.Localization.Abstractions;
using Microsoft.Extensions.Localization;

public sealed class MenuItem : IHasNameWithLocalizableDisplayName
{
    public string Name { get; } = "Dashboard";

    // 按资源名 + 键，延迟解析
    public ILocalizableString? DisplayName { get; }
        = new ResourceLocalizableString("Menu", "Menu.Dashboard");
}

// 有了 IStringLocalizerFactory（由实现包注入）后：
public string Render(MenuItem item, IStringLocalizerFactory factory)
    => item.GetLocalizedDisplayName(factory); // 缺失时回退到 item.Name
```

固定文本包装（无需资源文件）：

```csharp
ILocalizableString title = "订单中心".ToFixedLocalizableString();
```

## 依赖模块

- 内部依赖：[XiHan.Framework.Core](./core)。
- 第三方核心：`Microsoft.Extensions.Localization.Abstractions`。

## 相关模块

- [XiHan.Framework.Localization](./localization) — 本抽象层的具体实现包（JSON 资源加载、请求文化中间件、枚举本地化服务）。
- [XiHan.Framework.ObjectMapping](./objectmapping) — 依赖本抽象包以表达扩展属性的可本地化显示名。
