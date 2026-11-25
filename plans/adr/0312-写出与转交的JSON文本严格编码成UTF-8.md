# ADR-0312 写出与转交的 JSON 文本严格编码成 UTF-8

## 状态

已采纳（规范 RFC-0001「JSON 文本」新增「写出与转交」一段、`plugin/wasm.md` §2、`plugin/invocation.md` §5、`protocol/commandline.md`、RFC-0016「工具链」的 `xihan setup` 与 RFC-0001、RFC-0016、RFC-0017、RFC-0018 的状态行已改；`Plugins.Protocol` 的 `PluginJsonCodec`、`Hosting.CommandLine` 的 `invoke --json`、`Protocols.Mcp` 的 stdio 传输与 `Cli` 的 `xihan setup` 改用严格的 UTF-8；TypeScript 与 Java SDK 的同类改动由 SDK 一侧跟进；向量、档表、错误码与消息键不变）。

## 日期

2026-11-26

## 背景

ADR-0301 把 RFC-0001「JSON 文本」写成「以字符串交来的文本先严格编码成 UTF-8 再检查」，管的是读取一侧（`ResultJson.Read(string)`、`ManifestReader.Read(string)`、`PluginJson.ParseDetails`）。它的「待定」里留下两处写出与转交一侧的宽松编码，仓库所有者已拍板一并改：

1. **Wasm 宿主解码客体写来的字节**：`PluginJsonCodec.Decode` 用 `Encoding.UTF8.GetString`，非法的字节序列（截断的多字节序列、过长编码、编码成 UTF-8 的代理项）换成 U+FFFD。换出来的是一条合法的 proto3 JSON，宿主照常读入：插件写坏的错误文案读成带 U+FFFD 的文案，写坏的操作 ID 以另一个 ID 进回调。
2. **命令行 `invoke --json`**：`Encoding.UTF8.GetBytes` 把字符串里孤立的代理项换成 U+FFFD，载荷照常进管道，操作收到的是另一份合法的 JSON。

两种 SDK 也是这样：TypeScript 的 `Host.invoke` 收字符串载荷时经 `TextEncoder`（WHATWG 规定把孤立的代理项换成 U+FFFD），Java 的 `OperationRequest.of(String id, String json)` 用 `getBytes(UTF_8)`（换成 `?`）。

按任务在框架里再搜了一遍「把外部来的字符串或字节转成 JSON 文本、却用了宽松 UTF-8」的地方（`Encoding.UTF8.GetString/GetBytes`、`ByteString.CopyFromUtf8`、`File.ReadAllText`、`StreamReader`、`ReadAsStringAsync`），另外两处与上面同一类、失败语义现成：MCP stdio 传输把读取器交来的一行经 `Encoding.UTF8.GetBytes` 交给服务端，`xihan setup` 用 `File.ReadAllText` 读要合并的编码助手配置（换成 U+FFFD 之后合并、写回，用户文件里的那几个字节就被改掉了）。其余的见「影响」的「同类而没改的」。

## 备选

范围：

1. **只改任务点名的两处**：否决。任务要求能确认同一类的一并改；MCP stdio 与 `xihan setup` 都是「交来的 JSON 文本在字符串与字节之间转换」，失败语义现成（解析错误 -32700、`CLI_VALIDATION_006`），改法与那两处相同。
2. **把经 JSON 写出器写出的字符串值也算进来**（`Utf8JsonWriter` 与 `JsonSerializer` 把值里孤立的代理项写成 `�`，实测）：否决。那是 System.Text.Json 对所有字符串值的写法，牵动每一处写 JSON 的代码，交来的也不是一份 JSON 文本；RFC-0001 的新一段写明它不在这一条里，命令行字段选项的同类问题记为待定。
3. **规范只写 .NET 这几处**：否决。规则属于 RFC-0001「JSON 文本」，SDK 以字符串收载荷的接口是同一条规则的另一个使用方，规范要给出 SDK 的失败语义（`plugin/invocation.md` §5）。

各处的失败：

