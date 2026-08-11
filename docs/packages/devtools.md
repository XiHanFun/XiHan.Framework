# XiHan.Framework.DevTools

> 开发工具库。当前实际落地内容是一套从零实现、零第三方依赖的命令行（CommandLine）框架。

- **NuGet**：`XiHan.Framework.DevTools`
- **模块类**：`XiHanDevToolsModule`（当前为空类骨架，见下文）
- **所在层**：基础设施层
- **关键依赖**：仅框架内部 `XiHan.Framework.Core`（无任何第三方 `PackageReference`）；命令行框架完全自研

## 概述

XiHan.Framework.DevTools 定位为框架的开发工具库。就目前源码而言，它落地的核心是 `CommandLine/` 目录下一套**不依赖任何第三方 CLI 库**的命令行框架：把参数解析、命令注册与分发、强类型绑定、参数校验、帮助生成和交互式模式封装成一组可直接使用的类型，方便你为工具程序、脚手架或调试小工具快速搭出规范的命令行入口。

> **模块类现状**：包内 `XiHanDevToolsModule` 目前是一个**空的普通类**——它既未继承 `XiHanModule`，也没有 `ConfigureServices`。因此本包实际是一个「直接引用即可」的工具库，而非需要 `[DependsOn]` 挂载的功能模块；仓库内 README 里给出的 `[DependsOn(typeof(XiHanDevToolsModule))]` 用法**当前并不成立**（该 README 的「目录结构」也是模板化的，未反映真实的 `CommandLine` 子系统，以源码为准）。

## 何时使用

- 需要给控制台程序或工具快速加上命令、子命令、选项与位置参数解析，又不想引入外部 CLI 库。
- 想把命令行参数直接绑定到强类型对象（`Parse<T>` / `CommandLineBinder`）。
- 需要内置的 `--help` / `--version` 输出，或一个简单的交互式（REPL 式）命令行模式。

## 安装与启用

```bash
dotnet add package XiHan.Framework.DevTools
```

`XiHanDevToolsModule` 为空骨架，无需也无法通过它挂载功能。直接引用本包，使用 `CommandLine` 命名空间下的类型即可：

```csharp
using XiHan.Framework.DevTools.CommandLine;

var app = new CommandApp { Description = "示例工具" };
app.DiscoverCommands();          // 自动发现带 [Command] 特性的命令
return await app.RunAsync(args);
```

## 工作原理

`CommandApp.RunAsync` 的处理链路：`CommandLineParser` 解析原始参数 → 检查 `--help` / `--version` 内置选项 → 按命令名（含子命令）定位 `CommandDescriptor` → `CommandLineBinder` 将参数绑定到命令对象（含必填校验与自定义校验）→ 依命令实现的接口调用 `ExecuteAsync`（`ICommand`）或 `Execute`（`ISyncCommand`），返回退出码。命令通过 `[Command]` 特性声明、`AddCommand<T>()` 显式注册或 `DiscoverCommands()` 反射发现。

## 核心能力

- **命令应用宿主（`CommandApp`）**：`AddCommand<T>()` / `AddCommand(Type)` 注册、`DiscoverCommands(assembly?)` 按 `[Command]` 特性自动发现、`Run` / `RunAsync` 执行，自动处理 `--help` / `--version`，支持默认命令与隐藏命令（`GetVisibleCommands`）。
- **参数解析（`CommandLineParser` / `CommandLineParserFactory`）**：支持长选项（`--name`）、短选项（`-n`）、POSIX 组合短选项（`-abc`）、键值对（`key=value` / `key:value`）与 `--` 停止解析标记；`CommandLineParserFactory` 提供静态 `Parse` / `Parse<T>` / `TryParse` / `TryParse<T>` 便捷入口。
- **强类型绑定（`CommandLineBinder`）**：静态 `Bind<T>(ParsedArguments)` 以及扩展 `Bind(this CommandLineBinder, Type, ParsedArguments)`，把解析结果绑定到标注了选项/参数特性的属性或字段，并在绑定时执行必填与自定义校验。
- **命令定义**：实现 `ICommand`（异步）或 `ISyncCommand`（同步），配合 `[Command]`、`[CommandOption]`、`[CommandArgument]`、`[SubCommand]` 声明元数据，支持子命令层级。
- **参数校验**：内置 `[Range]`、`[FileExists]`、`[DirectoryExists]` 校验特性（均继承本包的 `ValidationAttribute`）及对应 `IValidator` 实现。
- **帮助与交互**：`HelpGenerator` 生成帮助文本；`RunInteractiveAsync` / `RunInteractive` 扩展方法提供交互式命令行模式（`InteractiveMode`）。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `CommandApp` | 命令行应用宿主，负责注册、发现、解析与执行命令，含 `--help` / `--version` 处理 |
| `CommandLineParser` / `CommandLineParserFactory` | 参数解析器（实例式 / 静态工厂式），产出 `ParsedArguments` 或强类型对象 |
| `CommandLineBinder` | 将 `ParsedArguments` 绑定到强类型命令对象（`Bind<T>` / `Bind(Type, ...)`） |
| `ICommand` / `ISyncCommand` | 异步 `Task<int> ExecuteAsync(CommandContext)` / 同步 `int Execute(CommandContext)` |
| `CommandContext` / `CommandDescriptor` | 命令执行上下文与命令描述符（含子命令、隐藏、默认命令等元数据） |
| `ParsedArguments` / `ArgumentToken` | 解析结果与单个参数词元 |
| `CommandAttribute` / `CommandOptionAttribute` / `CommandArgumentAttribute` / `SubCommandAttribute` | 声明命令、选项、位置参数与子命令的特性 |
| `ValidationAttribute` / `RangeAttribute` / `FileExistsAttribute` / `DirectoryExistsAttribute` | 校验特性基类与内置校验特性 |
| `IValidator` / `RangeValidator` / `FileExistsValidator` / `DirectoryExistsValidator` | 校验器接口与内置校验器 |
| `ParseOptions` | 解析行为配置（见下） |
| `HelpGenerator` / `InteractiveMode` | 帮助文本生成与交互式模式 |

