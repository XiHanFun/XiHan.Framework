# ADR-0331 MCP 的 stdio 一行设上限与消息过 JSON 文本的共同检查

## 状态

已采纳。

- 规范：
  - `protocol/mcp.md`：「操作映射」的错误表第一行扩成「不是 JSON，或没过 JSON 文本的共同检查；stdio 的一行不是合法的 UTF-8 或超过上限」，表后新增「JSON 文本的共同检查」一条；「生命周期与传输」的 stdio 一条新增单行上限与「解码之后照共同检查处理」两项。
  - RFC-0001「JSON 文本」：XiHan 读取的 JSON 文本加上 MCP 的 JSON-RPC 消息；状态行与「对应 ADR」已改。
  - 向量、错误码与消息键不变。
- .NET（`Protocols.Mcp`，`Preview`）：
  - `McpProtocolOptions` 新增 `MaxStdioLineBytes` 与 `DefaultMaxStdioLineBytes`，配置键 `XiHan:Protocols:Mcp:MaxStdioLineBytes`。
  - `McpStdioTransport` 的读取器按上限丢弃超长的一行；`McpServer` 处理每条消息之前先过 `JsonText.FindProblem`。
  - 命令行工具的配置索引（`Cli/Knowledge/configuration.json`）补上新键。
- 修订 ADR-0320 备选 20，末尾加一节。Kernel 与 Primitives 没有改动。

## 日期

2026-11-27

## 背景

仓库所有者对 MCP 拍板了两件：

1. **stdio 的一行不设上限**（ADR-0320 备选 20）：`McpStdioTransport` 按字节分行，一行整个拼进 `ArrayBufferWriter`，没有行尾的输入一直拼下去。对端是启动这个进程的本机客户端，但客户端的缺陷或转发的内容照样能让服务端吃光内存；HTTP 入口早有 `MaxRequestBodyBytes`（ADR-0199）。
2. **Streamable HTTP 入口与 JSON 文本的共同检查**：这一轮先实测了正文的几种坏法（stdio 一并实测，两种传输都交给 `McpServer.HandleAsync`，那里只调 `JsonDocument.Parse`）：

| 正文 | HTTP（修之前） | stdio（修之前） |
| --- | --- | --- |
| `arguments` 的字符串里有非法的 UTF-8（`ED A0 80`），或 `method` 里有 `FF` | 500 `KERNEL_UNEXPECTED_001`（Problem Details） | -32700（传输先过 `Utf8.IsValid`，ADR-0320） |
| 字符串之外的 `FF`、BOM、尾逗号、两个根值、空正文 | -32700，400 | -32700（BOM 只在整个输入的开头去掉；空行跳过） |
| `method` 或 `id` 里转义出的孤立代理项（`"\ud800"`） | 500 `KERNEL_UNEXPECTED_001` | `InvalidOperationException` 抛出 `RunAsync`，会话异常结束，后面的行都不处理 |
| `arguments` 里转义出的孤立代理项 | 200，`isError`：`KERNEL_VALIDATION_001`，位置 `$` | 同左 |
| 重复的 `id`、`method` 或 `params.name` | 200，取最后一个照常执行（`"name"` 重复时执行的是后一个 tool） | 同左 |
| `arguments` 里重复的成员 | 200，`isError`：`KERNEL_VALIDATION_001`，位置 `$.id` | 同左 |
| 嵌套 65 层（消息对象是第 1 层） | -32700，400（`JsonDocument` 的缺省深度 64） | -32700 |
| 嵌套 64 层 | 照常执行 | 照常执行 |

System.Text.Json 不核对字符串里的 UTF-8，也不拒绝转义出的孤立代理项，要到取字符串（`GetString`）时才抛出；重复的成员它取最后一个。只有「超过 64 层」与 RFC-0001「JSON 文本」本来一致。

## 备选

stdio 的单行上限：

1. **可配置的字节上限，放进 `McpProtocolOptions`**：采纳。
   - 配置键 `MaxStdioLineBytes`：照 MCP 已有的 `MaxRequestBodyBytes`、存储的 `MaxBlobBytes` 等 `Max<对象>Bytes` 的命名；名字里带 `Stdio`，与只管 HTTP 入口的 `MaxRequestBodyBytes` 分开。
   - 类型 `int`：一行缓冲在内存里，缓冲区的容量就是 `int`；配置值超过 `int.MaxValue` 在读配置时就报错（`ConfigurationValues.ReadInt32`），不是到用时才溢出。`MaxRequestBodyBytes` 是 `long`，但读出的正文同样进一个数组。
   - 缺省 4194304（4 MiB），与 `DefaultMaxRequestBodyBytes` 相同，也与 HTTP 操作端点的缺省请求体上限、gRPC 缺省的接收上限相同：一条消息经 HTTP 收得下，经 stdio 也收得下，换传输不会突然读不进。
