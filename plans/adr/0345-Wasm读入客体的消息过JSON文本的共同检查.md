# ADR-0345 Wasm 读入客体的消息过 JSON 文本的共同检查

## 状态

已采纳（规范 `plugin/wasm.md` §2 新增一条，RFC-0001「JSON 文本」的读取范围与状态行、RFC-0018 的状态行已改；本仓库 `Plugins.Protocol` 的 `PluginJsonCodec.Decode`（internal）实现；公开面、错误码、消息键与配置键不变；向量不变）。

## 日期

2026-11-27

## 背景

- ADR-0312 让 Wasm 宿主按严格的 UTF-8 解码客体写来的消息，非法的字节序列与不是合法 proto3 JSON 的消息同样处理；ADR-0334 顺带补了用例：中间件 `before` 改写的 Baggage 里转义写出的孤立代理项读不出。整条文本是否还要过 RFC-0001「JSON 文本」的共同检查（孤立代理项的转义、重复成员、64 层），规范没有写。
- 进程外插件写来不合法的 UTF-8 时，Google.Protobuf 读不进整条消息（`InvalidProtocolBufferException`）；Wasm 的 proto3 JSON 解析器对不合共同检查的文本各有取舍。
- 仓库所有者拍板：整条消息不合法。Wasm 读入客体写来的每条消息过共同检查，没过按客体协议错误处理，与进程外读不进同一处置，用现有的「客体消息不合法」的路径与错误码，不新造。

## 实测（.NET，改之前）

用例在现场生成的 WAT 插件里写出这样的原文（`WasmMessageJsonTextTests`），另在 `PluginJsonCodecTests` 里直接解码：

| 写法 | 位置 | 改之前 |
| --- | --- | --- |
| 孤立代理项的转义（`\ud800`、`\udc00`、颠倒的一对） | 已知字段、未知字段的值与成员名、映射的键；操作应答的 `text`、回调的 `operation`、发布信封的 `extensions`、中间件钩子应答的 `baggage` | 已经读不出：Google.Protobuf 的 JSON 分词器遇到不成对的代理项即抛出 `InvalidJsonException`（未知字段的值照样分词），`Decode` 得到空 |
| 重复的成员 | 同一层的已知字段（`status`、`operation`、`action`、`envelopes`） | 照收：后一个覆盖前一个（重复的数组按合并读入，发布了两条）。操作应答先写失败、后写成功的读成成功；回调先写 `wat.missing`、后写 `pings.create` 的调的是后者 |
| 重复的成员 | 映射的键（`baggage`）、未知字段里的对象 | 照收：映射后一个覆盖前一个；未知字段里的不看 |
| 嵌套超过 64 层 | 未知字段的值（64 层数组即第 65 层） | 照收：跳过未知字段时不计层数 |
| 嵌套超过 64 层 | 已知字段（`status` → `error` → 62 层 `inner`，第 65 层） | 照收：到 Google.Protobuf 自己的递归上限为止 |

改之前各处的处置（由同一个 `Decode` 返回空引出，ADR-0312 已有）：

