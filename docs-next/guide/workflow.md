# 工作流

本章按使用顺序讲工作流子系统：定义流程、发布、启动实例、处理用户任务、等待外部事件、查询与控制实例，最后讲表达式、故障与补偿、自定义活动与存储。读者是要在应用里实现审批流、编排长时间运行业务的开发者。

完整 API 见 [Workflow.Abstractions 包](../packages/workflow-abstractions)。

## 模型

工作流定义是一张有向图：节点（`WorkflowNode`）执行某种活动，转移（`WorkflowTransition`）连接节点并可带条件，变量（`WorkflowVariableDefinition`）声明实例的输入。实例（`WorkflowInstance`）是定义的一次执行，状态全部落在存储里；每执行一个节点留下一条节点实例（`WorkflowNodeInstance`）。需要等待的节点（人工审批、定时、信号、子流程、外部任务、重试、超时）以书签（`WorkflowBookmark`）挂起，书签被消费时从存储恢复执行；用持久化存储时，实例能跨越进程重启长时间等待。

值模型是 JSON（RFC-0021《工作流：持久化执行》）：变量、节点属性、输入、输出、节点状态与书签载荷都是 `JsonObject`，存储以 JSON 文本持久化，序列化走源生成，Native AOT 下可用。时间一律是 UTC 的 `DateTimeOffset`。

引擎在抽象包里（ADR-0016）。它在实例锁内按「波次」推进：从队列逐个执行节点，直到队列空或实例不再运行，然后一次写回实例、节点实例与书签，事件在波次之后发布。每个节点的活动在新作用域、新调用上下文里经完整管道执行：关联 ID 是实例 ID，租户是实例的租户，调用来源是 `InvocationProtocols.Workflow`；取消令牌是宿主停止令牌，调用方断开连接不会把执行到一半的状态写回。Provider 只实现三个存储、实例锁和到期书签的轮询驱动。

应用直接使用的四个服务都是作用域服务：

| 服务 | 用途 |
| --- | --- |
| `IWorkflowDefinitionCatalog` | 定义的生命周期：草稿、发布、新版本、停用、归档、删除 |
| `IWorkflowEngine` | 启动、恢复书签、信号，以及挂起、恢复、取消、终止、重试、改变量、跳转、迁移、清理 |
| `IWorkflowTaskService` | 用户任务：待办查询、完成、转办、加签、认领与释放、退回、催办 |
| `IWorkflowExternalTaskService` | 外部任务：外部工作者按主题领取、完成、失败、延长锁 |

## 启用

| 实现 | 存储 | 实例锁 | 适用 |
| --- | --- | --- | --- |
| [Workflow.InMemory](../packages/workflow-inmemory)（默认实现，`Profiles.Full` 已包含） | 有界内存表 | 进程内信号量 | 单实例、开发、测试；重启后实例丢失 |
| [Workflow.Database](../packages/workflow-database) | 应用的关系库（`persistence.relational@1`） | 数据库租约行 | 多实例、需要持久化 |

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Workflow.InMemory;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryWorkflowFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

特性在应用进入服务状态后启动到期书签的轮询（缺省每秒一次），定时、超时、重试、提醒、定时启动都靠它推进。

## 定义流程

### 用构造器

`WorkflowDefinitionBuilder` 以流式 API 生成定义。下面是一个请假审批：主管审批，超过 3 天的再由人事三人会签、60% 通过即可，主管 3 天不处理视为超时。

```csharp
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Tasks;

public static class LeaveWorkflow
{
    public static WorkflowDefinition Build() => WorkflowDefinitionBuilder.Create("hr.leave", "请假审批")
        .WithCategory("hr")
        .WithTimeout(TimeSpan.FromDays(7), WorkflowTimeoutAction.Terminate)
        .AddVariable("applicant", required: true)
        .AddVariable("days", required: true)
        .AddVariable("approvers", required: true, description: "主管，逗号分隔")
        .AddStart()
        .AddUserTask("manager", "主管审批", node => node
            .WithAssigneeRule(AssigneeRuleTypes.Expression, "approvers")
            .WithProperty(UserTaskActivity.TitleProperty, "{{ applicant }} 请假 {{ days }} 天")
            .WithProperty(UserTaskActivity.DueAfterSecondsProperty, 86400)
            .WithTimeout(TimeSpan.FromDays(3)))
        .AddDecision("route")
        .AddUserTask("hr", "人事会签", node => node
            .WithAssigneeRule(AssigneeRuleTypes.User, "hr-1", "hr-2", "hr-3")
            .WithProperty(UserTaskActivity.CompletionPolicyProperty, "All")
            .WithProperty(UserTaskActivity.ApprovalThresholdProperty, "60%"))
        .AddEnd("approved", outcome: UserTaskOutcomes.Approved)
        .AddEnd("rejected", outcome: UserTaskOutcomes.Rejected)
        .AddTransition("start", "manager")
        .AddTransition("manager", "route")
        // 决策节点按优先级（小者先）取第一条成立的出边
        .AddTransition("route", "hr", "outcome == 'approved' && days > 3")
        .AddTransition("route", "approved", "outcome == 'approved'", priority: 1)
        .AddDefaultTransition("route", "rejected")
        .AddTransition("hr", "approved", "outcome == 'approved'")
        .AddDefaultTransition("hr", "rejected")
        .Build();
}
```

