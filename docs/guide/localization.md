# 国际化

框架的多语言方案：文案放 JSON 文件、按请求头切文化、枚举与异常消息也一并本地化。这一章讲清资源怎么组织、文化怎么解析、以及哪些地方最容易踩空。

完整 API 与全部配置项见 [Localization 包](../packages/localization) 与 [Localization.Abstractions 包](../packages/localization-abstractions)。

## 整体流程

```text
请求进来
  └─ XiHanRequestCultureMiddleware 解析文化 → 设置 CurrentCulture / CurrentUICulture
       └─ 业务代码取 IStringLocalizer["Key"]
            └─ JSON 资源存储（虚拟文件系统加载的 *.json）
                 └─ miss → ResourceManager（.resx）兜底
                      └─ 仍 miss → 原样返回键名
```

三件事分别由三个东西负责，理解这个分工就不会找错地方：

| 职责 | 承担者 | 关键点 |
| --- | --- | --- |
| 决定「当前是哪种语言」 | `XiHanRequestCultureMiddleware` | 只在请求范围内生效，请求结束还原线程文化 |
| 决定「文案从哪来」 | `JsonLocalizationResourceStore` | 经虚拟文件系统读 JSON，支持热重载 |
| 决定「怎么取」 | `XiHanStringLocalizerFactory` / `XiHanJsonStringLocalizer` | 以 `Replace` 覆盖了微软默认工厂 |

## 安装与启用

```bash
dotnet add package XiHan.Framework.Localization
```

```csharp
[DependsOn(typeof(XiHanLocalizationModule))]
public class MyModule : XiHanModule { }
```

模块的 `ConfigureServices` 调用 `AddXiHanLocalization(config)`，从配置节 `XiHan:Localization` 绑定 `XiHanLocalizationOptions`，并把 `IStringLocalizerFactory` 替换为 `XiHanStringLocalizerFactory`（JSON 优先）、注册 `JsonLocalizationResourceStore` 与 `IEnumLocalizationService`。

::: tip 走 Web.Api 时中间件已经接好
`XiHanWebApiModule` 在 `OnApplicationInitialization` 里已经调用了 `app.UseXiHanRequestCulture()`（紧跟 TraceId、先于路由与 MVC）。

只有自己搭非 Web.Api 的宿主管线时，才需要手工调 `app.UseXiHanRequestCulture()`，且必须放在需要文化的中间件之前。
:::

## 资源文件怎么组织

资源根目录由 `ResourcesPath` 决定，默认虚拟路径 `/Localization`；存储会**递归**枚举其下所有 `*.json`。虚拟文件系统默认挂载应用基目录与当前工作目录，因此实际落点就是运行目录下的 `Localization/`。

工程里放物理文件时，记得让它随输出走：

```xml
<None Update="Localization\**\*.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

也可以把资源做成嵌入式资源，由虚拟文件系统的 `AddEmbedded<TAssembly>()` 提供，取值方式完全一致。

### 文件格式

推荐 `{Resource}.{culture}.json` 命名，并在文件内同时声明 `resource` 与 `culture`：

```json
{
  "resource": "Errors",
  "culture": "zh-CN",
  "texts": {
    "Auth.UserNotFound": "用户不存在。",
    "Tenant.CodeAlreadyExists": "租户编码已存在。",
    "CodeGeneration.TableAlreadyConfigured": "数据库表“{0}”已配置，请勿重复导入。"
  }
}
```

- 文本节点从 `texts` 读取，没有 `texts` 时读 `resources`，再没有就读根对象。
- 嵌套对象会被**扁平化**为 `a.b.c`；数组按原始文本存，数字与布尔转成字符串。
- 键的比较大小写不敏感。
- 同一「资源 + 文化」可以由多个文件贡献，按路径顺序合并，后者覆盖前者。

### 资源名与文化的解析优先级

| 情形 | 资源名取自 | 文化取自 |
| --- | --- | --- |
| JSON 同时有 `resource` 和 `culture` | JSON 的 `resource` | JSON 的 `culture` |
| JSON 无 `culture`，文件名形如 `Errors.zh-CN.json` | 文件名的 `Errors` | 文件名的 `zh-CN` |
| 文件名就是纯文化码，如 `zh-CN.json` | 所在目录名 | 文件名 |
| 都识别不出 | 所在目录名 | `DefaultCulture` |

::: warning 别只声明 resource 不声明 culture
JSON 里写了 `resource` 但没写 `culture` 时，会继续按文件名解析文化；一旦文件名带文化后缀，**文件名前缀会覆盖 JSON 里的 `resource`**。两处保持一致最省心。

另外，形如 `Errors.json`（无文化后缀，`culture` 与 `resource` 两个字段也都没写）会被归到默认文化，且资源名取的是**目录名**而不是 `Errors`——取值时必然 miss。
:::

## 取文案

注入 `IStringLocalizer<T>` 或 `IStringLocalizer`：

```csharp
using Microsoft.Extensions.Localization;

