# 3.4 生命周期与健康

这一页讲特性与应用从声明到停止经过的九个状态、启动与停止的顺序和超时、特性启动失败时的两种策略、要等容器建好才能做的启动核对，以及把运行期健康上报给探针的 `IHealthContributor`。读完这页，你能写一个在启动时建立连接、停止时释放的特性，知道 `XiHan:Kernel` 每个键的含义，并把应用接进 Kubernetes 的三种探针。前置知识是[能力图谱与 Provider](/startup/capabilities)。

## 为什么需要

一个应用启动时要连数据库、预热缓存、订阅消息，停止时要排空在途请求、释放连接。没有统一的生命周期，这些事散落在各处：有的在构造函数里连库，有的在第一次请求时才初始化，停止顺序更是没人管，依赖者还在用、被依赖者已经关了。框架给特性与应用一套共享的状态机：按能力图谱的拓扑顺序启动、逆序停止，每一步有超时，失败有策略，状态能被探针读到。健康不再是「进程活着」，而是「每个特性与每个外部连接处于什么状态」。

## 九个状态

特性、插件与应用共用同一个状态枚举 `LifecycleState`（原语层）：

| 状态 | 含义 |
| --- | --- |
| `Declared` | 清单已声明，尚未求解 |
| `Resolved` | 已进入能力图谱 |
| `Configured` | `Configure` 已执行 |
| `Started` | 正在启动（`IHostedFeature.StartAsync` 执行中） |
| `Ready` | 可服务 |
| `Degraded` | 可服务但有缺陷：缺少可选依赖，或某个健康条目报告降级 |
| `Draining` | 正在停止，不再接收新调用 |
| `Stopped` | 已停止，终态 |
| `Failed` | 失败 |

合法的迁移只有这些（`LifecycleStateExtensions.CanTransitionTo`）：

```text
Declared → Resolved → Configured → Started → Ready ⇄ Degraded
                                      │        │        │
                                      └────────┴────────┴──→ Draining → Stopped
除 Stopped 与 Failed 外的任何状态 → Failed；Failed → Resolved（重新装配）
```

`Ready` 与 `Degraded` 统称可服务（`IsServing()`），`Stopped` 与 `Failed` 是终态（`IsTerminal()`）。应用处于可服务状态时才接收调用，否则 `App.InvokeAsync` 拒绝，ASP.NET Core 宿主返回 503、错误码 `KERNEL_UNAVAILABLE_001`。

## 应用状态怎样聚合

应用状态由各特性状态聚合而来：

| 特性状态 | 应用状态 |
| --- | --- |
| 有失败且策略为 `TerminateStartup` | `Failed` |
| 全部停止 | `Stopped` |
| 任一特性在排空或已停止 | `Draining` |
| 全部可服务，且没有 `Degraded` | `Ready` |
| 全部可服务，有 `Degraded` | `Degraded` |

应用就绪后，任一健康条目报告 `Degraded` 或 `Failed` 只把应用从 `Ready` 压成 `Degraded`，不让应用退出。

## 启动与停止的顺序

`AddXiHan` 在容器里注册一个宿主服务，随 .NET 通用宿主启动和停止应用：

1. 启动前先执行全部启动核对（见下文），任一失败即终止。
2. 按能力图谱的拓扑顺序逐个启动特性：进入 `Started`，对实现了 `IHostedFeature` 的特性调用 `StartAsync`，完成后进入 `Ready`；声明的可选依赖缺失时进入 `Degraded`。只实现 `IFeature` 的特性直接从 `Started` 进入 `Ready`。
3. 每个 `StartAsync` 受启动超时约束，超时或抛出异常即特性失败（`KERNEL_HOSTING_002`），按失败策略处理。
4. 停止按逆拓扑顺序进行：依赖者先停，被依赖者后停，停止期间依赖仍可用。每个特性进入 `Draining`，调用 `StopAsync`，完成后进入 `Stopped`。
5. 停止超时记为 `KERNEL_HOSTING_003`，抛出异常记为失败；两者都不中断其余特性的停止。

## 最小示例

只在启动时建立连接、停止时释放的特性实现 `IHostedFeature`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace Shop.Reports;

public sealed class ReportsFeature : IHostedFeature
{
    private ReportWarmer? _warmer;

    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.shop.reports",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(ReportsFeature).FullName,
    };

    public void Configure(FeatureContext context) => context.Services.AddSingleton<ReportWarmer>();

    // 根容器已构建；取消令牌同时受宿主取消与启动超时约束
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _warmer = services.GetRequiredService<ReportWarmer>();
        await _warmer.WarmUpAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _warmer = null;
        return Task.CompletedTask;
    }
}

