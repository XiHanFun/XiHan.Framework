# ADR-0289 测试用的向量读取与遥测收集收进 Testing

## 状态

已采纳（`Testing` 新增 `SpecVectors`、`FrameworkWorkspace`、`SpanCapture`、`SpanSamplingRequest`、`MetricCapture` 与 `MetricCaptureEntry`，`Preview`；测试项目里的副本全部删除，向量一律严格读取；`Security.WebAuthn` 的两个线上记录可空的末位参数带上缺省值；规范不变）。

## 日期

2026-11-24

## 背景

- 回放规范向量的测试各自写了「从测试输出目录向上找 `XiHan.Framework.slnx`，再拼 `spec/conformance/vectors/<目录>/<文件>`」，再按自己的选项反序列化：
  - 10 个测试项目各有一份 `ConformanceVectors.cs`（`AI.Abstractions`、`Auditing.Abstractions`、`Localization.Abstractions`、`Messaging.Abstractions`、`MultiTenancy.Abstractions`、`ObjectStorage.Abstractions`、`Persistence.Abstractions`、`Primitives`、`Scheduling.Abstractions`、`Workflow.Abstractions`），只差目录名与读法：4 份严格（多出与缺少成员都失败，去掉顶层 `$comment`），3 份只要求构造参数在，3 份 Web 缺省什么都不拦。`Kernel.Tests` 另有一份 `Fixtures/SpecVectors.cs`，Web 缺省。
  - 9 个测试类把同一段定位与读取写在类里（`CacheKeyVectorTests`、`SecretNameVectorTests`、`RemoteDocumentVectorTests`、`HttpDeadlineVectorTests`、`CatalogResolutionVectorTests`、`BlobStoreProviderNameVectorTests`、`HostUsageVectorTests`、`LinuxHostReadingVectorTests`、`WebAuthnVectorTests`），选项又是三种之一；插件宿主的两个夹具各有一个 `VectorsDirectory`，`RemoteCacheVectorTests` 从示例目录倒推 `../../spec`。
  - 只为找示例、插件构建产物或 `src/` 的另有 11 处同样的向上查找（`CacheGoPluginHost`、`EchoPluginHost`、`EchoGoPluginHost`、`EchoWasmPluginHost`、`SimpleAppProcess`、`Cli.Tests` 的 `TestWorkspace`、`PackageAssetTests`、`InProcessPluginHostTests`、`KubernetesAppSampleTests`、`SampleConfigurationTests`、架构测试的 `Workspace`）。连同上面的，31 个文件各写了一遍向上查找，找不到时的异常消息各写各的。
- 不严格的读法会把向量的变化放过去：成员改了名，模型读成空值或缺省值，「每条都核对」的循环对着空数组照样通过。改成严格之后，16 个测试里的模型与文件对不上：同一个文件由几个测试类分别回放时各自只写了自己那部分（`baggage-keys.json`、`plugin/deadlines.json`、`host-usage.json`、`store-definitions.json`），可以不出现的成员没有缺省值（`manifests.json`、`wire-encoding.json`、`json-text.json`、`message-templates.json`、`webauthn.json`），`payload-reasons.json` 的 `requests` 没人读；另有 `Security.WebAuthn` 的两个线上记录读不了省略了空值的 JSON（见备选 7）。都是模型的问题，期望值没有一处错。
- 要断言 Span 与指标的测试各自挂 `ActivityListener` / `MeterListener`：`Kernel.Tests` 的 `TelemetryRecorder`（连 `TelemetryMeasurement`），插件两个测试项目一字不差的 `SpanRecorder`，`HttpTracingTests`、`ObserveTracingTests`、`TraceParentPublishingTests` 里内嵌的监听器。都用无界的队列，都没有测试。监听器是进程级的，这些副本靠按 trace-id 取、绑定异步流或 `[Collection]` 串行化避免串数据，各自写了一遍。
- ADR-0262 把 20 份日志收集器收成了 `LogCapture`，这两类是同一个情形。

## 备选