2. **缺省不设上限，配置了才限**：否决。不设上限正是要修的问题；4 MiB 对一条 JSON-RPC 消息已经很宽。
3. **用 `MaxRequestBodyBytes` 同时管 stdio**：否决。两者的对端与风险不同，`MaxRequestBodyBytes` 的文档与规范都写明只管 HTTP 入口，调大一个不该连带另一个。
4. **长度怎么算**：行尾不算，整个输入开头的 UTF-8 BOM 不算，与「一行的内容」同一个口径；刚好等于上限的一行照常处理。BOM 要到一行结束才认得出（可能分在两次读入里），第一行缓冲时多留三个字节，交出时按去掉 BOM 之后的长度再核对。
5. **超长的一行怎么办：读到行尾为止丢弃，答复解析错误，会话继续**：采纳，照所有者的决定，与非法的 UTF-8 同一条路（-32700，`PROTOCOLS_VALIDATION_001`，`id` 为 `null`）。
   - 写之前先比余量：一块最多 4 KiB，先写再比会多缓冲一块；超过即丢弃已缓冲的部分，其余的字节只找行尾，不再缓冲。
   - 超长的那一行里的换行之前的内容不当作消息：丢弃到行尾就是丢弃到这一行的行尾，CR、LF、CR LF 都认。
   - 输入在超长的一行里结束：同样答复一次再结束会话，与「最后一行可以没有行尾」一致。
6. **超长即结束会话**：否决。一行坏了不连累整个会话，这是 ADR-0320 按字节分行的理由；丢弃到行尾之后读取器的状态是确定的，接得回下一行。
7. **新的错误码或 HTTP 那样的 `PROTOCOLS_VALIDATION_016`**：否决，照所有者的决定走同一条路。stdio 没有状态码，客户端看到的是「这一行读不进」，与非法的 UTF-8 是同一种结果。
8. **取消**：每读一块之前核对取消令牌，取消时抛出 `OperationCanceledException`：采纳。丢弃超长的一行可能要读很多块，控制台的标准输入不认取消令牌，不在块与块之间核对就停不下来。两条消息之间取消仍照旧正常返回。

两种传输的 JSON 文本检查：

9. **在 `McpServer.HandleAsync` 里整条过 `JsonText.FindProblem`**：采纳。
   - 两种传输都经这里，一处改动两边一致；stdio 原有的 `Utf8.IsValid` 照旧先做（判空白要按字符解码），合法的 UTF-8 之后的其余几类由这一道查。
   - 多读一遍消息，相对一次调用可以忽略。
10. **没过即上表第一行（-32700，`PROTOCOLS_VALIDATION_001`，`id` 为 `null`，HTTP 400）**：采纳。
    - RFC-0001 把这几类都定为「不合法的 JSON 文本」，与语法错误同一个结论；stdio 的非法 UTF-8 已经是这个答复（ADR-0320），超过 64 层本来就是这个答复，实测与规范在这两处一致，其余照它们。
    - 不从没过检查的文本里取 `id`：重复的 `id` 取哪一个都不对，孤立代理项的 `id` 写不进答复。
11. **重复的成员答 -32600（Invalid Request）**：否决。JSON-RPC 的 -32600 是「JSON 合法但不是请求对象」；按 RFC-0001 带重复成员的文本就不是合法的 JSON 文本，与语法错误分开答只会让客户端多认一种情形。
12. **只查信封，`arguments` 照旧交给 Validate 阶段**：否决。`arguments` 是消息的一部分，消息整条不合法就不进入 JSON-RPC 的处理；要只查信封就得先解析出 `arguments` 的边界，那正是这道检查要先于它的东西。代价是 `arguments` 里的问题不再带位置（以前 `isError` 的文案里有 `$.id`），记在「影响」里。
13. **只修 500 与会话异常结束（改成接住 `InvalidOperationException`）**：否决。重复成员取最后一个照常执行（`"name"` 重复时执行了另一个 tool）同样是读成另一份消息，要的是同一道检查。

其他：

14. **加向量**：否决，理由同 ADR-0312 备选 13 与 ADR-0320 备选 25：MCP 的映射在 `host` 档，只有 .NET 实现；孤立的代理项与非法的字节写不进 JSON 向量。
15. **规范的档位**：stdio 的单行上限与共同检查都是写明以前不作规定、对 .NET 是收窄输入的行为，记 MAJOR（`v1.0.0` 之前允许）：合规的 MCP 客户端发的是合法的 JSON 文本、一行远小于 4 MiB，不受影响；以前读得进的重复成员与 `arguments` 里的孤立代理项现在读不进。

## 决策

