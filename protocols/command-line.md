来源：https://framework.docs.xihanfun.com/protocols/command-line

# 命令行宿主

这一页讲命令行宿主：它怎样把命令行参数翻译成内核调用，怎样启用，内置命令有哪些，调用操作时载荷从哪里来、上下文选项有哪些，输出与退出码的约定，怎样写自己的命令，以及怎样经 stdio 提供 MCP。读完你能把同一份操作契约做成运维工具或批处理脚本里的命令，并让 MCP 客户端以子进程方式连接你的应用。前置知识是[装配入口](/startup/bootstrap)与[操作契约](/web/operations)。

## 为什么需要

每个后端系统最后都会长出一批「脚本」：初始化数据、批量修复、导出报表、给运维排障。它们通常绕开业务层直接连数据库，没有认证、没有授权、没有审计，一不小心就改错租户。另一类需求是把应用交给 AI 客户端当 MCP 服务端，桌面客户端要求以子进程方式经 stdin / stdout 通信，Web 宿主做不到。

命令行宿主解决这两件事。它跑在 .NET 通用宿主里，一次进程执行一条命令；同一份操作契约在命令行上就是一条命令，`invoke orders.create --name widget` 与 HTTP 一样经过完整的内核管道，Authenticate 阶段解释凭据，Authorize 阶段按契约判定，审计照常记录。stdout 只承载命令输出，日志与失败信息只写 stderr，脚本可以直接把 stdout 交给 `jq` 一类的工具；[启动 Banner](/startup/bootstrap#启动-banner) 在命令行宿主上缺省不打印，用 `XiHan:Kernel:Banner:Enabled=true` 打开时也只写 stderr。装配了 MCP 适配器时，它还提供 `mcp` 命令作为 stdio 传输。

## 内置命令

| 命令 | 输出 |
| --- | --- |
| `help` | 用法与命令列表 |
| `graph` | 能力图谱（`App.DescribeGraph()`） |
| `pipeline` | 管道阶段与中间件（`App.DescribePipeline()`） |
| `operations` | 操作登记表，每行 `ID<TAB>稳定性<TAB>暴露的协议<TAB>permission=…`、`policy=…` 或 `-` |
| `invoke <操作 ID>` | 调用操作，见下文 |
| `mcp` | 由 `Protocols.Mcp` 注册：MCP 的 stdio 传输，接受与 `invoke` 相同的上下文选项 |

没有命令时（包括只写了 `--help`）把用法写到 stderr 并返回 64；写了命令再带 `--help` 时把用法写到 stdout 并返回 0。`operations` 列出登记表里的全部操作，包括没有暴露给命令行的，用来核对各协议上的暴露情况。

参数的解析规则：第一个不是选项的记号是命令名，其余是位置参数；`--name value` 与 `--name=value` 等价；后面不跟值的 `--flag` 记为 `"true"`；`--` 之后全是位置参数。标志后面要接位置参数时写 `--flag=true`，或把标志放到最后。

## 最小示例

我们从零建一个命令行应用。三步：用控制台项目，引用 `Microsoft.Extensions.Hosting` 与 `XiHan.Framework.Hosting.CommandLine`，要暴露操作时再以分析器方式引用 `XiHan.Framework.Kernel.Generators` 生成操作登记代码；`AddXiHan(...)` 装配特性，`AddXiHanCommandLine()` 登记内置命令；以 `RunXiHanCommandLineAsync(args)` 运行，返回值就是进程退出码。

```bash
dotnet new console -n SimpleCli
dotnet add package Microsoft.Extensions.Hosting
dotnet add package XiHan.Framework.Hosting.CommandLine --prerelease
dotnet add package XiHan.Framework.Profiles.Minimal --prerelease
dotnet add package XiHan.Framework.Protocols.Mcp --prerelease
```

操作把 `Expose` 写成 `CommandLine | Mcp`，一份契约同时是命令与 MCP tool：

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace SimpleCli;

public sealed record GreetRequest(string Name, int Times = 1);

public sealed record GreetResponse(string Message, string TraceId);

[Operation(Id, Stability = Stability.Experimental, Expose = Expose.CommandLine | Expose.Mcp)]
public sealed class Greet : IOperation<GreetRequest, GreetResponse>
{
    public const string Id = "greetings.create";

    public ValueTask<Result<GreetResponse>> HandleAsync(GreetRequest request, InvocationContext context)
    {
        if (request.Times is < 1 or > 10)
        {
            return ValueTask.FromResult<Result<GreetResponse>>(Error.Validation("SAMPLES_VALIDATION_001", CliMessages.TimesInvalid, MessageArguments.Of(("times", request.Times))));
        }

        var message = string.Join(" ", Enumerable.Repeat($"Hello, {request.Name}!", request.Times));
        return ValueTask.FromResult(Result.Success(new GreetResponse(message, context.TraceId)));
    }
}
```

`CliMessages.TimesInvalid` 是消息键常量，文案在 `Localization/<区域>.json` 里，写法见[全球化和本地化](/common/localization)。`GreetingsFeature` 是最小的能力包，`Configure` 里只调用 `context.AddGeneratedOperations()` 登记生成器产出的操作。`Program.cs`：

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

运行：

```bash
dotnet run -- operations
dotnet run -- greetings create --name Ada --times 2
```

```text
greetings.create	Experimental	Mcp, CommandLine	-
{"message":"Hello, Ada! Hello, Ada!","traceId":"4bf92f3577b34da6a3ce929d0e0e4736"}
```

`HostApplicationBuilder` 上的 `AddXiHan` 与 ASP.NET Core 宿主一样，先装载 `XIHAN_CONFIG_PATH` 目录里的 JSON 文件（可重载，紧接 `appsettings.{环境}.json` 插入），见[配置目录与热更新](/configuration/config-directory)。`AddXiHanCommandLine()` 清掉通用宿主的默认日志提供器，只留写 stderr 的控制台日志。`RunXiHanCommandLineAsync` 先启动宿主再执行命令，命令结束后按顺序停止宿主；Ctrl+C 取消当前命令（退出码 8），宿主仍正常停止。

## 调用操作与载荷来源

`invoke <操作 ID>` 只接受 `Expose` 含 `CommandLine`、稳定性不是 `Stability.Internal` 的操作，其他操作得到 `HOSTING_NOTFOUND_001`、退出码 3。命令名不是已登记的命令、且与下一个参数能拼成合法操作 ID 时，`<resource> <verb>` 等价于 `invoke <resource>.<verb>`，所以 `greetings create` 就是 `invoke greetings.create`。

载荷是一个 JSON 对象，按下面的优先顺序取第一个：

| 来源 | 写法 | 说明 |
| --- | --- | --- |
| `--json` | `--json '{"name":"Ada","times":2}'` | 必须是 JSON 对象 |
| `--data` | `--data request.json` 或 `--data @request.json` | 文件内容必须是 JSON 对象 |
| 字段选项 | `--name Ada --times 2` | 其余非保留选项拼成对象：值按操作的请求 Schema 写成对应类型，重复的选项成数组，没有字段时为 `{}` |

保留选项不进载荷：`json`、`data`、`authorization`、`tenant`、`correlation`、`locale`、`timeout`、`help`。

字段选项的值按操作的请求 JSON Schema 转换，所以 `--times 2` 进载荷是 `"times":2`：

| Schema 声明 | 写成 |
| --- | --- |
| `string`，或没有约束类型 | 字符串，`--name 42` 仍是 `"name":"42"` |
| `boolean` | `true` 或 `false`，不跟值的 `--flag` 是 `true` |
| `integer`、`number` | JSON 数字 |
| `array` | 数组，元素按 `items` 转换，单个值也是一元数组 |

对象字段不从文本解析，要传嵌套对象就用 `--json` 或 `--data`，这两种载荷都原样进入、不做转换。转换不了的值（如 `--times abc`）不是用法错误：它留成字符串进管道，由 Validate 阶段报 `KERNEL_VALIDATION_003`，退出码 2。操作没有 Schema（手写 `AddOperation` 时没传 `OperationSchema`）时值都写字符串，这时请求类型要从字符串读数字，序列化上下文需要 `NumberHandling = JsonNumberHandling.AllowReadingFromString`。

## 调用上下文参数

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

这里的 `app` 指应用发布出的可执行文件。各项进入调用上下文后的含义见[调用上下文](/startup/invocation-context)。

## 输出与退出码

成功时把响应 JSON 写一行到 stdout，退出码 0。失败时把一行 JSON 写到 stderr：`{"code":"<错误码>","detail":"<文案>","traceId":"<trace-id>"}`，`detail` 按 `--locale` 或默认区域翻译；还没有建出调用上下文的失败（用法错误、操作未暴露）没有 `traceId`。

```bash
dotnet run -- invoke greetings.create --json '{"name":"Ada","times":99}'; echo $?
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

用法错误包括：没有命令、未知命令（`HOSTING_NOTFOUND_002`）、`invoke` 缺操作 ID、`--json` 不是 JSON 对象、`--data` 文件不存在、`--timeout` 或 `--authorization` 格式不对（都是 `HOSTING_VALIDATION_001`）。认证失败与缺少权限都是 `DENIED`，退出码 4，不区分 HTTP 上的 401 与 403。错误码与 KIND 的全貌见[结果与错误处理](/web/errors)。

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

`CommandContext` 提供解析结果（`CommandLine`、`Arguments`、`Option(name)`、`HasOption(name)`）、作用域服务（`Services`）、输出流与取消令牌。命令要调用操作时，用 `CommandLineInvocation.Create(context, operationId)` 按同一套选项建调用上下文；一条命令里发起多次独立调用时用 `Create(context, services, operationId)`，每次在自己的服务作用域里建，`mcp` 命令就是这样为每个 `tools/call` 建上下文。失败用 `CommandLineFailure.WriteAsync` 写 stderr 并得到退出码，与 `invoke` 一致。命令名重复时 `CommandLineRunner` 构造失败（`ArgumentException`）。

## MCP stdio

装配了 `McpProtocolFeature` 时，`mcp` 命令以 stdio 提供 MCP 服务端：stdin 与 stdout 每行一条 JSON-RPC 消息，日志只写 stderr，stdin 关闭即结束会话。stdin 按 UTF-8 解码，与控制台代码页无关；答复只含 ASCII（非 ASCII 字符按 JSON 转义）。MCP 客户端以子进程方式启动应用：

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run -- mcp
```

答复里的 tool `greetings_create` 带消息目录里的本地化描述。`mcp` 命令接受与 `invoke` 相同的上下文选项，作用于整个会话里的每个 `tools/call`：凭据 `--authorization` 或环境变量 `XIHAN_AUTHORIZATION`（选项优先，MCP 规范要求 stdio 服务端从运行环境取凭据），`--tenant`、`--correlation`、`--locale`（`tools/list` 的标题与描述、失败文案按它翻译），`--timeout <秒>` 是每个 `tools/call` 的时限。每个 `tools/call` 经完整管道，凭据在这次调用的 Authenticate 阶段认证；stdio 没有外层认证，凭据无效时需要认证的 tool 得到 `isError`（`SECURITY_DENIED_003`），会话继续。选项不合法时不进入会话，stderr 写一行用法错误，退出码 64。

```bash
XIHAN_AUTHORIZATION="ApiKey dev-alice-key" dotnet run -- mcp --tenant acme --locale zh-CN
```

桌面客户端的配置通常是「命令 + 参数 + 环境变量」三项：命令填发布出的可执行文件，参数填 `mcp` 与需要的上下文选项，环境变量填 `XIHAN_AUTHORIZATION`。tool 的规则见 [MCP](/protocols/mcp)。

## 常见问题

### 为什么不用 System.CommandLine 之类的解析库

命令行宿主的参数模型很小：一个命令名、若干 `--key value` 选项、位置参数，载荷的类型由操作的请求 Schema 决定而不是由参数定义决定。引入解析库要为每个操作生成一套参数定义，与契约形成第二份拷贝。自定义命令需要复杂解析时，在 `ExecuteAsync` 里对 `context.Arguments` 自行处理即可。

### 同一个可执行文件能既当 Web 服务又当命令行吗

可以，但两个宿主不同：Web 用 `WebApplication.CreateBuilder`，命令行用 `Host.CreateApplicationBuilder`。常见做法是按第一个参数分流，或者把业务能力包放进类库，两个入口项目各自装配。两边 `AddXiHan` 的特性列表相同，操作就同时在 HTTP 与命令行上可用。

### 失败信息能不能也写到 stdout

不能，这是有意的约定：stdout 只有响应 JSON，脚本才能把它直接管道给 `jq`。失败是一行 JSON 写到 stderr，退出码按 KIND 区分；脚本用 `2>` 单独捕获 stderr 或只看退出码。

### 把 `--timeout` 设得很长，命令会一直等吗

`--timeout` 是这次调用的截止时间，管道里的中间件与操作拿到的 `CancellationToken` 到点取消。Ctrl+C 不受它影响，随时取消当前命令并返回 8，宿主照常停止。

### `operations` 里有操作，`invoke` 却说未暴露

`invoke` 只接受 `Expose` 含 `CommandLine` 的操作。`operations` 的第三列就是暴露的协议，少了 `CommandLine` 就在 `[Operation]` 的 `Expose` 里加上；`Stability.Internal` 的操作在任何协议上都不暴露。

## 相关包与下一步

- 包参考：[Hosting.CommandLine](/packages/hosting-commandline)、[Protocols.Mcp](/packages/protocols-mcp)
- 同一份契约的 HTTP 宿主：[ASP.NET Core 宿主](/web/hosting)、[操作契约](/web/operations)
- 错误码与 KIND：[结果与错误处理](/web/errors)
- 规范：RFC-0011《操作契约与 XGI》
- 下一章：[作业与计划](/scheduling/jobs)，按计划运行后台工作
