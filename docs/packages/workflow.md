# XiHan.Framework.Workflow

> 工作流引擎：图执行引擎 + 17 个内置活动 + 人工任务（审批）+ 表达式求值 + 定时器调度 + 内存存储默认实现。

- **NuGet**：`XiHan.Framework.Workflow`
- **模块类**：`XiHanWorkflowModule`
- **所在层**：基础设施层
- **关键依赖**：框架内部 `Caching` / `DistributedIds` / `EventBus` / `MultiTenancy` / `Script` / `Timing` / `Workflow.Abstractions`；第三方仅 `Microsoft.Extensions.Http`（HTTP 活动用）

## 概述

一套**基于图的流程执行引擎**：流程定义是「节点 + 连线」的有向图，节点绑定一个活动类型，连线可带条件表达式。引擎按图推进，遇到需要等待外部输入的节点（人工审批、定时、信号、子流程）就写下一个**书签**并挂起——**挂起期间不占用任何线程**，直到外界通过办理任务、定时到期、发布信号或子流程终态回调把它唤醒。

三条关键设计：

1. **单写者锁**：引擎对同一实例的所有操作以分布式锁保证单写者，可安全并发调用。
2. **书签消费后必收尾**：书签一经消费，该次执行批次就以引擎内部令牌推进并保证收尾持久化；调用方的取消令牌只在消费书签**之前**生效，批次途中的取消按节点故障处理（可重试），不会丢失实例状态。
3. **失控防护**：单批次最大节点执行数、子流程最大嵌套深度都有硬上限，环路定义不会把进程转死。

## 何时使用

- 需要**审批流**：请假、报销、多级会签、或签、依次审批、转办。
- 需要**长时间运行的业务编排**：跨天/跨周的流程，中间要等人、等外部系统、等定时。
- 需要**可视化/可配置**的流程：定义是纯数据，可落库、可由管理端编辑，不必改代码重新部署。
- 需要在流程里调 HTTP、跑脚本、发事件、起子流程、对集合逐项处理。

