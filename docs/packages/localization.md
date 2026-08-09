# XiHan.Framework.Localization

> 国际化实现：JSON 优先（ResourceManager 回退）的字符串本地化 + 按请求头动态切换文化的中间件 + 枚举字段级本地化服务。

- **NuGet**：`XiHan.Framework.Localization`
- **模块类**：`XiHanLocalizationModule`
- **所在层**：基础设施层
- **关键依赖**：`Microsoft.Extensions.Localization` + `Microsoft.AspNetCore.App`（中间件）+ 框架内部依赖（Localization.Abstractions / VirtualFileSystem / Threading / Settings / Utils）

## 概述

本包是 [XiHan.Framework.Localization.Abstractions](./localization-abstractions) 的具体实现。取值策略为 **JSON 优先、ResourceManager（.resx）回退**：`XiHanStringLocalizerFactory` 产出的 `IStringLocalizer` 先查 JSON 资源存储，miss 才回退到微软标准的 `ResourceManagerStringLocalizerFactory`；当资源没有 backing 程序集（如纯 JSON 的 `ApiResponse` / `Errors`）时以 `NullStringLocalizer` 兜底，避免因加载程序集失败而整体崩溃。JSON 资源经**虚拟文件系统**加载，支持磁盘与嵌入式资源，并可监听文件变化**动态热重载**。文化解析由 `XiHanRequestCultureMiddleware` 在请求管线中完成：按 `X-Language` 请求头 > `Accept-Language` > 默认文化的优先级设置当前请求的 `CultureInfo`。枚举本地化由 `EnumLocalizationService` 提供，把枚举类型解析为带 `Label`、`Description`、`Icon` 等元数据的展示定义。

## 何时使用

- 应用需要多语言支持，用 JSON 文件维护各语言文案（推荐），或复用既有 .resx 资源（回退）。
- 需要按请求头（默认 `X-Language`）或标准 `Accept-Language` 动态切换语言，并支持父文化 / 默认文化回退。
- 需要把枚举值本地化为带标签、图标、主题、排序等元数据的展示项（供前端下拉、标签渲染）。
- 需要在开发期改 JSON 文案即时生效（动态热重载）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Localization
```

```csharp
[DependsOn(typeof(XiHanLocalizationModule))]
public class MyModule : XiHanModule { }
```

模块 `ConfigureServices` 调用 `AddXiHanLocalization(config)`，从配置节 `XiHan:Localization` 绑定 `XiHanLocalizationOptions`，并完成如下注册（见 `XiHanLocalizationServiceCollectionExtensions`）：

- `services.AddLocalization()`（微软基础设施）+ `AddOptions<XiHanLocalizationOptions>()`；
- `ResourceManagerStringLocalizerFactory` 作为**兜底**工厂（`TryAddSingleton`）；
- `JsonLocalizationResourceStore`（`TryAddSingleton`，JSON 资源存储）；
- `IEnumLocalizationService -> EnumLocalizationService`（`TryAddSingleton`）；
- 用 **`Replace`** 把 `IStringLocalizerFactory` 替换为 `XiHanStringLocalizerFactory`（JSON 优先）。

模块本身**不自动接入中间件**——文化切换需要在 Web 宿主的请求管线里显式调用 `app.UseXiHanRequestCulture()`。

> 依赖模块：`XiHanLocalizationModule` 上标注 `[DependsOn]` 引入 `XiHanLocalizationAbstractionsModule`、`XiHanVirtualFileSystemModule`、`XiHanThreadingModule`、`XiHanSettingsModule`。

## 工作原理

**取值链（`XiHanJsonStringLocalizer`）**：以 `CurrentUICulture`（或固定文化）为准 → 查 `JsonLocalizationResourceStore` → miss 则查回退 `IStringLocalizer`（ResourceManager 或 `NullStringLocalizer`）→ 仍 miss 返回 `LocalizedString(name, name, resourceNotFound: true)`。

**JSON 资源解析（`JsonLocalizationResourceStore`）**：

- 从 `ResourcesPath`（默认 `/Localization`）**递归**枚举 `*.json`，用虚拟文件系统读取；
- 资源名与文化码解析顺序：JSON 内的 `resource`/`culture` 字段 > 文件名 `{Resource}.{culture}.json` > 目录名 / 纯文化码文件名；
- 文本节点从 `texts` 或 `resources` 子对象读取（没有则取根），并**扁平化嵌套键**为 `a.b.c` 形式；数组按原文本、数字/布尔转字符串；
- 缓存结构为 `资源名 -> 文化码 -> 键 -> 文本` 三层字典；
- 取值时构建**文化回退链**：当前文化 → 父文化（`FallbackToParentCultures`）→ 默认文化（`FallbackToDefaultCulture`），并在指定资源 miss 时回落到 `DefaultResourceName`。

**动态热重载**：监听虚拟文件系统的 `OnFileChanged` 与 `IOptionsMonitor` 变更；当 `EnableDynamicJsonReload=true` 且变化文件在 `ResourcesPath` 下且为 `.json` 时重置缓存并递增 `Version`（可供上层缓存失效）。

**请求文化解析（`XiHanRequestCultureMiddleware`）**：

```text
X-Language 头(可配) ──命中受支持文化?──▶ 用它
        └─miss─▶ Accept-Language(按 q 权重排序) ──命中?──▶ 用它
                        └─miss─▶ 默认文化(DefaultCulture)
