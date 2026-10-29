# XiHan.Framework.Kernel

微内核（Ring 1）。单包，子命名空间表达职责：

| 命名空间 | 职责 |
| --- | --- |
| `Hosting` | `AppBuilder`、`App`、`IFeature`、`IHostedFeature`、`FeatureContext`、`KernelOptions`、`BindingResolver`、`HealthSnapshot`、`IHealthContributor`（额外健康条目，如插件；条目 Degraded / Failed 让应用 Degraded 但不退出）、`PeriodicHealthContributor`（Provider 连通性探针的宿主：后台按间隔重探、缓存最近结果，`Check()` 立即返回；探针异常记为 `KERNEL_UNAVAILABLE_005`）、`AddXiHan()` |
| `Capabilities` | `[Capability]`、`CapabilityResolver`、`ResolvedGraph`、`ResolvedNode` |
| `Pipeline` | `IPipelineMiddleware`、`PipelineBuilder`（按类型 `UseAt` / 按实例 `UseInstance` / 同类型多实例按名 `UseNamedInstance`）、`PipelineStep`、`PipelineStage`（原语层）、`Placement`、内置中间件 `ObserveMiddleware`、`DeadlineMiddleware`（`Resilience` 区的 `ResilienceMiddleware`、`Validation` 区的 `ValidationMiddleware` 也是内置）；`IInvocationAccessor`（作用域服务，调用链深处不传上下文的服务取当前调用的上下文，调用之外为无值）；`InvocationOrigin` / `InvocationProtocols` 与 `GetOrigin()` / `SetOrigin()`（调用来源：协议、请求目标、远端地址、User-Agent、来源页、端点的路由模板与名字，由协议适配器建上下文时写入，内部调用没有，RFC-0003 规则 6，ADR-0043 / ADR-0048）。阶段内基准次序按 `Placement`、归属清单 ID 字典序、注册序号，只保证可复现、不表达依赖：要用到别的包中间件产出的（如认证后的主体、解析后的租户）必须显式 `Before` / `After` 约束，例如应用的会话门禁要声明 `After<SessionGateMiddleware, AuthenticationMiddleware>()` |
| `Diagnostics` | `ActivitySource` / `Meter` 桥、`IAuditSink` |
| `Security` | `Principal`、`TenantContext`、`SecurityContext`、`ResourceName`、`IPolicy`、`AuthorizationResult`、`PermissionRegistry` |
| `Plugins` | `IPlugin`、`IPluginHost`、`PluginContext`、`PermissionGrant` 抽象；具体隔离机制在 `Plugins.*` 包 |
| `Validation` | Validate 阶段的契约校验（RFC-0011、ADR-0024）：`ValidationMiddleware` 按 `OperationDescriptor.Schema` 的请求 Schema 校验载荷（`KERNEL_VALIDATION_003`）、解码后对实现 `IValidatableObject` 的请求执行自校验（`KERNEL_VALIDATION_004`），不通过不进 Handle；`JsonSchemaValidator`（draft 2020-12 的生成器子集：类型、必填、未声明属性、数组、枚举 / 常量、`$ref`、组合、数值范围、长度与模式、元素数；违规 32 条、深度 64 上限）、`SchemaViolation`（`$.path: reason`，另带 `kernel.validation.*` 消息键与参数供宿主按调用方区域翻译成字段级错误，`Field` 是去掉根 `$` 的点分字段键；`required` 违规挂在缺失属性自己的路径上）。失败结果的细节：`id`、`reason`、`violations`（文本）、`errors`（违规对象）。自校验的结果没有键，文案由应用给（`key.L()` 即得调用方语言），成员名按 camelCase 转成序列化名。类型化调用按 `RequestInfo` 编码一次再校验；原始载荷与解码后的请求都记录在上下文（`GetOperationPayload` / `GetOperationRequest`）。DataAnnotations 由 `Kernel.Generators` 折进 Schema，不在运行期反射 |
| `Resilience` | Resilience 阶段的限制（RFC-0004、ADR-0029）：`ResilienceMiddleware`（内置，紧随 `DeadlineMiddleware`）对带操作描述的调用按 `ResilienceOptions`（配置节 `XiHan:Kernel:Resilience`；`Operations:<操作 ID>` 按操作覆盖，缺省键沿用默认策略）施加熔断（连续 `FailureThreshold` 次失败打开 `BreakDuration`，到期半开只放一次试探；只有 KIND 为 UNAVAILABLE / TIMEOUT / UNEXPECTED 的结果与未处理异常算失败）、固定窗口限流（`PermitLimit` / `Window` / `QueueLimit`）与并发上限（`ConcurrencyLimit` / `ConcurrencyQueueLimit`），判定顺序熔断 → 限流 → 并发，拒绝以 `KERNEL_UNAVAILABLE_004` / `002` / `003` 返回且不进后续阶段（`details` 带 `id`，限流与熔断带 `retryAfter` 秒）；限制实例按操作 ID 各一份，没有操作描述的调用与没有任何限制的操作直接透传；排队受调用的取消令牌约束。限流与并发用 BCL 的 `System.Threading.RateLimiting`，熔断是内核自己的三态状态机；`ResiliencePolicy` 是一组限制的值对象 |
| `Operations` | 操作契约与 XGI（RFC-0011）：`[Operation]`、`IOperation<TRequest, TResponse>`、`OperationContract`、`OperationDescriptor`、`OperationSchema`、`OperationRegistry`、`OperationRequest` / `OperationResponse`、`IOperationInvoker`；特性用 `FeatureContext.AddOperation` 登记（通常由 `Kernel.Generators` 生成的 `AddGeneratedOperations` 代劳），协议适配器按登记表发布 |

## 责任边界

- 不处理 HTTP，不含任何子系统抽象，不含具体插件加载器；协议适配器把线上消息翻译成 `OperationRequest` 交给 `IOperationInvoker`，内核只负责解码、经管道执行与编码。
- 零反射；零第三方依赖，只引用 `Microsoft.Extensions.*` 抽象包与 BCL 扩展包 `System.Threading.RateLimiting`（Resilience 阶段的限流原语，不在共享框架里，架构测试允许名单登记）。
- 失败结果只携带消息键（`KernelMessages`）与参数，文案在 `Localization/<区域>.json`（10 种区域各一个文件，由 `Localization.InMemory` 装载）；`App.InvokeAsync` 按 Baggage `xihan.locale` 安装 `MessageScope`，让 `key.L()` 在调用链里得到调用方语言（RFC-0010）。启动期的 `FrameworkException` 不本地化。

## 依赖方向

依赖 `XiHan.Framework.Primitives`。被子系统、宿主、协议、插件包引用。

## 预算

公开类型 ≤ 120；启动额外开销 < 50 ms；空应用常驻内存 < 30 MB。

## 状态

P1：内核已实现，公开面记录在 `PublicAPI.Unshipped.txt`。
