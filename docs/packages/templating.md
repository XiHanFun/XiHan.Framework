# XiHan.Framework.Templating

> 模板渲染：基于第三方 Scriban 的完整引擎 + 一个轻量字符串替换引擎，经引擎注册表 `ITemplateEngineRegistry` 按名称统一管理。

- **NuGet**：`XiHan.Framework.Templating`
- **模块类**：`XiHanTemplatingModule`
- **所在层**：基础设施层
- **关键依赖**：`Scriban`（7.2.5，模板引擎）+ 框架内部依赖（Core / Serialization / Utils）。

## 概述

XiHan.Framework.Templating 提供「模板 + 数据 = 文本」的渲染能力，并把多种引擎统一到注册表 `ITemplateEngineRegistry` 下按名称管理。框架内置两种引擎：

- **Scriban 引擎** `ScribanTemplateEngine`（注册名 `"Scriban"`，模板类型 `ITemplateEngine<Template>`）：转发到第三方 Scriban，支持过滤器、函数、复杂表达式等完整语法。
- **字符串替换引擎** `DefaultTemplateEngine`（注册名 `"String"`，模板类型 `ITemplateEngine<string>`）：自研的轻量占位替换 + 简单条件/循环，**不解析 Scriban 语法**。

适合代码生成、通知/邮件文案、报表等场景。

## 何时使用

- 需要把数据填进模板生成文本（代码文件、邮件正文、通知内容等）。
- 需要在同一套注册表下切换/共存多种模板引擎。
- 需要模板缓存、语法校验、从文件渲染、渲染落盘等配套能力。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Templating
```

```csharp
[DependsOn(typeof(XiHanTemplatingModule))]
public class MyModule : XiHanModule { }
```

模块 `[DependsOn(typeof(XiHanSerializationModule))]`。启用后：

- `ConfigureServices` 调 `AddXiHanTemplating()`，用 `TryAdd*` 注册注册表、上下文工厂/访问器、两种引擎、模板服务、继承/片段管理器、安全分析器/检查器，并配置 `TemplatingOptions`。
- `OnApplicationInitialization` 把 Scriban 引擎与 String 引擎注册进 `ITemplateEngineRegistry`，并分别设为 `Template` 类型与 `string` 类型的默认引擎。

## 工作原理：`ITemplateService` 默认走的不是 Scriban（重要）

这是最容易踩的坑。`TemplateService` 的字符串重载内部固定取的是 **`string` 模板类型的默认引擎**：

```csharp
var engine = _engineRegistry.GetDefaultEngine<string>()
             ?? throw new InvalidOperationException("没有找到可用的模板引擎");
