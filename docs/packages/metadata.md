# XiHan.Framework.Metadata

> 框架"身份信息"的单一事实源：名称、版本、作者、组织、许可证、支持平台等静态元数据。

- **NuGet**：`XiHan.Framework.Metadata`
- **模块类**：—（无模块类，直接引用）
- **所在层**：元数据层
- **关键依赖**：仅 .NET 原生（`System.Reflection`），无框架内部依赖

## 概述

XiHan.Framework.Metadata 把整个框架的元信息集中到一个静态类 `XiHanMetadata` 中：框架名称、显示名、版权、作者与邮箱、组织与仓库/文档地址、许可证、关键词、支持的 .NET 版本与平台。这些身份信息大多是编译期常量，同时也提供从运行时程序集读取的版本与入口程序集信息。它是框架最底层的包之一，无任何内部依赖，任何模块都能安全引用它来获得统一的框架元信息，避免在各处硬编码名称与版本字符串。

## 何时使用

- 启动横幅、日志、诊断信息需要打印统一的框架名称与版本。
- 关于页 / 健康检查 / API 元信息需要展示作者、组织、仓库地址、许可证等。
- 运行时需要读取入口程序集（宿主应用）的名称与版本。
- 需要框架 ASCII Logo 或一键格式化的框架信息摘要。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Metadata
```

纯元数据库，**无模块类**，无需 `[DependsOn]`。直接引用后读取 `XiHanMetadata` 上的静态成员即可。

## 核心能力

- 集中维护框架身份常量：`Name`、`DisplayName`、`Copyright`、`Author`、`Organization`、`OrganizationUrl`、`RepositoryUrl`、`DocumentationUrl`、`License`、`LicenseUrl`、`Description`。
- 数组型元信息：`Keywords`、`SupportedFrameworks`（当前 `net10.0`）、`SupportedPlatforms`（`Windows` / `Linux` / `MacOS`）。
- 版本信息（从 `XiHanMetadata` 所在程序集读取）：`Version`、`FullVersion`、`MajorVersion`、`MinorVersion`、`PatchVersion`。
- 运行时入口信息（从入口程序集读取）：`EntryAssemblyName`、`EntryAssemblyVersion`。
- 展示用文本：`Logo`（ASCII 艺术字）、`SendWord`（框架寄语）。
- 格式化输出：`GetSummary()`（名称/版本/描述/寄语/入口程序集）与 `GetDetails()`（在摘要基础上追加作者、组织、仓库、文档、许可证）。

## 主要 API / 类型

只有一个静态类 `XiHanMetadata`，成员如下。

### 常量与元信息（静态字段）

| 成员 | 类型 | 值 / 说明 |
| --- | --- | --- |
| `Name` | `string` | `"XiHan.Framework"` |
| `DisplayName` | `string` | `"曦寒框架"` |
| `Copyright` | `string` | `"Copyright (c) 2021-Present XiHanFun and contributors."` |
| `Author` | `string` | `"XiHanFun"` |
| `Organization` | `string` | `"XiHanFun"` |
| `OrganizationUrl` | `string` | `https://github.com/XiHanFun` |
| `RepositoryUrl` | `string` | `https://github.com/XiHanFun/XiHan.Framework` |
| `DocumentationUrl` | `string` | `https://docs.xihanfun.com` |
| `License` | `string` | `"MIT"` |
| `LicenseUrl` | `string` | 指向仓库 `LICENSE` |
| `Description` | `string` | `"快速、轻量、高效、用心的开发框架和组件库。基于 .NET 10 构建。"` |
| `Keywords` | `string[]` | `dotnet`、`aspnetcore`、`csharp`、`web`、`webapp`、`xihan`、`framework`、`zhaifanhua`、`xihanfun`、`modular`、`extensible`（共 11 项） |
| `SupportedFrameworks` | `string[]` | `["net10.0"]` |
| `SupportedPlatforms` | `string[]` | `["Windows", "Linux", "MacOS"]` |

### 版本与运行时（只读属性）

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Version` | `string` | 框架程序集版本字符串，读取失败回退 `0.0.0` |
| `FullVersion` | `Version` | 框架完整 `Version` 对象 |
| `MajorVersion` / `MinorVersion` / `PatchVersion` | `int` | 主 / 次 / 修订版本号 |
| `EntryAssemblyName` | `string` | 入口（宿主）程序集名称，无入口时为空串 |
| `EntryAssemblyVersion` | `string` | 入口程序集版本字符串 |
| `Logo` | `string` | 框架 ASCII Logo |
| `SendWord` | `string` | 框架寄语 |

### 摘要方法

| 方法 | 签名 | 说明 |
| --- | --- | --- |
| `GetSummary()` | `static string GetSummary()` | 名称、显示名、版本、描述、寄语、入口程序集名与版本 |
| `GetDetails()` | `static string GetDetails()` | 在摘要基础上追加作者、组织、仓库、文档、许可证 |

> 版本相关成员依赖程序集的 `AssemblyVersion` 元数据；`Version`/`MajorVersion` 等读取的是 `XiHanMetadata` 所在的 Metadata 程序集，而 `EntryAssemblyName`/`EntryAssemblyVersion` 读取的是运行时入口程序集（宿主应用），两者不同。

## 使用示例

```csharp
using XiHan.Framework.Metadata;

// 启动横幅：打印 Logo + 摘要
Console.WriteLine(XiHanMetadata.Logo);
Console.WriteLine(XiHanMetadata.GetSummary());

// 关于页 / 诊断：完整详细信息
var details = XiHanMetadata.GetDetails();

// 单独读取名称与版本
var name = XiHanMetadata.Name;          // "XiHan.Framework"
var version = XiHanMetadata.Version;     // 如 "3.5.0.0"（随框架统一版本号 version.props 更新）
var major = XiHanMetadata.MajorVersion;  // 如 3

// 读取宿主应用（入口程序集）信息
var host = XiHanMetadata.EntryAssemblyName;
var hostVer = XiHanMetadata.EntryAssemblyVersion;
```

## 注意事项与最佳实践

- 身份常量以静态字段暴露，运行时可被赋值覆盖；正常使用应视为只读，不要修改，以免污染全局元信息。
- `Version` 系列反映的是**框架程序集**版本；要展示宿主应用版本请用 `EntryAssemblyName` / `EntryAssemblyVersion`。
- 无入口程序集（如某些测试宿主）时，入口相关属性返回空串，展示前请做好空值兜底。
- 需要统一框架名称/版本字符串时，一律读取本包成员，不要在业务代码里硬编码。

## 依赖模块

无框架内部依赖；仅使用 BCL 的 `System.Reflection` 读取程序集版本与入口程序集信息。

## 相关模块

- [XiHan.Framework.Core](./core) — 核心库，可读取 Metadata 提供的框架元信息。
- [XiHan.Framework.Utils](./utils) — 通用工具库，同为最底层基础包。