## 配置（`ParseOptions`）

`ParseOptions` 是解析行为配置对象（通过 `new CommandApp(parseOptions)` 或 `CommandLineParserFactory.Parse(args, options)` 传入），**非 appsettings 配置节**：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `AllowUnknownOptions` | `bool` | `false` | 是否允许未知选项 |
| `CaseSensitive` | `bool` | `false` | 命令/选项名是否大小写敏感 |
| `EnablePosixStyle` | `bool` | `true` | 是否启用 POSIX 组合短选项（`-abc`） |
| `AutoGenerateHelp` | `bool` | `true` | 是否自动处理帮助选项 |
| `AutoGenerateVersion` | `bool` | `true` | 是否自动处理版本选项 |
| `HelpOptions` | `string[]` | `["help", "h"]` | 帮助选项名 |
| `VersionOptions` | `string[]` | `["version", "v"]` | 版本选项名 |
| `ValueSeparators` | `char[]` | `['=', ':']` | 键值对分隔符 |
| `StopParsingMarker` | `string` | `"--"` | 停止解析标记 |

## 使用示例

```csharp
using XiHan.Framework.DevTools.CommandLine;
using XiHan.Framework.DevTools.CommandLine.Attributes;
using XiHan.Framework.DevTools.CommandLine.Commands;

[Command("greet", Description = "打招呼命令")]
public class GreetCommand : ICommand
{
    [CommandOption("name", "n", Description = "名字")]
    public string Name { get; set; } = "World";

    public Task<int> ExecuteAsync(CommandContext context)
    {
        Console.WriteLine($"Hello, {Name}!");
        return Task.FromResult(0);
    }
}

// 入口
var app = new CommandApp();
app.AddCommand<GreetCommand>();
return await app.RunAsync(args);   // 例如: greet --name XiHan
```

一次性把参数解析为强类型对象：

```csharp
var options = CommandLineParserFactory.Parse<GreetCommand>(args);
```

## 扩展点 / 自定义

- **自定义命令**：实现 `ICommand` / `ISyncCommand` 并标注 `[Command]`，用 `AddCommand<T>()` 或 `DiscoverCommands()` 接入。
- **自定义校验**：继承 `ValidationAttribute` 或实现 `IValidator`，绑定阶段会自动执行。
- **调整解析行为**：通过 `ParseOptions` 定制大小写敏感、POSIX 风格、值分隔符、帮助/版本选项等。

## 注意事项与最佳实践

- **模块类是空骨架**：不要指望 `[DependsOn(typeof(XiHanDevToolsModule))]` 生效——本包按普通工具库直接引用使用。
- **绑定要求**：强类型绑定目标类型需可无参构造（`Parse<T>` 约束 `T : new()`）；`CommandApp` 内部通过 `CommandDescriptor.CreateInstance()` 创建命令实例再复制绑定值。
- **以源码为准**：包内 README 是模板化文档，其 `[DependsOn]` 用法与「目录结构」均与真实实现不符。

## 依赖模块

- [XiHan.Framework.Core](./core) — 唯一的 `ProjectReference`；无任何第三方 `PackageReference`。
- 运行期还会用到 `XiHan.Framework.Utils` 的 `ConsoleTools`（着色输出，如 `ConsoleColorWriter`），随依赖链间接引入。

## 相关模块

- [XiHan.Framework.Core](./core) — 核心库，本包的唯一直接依赖。
- [XiHan.Framework.Utils](./utils) — 通用工具库，其中 `ConsoleTools` 为本包提供控制台输出支持。
