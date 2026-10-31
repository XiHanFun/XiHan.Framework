# ADR-0065 追踪标志与 Activity 父级

## 状态

已采纳（`spec/rfcs/0003-invocation-context.md` 字段表增加 `TraceFlags`、规则增加第 7 条；`spec/rfcs/0023-observability.md` 导出一节、`spec/protocol/http.md` 的 Trace 行同步）。

## 日期

2026-11-01

## 背景

文档站写到可观测性一章时发现，内核的 `xihan.invoke` Span 不遵守 RFC-0023 的「采样比例只作用于根 Span，有上游 trace 的沿用上游决定」：

- Observe 阶段以字符串 `00-{trace-id}-{parent-id}-01` 作父级开 Activity，采样位写死为 1。OpenTelemetry 的 `ParentBasedSampler` 看到「父级已采样」就采样，`xihan.invoke` 总被导出，`SamplingRatio` 对它无效。
- HTTP 载体解析 `traceparent` 时只留 trace-id 与 parent-id，上游「不采样」（`-00`）在这里就丢了，下游又被当成已采样。
- 没有 `traceparent` 的请求由 `App.CreateContext` 随机生成 trace-id，ASP.NET Core 自己的请求 Span 在另一条链路上，两者对不上。
- 调用上下文不带采样标志，Observe 阶段也不看 `Activity.Current`，无从做对。

约束：Problem Details、审计事件、命令行失败输出与日志都用调用上下文的 trace-id，导出的 Span 必须是同一个 trace-id，否则排障时对不上号。标准的 `ActivitySource` 没有办法给一个根 Activity 指定 trace-id，根 Span 的 trace-id 只能由它自己生成。

## 决策

- 原语层 `InvocationContext` 增加 `TraceFlags`（`ActivityTraceFlags?`）：上游给出的 trace-flags，上游「不采样」原样保留；为空表示没有上游，trace-id 由内核生成，这次调用是链路的根。派生上下文带同一组标志。
- `App.CreateContext` 增加可选参数 `traceFlags`，上下文来源按顺序：
  1. 调用方给了 trace-id：用它与 parent-id、标志；只给 trace-id 不给标志的按上游已采样处理，与之前的行为一致。
  2. 否则并入当前的 W3C `Activity`（ASP.NET Core 的请求 Span、外层调用的 Span）：取它的 trace-id 与 span-id。它有采样决定时连同标志；没有采样决定时标志为空。
  3. 都没有：生成 trace-id 与 parent-id，标志为空。
- 「有采样决定」的判据：Activity 经具名 `ActivitySource` 创建（监听器做过采样），或有父级（标志随父级）。没有监听器时 ASP.NET Core 为日志直接构造的根 Activity 两者都不满足，它的标志只是缺省值，不代表「不采样」。
- Observe 阶段开 `xihan.invoke`：
  1. 当前 Activity 与上下文同一 trace 且有采样决定：作为它的子 Span，采样随它。
  2. 否则上下文有标志：以 trace-id、parent-id 与标志为远端父级，采样随上游。
  3. 标志为空（链路的根）：先以没有父级的 `CreateActivity` 问一次采样器，只创建、不开始，拿到根规则的决定；再以 `ActivityContext(上下文的 trace-id, 无父 Span, 该决定)` 开启。开启前摘掉当前 Activity，否则 `Activity.Start` 会把它挂成父级；这一步只在 Observe 中间件的异步方法里同步发生，返回时由异步状态机还原调用方的当前 Activity。
- HTTP 载体解析 `traceparent` 的 trace-flags 并传给 `App.CreateContext`；没有合法的 `traceparent` 时不再自己生成，交给第 2 条并入 ASP.NET Core 的请求 Activity。

## 理由

- 根 Span 用「先问一次、再以无父 Span 的上下文开启」：这样采样决定来自采样器的根规则，`SamplingRatio` 生效；导出的 Span 没有父级，trace-id 又是上下文的。别的办法各有硬伤：
  - 让 trace-id 跟着 Activity 走，要改 `InvocationContext.TraceId` 的不可变语义，所有在管道前用过 trace-id 的宿主都要跟着改。
  - 改全局的 `Activity.TraceIdGenerator`，会影响进程里所有的 Activity。
  - 给根 Span 编一个不存在的父级，就是原来的做法，导出后在追踪界面里显示为「缺少父 Span」。
- 问一次采样器只创建 Activity、不开始，监听器的开始与结束回调都不会触发，代价只是一次采样调用。根规则通常按 trace-id 比例判定，探测用的是随机 trace-id，比例上等价；根 Span 没有上游，不存在跨服务一致性的问题。
- 并入当前 Activity 是 RFC-0003「上下文不另起链路」的直接要求：ASP.NET Core 的请求 Span、HttpClient 的出站 Span 与 `xihan.invoke` 在同一条链路上，嵌套调用也自然父子相连。
- 只给 trace-id 不给标志按已采样处理：消息派发、插件回调、AI 工具调用这几处目前只传 trace-id，行为与之前完全一致。它们改为解析并传递标志，各在所属的载体里另行处理。

## 影响

- 公开 API：`InvocationContext.TraceFlags`（Primitives）；`App.CreateContext` 增加末尾的可选参数 `traceFlags`（Kernel）。
- 行为：
  - 上游 `-00` 的 HTTP 请求，`xihan.invoke` 不再被采样。
  - 没有上游的调用按 `SamplingRatio` 采样。
  - 没有 `traceparent` 的 HTTP 请求与 ASP.NET Core 的请求 Span 同一 trace-id，`xihan.invoke` 是它的子 Span。
  - 有当前 Activity 时，`App.CreateContext`（不传 trace-id）不再生成新的 trace-id。
- 未改：消息信封、插件元数据与插件出站的 `traceparent` 仍只传 trace-id 与 parent-id，按已采样处理。
- 测试：
  - 内核：上游采样与不采样、并入当前 Activity 与嵌套调用、链路的根按根规则采样且 trace-id 一致、没有采样决定的旧式根 Activity。
  - HTTP：`-00` / `-01` 的标志，没有 `traceparent` 时并入 ASP.NET Core 的请求 Activity。
  - OpenTelemetry：真实的 `ParentBasedSampler` 下 `SamplingRatio = 0` 不导出根调用，上游采样的仍导出。