构造器为 `Start`、`End`、`Decision`、`Parallel`、`Join`、`Delay`、`UserTask`、`EventGateway`、`Http`、`ExternalTask`、`Compensate` 提供专用方法，其余活动用 `AddNode(id, activityType, displayName, configure)` 加 `WithProperty` 设置属性。节点上的通用设置：`WithRetry(maxAttempts, firstDelay, backoffFactor)`、`WithTimeout`、`WithContinueOnError`、`WithOutputMapping(variable, expression)`。定义级设置：`WithCompensation`（取消时补偿）、`WithCompensateOnFault`（故障时也补偿）、`WithTimeout(timeout, action)`（实例级超时，`Fault` 或 `Terminate`）、`WithCategory`、`WithDescription`、`WithProperties`。

### 用 JSON

流程设计器交来的是 JSON。`WorkflowJson.Serialize` 与 `WorkflowJson.DeserializeDefinition` 做往返，属性名是 camelCase，枚举是字符串，时长是 `TimeSpan` 的常量格式：

```json
{
  "id": "draft",
  "name": "hr.expense",
  "version": 1,
  "displayName": "报销",
  "variables": [{ "name": "amount", "required": true }],
  "nodes": [
    { "id": "start", "activityType": "Start" },
    {
      "id": "approve",
      "activityType": "UserTask",
      "displayName": "财务审批",
      "properties": {
        "AssigneeRules": [{ "type": "User", "values": ["finance-1"] }],
        "Title": "报销 {{ amount }} 元"
      },
      "timeout": "2.00:00:00"
    },
    {
      "id": "notify",
      "activityType": "Http",
      "properties": { "Url": "https://erp.localhost/expense", "Method": "POST", "Body": "{\"amount\": {{ amount }}}" },
      "retryPolicy": { "maxAttempts": 3, "firstDelay": "00:00:10", "backoffFactor": 2 }
    },
    { "id": "end", "activityType": "End" }
  ],
  "transitions": [
    { "id": "t1", "sourceNodeId": "start", "targetNodeId": "approve" },
    { "id": "t2", "sourceNodeId": "approve", "targetNodeId": "notify", "condition": "outcome == 'approved'" },
    { "id": "t3", "sourceNodeId": "approve", "targetNodeId": "end", "isDefault": true },
    { "id": "t4", "sourceNodeId": "notify", "targetNodeId": "end" }
  ]
}
```

```csharp
using XiHan.Framework.Workflow.Abstractions.Serialization;

var parsed = WorkflowJson.DeserializeDefinition(File.ReadAllText("expense.json"));
// 不是合法 JSON 或名字不合文法时返回 WORKFLOW_VALIDATION_001
var definition = parsed.ValueOrThrow();
```

`id`、`version`、`status` 在 `CreateAsync` 时由目录重新分配，JSON 里的值只是占位。节点没有 `properties` 或写成 `null` 时按空属性处理。

### 节点、转移与变量

::: v-pre
| 元素 | 字段 |
| --- | --- |
| `WorkflowNode` | `Id`（`^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$`，定义内唯一）、`ActivityType`、`DisplayName`、`Properties`（字符串值支持 `{{ 表达式 }}` 模板）、`RetryPolicy`、`Timeout`（挂起后多久超时）、`ContinueOnError`、`OutputMappings`（变量名 → 表达式） |
| `WorkflowTransition` | `Id`、`SourceNodeId`、`TargetNodeId`、`Condition`（表达式，空为无条件）、`Priority`（小者先）、`IsDefault` |
| `WorkflowVariableDefinition` | `Name`、`Required`、`DefaultValue`、`Description` |
:::

定义名 `WorkflowName` 用主题文法 `^[a-z0-9][a-z0-9._-]{0,127}$`，一个名字下有多个版本。活动类型 `ActivityType` 的文法是 `^[A-Za-z][A-Za-z0-9._-]{0,63}$`，内置活动用 PascalCase 名。

### 出边与结果标签

节点以结果标签完成时（用户任务的 `approved` / `rejected` / `timeout`、事件网关的信号名、`ContinueOnError` 的 `error`、活动自定的标签），标签写进节点输出，也写进实例变量 `outcome`；当前节点与后继决策节点的出边条件都按它选边。没有标签的节点不覆盖 `outcome`。

出边选择由活动登记时的 `ActivityOutgoing` 决定：

| 模式 | 行为 | 使用者 |
| --- | --- | --- |
| `AllMatched`（缺省） | 取全部无条件或条件成立的出边；一条都没有时取默认出边 | 大多数活动 |
| `Exclusive` | 按优先级取第一条无条件或条件成立的出边；没有时取默认出边 | `Decision` |
| `All` | 取全部出边 | `Parallel` |
| `None` | 不流出 | `End`、`Terminate`、`Fault` |

`AllMatched` 与 `Exclusive` 一条也选不到时实例故障。默认出边只作兜底，有条件成立时不走它（4.x 在 `AllMatched` 下总是流出默认边）。条件引用了未知变量是表达式错误，实例故障，不会悄悄当成假。

