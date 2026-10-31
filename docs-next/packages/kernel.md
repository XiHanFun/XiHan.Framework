# XiHan.Framework.Kernel

微内核：应用构建、能力图谱求解、执行管道与内置中间件、操作契约与调用器、安全原语、健康与插件抽象。所有 5.0 应用都经它装配；它不处理 HTTP、不含任何子系统抽象，也不含具体的插件加载器。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Kernel` |
| 环层 | Ring 1（内核） |
| 能力 | 清单 `xihan.kernel.core` 提供 `kernel.hosting@1`、`kernel.capabilities@1`、`kernel.pipeline@1`、`kernel.diagnostics@1`、`kernel.security@1`、`kernel.plugins@1` |
| 特性入口 | 内核清单永远参与求解，不需要 `UseFeature` |
| 配置节 | `XiHan:Kernel`、`XiHan:Kernel:Resilience`、`XiHan:Providers`、`XiHan:Bindings` |
| 依赖 | `Primitives`；`Microsoft.Extensions.*` 抽象包（Configuration、DependencyInjection、Hosting、Logging、Options）与 `System.Threading.RateLimiting` 10.0.13 |
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
| `Kernel.Security` | `Principal`、`TenantContext`、`SecurityContext`、`ResourceName`、`IPolicy`、`AuthorizationResult`、`PermissionDefinition`、`PermissionRegistry` |
| `Kernel.Plugins` | `IPlugin`、`IPluginHost`、`PluginContext`、`PermissionGrant` 抽象 |
| `Kernel.Localization` | 内核自己的消息目录 |

`AddXiHan` 收集特性、求解能力图谱、按拓扑顺序调用 `Configure`，然后把 `App`、内置中间件、权限表、操作登记表、`IInvocationAccessor` 与一个宿主服务注册进容器；容器由宿主构建，内核只配置 `IServiceCollection`。宿主服务随 .NET 通用宿主启动与停止应用。

## 何时使用

总是需要；组合包、宿主包与所有子系统包都依赖它。应用通常经 `Hosting.AspNetCore` 或 `Hosting.CommandLine` 间接使用：它们在 `IHostApplicationBuilder` 上提供同名的 `AddXiHan`，ASP.NET Core 版本另外装载 `XIHAN_CONFIG_PATH` 配置目录。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Kernel" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

在通用宿主上直接使用内核（没有 HTTP）：

```csharp
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Kernel.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<WorkerFeature>());
await builder.Build().RunAsync();
```

`WorkerFeature` 是应用自己的特性，写法见[能力包与清单](../guide/modularity)。

## 配置

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `XiHan:Kernel:StartTimeoutSeconds` | `30` | 每个特性启动的超时 |
| `XiHan:Kernel:StopTimeoutSeconds` | `30` | 每个特性停止的超时 |
| `XiHan:Kernel:FailurePolicy` | `TerminateStartup` | 特性启动失败时终止启动，或 `IsolateFeature` 只隔离该特性 |
| `XiHan:Kernel:UseBuiltInMiddleware` | `true` | 是否登记 Observe、截止时间、Resilience、Validate 四个内置中间件 |
| `XiHan:Kernel:Resilience:*` | 不限制 | 按操作的熔断、限流与并发上限，见[弹性与限流](../guide/resilience) |
| `XiHan:Providers:<能力 ID>` | — | 指定某能力的提供者清单 ID |
| `XiHan:Bindings:<名字>` | — | 资源绑定；另读环境变量 `SERVICE_BINDING_ROOT` 指向的 Service Binding 目录 |

`XIHAN_CONFIG_PATH`（`ConfigurationDirectory.Variable`）指向的目录里的 JSON 文件由 `Hosting.AspNetCore` 的 `AddXiHan` 装载为可重载的配置源。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `HostApplicationBuilderExtensions.AddXiHan` | 在 `IHostApplicationBuilder` 上装配应用 |
| `AppBuilder` | `UseFeature<T>()`、`UseFeature(instance)`、`UseProvider`、`UseOptions`、`UseServiceBindingRoot`、`ConfigurePipeline`、`Pipeline`；只能构建一次 |
| `App` | 运行中的应用：`State`、`Graph`、`Features`、`Health()`、`InvokeAsync`、`CreateContext`、`DescribeGraph()`、`DescribePipeline()` |
| `IFeature` / `IHostedFeature` | 特性：`Manifest` + `Configure`；托管特性另有 `StartAsync` / `StopAsync` |
| `FeatureContext` | `Configure` 的入口：`Services`、`Configuration`、`Node`、`AddMiddleware`、`AddPermission`、`AddOperation`、`GetBinding` / `TryGetBinding` |
| `KernelOptions` / `FailurePolicy` | 内核选项与失败策略 |
| `IHealthContributor` / `PeriodicHealthContributor` / `ContributorStatus` / `HealthSnapshot` | 健康条目与快照 |
| `ResolvedGraph` / `ResolvedNode` | 已解析的能力图谱，节点带被选中的能力、可选依赖缺失与 shadowed 标记 |
| `PipelineBuilder` / `IPipelineMiddleware` / `PipelineStep` / `Placement` | 管道 |
| `IInvocationAccessor` | 作用域服务，取当前调用的上下文 |
| `InvocationContextExtensions` | `GetOutcome` / `SetOutcome`、`GetOrigin` / `SetOrigin`、`Get` / `Set`（`ContextItemKey<T>`） |
| `IOperationInvoker` / `OperationRegistry` / `OperationDescriptor` | 操作调用与登记 |
| `SecurityContext` / `Principal` / `TenantContext` | 安全上下文，`context.GetSecurity()` 读取，未认证时为 `SecurityContext.Anonymous` |
| `PermissionDefinition` / `PermissionRegistry` | 权限码登记 |
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

对外暴露这类端点时要加认证约束（`RequireXiHanAuthentication`），见[宿主与 Web 应用](../guide/web)。

## 行为与限制

| 预算 | 值 |
| --- | --- |
| 公开类型 | ≤ 120 |
| 第三方依赖 | 0（只允许 `Microsoft.Extensions.*` 抽象包与 `System.Threading.RateLimiting`） |
| 启动额外开销 | < 50 ms |
| 空应用常驻内存 | < 30 MB |
| 运行时反射 | 0 |

失败结果只携带消息键（`KernelMessages`）与参数，文案在包内 10 种区域的 `Localization/<区域>.json`，由 `Localization.InMemory` 装载；启动期抛出的 `FrameworkException` 不本地化。

| 错误码 | 情形 |
| --- | --- |
| `KERNEL_CAPABILITY_001`–`005` | 能力求解失败：缺失、循环、版本冲突、多候选未指定、清单 ID 重复 |
| `KERNEL_PIPELINE_001`–`003` | 中间件约束成环、阶段名未知、重复登记或约束引用无效 |
| `KERNEL_HOSTING_001` | 必需绑定缺失或类型不匹配 |
| `KERNEL_HOSTING_002` / `003` | 特性启动或停止失败 / 停止超时 |
| `KERNEL_HOSTING_004` | 应用状态不允许该操作（重复构建、未就绪时调用） |
| `KERNEL_UNAVAILABLE_001` | 应用未就绪或正在停止（HTTP 503） |
| `KERNEL_UNAVAILABLE_002` / `003` / `004` | 限流 / 并发上限 / 熔断拒绝 |
| `KERNEL_UNAVAILABLE_005` | 周期健康探针失败或超时 |
| `KERNEL_TIMEOUT_001` / `KERNEL_CANCELLED_001` | 超过截止时间 / 调用被取消 |
| `KERNEL_UNEXPECTED_001` | 调用期间的未处理异常 |
| `KERNEL_SECURITY_001` / `002` | 权限码重复 / 权限表已满 |
| `KERNEL_NOTFOUND_001` | 操作未登记 |
| `KERNEL_VALIDATION_001`–`004` | 载荷非法、类型化调用类型不符、不满足 Schema、自校验失败 |
| `KERNEL_OPERATION_001` / `002` | 操作 ID 重复 / 登记表已满 |

## 相关

- [能力包与清单](../guide/modularity)
- [生命周期与健康](../guide/lifecycle)
- [执行管道](../guide/pipeline)
- [操作契约](../guide/operations)
- [Primitives](./primitives)、[Kernel.Generators](./kernel-generators)、[Kernel.Analyzers](./kernel-analyzers)
- 规范：RFC-0002《清单》、RFC-0003《调用上下文与传播》、RFC-0004《管道阶段与排序》、RFC-0011《操作契约与 XGI》