```

`SupportedCultures` 非空时仅接受列表内文化（大小写不敏感）；为空则不限制、只要求是合法文化。中间件在请求结束的 `finally` 中**还原线程文化**，避免线程池复用残留；解析后的文化名写入 `HttpContext.Items["__XiHanCulture"]`（常量 `XiHanRequestCultureMiddleware.CultureItemKey`）。

## 核心能力

- **JSON 优先、ResourceManager 回退**：`XiHanStringLocalizerFactory` 按类型或（baseName, location）创建 `IStringLocalizer`，无 backing 程序集时容错为 `NullStringLocalizer`（仅 JSON）。
- **基于虚拟文件系统的 JSON 存储**：磁盘 + 嵌入式资源统一加载，键扁平化，多层文化回退。
- **动态热重载**：文件或选项变化即失效缓存，`Version` 递增。
- **请求文化中间件**：`X-Language` > `Accept-Language` > 默认文化，支持受支持文化白名单与父/默认文化回退。
- **枚举本地化**：`EnumLocalizationService` 按类型/类型名解析枚举，产出 `LocalizedEnumDefinition`（含每项 `Label`、`Icon`、`Theme`、`Order`、`Hidden`、`Disabled` 等）。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanLocalizationModule` | 模块入口，`ConfigureServices` 调 `AddXiHanLocalization` |
| `XiHanStringLocalizerFactory` | `IStringLocalizerFactory` 实现（JSON 优先，`Replace` 覆盖默认）。方法 `Create(Type)`、`Create(string baseName, string location)` |
| `XiHanJsonStringLocalizer` | `IStringLocalizer` 实现（JSON + 回退）。索引器 `this[name]`、`this[name, args]`；`GetAllStrings(bool)`；`WithCulture(CultureInfo)` |
| `JsonLocalizationResourceStore` | JSON 资源存储/加载器。`bool TryGetString(string resourceName, CultureInfo culture, string name, out string value)`、`IReadOnlyList<LocalizedString> GetAllStrings(...)`、`long Version` |
| `EnumLocalizationService` | `IEnumLocalizationService` 实现，反射构建枚举短名/完整名索引 |
| `XiHanRequestCultureMiddleware` | 请求文化解析中间件，常量 `CultureItemKey = "__XiHanCulture"` |
| `XiHanLocalizationOptions` | 配置选项（配置节 `XiHan:Localization`） |
| `XiHanLocalizationServiceCollectionExtensions` | DI 入口 `AddXiHanLocalization(IServiceCollection, IConfiguration?)` |
| `XiHanRequestCultureApplicationBuilderExtensions` | 中间件入口 `UseXiHanRequestCulture(IApplicationBuilder)` |

## 配置

配置节：`XiHan:Localization`（`XiHanLocalizationOptions.SectionName`）。

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `ResourcesPath` | `string` | `/Localization` | 本地化资源根目录（虚拟路径） |
| `EnableDynamicJsonReload` | `bool` | `true` | 是否启用 JSON 动态热重载 |
| `DefaultResourceName` | `string` | `Default` | 默认资源名（指定资源 miss 时回落） |
| `EnumResourceName` | `string` | `Enums` | 枚举本地化默认资源名 |
| `EnumLocalizationKeyPrefix` | `string` | `""` | 枚举本地化默认键前缀（空表示不加前缀） |
| `DefaultCulture` | `string` | `zh-CN` | 默认文化 |
| `SupportedCultures` | `IList<string>` | `["zh-CN", "en-US"]` | 受支持文化白名单；为空表示不限制 |
| `CultureHeaderName` | `string` | `X-Language` | 请求文化解析使用的请求头名 |
| `FallbackToParentCultures` | `bool` | `true` | 是否回退到父文化 |
| `FallbackToDefaultCulture` | `bool` | `true` | 是否回退到默认文化 |

