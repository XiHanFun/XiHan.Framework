来源：https://framework.docs.xihanfun.com/packages/kernel

# XiHan.Framework.Kernel

微内核：应用构建、能力图谱求解、执行管道与内置中间件、操作契约与调用器、安全原语、健康与插件抽象。所有应用都经它装配；它不处理 HTTP、不含任何子系统抽象，也不含具体的插件加载器。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Kernel` |
| 环层 | Ring 1（内核） |
| 能力 | 清单 `xihan.kernel.core` 提供 `kernel.hosting@1`、`kernel.capabilities@1`、`kernel.pipeline@1`、`kernel.diagnostics@1`、`kernel.security@1`、`kernel.plugins@1` |
| 特性入口 | 内核清单永远参与求解，不需要 `UseFeature` |
| 配置节 | `XiHan:Kernel`、`XiHan:Kernel:Resilience`、`XiHan:Providers`、`XiHan:Bindings` |
| 依赖 | `Primitives`；`Microsoft.Extensions.*` 抽象包（Configuration、DependencyInjection、Hosting、Logging、Options）与 `System.Threading.RateLimiting` |
| AOT | 兼容，零反射 |
| 稳定性 | 核心类型 `Stable`（`AppBuilder`、`App`、`IFeature`、`IHostedFeature`、`FeatureContext`、`PipelineBuilder`），其余以各类型的 `[ApiLevel]` 为准 |

## 概述

内核是单个包，用子命名空间划分职责：

| 命名空间 | 职责 |
| --- | --- |
| `Kernel.Hosting` | `AddXiHan`、`AppBuilder`、`App`、`IFeature`、`IHostedFeature`、`FeatureContext`、`KernelOptions`、资源绑定、健康快照与 `IHealthContributor`、`PeriodicHealthContributor` |
| `Kernel.Capabilities` | `CapabilityResolver`、`ResolvedGraph`、`ResolvedNode`、`ManifestReader`（读写 `xihan.manifest.json`） |
| `Kernel.Pipeline` | `IPipelineMiddleware`、`PipelineBuilder`、`PipelineStep`、`Placement`、`DeadlineMiddleware`、`IInvocationAccessor`、调用来源 `InvocationOrigin`、上下文扩展 |
| `Kernel.Operations` | `[Operation]`、`IOperation<TRequest, TResponse>`、`OperationContract`、`OperationDescriptor`、`OperationSchema`、`OperationRegistry`、`IOperationInvoker` |
| `Kernel.Validation` | `ValidationMiddleware`、`JsonSchemaValidator`、`SchemaViolation` |
| `Kernel.Resilience` | `ResilienceMiddleware`、`ResilienceOptions`、`ResiliencePolicy` |
| `Kernel.Diagnostics` | `ObserveMiddleware`、`ActivitySource` 与 `Meter` 桥、`IAuditSink` |
| `Kernel.Security` | `Principal`、`TenantContext`、`SecurityContext`、`ResourceName`、`IPolicy`（`EvaluateAsync` → `Result<AuthorizationResult>`：成功是允许或拒绝，失败是无法判定）、`AuthorizationResult`、`PermissionDefinition`、`PermissionRegistry` |
| `Kernel.Plugins` | `IPlugin`、`IPluginHost`、`PluginContext`、`PermissionGrant` 抽象 |
| `Kernel.Localization` | 内核自己的消息目录 |

`AddXiHan` 收集特性、求解能力图谱、按拓扑顺序调用 `Configure`，然后把 `App`、内置中间件、权限表、操作登记表、`IInvocationAccessor` 与一个宿主服务注册进容器；容器由宿主构建，内核只配置 `IServiceCollection`。宿主服务随 .NET 通用宿主启动与停止应用。

## 何时使用

总是需要；组合包、宿主包与所有子系统包都依赖它。应用通常经 `Hosting.AspNetCore` 或 `Hosting.CommandLine` 间接使用：它们在 `IHostApplicationBuilder` 上提供同名的 `AddXiHan`，两个宿主的版本另外装载 `XIHAN_CONFIG_PATH` 配置目录。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Kernel --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。

在通用宿主上直接使用内核（没有 HTTP）：

```csharp
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Kernel.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<WorkerFeature>());
await builder.Build().RunAsync();
```

`WorkerFeature` 是应用自己的特性，写法见[能力包与清单](/startup/features)。

## 配置

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `XiHan:Kernel:StartTimeoutSeconds` | `30` | 每个特性启动的超时 |
| `XiHan:Kernel:StopTimeoutSeconds` | `30` | 每个特性停止的超时 |
| `XiHan:Kernel:FailurePolicy` | `TerminateStartup` | 特性启动失败时终止启动，或 `IsolateFeature` 只隔离该特性 |
| `XiHan:Kernel:UseBuiltInMiddleware` | `true` | 是否登记 Observe、截止时间、Resilience、Validate 四个内置中间件 |
| `XiHan:Kernel:Resilience:*` | 不限制 | 按操作的熔断、限流与并发上限，见[弹性与限流](/common/resilience) |
| `XiHan:Security:Outbound:*` | 只能连公网地址 | [出站地址策略](/security/outbound)的放行项：`AllowLoopback`、`AllowPrivateNetwork`、`AllowedNetworks`、`AllowedHosts` |
| `XiHan:Providers:<能力 ID>` | — | 指定某能力的提供者清单 ID |
| `XiHan:Bindings:<名字>` | — | 资源绑定；另读环境变量 `SERVICE_BINDING_ROOT` 指向的 Service Binding 目录 |

`XIHAN_CONFIG_PATH`（`ConfigurationDirectory.Variable`）指向的目录里的 JSON 文件由 `Hosting.AspNetCore` 与 `Hosting.CommandLine` 的 `AddXiHan` 装载为可重载的配置源，插在 `ConfigurationDirectory.IsApplicationSettings` 认出的最后一个 appsettings 文件源之后。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `HostApplicationBuilderExtensions.AddXiHan` | 在 `IHostApplicationBuilder` 上装配应用 |
| `AppBuilder` | `UseFeature<T>()`、`UseFeature(instance)`、`UseProvider`、`UseOptions`、`UseServiceBindingRoot`、`ConfigurePipeline`（归属缺省 `app`，按类型登记的中间件构建时补登记为单例）、`Pipeline`；只能构建一次 |
| `App` | 运行中的应用：`State`、`Graph`、`Features`、`Health()`、`InvokeAsync`、`CreateContext`、`DescribeGraph()`、`DescribePipeline()` |
| `IFeature` / `IHostedFeature` | 特性：`Manifest` + `Configure`；托管特性另有 `StartAsync` / `StopAsync` |
| `IShadowAwareFeature` | 能力全部被顶替时内核改调 `ConfigureShadowed`，只登记与能力选择无关的服务（命名存储实例的 Provider）；不注册契约、不贡献中间件、不启动 |
| `FeatureContext` | `Configure` 的入口：`Services`、`Configuration`、`Node`、`AddMiddleware`、`AddPermission`（归属为本特性的清单 ID）、`AddOperation`、`AddStartupCheck`（启动核对：容器建好之后、任何特性启动之前执行，抛出即终止启动，不归失败策略管，ADR-0085）、`GetBinding` / `TryGetBinding` |
| `KernelOptions` / `FailurePolicy` | 内核选项与失败策略 |
| `IHealthContributor` / `PeriodicHealthContributor` / `ContributorStatus` / `HealthSnapshot` | 健康条目与快照 |
| `ResolvedGraph` / `ResolvedNode` | 已解析的能力图谱，节点带被选中的能力、可选依赖缺失与 shadowed 标记；`IsRequestedFor` 区分被要求的选中（有依赖或经 `XiHan:Providers` / `UseProvider` 指定）与兜底选出 |
| `PipelineBuilder` / `IPipelineMiddleware` / `PipelineStep` / `Placement` | 管道 |
| `IInvocationAccessor` | 作用域服务，取当前调用的上下文 |
| `InvocationContextExtensions` | `GetOutcome` / `SetOutcome`、`GetOrigin` / `SetOrigin`、`Get` / `Set`（`ContextItemKey<T>`） |
| `IOperationInvoker` / `OperationRegistry` / `OperationDescriptor` | 操作调用与登记；`OperationRegistry(permissions)` 绑定核对用的权限登记表，`AddRange(owner, operations, permissions)` 一个归属的一组操作连同它定义的权限码全进或全不进；`RemoveOwnedBy(owner)` 在锁里一次撤回该归属的全部操作与权限码（插件卸载时用，之后可以再登记，ADR-0085）；`Enforce(check)` 核对已登记的操作并挂上核对，此后每次登记都经过它（授权能力用它核对契约的策略名，ADR-0080） |
| `SecurityContext` / `Principal` / `TenantContext` | 安全上下文，`context.GetSecurity()` 读取，未认证时为 `SecurityContext.Anonymous` |
| `PermissionDefinition` / `PermissionRegistry` | 权限码登记，每个码记下归属（特性或插件的清单 ID）；`Add(owner, permission)` / `AddRange(owner, permissions)` 一批全进或全不进，`RemoveOwnedBy(owner)` 按归属撤回，写入与撤回在锁内（插件运行期加载与卸载也会登记、撤回） |
| `OutboundAddressPolicy` / `OutboundAddressOptions` / `OutboundAddressDeniedException` | [出站地址策略](/security/outbound)：宿主登记的单例，`Apply(SocketsHttpHandler)` 让连接按实际 IP 过策略，`ConnectAsync(host, port, ct)` 给不是 HTTP 的连接，`IsAllowed(IPAddress)` 判断一个 IP |
| `KernelCapabilities` | 能力 ID 常量与内核清单 |

## 使用示例

读取能力图谱与健康状态：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();

app.MapGet("/diagnostics/graph", (App xihan) => xihan.DescribeGraph());
app.MapGet("/diagnostics/state", (App xihan) => xihan.Health().State.ToString());

app.Run();
```