4. **Wasm：非法的字节序列与不是 JSON 同样处理**（`Decode` 返回空，调用方的现有语义：导出函数的响应按崩溃处理，`PLUGINS_UNEXPECTED_001` + `plugins.process.failed`，`reason` 为 `the response is not a valid JSON message`；导入函数的请求以 `PLUGINS_VALIDATION_005` + `plugins.module.invalid` 应答）：采纳。未知字段里的非法字节也算：整条文本先要是合法的 UTF-8，解析器才谈得上忽略未知字段。
5. **Wasm：另给非法 UTF-8 一个 `reason`**：否决。`Decode` 的契约一直是「不是合法的消息返回空」，非法的 UTF-8 本来就不是合法的 JSON（RFC 8259 §8.1），另起一个 `reason` 要改两处调用方与 `plugin-messages.json`，运营方的处置相同（插件写坏了消息）。
6. **`Encode` 不改**：否决。proto3 JSON 的写出器遇到孤立的代理项即抛出 `ArgumentException`（实测 Google.Protobuf 3.36.2），写出的文本里不会有它们，改成严格编码不改变行为；同一个类型两个方向用同一个编码，读的人不必再去确认写出一侧为什么宽松。
7. **命令行：孤立的代理项是用法错误**（`HOSTING_VALIDATION_001`，退出码 64，消息键沿用 `hosting.commandline.invoke_json_invalid`，`reason` 为 `the text has a lone surrogate and cannot be encoded as UTF-8`）：采纳。与不是 JSON 对象同一个处理，不发起调用；新登记消息键否决，`--json` 的那句文案对它成立。
8. **MCP stdio：读取器交来的一行编码不成时答复解析错误**（与不是 JSON 同一个答复：`id` 为 `null`、-32700、`data.code` 为 `PROTOCOLS_VALIDATION_001`），下一行照常处理：采纳。服务端新增 internal 的 `McpServer.NotJsonReply()`，`HandleAsync` 解析失败时用的是同一个。
9. **MCP stdio：标准输入的字节也严格解码**（`OpenInput` 的读取器现在把非法字节换成 U+FFFD）：这次不做，记为待定。`StreamReader` 的解码器抛出之后已读进缓冲的那一块字节就丢了，行的边界跟着丢；让解码器把非法字节换成一个能认出来的孤立代理项，.NET 不允许（`DecoderFallbackBuffer` 核对替换出来的字符，实测抛 `ArgumentException`）。剩下的写法是按字节分行，要改 `OpenInput` 与 `RunAsync` 的公开签名（`Preview`），留给所有者定。
10. **`xihan setup`：不是合法 UTF-8 的文件是不合法的 JSON**（`CLI_VALIDATION_006` + `cli.setup.config_invalid`，`reason` 为 `the file is not valid UTF-8`，一个文件都不写）：采纳。RFC-0016 早已写明「不是合法 JSON 对象时一个文件都不写」，这里补上它的一种情形。读法照 `File.ReadAllText` 按字节顺序标记识别编码（UTF-8 的 BOM 跳过），只换成严格的 UTF-8。

SDK：

11. **SDK 收字符串载荷时在本地失败，`KERNEL_VALIDATION_001` + `kernel.operation.invalid_payload`（`id` 为操作 ID，`reason` 为 `$`），不发请求**：采纳。与宿主读到不是 JSON 的载荷时的错误相同（RFC-0011 规则 2）；排在权限、租户与截止时间的本地核对之后，与宿主的先后一致（宿主在管道的 Validate 阶段才解码载荷）。
12. **SDK 抛出语言自己的编码异常**：否决。回调客户端的失败都以 `Error` 交给作者（`plugin/invocation.md` §5），异常要作者另外捕获，与宿主那一侧的结果也对不上。
13. **加向量**：否决。理由同 ADR-0301 备选 27：孤立的代理项写进 JSON 向量，各语言的 JSON 读取器读出来的输入不同；由各实现的单元测试核对。

## 决策