public class GreetingService(IStringLocalizer<GreetingService> localizer)
{
    // 资源名 = 类型短名 GreetingService
    public string Hello() => localizer["HelloWorld"];

    // 带格式化参数，按当前文化 string.Format
    public string Welcome(string name) => localizer["Welcome", name];
}
```

也可以按资源名直接创建，适合与类型无关的公共资源（如 `Errors`、`ApiResponse`）：

```csharp
public class ErrorTextProvider(IStringLocalizerFactory factory)
{
    public string NotFound() => factory.Create("Errors", "Errors")["Auth.UserNotFound"];
}
```

| 创建方式 | 资源名取值规则 |
| --- | --- |
| `Create(Type)` / `IStringLocalizer<T>` | 类型短名（`Type.Name`） |
| `Create(baseName, location)` | `baseName` 按 `.` `/` `\` 拆分后的**最后一段** |

::: tip 纯 JSON 资源不需要 .resx
`Create(baseName, location)` 的 ResourceManager 兜底会尝试把 `location` 当程序集加载。像 `Errors` 这种没有 backing 程序集的纯 JSON 资源，工厂会把兜底降级为「无兜底」，取值仍走 JSON，不会因为加载程序集失败而崩。
:::

### 取值链与回退

一次取值按下面顺序找，命中即返回：

1. 按**文化回退链**逐个文化查；每个文化下先查指定资源，miss 再查 `DefaultResourceName`（默认 `Default`）。
2. JSON 全 miss → 回退 `IStringLocalizer`（ResourceManager 或空实现）。
3. 仍 miss → 返回 `LocalizedString(name, name, resourceNotFound: true)`，即页面上看到的是**键名本身**。

文化回退链的构成：

| 顺序 | 内容 | 受控开关 |
| --- | --- | --- |
| 1 | 当前文化（`CurrentUICulture` 或 `WithCulture` 指定的固定文化） | — |
| 2 | 逐级父文化（`zh-CN` → `zh`） | `FallbackToParentCultures` |
| 3 | `DefaultCulture` | `FallbackToDefaultCulture` |

## 请求文化与 X-Language

```text
X-Language 头（头名可配） ──在受支持文化内?──▶ 用它
        └─否─▶ Accept-Language（按 q 权重降序逐个试） ──命中?──▶ 用它
                        └─否─▶ DefaultCulture
```

- 头名由 `CultureHeaderName` 配置，默认 `X-Language`。
- `SupportedCultures` 非空时，只接受**列表内**的文化（大小写不敏感）；列表为空表示不限制，只要求是合法文化码。
- 解析结果写入 `HttpContext.Items`，键为常量 `XiHanRequestCultureMiddleware.CultureItemKey`（`"__XiHanCulture"`），方便日志与请求上下文取用。
- 中间件在 `finally` 中还原线程的 `CurrentCulture` / `CurrentUICulture`，避免线程池复用时残留请求级文化。

::: warning 白名单是精确匹配，不做父文化归并
`SupportedCultures` 配 `["zh-CN", "en-US"]` 时，客户端发 `X-Language: en` **不会**命中 `en-US`——它会继续往下走 `Accept-Language`，最终多半落到默认文化。

要么让前端发完整文化码，要么把 `en` 也加进白名单。
:::

::: tip 后台任务没有请求文化
中间件只覆盖请求管线。定时任务、队列消费者等后台线程用的是进程默认文化，需要指定语言时显式创建带固定文化的本地化器（`XiHanJsonStringLocalizer.WithCulture(culture)`），或在枚举查询里传 `CultureName`。
:::

## 枚举本地化

枚举展示文案的单一事实源是本地化资源，默认资源名由 `EnumResourceName` 决定（默认 `Enums`）：

```json
{
  "resource": "Enums",
  "culture": "en-US",
  "texts": {
    "EnableStatus.Disabled": "Disabled",
    "EnableStatus.Enabled": "Enabled",
    "UserGender.Unknown": "Unknown"
  }
}
```

通过 `IEnumLocalizationService` 取：

```csharp
public class EnumMetadataService(IEnumLocalizationService enumLocalization)
{
    // 按类型取，未指定文化时用当前 UI 文化
    public LocalizedEnumDefinition ByType()
        => enumLocalization.Get(typeof(EnableStatus));