```json
{
  "XiHan": {
    "Localization": {
      "ResourcesPath": "/Localization",
      "EnableDynamicJsonReload": true,
      "DefaultResourceName": "Default",
      "EnumResourceName": "Enums",
      "DefaultCulture": "zh-CN",
      "SupportedCultures": ["zh-CN", "en-US"],
      "CultureHeaderName": "X-Language",
      "FallbackToParentCultures": true,
      "FallbackToDefaultCulture": true
    }
  }
}
```

## 使用示例

### 1. 注入 `IStringLocalizer` 取值

```csharp
using Microsoft.Extensions.Localization;

public class GreetingService(IStringLocalizer<GreetingService> localizer)
{
    // 先查 JSON（资源名取类型短名 GreetingService），miss 回退 ResourceManager
    public string Hello() => localizer["HelloWorld"];

    // 带格式化参数
    public string Welcome(string name) => localizer["Welcome", name];
}
```

对应 JSON（`/Localization/GreetingService.en-US.json` 或用 `resource`/`culture` 字段声明）：

```json
{
  "resource": "GreetingService",
  "culture": "en-US",
  "texts": {
    "HelloWorld": "Hello World",
    "Welcome": "Welcome, {0}!"
  }
}
```

### 2. 在请求管线接入文化中间件

```csharp
// 建议放在管线早期（路由/MVC 之前，紧跟 TraceId 等），使后续管线在请求文化下执行
app.UseXiHanRequestCulture();
```

客户端发 `X-Language: en-US` 请求头即可切到英文（须在 `SupportedCultures` 内）。

### 3. 枚举本地化

```csharp
public class EnumApi(IEnumLocalizationService enumLocalization)
{
    public LocalizedEnumDefinition GetOrderStatus()
        // 按类型名解析（短名或完整名皆可），未指定文化时用当前 UI 文化
        => enumLocalization.Get("OrderStatus", new EnumLocalizationQuery { Ordered = true });
}
```

## 扩展点 / 自定义

- **替换字符串本地化工厂**：DI 用 `Replace` 覆盖了 `IStringLocalizerFactory`；如需自定义可在本模块之后再次 `Replace`。
- **纯 JSON 资源（无 .resx）**：直接放 JSON 即可，工厂对无 backing 程序集的资源会容错为 `NullStringLocalizer`，取值仍走 JSON。
- **切换资源目录 / 关闭热重载**：改 `ResourcesPath` / `EnableDynamicJsonReload`（`IOptionsMonitor` 感知变更并重建缓存）。
- **自定义文化来源头**：改 `CultureHeaderName`（默认 `X-Language`）。

## 注意事项与最佳实践

- **中间件不自动注册**：仅引模块不接管线不会切换文化，务必显式 `UseXiHanRequestCulture()`，且放在需要文化的中间件之前。
- **线程文化会被还原**：中间件在请求结束还原 `CurrentCulture`/`CurrentUICulture`，不要依赖请求后线程仍保持请求文化。
- **JSON 键会被扁平化**：嵌套对象展开为 `a.b.c`，取值键需与扁平化结果一致。
- **枚举短名冲突**：`EnumLocalizationService` 构建全域枚举索引时，同名短名（不同命名空间）会被判为二义而从短名索引移除——此时须用完整名解析。
- **默认资源回落**：指定资源 miss 会回落 `DefaultResourceName`；把通用文案放默认资源可减少重复。

## 依赖模块

- 内部依赖：[XiHan.Framework.Localization.Abstractions](./localization-abstractions)、[XiHan.Framework.VirtualFileSystem](./virtual-file-system)、[XiHan.Framework.Threading](./threading)、[XiHan.Framework.Settings](./settings)、[XiHan.Framework.Utils](./utils)。
- 第三方核心：`Microsoft.Extensions.Localization`；中间件依赖 `Microsoft.AspNetCore.App`。

## 相关模块

- [XiHan.Framework.Localization.Abstractions](./localization-abstractions) — 本包实现的接口契约层。
- [XiHan.Framework.VirtualFileSystem](./virtual-file-system) — 提供 JSON 资源的统一加载与变更监听。
