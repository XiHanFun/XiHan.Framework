# 实例与用户任务

这一页讲流程跑起来之后的事：怎样启动实例（含信号启动与定时启动）、怎样处理待办（认领、释放、退回、催办、会签阈值、截止与提醒）、怎样写自定义指派规则、实例在等什么又怎样被唤醒（书签、信号、外部任务）、怎样查询与控制实例。读完后，我们能给上一页的请假审批流做出发起、待办与管理界面。前置知识是[定义与发布](/workflow/definitions)。

## 为什么需要

定义只是图纸，业务真正关心的是一次次执行：谁发起了、停在谁手上、超时没有、能不能撤回。这些操作有大量边界——重复提交、并发审批、会签人数不够、审批人离职要转办、流程改版后旧实例怎么迁。工作流引擎把它们做成带明确错误码的服务：启动有重用策略，待办有认领与会签语义，等待有书签与信号，控制有状态机与批量逐项结果，应用只写业务界面。

## 概念

| 概念 | 说明 |
| --- | --- |
| 实例状态 `WorkflowInstanceStatus` | `Running`、`Suspended`、`Completed`、`Canceled`、`Faulted`、`Terminated`；故障信息在 `Fault`、`FaultNodeId`、`FaultNodeInstanceId`，`End` 节点给出的结果在 `Outcome` |
| 书签 `WorkflowBookmark` | 等待中的节点挂起的凭据，种类见下文「等待与恢复」；用户任务的任务 ID 就是书签 ID |
| 用户任务 `UserTask` | 待办视图：`Id`、`InstanceId`、`NodeId`、`AssigneeId`、`ClaimedBy`、`Title`、`InstanceTitle`、`FormKey`、`FormData`、`Priority`、`DueAt`、`CorrelationId`、`StarterId`、`CreatedAt` |
| 外部任务 `ExternalTask` | 交给外部工作者的工作：`Id`、`Topic`、`Payload`、`Variables`、`Attempt`、`LockedUntil` |
| 分区 | 启动、查询、任务、信号都只在调用方的分区（当前租户，或平台）里进行，见[分区](/workflow/definitions#分区) |

## 最小示例

发起一次请假申请，并给审批人一个待办列表与通过按钮：

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;

public sealed class LeaveApplications(IWorkflowEngine engine)
{
    public ValueTask<Result<WorkflowInstance>> ApplyAsync(string applicant, int days, IReadOnlyList<string> approvers, string requestNo, CancellationToken cancellationToken)
        => engine.StartAsync(new WorkflowStartRequest
        {
            Name = "hr.leave",
            Variables = new JsonObject
            {
                ["applicant"] = applicant,
                ["days"] = days,
                ["approvers"] = string.Join(",", approvers),
            },
            StarterId = applicant,
            CorrelationId = requestNo,
            // 同一申请单号已有未结束的实例时拒绝，防止重复提交
            ReusePolicy = WorkflowReusePolicy.RejectActive,
        }, cancellationToken);
}
```

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Tasks;

public sealed class Inbox(IWorkflowTaskService tasks)
{
    // 第 page 页（从 0 起），最新的在前；Total 用来算页数
    public ValueTask<Result<UserTaskPage>> ListAsync(string userId, int page, CancellationToken cancellationToken)
        => tasks.QueryAsync(new UserTaskQuery { AssigneeId = userId, Descending = true, Skip = page * 20, Take = 20 }, cancellationToken);

    public ValueTask<Result<WorkflowInstance>> ApproveAsync(string taskId, string userId, string? comment, CancellationToken cancellationToken)
        => tasks.CompleteAsync(new UserTaskCompletion(taskId, userId) { Outcome = UserTaskOutcomes.Approved, Comment = comment }, cancellationToken);
}
```

两个类都登记为作用域服务。运行后：`ApplyAsync` 返回的实例状态是 `Running`，停在 `manager` 节点；`approvers` 里的每个人在 `ListAsync` 里看到一条标题为「某人 请假 N 天」的任务。任一主管调用 `ApproveAsync`，`days > 3` 的申请流到人事会签，否则实例直接 `Completed`、`Outcome` 为 `approved`；同一单号再调一次 `ApplyAsync` 得到 `WORKFLOW_CONFLICT_005`。

## 启动实例

`WorkflowStartRequest` 的字段：

| 字段 | 说明 |
| --- | --- |
| `Name` + `Version` / `DefinitionId` | 找定义；`Version` 为空取最新已发布版本，只在调用方的分区里找。定义不存在、不是 `Published` 或在别的分区 → `WORKFLOW_NOTFOUND_001` |
| `Variables` | 初始变量；声明了的变量缺省时补默认值，缺必填 → `WORKFLOW_VALIDATION_002` |
| `StarterId` | 发起人，`Starter` 指派规则与 `ExcludeStarter` 用它 |
| `CorrelationId` | 业务关联 ID，信号按它匹配，重用策略按它查重 |
| `ReusePolicy` | `AllowDuplicate`（缺省）、`RejectActive`（已有未结束实例 → `WORKFLOW_CONFLICT_005`）、`ReturnActive`（返回已有实例）；没有 `CorrelationId` 时忽略。查重只看同一分区的实例；查重与写入在同一把实例锁里，并发的同键启动只建一个实例；等锁超时是 `WORKFLOW_TIMEOUT_001` |
| `StartAt` | 延迟启动：开始节点以定时书签挂起到该时刻 |
| `DisplayName` | 实例显示名，空取定义的 |

实例 ID 由引擎生成。子流程的父实例、父节点实例与深度只由引擎在启动子实例时设置，不在启动请求里；要幂等启动，用 `CorrelationId` 与 `ReusePolicy`。

实例属于调用方的分区。返回的是第一个波次结束后的实例：流程一路走到 `End` 时状态已是 `Completed`，停在用户任务上时是 `Running`。定义有实例级超时时，启动时就排好 `InstanceTimeout` 书签，到期按 `TimeoutAction` 让实例故障或终止。

### 信号启动与定时启动

`Start` 节点的两个属性让定义自己启动：

- `SignalName`：`SignalAsync(name, payload, correlationId)` 在恢复匹配的信号书签之后，为每个 `Start.SignalName` 等于该名字的已发布定义（调用方分区的）启动一个实例，载荷写进变量 `signal`，关联 ID 是信号的关联 ID。
- `Cron`（可带 `TimeZone`）：发布时建一个定义级 `DefinitionTimer` 书签，到期由轮询启动一个实例并推算下一次；停用、归档、删除定义时删除书签。文法与调度相同，见[作业与计划](/scheduling/jobs)。

```csharp
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;

var onOrder = WorkflowDefinitionBuilder.Create(new WorkflowName("orders.fulfil"))
    .AddNode("start", new ActivityType(ActivityTypes.Start), configure: node => node.WithProperty(StartActivity.SignalNameProperty, "order.created"))
    .AddExternalTask("ship", "warehouse.ship")
    .AddEnd()
    .AddTransition("start", "ship").AddTransition("ship", "end")
    .Build();

var nightly = WorkflowDefinitionBuilder.Create(new WorkflowName("finance.reconcile"))
    .AddNode("start", new ActivityType(ActivityTypes.Start), configure: node => node
        .WithProperty(StartActivity.CronProperty, "0 0 2 * * *")
        .WithProperty(StartActivity.TimeZoneProperty, "Asia/Shanghai"))
    .AddNode("log", new ActivityType(ActivityTypes.Log), configure: node => node.WithProperty(LogActivity.MessageProperty, "reconcile started"))
    .AddEnd()
    .AddTransition("start", "log").AddTransition("log", "end")
    .Build();
```

## 处理用户任务

`UserTask` 节点挂起时为每个指派人建一个用户任务书签，任务 ID 就是书签 ID。指派来源有三个，合并后去重保序：

| 属性 | 说明 |
| --- | --- |
| `Assignees` | 指派人列表 |
| `AssigneesExpression` | 求值为列表或逗号分隔字符串的表达式 |
| `AssigneeRules` | 规则列表 `[{ "type": ..., "values": [...] }]`；内置类型 `User`（原样）、`Starter`（发起人）、`Expression`（表达式求值） |

其余属性：

| 属性 | 缺省 | 说明 |
| --- | --- | --- |
| `CompletionPolicy` | `Any` | `Any` 任一人通过即通过；`All` 会签；`Sequential` 依次逐个审批 |
| `ApprovalThreshold` | 全部 | `All` 下的通过阈值：人数（如 `2`）或百分比（如 `"60%"`，按指派人数向上取整）；剩余人数不可能达到时以 `rejected` 完成 |
| `ExcludeStarter` | `false` | 从指派人里去掉发起人 |
| `EmptyAssigneeStrategy` | `Fault` | 没有指派人时：`Fault` 节点故障，`AutoApprove`、`AutoReject`，`Fallback` 改用 `FallbackAssigneeRules` |
| `Title`、`FormKey` | 节点显示名、空 | 模板，写进任务视图 |
| `FormData`、`Priority` | 空 | 表单数据、优先级 |
| `DueAfterSeconds` / `DueAt` | 空 | 截止时长或截止时刻，`DueAt` 优先；只写进任务视图，不自动完成任务 |
| `ReminderAfterSeconds` | 空 | 每隔该时长发布一次 `workflow.task.reminder`，直到任务结束 |
| `CcUserIds` / `CcRules` | 空 | 抄送人，写进任务事件 |
| `AllowedOutcomes` | `approved`、`rejected` | 允许的完成结果 |

节点的 `Timeout` 另插一个超时书签，到期以 `outcome = "timeout"` 完成节点。`Sequential` 一次只有当前指派人的一个任务。`All` 下每个人完成时合并其变量并保留其余人的任务，达到阈值才完成节点；任一人给出非 `approved` 的结果立即以该结果完成节点。

`IWorkflowTaskService` 的操作，入参都是记录（`UserTaskCompletion`、`UserTaskTransfer`、`UserTaskAddition`、`UserTaskClaim`、`UserTaskRelease`、`UserTaskReturn`、`UserTaskUrge`），批量方法接集合、单条写法是重载：

| 方法 | 作用 | 主要错误 |
| --- | --- | --- |
| `QueryAsync(UserTaskQuery)` | 读待办的唯一入口：某人的待办给 `AssigneeId`，某实例的给 `InstanceId`，两者必填其一，可按定义名、节点、创建时间与关键字 `Keyword` 过滤，按创建时间升序或降序（`Descending`）分页；返回 `UserTaskPage`（`Items` 这一页、`Total` 总数） | `WORKFLOW_VALIDATION_002`（缺条件；`Skip` 为负或 `Take` 不在 1 到 1000） |
| `CompleteAsync(completions)` | 完成任务，可带结果、意见与变量 | `WORKFLOW_DENIED_001`（不是指派人）、`WORKFLOW_VALIDATION_002`（结果不允许）、`WORKFLOW_NOTFOUND_003` |
| `TransferAsync(transfers)` | 转办给另一个人 | `WORKFLOW_CONFLICT_004`（目标已是指派人） |
| `AddAssigneesAsync(additions)` | 加签：`Any` / `All` 立即加任务，`Sequential` 排到队尾 | `WORKFLOW_CONFLICT_004` |
| `ClaimAsync(claims)` / `ReleaseAsync(releases)` | `Any` 策略下认领（撤下其他人的任务）与释放 | `WORKFLOW_CONFLICT_004`（已被认领）、`WORKFLOW_VALIDATION_002`（不是 `Any`） |
| `ReturnAsync(returns)` | 退回：目标缺省为本节点之前最近完成的用户任务节点，没有则退回 `Start` 之后的首个节点；以跳转实现 | `WORKFLOW_VALIDATION_002` |
| `UrgeAsync(urges)` | 催办：发布 `workflow.task.urged`，不改状态 | — |

任务标题是节点 `Title` 模板渲染的结果（缺省节点名），实例标题是启动时给的显示名、没给取定义的显示名、再没有取定义名。查询只返回当前分区、实例处于运行中的任务：实例挂起或故障时它的任务退出结果，恢复后回来。过滤、排序与分页都由存储执行，`Total` 是同条件的总数；`Take` 缺省 100、最大 1000（`UserTaskQuery.MaxTake`），越界在查询时返回校验错误，HTTP 是 400。关键字规则见[关键字](/workflow/definitions#关键字)。节点实例的 `State` 记录 `assignees`、`approved`、`rejected`、`claimedBy` 与 `history`（每条含 `actorId`、`outcome`、`comment`、`at`），审批记录从这里读。

在最小示例的 `Inbox` 上补搜索、转办与退回：

```csharp
// 待办搜索框：任务标题、流程标题、定义名或单据号（关联 ID）包含关键字即命中
public ValueTask<Result<UserTaskPage>> SearchAsync(string userId, string keyword, int page, CancellationToken cancellationToken)
    => tasks.QueryAsync(new UserTaskQuery { AssigneeId = userId, Keyword = keyword, Descending = true, Skip = page * 20, Take = 20 }, cancellationToken);

public ValueTask<Result> HandOverAsync(string taskId, string userId, string colleague, CancellationToken cancellationToken)
    => tasks.TransferAsync(new UserTaskTransfer(taskId, userId) { TargetAssigneeId = colleague, Comment = "出差，转同事处理" }, cancellationToken);

public ValueTask<Result<WorkflowInstance>> SendBackAsync(string taskId, string userId, CancellationToken cancellationToken)
    => tasks.ReturnAsync(new UserTaskReturn(taskId, userId) { Comment = "材料不全" }, cancellationToken);
```

### 自定义指派规则

组织架构、角色这类规则由应用实现 `IAssigneeProvider` 并按类型登记，同类型重复登记是编程错误：

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Assignees;

public interface IOrgChart
{
    ValueTask<Result<IReadOnlyList<string>>> ManagersOfAsync(IReadOnlyList<string> departments, CancellationToken cancellationToken);
}

public sealed class DepartmentManagerProvider(IOrgChart org) : IAssigneeProvider
{
    public string RuleType => "DepartmentManager";

    public Result Validate(AssigneeRule rule) => Result.Success();

    // 失败原样返回：不能用空列表掩盖组织数据不可用
    public ValueTask<Result<IReadOnlyList<string>>> ResolveAsync(AssigneeResolution resolution, CancellationToken cancellationToken)
        => org.ManagersOfAsync(resolution.Rule.Values, cancellationToken);
}
```

`builder.Services.AddAssigneeProvider<DepartmentManagerProvider>()` 登记后，节点里写 `WithAssigneeRule("DepartmentManager", "finance")`。规则类型未登记时校验与执行都返回 `WORKFLOW_NOTFOUND_004`；Provider 返回失败时节点故障。

## 等待与恢复

书签的种类（`BookmarkKinds`）：

| 种类 | 由谁建 | 怎样恢复 |
| --- | --- | --- |
| `UserTask` | 用户任务 | `IWorkflowTaskService.CompleteAsync` |
| `Timer` | `Delay`、延迟启动 | 到期由轮询恢复 |
| `Signal` | `WaitSignal`、`EventGateway` | `SignalAsync` |
| `SubWorkflow` | `SubWorkflow`、`ForEach` | 子实例结束时回调父实例 |
| `Retry` | 节点重试 | 到期由轮询恢复 |
| `NodeTimeout` | 节点 `Timeout` | 到期由轮询恢复 |
| `External` | `ExternalTask` | 外部工作者完成；锁到期由轮询解锁 |
| `Reminder` | 用户任务提醒 | 引擎直接处理 |
| `InstanceTimeout` | 定义 `Timeout` | 引擎直接处理 |
| `DefinitionTimer` | `Start.Cron` | 引擎直接处理，`InstanceId` 是定义 ID |

`ResumeAsync(bookmarkId, inputs, expectedKey)` 消费一个书签并恢复节点；实例不在运行中，或 `expectedKey` 与书签的 `Key` 不同时返回 `WORKFLOW_CONFLICT_003`，轮询器遇到这类书签把到期时间后推 `NotResumableBackoff`（缺省 5 分钟）。`SignalAsync(name, payload, correlationId)` 消费当前分区里匹配的全部信号书签（书签的 `CorrelationId` 为空时接受任意关联），再按信号启动规则启动实例，返回消费数加启动数。

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Workflow.Abstractions.Engine;

public sealed class PaymentCallbacks(IWorkflowEngine engine)
{
    // 支付网关回调：唤醒在等 payment.received 信号的、关联 ID 为该订单号的实例
    public async ValueTask<int> PaidAsync(string orderNo, decimal amount, CancellationToken cancellationToken)
        => (await engine.SignalAsync("payment.received", new JsonObject { ["amount"] = amount }, orderNo, cancellationToken)).ValueOrThrow();
}
```

### 外部任务

`ExternalTask` 活动把一段工作交给外部工作者（另一个进程、另一种语言写的服务），按 Camunda 外部任务的方式工作：

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Workflow.Abstractions.ExternalTasks;

public sealed class ShippingWorker(IWorkflowExternalTaskService external)
{
    public async ValueTask RunOnceAsync(CancellationToken cancellationToken)
    {
        // 领取并加锁 5 分钟：锁内只有本工作者能完成或失败它
        var fetched = (await external.FetchAsync("warehouse.ship", "worker-1", TimeSpan.FromMinutes(5), 10, cancellationToken)).ValueOrThrow();
        foreach (var task in fetched)
        {
            var order = task.Payload?["order"]?.GetValue<string>();
            if (order is { Length: > 0 })
            {
                await external.CompleteAsync(new ExternalTaskCompletion(task.Id, "worker-1") { Outputs = new JsonObject { ["shipped"] = true } }, cancellationToken);
            }
            else
            {
                // 带 RetryAfter：解锁并推迟，任务的 Attempt 加一；不带则节点以该错误故障，按节点重试策略处理
                await external.FailAsync(new ExternalTaskFailure(task.Id, "worker-1") { Error = "order missing", RetryAfter = TimeSpan.FromMinutes(1) }, cancellationToken);
            }
        }
    }
}
```

领取只返回当前分区里该主题未锁定（或锁已过期）且已到可领取时刻的任务，按创建时间最多 `maxTasks` 条。`maxTasks` 取 1 到 `IWorkflowExternalTaskService.MaxTasksPerFetch`（100），越界返回 `WORKFLOW_VALIDATION_002`；条数与分区、可领取时刻一起下推到存储查询。候选在实例锁内重读确认，被别的工作者抢先或实例已不在运行中的跳过，所以一次领到的可能少于 `maxTasks`，空手而归不代表主题下没有任务。`ExtendLockAsync(ExternalTaskLockExtension)` 延长锁。没有持有未过期的锁时完成、失败、延长都返回 `WORKFLOW_DENIED_002`。锁到期的任务由轮询自动解锁，别的工作者可以重新领取。

## 查询实例

实例与节点实例的查询用引擎的 `FindInstanceAsync`、`ListInstancesAsync`、`CountInstancesAsync`、`NodeInstancesAsync`、`BookmarksAsync`。它们只查调用方的分区；按 ID 找别的分区的实例为无值或 `WORKFLOW_NOTFOUND_002`。

```csharp
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;

public sealed class LeaveReports(IWorkflowEngine engine)
{
    public async ValueTask<(IReadOnlyList<WorkflowInstance> Items, long Total)> PendingAsync(int page, CancellationToken cancellationToken)
    {
        // 分区取自当前调用，查询里没有租户条件
        var query = new WorkflowInstanceQuery
        {
            DefinitionName = "hr.leave",
            Statuses = [WorkflowInstanceStatus.Running, WorkflowInstanceStatus.Suspended],
            Skip = page * 20,
            Take = 20,
        };
        var items = (await engine.ListInstancesAsync(query, cancellationToken)).ValueOrThrow();
        var total = (await engine.CountInstancesAsync(query, cancellationToken)).ValueOrThrow();
        return (items, total);
    }

    // 节点实例按开始时间升序，就是流程走过的轨迹
    public async ValueTask<IReadOnlyList<WorkflowNodeInstance>> TrailAsync(string instanceId, CancellationToken cancellationToken)
        => (await engine.NodeInstancesAsync(instanceId, cancellationToken)).ValueOrThrow();

    // 实例在等什么：用户任务、定时、信号、子流程、超时……（实例详情的「待恢复」）
    public async ValueTask<IReadOnlyList<WorkflowBookmarkSummary>> WaitingAsync(string instanceId, CancellationToken cancellationToken)
        => (await engine.BookmarksAsync(instanceId, cancellationToken)).ValueOrThrow();
}
```

`WorkflowInstanceQuery` 按状态（`Status` 或 `Statuses`）、定义名、关联 ID 集合、父实例、只取顶层、发起人、关键字、创建时间范围（`CreatedFrom` / `CreatedTo`）、结束时间上界（`EndedBefore`）过滤，同时给出的取交集，按排序项 `Sorts` 排序（可多个，缺省创建时间降序，字段与规则见[列表排序](/workflow/definitions#列表排序)）；过滤、排序、分页与计数都由存储执行。`Skip` 缺省 0，`Take` 缺省 100、最大 1000（`WorkflowInstanceQuery.MaxTake`）。分页越界不在构造时抛出，`ListInstancesAsync` 返回 `WORKFLOW_VALIDATION_002`（HTTP 400），不进存储；`CountInstancesAsync` 不看分页。

| 条件 | 说明 |
| --- | --- |
| `CorrelationIds` | 一组关联 ID，实例的关联 ID 是其中之一即匹配；重复的只算一个。`null` 不过滤，**空集合不匹配任何实例**（列表为空、计数为 0，不进存储）。最多 1000 个（`WorkflowInstanceQuery.MaxCorrelationIds`），不能有空白的 ID，否则列表与计数都返回 `WORKFLOW_VALIDATION_002`（消息键 `workflow.instance.query_correlations_invalid`） |
| `TopLevelOnly` | 只取顶层实例（`ParentInstanceId` 为空）。子流程继承父实例的发起人与关联 ID，「我发起的」与按单据查进度都要加它 |
| `Keyword` | 不区分大小写地包含在实例标题或关联 ID 里即匹配 |
| `Sorts` | 字段 `CreatedAt`、`EndedAt`、`Status`、`DefinitionName`、`Title` 加方向，有序、最多 5 项、每个字段一次；空或没给时创建时间降序 |

```csharp
// 审批中心「我发起的」：只取顶层，带标题搜索
var mine = new WorkflowInstanceQuery { StarterId = userId, TopLevelOnly = true, Keyword = keyword, Skip = page * 20, Take = 20 };
// 实例列表点「结束时间」列升序
var finished = new WorkflowInstanceQuery { Sorts = [new(WorkflowInstanceSortField.EndedAt)], Skip = page * 20, Take = 20 };
// 状态升序（生命周期），同状态的定义名升序，再同的创建时间降序
var grouped = new WorkflowInstanceQuery
{
    Sorts =
    [
        new(WorkflowInstanceSortField.Status),
        new(WorkflowInstanceSortField.DefinitionName),
        new(WorkflowInstanceSortField.CreatedAt, Descending: true),
    ],
    Skip = page * 20,
    Take = 20,
};

// 一页单据的审批进度：一次查询，按关联 ID 分组
var progress = (await engine.ListInstancesAsync(new WorkflowInstanceQuery
{
    DefinitionName = "permission-request",
    CorrelationIds = requests.Select(static r => r.Id.ToString()).ToList(),
    TopLevelOnly = true,
    Take = WorkflowInstanceQuery.MaxTake,
}, cancellationToken)).ValueOrThrow().ToLookup(static instance => instance.CorrelationId);
```

## 控制实例

| 方法 | 允许的状态 | 作用 |
| --- | --- | --- |
| `SuspendAsync(ids, reason)` | `Running` | 挂起，到期书签暂不推进 |
| `ResumeInstancesAsync(ids)` | `Suspended` | 恢复；不重新驱动书签，到期书签由轮询接续 |
| `CancelAsync(ids, reason)` | `Running`、`Suspended`、`Faulted` | 取消：清掉未结束节点与全部书签，`EnableCompensation` 时补偿，级联到未结束的子流程 |
| `TerminateAsync(ids, reason)` | `Running`、`Suspended`、`Faulted` | 终止：同取消，但不补偿 |
| `RetryAsync(ids)` | `Faulted` 且有故障节点 | 清故障，从故障节点再执行一次（`Attempt + 1`） |
| `SetVariablesAsync(updates)` | `Running`、`Suspended` | `new WorkflowVariableUpdate(instanceId) { Variables = …, Remove = … }`：合并变量、删除指定键 |
| `JumpAsync(jumps)` | `Running`、`Suspended`、`Faulted` | `new WorkflowJump(instanceId, targetNodeId) { Reason = …, Variables = …, ActorId = … }`：取消全部未结束节点与书签，从目标节点重新执行 |
| `MigrateAsync(migrations)` | `Running`、`Suspended` | `WorkflowMigration(instanceId, targetDefinitionId)`：迁到同名的另一个已发布版本；未结束节点在目标版本里必须存在且活动类型相同 |
| `PurgeAsync(request)` | 已结束 | `WorkflowPurgeRequest`：删除早于 `EndedBefore` 结束的实例及其节点实例与书签，可按 `Statuses` 与 `DefinitionName` 过滤，一次最多 `Limit` 条（1～1000，缺省 1000；越界返回 `WORKFLOW_VALIDATION_002`，不开事务） |

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Workflow.Abstractions.Engine;

// 改一个变量并删掉一个过期的键
await engine.SetVariablesAsync(new WorkflowVariableUpdate(instanceId) { Variables = new JsonObject { ["days"] = 2 }, Remove = ["draft"] }, cancellationToken);
// 管理员把卡住的实例跳回主管审批
await engine.JumpAsync(new WorkflowJump(instanceId, "manager") { Reason = "重新审批", ActorId = adminId }, cancellationToken);
// 迁到新发布的版本
await engine.MigrateAsync(new WorkflowMigration(instanceId, newVersion.Id), cancellationToken);
```

批量方法（这里的控制、任务与外部任务服务的操作）返回与入参等长、同序的逐项结果，空集合直接返回空结果、不开事务。自己开调用时整批一个事务：某一项失败不回滚其余项，失败的项没有写入；提交失败时每一项都报告提交的错误；在调用方的工作单元里时并入调用方的事务。取消在任何时刻都整批回滚并抛出 `OperationCanceledException`，不留下半批。

调用只操作得了自己分区的实例：别的分区的实例按不存在处理，返回 `WORKFLOW_NOTFOUND_002`，`PurgeAsync` 只清自己的分区；按书签 ID 的 `ResumeAsync` 遇到别的分区的书签返回 `WORKFLOW_NOTFOUND_003`。状态不允许时返回 `WORKFLOW_CONFLICT_001`；已经结束的实例上取消与终止幂等返回。跳转目标不存在、迁移目标不兼容返回 `WORKFLOW_VALIDATION_002`；死汇合、波次上限这类引擎级故障没有故障节点，不能重试。所有控制操作都是批量、逐项结果，单条写法在 `WorkflowEngineExtensions` 里。`XiHan:Workflow:RetentionDays` 大于 0 时，轮询器每 `HousekeepingIntervalSeconds` 逐个分区自动清理一次。

## 常见问题

### 用户点了两次提交，怎样只建一个实例

给启动请求带业务单号 `CorrelationId` 并设 `ReusePolicy = RejectActive`（拒绝）或 `ReturnActive`（返回已有实例）。查重与写入在同一把实例锁里，并发的同键启动只建一个。

### 待办里看不到任务了，任务丢了吗

查询只返回实例处于 `Running` 的任务。实例被挂起或故障时任务暂时退出结果，恢复或重试后回来；用 `BookmarksAsync(instanceId)` 能看到书签仍在。实例被取消、终止或跳转时任务才真正消失。

### 会签 60% 怎么算，剩下的人还要审吗

`ApprovalThreshold` 为百分比时按指派人数向上取整：3 人 60% 是 2 人。达到阈值节点立即完成，其余人的任务撤下；任一人给出非 `approved` 的结果立即以该结果完成；剩余人数已不可能达到阈值时以 `rejected` 完成。

### 审批人离职了，流程卡住怎么办

管理员用 `TransferAsync` 转办给别人，或 `AddAssigneesAsync` 加签；没人能操作时用 `JumpAsync` 跳到别的节点。这些操作都记在节点实例的 `history` 里。要从源头避免，指派规则写成按部门或角色解析的 `IAssigneeProvider`，不写死人。

### 外部工作者领了任务却崩了

锁到期后轮询自动解锁，别的工作者可以重新领取；活着但处理得慢就用 `ExtendLockAsync` 续锁。工作者报 `FailAsync` 带 `RetryAfter` 时任务推迟再领、`Attempt` 加一，不带时节点故障、按节点的重试策略处理。

### 怎样把实例的故障原因展示给用户

`instance.Fault` 存的是错误码、消息键与参数，不是文案：在调用里用 `instance.Fault?.Localize()` 按调用方区域渲染，要作为错误返回用 `ToError()`，详见[故障原因的展示](/workflow/activities#故障原因的展示)。

## 相关包与下一步

- [Workflow.Abstractions](/packages/workflow-abstractions)：引擎、任务服务、外部任务与查询类型
- [Workflow.InMemory](/packages/workflow-inmemory)、[Workflow.Database](/packages/workflow-database)：两种实现的轮询、锁与容量
- 相关章节：[发布与订阅](/messaging/pubsub)（订阅 `workflow.task.*` 做待办通知）、[租户解析与隔离](/multi-tenancy/resolution)
- 下一步：[活动与扩展](/workflow/activities)——内置活动、表达式、故障与补偿、自定义活动、事件、存储与配置
