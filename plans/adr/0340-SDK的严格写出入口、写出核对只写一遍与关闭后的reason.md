# ADR-0340 SDK 的严格写出入口、写出核对只写一遍与关闭后的 reason

## 状态

已采纳（只改六个 SDK 中的五个与它们的 `CHANGELOG.md`；规范与框架代码不改。TypeScript 新增 `Result.write` / `ErrorInfo.write`，Java 新增 `PrimitivesJson.write`，Rust 的 `json::to_vec` 写出后过共同检查，Python 响应类型的同名成员注册即报错，Java 关闭之后的 `reason` 与 TypeScript、Python、PHP 一致；Go、TypeScript、Rust 删掉与 `sdk-envelope-times.json` 重叠的手写用例）。

## 日期

2026-11-27

## 背景

第九轮各 SDK 交回、记进 `plans/待办-边角问题.md`「SDK」一节的几条，第十轮由仓库所有者逐项拍板：

- **TypeScript 没有严格写出 `Result` 的入口**：ADR-0320 让 `Result` / `Error` 的线上编码写不出即报错（RFC-0001「写出与转交」），TypeScript 只在文档里写明用带 `reject` 选项的 `writeJson`，`JSON.stringify(result)` 与缺省的 `writeJson` 照旧宽松。
- **Java 写不出时抛 `JsonMappingException`**：`PrimitivesModule` 的序列化器写不出时抛 Jackson 的 `JsonMappingException`，.NET 是 `ArgumentException`；另经一次 `TokenBuffer` 缓冲、多一遍写出。
- **Rust 的 `json::to_vec` 不查 64 层与重复成员**：`Host::invoke_json` 与 `with_json_data` 经它写出；`WireResult` / `Error` 每次顶层写出都先用 serde_json 在缓冲里多写一遍再核对，调用器的 `encode` 在 `to_vec` 之后又跑一次 `json::check`。
- **Python dataclass 同名成员的报错时机**：两个字段的 `json_field` 同名，作请求类型时注册即抛 `TypeError`（ADR-0319），作响应类型要到调用时才以 `KERNEL_UNEXPECTED_001` 结束。
- **关闭之后的 `reason` 各家不统一**：`plugin/invocation.md` §5 规定释放回调客户端之后的回调结果是 `plugins.host.unavailable`（ADR-0328），不规定 `reason` 的文字；Java 是 `Unavailable: the plugin runtime has stopped`，TypeScript、Python、PHP 是 `Unavailable: the host client is closed`。
- **手写的信封 `time` 用例与向量重叠**：ADR-0306 时手写的用例（Go `writtenTimes` 13 条、TypeScript 10 条、Rust `WRITTEN` 13 条）与 ADR-0310 的 `plugin/sdk-envelope-times.json` 部分重叠；第九轮只精简了 Java、Python、PHP。

## 备选

1. **TypeScript 的入口**：
   - 改缺省的 `writeJson` 为严格：否决，`writeJson` 是通用的 JSON 写出器，宽松是它的约定，别处的调用会跟着变。
   - 新增 `Result.write(result)`（采纳），与 `Result.parse` 对称；顺带加 `ErrorInfo.write(error)`，与 .NET 的 `ResultJson.WriteError`、Go 的 `(*Error).MarshalJSON`、Java 的入口对齐。
   - 写不出时抛 `TypeError`：否决；采纳抛 `FrameworkError`（`PRIMITIVES_VALIDATION_001`），`cause` 是 `writeJson` 抛的 `TypeError`，与 Go 返回同码的 `FrameworkError` 一致。
   - 非有限的浮点：照 RFC-0001「Result」写成字符串 `"NaN"` / `"Infinity"`，不抛出（与 Go、TypeScript 原来的 `reject` 写法一致；第十轮简报把它列成写不出是笔误）。
2. **Java 的异常**：
   - 让 `mapper.writeValueAsString(result)` 直接抛 `IllegalArgumentException`：做不到，Jackson 总把序列化器抛的运行时异常包成 `JsonMappingException`。
   - 新增 SDK 自己的写出入口 `PrimitivesJson.write(ObjectMapper, Result<?>)` / `write(ObjectMapper, Error)`（采纳），在入口解开再抛；序列化器本身改抛 `IllegalArgumentException`，直接用 `ObjectMapper` 写出时得到的是 cause 为它的 `JsonMappingException`。
   - `TokenBuffer` 多一遍写出：照旧接受（所有者定的）。
3. **Rust 的核对放在哪**：
   - 每个 `WireResult` / `Error` 的 `Serialize` 各自核对（原做法）：嵌在别的值里时每层都多写一遍。
   - `json::to_vec` 写完对整段文本跑 `json::check`（采纳）：写的过程中设线程本地标记，嵌在里面的 `WireResult` / `Error` 不再在缓冲里多写一遍；经别的序列化器（`serde_json::to_vec`）写时照旧先核对，结果不变。层数从整个值的根算，与调用器编码响应的口径一致。