### 校验

`WorkflowDefinitionValidator` 在发布与启动前执行，失败返回 `WORKFLOW_VALIDATION_001`，`details.errors` 列出全部问题：名字与版本合法；至少一个节点；节点 ID 唯一；活动类型都已登记；恰好一个 `Start` 且没有入边；转移两端存在；条件与输出映射的表达式语法合法；每个节点最多一条默认出边；`Join` 的 `Mode`、`UserTask` 的 `CompletionPolicy`、`EmptyAssigneeStrategy` 与 `ApprovalThreshold` 取值合法；`End` 的 `Outcome` 不是空串；每个 `UserTask` 至少一个指派来源且规则类型已登记；`Start.Cron` 是合法 cron；`EventGateway` 至少一个信号；`Http` 的 `Url` 非空、`Method` 合法；所有节点从 `Start` 可达。

## 发布与版本

目录按版本管理定义。同名定义的每个版本是一条独立记录，状态是 `Draft`、`Published`、`Disabled` 或 `Archived`：

| 方法 | 允许的状态 | 作用 |
| --- | --- | --- |
| `CreateAsync(definition)` | — | 新建草稿：新 ID，版本号 = 同名最大版本 + 1，租户缺省取当前调用的租户 |
| `UpdateAsync(definition)` | `Draft` | 整体替换草稿，名字、版本、租户不变 |
| `PublishAsync(ids)` | `Draft` | 先校验再发布；带 `Start.Cron` 的定义同时排一个定时启动书签 |
| `NewVersionAsync(name)` | — | 以最新版本为底复制出一个草稿 |
| `DisableAsync(ids)` | `Published` | 停用，删除定时启动书签 |
| `ArchiveAsync(ids)` | 任意 | 归档，删除定时启动书签 |
| `DeleteAsync(ids)` | `Draft` | 删除草稿 |
| `FindAsync(id)` / `FindPublishedAsync(name, version)` / `ListAsync(name, status)` | — | 查询；`version` 为空取最新已发布版本 |
| `ActivityTypes()` | — | 已登记活动的描述符（类型、显示名、分类、输入属性），供设计器列出可用节点 |

状态不允许时返回 `WORKFLOW_CONFLICT_002`。按标识的操作都是批量、逐项结果，单条写法是 `WorkflowDefinitionCatalogExtensions` 里的扩展方法。

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;

public sealed class LeaveSetup(IWorkflowDefinitionCatalog catalog)
{
    public async ValueTask<Result<WorkflowDefinition>> PublishAsync(CancellationToken cancellationToken)
    {
        var created = await catalog.CreateAsync(LeaveWorkflow.Build(), cancellationToken);
        if (!created.TryGetValue(out var draft))
        {
            return created;
        }

        return await catalog.PublishAsync(draft.Id, cancellationToken);
    }
}
```

发布新版本不会改变旧版本的状态。按名字启动时取最新的已发布版本，已经在跑的实例留在原来的版本上，需要时用 `MigrateAsync` 迁到新版本。只想让新实例停用旧版本时，对旧版本调用 `DisableAsync`。

## 启动实例

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

`WorkflowStartRequest` 的字段：

| 字段 | 说明 |
| --- | --- |
| `Name` + `Version` / `DefinitionId` | 找定义；`Version` 为空取最新已发布版本。定义不存在或不是 `Published` → `WORKFLOW_NOTFOUND_001` |
| `Variables` | 初始变量；声明了的变量缺省时补默认值，缺必填 → `WORKFLOW_VALIDATION_002` |
| `StarterId` | 发起人，`Starter` 指派规则与 `ExcludeStarter` 用它 |
| `CorrelationId` | 业务关联 ID，信号按它匹配，重用策略按它查重 |
| `ReusePolicy` | `AllowDuplicate`（缺省）、`RejectActive`（已有未结束实例 → `WORKFLOW_CONFLICT_005`）、`ReturnActive`（返回已有实例）；没有 `CorrelationId` 时忽略 |
| `StartAt` | 延迟启动：开始节点以定时书签挂起到该时刻 |
| `TenantId` | 实例租户，缺省取当前调用的租户 |
| `DisplayName`、`InstanceId` | 实例显示名与指定的实例 ID |

返回的是第一个波次结束后的实例：流程一路走到 `End` 时状态已是 `Completed`，停在用户任务上时是 `Running`。定义有实例级超时时，启动时就排好 `InstanceTimeout` 书签，到期按 `TimeoutAction` 让实例故障或终止。

### 信号启动与定时启动

`Start` 节点的两个属性让定义自己启动：

- `SignalName`：`SignalAsync(name, payload, correlationId)` 在恢复匹配的信号书签之后，为每个 `Start.SignalName` 等于该名字的已发布定义（调用方租户或无租户的）启动一个实例，载荷写进变量 `signal`，关联 ID 是信号的关联 ID。
- `Cron`（可带 `TimeZone`）：发布时建一个定义级 `DefinitionTimer` 书签，到期由轮询启动一个实例并推算下一次；停用、归档、删除定义时删除书签。文法与调度相同，见[调度与后台作业](./scheduling#cron-文法)。

```csharp
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;

