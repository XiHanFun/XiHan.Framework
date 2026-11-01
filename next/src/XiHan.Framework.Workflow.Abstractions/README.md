# XiHan.Framework.Workflow.Abstractions

工作流契约与引擎（Ring 2，RFC-0021，ADR-0016 / ADR-0020 / ADR-0077）。能力 `workflow.execution@1` 由 Provider 提供三个存储、事务、实例锁与到期书签的轮询驱动；默认实现 `Workflow.InMemory`，多实例换 `Workflow.Database`。

引擎、定义目录、任务服务与外部任务服务在调用里被调用时加入调用方的事务（`IWorkflowTransaction.IsActive`）：存储写入、活动与结果处理器的写入、经发件箱发出的事件随业务提交或回滚，取得的实例锁持有到调用的服务作用域结束才释放；只读调用里的写入在执行活动之前返回 `PERSISTENCE_DENIED_001`。在调用之外（后台任务、轮询器、定时器）被调用时自己开一次经完整管道的调用（来源协议 `workflow`），在那次调用的事务里执行。数据按分区隔离：平台一个分区、每个租户一个分区，调用只读写自己的分区，平台与租户互不可见，没有全局定义。

| 契约 | 用途 |
| --- | --- |
| `WorkflowName`、`ActivityType`、`WorkflowIdentifiers` | 定义名（主题文法）、活动类型名、节点 / 转移 ID 文法与 UUID v7 标识 |
| `WorkflowDefinition`、`WorkflowNode`、`WorkflowTransition`、`WorkflowVariableDefinition`、`WorkflowRetryPolicy` | 定义：图、节点属性（`JsonObject`，字符串支持 `{{ 表达式 }}`）、输出映射、条件出边与默认出边、变量声明、重试、实例级超时（`Timeout` / `TimeoutAction`）、补偿（取消时 / 故障时） |
| `WorkflowDefinitionBuilder`、`WorkflowDefinitionValidator`、`WorkflowJson` | 流式构造、发布前校验（`details.errors`）、源生成 JSON 往返（AOT） |
| `IWorkflowDefinitionCatalog`、`ActivityDescriptor` | 定义生命周期：草稿 → 发布 → 停用 / 归档，新版本；按标识批量；`Start.Cron` 的定义在发布时排定时启动；活动描述符供设计器 |
| `IActivity`、`IResumableActivity`、`ICompensableActivity`、`ActivityContext`、`ActivityOutcome` | 活动是作用域服务，在引擎这次操作的调用里执行（同一作用域、同一事务）；返回完成 / 挂起 / 结束 / 终止，失败的 `Result` 即故障 |
| `ActivityTypes` 与内置活动 | `Start`（信号 / 定时启动、延迟启动）、`End`、`Terminate`、`SetVariable`、`Decision`、`Parallel`、`Join`、`Delay`（时长 / 时刻 / cron）、`UserTask`、`PublishEvent`、`WaitSignal`、`EventGateway`、`SubWorkflow`、`ForEach`、`Log`、`Fault`、`Compensate`、`Http`（`WorkflowHttpClient`，应用可替换）、`ExternalTask` |
| `WorkflowInstance`、`WorkflowNodeInstance`、`WorkflowBookmark`、`BookmarkKinds` | 实例与节点实例（状态、变量、输入输出、私有状态），书签（用户任务 / 定时 / 信号 / 子流程 / 重试 / 节点超时 / 外部任务 / 提醒 / 实例级超时 / 定义级定时） |
| `IWorkflowEngine`、`WorkflowEngine` | 启动（`StartAt` 延迟、`ReusePolicy` 幂等：查重与写入在同一把实例锁里，并发启动也只建一个）、消费书签、信号（恢复 + 信号启动），批量挂起 / 恢复 / 取消 / 终止 / 重试 / 改变量 / 跳转 / 迁移 / 清理，实例查询（列表、计数、详情、节点轨迹）；只看得到、操作得了调用方分区的实例与书签；波次执行、一次批量写回、事件在事务里经作用域的发布者发出 |
| `IWorkflowTaskService`、`UserTask`、`UserTaskQuery`、`UserTaskCompletion` … | 待办与查询、完成（Any / All 按阈值 / Sequential）、转办、加签、认领 / 释放、退回、催办；优先级、截止、表单键、提醒 |
| `IWorkflowExternalTaskService`、`ExternalTask` … | 外部工作者按主题领取（加锁）、完成、失败（重试或故障）、延长锁 |
| `IAssigneeProvider`、`AssigneeResolver`、`AssigneeRuleTypes` | 指派规则：内置 `User` / `Starter` / `Expression`，应用登记自己的类型 |
| `IWorkflowExpressionEvaluator`、`ExpressionScope` | 无依赖的小语言：比较、算术、成员与索引、内置函数，`{{ }}` 模板；`Evaluate` 与 `ActivityContext.EvaluateProperty` 返回 `Result<Maybe<JsonNode>>`，结果为 null（字面量、缺少的成员或键、值为 null 的变量、没有这个属性）是成功的无值，模板里渲染成空串（ADR-0059） |
| `IWorkflowDefinitionStore`、`IWorkflowInstanceStore`、`IWorkflowBookmarkStore`、`IWorkflowTransaction`、`IWorkflowInstanceLock` | Provider 契约：存储是作用域服务，每个方法的第一个参数是分区（`tenantId`，空为平台），写入的记录必须属于该分区；批量写入；实例查询带多状态 / 发起人 / 时间范围 / 分页与计数；书签的 `AvailableAsync` 以可领取时刻为键限量查询（外部任务领取把条数下推到存储）；`IWorkflowTransaction` 回答能不能加入调用方事务、加入的事务能不能写 |
| `DueBookmarkPoller`、`WorkflowOptions` | 到期书签轮询与按保留期清理（`RetentionDays`、`HousekeepingIntervalSeconds`），Provider 特性启动；每个周期覆盖平台与租户目录（`ITenantStore`，装配了多租户时）里启用的每个租户，逐个分区开调用 |
| `WorkflowTopics`、`WorkflowEventData` | `workflow.instance.*` / `node.*` / `task.*` / `external.*` / `custom` 消息（CloudEvents 信封） |

