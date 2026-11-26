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
| `Kernel.Hosting` | `AddXiHan`、`AppBuilder`、`App`、`IFeature`、`IHostedFeature`、`FeatureContext`、`KernelOptions`、`StartupBannerOptions`、资源绑定（`BindingResolver`、`BindingValue` 与内核装配特性、插件宿主共用的绑定核对 `BindingRules`）、健康快照与 `IHealthContributor`、`PeriodicHealthContributor` |
| `Kernel.Capabilities` | `ResolvedGraph`、`ResolvedNode`（能力求解的结果）、`ManifestReader`（读写 `xihan.manifest.json`） |
| `Kernel.Pipeline` | `IPipelineMiddleware`、`PipelineBuilder`、`PipelineStep`、`Placement`、`DeadlineMiddleware`、`IInvocationAccessor`、调用来源 `InvocationOrigin`、上下文扩展 |
| `Kernel.Operations` | `[Operation]`、`IOperation<TRequest, TResponse>`、`OperationContract`、`OperationDescriptor`、`OperationSchema`、`OperationRegistry`、`IOperationInvoker` |
| `Kernel.Validation` | `ValidationMiddleware`、`JsonSchemaValidator`、`SchemaViolation` |
| `Kernel.Json` | `ResultJson`（Result 与 Error 的线上 JSON 编码，RFC-0001「Result」）、`JsonText`、`JsonTextProblem` 与 `JsonTextProblemKind`（读取 JSON 文本的共同检查，RFC-0001「JSON 文本」；XGI 入口写出的响应也过它） |
| `Kernel.Resilience` | `ResilienceMiddleware`、`ResilienceOptions`、`ResiliencePolicy` |
| `Kernel.Diagnostics` | `ObserveMiddleware`、`ActivitySource` 与 `Meter` 桥、`IAuditSink`、`UnreadableDeadlineLog`（截止时间读不出时的限频告警） |
| `Kernel.Security` | `Principal`、`TenantContext`、`SecurityContext`、`ResourceName`、`IPolicy`（`EvaluateAsync` → `Result<AuthorizationDecision>`：成功是允许或拒绝，失败是无法判定）、`AuthorizationDecision`、`PermissionDefinition`、`PermissionRegistry` |
| `Kernel.Plugins` | `IPlugin`、`IPluginHost`、`PluginContext`、`PluginPermissions` 抽象 |
| `Kernel.Localization` | 内核自己的消息目录 |

`AddXiHan` 收集特性、求解能力图谱、按拓扑顺序调用 `Configure`，然后把 `App`、内置中间件、权限表、操作登记表、`IInvocationAccessor` 与一个宿主服务注册进容器；容器由宿主构建，内核只配置 `IServiceCollection`。宿主服务随 .NET 通用宿主启动与停止应用。

## 何时使用