1. **留在各测试项目**：否决。向量的定位与读取 20 份（11 个读取类、9 处类内）、向上查找 31 处、遥测监听 6 处，读法的严格程度三种并存；下一个回放向量的测试项目还会再抄一份，挑到哪种读法看运气。
2. **只统一定位、读法各自定**：否决。不严格的读法正是要去掉的：向量是跨语言的规范，参考实现回放时漏读一个字段，别的语言照着通过了也不算数。
3. **严格读取时对缺省值放宽**（只拦多出的成员，或不要求构造参数）：否决。改成严格后对不上的都靠在模型里补成员或给可以不出现的成员缺省值解决，没有一处需要放宽读法。
4. **同一文件由几个测试类分别回放时，合并成一个共享模型**：否决。几个类各自回放文件的不同部分、类型各在各的类里，合并要把十几个嵌套记录搬出来。改成各自的模型把别的类回放的成员写成 `JsonElement`：顶层每个成员都对得上，每一部分仍由一个类按类型回放。
5. **遥测收集换成 `Microsoft.Extensions.Diagnostics.Testing` 的 `MetricCollector<T>` 与 OpenTelemetry 的内存导出器**：否决。前者按 `Meter` 实例或仪表收，框架的仪表是静态的、并行的用例会串；后者要给测试项目加 OpenTelemetry 的依赖，`Testing` 也得跟着带。`ActivityListener` 与 `MeterListener` 在 BCL 里，`Testing` 不多引用任何包。
6. **Span 与指标合成一个收集器**（`TelemetryRecorder` 的形状）：否决。插件与宿主的测试只要 Span，`Kernel.Tests` 的两个都要；分成两个，各自的过滤（源名、Meter 名）与隔离方式（trace-id、异步流）也各管各的。
7. **测 `WebAuthn` 时另写一套线上记录、只为严格读取**：否决。`WebAuthnAttestationResponse.Transports` 与 `WebAuthnAssertionResponse.UserHandle` 写出时省略空值，读入时却因为构造参数没有缺省值，按构造参数严格绑定（.NET 10 的 `JsonSerializerOptions.Strict` 也这样）就读不了自己写出的 JSON。这是类型本身的问题，给这两个末位参数缺省值 `null`。

## 决策