对外暴露这类端点时要加认证约束（`RequireXiHanAuthentication`），见[宿主与 Web 应用](/web/hosting)。

## 行为与限制

| 预算 | 值 |
| --- | --- |
| 公开类型 | ≤ 120 |
| 第三方依赖 | 0（只允许 `Microsoft.Extensions.*` 抽象包与 `System.Threading.RateLimiting`） |
| 启动额外开销 | < 50 ms |
| 空应用常驻内存 | < 30 MB |
| 运行时反射 | 0 |

失败结果只携带消息键（`KernelMessages`）与参数，文案在包内 10 种区域的 `Localization/<区域>.json`，由 `Localization.InMemory` 装载；启动期抛出的 `FrameworkException` 不本地化。

管道、校验与弹性的补充约定：

- 管道中间件的登记：按类型 `UseAt`、按实例 `UseInstance`、同类型多实例按名 `UseNamedInstance`；命名实例的次序约束用按名字的 `Before(名字, 目标)` / `After(名字, 目标)`。应用构建之后只能经 `UseOwned(归属, 回调)` 成组登记、经 `RemoveOwnedBy(归属)` 按归属撤回，两者都立即核对、全进或全不进；管道按版本构建，在途调用按进入时的管道走完。
- 阶段内的基准次序按 `Placement`、归属清单 ID 字典序、注册序号，只保证可复现、不表达依赖：要用到别的包中间件产出的（认证后的主体、解析后的租户），必须显式 `Before` / `After` 约束，例如应用的会话门禁声明 `After<SessionGateMiddleware, AuthenticationMiddleware>()`。
- `DeadlineMiddleware` 只看令牌归因：截止令牌已触发一律报 `KERNEL_TIMEOUT_001`，调用方同时取消也一样，下游抛出的取消与写下的 `KERNEL_CANCELLED_001` 结果同一规则；不另看时钟，截止令牌还没触发时调用方的取消仍报取消。
- `InvocationOrigin` / `InvocationProtocols` 经 `GetOrigin()` / `SetOrigin()` 读写调用来源（协议、请求目标、远端地址、User-Agent、来源页、端点的路由模板与名字），由协议适配器建上下文时写入，内部调用没有。
- `IOperationResponseFilter`：成功响应写进上下文前按登记顺序过滤，四个调用入口与各协议编码都拿过滤后的结果。
- 校验：`JsonSchemaValidator` 支持 draft 2020-12 的生成器子集（类型、必填、未声明属性、数组、枚举 / 常量、`$ref`、组合、数值范围、长度与模式、元素数），违规最多 32 条、深度上限 64；`SchemaViolation` 是 `$.path: reason`，另带 `kernel.validation.*` 消息键与参数供宿主按调用方区域翻译成字段级错误，`Field` 是去掉根 `$` 的点分字段键，`required` 违规挂在缺失属性自己的路径上。失败结果的细节有 `id`、`reason`、`violations`（文本）、`errors`（违规对象）。实现 `IValidatableObject` 的请求在解码后自校验，结果没有键、文案由应用给，成员名按 camelCase 转成序列化名。类型化调用按 `RequestInfo` 编码一次再校验；原始载荷与解码后的请求记录在上下文（`GetOperationPayload` / `GetOperationRequest`）。DataAnnotations 由 `Kernel.Generators` 折进 Schema，不在运行期反射。
- 弹性：`Operations:<操作 ID>` 按操作覆盖，缺省键沿用默认策略；熔断连续 `FailureThreshold` 次失败打开 `BreakDuration`，到期半开只放一次试探，只有 KIND 为 UNAVAILABLE / TIMEOUT / UNEXPECTED 的结果与未处理异常算失败；限流是固定窗口（`PermitLimit` / `Window` / `QueueLimit`），并发有 `ConcurrencyLimit` / `ConcurrencyQueueLimit`；判定顺序熔断 → 限流 → 并发，拒绝时 `details` 带 `id`，限流与熔断带 `retryAfter` 秒；限制实例按操作 ID 各一份，没有操作描述的调用与没有任何限制的操作直接透传，排队受调用的取消令牌约束。限流与并发用 BCL 的 `System.Threading.RateLimiting`，熔断是内核自己的三态状态机。
- `PeriodicHealthContributor` 是 Provider 连通性探针的宿主：后台按间隔重探、缓存最近结果，`Check()` 立即返回；探针异常记为 `KERNEL_UNAVAILABLE_005`；`ContributorStatus` 除名字、状态、错误外还带说明、检查时刻、耗时与标量数据。
- `App.InvokeAsync` 把 Baggage `xihan.locale` 经 `IMessageResolver.Negotiate` 协商后安装 `MessageScope`，让 `key.L()` 在调用链里得到调用方语言，与 Problem Details 等按上下文解析的文案同一区域。
- 出站地址策略的宿主总是登记为单例；`ConnectAsync` 给不是 HTTP 的连接。

