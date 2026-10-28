# XiHan.Framework.Hosting.CommandLine

命令行宿主（Ring 4）。在通用宿主（`Host.CreateApplicationBuilder`）里把命令行参数翻译为内核调用：一份操作契约在命令行上也能服务（RFC-0011，`spec/protocol/commandline.md`）。

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.AddXiHan(xihan => xihan.UseMinimalProfile().UseFeature<McpProtocolFeature>().UseFeature<GreetingsFeature>());
builder.Services.AddXiHanCommandLine();
return await builder.Build().RunXiHanCommandLineAsync(args);
```

```bash
app help                                   # 用法与命令列表
app graph                                  # 能力图谱（App.DescribeGraph）
app pipeline                               # 管道阶段与中间件（App.DescribePipeline）
app operations                             # 登记表：ID、稳定性、暴露的协议、权限或策略
app invoke greetings.create --name Ada     # 字段选项拼成载荷；或 --json '{"name":"Ada"}'、--data request.json
app greetings create --name Ada --times 2  # <resource> <verb> 是 invoke 的简写
app mcp                                    # Protocols.Mcp 注册的 stdio 传输
```

## 规则

- 解析：第一个非选项记号是命令名；`--name value`、`--name=value`、无值的 `--flag` 记为 `true`；`--` 之后全是位置参数；值不做类型转换。
- `invoke`：只接受 `Expose` 含 `CommandLine` 且非 `Stability.Internal` 的操作。载荷按 `--json`（须为对象）> `--data <file>`（可写 `@file`）> 其余 `--<field> <value>` 选项（字符串，重复成数组）的顺序取；请求类型从字符串读数字需启用 `NumberHandling = AllowReadingFromString`。
- 上下文：`--tenant`、`--correlation`、`--locale` 进 Baggage；`--timeout <秒>` 换算为截止时间；`--authorization '<scheme> <value>'`（或环境变量 `XIHAN_AUTHORIZATION`）进入凭据由 Authenticate 阶段解释；调用来源记 `cli` 与操作 ID（ADR-0043）。调用经完整内核管道，Authorize 阶段按契约判定。
- 输出：成功把响应 JSON 写到 stdout；失败把 `{"code","detail","traceId"}` 写到 stderr（`detail` 按 `--locale` 或默认区域翻译），退出码按错误 KIND：VALIDATION 2、NOTFOUND 3、DENIED 4、CONFLICT 5、UNAVAILABLE 6、TIMEOUT 7、CANCELLED 8、其他 1；用法错误 64。
- 日志：`AddXiHanCommandLine()` 把日志改为只写 stderr，stdout 只承载命令输出。
- 扩展：实现 `ICommand`（名字、说明的消息键、执行）并 `AddXiHanCommand<T>()`；命令名重复在启动时失败。

`AddXiHan(this HostApplicationBuilder, …)` 与 ASP.NET Core 宿主一样先装载 `XIHAN_CONFIG_PATH` 目录里的 JSON 文件（可重载）。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Security.Abstractions`、`XiHan.Framework.Localization.Abstractions` 与 `Microsoft.Extensions.Logging.Console`。被 `Protocols.Mcp`（`mcp` 命令）引用。

## 状态

P3：可用。`xihan graph` / `xihan pipeline dump`（P1-19）由本宿主的 `graph` / `pipeline` 命令提供。
