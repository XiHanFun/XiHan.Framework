# 命令行宿主

本页讲命令行宿主怎样把命令行参数翻译成内核调用：内置命令、操作调用的载荷与上下文选项、输出与退出码、自定义命令，以及经 stdio 提供 MCP，适用于运维工具、批处理脚本和要给 MCP 客户端当子进程的应用。完整 API 见 [Hosting.CommandLine 包](../packages/hosting-commandline)。

## 宿主做什么

`Hosting.CommandLine` 是 Ring 4 的宿主，跑在 .NET 通用宿主（`Host.CreateApplicationBuilder`）里，一次进程执行一条命令。同一份操作契约在命令行上是一条命令：`invoke greetings.create --name Ada` 或简写 `greetings create --name Ada`，调用与 HTTP 一样经过完整的内核管道，Authenticate 阶段解释凭据，Authorize 阶段按契约判定。stdout 只承载命令输出，日志与失败信息只写 stderr，脚本可以直接把 stdout 交给 `jq` 一类的工具。

## 启用

1. 用控制台项目，引用 `Microsoft.Extensions.Hosting` 与 `XiHan.Framework.Hosting.CommandLine`；要暴露操作时再引用 `Kernel.Generators`（以分析器方式）生成操作登记代码。
2. `AddXiHan(...)` 装配特性，`builder.Services.AddXiHanCommandLine()` 登记内置命令。
3. 以 `RunXiHanCommandLineAsync(args)` 运行，返回值就是进程退出码。

`samples/SimpleCli` 的 `Program.cs`：

```csharp
using Microsoft.Extensions.Hosting;
using SimpleCli;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.Mcp;

var builder = Host.CreateApplicationBuilder(args);

// 同一份操作契约既是命令（greetings create --name Ada），也是 MCP tool（mcp 命令的 stdio 传输）
builder.AddXiHan(xihan => xihan
    .UseMinimalProfile()
    .UseFeature<McpProtocolFeature>()
    .UseFeature<GreetingsFeature>());

builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("samples", typeof(CliMessages)));
builder.Services.AddXiHanCommandLine();

return await builder.Build().RunXiHanCommandLineAsync(args);
```

