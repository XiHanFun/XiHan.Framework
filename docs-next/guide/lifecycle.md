# 生命周期与健康

本章说明特性与应用的九态生命周期、启动与停止的顺序和超时、失败策略，以及健康条目与探针端点。适用于需要在启动时建立连接、停止时释放资源，或要把应用接入 Kubernetes 探针的开发者。

完整 API 见 [Kernel 包](../packages/kernel)。

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

`Ready` 与 `Degraded` 统称可服务（`IsServing()`）。应用处于可服务状态时才接收调用，否则 `App.InvokeAsync` 拒绝，宿主返回 `KERNEL_UNAVAILABLE_001`（HTTP 503）。

## 启动与停止

`AddXiHan` 在容器里注册一个宿主服务，随 .NET 通用宿主启动和停止应用：

1. 启动按能力图谱的拓扑顺序逐个启动特性：进入 `Started`，对实现了 `IHostedFeature` 的特性调用 `StartAsync`，完成后进入 `Ready`；声明的可选依赖缺失时进入 `Degraded`。
2. 每个 `StartAsync` 受启动超时约束，超时或抛出异常即特性失败（`KERNEL_HOSTING_002`）。
3. 停止按逆拓扑顺序进行：依赖者先停，被依赖者后停，停止期间依赖仍可用。每个特性进入 `Draining`，调用 `StopAsync`，完成后进入 `Stopped`。
4. 停止超时记为 `KERNEL_HOSTING_003`，抛出异常记为失败；两者都不中断其余特性的停止。

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

不需要启动与停止逻辑的特性只实现 `IFeature`，它们在启动阶段直接从 `Started` 进入 `Ready`。

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

非法值（非正数、拼错的枚举名）在构建应用时抛出 `ArgumentException`。代码里可用 `AppBuilder.UseOptions(new KernelOptions { ... })` 覆盖配置。

## 失败策略

| 策略 | 特性启动失败时 | 应用状态 |
| --- | --- | --- |
| `TerminateStartup` | 抛出 `FrameworkException`（`KERNEL_HOSTING_002`），宿主启动失败、进程退出 | `Failed` |
| `IsolateFeature` | 该特性记为 `Failed`，其余特性继续启动 | 其余都可服务时为 `Degraded`，探针可区分 |

应用状态由各特性状态聚合：有失败且策略为终止 → `Failed`；全部停止 → `Stopped`；任一特性在排空或已停止 → `Draining`；全部可服务 → `Ready`（有 `Degraded` 则 `Degraded`）。应用就绪后，任一健康条目报告 `Degraded` 或 `Failed` 只把应用从 `Ready` 压成 `Degraded`，不让应用退出。

## 健康条目

特性之外的健康信息由 `IHealthContributor` 提供：外部连接是否可用、内存存储是否接近容量、插件进程是否存活。内核从容器里取出全部 `IHealthContributor`，每次读健康快照时调用 `Check()`，返回的 `ContributorStatus` 进入快照的 `Contributions`：

| `ContributorStatus` 成员 | 说明 |
| --- | --- |
| `Name` | 条目名，约定 `<领域>.<机制>.<对象>`，如 `caching.redis.connection` |
| `State` | `Ready`、`Degraded` 或 `Failed` |
| `Error` | 不健康时的错误 |
| `Description` | 检查的是什么 |
| `CheckedAt` / `Duration` | 检查时刻与耗时 |
| `Data` | 名字到标量（字符串、布尔、数值或空）的字典，如已用量与上限 |

`Check()` 必须立即返回。连接类检查用内核的 `PeriodicHealthContributor`：它在后台按间隔执行探针、缓存最近一次结果，`Check()` 只读缓存；探针失败或抛异常记为 `KERNEL_UNAVAILABLE_005` 并报 `Degraded`，恢复后报 `Ready`。框架的 Provider 都按这个方式实现，探测间隔由各自配置节的 `HealthProbeIntervalSeconds` 决定（缺省 15 秒）。

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

在特性的 `Configure` 里登记：`context.Services.AddSingleton<IHealthContributor, ExportQueueHealth>()`，并把条目名写进清单的 `Contributes.Health`。框架包的架构测试强制两边一致；应用不受此测试约束，但建议同样声明，`graph` 命令会把它列出来。

## 探针端点

`Hosting.AspNetCore` 的 `app.MapXiHanHealth()` 映射三个端点，它们不经过内核管道，应用未就绪时也能应答：

| 端点 | 通过条件 | 用途 |
| --- | --- | --- |
| `/health/startup` | 每个特性的状态都不早于 `Started`（启动流程已走到每个特性，含失败的） | startupProbe |
| `/health/ready` | 应用处于 `Ready` 或 `Degraded` | readinessProbe |
| `/health/live` | 应用不是 `Failed` | livenessProbe |

通过返回 200，否则返回 503。响应体列出应用状态、每个特性与每个健康条目：

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

健康条目带说明、检查时刻、耗时与数据时，以 `description`、`checkedAt`、`durationMs`、`data` 字段输出，没有的省略。前缀可改：`app.MapXiHanHealth("/internal/health")`。

不经 HTTP 时，从容器取 `App` 调用 `App.Health()` 得到同样的 `HealthSnapshot`；命令行宿主没有探针端点，由进程退出码表达结果。

## 相关

- [能力包与清单](./modularity)
- [部署与打包](./deployment)：探针、停机宽限与 `terminationGracePeriodSeconds`
- [插件](./plugins)：插件进程的状态与重启
- 规范：RFC-0002《清单》中的健康项、RFC-0001《原语》中的生命周期
