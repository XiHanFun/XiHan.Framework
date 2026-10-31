# XiHan.Framework.Scheduling.InMemory

调度的默认实现，同时提供 `scheduling.jobs@1`（进程内作业存储加抽象包的引擎）与 `scheduling.leader@1`（进程内租约）。适用于单实例部署、开发与测试；`Profiles.Full` 已经包含它。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Scheduling.InMemory` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `scheduling.jobs@1`、`scheduling.leader@1`（`role: default`）；依赖 `kernel.hosting@1` |
| 特性入口 | `InMemorySchedulingFeature`（清单 ID `xihan.scheduling.inmemory`） |
| 配置节 | `XiHan:Scheduling`（引擎选项）、`XiHan:Scheduling:InMemory`（`MaxRuns`） |
| 依赖 | `XiHan.Framework.Scheduling.Abstractions`；无第三方依赖 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

本包只有作业存储与领导选举两样东西：misfire、并发阻塞、暂停与恢复、动态登记、重试、取消、历史都由抽象包的 `JobSchedulerEngine` 完成，语义与数据库实现一致。

`InMemoryJobStore` 把作业、触发器、运行历史与节点放在进程内的表里，全部操作在一把锁内完成，读出来的是深拷贝，调用方改不到存储里的对象。运行历史有界，最多 `MaxRuns` 条，超出时丢弃最旧的已结束记录。存储不跨实例共享（`IsShared` 为假），因此集群唯一的作业（`RunOnEveryInstance = false`）只在持有领导租约的实例上按计划运行，`RunOnEveryInstance` 的作业每个实例各自运行。

`InMemoryLeaderElection` 是进程内的租约表：同一资源在本进程内只有一个持有者，每次获取生成新的持有者标识，过期的记录视为空闲。它不能在实例之间互斥。

特性按能力求解的结果注册契约：

| 被选中的能力 | 注册 |
| --- | --- |
| `scheduling.leader` | `ILeaderElection` → `InMemoryLeaderElection` |
| `scheduling.jobs` | `IJobStore` → `InMemoryJobStore`，并装配 `JobSchedulerEngine` 与 `IJobScheduler` |

只有 `scheduling.leader` 被别的 Provider 顶替时（如装配了 `Scheduling.Kubernetes`），引擎用那个 Provider 的选举，作业存储仍在本进程内。具体类型与健康项总是注册。

## 何时使用

- 单实例部署、开发环境、测试：零外部基础设施。
- Kubernetes 多副本、只要求集群唯一的作业只跑一份：保留本包，加 [Scheduling.Kubernetes](./scheduling-kubernetes) 顶替领导选举。
- 需要运行时作业在实例间共享、历史持久化、节点失联恢复：换 [Scheduling.Database](./scheduling-database)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Scheduling.InMemory" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Full` 已经装配本包；不用组合包时单独装配：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Scheduling.InMemory;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<InMemorySchedulingFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Scheduling:InMemory:MaxRuns` | 正整数 | 1000 | 运行历史上限，超出丢弃最旧的已结束记录 |

引擎选项（轮询周期、并发上限、misfire 阈值、租约、历史保留）在 `XiHan:Scheduling`，见 [Scheduling.Abstractions](./scheduling-abstractions#配置)。值不合法时启动抛 `ArgumentException`。

```json
{
  "XiHan": {
    "Scheduling": {
      "PollIntervalSeconds": 1,
      "MaxConcurrentRuns": 32,
      "HistoryRetentionDays": 7,
      "InMemory": { "MaxRuns": 1000 }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `InMemorySchedulingFeature` | 特性：注册存储、选举与健康项，被选中提供作业能力时装配引擎；应用启动时启动引擎，停止时等进行中的运行结束并释放租约 |
| `InMemoryJobStore` | 进程内 `IJobStore`，`IsShared` 为假 |
| `InMemoryLeaderElection` | 进程内 `ILeaderElection` |
| `InMemorySchedulingOptions` | `MaxRuns` |
| `InMemorySchedulingHealthContributor` | 健康项 `scheduling.inmemory.leader`（`InMemorySchedulingFeature.HealthName`） |

## 使用示例

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.InMemory;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddJob<HeartbeatJob>("heartbeat", JobSchedule.Every(TimeSpan.FromSeconds(10)));
builder.AddXiHan(xihan => xihan.UseFeature<InMemorySchedulingFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();

public sealed class HeartbeatJob(ILogger<HeartbeatJob> logger) : IJob
{
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Heartbeat run {RunId}.", run.Id);
        return ValueTask.FromResult(Result.Success());
    }
}
```

`next/samples/KubernetesApp` 的 `HeartbeatFeature` 用同样的写法在特性的 `Configure` 里登记作业，本地运行时就由本包调度。

## 行为与限制

- 进程重启后，运行时登记的作业、暂停状态、运行历史都丢失；代码登记的作业在下次启动时重新写入。
- 运行历史全部是运行中的记录、已经没有可丢弃的已结束记录时，开始新运行返回 `SCHEDULING_UNAVAILABLE_004`。
- 存储不共享，没有节点心跳与失联恢复：`RequestsRecovery` 不起作用，集群唯一的作业的重试也只有本进程看得到。
- 多个实例各用一份本包时，每个实例都持有自己的进程内租约，集群唯一的作业会在每个实例上各跑一份。
- 健康项 `scheduling.inmemory.leader` 同步计算：引擎在跑为 Ready（进程内租约持有与否都不影响服务），引擎停止为 Stopped；作业能力被别的 Provider 顶替、没有引擎时也是 Ready。
- 通过 `Scheduling.Conformance` 的调度器、存储与领导选举三个套件。

## 相关

- [调度与后台作业](../guide/scheduling)
- [Scheduling.Abstractions](./scheduling-abstractions)
- [Scheduling.Database](./scheduling-database)
- [Scheduling.Kubernetes](./scheduling-kubernetes)
- [Profiles.Full](./profiles-full)
- 规范：RFC-0013《调度与领导选举》；ADR-0020