| 错误码 | 情形 |
| --- | --- |
| `KERNEL_CAPABILITY_001`–`005` | 能力求解失败：缺失、循环、版本冲突、多候选未指定、清单 ID 重复 |
| `KERNEL_PIPELINE_001`–`003` | 中间件约束成环、阶段名未知、重复登记或约束引用无效；运行期改动（`UseOwned` / `RemoveOwnedBy`）不合规则、按归属撤回会留下悬空约束、应用构建之后单项改动管道 |
| `KERNEL_HOSTING_001` | 必需绑定缺失或类型不匹配 |
| `KERNEL_HOSTING_002` / `003` | 特性启动或停止失败 / 停止超时 |
| `KERNEL_HOSTING_004` | 应用状态不允许该操作（重复构建、未就绪时调用） |
| `KERNEL_UNAVAILABLE_001` | 应用未就绪或正在停止（HTTP 503） |
| `KERNEL_UNAVAILABLE_002` / `003` / `004` | 限流 / 并发上限 / 熔断拒绝 |
| `KERNEL_UNAVAILABLE_005` | 周期健康探针失败或超时 |
| `KERNEL_TIMEOUT_001` / `KERNEL_CANCELLED_001` | 超过截止时间（截止令牌已触发时下游抛出或写下的取消都报超时，调用方同时取消也一样）/ 截止时间到达之前调用方取消 |
| `KERNEL_UNEXPECTED_001` | 调用期间的未处理异常 |
| `KERNEL_SECURITY_001` / `002` | 权限码重复 / 权限表已满 |
| `KERNEL_NOTFOUND_001` | 操作未登记 |
| `KERNEL_VALIDATION_001`–`004` | 载荷非法、类型化调用类型不符、不满足 Schema、自校验失败 |
| `KERNEL_VALIDATION_005` | 操作契约引用了未登记的权限码：装配完成时抛出、启动失败；装配之后的登记（插件握手之后）返回失败 |
| `KERNEL_OPERATION_001` / `002` | 操作 ID 重复 / 登记表已满 |

## 相关

- [能力包与清单](/startup/features)
- [生命周期与健康](/startup/lifecycle)
- [执行管道](/startup/pipeline)
- [操作契约](/web/operations)
- [Primitives](./primitives)、[Kernel.Generators](./kernel-generators)、[Kernel.Analyzers](./kernel-analyzers)
- 规范：RFC-0002《清单》、RFC-0003《调用上下文与传播》、RFC-0004《管道阶段与排序》、RFC-0011《操作契约与 XGI》
