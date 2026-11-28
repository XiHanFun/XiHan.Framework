# ADR-0353 内核在宿主的 StartingAsync 里启动特性、StoppedAsync 里停止，普通宿主服务排在特性就绪之后

## 状态

已采纳。改动范围：
- 内核：内部类型 `AppHostedService`。
- `Observability.OpenTelemetry`：特性多登记一条启动核对。

公开面、错误码、消息键、配置键都不变，规范也不变：RFC 不写 .NET 通用宿主的接线，ADR-0129 的 Banner 时机同样只在 ADR 里。RFC-0011「启动核对在容器建好之后、第一个特性启动之前」照旧成立。

## 日期

2026-11-29

## 背景

XiHan.BasicApp 在 2026-11-21、22 两天连着修了一个问题（`db83680d3`、`3bb821a23`），并定下一条规则：宿主里不登记 `BackgroundService`，常驻循环一律由能力包实现 `IHostedFeature`，由守护测试 `BackgroundServicesTests` 保证。

当时的现象：
- 内核启动核对失败时，宿主不经 `StopAsync` 直接释放。
- 宿主的后台服务在核对之前就已开始，执行体还没跑起来就被取消，宿主照样把它记成后台服务故障。

基于 BasicApp 二开的项目据此认为很多后台功能做不了。实际上做得了，只是不能用 .NET 最常见的 `BackgroundService`。这与「优先使用 .NET 内建能力」相矛盾。

根因在内核：`AppBuilder.Build()` 最后才 `AddHostedService<AppHostedService>()`，应用在它的 `StartAsync` / `StopAsync` 里启停。

## 查明的事实

- **通用宿主的次序**（.NET 10，`Microsoft.Extensions.Hosting`）：
  - 启动：先对全部 `IHostedLifecycleService` 调 `StartingAsync`，再对全部宿主服务调 `StartAsync`，最后调 `StartedAsync`。三段依次进行，每段内按登记顺序；`ServicesStartConcurrently` 为真时段内并发，但段与段之间仍然先后分明。
  - `StartingAsync` 抛出即终止启动，后面的 `StartAsync` 一个都不调。
  - 停止：`StoppingAsync`、`StopAsync`、`StoppedAsync` 三段，各段内按登记的逆序。
- **以前的次序**：
  - 应用与特性登记的普通宿主服务（含 `BackgroundService`）有两种：登记在 `AddXiHan` 之前的，以及特性在 `Configure` 里登记的。两种都排在 `AppHostedService` 之前。
  - 结果它们先于启动核对和特性启动开始：特性还没就绪，它们就已经在用消息、持久化这些能力。核对一失败，它们已经开始了，这就是 BasicApp 碰到的现象。
  - 停止按逆序，`AppHostedService` 先停：特性停完了，它们还在跑。
  - 宿主并发启停时，它们与应用的启动同时进行，没有先后可言。
- **复现**：新用例 `AppHostedServiceTests`，用真实的通用宿主。
  - 四种组合：后台服务登记在 `AddXiHan` 之前或由特性登记，宿主依次或并发启停。改动前全部失败，后台服务的 `StartAsync` 排在特性启动之前。
  - 启动核对失败的用例同样失败：后台服务已经开始。
- **OpenTelemetry 的提供者**：
  - `OpenTelemetryObservabilityFeature` 只调 `AddOpenTelemetry()`。追踪、指标与日志的提供者由 OpenTelemetry 自带的 `TelemetryHostedService`（普通 `IHostedService`）在 `StartAsync` 里解析出来，提供者建出之前，追踪源没有监听者，仪表也不启用。
  - 以前它在 `Configure` 里登记，排在 `AppHostedService` 之前，特性启动期间的 Span 与指标都收得到。
  - 只改内核的话，应用先在 `StartingAsync` 里启动特性，这段遥测就丢了。用新用例 `GenericHost_BuildsProvidersBeforeAnyFeatureStarts` 核对过：改动前的内核上通过，只改内核时失败（追踪源没有监听者）。
- **其他宿主服务**：
  - 框架各包里只有内核登记宿主服务。
  - 特性调用的第三方装配里，只有 `AddOpenTelemetry()` 会登记宿主服务。`AddGrpc()` 不登记。
- **不经通用宿主的路径**：
  - `XiHan.Framework.Testing` 的 `TestHost` 直接调 `App.StartAsync`，不受影响。
  - 命令行宿主经 `IHost.StartAsync` / `StopAsync`，走新的次序。

## 备选