不适合：纯粹的「定时跑一段代码」用 [Tasks](./tasks) 的调度器；「一次性异步任务」用 `IBackgroundJobManager`。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Workflow
```

```csharp
[DependsOn(typeof(XiHanWorkflowModule))]
public class MyModule : XiHanModule { }
```

`XiHanWorkflowModule.ConfigureServices` 调用 `AddXiHanWorkflow(configuration)`，绑定两个配置节并注册（**全部 `TryAdd` 语义，均可被业务侧 `Replace` 替换**）：

- `IWorkflowExpressionEvaluator` → `WorkflowExpressionEvaluator`（内置轻量表达式）
- `IWorkflowActivityRegistry` → `WorkflowActivityRegistry`
- `IWorkflowEventPublisher` → `LocalEventBusWorkflowEventPublisher`
- `IWorkflowDefinitionStore` / `IWorkflowInstanceStore` / `IWorkflowBookmarkStore` → 三个 **`InMemory*`** 实现
- `IWorkflowEngine` → `WorkflowEngine`
- `IWorkflowDefinitionManager` → `WorkflowDefinitionManager`
- `IWorkflowUserTaskService` → `WorkflowUserTaskService`
- `IScriptEngine` → `ScriptEngine`（脚本活动依赖，Script 模块未注册 DI，这里补默认注册）
- HTTP 活动的命名 `HttpClient`
- 托管服务 `WorkflowTimerWorker`（定时器轮询）
- 17 个内置活动

::: warning 默认存储是内存实现
`InMemoryWorkflowDefinitionStore` / `InMemoryWorkflowInstanceStore` / `InMemoryWorkflowBookmarkStore` 都是**进程内内存**存储，**进程重启即全部丢失**，也不跨实例。生产必须实现三个 Store 端口并 `Replace` 掉默认注册。
:::

## 工作原理

### 执行批次

`StartAsync` / `ResumeBookmarkAsync` 都是「尽可能往前推」的：拿到实例锁后从当前节点开始沿连线推进，同步链路能走多远走多远（可能一次调用就直接走到完成），直到遇到需要挂起的节点或到达终态才落盘返回。

单次批次的节点执行数受 `MaxNodeExecutionsPerBurst`（默认 1000）限制，防止定义里的环路失控空转。

### 书签与恢复

需要等待的活动返回「挂起」结果并附带一个 `WorkflowBookmarkRequest`，引擎据此写入书签、把实例置为等待。恢复来源有四条：

| 来源 | 触发方式 |
| --- | --- |
| 人工任务办理 | `IWorkflowUserTaskService.CompleteAsync(taskId, actorId, outcome, …)` |
| 定时器到期 | `WorkflowTimerWorker` 轮询到期的 `Timer` / `Retry` / `NodeTimeout` 书签 |
| 信号发布 | `IWorkflowEngine.PublishSignalAsync(signalName, payload, correlationId)` |
| 子流程终态 | 子实例进入终态后回调父节点 |

`ResumeBookmarkAsync` 的 `expectedBookmarkKey` 参数会在锁内校验书签索引键——**任务已被转办**时键已变化，恢复会被拒绝，避免旧受理人的办理动作生效。

### 定时器 Worker

`WorkflowTimerWorker` 是一个托管服务：启动后等 `FirstWaitDurationMilliseconds`，随后按 `PollPeriodMilliseconds` 轮询到期书签；每轮先抢分布式锁（`DistributedLockName`），保证**集群内单活**。

实例处于不可恢复状态（挂起/故障）时，定时类书签会按 `NotResumableTimerBackoffSeconds`（默认 300 秒）回退，避免同一到期书签每轮空转占满取回配额。

### 表达式求值

内置 `WorkflowExpressionEvaluator` 是一个**轻量表达式语言**（非 C# 脚本），用于连线条件与变量赋值：

- 变量引用（点号导航 / 索引）、数字与字符串字面量、`true` / `false` / `null`
- 算术 `+ - * / %`、比较 `== != < <= > >=`、逻辑 `&& || !`（短路）
- 内置函数：`len` / `contains` / `startsWith` / `endsWith` / `upper` / `lower` / `trim` / `isNullOrEmpty` / `abs` / `min` / `max` / `round` / `toNumber` / `toString` / `now` / `date`

数字统一以 `decimal` 求值；语法树按表达式文本缓存复用。要更强的表达能力就用 `Script` 活动（C# 脚本），或 `Replace` 掉 `IWorkflowExpressionEvaluator`。

## 内置活动

| 类型编码 | 活动 | 说明 |
| --- | --- | --- |
| `Start` / `End` | 开始 / 结束 | 流程边界 |
| `Terminate` | 终止 | 强制结束整个实例 |
| `Fault` | 抛出故障 | 主动把实例置为故障态 |
| `SetVariable` | 设置变量 | 按表达式给流程变量赋值 |
| `Decision` | 独占网关 | 条件分支，走第一条满足条件的连线，否则走默认连线 |
| `Parallel` | 并行网关 | 分支扇出 |
| `Join` | 汇聚网关 | 分支汇合 |
| `Delay` | 延时等待 | 写 `Timer` 书签挂起 |
| `WaitSignal` | 等待信号 | 写 `Signal` 书签挂起 |
| `UserTask` | 人工任务 | 审批，写 `UserTask` 书签挂起 |
| `Http` | HTTP 请求 | 调外部接口 |
| `Script` | C# 脚本 | 走 `IScriptEngine` |
| `PublishEvent` | 发布事件 | 走事件总线 |
| `SubWorkflow` | 子流程 | 起一个子实例，终态回调父节点 |
| `ForEach` | 遍历 | 对集合逐项/并行执行 |
| `Log` | 日志 | 记一条日志 |

## 配置

配置节 `XiHan:Workflow`（`XiHanWorkflowOptions.SectionName`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `MaxNodeExecutionsPerBurst` | `int` | `1000` | 单次执行批次的最大节点执行数（防环路失控空转） |
| `InstanceLockExpirySeconds` | `int` | `120` | 实例锁过期秒数 |
| `InstanceLockAcquireTimeoutSeconds` | `int` | `10` | 实例锁获取超时（超时视为并发冲突抛 `WorkflowLockTimeoutException`） |
| `InstanceLockRetryIntervalMilliseconds` | `int` | `200` | 实例锁获取重试间隔 |
| `NotResumableTimerBackoffSeconds` | `int` | `300` | 实例不可恢复时定时类书签的到期回退秒数 |
| `MaxSubWorkflowDepth` | `int` | `16` | 子流程最大嵌套深度（超过则子实例拒绝启动并回调父节点故障） |

配置节 `XiHan:Workflow:Worker`（`XiHanWorkflowWorkerOptions.SectionName`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `IsTimerEnabled` | `bool` | `true` | 是否启用定时器轮询（**关闭后延时/重试/超时书签不会被自动恢复**） |
| `FirstWaitDurationMilliseconds` | `int` | `5000` | 启动后首次轮询前的等待 |
| `PollPeriodMilliseconds` | `int` | `5000` | 轮询周期 |
| `MaxBookmarkFetchCount` | `int` | `100` | 单轮最大取回书签数 |
| `DistributedLockName` | `string` | `"XiHanWorkflowTimerWorker"` | 分布式锁资源名（集群内单活轮询） |
| `DistributedLockExpirySeconds` | `int` | `300` | 分布式锁过期秒数 |

```json
{
  "XiHan": {
    "Workflow": {
      "MaxNodeExecutionsPerBurst": 1000,
      "MaxSubWorkflowDepth": 16,
      "Worker": {
        "IsTimerEnabled": true,
        "PollPeriodMilliseconds": 5000
      }
    }
  }
}
```

## 使用示例

### 示例 1：用构建器定义一个请假审批流程

```csharp
using XiHan.Framework.Workflow.Builders;

