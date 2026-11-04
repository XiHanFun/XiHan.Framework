# ADR-0029 Resilience 阶段：内核内置按操作 ID 的熔断、固定窗口限流与并发上限

## 状态

已采纳（RFC-0004 修订）。

## 日期

2026-10-27

## 背景

RFC-0004 给 `resilience` 阶段的职责是"超时、限流、自适应并发、熔断"，5.0 只实现了截止时间（`DeadlineMiddleware`）。BasicApp 6.0 的导出、报表、AI 调用等重操作需要在框架层限流与隔离故障，而不是每个应用自己在处理器里数计数器，用户于 2026-10-27 决定在内核实现。约束：内核零反射、零第三方依赖；拒绝必须是结果而不是异常，协议适配器按 KIND 映射（UNAVAILABLE → HTTP 503）；限制按操作 ID 配置并可覆盖。

## 备选

1. 引入 Polly（`Microsoft.Extensions.Resilience`）：功能全，但把 Polly 及其依赖带进 Ring 1，公开面也会泄漏第三方类型。
2. 限流与并发自己实现：代码不多，但滑动窗口、排队、许可释放的正确性要自己背。
3. 限流与并发用 BCL 的 `System.Threading.RateLimiting`（`FixedWindowRateLimiter`、`ConcurrencyLimiter`，排队与许可租约都有），熔断自己写（三态状态机）；该包是 .NET 基础类库的一部分但不在共享框架里，要进架构测试的允许名单。
4. 自适应并发（按延迟调上限）：RFC 提到过，但没有可验证的目标，先不做。
5. 限制实例按调用方（主体、租户）分桶：BasicApp 会需要，但键的选择是应用策略；内核只按操作 ID，应用在本阶段加自己的中间件。
6. 熔断的失败判定：所有失败结果都算，或只算基础设施类（KIND 为 UNAVAILABLE / TIMEOUT / UNEXPECTED）与未处理异常。
7. 排队等待不受截止时间约束，或受调用的取消令牌约束。
8. 选项放进 `KernelOptions`，或单独的 `ResilienceOptions`（配置节 `XiHan:Kernel:Resilience`）。

## 决策

采用备选 3、6（只算基础设施类）、7（受令牌约束）、8（单独选项）；4、5 不做。

- `Kernel/Resilience`：`ResiliencePolicy`（一组限制的值对象：`PermitLimit` / `Window` / `QueueLimit`、`ConcurrencyLimit` / `ConcurrencyQueueLimit`、`FailureThreshold` / `BreakDuration`，init 访问器校验）、`ResilienceOptions`（节本身是默认策略，`Operations:<操作 ID>` 覆盖且缺省键沿用默认；操作 ID 须合法；显式逐键读取不反射）、`ResilienceMiddleware`（内置，Resilience 阶段开头、`DeadlineMiddleware` 之后；只作用于带操作描述的调用；按操作 ID 各一份 `OperationLimits`；判定顺序熔断 → 限流 → 并发；拒绝以 UNAVAILABLE 结果结束且不进后续阶段）、`CircuitBreaker`（Closed / Open / HalfOpen，半开只放一次试探；放行后被限流或并发拒绝的试探让出名额；打开期间在途调用结束时不改状态）。
- 错误码 `KERNEL_UNAVAILABLE_002`（限流）/ `003`（并发）/ `004`（熔断），`details` 带 `id`，限流与熔断带 `retryAfter`（秒，向上取整、至少 1）；消息键 `kernel.resilience.*` 进 10 种区域。
- `AppBuilder` 在 `UseBuiltInMiddleware` 时读取配置节、登记选项与中间件；关闭内置中间件时都不登记、配置节不读取。
- `DependencyTests._allowedExternalPackages` 加 `System.Threading.RateLimiting`，`Directory.Packages.props`、03 代码规范、02 设计哲学、技能与门禁说明同步。
- RFC-0004 新增「Resilience 阶段」一节与错误码登记。

## 理由

- 用 BCL 的限流器而不是 Polly（备选 1 → 3）：公开面只出现框架自己的类型；包没有其他依赖，和 `Microsoft.Extensions.Options` 一样属于"没有 Abstractions 拆分但本身只含原语"的例外。
- 只算基础设施类失败（备选 6）：校验、不存在、拒绝是调用方的问题，调用方的坏请求不该把下游判成坏了；异常是最明确的失败信号，穿过本中间件时记一次再交给 Observe 阶段翻译。
- 排队受令牌约束（备选 7）：截止时间是调用的硬约束，排队不能让它失效；`DeadlineMiddleware` 在前面，取消后由它按超时结束。
- 单独选项（备选 8）：`KernelOptions` 是宿主生命周期的选项；限制策略有自己的嵌套结构（按操作覆盖），分开读取更清楚。
- 不做分桶与自适应（备选 4、5）：没有可验证的目标就不进内核；RFC 把这两项明确留给应用。

## 影响

- 内核多一个包引用 `System.Threading.RateLimiting`（`netstandard2.0` 无关，内核只有 `net10.0`）；架构测试的允许名单与文档同步。
- 默认没有配置时没有任何限制，现有应用行为不变；配置了的操作在拒绝时返回 503。
- 测试：`Kernel.Tests/Resilience` 覆盖选项解析、管道位置、限流容量、并发语义、熔断开闭与半开单试探、异常与调用方错误的区分、按操作覆盖、关闭内置中间件与非操作调用。