public sealed class ReportWarmer
{
    public Task WarmUpAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

把它加进任一宿主（`UseFeature<ReportsFeature>()`），运行后 `curl -s localhost:5000/health/ready` 的 `features` 里出现 `{ "id": "xihan.shop.reports", "state": "Ready" }`。让 `WarmUpAsync` 抛出异常再启动，缺省策略下进程退出并记录 `KERNEL_HOSTING_002`；让它 `await Task.Delay(Timeout.Infinite, cancellationToken)` 则在 `StartTimeoutSeconds` 后以同一错误码失败。

## 内核选项

配置节 `XiHan:Kernel`（`KernelOptions`）：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `StartTimeoutSeconds` | 正数 | `30` | 每个特性 `StartAsync` 的超时 |
| `StopTimeoutSeconds` | 正数 | `30` | 每个特性 `StopAsync` 的超时；部署到 Kubernetes 时与 `terminationGracePeriodSeconds` 对齐 |
| `FailurePolicy` | `TerminateStartup` / `IsolateFeature` | `TerminateStartup` | 特性启动失败时的处理 |
| `UseBuiltInMiddleware` | 布尔 | `true` | 是否登记内核内置中间件（Observe、截止时间、Resilience、Validate） |

```json
{
  "XiHan": {
    "Kernel": {
      "StartTimeoutSeconds": 30,
      "StopTimeoutSeconds": 30,
      "FailurePolicy": "TerminateStartup"
    }
  }
}
```

非法值（非正数、拼错的枚举名）在构建应用时抛出 `ArgumentException`。代码里用 `AppBuilder.UseOptions(new KernelOptions { ... })` 覆盖配置，属性名是 `StartTimeout`、`StopTimeout`（`TimeSpan`）、`FailurePolicy`、`UseBuiltInMiddleware`。运行期从容器取 `App.Options` 可读到生效的值。

## 失败策略

| 策略 | 特性启动失败时 | 应用状态 |
| --- | --- | --- |
| `TerminateStartup` | 抛出 `FrameworkException`（`KERNEL_HOSTING_002`），宿主启动失败、进程退出 | `Failed` |
| `IsolateFeature` | 该特性记为 `Failed`，其余特性继续启动 | 其余都可服务时为 `Degraded`，探针可区分 |

选哪一个取决于编排环境：Kubernetes 下通常用缺省的 `TerminateStartup`，让 startupProbe 失败、容器重启；单机长驻进程或有降级能力的应用才用 `IsolateFeature`，并在健康端点上盯住 `Failed` 的特性。

失败策略只管特性启动时的失败（连不上外部服务这类运行条件）。装配错误不归它管，不论策略都终止启动：装配完成时的核对（如契约引用了未登记的权限码，`KERNEL_VALIDATION_005`）在 `AppBuilder.Build` 里抛出，要等容器建好才能做的核对放在启动核对里。

## 启动核对

特性在 `Configure` 里用 `FeatureContext.AddStartupCheck(check)` 登记要等容器建好才能做的装配核对。最典型的是授权能力核对操作契约引用的策略名，因为策略是容器里的 `IPolicy`，装配期还拿不到，见[授权与权限](/security/authorization)。

```csharp
public void Configure(FeatureContext context)
{
    // 核对不通过时抛出带错误码的 FrameworkException，一次列出全部问题
    context.AddStartupCheck(static services => services.GetRequiredService<ReportCatalog>().EnsureEveryReportHasATemplate());
}
```

- 时机：宿主启动应用时，容器建好之后、第一个特性启动之前，按登记顺序各执行一次；重复启动时通过过的核对不再执行。
- 失败：任一抛出即终止启动，异常原样抛出（不包成 `KERNEL_HOSTING_002`），后面的核对与全部特性都不再执行，记一条 Error 日志。
- 不归失败策略管：`IsolateFeature` 下同样终止。核对的是「引用了不存在的东西」这类装配错误，隔离哪个特性都修不好，只会让应用带着错误降级运行。
- 连接外部服务、预热缓存这类可能因运行条件失败的工作不要放进启动核对，放在 `IHostedFeature.StartAsync` 里，由失败策略决定是终止还是隔离。

## 健康条目

特性之外的健康信息由 `IHealthContributor` 提供：外部连接是否可用、内存存储是否接近容量、插件进程是否存活。内核从容器里取出全部 `IHealthContributor`，每次读健康快照时调用 `Check()`，返回的 `ContributorStatus` 进入快照的 `Contributions`：

| `ContributorStatus` 成员 | 说明 |
| --- | --- |
| `Name` | 条目名，约定 `<领域>.<机制>.<对象>`，如 `caching.redis.connection` |
| `State` | `Ready`、`Degraded` 或 `Failed` |
| `Error` | 不健康时的错误 |
| `Description` | 检查的是什么 |
| `CheckedAt`、`Duration` | 检查时刻与耗时 |
| `Data` | 名字到标量（字符串、布尔、数值或空）的字典，如已用量与上限 |

`Check()` 必须立即返回。连接类检查用内核的 `PeriodicHealthContributor`：构造时给名字、探针委托（返回 `ValueTask<Result>`）、间隔、`TimeProvider` 与 `ILoggerFactory`，它在后台按间隔执行探针、缓存最近一次结果，`Check()` 只读缓存；探针失败或抛异常记为 `KERNEL_UNAVAILABLE_005` 并报 `Degraded`，恢复后报 `Ready`。框架的 Provider 都按这个方式实现，探测间隔由各自配置节的 `HealthProbeIntervalSeconds` 决定（缺省 15 秒）。

同步计算的条目直接实现接口：

```csharp
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace Shop.Reports;

public sealed class ExportQueue
{
    public const int Limit = 1000;

    public int Count { get; set; }
}

public sealed class ExportQueueHealth(ExportQueue queue) : IHealthContributor
{
    public IReadOnlyList<ContributorStatus> Check() =>
    [
        new ContributorStatus("shop.exports.queue", queue.Count < ExportQueue.Limit ? LifecycleState.Ready : LifecycleState.Degraded)
        {
            Description = "Pending report exports",
            Data = new Dictionary<string, object?> { ["count"] = queue.Count, ["limit"] = ExportQueue.Limit },
        },
    ];
}
```

在特性的 `Configure` 里登记 `context.Services.AddSingleton<IHealthContributor, ExportQueueHealth>()`，并把条目名写进清单的 `Contributes.Health`。框架包的架构测试强制两边一致；应用不受此测试约束，但建议同样声明，`graph` 命令会把它列出来。

## 探针端点

`Hosting.AspNetCore` 的 `app.MapXiHanHealth()` 映射三个端点。它们不经过内核管道，应用未就绪时也能应答：

| 端点 | 通过条件 | Kubernetes 探针 |
| --- | --- | --- |
| `/health/startup` | 每个特性的状态都不早于 `Started`（启动流程已走到每个特性，含失败的） | startupProbe |
| `/health/ready` | 应用处于 `Ready` 或 `Degraded` | readinessProbe |
| `/health/live` | 应用不是 `Failed` | livenessProbe |

通过返回 200、`status` 为 `pass`，否则 503、`status` 为 `fail`。响应体列出应用状态、每个特性与每个健康条目：

```json
{
  "status": "pass",
  "state": "Ready",
  "features": [
    { "id": "xihan.localization.inmemory", "state": "Ready" },
    { "id": "xihan.protocols.http", "state": "Ready" }
  ],
  "contributions": []
}
```

特性失败时该项多一个 `error` 字段给出错误码；健康条目带说明、检查时刻、耗时与数据时，以 `description`、`checkedAt`、`durationMs`、`data` 字段输出，没有的省略。前缀可改：`app.MapXiHanHealth("/internal/health")`。

不经 HTTP 时，从容器取 `App` 调用 `App.Health()` 得到同样的 `HealthSnapshot`（`Started`、`Ready`、`Live` 三个布尔加 `State`、`Features`、`Contributions`）；命令行宿主没有探针端点，由进程退出码表达结果。探针的 Kubernetes 配置与停机宽限见 [Kubernetes](/deployment/kubernetes)。

## 常见问题

### 我的特性只注册服务，要不要实现 `IHostedFeature`

不要。只实现 `IFeature` 的特性在启动阶段直接进入 `Ready`，没有超时也没有失败风险。只有确实需要在启动时做 I/O（连接、预热、订阅）或在停止时释放资源的特性才实现 `IHostedFeature`。

### `StartAsync` 里能用作用域服务吗

`StartAsync` 收到的是根容器，直接解析作用域服务会出错（测试宿主开了 `ValidateScopes`）。需要时用 `IServiceScopeFactory` 自建作用域，见[依赖注入](/startup/dependency-injection)。

### 为什么应用是 `Degraded` 但 `/health/ready` 仍然返回 200

`Degraded` 是可服务状态：少了一个可选依赖，或某个外部连接暂时不可用，但核心功能还在。readinessProbe 只回答「能不能接流量」，所以通过；要区分 `Ready` 与 `Degraded`，读响应体的 `state`，或在监控里对 `contributions` 的状态报警。

### 停止时在途请求会被打断吗

特性进入 `Draining` 后应用不再接收新调用，已进入管道的调用按进入时的状态走完；每个特性的 `StopAsync` 受 `StopTimeoutSeconds` 约束。要给在途请求足够时间，把这个值与编排器的宽限期对齐，Kubernetes 下即 `terminationGracePeriodSeconds`。

### 探针端点需要认证吗

探针带绕过管道的元数据，不经认证与授权，设计上只暴露给编排器。公网环境把它们放到内部端口或经网络策略限制；要在应用层保护时，用 ASP.NET Core 自己的认证方案，见[端点里使用框架](/web/endpoints)。

## 相关包与下一步

- 包参考：[Kernel](/packages/kernel)、[Primitives](/packages/primitives)、[Hosting.AspNetCore](/packages/hosting-aspnetcore)
- 下一页：[依赖注入](/startup/dependency-injection)，看服务在哪里注册、什么生命周期
- 延伸：[Kubernetes](/deployment/kubernetes)（探针与停机宽限）、[隔离级别与清单](/plugins/overview)（插件进程的状态与重启）