var definition = WorkflowDefinitionBuilder.Create("leave-request", "请假审批")
    .AddVariable("amount", required: true)
    .AddStart()
    .AddDecision("gateway")
    .AddUserTask("manager", "经理审批", node => node.WithProperty("Assignees", new List<string> { "1001" }))
    .AddEnd()
    .AddTransition("start", "gateway")
    .AddTransition("gateway", "manager", "amount > 10000")   // 条件走内置表达式
    .AddDefaultTransition("gateway", "end")                   // 不满足条件时的默认分支
    .AddTransition("manager", "end")
    .Build();
```

定义也可以用 `WorkflowDefinitionJsonSerializer` 与 JSON 互转，从而落库、由管理端编辑。

### 示例 2：注册定义并启动实例

```csharp
public class LeaveAppService(
    IWorkflowDefinitionManager definitions,
    IWorkflowEngine engine) : ApplicationServiceBase
{
    public async Task<string> SubmitAsync(decimal amount)
    {
        // 定义必须先发布（Published）才能启动实例
        var created = await definitions.CreateAsync(definition);
        await definitions.PublishAsync(created.Id);

        var instance = await engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "leave-request",
            Variables = new Dictionary<string, object?> { ["amount"] = amount },
            CorrelationId = "leave-2026-0001",   // 业务相关性标识，信号按它定向
            StarterId = CurrentUser.Id?.ToString(),
        });

        return instance.Id;   // 同步链路能走多远走多远，可能已直接完成
    }
}
```

### 示例 3：办理待办

```csharp
public class ApprovalAppService(IWorkflowUserTaskService userTasks) : ApplicationServiceBase
{
    public Task<List<WorkflowUserTask>> GetMyTodoAsync(string userId)
        => userTasks.GetPendingAsync(userId);

    public Task<WorkflowInstance> ApproveAsync(string taskId, string userId, string? comment)
        => userTasks.CompleteAsync(taskId, userId, WorkflowUserTaskOutcomes.Approved, comment);
}
```

办理人必须与任务受理人一致；`outcome` 除 `approved` / `rejected` / `timeout` 外**允许自定义**，配合连线条件即可做多路分支。

### 示例 4：发布信号唤醒等待中的实例

```csharp
// 恢复所有匹配的 Signal 书签，返回成功恢复的书签数
var resumed = await engine.PublishSignalAsync(
    "payment-received",
    payload: new Dictionary<string, object?> { ["orderId"] = 1001 },
    correlationId: "order-1001");   // 为空表示广播
```

### 示例 5：自定义活动

```csharp
using XiHan.Framework.Workflow.Abstractions.Activities;

[WorkflowActivity("SendSms")]
public class SendSmsActivity(ISmsSender sms) : WorkflowActivityBase
{
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        // 节点上的自定义属性来自定义（node.WithProperty(...)）
        var phone = context.Node.Properties["Phone"]?.ToString();
        // 流程变量走 context.Variables
        var applicant = context.Variables.Get<string>("applicant");