4. **Python 响应类型的核对范围**：沿注解递归，只核对其中的 dataclass（字段、序列元素、映射值、`Maybe`、`Result`、联合、`Annotated`、类型别名）；注解解析不了（`NameError`）时只核对那一层，不为响应类型新增注册失败，嵌套的值写出时 `encode` 照样核对。.NET 只读核对的结论：请求、响应及嵌套类型的 JSON 名冲突，在取源生成的 `JsonTypeInfo`（`AddOperation` 的实参）时即抛 `InvalidOperationException`，也是启动时报错。
5. **关闭之后的 `reason`**：写进规范：否决，规范只规定结果是 `plugins.host.unavailable`，文字留给实现。采纳：Java 改成另三家的 `Unavailable: the host client is closed`——Java 在 `PluginRuntime.release()` 里调 `Host.close()`，之后 `channel()` 为空，与 §5「SDK 释放了回调客户端之后的回调」是同一种情形。
6. **手写时间用例**：照第九轮 Java、Python、PHP 的标准，向量覆盖到同一类情形就删，向量表达不了的留下（Go 的本机时区与单调时钟读数；TypeScript 的本机时区、越界年份、Invalid Date）；原来借手写用例做的「写出的文本读回同一时刻」挪进 `sdk-envelope-times.json` 回放的 writing 循环。Rust 在 Windows 上 `SystemTime` 只有 100 纳秒精度，直接删会让本机不再核对纳秒的写法，所以改在 `UtcTime` 一层回放 writing 的纳秒一列。

## 决策

1. **TypeScript**：新增 `Result.write(result: Result<unknown>): string` 与 `ErrorInfo.write(error: ErrorInfo): string`。写得出时与 `writeJson(result)` 逐字相同；有孤立代理项、从结果的根算起超过 64 层、重复成员时抛 `FrameworkError`（`PRIMITIVES_VALIDATION_001`，`cause` 为 `TypeError`）；另查有值的成功写不出值（函数、symbol、`toJSON` 交出 `undefined`）与非 `Maybe` 的值写成 `null`。`JSON.stringify` 与缺省的 `writeJson` 不变，README 的示例改用 `Result.write`。
2. **Java**：新增 `PrimitivesJson.write(ObjectMapper, Result<?>)` 与 `write(ObjectMapper, Error)`，返回 `String`；写不出抛 `IllegalArgumentException`，消息取原因链里第一个 `IllegalArgumentException` 的（最多看 16 层，否则固定文字），Jackson 的异常留作 cause。
3. **Rust**：`json::to_vec` 写完过 `json::check`，没过返回 `PRIMITIVES_VALIDATION_001` 的错误、不交出文本；经它写出时 `WireResult` / `Error` 只写一遍；调用器的 `encode` 去掉重复的检查。
4. **Python**：`Descriptor` 对响应类型也调 `check_json_names`，同名字段注册即抛 `TypeError`（消息同 ADR-0319），什么也不登记。
5. **`reason`**：Java 关闭之后的回调 `reason` 改为 `Unavailable: the host client is closed`；TypeScript、Python、PHP 不变；规范不改。
6. **时间用例**：Go、TypeScript、Rust 删掉与 `sdk-envelope-times.json` 重叠的手写用例（13、10、13 条），各自保留向量表达不了的；读回同一时刻的核对挪进向量回放。

## 理由

- 线上编码要交给对方读，写不出就不能交出文本（ADR-0320）；把严格写出做成 SDK 自己的入口，作者不必记住一组 `reject` 选项，各语言也有了与 .NET `ResultJson.Write` 对得上的一处。
- 异常类型对齐 .NET 的 `ArgumentException`：写不出是调用方交了写不成的值，属于编程错误，不该带着序列化库的异常类型。
- Rust 把核对收到整段文本一次，结果与逐个值核对相同（层数从更外的根算只会更多，重复成员与转义在整段文本里同样看得到），省掉嵌套时一层一遍的重复写出。
- 同名成员在注册时报，与 .NET 启动时报一致，比调用时才以内部错误结束早得多、也容易定位。
- `reason` 是排障时看到的文字，四家同一种情形说同一句话；它不是契约，不必进规范。

## 影响

- **TypeScript**：新增公开面 `Result.write`、`ErrorInfo.write`；从 rc.1 升级时把交给对方读的线上编码换成这两个入口，捕获 `TypeError` 的地方改为捕获 `FrameworkError`。
- **Java**：新增公开面 `PrimitivesJson.write`；序列化器写不出改抛 `IllegalArgumentException`（直接用 `ObjectMapper` 时是 cause 为它的 `JsonMappingException`）；按旧 `reason` 文字判断的地方要改。
- **Rust**：`json::to_vec` 写出过深、有重复成员或 `RawValue` 里有孤立代理项转义的值时返回错误（相对 rc.1 是本周期新增的 API，不算破坏）；结果 64 层外面再包一层结构体时，`json::to_vec` 写不出而 `serde_json::to_vec` 照写；顶层 `WireResult` 写不出时消息由 `the result cannot be written…` 变成 `the value cannot be written…`，错误码不变。
- **Python**：以同名字段的 dataclass（含嵌套）为响应类型注册操作抛 `TypeError`。
- **Go**：只删手写用例，行为不变。
- **PHP**：不涉及（`reason` 本来一致）。
- **文档**：各 SDK 的 `CHANGELOG.md`「未发布」与框架文档站 `docs/sdks/*.md` 已改。

## 没有解决的

- Go 没有自己的「已关闭」状态：`Serve` 退出时关掉连接，之后的回调由 grpc-go 报出，`reason` 是 `Cancelled: grpc: the client connection is closing`（结果仍是 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable`），与另四家的文字不同，这次没改。
- 插件回调宿主时 SDK 写进元数据的 Baggage 遇到孤立代理项怎样，各语言没核对，规范也没写（ADR-0334「没有解决的」）。