1. **RFC-0001「JSON 文本」新增一段「写出与转交」**：XiHan 在字符串与 UTF-8 字节之间转换一份 JSON 文本、再写出或转交时，转换是严格的，两个方向都不先换成 U+FFFD。以字符串给出、要变成字节的，孤立的代理项按那一处不合法的 JSON 处理；以字节收到、要先解码再解析的，非法的字节序列按不是 JSON 处理。值里的字符串经 JSON 写出器写出时不在这一条里。
2. **.NET 四处**：
   - `PluginJsonCodec.Decode`：严格解码，`DecoderFallbackException` 与解析失败一样返回空；`Encode` 同用严格的编码（`plugin/wasm.md` §2）。
   - `InvokeCommand`：`--json` 严格编码，编码不成为用法错误（`protocol/commandline.md`）。
   - `McpStdioTransport.RunAsync`：每一行严格编码，编码不成答复解析错误、继续下一行。
   - `SetupPlanner.ReadConfig`：严格解码，非法的 UTF-8 为 `CLI_VALIDATION_006`、文件不动（RFC-0016「工具链」）。
3. **SDK**（`plugin/invocation.md` §5）：`Host.Invoke` 的载荷以字符串给出时，发出之前严格编码成 UTF-8；编码不成即不发请求，结果为 `KERNEL_VALIDATION_001` + `kernel.operation.invalid_payload`（`id` 为操作 ID，`reason` 为 `$`），排在权限、租户与截止时间的核对之后。载荷以字节给出时原样发出。

## 理由

- 换成 U+FFFD 得到的仍是一份合法的 JSON：读的一方看不出交来的文本是坏的，转交出去的已经是另一份文本；写回文件的地方连用户原来的字节也改掉了。与 ADR-0301 同一个理由，换到写出与转交一侧。
- 每一处都沿用那里已有的「不是合法 JSON」的失败，运营方与调用方看到的错误不因为坏在编码上而换一种。
- SDK 的失败照宿主的错误写，插件作者在本地拿到的与把同样坏的载荷交给宿主时一样。

## 影响

- **公开 API**：没有变化（`PluginJsonCodec`、`InvokeCommand`、`SetupPlanner` 是 internal，`McpServer.NotJsonReply` 是 internal 成员，`McpStdioTransport` 的签名不变）。没有新的错误码、消息键与配置键。
- **对已有部署与代码的行为变化（升级须知）**：
  - Wasm 插件写给宿主的消息有非法的 UTF-8 字节序列时，响应按崩溃处理（`PLUGINS_UNEXPECTED_001`，实例重建）、导入函数的请求以 `PLUGINS_VALIDATION_005` 应答；以前换成 U+FFFD 照常读入。各语言 SDK 的 proto3 JSON 写出器写的都是合法的 UTF-8，按 SDK 写的插件不受影响。
  - `invoke --json` 的文本有孤立的代理项时是用法错误（退出码 64）；以前换成 U+FFFD 照常调用。只在 Windows 上能出现：别的系统上命令行参数是字节，.NET 解码时已经换成 U+FFFD。
  - 交给 `McpStdioTransport.RunAsync` 的读取器给出有孤立代理项的一行时答复解析错误（-32700）；以前换成 U+FFFD 照常处理。`OpenInput` 读出的行不会有孤立的代理项，`mcp` 命令的行为不变。
  - `xihan setup` 要合并的配置不是合法的 UTF-8 时报 `CLI_VALIDATION_006`、一个文件都不写；以前换成 U+FFFD 合并后写回。