- **导出函数的响应**（握手、生命周期、健康、操作、事件、中间件钩子）：`WasmPluginRuntime.CallAsync` 得到 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`（`phase` 为导出函数名，`reason` 为 `the response is not a valid JSON message`），在后台按崩溃处理、退避重建实例；启动与发现阶段由各自的路径处理（`HandleCrashAsync` 在 Declared / Resolved / Configured 状态下不重复处理）。中间件钩子失败即关闭，调用以这个错误结束。
- **导入函数的请求**（`invoke`、`publish`）：`WasmHostFunctions` 以 `PLUGINS_VALIDATION_005` + `plugins.module.invalid` 应答（`reason` 为 `xihan.invoke received an invalid InvokeRequest` / `xihan.publish received an invalid PublishRequest`，发布的结果只有一项），不陷阱、实例照常服务，只让这次回调失败。

## 备选

1. **在 `PluginJsonCodec.Decode` 里先过 `JsonText.FindProblem`**：采纳。两个方向的调用方都只看 `Decode` 是否为空，现有的处置原样沿用；一致性套件（`WasmPluginConformance`）读插件的应答也经它，跟着从严。
2. **在调用方（`CallAsync`、导入函数）各自检查**：否决。三处各写一份，与「判定只有一份」的做法不符（ADR-0334）。
3. **另给没过检查的消息一个 `reason`（如 `the response is not valid JSON text at '<路径>'`，与应答载荷的那一条一样，ADR-0315）**：否决。所有者定了用现有的路径与错误码；ADR-0312 备选 5 也是同一个理由：`Decode` 的契约是「不是合法的消息返回空」，非法的 UTF-8 没有另起 `reason`，这里同样不另起。载荷的检查给路径，是因为载荷是插件交给调用方的数据，消息本身合法；这里是整条消息不合法。
4. **只检查已知字段**：否决。共同检查是整条文本的，ADR-0312 的非法 UTF-8 也算未知字段里的；未知字段里的重复成员与深嵌套，换一个按更新的消息定义读的读者就成了已知字段。
5. **重复成员只要值相同就放过**：否决。RFC-0001 的规则不看值，各语言的 JSON 读取器也不比较值。
6. **用内核的 `JsonText`**：它是公开的 Stable 类型（ADR-0283），`Plugins.Protocol` 本来就引用内核，不需要改内核的可见性；原语与内核的程序集不变。

## 决策

1. **规范**（`plugin/wasm.md` §2，MINOR，写明以前不作规定的行为）：客体写来的每条消息整条过 RFC-0001「JSON 文本」的共同检查（孤立代理项的转义、任何一层的重复成员、64 层，未知字段的值也算）；没过的与不是合法 proto3 JSON 的消息、与有非法 UTF-8 的消息同一处置，整条消息不合法，不挑出读得出的字段。写明两个方向的处置：导出函数的响应是这次调用的 `PLUGINS_UNEXPECTED_001`（`phase` 为导出函数名、`reason` 为 `the response is not a valid JSON message`），运行期按崩溃处理、退避重建，中间件钩子失败即关闭；导入函数的请求只让这次回调失败（`PLUGINS_VALIDATION_005`，两种 `reason`），不陷阱。RFC-0001「JSON 文本」的读取范围加上 Wasm 的协议消息，状态行同日第五次写明；RFC-0018 状态行同日再写明。`invocation.md` 不改：§3「读入 `Error`」说的是错误各字段的读法，整条消息的读法在 `wasm.md` §2。
2. **实现**（`Plugins.Protocol`）：`PluginJsonCodec.Decode` 先以 `JsonText.FindProblem` 检查字节，有问题即返回空，再照旧严格解码、解析。
3. **测试**：
   - `PluginJsonCodecTests`：孤立代理项的转义在已知字段、未知字段的值与成员名、映射的键里都读不出（改之前就读不出，钉住）；四种重复成员（已知字段、未知字段、映射的键、转义还原后同名）读不出；未知字段第 64 层读得出、第 65 层读不出，`inner` 套到第 64 层读得出、第 65 层读不出。改之前重复成员与第 65 层的五条失败。
   - `Plugins.Wasm.Tests` 新增 `WasmMessageJsonTextTests`（夹具 `WatPlugin` 加 `WithMiddlewareAnswering` 与 `Custom` 的 `permissions` 参数）：操作应答、回调请求、发布请求与中间件钩子的应答各写孤立代理项的转义与重复成员，操作应答另写第 65 层；应答是 `PLUGINS_UNEXPECTED_001`（`phase`、`reason` 照上），回调是 `PLUGINS_VALIDATION_005` 且实例 Ready、没有重启，发布一条都不发、实例照常，中间件失败即关闭。改之前孤立代理项的四条通过，其余七条失败。
4. `Plugins.Wasm` 的 README 补一句。

## 理由

- 同一条消息，宽松的读者与严格的读者读出不同的字段：重复的 `status` 先写失败、后写成功，.NET 读成成功，按「前一个」或拒收的读者读成失败或不合法。共同检查就是为了让 XiHan 的各个读者对同一段文本只有一种读法（ADR-0236），协议消息不该是例外。
- 处置不新造：读不出的消息本来就有两条现成的路径（ADR-0312），共同检查只是让「读不出」的范围与 RFC-0001 一致；进程外的 protobuf 消息读不进也是整条丢掉。
- 判定只有一份（内核的 `JsonText`），放在两个方向共用的解码处。

## 影响

- **行为**（`Plugins.Wasm`，Experimental）：客体写来的消息有重复成员（任何一层，未知字段里的也算）或嵌套超过 64 层时，导出函数的响应按读不出处理（`PLUGINS_UNEXPECTED_001`，运行期实例重建），导入函数的请求以 `PLUGINS_VALIDATION_005` 应答；以前后一个覆盖前一个、照常读入。孤立代理项的转义以前就读不出，结果不变。各语言 SDK 的 proto3 JSON 写出器不写重复成员，按 SDK 写的插件只有错误套了 62 层以上内部错误时受影响。
- **公开面**：不变；没有新的错误码、消息键与配置键。
- **程序集体积**：原语 103424 字节、内核 364544 字节（本工作区口径），都没有变。
- **规范**：`plugin/wasm.md` §2（MINOR）、RFC-0001「JSON 文本」的范围（MINOR）、RFC-0001 与 RFC-0018 的状态行。向量不变：协议消息的写法各语言的 proto3 JSON 读写器不同，读取检查本身由 `primitives/json-text.json` 覆盖。
- **六个 SDK**：不涉及（SDK 写的是进程外插件，读宿主的 protobuf 消息）。