    // 按类型名取（短名或完整名皆可）
    public LocalizedEnumDefinition ByName()
        => enumLocalization.Get("EnableStatus", new EnumLocalizationQuery
        {
            CultureName = "en-US",
            IncludeHidden = false,
            Ordered = true
        });

    // 批量与安全版本
    public IReadOnlyDictionary<string, LocalizedEnumDefinition> Many()
        => enumLocalization.GetMany(["EnableStatus", "UserGender"]);
}
```

返回的 `LocalizedEnumDefinition.Items` 每项都带 `Name`、`Value`、`Label`、`Description`、`Theme`、`Icon`、`Order`、`Hidden`、`Disabled`、`Extra`，前端可以直接渲染下拉与状态标签。

### 键的候选顺序

对每个枚举项，按下表**依次**尝试，第一个命中的即为 `Label`；全部 miss 则回退到该项的 `Description`（来自 `[EnumDisplay]` / `[Description]` 等特性）。

| 顺序 | 候选键 | 来源 |
| --- | --- | --- |
| 1 | 字段显式指定的键 | `[EnumLocalizationKey("...")]` 或 `[EnumDisplay(LocalizationKey = "...")]` |
| 2 | `{前缀}.{枚举名}.{字段名}` | 前缀取 `[EnumLocalizationResource(KeyPrefix = "...")]`，为空时取 `EnumLocalizationKeyPrefix`；两者都为空则跳过本行 |
| 3 | `{枚举名}.{字段名}` | 约定（**推荐用这一种**） |
| 4 | `{枚举名}_{字段名}` | 约定 |
| 5 | `{字段名}` | 约定 |

资源名的选取顺序：字段上的 `[EnumLocalizationResource]` → 枚举类型上的 `[EnumLocalizationResource]` → `EnumResourceName`；解析结果为空时兜底 `DefaultResourceName`。

::: warning 短名冲突会解析失败
首次按名称取值时才扫描已加载程序集建立枚举索引（`Get(Type)` 不走该索引）。若两个不同命名空间存在**同名枚举**，该短名会被判为二义并从短名索引中移除——此时 `Get("XxxStatus")` 抛 `KeyNotFoundException`，必须改用完整名或 `Get(Type)`。

用 `TryGet` 可以拿到 `false` 而不是异常。
:::

## 可本地化异常

面向用户的错误消息不要硬编码中文。抛异常时传一个 `ResourceLocalizableString`，同时给一句回退文案：

```csharp
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Localization.Abstractions;

// 无参
throw new UserFriendlyException(
    new ResourceLocalizableString("Errors", "Auth.UserNotFound"),
    "用户不存在。");

// 带格式化参数（对应 JSON 里的 {0}）
throw new UserFriendlyException(
    new ResourceLocalizableString("Errors", "CodeGeneration.TableAlreadyConfigured", tableName),
    $"数据库表“{tableName}”已配置，请勿重复导入。");
