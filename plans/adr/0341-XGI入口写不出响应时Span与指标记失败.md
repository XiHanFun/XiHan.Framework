# ADR-0341 XGI 入口写不出响应时 Span 与指标记失败

## 状态

已采纳（规范 RFC-0023「内核的遥测」第 3 条、RFC-0011 规则 10「编码」与两个状态行已改；内核的 `ObserveMiddleware`、`OperationInvoker`、`App`，新增 internal 的 `OperationCall`。公开 API、错误码、消息键、配置键与向量不变）。

## 日期

2026-11-27

## 背景

ADR-0329 实测：XGI 入口（`OperationInvoker.InvokeAsync(InvocationContext, OperationRequest)`）在管道结束之后把响应对象编码成载荷（RFC-0011 规则 10「编码」），写不出时调用方拿到 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`、没有载荷；Observe 阶段的 `xihan.invoke` 却在管道里就结束了，`xihan.outcome` 是 `success`、状态 `Unset`，`xihan.invocations` 与 `xihan.invocation.duration` 也记成 `success`。ADR-0329 只补了一条 Error 日志，「改在管道里编码」没做（ADR-0329 备选 16）。

仓库所有者拍板：记成失败。Span 状态 Error，`xihan.outcome` 与指标按调用方实际拿到的 `KERNEL_UNEXPECTED_001` 记失败，与其余失败的记法一致。

规范只写了「调用的结果是失败时 Span 的状态为 Error，说明是错误码」（RFC-0023「内核的遥测」第 3 条），没写这个「结果」是管道里的还是调用方拿到的。

修之前实测（`SpanCapture` / `MetricCapture`）：

- 经完整管道的 XGI 入口写不出响应的三种（`texts.render` 的孤立代理项 `value-high`，`texts.check` 的 65 层 `depth-65` 与重复成员 `raw-duplicate`）：调用方拿到 `KERNEL_UNEXPECTED_001`；Span 状态 `Unset`、说明为空，`xihan.outcome` 为 `success`，两个指标的 `xihan.outcome` 维度为 `success`，`xihan.operation` 是操作 ID。
- 幂等记录写不出响应（ADR-0324、ADR-0329）：Idempotency 阶段在管道里把结果换成 `KERNEL_UNEXPECTED_001`，XGI 入口看到失败、不再编码。首次调用与重试回放的调用方都拿到 `KERNEL_UNEXPECTED_001`、没有载荷，Span 都是 Error、说明 `KERNEL_UNEXPECTED_001`，`xihan.outcome` 与两个指标是 `failure`。与 XGI 入口改过之后的记法一致，不用改。
- 宿主端点（`Protocols.Http` 经 `ExecuteAsync`）在 Handle 阶段里编码，写不出时 Observe 阶段本来就看到失败；类型化入口不编码响应。有这个缺陷的只是经完整管道的 XGI 入口：gRPC、MCP、命令行的 `invoke`、AI 工具、进程外与 Wasm 插件的回调，以及 `TestHost.InvokeOperationRawAsync`。

## 备选

1. **编码挪进 Observe 阶段的内置中间件：排在它后面的中间件都结束之后、定结果与结束 Span 之前编码**：采纳。
   - 调用器交给 `App.InvokeAsync` 的处理器换成新的 internal 类型 `OperationCall` 的 `HandleAsync`（代替以前捕获描述与请求的闭包）。`ObserveMiddleware` 进入时经 `App.FindHandler` 取上下文里的处理器，委托的目标是 `OperationCall` 就是这次 XGI 调用（只在操作调用上取）；`nextStep` 返回之后调 `OperationCall.Encode`：管道里是成功才编码，写不出照旧记那条日志、把上下文里的结果换成 `KERNEL_UNEXPECTED_001`，随后 `finally` 照换过的结果定 `xihan.outcome`、状态与两个指标。
   - 管道结束之后 `OperationCall.Complete` 交出编过的响应；管道交出的结果是失败，或者没有编过（关掉了内置中间件，`UseBuiltInMiddleware = false`），就照以前在这里组装、编码。
   - 处理器在同一个上下文（或共享 Items 的派生上下文）上再发起调用会换掉上下文里的处理器，所以与操作 ID 一样在进入时取。
2. **Observe 阶段把结束 Span 与记指标交给调用器，编码之后再记**：否决。Activity、开始时刻与操作维度要跨出中间件交给调用器，多一个状态对象；不经调用器的管道（消息、作业）还得照旧自己记，两条路径。
3. **XGI 入口在 Handle 阶段末尾编码**（宿主端点的做法）：否决。RFC-0011 规则 10 定了编码在管道之后、中间件看到编码之前的结果（ADR-0329 备选 16）；熔断、审计、工作单元会改看编码之后的结果，幂等回放（Handle 不执行）仍要在管道之后编码。
4. **另放一个上下文项标出「这次要编码」**：否决。每次调用多一个 `ConcurrentDictionary` 节点，成功路径多分配；派生上下文共享 Items，里层的类型化调用会读到外层的标记。按处理器认出不用另放东西，处理器本来就是每次调用一个。
5. **调用器在管道之后编码，写不出时再改 Span**：否决。Activity 已经结束并交给导出器，指标已经记下，改不回来。
6. **失败的记法**：照现有的失败路径，不另造名字。Span 状态 `Error`、状态说明是错误码 `KERNEL_UNEXPECTED_001`（框架没有单独的错误码标签，其余失败也只写在状态说明里），`xihan.outcome` 为 `failure`，两个指标的 `xihan.outcome` 维度为 `failure`，`xihan.operation` 照旧是操作 ID。
7. **排在 Observe 内置中间件之后的中间件（审计、熔断）也看到编码之后的结果**：不做。所有者只定了 Span 与指标；它们看到的仍是编码之前的结果（RFC-0011 规则 10），记在「影响」的发现里。
8. **规范**：RFC-0023 已经写了结果的取值与 Span 状态，写明「结果以调用方拿到的为准」、XGI 入口写不出计入失败（写明，不是修订：以前没说是哪个结果）；RFC-0011 规则 10「编码」加一句指过去。不加向量：遥测不在向量里，这一节写的是 .NET 内核的信号，六个 SDK 不发 `xihan.invoke`。

## 决策

1. **规范**：RFC-0023「内核的遥测」第 3 条写明结果以调用方拿到的为准，XGI 入口在管道之后写不出响应时 `xihan.invoke` 状态 Error、说明 `KERNEL_UNEXPECTED_001`，`xihan.outcome` 与两个指标记 `failure`，与幂等记录写不出时相同；.NET 在 Observe 阶段的内置中间件里、排在它后面的中间件都结束时编码，Span 与耗时包括编码。RFC-0011 规则 10「编码」加一句指向它。两个状态行各加一条「同日写明」「同日再写明」，对应 ADR 加上本 ADR。
2. **内核**：
   - 新增 internal 的 `OperationCall`（`HandleAsync`、`Encode`、`Complete`、`Find`），XGI 入口的 `InvokeAsync` 以它的 `HandleAsync` 作处理器、以 `Complete` 交出响应。
   - `App.FindHandler`（internal）读 `InvokeAsync` 放进上下文的处理器。
   - `ObserveMiddleware` 进入时对操作调用取 `OperationCall.Find`，`nextStep` 之后调 `Encode`，再照旧定结果。
   - `OperationInvoker.DispatchAsync`、`ToResponse` 改成 internal，供 `OperationCall` 调用，内容不变。

## 理由

- 可观测的信号要与调用方拿到的一致：调用方拿到 500 一类的错误，看板与告警却记成成功，这类缺陷就从成功率里漏掉了。
- 编码相对其余中间件的位置不变，只把 Observe 阶段定结果的时机挪到编码之后：RFC-0011 规则 10 对其余中间件照旧成立，改动最小。
- 不多分配：按处理器认出调用，不另放上下文项；`OperationCall` 代替原来的闭包，每次调用照旧是一个对象加一个委托。

## 影响

- **公开 API**：不变。新增的类型与成员都是 internal；错误码、消息键、配置键、向量不变。
- **对已有部署的行为变化（升级须知）**：
  - 经完整管道的 XGI 入口写不出响应时，`xihan.invoke` 的状态为 Error、说明 `KERNEL_UNEXPECTED_001`，`xihan.outcome` 与 `xihan.invocations`、`xihan.invocation.duration` 的 `xihan.outcome` 维度为 `failure`；以前是 `success`、状态 Unset。按这两个指标算成功率的看板与告警会把这类调用算作失败。
  - 这些调用的 `xihan.invoke` 时长与 `xihan.invocation.duration` 现在包括编码响应的时间；`OperationInvoker` 写不出时的那条 Error 日志记在 Span 之内。
  - Ingress 阶段的中间件，与 Observe 阶段排在内置中间件之前的中间件，看到的是编码之后的结果；排在内置中间件之后的照旧看到编码之前的结果。
- **规范**（规范工作区已提交）：RFC-0023「内核的遥测」第 3 条、RFC-0011 规则 10 与两个状态行，MINOR（把以前没说的「结果」写死成调用方拿到的；只涉及 .NET 内核发出的遥测，不让任何互通失败）。
- **Go 一致性模块与六个 SDK**：不受影响。SDK 不发 `xihan.invoke` 与这两个指标。
- **测试**：
  - `ObserveTelemetryTests`：孤立代理项、65 层、重复成员三种写不出，Span 状态 Error、说明 `KERNEL_UNEXPECTED_001`，`xihan.outcome` 为 `failure`，两个指标各一条、维度是操作 ID 与 `failure`。改之前 3 条失败。
  - `OperationTests`：关掉内置中间件时在管道之后编码，照常写出，写不出以 `KERNEL_UNEXPECTED_001` 结束、只记一条日志。
  - `IdempotentOperationReplayTests`（`Caching.InMemory.Tests`）：幂等记录写不出时首次调用与重试回放的 Span 都是 Error、说明 `KERNEL_UNEXPECTED_001`，指标两条都是 `failure`（修之前就通过，作实测核对）。
  - 既有的成功路径、嵌套调用、不采样与没有监听器的用例照过；ADR-0329 的日志用例（只记一条）照过。
- **性能**（本机，BenchmarkDotNet 缺省作业；`XgiInvokeBenchmarks.InvokeOperation` 是只为这次测量写、没有提交的基准：`TestHost` 里一次 XGI 调用经完整管道，含建作用域与上下文，响应与 `OperationBenchmarks` 的相同，没有监听器）：

  | | 每次分配 | 平均耗时 |
  | --- | --- | --- |
  | 改前（dev @ 42c8f0ae1） | 8424 B | 3890.5 ns |
  | 改后（与 ADR-0342 一起） | 8080 B | 3848.5 / 3951.4 / 4050.5 ns（三次） |

  分配少的 344 B 里 312 B 是 ADR-0342 的写出（`EncodeResponse` 1248 → 936 B），其余 32 B 是调用器这边：`OperationCall` 代替闭包之后成功路径不多分配。耗时的差在本机的波动之内（同一份代码几次之间差到 5%），只作参考。
- **体积**：见 ADR-0342 的「体积」，两项合计。
- **发现而没改的**（报告另列）：
  - 审计（`AuditMiddleware` 在 Observe 阶段 `End`，排在内置中间件之后）与熔断（Resilience 阶段）对 XGI 入口写不出响应的调用记的是成功。
  - ASP.NET Core 宿主在幂等回放时由 `UseXiHan()` 在管道之后编码（`ApplicationBuilderExtensions`），写不出时异常不在 try 之内、不经 Problem Details；回放的响应来自首次调用写得出的记录，实际几乎走不到。