总是需要；组合包、宿主包与所有子系统包都依赖它。应用通常经 `Hosting.AspNetCore` 或 `Hosting.CommandLine` 间接使用：它们在 `IHostApplicationBuilder` 上提供同名的 `AddXiHan`，两个宿主的版本另外装载 `XIHAN_CONFIG_PATH` 配置目录。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Kernel --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[环境与安装](/getting-started/installation)。

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
| `XiHan:Kernel:StartTimeoutSeconds` | `30` | 每个特性 `StartAsync` 的超时，正数 |
| `XiHan:Kernel:StopTimeoutSeconds` | `30` | 每个特性 `StopAsync` 的超时，正数；部署到 Kubernetes 时与 `terminationGracePeriodSeconds` 对齐 |
| `XiHan:Kernel:FailurePolicy` | `TerminateStartup` | 特性启动失败时终止启动，或 `IsolateFeature` 只隔离该特性，取舍见[生命周期与健康](/startup/lifecycle#失败策略) |
| `XiHan:Kernel:UseBuiltInMiddleware` | `true` | 是否登记 Observe、截止时间、Resilience、Validate 四个内置中间件 |
| `XiHan:Kernel:Banner:Enabled` | `true` | 宿主启动时是否打印[启动 Banner](/startup/bootstrap#启动-banner)；命令行宿主缺省 `false` |
| `XiHan:Kernel:Resilience:PermitLimit` | 不限流 | 每个限流窗口允许的调用数，正整数 |
| `XiHan:Kernel:Resilience:WindowSeconds` | `1` | 限流窗口，正数 |
| `XiHan:Kernel:Resilience:QueueLimit` | `0` | 窗口用尽时排队等待的调用数，非负整数 |
| `XiHan:Kernel:Resilience:ConcurrencyLimit` | 不限并发 | 同时执行的调用数上限，正整数 |
| `XiHan:Kernel:Resilience:ConcurrencyQueueLimit` | `0` | 并发用尽时排队等待的调用数，非负整数 |
| `XiHan:Kernel:Resilience:FailureThreshold` | 不熔断 | 连续失败多少次后打开熔断器，正整数 |
| `XiHan:Kernel:Resilience:BreakSeconds` | `30` | 熔断打开的时长，正数 |
| `XiHan:Kernel:Resilience:Operations:<操作 ID>:*` | 沿用上面的默认策略 | 按操作覆盖同名的七个键，语义见[弹性与限流](/common/resilience) |
| `XiHan:Security:Outbound:*` | 只能连公网地址 | [出站地址策略](/security/outbound)的放行项：`AllowLoopback`、`AllowPrivateNetwork`、`AllowedNetworks`、`AllowedHosts` |
| `XiHan:Providers:<能力 ID>` | — | 指定某能力的提供者清单 ID |
| `XiHan:Bindings:<名字>` | — | 资源绑定；另读环境变量 `SERVICE_BINDING_ROOT` 指向的 Service Binding 目录 |

`XIHAN_CONFIG_PATH`（`ConfigurationDirectory.Variable`）指向的目录里的 JSON 文件由 `Hosting.AspNetCore` 与 `Hosting.CommandLine` 的 `AddXiHan` 装载为可重载的配置源，插在 `ConfigurationDirectory.IsApplicationSettings` 认出的最后一个 appsettings 文件源之后，规则见[配置目录与热更新](/configuration/config-directory)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `HostApplicationBuilderExtensions.AddXiHan` | 在 `IHostApplicationBuilder` 上装配应用 |
| `AppBuilder` | `UseFeature<T>()`、`UseFeature(instance)`、`UseProvider`、`UseOptions`、`UseServiceBindingRoot`、`ConfigurePipeline`（归属缺省 `app`，按类型登记的中间件构建时补登记为单例）、`Pipeline`；只能构建一次 |
| `App` | 运行中的应用：`State`、`Graph`、`Features`、`Health()`、`InvokeAsync`、`CreateContext`、`DescribeGraph()`、`DescribePipeline()` |
| `IFeature` / `IHostedFeature` | 特性：`Manifest` + `Configure`；托管特性另有 `StartAsync` / `StopAsync` |
| `IShadowAwareFeature` | 能力全部被顶替时内核改调 `ConfigureShadowed`，只登记与能力选择无关的服务（命名存储实例的 Provider）；不注册契约、不贡献中间件、不启动 |
| `FeatureContext` | `Configure` 的入口：`Services`、`Configuration`、`Node`、`AddMiddleware`、`AddPermission`（归属为本特性的清单 ID）、`AddOperation`、`AddStartupCheck`（启动核对：容器建好之后、任何特性启动之前执行，抛出即终止启动，不归失败策略管，ADR-0085）、`GetBinding` / `FindBinding`、`Bindings`（装配时解析好的一份，此后不变）、`ServiceBindingRoot`（应用经 `UseServiceBindingRoot` 指定的根目录，没有指定时为 `null`；装配之后还要按同一套来源解析绑定的特性把它与 `Configuration` 交给 `BindingResolver.Resolve`，ADR-0268） |
| `BindingResolver` / `BindingValue` | 资源绑定的解析与结果：`Resolve(configuration, serviceBindingRoot)` 先读 Service Binding 目录、再读 `XiHan:Bindings`；根目录下以 `.` 开头的子目录不是绑定（解析全部与点名都不读、不计），绑定目录里的子目录不是属性，目录项可以是符号链接（名字取目录项自己的，内容与大小按链接最终指向的计，Kubernetes 的 projected volume 挂到根目录、Secret 卷挂到绑定目录都读得出）；绑定目录里一个文件最多 `MaxFileBytes`（524288）字节、一个绑定的文件合计最多 `MaxBindingBytes`（1048576）字节，按原始字节计、读到上限就停；一个绑定目录里读的文件最多 `MaxBindingFiles`（128）个，解析全部绑定时根目录下最多 `MaxBindings`（128）个绑定，先数、后读，点了名的解析只数被点名的绑定的文件；超过即抛出 `KERNEL_HOSTING_005`（消息只带绑定名、文件名与上限，条目数超限时不带文件名），配置来源不受限；`BindingValue` 是初始化器记录，`Name`、`Type`、`Value`、`Properties` 都是 `required` 成员，`Property(key)` 取单个属性，`ToString()` 遮蔽主值、属性只列名字（按序数排序，如 `Properties = [password, username]`），值不打印 |
| `BindingRules` | 清单声明的绑定的核对（Preview，`Kernel.Hosting`），内核装配特性与进程外、Wasm、进程内三种插件宿主共用同一处：`HasContent(binding)` 判断解析出的绑定有没有能交给插件或特性的内容（主值或至少一个属性，只给了类型的没有）；`FindViolation(declared, resolved)` 返回写进错误 `reason` 的固定英文文本，必需（`required` 缺省 `true`）的绑定没有内容为 `binding '<名>' is required but not provided`，来源声明的类型与清单不同（按序数比较，必需与可选都核对）为 `binding '<名>' is of type '<来源的类型>' but the manifest declares '<清单的类型>'`，主值与属性都不出现。内核装配特性时按清单的顺序对每个声明的绑定调用它，第一处不合即抛 `KERNEL_HOSTING_001`，消息 `Feature '<清单 ID>' is not configured: <reason>.`，见[资源绑定](/configuration/bindings#必需绑定与启动期失败) |
| `PluginContext` | 插件 `Configure` 的入口：`Manifest`、`Services`、`Configuration`、`Permissions`、`Pipeline`、`ServiceBindingRoot`（宿主构造时给的绑定根目录，没给时为 `null`、根目录取环境变量；进程内插件自己读绑定时写 `BindingResolver.Resolve(context.Configuration, context.ServiceBindingRoot)`，读到的与宿主加载前核对的是同一套来源，ADR-0301）。构造的最后一个参数 `serviceBindingRoot` 可选 |
| `KernelOptions` / `FailurePolicy` | 内核选项与失败策略 |
| `ConfigurationValues` | 从配置逐键读标量：`ReadSeconds`、`ReadInt32`、`ReadInt64`、`ReadBoolean`、`ReadEnum<TEnum>`。键不存在返回 `null`；写了值（含空串）就必须合法，否则抛 `ArgumentException`，消息是 `'<完整配置键>' must be …, but was '<原值>'.`。各包的选项类都用它，自己写选项类时也用它，不要各写一份解析 |
| `StartupBannerOptions` | 启动 Banner：`Enabled`（读 `XiHan:Kernel:Banner:Enabled`）、`Output`（写入目标，缺省标准输出）、`ApplicationVersion`（宿主包从入口程序集填入）；`FromConfiguration(configuration, defaults)` 让宿主给出自己的缺省 |
| `IHealthContributor` / `PeriodicHealthContributor` / `ContributorStatus` / `HealthSnapshot` | 健康条目与快照 |
| `ResolvedGraph` / `ResolvedNode` | 已解析的能力图谱，节点带被选中的能力、可选依赖缺失与 shadowed 标记；`IsRequestedFor` 区分被要求的选中（有依赖或经 `XiHan:Providers` / `UseProvider` 指定）与兜底选出 |
| `PipelineBuilder` / `IPipelineMiddleware` / `PipelineStep` / `Placement` / `MiddlewareDescriptor` | 管道；`PipelineBuilder.Describe()` 返回 `MiddlewareDescriptor` 列表，它是初始化器记录：`Middleware`、`Stage`、`Placement`、`Owner` 是 `required` 成员，`Name`、`Before`、`After` 可选 |
| `IInvocationAccessor` | 作用域服务，取当前调用的上下文 |
| `InvocationContextExtensions` | `FindOutcome` / `SetOutcome`、`FindOrigin` / `SetOrigin`、`Find` / `Set`（`ContextItemKey<T>`） |
| `IOperationInvoker` / `OperationRegistry` / `OperationDescriptor` | 操作调用与登记；`OperationRegistry(permissions)` 绑定核对用的权限登记表，`AddRange(owner, operations, permissions)` 一个归属的一组操作连同它定义的权限码全进或全不进；`RemoveOwnedBy(owner)` 在锁里一次撤回该归属的全部操作与权限码（插件卸载时用，之后可以再登记，ADR-0085）；`Enforce(check)` 核对已登记的操作并挂上核对，此后每次登记都经过它（授权能力用它核对契约的策略名，ADR-0080）；`EnforceAdditions(check)` 挂整组的核对，`check(已登记的, 新增的)` 看得到同一组里的其他项（协议适配器用它挡住路由与 tool 名冲突）；`GetChangeToken()` 的令牌在内容每变一次时触发，协议适配器据此让端点、文档与 tool 表跟随登记表（ADR-0251） |
| `SecurityContext` / `Principal` / `TenantContext` | 安全上下文，`context.GetSecurity()` 读取，未认证时为 `SecurityContext.Anonymous`。`SecurityContext` 与 `TenantContext` 是初始化器记录：`new SecurityContext { Principal = principal, Tenant = tenant }`，`Principal` 与 `Tenant` 都是 `required` 成员，`Principal` 不能为 `null`，`Tenant` 可以是 `null`（无租户写 `Tenant = null`）但必须写出，漏写是编译错误 CS9035；`new TenantContext { Id = id, DisplayName = name }`，`Id` 是 `required` 成员、不能为空白。校验在设值时做，`with` 走同一校验；换主体时写 `Tenant = existing.Tenant`，或用 `existing with { Principal = principal }` |
| `UnreadableDeadlineLog` | 调用方给的截止时间读不出时的限频告警（Preview），内核登记为单例：`ReportHeader(header, length, operationId)` 报请求头，`ReportPluginMetadata(pluginId, length)` 报插件回调的元数据，两类各自在 `Window`（1 分钟）里最多记一条 Warning，下一条带上被压掉的条数；只带头名或插件 ID 与原值的字符数，不带原值。Warning 级别没开时不计数。自写载体读不出调用方给的截止时间时经它报告 |
| `JsonText` / `JsonTextProblem` / `JsonTextProblemKind` | 读取 JSON 文本的共同检查：`FindProblem(utf8)` 核对语法、嵌套不超过 `MaxDepth`（64）层、合法的 Unicode 与任何一层都没有重复的成员，通过时返回无值的 `Maybe<JsonTextProblem>`，否则是第一个问题。`JsonTextProblem` 是记录类，`Kind` 与 `Path` 都是 `required` 成员；`Kind` 是 `Syntax`（不合 RFC 8259 的文法）、`NestingTooDeep`（第一个落在第 65 层的对象或数组）、`InvalidUtf8`（字符串或成员名的原始字节不是合法的 UTF-8）、`LoneSurrogate`（不成对的代理项转义）或 `DuplicateMember`，前四种都是语法错误、`Path` 一律是 `$`，重复成员时 `Path` 是第二次出现的路径（如 `$.data.lines[0].sku`）。语法错误不论出现在哪里都优先于重复成员，几种语法错误之间报文本里第一处；按种类分支时给其余种类留缺省分支。操作载荷、清单与消息信封都先过它，XGI 入口写出的响应也过它（ADR-0319） |
| `PermissionDefinition` / `PermissionRegistry` | 权限码登记，每个码记下归属（特性或插件的清单 ID）；`Add(owner, permission)` / `AddRange(owner, permissions)` 一批全进或全不进，`RemoveOwnedBy(owner)` 按归属撤回，写入与撤回在锁内（插件运行期加载与卸载也会登记、撤回） |
| `OutboundAddressPolicy` / `OutboundAddressOptions` / `OutboundAddressDeniedException` | [出站地址策略](/security/outbound)：宿主登记的单例，`Apply(SocketsHttpHandler)` 让连接按实际 IP 过策略，`ConnectAsync(host, port, ct)` 给不是 HTTP 的连接，`IsAllowed(IPAddress)` 判断一个 IP |
| `ResultJson` | Result 与 Error 的线上 JSON 编码：`Write` / `WriteMaybe` / `WriteError` 写出，`Read` / `ReadMaybe` / `ReadError` 读取；值经调用方给的 `JsonTypeInfo<T>`（源生成） |
| `KernelCapabilities` | 能力 ID 常量与内核清单 |

## 使用示例

读取能力图谱与健康状态：

```csharp
using XiHan.Framework.Kernel.Hosting;

builder.AddXiHan(xihan => xihan.UseStandardProfile());

// app.UseXiHan() 之后
app.MapGet("/diagnostics/graph", (App xihan) => xihan.DescribeGraph());
app.MapGet("/diagnostics/state", (App xihan) => xihan.Health().State.ToString());
```

对外暴露这类端点时要加认证约束（`RequireXiHanAuthentication`），见 [ASP.NET Core 宿主](/web/hosting)。

## 行为与限制

| 预算 | 值 |
| --- | --- |
| 公开类型 | ≤ 120 |
| 第三方依赖 | 0（只允许 `Microsoft.Extensions.*` 抽象包与 `System.Threading.RateLimiting`） |
| 启动额外开销 | JIT 下 ≤ 100 ms，Native AOT 下 < 50 ms |
| 空应用常驻内存 | 框架额外 ≤ 15 MB |
| 运行时反射 | 0 |

失败结果只携带消息键（`KernelMessages`）与参数，文案在包内 10 种区域的 `Localization/<区域>.json`，由 `Localization.InMemory` 装载；启动期抛出的 `FrameworkException` 不本地化。

管道、校验与弹性的补充约定：

- 管道中间件的登记方法、阶段内的排序规则与应用构建之后的成组改动（`UseOwned` / `RemoveOwnedBy`）见[执行管道](/startup/pipeline#排序规则)。
- `DeadlineMiddleware` 只看令牌归因：截止令牌已触发一律报 `KERNEL_TIMEOUT_001`，调用方同时取消也一样，下游抛出的取消与写下的 `KERNEL_CANCELLED_001` 结果同一规则；不另看时钟，截止令牌还没触发时调用方的取消仍报取消。
- `InvocationOrigin` / `InvocationProtocols` 经 `FindOrigin()` / `SetOrigin()` 读写调用来源（协议、请求目标、远端地址、User-Agent、来源页、端点的路由模板与名字），由协议适配器建上下文时写入，内部调用没有。
- `IOperationResponseFilter`：成功响应写进上下文前按登记顺序过滤，四个调用入口与各协议编码都拿过滤后的结果。
- 响应的编码（RFC-0011 规则 10）：XGI 入口（`InvokeAsync(context, request)`）把响应写成载荷时写不成合法的 JSON 文本即写不出：浮点成员是 NaN，或字符串（成员名、值、字典的键）里有孤立的代理项（ADR-0318）；写出的整段文本还要过 `JsonText.FindProblem`，嵌套超过 64 层（从响应的根算起，原样嵌进去的文本也算）、任何一层有重复的成员（扩展数据与声明的属性同名、字典的键按命名策略撞名、转换器写两次同一个名字、`WriteRawValue` 原样写的文本或成员里的 `JsonElement` / `JsonNode` 带进来的），或原样嵌进去的 `\ud800` 这样的转义，同样写不出（ADR-0319）。`OperationDescriptor<…>.EncodeResponse` 这时抛 `ArgumentException`（超过写出的深度上限时是 System.Text.Json 的 `JsonException`），调用以 `KERNEL_UNEXPECTED_001`（`kernel.invocation.unhandled_exception`）结束，没有参数、不带 `inner`、没有载荷，不像 System.Text.Json 的缺省那样把孤立的代理项写成转义的 U+FFFD。经它编码响应的 HTTP（500）、gRPC、MCP、命令行 `invoke`、AI 工具与插件回调 `Host.Invoke` 结果相同；开了幂等的调用在记录响应时同样写不出。类型化入口（`InvokeAsync<TRequest, TResponse>`）不经 JSON，交出处理器返回的对象。
- 写出的层数与字节：深度上限是序列化上下文的 `MaxDepth` 与 64 取小，64 层、最里层有值的响应照常写出；上下文放宽 `MaxDepth`（如 128）也只写到 64 层，设得更小时照它，与按同一个上下文读请求的层数相同。写出用的元数据按只改了 `MaxDepth` 的选项副本向同一个解析器重新取，源生成的上下文不经反射，第一次写出时才取；只认原选项的自定义解析器给不出这份元数据，第一次写出即失败。合法响应的字节与 `JsonSerializer.SerializeToUtf8Bytes` 写出的相同；文本里有 `�` 转义或上下文用了自定义的编码器时另写一遍只为核对，不经编码器写出的 `�` 转义因此也算写不出：转换器以 `WriteRawValue` 原样写的、预先编码的 `JsonEncodedText` 里的，以及缺省编码器下名字里有 U+FFFD 的属性名，框架自己的响应类型没有这样的写法。核对整段文本的代价与把它读一遍相当（ADR-0318「修订」、ADR-0319）。
- 校验：`JsonSchemaValidator` 支持 draft 2020-12 的生成器子集（类型、必填、未声明属性、数组、枚举 / 常量、`$ref`、组合、数值范围、长度与模式、元素数），违规最多 32 条、深度上限 64；`SchemaViolation` 是 `$.path: reason`，另带 `kernel.validation.*` 消息键与参数供宿主按调用方区域翻译成字段级错误，`Field` 是去掉根 `$` 的点分字段键，`required` 违规挂在缺失属性自己的路径上。失败结果的细节有 `id`、`reason`、`violations`（文本）、`errors`（违规对象）。实现 `IValidatableObject` 的请求在解码后自校验，结果没有键、文案由应用给，成员名按 camelCase 转成序列化名。类型化调用按 `RequestInfo` 编码一次再校验；原始载荷与解码后的请求记录在上下文（`FindOperationPayload` / `FindOperationRequest`）。DataAnnotations 由 `Kernel.Generators` 折进 Schema，不在运行期反射。
- 弹性：`Operations:<操作 ID>` 按操作覆盖，缺省键沿用默认策略；熔断连续 `FailureThreshold` 次失败打开 `BreakDuration`，到期半开只放一次试探，只有 KIND 为 UNAVAILABLE / TIMEOUT / UNEXPECTED 的结果与未处理异常算失败；限流是固定窗口（`PermitLimit` / `Window` / `QueueLimit`），并发有 `ConcurrencyLimit` / `ConcurrencyQueueLimit`；判定顺序熔断 → 限流 → 并发，拒绝时 `details` 带 `id`，限流与熔断带 `retryAfter` 秒；限制实例按操作 ID 各一份，没有操作描述的调用与没有任何限制的操作直接透传，排队受调用的取消令牌约束。限流与并发用 BCL 的 `System.Threading.RateLimiting`，熔断是内核自己的三态状态机。
- `PeriodicHealthContributor` 是 Provider 连通性探针的宿主：后台按间隔重探、缓存最近结果，`Check()` 立即返回；探针异常记为 `KERNEL_UNAVAILABLE_005`；`ContributorStatus` 除名字、状态、错误外还带说明、检查时刻、耗时与标量数据。
- `App.InvokeAsync` 把 Baggage `xihan.locale` 经 `IMessageResolver.Negotiate` 协商后安装 `MessageScope`，让 `key.L()` 在调用链里得到调用方语言，与 Problem Details 等按上下文解析的文案同一区域。
- 出站地址策略的宿主总是登记为单例；`ConnectAsync` 给不是 HTTP 的连接。
- `ResultJson` 与各语言 SDK 逐字一致（向量 `primitives/wire-encoding.json`）：无值成功只写 `{"ok":true}`，`Error` 按 `code`、`message`、`messageKey`、`details`、`inner` 的顺序写出，`message` 是 `Error.Message`；读取先过共同的 JSON 读取检查，再核对成员。读取返回结果的结果：外层失败是编码不合法（`PRIMITIVES_VALIDATION_001`，消息键 `kernel.wire.invalid_json` 或 `kernel.wire.invalid`，`reason` 是出错的路径，如 `$.value`、`$.error.inner.code`），外层成功时内层才是对方的成功或失败。可能没有的值用 `ReadMaybe` / `WriteMaybe`（无值是 `"value":null`）。值的数字写法由 `JsonTypeInfo<T>` 决定，System.Text.Json 把整数值的 `double` 写成 `2`；要按值模型原样往返任意 JSON 用 `JsonElement`，错误细节按值模型读写。`message` 只有空白时读成不合法：.NET 的错误文案不能是空白。

| 错误码 | 情形 |
| --- | --- |
| `KERNEL_CAPABILITY_001`–`005` | 能力求解失败：缺失、循环、版本冲突、多候选未指定、清单 ID 重复 |
| `KERNEL_PIPELINE_001`–`003` | 中间件约束成环、阶段名未知、重复登记或约束引用无效；运行期改动（`UseOwned` / `RemoveOwnedBy`）不合规则、按归属撤回会留下悬空约束、应用构建之后单项改动管道 |
| `KERNEL_HOSTING_001` | 清单声明的绑定不合：必需的没给或只给了类型，或来源声明的类型与清单不同（必需与可选都核对），消息 `Feature '<清单 ID>' is not configured: <reason>.`；`GetBinding` 取一个不存在的绑定同样是这个码 |
| `KERNEL_HOSTING_002` / `003` | 特性启动或停止失败 / 停止超时 |
| `KERNEL_HOSTING_004` | 应用状态不允许该操作（重复构建、未就绪时调用） |
| `KERNEL_HOSTING_005` | 绑定目录超过大小或条目数的上限（一个文件 524288 字节、一个绑定合计 1048576 字节，一个绑定 128 个文件、根目录下 128 个绑定），装配时启动失败 |
| `KERNEL_UNAVAILABLE_001` | 应用未就绪或正在停止（HTTP 503） |
| `KERNEL_UNAVAILABLE_002` / `003` / `004` | 限流 / 并发上限 / 熔断拒绝 |
| `KERNEL_UNAVAILABLE_005` | 周期健康探针失败或超时 |
| `KERNEL_TIMEOUT_001` / `KERNEL_CANCELLED_001` | 超过截止时间（截止令牌已触发时下游抛出或写下的取消都报超时，调用方同时取消也一样）/ 截止时间到达之前调用方取消 |
| `KERNEL_UNEXPECTED_001` | 调用期间的未处理异常 |
| `KERNEL_SECURITY_001` / `002` | 权限码重复 / 权限表已满 |
| `KERNEL_NOTFOUND_001` | 操作未登记 |
| `KERNEL_VALIDATION_001`–`004` | 载荷非法、类型化调用类型不符、不满足 Schema、自校验失败 |
| `KERNEL_VALIDATION_005` | 操作契约引用了未登记的权限码：装配完成时抛出、启动失败；装配之后的登记（插件握手之后）返回失败 |
| `KERNEL_VALIDATION_006` | 要求授权而没有判定者：操作契约声明了权限码或策略，Authorize 阶段却没有中间件；或端点挂了授权要求，却没有装配授权能力。启动失败；装配之后登记的这类操作登记失败（ADR-0217） |
| `KERNEL_OPERATION_001` / `002` | 操作 ID 重复 / 登记表已满 |

## 相关

- [能力包与清单](/startup/features)
- [生命周期与健康](/startup/lifecycle)
- [执行管道](/startup/pipeline)
- [操作契约](/web/operations)
- [Primitives](./primitives)、[Kernel.Generators](./kernel-generators)、[Kernel.Analyzers](./kernel-analyzers)
- 规范：RFC-0002《清单》、RFC-0003《调用上下文与传播》、RFC-0004《管道阶段与排序》、RFC-0011《操作契约与 XGI》