```

机制：

- `BusinessException.LocalizableMessage` 以 `object` 弱类型承载可本地化消息，`UserFriendlyException` 的第二个构造函数负责写入。
- 真正解析发生在 `XiHanApiResponseResultFilter`：识别出 `ILocalizableString` 后按**当前请求文化**解析，缺键时回退到异常的 `Message`（也就是你传的那句回退文案）。
- 未携带可本地化消息的 500 响应，会按资源 `ApiResponse`、键 `ServerError` 本地化通用提示；正常响应的默认消息按业务码名（如 `Success`、`BadRequest`）在同一资源里查。

::: tip 回退文案不是可选项
第二个参数同时充当「本地化键缺失时的展示文案」和「日志里的异常消息」。省掉它，键一旦写错，用户看到的就是空消息。
:::

其它可本地化构件：

| 类型 | 用途 |
| --- | --- |
| `FixedLocalizableString` | 固定文本，直接返回原值；`"文本".ToFixedLocalizableString()` 可快速构造 |
| `ResourceLocalizableString(Type, name, args)` | 按资源类型解析，等价于 `IStringLocalizer<T>` |
| `IHasNameWithLocalizableDisplayName` | 「有标识名 + 可本地化显示名」的对象契约，配合 `GetLocalizedDisplayName(factory)` 使用 |
| `LocalizeOrFallback(factory, fallback)` | 解析并在缺键时回退，避免到处判 `ResourceNotFound` |

## 动态热重载

`EnableDynamicJsonReload` 默认 `true`。资源存储同时监听两个来源：

| 触发源 | 条件 |
| --- | --- |
| 虚拟文件系统 `OnFileChanged` | 变更文件在 `ResourcesPath` 之下且扩展名为 `.json` |
| `IOptionsMonitor<XiHanLocalizationOptions>` 变更 | 重算资源路径、重挂监听 |

命中后重置整份缓存并递增 `JsonLocalizationResourceStore.Version`，下次取值时按需重新加载。`Version` 可以作为上层缓存（例如前端字典缓存）的失效依据。

::: tip 改文案不用重启，改配置也不用
开发期直接改运行目录下的 JSON 即可即时生效。生产若不希望文件监听开销，把 `EnableDynamicJsonReload` 置 `false`。
:::

## 配置

配置节 `XiHan:Localization`，常用几项：

```json
{
  "XiHan": {
    "Localization": {
      "ResourcesPath": "/Localization",
      "DefaultCulture": "zh-CN",
      "SupportedCultures": ["zh-CN", "en-US"],
      "CultureHeaderName": "X-Language",
      "DefaultResourceName": "Default",
      "EnumResourceName": "Enums",
      "EnableDynamicJsonReload": true
    }
  }
}
```

| 键 | 默认值 | 什么时候需要改 |
| --- | --- | --- |
| `ResourcesPath` | `/Localization` | 资源不放默认目录时 |
| `DefaultCulture` | `zh-CN` | 主语言不是中文时 |
| `SupportedCultures` | `["zh-CN", "en-US"]` | 新增语言时**必须**同步加，否则请求头切不过去 |
| `CultureHeaderName` | `X-Language` | 前端约定了别的头名时 |
| `DefaultResourceName` | `Default` | 想换通用文案的兜底资源名时 |
| `EnumResourceName` | `Enums` | 枚举文案不放 `Enums` 资源时 |
| `EnableDynamicJsonReload` | `true` | 生产想关掉文件监听时 |

完整配置表见 [Localization 包](../packages/localization)。

## 常见问题

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 页面显示的是键名本身 | JSON 与 ResourceManager 都没命中该键 | 核对资源名、文化码、扁平化后的完整键 |
| 文件放了但完全不生效 | 未随输出拷贝，或不在 `ResourcesPath` 之下 | 加 `CopyToOutputDirectory`，确认运行目录下确有该文件 |
| 资源名对不上 | 文件名无文化后缀，资源名回落成了**目录名** | 用 `{Resource}.{culture}.json`，或在 JSON 内声明 `resource` + `culture` |
| 发 `X-Language: en` 不切换 | 白名单精确匹配，`en` ≠ `en-US` | 发完整文化码，或把 `en` 加入 `SupportedCultures` |
| 切了语言但枚举标签还是中文 | 枚举键写法与候选顺序不符 | 统一用 `{枚举名}.{字段名}`，并确认资源名是 `EnumResourceName` |
| `Get("XxxStatus")` 抛 `KeyNotFoundException` | 不同命名空间存在同名枚举，短名二义 | 用完整名或 `Get(Type)`；不确定时用 `TryGet` |
| 异常消息没被翻译 | 抛的是普通异常，或没传 `ILocalizableString` | 用 `UserFriendlyException(new ResourceLocalizableString(...), 回退文案)` |
| 后台任务的文案永远是默认语言 | 后台线程不经过请求文化中间件 | 显式指定文化（`WithCulture` 或 `EnumLocalizationQuery.CultureName`） |
| 自定义的 `IStringLocalizerFactory` 不生效 | 本模块用 `Replace` 覆盖了该服务 | 在本模块之后再次 `Replace` |

## 下一步

- [配置与选项](./configuration) — `XiHan:Localization` 所在的配置体系与选项绑定方式
- [Web 应用开发](./web) — 请求管线顺序，中间件在哪一段接入
- [常见问题](./faq) — 框架层通用排查清单
- [Localization 包](../packages/localization) — 完整 API 清单与全部配置项
- [Localization.Abstractions 包](../packages/localization-abstractions) — `ILocalizableString` 等契约
- [VirtualFileSystem 包](../packages/virtual-file-system) — JSON 资源的加载与变更监听