```csharp
var definition = WorkflowDefinitionBuilder.Create("leave", "请假")
    .WithTimeout(TimeSpan.FromDays(7), WorkflowTimeoutAction.Terminate)
    .AddVariable("days", required: true)
    .AddStart()
    .AddUserTask("approve", "主管审批", node => node
        .WithAssigneeRule(AssigneeRuleTypes.User, "manager", "deputy")
        .WithProperty(UserTaskActivity.CompletionPolicyProperty, "All")
        .WithProperty(UserTaskActivity.ApprovalThresholdProperty, "50%")
        .WithProperty(UserTaskActivity.ReminderAfterSecondsProperty, 86400)
        .WithTimeout(TimeSpan.FromDays(3)))
    .AddHttp("notify", "https://hr.example.com/leave/{{ days }}", "POST", configure: node => node.WithProperty(HttpActivity.BodyProperty, "{\"outcome\": \"{{ outcome }}\"}").WithOutputMapping("ticket", "outputs.http.body.ticket"))
    .AddDecision("check")
    .AddEnd("approved", outcome: "approved")
    .AddEnd("rejected", outcome: "rejected")
    .AddTransition("start", "approve").AddTransition("approve", "notify").AddTransition("notify", "check")
    .AddTransition("check", "approved", "outcome == 'approved'").AddDefaultTransition("check", "rejected")
    .Build();
var created = await catalog.CreateAsync(definition, ct);
await catalog.PublishAsync(created.ValueOrThrow().Id, ct);
var instance = await engine.StartAsync(new WorkflowStartRequest { Name = "leave", Variables = new JsonObject { ["days"] = 2 }, StarterId = "alice", CorrelationId = "leave-2026-001", ReusePolicy = WorkflowReusePolicy.RejectActive }, ct);
var todo = await tasks.PendingAsync("manager", ct);
await tasks.CompleteAsync(new UserTaskCompletion(todo.ValueOrThrow()[0].Id, "manager", UserTaskOutcomes.Approved), ct);
```

节点以结果标签完成时（用户任务的 `approved` / `rejected` / `timeout`、事件网关的信号名、`ContinueOnError` 的 `error`、活动自定的标签），标签写进实例变量 `outcome`，当前节点与后继决策节点的出边条件都按它选边；条件都不成立时才走默认出边。

定义不合法 → `WORKFLOW_VALIDATION_001`；定义未发布或在别的分区 → `WORKFLOW_NOTFOUND_001`（实例、书签在别的分区 → `_002` / `_003`）；状态不允许 → `WORKFLOW_CONFLICT_001` / `_002`；同关联已有未结束实例 → `_005`；操作者不是指派人 → `WORKFLOW_DENIED_001`；工作者没有锁 → `_002`；等锁超时 → `WORKFLOW_TIMEOUT_001`；只读调用里写入 → `PERSISTENCE_DENIED_001`；在调用之外被调用而应用不在服务状态 → `WORKFLOW_UNAVAILABLE_001`。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Messaging.Abstractions`、`XiHan.Framework.MultiTenancy.Abstractions`（租户目录）、`XiHan.Framework.Scheduling.Abstractions`（cron）。被 `Workflow.InMemory`、`Workflow.Database`、`Workflow.Conformance` 引用。

## 状态

5.x P0 + P5-11 补齐：可用（Preview）。