var onOrder = WorkflowDefinitionBuilder.Create("orders.fulfil")
    .AddNode("start", ActivityTypes.Start, configure: node => node.WithProperty(StartActivity.SignalNameProperty, "order.created"))
    .AddExternalTask("ship", "warehouse.ship")
    .AddEnd()
    .AddTransition("start", "ship").AddTransition("ship", "end")
    .Build();

var nightly = WorkflowDefinitionBuilder.Create("finance.reconcile")
    .AddNode("start", ActivityTypes.Start, configure: node => node
        .WithProperty(StartActivity.CronProperty, "0 0 2 * * *")
        .WithProperty(StartActivity.TimeZoneProperty, "Asia/Shanghai"))
    .AddNode("log", ActivityTypes.Log, configure: node => node.WithProperty(LogActivity.MessageProperty, "reconcile started"))
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

`IWorkflowTaskService` 的操作：

| 方法 | 作用 | 主要错误 |
| --- | --- | --- |
| `PendingAsync(assigneeId)` / `PendingByInstanceAsync(instanceId)` | 某人或某实例的待办 | — |
| `QueryAsync(UserTaskQuery)` | 指派人或实例必填其一，可按定义名、节点、创建时间过滤，分页（`Limit` 缺省 100、最大 1000） | `WORKFLOW_VALIDATION_002` |
| `CompleteAsync(completions)` | 完成任务，可带结果、意见与变量 | `WORKFLOW_DENIED_001`（不是指派人）、`WORKFLOW_VALIDATION_002`（结果不允许）、`WORKFLOW_NOTFOUND_003` |
| `TransferAsync(transfers)` | 转办给另一个人 | `WORKFLOW_CONFLICT_004`（目标已是指派人） |
| `AddAssigneesAsync(additions)` | 加签：`Any` / `All` 立即加任务，`Sequential` 排到队尾 | `WORKFLOW_CONFLICT_004` |
| `ClaimAsync(claims)` / `ReleaseAsync(releases)` | `Any` 策略下认领（撤下其他人的任务）与释放 | `WORKFLOW_CONFLICT_004`（已被认领）、`WORKFLOW_VALIDATION_002`（不是 `Any`） |
| `ReturnAsync(returns)` | 退回：目标缺省为本节点之前最近完成的用户任务节点，没有则退回 `Start` 之后的首个节点；以跳转实现 | `WORKFLOW_VALIDATION_002` |
| `UrgeAsync(urges)` | 催办：发布 `workflow.task.urged`，不改状态 | — |

查询只返回当前租户、实例处于运行中的任务。节点实例的 `State` 记录 `assignees`、`approved`、`rejected`、`claimedBy` 与 `history`（每条含 `actorId`、`outcome`、`comment`、`at`），审批记录从这里读。

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Tasks;

public sealed class Inbox(IWorkflowTaskService tasks)
{
    public async ValueTask<IReadOnlyList<UserTask>> ListAsync(string userId, CancellationToken cancellationToken)
        => (await tasks.QueryAsync(new UserTaskQuery { AssigneeId = userId, Limit = 50 }, cancellationToken)).ValueOrThrow();

    public ValueTask<Result<WorkflowInstance>> ApproveAsync(string taskId, string userId, string? comment, CancellationToken cancellationToken)
        => tasks.CompleteAsync(new UserTaskCompletion(taskId, userId, UserTaskOutcomes.Approved, comment), cancellationToken);

    public ValueTask<Result> HandOverAsync(string taskId, string userId, string colleague, CancellationToken cancellationToken)
        => tasks.TransferAsync(new UserTaskTransfer(taskId, userId, colleague, "出差，转同事处理"), cancellationToken);

    public ValueTask<Result<WorkflowInstance>> SendBackAsync(string taskId, string userId, CancellationToken cancellationToken)
        => tasks.ReturnAsync(new UserTaskReturn(taskId, userId, Comment: "材料不全"), cancellationToken);
}
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

`ResumeAsync(bookmarkId, inputs, expectedKey)` 消费一个书签并恢复节点；实例不在运行中，或 `expectedKey` 与书签的 `Key` 不同时返回 `WORKFLOW_CONFLICT_003`，轮询器遇到这类书签把到期时间后推 `NotResumableBackoff`（缺省 5 分钟）。`SignalAsync(name, payload, correlationId)` 消费当前租户下匹配的全部信号书签（书签的 `CorrelationId` 为空时接受任意关联），再按信号启动规则启动实例，返回消费数加启动数。

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
                await external.CompleteAsync(new ExternalTaskCompletion(task.Id, "worker-1", new JsonObject { ["shipped"] = true }), cancellationToken);
            }
            else
            {
                // 带 RetryAfter：解锁并推迟，任务的 Attempt 加一；不带则节点以该错误故障，按节点重试策略处理
                await external.FailAsync(new ExternalTaskFailure(task.Id, "worker-1", "order missing", TimeSpan.FromMinutes(1)), cancellationToken);
            }
        }
    }
}
```

领取只返回当前租户下该主题未锁定（或锁已过期）且已到可领取时刻的任务；`ExtendLockAsync` 延长锁。没有持有未过期的锁时完成、失败、延长都返回 `WORKFLOW_DENIED_002`。锁到期的任务由轮询自动解锁，别的工作者可以重新领取。

## 查询实例

实例与节点实例的查询直接用存储契约 `IWorkflowInstanceStore`（单例）。

::: warning 存储查询不按租户过滤
`WorkflowInstanceQuery.TenantId` 为空时不限定租户，返回所有租户的实例。多租户应用必须在查询里写明当前租户。
:::

```csharp
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;