return await engine.RenderAsync(templateSource, context);
```

而模块初始化时 `SetDefaultEngine<string>("String")`，所以 `string` 类型的默认引擎是 `DefaultTemplateEngine`（简单替换），**不是 Scriban**。

- 因此 `ITemplateService.RenderAsync(string, ...)` / `RenderFileAsync` / `ValidateTemplate` 全部走简单替换引擎。
- `DefaultTemplateEngine` 只支持三类占位语法（用正则/字符串替换实现，见下），**不认识 Scriban 的过滤器、函数、成员访问等**：
  - 变量占位：<code v-pre>{{变量名}}</code>
  - 条件：<code v-pre>{{if 条件}}...{{else}}...{{endif}}</code>，条件仅支持 `==` / `!=` / 变量存在性
  - 循环：<code v-pre>{{for item in list}}...{{endfor}}</code>
- **`TemplatingOptions.DefaultEngine` 默认为 `"Scriban"`，但它不影响 `ITemplateService` 的这条路径**——该路径只认注册表里 `string` 类型的默认引擎（`"String"`）。别被这个字段误导。

要真正跑 Scriban，走下面两条路径之一（见"使用示例"）：原生 `Scriban.Template.Parse`，或从注册表取 `"Scriban"` 引擎。

## 核心能力

- **引擎注册表**：`ITemplateEngineRegistry` 按名称注册/获取引擎，并可设置某模板类型的默认引擎。
- **两种内置引擎**：Scriban（完整语法）与 String（轻量替换）。
- **模板服务门面**：`ITemplateService` 提供 `RenderAsync` / `RenderFileAsync` / `ValidateTemplate` / `CreateContext`。
- **模板上下文**：`ITemplateContext` 承载变量/函数/作用域，Scriban 与 String 引擎都基于它取值；`ITemplateContextFactory.CreateBuilder()` 可拿到 `ITemplateContextBuilder`（`AddVariable` / `AddVariables` / `AddObject` / `AddFunction` / `AddGlobalFunctions` / `Build`）做链式构造。
- **语法校验**：`TemplateValidationResult`（`IsValid` / `ErrorMessage` / `ErrorLine` / `ErrorColumn`）。
- **文件与缓存**：`DefaultTemplateEngine` 及 `TemplateService` 提供从文件渲染、渲染落盘、模板缓存等辅助方法。
- **安全分析（进阶，已注册）**：`ITemplateSecurityAnalyzer`（`AnalyzeSecurity` / `DetectThreats` / `ValidateWhitelist` / `CheckBlacklist` / `ScanFileIncludes`）+ `ITemplateSecurityChecker`（`CheckSecurity` / `CheckSecurityAsync` / `CheckExpression` / `ValidateContext`），均按 `TemplateSecurityOptions` 出 `TemplateSecurityResult` / `TemplateSecurityReport`（含 `SecurityThreat` / `SecurityRiskLevel` / `SecurityThreatType`）。
- **继承与片段复用（进阶，已注册）**：`ITemplateInheritanceManager`（`RegisterLayout` / `GetLayout` / `ParseInheritance` / `MergeTemplate` / `RenderInheritedTemplateAsync`）与 `ITemplatePartialManager`（`RegisterPartial` / `GetPartial(Async)` / `RemovePartial` / `GetPartialNames` / `RenderPartialAsync` / `PrecompileAllPartialsAsync` / `ClearPartialCache`）——两者内部各自维护一份内存字典，**不**依赖 `IPartialTemplateProvider` / `IFileSystemPartialProvider` / `IMemoryPartialProvider` / `ILayoutTemplateResolver` 等 provider 接口（这些接口连同其唯一实现 `MemoryPartialProvider` 存在于源码中，但默认 DI 未注册、也未被内置管理器消费，属于自行接入的扩展点）。
- **免 DI 的轻量静态 API**：`XiHan.Framework.Templating.Simple` 命名空间（`TemplateEngine` / `FileTemplateHelper` / `TemplateCache` / `TemplateExtensions`），见下方独立小节。
- **流式构建 API**：`Engines.DefaultTemplateEngineExtensions` 提供 `"..".CreateBuilder()` → `TemplateBuilder`（`WithVariable` / `WithVariables` / `WithModel` / `WithVariableIf` / `Render` / `RenderAsync` / `Validate` / `Clone`）。
- **未内置实现的接口**：`Compilers` / `Parsers` 两个命名空间下定义了一整套 AST/编译期抽象（`ITemplateParser`、`ITemplateAstBuilder`、`ITemplateCompiler<T>`、`ITemplatePrecompiler<T>`、`ITemplateExpression`、`ITemplateConditional`、`ITemplateLoop` 等接口及配套 DTO），当前**没有任何内置实现类，也未注册进 DI**，仅作为预留的扩展点定义，不能直接使用。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `ITemplateEngineRegistry` / `TemplateEngineRegistry` | 注册表。`RegisterEngine<T>(name, engine)` / `GetEngine<T>(name)` / `GetDefaultEngine<T>()` / `SetDefaultEngine<T>(name)` |
| `ITemplateEngine<T>` | 引擎接口：`Task<string> RenderAsync(T, ITemplateContext, CancellationToken)`、`string Render(T, ITemplateContext)`、`T Parse(string)`、`TemplateValidationResult Validate(string)` |
| `ScribanTemplateEngine` | `ITemplateEngine<Template>`，`Parse` 走 `Scriban.Template.Parse`，`Render` 转发 Scriban |
| `DefaultTemplateEngine` | `ITemplateEngine<string>`，轻量替换 + 条件/循环，另有 `RenderWithVariables` / `RenderWithModel` 两个非接口方法 |
| `ITemplateService` / `TemplateService` | 业务门面：`RenderAsync(string, object?)`、`RenderAsync(string, IDictionary<string, object?>)`、`RenderFileAsync(string, object?)`、`ValidateTemplate(string)`、`CreateContext(object?)` |
| `ITemplateContext` | 变量/函数/作用域：`GetVariable` / `SetVariable` / `HasVariable` / `GetVariableNames` / `PushScope` / `Clone` 等 |
| `ITemplateContextFactory` / `TemplateContextFactory` | `CreateContext()` / `CreateContext(IDictionary<string,object?>)` / `CreateContext(object)` / `CreateBuilder()` |
| `ITemplateInheritanceManager` / `TemplateInheritanceManager` | 布局注册与合并：`RegisterLayout` / `GetLayout` / `ParseInheritance` / `MergeTemplate` / `RenderInheritedTemplateAsync`（单例，内部内存字典） |
| `ITemplatePartialManager` / `TemplatePartialManager` | 片段注册与渲染：`RegisterPartial` / `GetPartial(Async)` / `RemovePartial` / `GetPartialNames` / `RenderPartialAsync` / `PrecompileAllPartialsAsync` / `ClearPartialCache`（单例，内部内存字典） |
| `ITemplateSecurityAnalyzer` / `TemplateSecurityAnalyzer` | `AnalyzeSecurity` / `DetectThreats` / `ValidateWhitelist` / `CheckBlacklist` / `ScanFileIncludes` |
| `ITemplateSecurityChecker` / `TemplateSecurityChecker` | `CheckSecurity` / `CheckSecurityAsync` / `CheckExpression` / `ValidateContext`，接收 `TemplateSecurityOptions?` |
| `TemplateValidationResult` | `record`，`IsValid` / `ErrorMessage` / `ErrorLine` / `ErrorColumn`，`Success` / `Failure(...)` |
| `TemplatingOptions` | 模板行为配置 |
| `Simple.TemplateEngine` / `FileTemplateHelper` / `TemplateCache` / `TemplateExtensions` | 免 DI 静态 API，见下方小节 |
| `Engines.DefaultTemplateEngineExtensions` / `TemplateBuilder` | 面向 `DefaultTemplateEngine` 的流式扩展方法与构建器 |
| `Extensions.TemplateExtensions` | 面向 `IServiceProvider` 的字符串/上下文扩展方法（`RenderAsync` / `Render` / `Validate` / `AddVariables` / `AddObject` / `WithScope(Async)`） |

## 配置

`AddXiHanTemplating()` 通过代码 `AddOptions<TemplatingOptions>().Configure(...)` 设默认值（未绑定独立配置节，`TemplatingOptions` 无 `SectionName`），如需覆盖可在应用侧再 `Configure<TemplatingOptions>`。

> **当前版本里 `TemplatingOptions` 基本是"声明位"**：全仓搜索只有 `TemplateService` 的构造函数接收并存了这个选项对象，除 `DefaultEngine` 外（其误导性见下文说明），其余字段——包括 `EnableCaching` / `CacheExpiration` / `MaxCacheSize` / `EnableDebugMode` / `EnablePerformanceMonitoring` / `RenderTimeout` / `MaxTemplateSize` / `DefaultEncoding` / `TemplateFileExtensions` / `EnableSecurityChecks` / `EnablePrecompilation` / `TemplateRootDirectory` / `LayoutDirectory` / `PartialDirectory`——目前**均未被任何内置引擎或服务读取**，改它们不会改变任何运行时行为（真正的渲染超时/缓存上限/安全检查开关等能力尚未接线）。可以正常 `Configure<TemplatingOptions>` 覆盖这些值供业务侧自行读取使用，但不要指望框架据此自动生效。

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `DefaultEngine` | `string` | `"Scriban"` | 语义上的默认引擎名。**注意**：不影响 `ITemplateService` 字符串重载（该路径固定用 `string` 类型默认引擎） |
| `EnableCaching` | `bool` | `true` | 是否启用缓存 |
| `CacheExpiration` | `TimeSpan` | 30 分钟 | 缓存过期 |
| `MaxCacheSize` | `int` | `1000` | 最大缓存条目 |
| `EnableDebugMode` | `bool` | `false` | 调试模式 |
| `EnablePerformanceMonitoring` | `bool` | `false` | 性能监控 |
| `RenderTimeout` | `TimeSpan` | 30 秒 | 渲染超时 |
| `MaxTemplateSize` | `int` | `1MB` | 最大模板字节数 |
| `DefaultEncoding` | `string` | `"UTF-8"` | 默认文件编码 |
| `TemplateFileExtensions` | `string[]` | `.html/.htm/.liquid/.scriban` | 模板文件扩展名 |
| `EnableSecurityChecks` | `bool` | `true` | 安全检查 |
| `EnablePrecompilation` | `bool` | `false` | 模板预编译 |
| `TemplateRootDirectory` | `string?` | `null` | 模板根目录 |
| `LayoutDirectory` | `string?` | `null` | 布局目录 |
| `PartialDirectory` | `string?` | `null` | 片段目录 |

## 使用示例

### 1. 简单替换引擎（`ITemplateService` 默认路径）

```csharp
public class NotifyService(ITemplateService templates)
{
    public Task<string> BuildAsync() =>
        templates.RenderAsync(
            "Hello {{name}}!{{if vip}} 尊贵的会员{{endif}}",
            new { name = "xihan", vip = true });
}
```

上例走 `DefaultTemplateEngine`（"String"），只做占位替换与简单条件——**Scriban 过滤器在这里无效**。

### 2. 真正跑 Scriban：原生 API

```csharp
// 用原生 Scriban，支持过滤器/函数
var template = Scriban.Template.Parse("Hello {{ name | string.upcase }}!");
string result = template.Render(new { name = "xihan" }); // "Hello XIHAN!"
```

### 3. 真正跑 Scriban：从注册表取引擎

```csharp
public class CodeGen(ITemplateEngineRegistry registry)
{
    public async Task<string> RenderAsync(string source, ITemplateContext ctx)
    {
        var engine = registry.GetEngine<Scriban.Template>("Scriban")!;
        var tpl = engine.Parse(source);            // Scriban.Template.Parse
        return await engine.RenderAsync(tpl, ctx);
    }
}
```

> 关键结论：**不要把 Scriban 语法丢给 `ITemplateService` 期待被 Scriban 解析**。需要 Scriban 时用方式 2 或方式 3。

### 4. 免 DI 的轻量静态 API：`XiHan.Framework.Templating.Simple`

`Simple` 命名空间是一套完全独立、**不经过 DI / `ITemplateEngineRegistry` / `ITemplateService`** 的静态占位替换工具，语法与 `DefaultTemplateEngine` 类似（<code v-pre>{{变量}}</code> / <code v-pre>{{if}}...{{else}}...{{endif}}</code> / <code v-pre>{{for x in list}}...{{endfor}}</code>），但代码实现互相独立、不共享缓存：

```csharp
using XiHan.Framework.Templating.Simple;

