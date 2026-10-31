# XiHan.Framework.Hosting.CommandLine

命令行宿主：在 .NET 通用宿主里把命令行参数翻译成内核调用，内置 `help`、`graph`、`pipeline`、`operations`、`invoke` 命令。适用于运维工具、批处理脚本，以及要以 stdio 给 MCP 客户端当子进程的应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Hosting.CommandLine` |
| 环层 | Ring 4（宿主） |
| 能力 | — |
| 特性入口 | —（`AddXiHanCommandLine()` 登记） |
| 配置节 | —；环境变量 `XIHAN_CONFIG_PATH`（挂载的配置目录）、`XIHAN_AUTHORIZATION`（缺省凭据） |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Security.Abstractions`、`XiHan.Framework.Localization.Abstractions`；`Microsoft.Extensions.Hosting` 10.0.13、`Microsoft.Extensions.Configuration.Json` 10.0.13、`Microsoft.Extensions.Logging.Console` 10.0.13 |
| AOT | 兼容（`IsAotCompatible`） |
| 稳定性 | Preview |

## 概述

一次进程执行一条命令。`invoke <操作 ID>` 与简写 `<resource> <verb>` 把 `--json`、`--data` 或字段选项拼成载荷，把 `--tenant`、`--correlation`、`--locale`、`--timeout`、`--authorization` 翻译成调用上下文，经完整内核管道调用暴露给命令行（`Expose.CommandLine`）的操作。成功时响应 JSON 写到 stdout，失败时一行 `{"code","detail","traceId"}` 写到 stderr，退出码按错误码的 KIND 段决定。`AddXiHanCommandLine()` 把日志改成只写 stderr，stdout 只有命令输出。

其他包可以登记自己的命令：实现 `ICommand` 后 `AddXiHanCommand<T>()`，`Protocols.Mcp` 就以这种方式提供 `mcp` 命令（MCP 的 stdio 传输）。

## 何时使用

同一份操作契约要在终端、CI 脚本或定时任务里调用时用它；要让 MCP 客户端以子进程方式连接应用时，与 [Protocols.Mcp](./protocols-mcp) 一起用。对外提供网络服务用 [Hosting.AspNetCore](./hosting-aspnetcore)。组合包都不包含本包。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Hosting.CommandLine" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Profiles.Minimal;

var builder = Host.CreateApplicationBuilder(args);
builder.AddXiHan(xihan => xihan.UseMinimalProfile());
builder.Services.AddXiHanCommandLine();

return await builder.Build().RunXiHanCommandLineAsync(args);
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `HostApplicationBuilderConfigurationExtensions` | `AddXiHan(configure)`：先装载 `XIHAN_CONFIG_PATH` 目录，再交给内核装配应用 |
| `CommandLineServiceCollectionExtensions` | `AddXiHanCommandLine()` 登记运行器与内置命令并把日志改到 stderr；`AddXiHanCommand<TCommand>()` 登记命令 |
| `CommandLineHostExtensions` | `RunXiHanCommandLineAsync(args)`：启动宿主、执行命令、停止宿主，返回退出码；Ctrl+C 取消命令 |
| `Commands.ICommand` | 命令：`Name`、`DescriptionKey`（说明的消息键）、`ExecuteAsync(CommandContext)` |
| `Commands.CommandContext` | 解析结果、作用域服务、`Output` / `Error`、取消令牌 |
| `Commands.HelpCommand`、`GraphCommand`、`PipelineCommand`、`OperationsCommand`、`InvokeCommand` | 内置命令 |
| `Parsing.CommandLineParser`、`Parsing.ParsedCommandLine` | 参数解析 |
| `Runtime.CommandLineInvocation` | `Create(context, operationId)` 按选项建调用上下文；`ReservedOptions`；`AuthorizationVariable` = `XIHAN_AUTHORIZATION` |
| `Runtime.CommandLineFailure` | `WriteAsync(...)` 把失败写到 stderr 并给出退出码 |
| `Runtime.CommandLineRunner` | 命令表与分发 |
| `CommandLineExitCodes` | 退出码常量与 `For(error)` |
| `CommandLineErrors` | `HOSTING_VALIDATION_001`、`HOSTING_NOTFOUND_001`、`HOSTING_NOTFOUND_002` |

## 使用示例

```bash
dotnet run --project next/samples/SimpleCli -- greetings create --name Ada --times 2
dotnet run --project next/samples/SimpleCli -- invoke greetings.create --json '{"name":"Ada"}' --locale zh-CN
XIHAN_AUTHORIZATION="ApiKey dev-alice-key" dotnet run --project next/samples/SimpleCli -- greetings create --name Ada
```

自定义命令与选项的完整说明见[命令行宿主](../guide/command-line)。

## 行为与限制

- 参数值不做类型转换，字段选项进载荷都是字符串；请求类型要从字符串读数字，序列化上下文需要 `NumberHandling = JsonNumberHandling.AllowReadingFromString`。
- 保留选项 `json`、`data`、`authorization`、`tenant`、`correlation`、`locale`、`timeout`、`help` 不进载荷。
- `--authorization` 与 `XIHAN_AUTHORIZATION` 只作用于 `invoke`；`mcp` 命令的调用以匿名主体执行。
- 命令名重复时 `CommandLineRunner` 构造失败（`ArgumentException`）。

| KIND / 情形 | 退出码 |
| --- | --- |
| 成功 | 0 |
| `VALIDATION` | 2 |
| `NOTFOUND`（含 `HOSTING_NOTFOUND_001` 操作未暴露） | 3 |
| `DENIED` | 4 |
| `CONFLICT` | 5 |
| `UNAVAILABLE` | 6 |
| `TIMEOUT` | 7 |
| `CANCELLED`、Ctrl+C | 8 |
| 其他 | 1 |
| 用法错误（`HOSTING_VALIDATION_001`、`HOSTING_NOTFOUND_002`） | 64 |

## 相关

- [命令行宿主](../guide/command-line)、[操作契约](../guide/operations)
- [Protocols.Mcp](./protocols-mcp)：`mcp` 命令
- [Hosting.AspNetCore](./hosting-aspnetcore)、[Cli 命令行工具](./cli)
- 规范：`spec/protocol/commandline.md`《命令行载体》、RFC-0011《操作契约与 XGI》