public sealed class LeaveReports(IWorkflowInstanceStore instances)
{
    public async ValueTask<(IReadOnlyList<WorkflowInstance> Items, long Total)> PendingAsync(string? tenantId, int page, CancellationToken cancellationToken)
    {
        var query = new WorkflowInstanceQuery
        {
            DefinitionName = "hr.leave",
            Statuses = [WorkflowInstanceStatus.Running, WorkflowInstanceStatus.Suspended],
            TenantId = tenantId,
            Skip = page * 20,
            Limit = 20,
        };
        var items = (await instances.ListAsync(query, cancellationToken)).ValueOrThrow();
        var total = (await instances.CountAsync(query, cancellationToken)).ValueOrThrow();
        return (items, total);
    }

    // 节点实例按开始时间升序，就是流程走过的轨迹
    public async ValueTask<IReadOnlyList<WorkflowNodeInstance>> TrailAsync(string instanceId, CancellationToken cancellationToken)
        => (await instances.NodeInstancesAsync(instanceId, cancellationToken)).ValueOrThrow();
}
```

`WorkflowInstanceQuery` 按状态（`Status` 或 `Statuses`）、定义名、关联 ID、父实例、发起人、租户、创建时间范围、结束时间上界过滤，按创建时间降序；`Limit` 缺省 100、最大 1000（`WorkflowInstanceQuery.MaxLimit`）。实例的 `Status` 是 `Running`、`Suspended`、`Completed`、`Canceled`、`Faulted` 或 `Terminated`，故障信息在 `Fault`（错误码与消息）、`FaultNodeId`、`FaultNodeInstanceId`，`End` 节点给出的结果在 `Outcome`。

## 控制实例

| 方法 | 允许的状态 | 作用 |
| --- | --- | --- |
| `SuspendAsync(ids, reason)` | `Running` | 挂起，到期书签暂不推进 |
| `ResumeInstancesAsync(ids)` | `Suspended` | 恢复；不重新驱动书签，到期书签由轮询接续 |
| `CancelAsync(ids, reason)` | `Running`、`Suspended`、`Faulted` | 取消：清掉未结束节点与全部书签，`EnableCompensation` 时补偿，级联到未结束的子流程 |
| `TerminateAsync(ids, reason)` | `Running`、`Suspended`、`Faulted` | 终止：同取消，但不补偿 |
| `RetryAsync(ids)` | `Faulted` 且有故障节点 | 清故障，从故障节点再执行一次（`Attempt + 1`） |
| `SetVariablesAsync(updates)` | `Running`、`Suspended` | 合并变量、删除指定键 |
| `JumpAsync(jumps)` | `Running`、`Suspended`、`Faulted` | 取消全部未结束节点与书签，从目标节点重新执行 |
| `MigrateAsync(migrations)` | `Running`、`Suspended` | 迁到同名的另一个已发布版本；未结束节点在目标版本里必须存在且活动类型相同 |
| `PurgeAsync(request)` | 已结束 | 删除早于给定时刻结束的实例及其节点实例与书签，可按状态与定义名过滤，一次最多 1000 条 |

状态不允许时返回 `WORKFLOW_CONFLICT_001`；已经结束的实例上取消与终止幂等返回。跳转目标不存在、迁移目标不兼容返回 `WORKFLOW_VALIDATION_002`；死汇合、波次上限这类引擎级故障没有故障节点，不能重试。所有控制操作都是批量、逐项结果，单条写法在 `WorkflowEngineExtensions` 里。`XiHan:Workflow:RetentionDays` 大于 0 时，轮询器每 `HousekeepingIntervalSeconds` 自动清理一次。

## 内置活动

`AddWorkflowEngine` 登记 19 个内置活动（`ActivityTypes`）：

| 活动 | 出边 | 主要属性 |
| --- | --- | --- |
| `Start` | AllMatched | `SignalName`、`Cron`、`TimeZone` |
| `End` | None | `Outcome`：先调用全部 `IWorkflowOutcomeHandler`，再结束实例 |
| `Terminate` | None | `Reason` |
| `SetVariable` | AllMatched | `Values`（字面量对象）、`Expressions`（变量名 → 表达式） |
| `Decision` | Exclusive | — |
| `Parallel` | All | — |
| `Join` | AllMatched | `Mode`：`WaitAll`（缺省，入边全部到达才执行）/ `WaitAny`（首个到达执行） |
| `Delay` | AllMatched | `Duration`（秒或表达式）、`Until`（时刻表达式）、`Cron` + `TimeZone` |
| `UserTask` | AllMatched | 见[处理用户任务](#处理用户任务) |
| `PublishEvent` | AllMatched | `EventName`、`Payload`：发布 `workflow.custom` 消息 |
| `WaitSignal` | AllMatched | `SignalName`、`AcceptAnyCorrelation` |
| `EventGateway` | AllMatched | `Signals`、`TimeoutSeconds`、`AcceptAnyCorrelation`：先到的信号胜出，结果标签是信号名或 `timeout`，输出 `signal`、`payload` |
| `SubWorkflow` | AllMatched | `DefinitionName`、`DefinitionVersion`、`Variables`、`VariableExpressions`、`WaitForCompletion`、`ResultVariable`、`FailOnChildFault` |
| `ForEach` | AllMatched | `Items`（求值为数组）、`ItemVariable`、`Parallel`、`FailFast`，子流程属性同 `SubWorkflow` |
| `Log` | AllMatched | `Message`、`Level` |
| `Fault` | None | `Message`：让实例故障 |
| `Compensate` | AllMatched | `NodeIds`：对指定的已完成节点（缺省为之前全部已完成节点）逆序补偿后继续 |
| `Http` | AllMatched | `Url`、`Method`、`Headers`、`Body`、`ContentType`、`TimeoutSeconds`、`ResultVariable`（缺省 `http`）、`FailOnErrorStatus`（缺省 `true`）；输出 `{ status, headers, body }`，JSON 响应解析为对象 |
| `ExternalTask` | AllMatched | `Topic`、`Payload`；锁的时长由领取方在 `FetchAsync` 里给出 |

`Http` 经单例 `WorkflowHttpClient` 发请求，超时缺省 `XiHan:Workflow:HttpTimeoutSeconds`（30 秒）。需要代理或认证处理器时，在装配前注册自己的实例 `new WorkflowHttpClient(httpClient)` 替换它。5.0 不提供 `Script` 活动：表达式语言与 `SetVariable` 覆盖了常见场景，脚本引擎属于第三方依赖。

子流程的子实例继承父实例的租户、关联 ID 与发起人，深度加一，超过 `MaxSubWorkflowDepth`（缺省 16）时返回 `WORKFLOW_VALIDATION_002`；子实例结束时以 `childInstanceId`、`childStatus`、`childVariables`、`childFault` 回调父实例。

## 表达式

条件、模板、输出映射、表达式指派都用同一个无依赖的小语言（`IWorkflowExpressionEvaluator`）：

| 类别 | 内容 |
| --- | --- |
| 运算符 | `\|\|`、`&&`、`==`、`!=`、`<`、`<=`、`>`、`>=`、`+`、`-`、`*`、`/`、`%`、一元 `!` 与 `-` |
| 访问 | 成员 `.x`、索引 `[i]` |
| 字面量 | 数字（decimal）、单双引号字符串、`true`、`false`、`null` |
| 函数（不区分大小写） | `len`、`contains`、`startsWith`、`endsWith`、`upper`、`lower`、`trim`、`isNullOrEmpty`、`abs`、`min`、`max`、`round`、`toNumber`、`toString`、`now`、`date` |

::: v-pre
`+` 任一侧是字符串即拼接；`==` 跨数字类型宽松比较，字符串按序数比较。标识符是实例变量，输出映射里另有 `outputs`（节点输出）。节点属性里的字符串支持 `{{ 表达式 }}` 模板，引号内的 `}}` 不算结束。
:::

空值的语义（ADR-0059）：`Evaluate` 与 `ActivityContext.EvaluateProperty` 返回 `Result<Maybe<JsonNode>>`。结果为 null，包括字面量 `null`、缺少的成员或键、值为 null 的变量、没有这个属性，都是成功的无值，不是失败。下游的处理：

- `EvaluateCondition` 遇到无值失败，因为无值不是布尔。
- `Render` 把无值渲染成空串。
- 写回变量、输出与子流程变量时，无值写成 JSON 的 `null`。

**未知的根变量是错误，不是无值**：`amount > 100` 在没有 `amount` 变量时返回 `WORKFLOW_VALIDATION_003`，实例故障；判断可选变量用在启动时给默认值，或者把它放进对象里按成员访问。已解析的表达式按文本缓存，上限 `ExpressionCacheSize`（缺省 10000），满了整体清空重来。

## 故障、重试与补偿

节点失败（活动返回失败的 `Result`，或抛出异常并被翻译为 `WORKFLOW_UNEXPECTED_001`）时按顺序处理：

1. 有重试策略且未用尽：节点记为 `Faulted`，插入 `Retry` 书签，第 n 次尝试失败后等 `FirstDelay × BackoffFactor^(n−1)`，实例保持运行。`WorkflowRetryPolicy.MaxAttempts` 含首次（至少 1，1 为不重试），`FirstDelay` 缺省 10 秒，`BackoffFactor` 至少 1、缺省 2。
2. `ContinueOnError`：节点记为 `Faulted`，变量 `lastError` 写入错误码与消息，以 `outcome = "error"` 流出。
3. 否则实例 `Faulted`，记录 `Fault`、`FaultNodeId`、`FaultNodeInstanceId`；`CompensateOnFault` 时执行补偿。之后可以 `RetryAsync` 从故障节点重来，或 `JumpAsync` 改走别的节点。

补偿按完成顺序的逆序调用已完成节点的 `ICompensableActivity.CompensateAsync`，节点状态改为 `Compensated`。三个入口：`EnableCompensation` 的定义被取消时、`CompensateOnFault` 的定义故障时、流程走到 `Compensate` 节点时（BPMN 的补偿抛出事件）。

一个波次执行的节点数超过 `MaxNodeExecutionsPerBurst`（缺省 1000）时实例故障，防止条件写错导致的死循环；波次结束时仍有凑不齐的汇合而又没有书签，实例同样故障。

## 自定义活动

活动实现 `IActivity`（需要书签恢复的再实现 `IResumableActivity`，需要补偿的实现 `ICompensableActivity`），是作用域服务：

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;

public interface ISmsSender
{
    ValueTask<Result> SendAsync(string phone, string text, CancellationToken cancellationToken);
}

public sealed class SmsActivity(ISmsSender sms) : IActivity
{
    public const string TypeName = "Sms";

    public async ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        // RenderProperty 渲染 {{ }} 模板，缺少的变量渲染成空串
        var phone = context.RenderProperty("Phone");
        var text = context.RenderProperty("Text");
        if (!phone.TryGetValue(out var to) || !text.TryGetValue(out var body))
        {
            return Result.Failure<ActivityOutcome>((phone.Error ?? text.Error)!);
        }

        var sent = await sms.SendAsync(to, body, cancellationToken);
        if (sent.IsFailure)
        {
            // 失败即节点故障，按节点的重试策略处理
            return Result.Failure<ActivityOutcome>(sent.Error!);
        }

        return Result.Success(ActivityOutcome.Complete(new JsonObject { ["smsSent"] = true }));
    }
}
```