// 基础占位替换
var text = TemplateEngine.Render("Hello {{name}}!", new { name = "xihan" });

// 条件 + 循环（RenderAdvanced）
var report = TemplateEngine.RenderAdvanced(
    "{{for item in items}}- {{item}}\n{{endfor}}",
    new Dictionary<string, object?> { ["items"] = new[] { "A", "B" } });

// 直接渲染模板文件 / 落盘
var html = FileTemplateHelper.RenderFile("template.html", new { title = "首页" });
await FileTemplateHelper.RenderToFileAsync("in.tpl", "out.html", new { title = "首页" });

// 静态内存缓存（进程级 ConcurrentDictionary，与 TemplatingOptions.EnableCaching 无关）
TemplateCache.SetTemplate("welcome", "Hi {{name}}");
var rendered = TemplateCache.RenderCachedTemplate("welcome", new Dictionary<string, object?> { ["name"] = "xihan" });

// 扩展方法风格
var s = "Hi {{name}}".RenderTemplate(new { name = "xihan" });
```

适合脚本、控制台工具、单元测试等不方便走 DI 容器的场景。**不要**与下方 `DefaultTemplateEngineExtensions`（`Engines` 命名空间）混淆——两者都提供名为 `RenderTemplate` 的字符串扩展方法，签名相同，若同时 `using` 两个命名空间会导致编译期二义性错误（`CS0121`），只能保留一个 `using`。

### 5. 流式构建：`DefaultTemplateEngineExtensions` + `TemplateBuilder`

```csharp
using XiHan.Framework.Templating.Engines;