        await sms.SendAsync(phone!, $"{applicant} 的申请已通过");

        // 三种收尾：Complete 继续 / Suspend 挂起等书签 / Fault 置为故障
        return ActivityExecutionResult.Complete();
    }
}
```

```csharp
// 注册（同时登记进 XiHanWorkflowOptions.Activities）
services.AddXiHanWorkflowActivity<SendSmsActivity>();
```

`ActivityExecutionResult` 的工厂方法：`Complete(outputs, outcome)` 继续推进（`outcome` 可参与连线条件）、`Suspend(bookmarks)` 挂起等待书签、`Fault(message)` 置为故障，另有 `CompleteWithChildren` / `SuspendWithChildren` 供起子流程用。

需要「挂起等外部输入」的活动实现 `IResumableWorkflowActivity`；需要「实例取消时回补」的实现 `ICompensableWorkflowActivity`。

### 示例 6：换成持久化存储

```csharp
services.Replace(ServiceDescriptor.Singleton<IWorkflowDefinitionStore, DbWorkflowDefinitionStore>());
services.Replace(ServiceDescriptor.Singleton<IWorkflowInstanceStore, DbWorkflowInstanceStore>());
services.Replace(ServiceDescriptor.Singleton<IWorkflowBookmarkStore, DbWorkflowBookmarkStore>());
```

必须用 `Replace`——框架用 `TryAdd` 先注册了内存实现，再 `TryAdd` 会被静默忽略。

## 实例生命周期 API

`IWorkflowEngine` 是实例生命周期的统一入口：

| 方法 | 说明 |
| --- | --- |
| `StartAsync(request)` | 启动实例（**仅允许启动已发布定义**） |
| `ResumeBookmarkAsync(bookmarkId, inputs, throwIfNotResumable, expectedBookmarkKey)` | 消费书签并驱动继续执行 |
| `PublishSignalAsync(signalName, payload, correlationId)` | 恢复所有匹配的信号书签，返回恢复数量 |
| `SuspendAsync(instanceId, reason)` | 挂起（书签保留但拒绝恢复） |
| `ResumeAsync(instanceId)` | 恢复被挂起的实例 |
| `CancelAsync(instanceId, reason)` | 取消（删书签、取消挂起节点；**定义启用补偿时按执行逆序补偿**）；已终态时幂等 |
| `TerminateAsync(instanceId, reason)` | 终止（强制结束，**不执行补偿**）；已终态时幂等 |
| `RetryAsync(instanceId)` | 从故障节点重新执行 |

## 注意事项与最佳实践

- **默认存储是内存的**，生产必须换成持久化实现，否则重启丢全部实例与书签。
- **定义必须先发布**（`Published`）才能启动实例；停用（`Disabled`）后不可启动新实例，存量实例继续运行。
- 关掉 `IsTimerEnabled` 会让延时 / 重试 / 节点超时书签**永不自动恢复**，只有人工任务与信号还能推进流程。
- `MaxSubWorkflowDepth` 是递归定义的硬闸：超深的子实例会拒绝启动并把父节点置为故障，而不是把栈打爆。
- 内置表达式求值器不是 C# 脚本，语法有限；复杂逻辑用 `Script` 活动或替换求值器。
- 集群部署时定时器 Worker 靠分布式锁单活——分布式锁来自 Caching，未接 Redis 时会退化为进程内锁，多实例会各跑各的。

## 依赖模块

- [XiHan.Framework.Workflow.Abstractions](./workflow-abstractions) — 契约与模型。
- [XiHan.Framework.Caching](./caching) — 实例锁与 Worker 单活锁复用其分布式锁。
- [XiHan.Framework.EventBus](./eventbus) — 工作流事件的默认发布通道。
- [XiHan.Framework.DistributedIds](./distributed-ids) — 实例/节点/书签标识生成。
- [XiHan.Framework.MultiTenancy](./multitenancy) — 多租户上下文。
- [XiHan.Framework.Script](./script) — `Script` 活动的 C# 脚本执行。
- [XiHan.Framework.Timing](./timing) — 时间基础设施。

## 相关模块

- [XiHan.Framework.Tasks](./tasks) — 定时任务与后台作业；「定时跑代码」用它，「等人审批的长流程」用工作流。
- [XiHan.Framework.Http](./http) — HTTP 韧性能力。