::: v-pre
`builder.Services.AddActivity<SmsActivity>(SmsActivity.TypeName)` 登记后，定义里写 `AddNode("notify", SmsActivity.TypeName, "短信通知", node => node.WithProperty("Phone", "{{ phone }}").WithProperty("Text", "申请已 {{ outcome }}"))`。`AddActivity` 的第三个参数是 `ActivityOutgoing`，第四个是给设计器的 `ActivityDescriptor`。活动的返回：
:::

| `ActivityOutcome` | 含义 |
| --- | --- |
| `Complete(outputs, label)` | 完成，输出合并进实例变量，`label` 是结果标签 |
| `Suspend(bookmarks)` / `SuspendWithOutputs(outputs, bookmarks)` | 以书签挂起 |
| `EndInstance(outputs, label)` | 结束整个实例 |
| `TerminateInstance(reason)` | 终止实例 |
| `CompleteWithChildren` / `SuspendWithChildren` | 同时启动子实例 |

`ActivityContext` 提供 `Instance`、`Node`、`NodeInstance`、`Variables`、`State`（节点私有状态）、`Services`、`Invocation`（本次执行的调用上下文）与 `Clock`。`End` 节点带 `Outcome` 时，引擎先调用全部 `IWorkflowOutcomeHandler`（`AddWorkflowOutcomeHandler<T>()` 登记）再结束实例，审批结果回写业务表适合放在这里。