- `XiHan.Framework.Testing` 新增（都是 `Preview`，与 `LogCapture` 相同）：
  - `FrameworkWorkspace`：`Root` 是从 `AppContext.BaseDirectory` 逐级向上第一个含 `XiHan.Framework.slnx` 的目录，第一次读取时找、之后缓存；找不到时抛 `InvalidOperationException`，消息写明找的文件与起点。
  - `SpecVectors`：
    - `Directory` 是 `<工作区>/spec/conformance/vectors`；`PathOf(area, fileName)` 只拼路径，目录名与文件名都必须是单独一段（空白、`.`、`..`、含 `/` 或 `\` 抛 `ArgumentException`），给按 `JsonDocument` 自己读或写回向量的测试用。
    - `Read<T>(area, fileName)` 按反射读（标 `RequiresUnreferencedCode` / `RequiresDynamicCode`，`Testing` 是 AOT 兼容的包）；`Read<T>(area, fileName, IJsonTypeInfoResolver)` 按源生成的上下文读，只取上下文的类型信息，选项照样是下面这套。
    - 读法一律严格：Web 缺省（camelCase、不区分大小写）、`UnmappedMemberHandling.Disallow`、`RespectRequiredConstructorParameters`、`AllowDuplicateProperties = false`，`required` 成员照 STJ 的规则必须出现；先按不许重名解析，去掉顶层的 `$comment` 再绑定，别处的 `$comment` 是向量的内容。反射与源生成两条路都拦多出的成员、缺少的构造参数与 `required` 成员（源生成下不加约束时缺了的 `init` 与构造参数会传进空值）。
    - 文件不存在抛 `FileNotFoundException`（提示检出规范子模块），内容为空、只有 `null`、顶层不是对象抛 `InvalidOperationException`，与模型对不上抛 `JsonException`，三者的消息都带文件路径。
  - `SpanCapture`：创建时登记一个只听给定源名的 `ActivityListener`，释放时移除。
    - 收开始的 Span（`Started`）、结束的 Span（`Stopped`）与采样器被询问的创建（`SamplingRequests`，元素 `SpanSamplingRequest`：源、名字、种类、trace-id、创建时的标签），都是快照。
    - `Spans(traceId[, name])` 取一条链路上结束的 Span。缺省采样同 OpenTelemetry 的 ParentBased(AlwaysOn)，`Sampler`（`init`）可以换。
  - `MetricCapture`：创建时启动一个只打开给定 Meter 名的仪表的 `MeterListener`，释放时停止。
    - 收全部数值类型的仪表，值换成 `double`；`Entries` 是快照，元素 `MetricCaptureEntry`：Meter、仪表、值、标签，重复的标签键取最后一个，不往被测组件里抛。
    - `RestrictToCurrentFlow()` 之后只收当前异步流及它派生的流里记下的，同一条流可以绑几个收集器，绑定不传回调用方。
  - 两个收集器都最多留 `Capacity` 条（`DefaultCapacity` = 10000，与 `LogCapture` 相同，构造时可给、必须为正），满了丢最早的；每样各算各的；多线程同时写入与读取是安全的；释放之后正在途中的回调也不再收，已经收到的仍可读取（`LogCapture` 释放后照样收，它没有监听要停）。
- 测试项目里的副本删除（16 个文件、23 处类内的定位或监听代码），调用点改用上面的类型：
  - 回放向量的一律 `SpecVectors.Read<T>("<目录>", "<文件>")`，按 `JsonDocument` 读的改用 `SpecVectors.PathOf`；只为找示例、构建产物与 `src/` 的改用 `FrameworkWorkspace.Root`，含架构测试的 `Workspace.Root`（架构测试本来就引用全部正式包）。
  - `TelemetryRecorder` 的用法拆成一个 `SpanCapture` 加一个限定到当前异步流的 `MetricCapture`，`SamplerSaw` 改成在 `SamplingRequests` 里按 trace-id、名字与标签找；`ObserveTracingTests` 的根规则留作 `Sampler`，`TraceParentPublishingTests` 的固定采样结果也是；挂监听器的用例原先所在的不并行集合（`TelemetryGroup`）照旧。
- 改成严格之后补齐的模型：同一文件由几个类分别回放的，各自把别的类回放的成员写成 `JsonElement`（并注明由谁回放）；文件里可以不出现的成员给缺省值，必要时把这些参数挪到末尾（`WebAuthnVectorTests` 的认证样例用 `JsonPropertyOrder` 保住写出的次序）；`payload-reasons.json` 的 `requests` 读进模型，并核对它列的请求类型与测试的描述一一对应。
- `Security.WebAuthn` 的 `WebAuthnAttestationResponse` 与 `WebAuthnAssertionResponse` 末位参数 `Transports`、`UserHandle` 带上缺省值 `null`。
- 原先不引用 `Testing` 的 8 个测试项目加上项目引用：`ObjectStorage.Abstractions`、`Primitives`、`Scheduling.Abstractions`、`Workflow.Abstractions`、`Configuration.Abstractions`、`Samples`、`Kernel.Analyzers`、`Plugins.InProcess` 的 `.Tests`。

## 影响

- 公开面：`Testing` 多 6 个 `Preview` 类型，包的依赖不变（`System.Text.Json` 与 `System.Diagnostics.DiagnosticSource` 在 BCL 里）。`SpecVectors` 与 `FrameworkWorkspace` 只在框架仓库的检出里找得到目标，应用的测试用不上，留在公开面是为了提供程序与 SDK 的仓库带着规范子模块时也能用。
- `WebAuthnAttestationResponse` 与 `WebAuthnAssertionResponse` 的构造多了缺省值，源代码与二进制都兼容；按构造参数严格绑定的读法现在接受省略这两个成员的 JSON。
- 8 个测试项目的构建闭包多了 `Testing` 依赖的几个包（内核、缓存与持久化的抽象、`Security.WebAuthn`），只影响这些测试项目，不影响被测包自身的依赖。`Primitives.Tests` 因此间接引用内核，原语层的依赖门禁只看正式包，不受影响。
- 每个被改的测试项目用例数不变、全部通过；`Testing.Tests` 新增 48 条：工作区根的查找与报错，严格读取经反射与源生成各拦多出的成员、缺少的构造参数与 `required` 成员、重名成员、非顶层的 `$comment`，文件不存在、为空与不是对象的报错，目录名与文件名的校验，两个收集器的过滤、采样、快照、上限、并发、按异步流收与释放之后。
- `SpecVectorCoverageTests` 照旧按测试源码里出现的文件名字面量识别回放，调用点仍写着文件名，识别出的向量文件与改动前相同（70 个里 69 个，未认领的 `plugin` 档里有一个没有回放）。