1. **`AppHostedService` 实现 `IHostedLifecycleService`，应用在 `StartingAsync` 里启动、`StoppedAsync` 里停止**：采纳。不论登记在哪、宿主是否并发启停，普通宿主服务开始时特性都已就绪，停止时特性都还在。
2. **把 `AppHostedService` 插到服务集合的最前面**：否决。宿主并发启停时 `StartAsync` 同时进行，排在前面也没有先后。别的库同样往前插时次序又不确定，而且只顾得到启动，顾不到并发停止。
3. **维持现状，在文档里写「后台循环只用 `IHostedFeature`」**：否决，这就是 BasicApp 现在的规则。普通 `BackgroundService` 是 .NET 里最自然的写法；第三方库登记的宿主服务（如 OpenTelemetry）应用管不着；停止时特性先于宿主服务停下的问题也还在。
4. **内核接管其他宿主服务的启停**：否决。要包装宿主的生命周期，违背「只扩展不包装」。
5. **OpenTelemetry 特性改成 `IHostedFeature`，在 `StartAsync` 里建出提供者**：否决。它在特性里的位置由拓扑决定，排在它前面的特性启动期间的遥测仍然收不到；而且要改一个 Stable 类型实现的接口。

## 决策

1. `AppHostedService` 实现 `IHostedLifecycleService`：
   - `StartingAsync` 调 `App.StartAsync`，执行启动核对并按拓扑顺序启动特性。
   - `StoppedAsync` 调 `App.StopAsync`，按逆序停止特性。
   - `StartAsync`、`StartedAsync`、`StoppingAsync`、`StopAsync` 什么都不做。
   - 登记位置不变，仍在 `StartupBannerService` 之后，Banner 照旧先打印。
2. `OpenTelemetryObservabilityFeature` 被选中时，在 `Configure` 里登记一条启动核对，依次解析 `TracerProvider`、`MeterProvider` 与 `LoggerProvider`（没有登记的信号取到 `null`）。
   - 启动核对在容器建好之后、任何特性启动之前执行，所以特性启动期间的遥测都收得到，与特性的先后无关。
   - `TelemetryHostedService` 之后再解析，拿到的是同一批单例。
   - 提供者建不出（如导出器配置不对）时，以启动核对失败终止启动，以前是在宿主服务里失败。
3. 新增用例：
   - `AppHostedServiceTests`：四种组合下的启停次序，以及启动核对失败时不启动任何宿主服务。
   - `GenericHost_BuildsProvidersBeforeAnyFeatureStarts`：真实宿主上，探测特性排在 OpenTelemetry 特性前面，启动时追踪源有监听者，新建的计数器已启用。

## 影响

- **新的启动次序**：
  1. 登记在 `AddXiHan` 之前的 `StartingAsync`，如 Banner。
  2. 应用：图谱与管道日志、启动核对、特性启动。
  3. 登记在 `AddXiHan` 之后的 `StartingAsync`。
  4. 全部 `StartAsync`：`BackgroundService`、`TelemetryHostedService`、Web 宿主的 `GenericWebHostService` 都在这一段。
  5. `StartedAsync`。

  停止的次序反过来：全部 `StopAsync` 之后才停特性。登记在 `AddXiHan` 之后、实现了 `StoppedAsync` 的服务先于特性停止。
- **Web 宿主**：服务器开始监听时特性一定已经就绪。以前靠 `WebApplicationBuilder` 把服务器的宿主服务排在最后，现在不依赖它。
- **停止的时间预算**：特性改在最后停止，用的是宿主 `ShutdownTimeout` 剩下的时间。宿主服务停得慢，留给特性的时间就少。内核对每个特性的停止超时不变。
- **迁移**：
  - 宿主服务的 `StartAsync` 里做的准备，如果特性启动时就要用，现在排在特性之后，用不上了。要么改成登记在 `AddXiHan` 之前的 `IHostedLifecycleService.StartingAsync`，要么做成特性。
  - 框架里没有这样的宿主服务。更新日志的升级须知写一条。
- **`TestHost`**：仍不经宿主服务启动。OpenTelemetry 特性的启动核对在 `TestHost` 里同样执行，提供者在启动时就建出，以前要等第一次解析。
- **XiHan.BasicApp**：升级框架之后，可以撤掉「宿主里不登记 `BackgroundService`」这条规则。`BackgroundServicesTests` 的断言要跟着改，可以改成守护「后台服务在特性就绪之后开始」，由 BasicApp 自己决定。
- **验证**（本机 Windows，Release）：
  - `XiHan.Framework.Kernel.Tests`、`Hosting.AspNetCore.Tests`、`Hosting.CommandLine.Tests`、`Testing.Tests`、`Observability.OpenTelemetry.Tests` 全部通过；整个解决方案的 MTP 测试通过（门控用例照常跳过），Release 构建零警告，文档站构建通过。
  - 新用例在改动前的代码上失败，改动后通过；OpenTelemetry 的用例在改动前的内核上通过、只改内核时失败、两处都改后通过。

## 没有解决的

- 宿主并发启动时，Banner 与应用的 `StartingAsync` 同时进行，Banner 可能与图谱、管道的启动日志交错。缺省是依次启动，不受影响；以前并发时同样不保证先后。
- 启动核对本来是装配核对，OpenTelemetry 借它作「容器建好、特性启动之前」的时机。内核没有为此单独开钩子；以后再有包要在这个时机做事，再考虑给 `FeatureContext` 加一个专门的登记点。