## 事件

装配了消息能力时，引擎发布 CloudEvents 信封：`Source` 为 `/workflow`，`Type` 是主题，`Data` 是 `WorkflowEventData` 的 JSON，扩展属性带 `tenant` 与 `correlation`。没有发布者时跳过，发布失败只记日志。

| 主题前缀 | 主题 |
| --- | --- |
| `workflow.instance.*` | `started`、`completed`、`faulted`、`canceled`、`terminated`、`suspended`、`resumed`、`jumped`、`migrated`、`timedout` |
| `workflow.node.*` | `executing`、`executed`、`faulted`（`WillRetry` 标出是否会重试） |
| `workflow.task.*` | `created`、`completed`、`transferred`、`claimed`、`released`、`returned`、`urged`、`reminder` |
| `workflow.external.*` | `created`、`completed`、`failed` |
| `workflow.custom` | `PublishEvent` 活动发出，`Subject` 是事件名 |

待办通知、催办短信、审批结果同步都订阅这些主题来做，见[消息与事件](./messaging)。

## 存储与实例锁

Provider 实现四个契约：`IWorkflowDefinitionStore`、`IWorkflowInstanceStore`、`IWorkflowBookmarkStore`、`IWorkflowInstanceLock`。三个存储都按批量写入、不按租户过滤、不做乐观并发：写入由实例锁串行化。`IWorkflowInstanceLock.AcquireAsync(instanceId)` 等待超过 `LockTimeout`（缺省 10 秒）返回 `WORKFLOW_TIMEOUT_001`，锁不可重入。存储不可达 → `WORKFLOW_UNAVAILABLE_001`，内存实现容量已满 → `WORKFLOW_UNAVAILABLE_002`。

| 方面 | Workflow.InMemory | Workflow.Database |
| --- | --- | --- |
| 存储 | 有界内存表，保存与返回深拷贝；定义 1000、实例 10000、节点实例 100000、书签 100000 | 查询列加整份模型的 JSON 文档，经仓储读写 |
| 事务 | 无 | 每个存储操作一次独立的管道调用与事务，不加入调用方的工作单元 |
| 实例锁 | 每个实例一个进程内信号量 | 租约行：插入即拿到，过期按并发戳接管，持有期间每三分之一租约续约（`LockLeaseSeconds` 缺省 60） |
| 轮询 | 本进程轮询 | 只在持有 `workflow.poller` 租约的节点上轮询 |
| 重启 | 实例全部丢失 | 实例与书签保留，重启后接着推进 |
| 多实例 | 不支持 | 多个节点共用一个库 |