1. **规范**：
   - `protocol/mcp.md`：两种传输交来的每条消息先整条过 RFC-0001「JSON 文本」的检查，没过即 -32700 / `PROTOCOLS_VALIDATION_001` / `id` 为 `null` / HTTP 400，不进入 JSON-RPC 的处理；stdio 一行不超过 `XiHan:Protocols:Mcp:MaxStdioLineBytes`（缺省 4 MiB，行尾与开头的 BOM 不算），超过的一行读到行尾丢弃、同一个解析错误、会话继续。
   - RFC-0001「JSON 文本」：读取的 JSON 文本加上 MCP 的 JSON-RPC 消息。
2. **.NET**：
   - `McpProtocolOptions.MaxStdioLineBytes`（`int`，正整数，缺省 `DefaultMaxStdioLineBytes` = 4194304），`FromConfiguration` 读 `MaxStdioLineBytes`，不是 32 位的正整数时抛出 `ArgumentException`、消息带配置键。
   - `McpStdioTransport`：读取器按上限丢弃超长的一行，交出「超长」时传输答复 `NotJsonReply` 并继续；每读一块之前核对取消。上限取自服务端的选项（`McpServer.Options`，internal），`RunAsync` 的签名不变。
   - `McpServer.HandleAsync`：先 `JsonText.FindProblem`，有问题即 `NotJsonReply`。

## 理由

- 单行上限照 HTTP 入口已有的请求体上限，两个传输都有界；缺省值相同，换传输不改变能收的消息。
- 读不进的一行与非法的 UTF-8 走同一条路，客户端只需要认一种答复，会话不因一行坏了而结束。
- 共同检查放在两种传输共用的那一处，规则与 RFC-0001 一致：读进来的 JSON 文本只有一种合法的读法，不会因为解析器的取舍读成另一份消息，也不会把调用方的输入问题报成服务端的 500。

## 影响

- **公开 API**（`PublicAPI.Unshipped.txt` 已同步）：新增 `McpProtocolOptions.MaxStdioLineBytes`（get / init）与常量 `DefaultMaxStdioLineBytes`；`McpServer.Options` 是 internal。没有新的错误码与消息键。
- **配置键**：新增 `XiHan:Protocols:Mcp:MaxStdioLineBytes`（正整数，最大 2147483647，缺省 4194304）；`Cli/Knowledge/configuration.json` 已补。
- **行为变化（更新日志，升级须知）**：
  - `mcp` 命令（应用的与命令行工具的）：stdin 的一行超过 4 MiB（或配置的上限）时答复解析错误（-32700）、这一行不执行，接着读下一行；以前整行读进内存照常处理。要收更长的消息，配大 `MaxStdioLineBytes`。
  - MCP 两种传输：消息里有任何一层重复的成员时答复解析错误（-32700，HTTP 400），不执行；以前取最后一个照常执行。
  - MCP 两种传输：`arguments` 里转义出的孤立代理项与重复的成员答复解析错误（-32700，HTTP 400）；以前是 `tools/call` 的 `isError` 结果（`KERNEL_VALIDATION_001`，带位置）。
  - Streamable HTTP：字符串里非法的 UTF-8、`method` 与 `id` 里转义出的孤立代理项答复解析错误（400）；以前是 500 `KERNEL_UNEXPECTED_001`。stdio：`method` 与 `id` 里转义出的孤立代理项答复解析错误、接着读下一行；以前 `RunAsync` 抛出 `InvalidOperationException`，会话异常结束。
  - 取消在读一行的中途到达时，`McpStdioTransport.RunAsync` 抛出 `OperationCanceledException`（每读一块之前核对）；以前要看输入流自己认不认取消令牌。
- **规范**（规范工作区已提交）：`protocol/mcp.md`「操作映射」与「生命周期与传输」、RFC-0001「JSON 文本」与状态行，MAJOR（`v1.0.0` 之前允许）。
- **Go 一致性模块**：不变。
- **六个 SDK**：不用跟。SDK 不实现 MCP 的协议映射。
- **测试**：
  - `McpStdioTests`：刚好等于上限的一行照常、多一个字节答复解析错误、下一行照常；超长的一行（分好几块读入、以单独的 CR 结束）读到行尾丢弃、其中像消息的内容不执行、下一行照常，输入在超长的一行里结束同样答复一次；开头的 BOM 不算进长度；没有行尾的无尽输入在取消之后停下、不再读（输入流不认取消令牌）；`MaxStdioLineBytes` 的配置不是 32 位的正整数时报错、消息带配置键，缺省值；`method` / `id` 里的孤立代理项、重复的 `id`、`arguments` 里的重复成员与孤立代理项都答复解析错误、会话继续、没有执行 tool。
  - `McpProtocolTests`：九种正文（字符串里非法的 UTF-8 两处、孤立代理项三处、重复成员三处、65 层）都是 400 的解析错误、`id` 为 `null`、没有执行 tool；64 层、本来就是 U+FFFD 的字符与代理对照常执行。
  - 改实现之前 13 条失败（65 层与新增的通过用例除外）。
- **文档**：`Protocols.Mcp` 的 README 已同步；文档站与更新日志待改。