`GreetingsFeature` 登记了一个操作 `greetings.create`，它的 `[Operation]` 写了 `Expose = Expose.CommandLine | Expose.Mcp`。`HostApplicationBuilder` 上的 `AddXiHan` 与 ASP.NET Core 宿主一样，先装载 `XIHAN_CONFIG_PATH` 目录里的 JSON 文件（可重载），见[宿主与 Web 应用](./web#启用)。`AddXiHanCommandLine()` 清掉通用宿主的默认日志提供器，只留写 stderr 的控制台日志。`RunXiHanCommandLineAsync` 先启动宿主再执行命令，命令结束后按顺序停止宿主；Ctrl+C 取消当前命令（退出码 8），宿主仍正常停止。

## 内置命令

| 命令 | 输出 |
| --- | --- |
| `help` | 用法与命令列表 |
| `graph` | 能力图谱（`App.DescribeGraph()`） |
| `pipeline` | 管道阶段与中间件（`App.DescribePipeline()`） |
| `operations` | 操作登记表，每行 `ID<TAB>稳定性<TAB>暴露的协议<TAB>permission=…`、`policy=…` 或 `-` |
| `invoke <操作 ID>` | 调用操作，见下一节 |
| `mcp` | `Protocols.Mcp` 注册：MCP 的 stdio 传输 |

没有命令时（包括只写了 `--help`）把用法写到 stderr 并返回 64；写了命令再带 `--help` 时把用法写到 stdout 并返回 0。

```bash
dotnet run --project next/samples/SimpleCli -- operations
```

```text
greetings.create	Experimental	Mcp, CommandLine	-
```

`operations` 列出登记表里的全部操作，包括没有暴露给命令行的，用来核对各协议上的暴露情况。

## 调用操作

`invoke <操作 ID>` 只接受 `Expose` 含 `CommandLine`、稳定性不是 `Stability.Internal` 的操作，其他操作得到 `HOSTING_NOTFOUND_001`、退出码 3。命令名不是已登记的命令、且与下一个参数能拼成合法操作 ID 时，`<resource> <verb>` 等价于 `invoke <resource>.<verb>`。

载荷是一个 JSON 对象，按下面的优先顺序取第一个：

| 来源 | 写法 | 说明 |
| --- | --- | --- |
| `--json` | `--json '{"name":"Ada","times":2}'` | 必须是 JSON 对象 |
| `--data` | `--data request.json` 或 `--data @request.json` | 文件内容必须是 JSON 对象 |
| 字段选项 | `--name Ada --times 2` | 其余非保留选项拼成对象：值都是字符串，重复的选项成数组，没有字段时为 `{}` |

字段选项的值不做类型转换，`--times 2` 进载荷是 `"times":"2"`；请求类型要从字符串读数字，序列化上下文需要 `NumberHandling = JsonNumberHandling.AllowReadingFromString`（示例的 `CliJsonContext` 就是这样写的）。保留选项不进载荷：`json`、`data`、`authorization`、`tenant`、`correlation`、`locale`、`timeout`、`help`。

参数的解析规则：第一个不是选项的记号是命令名，其余是位置参数；`--name value` 与 `--name=value` 等价；后面不跟值的 `--flag` 记为 `"true"`；`--` 之后全是位置参数。标志后面要接位置参数时写 `--flag=true`，或把标志放到最后。

```bash
dotnet run --project next/samples/SimpleCli -- greetings create --name Ada --times 2
```

```json
{"message":"Hello, Ada! Hello, Ada!","traceId":"4bf92f3577b34da6a3ce929d0e0e4736"}
```

## 调用上下文

| 选项 | 调用上下文 | 说明 |
| --- | --- | --- |
| `--tenant <id>` | Baggage `xihan.tenant` | |
| `--correlation <id>` | Baggage `xihan.correlation` | |
| `--locale <区域>` | Baggage `xihan.locale` | 规范化后写入，失败文案据此翻译；格式不合法时忽略 |
| `--timeout <秒>` | 截止时间 | 正数秒，可带小数，按宿主时钟换算成绝对时间；不是正数时为用法错误 |
| `--authorization '<方案> <值>'` | 凭据 | 没有选项时取环境变量 `XIHAN_AUTHORIZATION`；原样放入，由 Authenticate 阶段解释；格式不是「方案 空格 值」时为用法错误 |

每次调用生成新的 trace-id，调用来源记协议 `cli` 与操作 ID，没有远端地址。凭据放进环境变量可以不出现在进程列表与 shell 历史里：

```bash
export XIHAN_AUTHORIZATION="ApiKey dev-alice-key"
app orders create --name widget --amount 3 --tenant acme --locale zh-CN --timeout 30
```

这里的 `app` 指应用发布出的可执行文件。

## 输出与退出码

成功时把响应 JSON 写一行到 stdout，退出码 0。失败时把一行 JSON 写到 stderr：`{"code":"<错误码>","detail":"<文案>","traceId":"<trace-id>"}`，`detail` 按 `--locale` 或默认区域翻译；还没有建出调用上下文的失败（用法错误、操作未暴露）没有 `traceId`。

```bash
dotnet run --project next/samples/SimpleCli -- invoke greetings.create --json '{"name":"Ada","times":99}'; echo $?
```

```text
{"code":"SAMPLES_VALIDATION_001","detail":"The number of greetings must be between 1 and 10, but 99 was given.","traceId":"0af7651916cd43dd8448eb211c80319c"}
2
```

退出码按错误码的 KIND 段决定（`CommandLineExitCodes.For`）：

| KIND / 情形 | 退出码 | 常量 |
| --- | --- | --- |
| 成功 | 0 | `Success` |
| `VALIDATION` | 2 | `Validation` |
| `NOTFOUND` | 3 | `NotFound` |
| `DENIED` | 4 | `Denied` |
| `CONFLICT` | 5 | `Conflict` |
| `UNAVAILABLE` | 6 | `Unavailable` |
| `TIMEOUT` | 7 | `Timeout` |
| `CANCELLED`、Ctrl+C | 8 | `Cancelled` |
| 其他（含 `UNEXPECTED`） | 1 | `Unexpected` |
| 用法错误 | 64 | `Usage` |

用法错误包括：没有命令、未知命令（`HOSTING_NOTFOUND_002`）、`invoke` 缺操作 ID、`--json` 不是 JSON 对象、`--data` 文件不存在、`--timeout` 或 `--authorization` 格式不对（都是 `HOSTING_VALIDATION_001`）。认证失败与缺少权限都是 `DENIED`，退出码 4，不区分 HTTP 上的 401 与 403。

## 自定义命令

实现 `ICommand`（命令名、说明的消息键、执行），用 `AddXiHanCommand<TCommand>()` 登记；`help` 按名字排序列出全部命令，说明用 `DescriptionKey` 按当前区域取文案。

```csharp
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Primitives;

public sealed class VersionCommand : ICommand
{
    public string Name => "version";

    public string DescriptionKey => ToolMessages.VersionDescription;

    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        // 命令输出只写 context.Output（stdout），诊断信息写 context.Error（stderr）
        await context.Output.WriteLineAsync(FrameworkInfo.Version);
        return CommandLineExitCodes.Success;
    }
}

public static class ToolMessages
{
    // 文案放在应用消息目录的 Localization/<区域>.json 里
    public const string VersionDescription = "tool.version.description";
}
```

```csharp
builder.Services.AddXiHanCommand<VersionCommand>();
```

`CommandContext` 提供解析结果（`CommandLine`、`Arguments`、`Option(name)`、`HasOption(name)`）、作用域服务（`Services`）、输出流与取消令牌。命令要调用操作时，用 `CommandLineInvocation.Create(context, operationId)` 按同一套选项建调用上下文，失败用 `CommandLineFailure.WriteAsync` 写 stderr 并得到退出码，与 `invoke` 一致。命令名重复时 `CommandLineRunner` 构造失败（`ArgumentException`）。

## MCP stdio

装配了 `McpProtocolFeature` 时，`mcp` 命令以 stdio 提供 MCP 服务端：stdin 与 stdout 每行一条 JSON-RPC 消息，日志只写 stderr，stdin 关闭即结束会话。MCP 客户端以子进程方式启动应用：

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project next/samples/SimpleCli -- mcp
```

答复里的 tool `greetings_create` 带消息目录里的本地化描述。stdio 调用不带凭据、租户与区域，以匿名主体执行，`--authorization` 等选项与 `XIHAN_AUTHORIZATION` 只作用于 `invoke`；要求权限或登录的操作经 stdio 调用总是得到 `isError` 结果。tool 的规则见 [gRPC、MCP 与 CloudEvents](./protocols#mcp)。

## 相关

- [Hosting.CommandLine 包](../packages/hosting-commandline)、[Protocols.Mcp 包](../packages/protocols-mcp)
- [操作契约](./operations)：`Expose.CommandLine`
- [宿主与 Web 应用](./web)：同一份契约的 HTTP 宿主
- [结果与错误](./errors)：错误码与 KIND
- 规范：`spec/protocol/commandline.md`《命令行载体》、`spec/conventions/operation-ids.md`《操作 ID》、RFC-0011《操作契约与 XGI》