## 配置

引擎选项在 `XiHan:Workflow`（`WorkflowOptions`），内存与数据库实现读同一节：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `MaxNodeExecutionsPerBurst` | 整数 | 1000 | 一个波次最多执行的节点数 |
| `MaxSubWorkflowDepth` | 整数 | 16 | 子流程嵌套深度上限 |
| `LockTimeoutSeconds` | 秒 | 10 | 等实例锁的上限 |
| `NotResumableBackoffSeconds` | 秒 | 300 | 不可恢复的到期书签后推多久 |
| `ExpressionCacheSize` | 整数 | 10000 | 表达式缓存条目上限 |
| `RetentionDays` | 天 | 0 | 已结束实例的保留期，0 为不自动清理 |
| `HousekeepingIntervalSeconds` | 秒 | 3600 | 自动清理的周期 |
| `HttpTimeoutSeconds` | 秒 | 30 | `Http` 活动的缺省超时 |

```json
{
  "XiHan": {
    "Workflow": {
      "LockTimeoutSeconds": 10,
      "RetentionDays": 90,
      "InMemory": { "PollIntervalSeconds": 1, "BatchSize": 100 }
    }
  }
}
```

解析不了的值（如把数字写成文字）被忽略、沿用缺省值，启动时不报错；能解析但超出取值范围的数字（如 `LockTimeoutSeconds` 为 0）启动时抛 `ArgumentOutOfRangeException`。Provider 自己的键见 [Workflow.InMemory](../packages/workflow-inmemory#配置) 与 [Workflow.Database](../packages/workflow-database#配置)。

## 错误码

| 错误码 | 含义 |
| --- | --- |
| `WORKFLOW_VALIDATION_001` | 定义不合法，`details.errors` 列出全部问题 |
| `WORKFLOW_VALIDATION_002` | 输入不合法：缺必填变量、子流程深度超限、任务结果不允许、跳转目标不存在、迁移目标不兼容、任务查询缺条件 |
| `WORKFLOW_VALIDATION_003` | 表达式语法不合法或求值失败 |
| `WORKFLOW_NOTFOUND_001` | 定义不存在或没有已发布版本 |
| `WORKFLOW_NOTFOUND_002` | 实例不存在 |
| `WORKFLOW_NOTFOUND_003` | 书签（用户任务）不存在 |
| `WORKFLOW_NOTFOUND_004` | 活动类型或指派规则类型未登记 |
| `WORKFLOW_CONFLICT_001` | 实例状态不允许该操作 |
| `WORKFLOW_CONFLICT_002` | 定义状态不允许该操作 |
| `WORKFLOW_CONFLICT_003` | 书签不可恢复：实例不在运行中、键不匹配 |
| `WORKFLOW_CONFLICT_004` | 指派人已在任务上，或任务已被认领 |
| `WORKFLOW_CONFLICT_005` | 同一关联 ID 下已有未结束的实例（`RejectActive`） |
| `WORKFLOW_DENIED_001` | 操作者不是任务的指派人 |
| `WORKFLOW_DENIED_002` | 工作者没有持有外部任务的锁，或锁已过期 |
| `WORKFLOW_TIMEOUT_001` | 等实例锁超时 |
| `WORKFLOW_UNAVAILABLE_001` | 工作流存储不可达 |
| `WORKFLOW_UNAVAILABLE_002` | 工作流存储容量已满 |
| `WORKFLOW_UNEXPECTED_001` | 活动抛出未处理异常，或引擎内部故障（波次上限、死汇合） |

消息模板在消息目录 `workflow` 里，十种区域都有译文。

## 与其他能力的关系

- `Delay` 的 `Cron` 与 `Start` 的定时启动复用调度子系统的 `CronExpression`，文法见[调度与后台作业](./scheduling#cron-文法)；工作流自己的到期书签由轮询器推进，不占用调度器的作业。
- 活动经完整管道执行，租户、授权、审计与追踪的行为与[执行管道](./pipeline)一致；实例的租户见[多租户](./multi-tenancy)。
- 数据库实现经 `persistence.relational@1` 的仓储读写，见[持久化](./persistence)。
- 4.x 的 `IWorkflowDefinitionManager` 换成 `IWorkflowDefinitionCatalog`，`IWorkflowUserTaskService` 换成 `IWorkflowTaskService`，变量从 `Dictionary<string, object?>` 换成 `JsonObject`，异常换成 `Result`；完整对照见[从 4.x 迁移](./upgrade)。

## 相关

- [Workflow.Abstractions](../packages/workflow-abstractions)
- [Workflow.InMemory](../packages/workflow-inmemory)
- [Workflow.Database](../packages/workflow-database)
- [测试与一致性套件](./testing)
- 规范：RFC-0021《工作流：持久化执行》；ADR-0016、ADR-0020、ADR-0059