var result = "Hello {{name}}!{{if vip}} VIP{{endif}}"
    .CreateBuilder()
    .WithVariable("name", "xihan")
    .WithVariableIf(user.IsVip, "vip", true)
    .Render();
```

`CreateBuilder()` 内部持有一个模块级共享的 `DefaultTemplateEngine` 实例（与 DI 容器里的实例互不相关），`TemplateBuilder` 支持 `WithModel` / `Clone` / `Validate` 等链式方法。

## 扩展点 / 自定义

- **注册自定义引擎**：实现 `ITemplateEngine<T>` 后 `registry.RegisterEngine<T>("MyEngine", engine)`，需要时 `SetDefaultEngine<T>("MyEngine")` 设为该模板类型默认。
- **替换内置服务**：DI 注册均为 `TryAdd*`，在你的模块里先注册同接口实现即可覆盖默认。
- **自定义变量解析**：替换 `ITemplateVariableResolver` / `ITemplateContextFactory` 以改变变量来源与上下文构造。
- **片段/布局的存储后端**：`IPartialTemplateProvider` / `IFileSystemPartialProvider` / `IMemoryPartialProvider`（含内置的 `MemoryPartialProvider`）/ `ILayoutTemplateResolver` 是预留的 provider 抽象，默认未注册、内置的 `TemplateInheritanceManager` / `TemplatePartialManager` 也不会去调用它们；要接入文件系统等外部存储，需要自行实现并接线。
- **AST/编译期扩展预留**：`Compilers` / `Parsers` 命名空间的接口（`ITemplateParser`、`ITemplateCompiler<T>`、`ITemplatePrecompiler<T>` 等）目前没有内置实现，不能直接使用，仅供未来或自定义扩展参考。

## 注意事项与最佳实践

- **默认引擎陷阱**：`ITemplateService` 的字符串重载永远走 `string` 类型默认引擎（`DefaultTemplateEngine`），与 `TemplatingOptions.DefaultEngine` 无关。要 Scriban 请走原生 API 或注册表 `"Scriban"` 引擎。
- **占位语法差异**：简单引擎的条件仅支持 `==` / `!=` / 变量存在性判断，循环仅支持 <code v-pre>{{for x in list}}</code>；复杂逻辑请用 Scriban。
- **文件渲染**：`RenderFileAsync` 找不到文件会抛 `FileNotFoundException`；`Simple.FileTemplateHelper` 同样会抛 `FileNotFoundException`。
- **校验与渲染是不同引擎/路径**：`ValidateTemplate` 同样走 `string` 默认引擎，校验的是简单语法而非 Scriban 语法。
- **三套"字符串渲染扩展方法"并存、慎重 `using`**：`Extensions.TemplateExtensions`（经 `IServiceProvider` 走 DI 注册表）、`Engines.DefaultTemplateEngineExtensions`（固定用一个模块级 `DefaultTemplateEngine` 实例，与 DI 容器无关）、`Simple.TemplateExtensions`（固定用 `Simple.TemplateEngine` 的正则实现）三者互相独立，其中后两者都定义了签名相同的 `RenderTemplate(this string, IDictionary<string, object?>)` / `RenderTemplate(this string, object?)`，同时 `using` 会导致 `CS0121` 二义性编译错误。
- **`Simple.TemplateCache` 是进程级静态状态**：底层是 `static readonly ConcurrentDictionary`，全局唯一、无过期/无容量上限，与 `TemplatingOptions` 的缓存字段完全无关（见"配置"一节），清理需自行调用 `RemoveTemplate` / `ClearTemplates`。

## 依赖模块

- [XiHan.Framework.Core](./core) — 模块化与依赖注入基础。
- [XiHan.Framework.Serialization](./serialization) — 模块 `[DependsOn]` 依赖。
- [XiHan.Framework.Utils](./utils) — 通用工具库。
- 第三方核心依赖：`Scriban`（7.2.5）。

## 相关模块

- [XiHan.Framework.VirtualFileSystem](./virtual-file-system) — 从虚拟路径加载模板资源时可配合使用。
- [XiHan.Framework.Script](./script) — 动态脚本，与模板可配合做动态内容生成。