- **规范（规范工作区已提交）**：RFC-0001 状态行与「JSON 文本」的「写出与转交」一段；`plugin/wasm.md` §2 与 RFC-0018 状态行；`plugin/invocation.md` §5 插件一侧发出之前的核对与 RFC-0017 状态行；`protocol/commandline.md` 的载荷一条；RFC-0016 状态行与 `xihan setup` 一条。都是写明（MINOR）：以前不作规定，写死的是参考实现以外各方本来就该有的结果，不让两个按规范实现的对端互通失败；对按 TypeScript、Java SDK 现在的做法写字符串载荷的插件，结果从「换成 U+FFFD 照发」变成本地失败。向量不变（备选 13）。
- **Go 一致性模块**：不变（`rules.CheckJSONText` 读的是字节）。
- **六个 SDK**：TypeScript 与 Java 要改（报告与 SDK 一侧的 ADR 跟进）；Go 的 `[]byte(s)`、PHP 的字符串本来就是字节，不经编码转换，坏的字节原样交给宿主、由宿主拒绝；Rust 的 `&str` 总是合法的 UTF-8；Python 的 `str.encode("utf-8")` 缺省即严格，但抛出的是 `UnicodeEncodeError`，收 `str` 载荷的接口要按决策 3 换成那个错误。
- **同类而没改的**（报告第 8 条另列）：
  - MCP stdio 的 `OpenInput` 宽松解码标准输入（备选 9）。
  - 插件交回的响应载荷宿主不过「JSON 文本」的检查（`ProxyOperationDescriptor.DecodeResponse` 原样交回），之后 MCP 的 tool 结果文本与 `invoke` 的 stdout 用 `Encoding.UTF8.GetString` 宽松解码，gRPC 载体的 `ProtoEncoder` 把 `\ud800` 这样转义出的孤立代理项宽松编码进 protobuf 字符串字段。类型化操作的响应由 System.Text.Json 写出，总是合法的。
  - 进程外插件经 gRPC 收发的 protobuf `string` 字段由 Google.Protobuf 宽松编解码（C# 运行时不核对 UTF-8，Go 与 Java 的 protobuf 运行时拒绝），如 `Error.details` 的 JSON 文本。
  - 命令行字段选项（`FieldOptionPayload`）、插件的配置节（`PluginJson.SerializeSection`）等经 `Utf8JsonWriter` 写出的字符串值（备选 2）。
  - 工作流 `Http` 活动以 `Encoding.UTF8.GetString` 读任意 HTTP 正文（声明是 JSON 的再解析），Nacos Provider 以 `ReadAsStringAsync` 读第三方接口与配置内容，`xihan pack` 以 `File.ReadAllText` 读挂载的配置文件嵌进 ConfigMap：读的是第三方的或应用自己的文本，没有现成的「不是 JSON」的失败语义。
  - 一致性套件以 `ByteString.CopyFromUtf8` 编码作者给的样例载荷（`SampleRequests`）。
  - 日志文本（Wasm 的 `log` 导入、进程外插件的 stderr 行）与握手行（`handshake.md` 写明换成 U+FFFD）不是 JSON 文本的转换。
- **测试**：
  - `PluginJsonCodecTests`（新增）：截断、不能出现的字节、过长编码、编码成 UTF-8 的代理项、超出 U+10FFFF 的五种字节序列与未知字段里的非法字节都得到空，多字节文本照常读出；`Encode` 遇到孤立的代理项抛出，代理对写出合法的 UTF-8。改之前六条失败。
  - `WasmMessageUtf8Tests`（新增，端到端，现场生成的 WAT 插件写出这样的字节）：响应里有截断的序列时是 `PLUGINS_UNEXPECTED_001`、`reason` 为 `the response is not a valid JSON message`（改之前读出插件自己的 `ORDERS_VALIDATION_001`）；回调请求的操作 ID 里有编码成 UTF-8 的代理项时是 `PLUGINS_VALIDATION_005`（改之前进到权限核对，`PLUGINS_DENIED_002`）；合法的多字节文本照常读出。
  - `CommandLineRunnerTests`：`--json` 带孤立的代理项是用法错误、没有发起调用，代理对照常调用（改之前退出码 0）。`McpStdioTests`：读取器交来的一行带孤立的代理项时答复 -32700、`id` 为 `null`、没有执行操作，下一行照常答复。`SetupPlannerTests`：不是合法 UTF-8 的配置报 `CLI_VALIDATION_006`、`Apply` 失败且文件的字节不变，带 UTF-8 BOM 的照常读出。
- **文档**：`Hosting.CommandLine`、`Plugins.Wasm`、`Protocols.Mcp` 的 README 已同步；文档站的 `protocols/commandline`、`plugins/wasm`、`protocols/mcp`、`appendix/spec` 与更新日志待改，SDK 页随 SDK 的改动。
- **待定**：`OpenInput` 的严格解码（备选 9）；插件响应载荷的检查；经 JSON 写出器写出的字符串值（备选 2）。
