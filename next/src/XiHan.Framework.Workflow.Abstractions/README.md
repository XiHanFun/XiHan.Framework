# XiHan.Framework.Workflow.Abstractions

工作流契约与引擎（Ring 2，RFC-0021，ADR-0016）。能力 `workflow.execution@1` 由 Provider 提供三个存储、实例锁与到期书签的轮询驱动；默认实现 `Workflow.InMemory`。

| 契约 | 用途 |
| --- | --- |
| `WorkflowName`、`ActivityType`、`WorkflowIdentifiers` | 定义名（主题文法）、活动类型名、节点 / 转移 ID 文法与 UUID v7 标识 |
| `WorkflowDefinition`、`WorkflowNode`、`WorkflowTransition`、`WorkflowVariableDefinition`、`WorkflowRetryPolicy` | 定义：图、节点属性（`JsonObject`，字符串支持 `{{ 表达式 }}`）、条件出边与默认出边、变量声明、重试 |
| `WorkflowDefinitionBuilder`、`WorkflowDefinitionValidator`、`WorkflowJson` | 流式构造、发布前校验（`details.errors`）、源生成 JSON 往返（AOT） |
| `IWorkflowDefinitionCatalog` | 定义生命周期：草稿 → 发布 → 停用 / 归档，新版本；按标识批量 |
| `IActivity`、`IResumableActivity`、`ICompensableActivity`、`ActivityContext`、`ActivityOutcome` | 活动是作用域服务，每个节点在新作用域、新上下文里经完整内核管道执行；返回完成 / 挂起 / 结束 / 终止，失败的 `Result` 即故障 |
| `ActivityTypes` 与内置活动 | `Start`、`End`、`Terminate`、`SetVariable`、`Decision`、`Parallel`、`Join`、`Delay`、`UserTask`、`PublishEvent`、`WaitSignal`、`SubWorkflow`、`ForEach`、`Log`、`Fault` |
| `WorkflowInstance`、`WorkflowNodeInstance`、`WorkflowBookmark`、`BookmarkKinds` | 实例与节点实例（状态、变量、输入输出、私有状态），书签（用户任务 / 定时 / 信号 / 子流程 / 重试 / 超时） |
| `IWorkflowEngine`、`WorkflowEngine` | 启动、消费书签、信号，批量挂起 / 恢复 / 取消 / 终止 / 重试；波次执行、一次批量写回、事件波次后发布 |
| `IWorkflowTaskService`、`UserTask`、`UserTaskCompletion` … | 待办、完成（Any / All / Sequential）、转办、加签 |
| `IAssigneeProvider`、`AssigneeResolver`、`AssigneeRuleTypes` | 指派规则：内置 `User` / `Starter` / `Expression`，应用登记自己的类型 |
| `IWorkflowExpressionEvaluator`、`ExpressionScope` | 无依赖的小语言：比较、算术、成员与索引、内置函数，`{{ }}` 模板 |
| `IWorkflowDefinitionStore`、`IWorkflowInstanceStore`、`IWorkflowBookmarkStore`、`IWorkflowInstanceLock` | Provider 契约：批量写入，不按租户过滤 |
| `DueBookmarkPoller` | 到期书签轮询，Provider 特性启动 |
| `WorkflowTopics`、`WorkflowEventData` | `workflow.instance.*` / `node.*` / `task.*` / `custom` 消息（CloudEvents 信封） |

```csharp
var definition = WorkflowDefinitionBuilder.Create("leave", "请假")
    .AddVariable("days", required: true)
    .AddStart()
    .AddUserTask("approve", "主管审批", node => node.WithAssigneeRule(AssigneeRuleTypes.User, "manager").WithTimeout(TimeSpan.FromDays(3)))
    .AddDecision("check")
    .AddEnd("approved", outcome: "approved")
    .AddEnd("rejected", outcome: "rejected")
    .AddTransition("start", "approve")
    .AddTransition("approve", "check")
    .AddTransition("check", "approved", "outcome == 'approved'")
    .AddDefaultTransition("check", "rejected")
    .Build();
var created = await catalog.CreateAsync(definition, ct);
await catalog.PublishAsync(created.ValueOrThrow().Id, ct);
var instance = await engine.StartAsync(new WorkflowStartRequest { Name = "leave", Variables = new JsonObject { ["days"] = 2 }, StarterId = "alice" }, ct);
var todo = await tasks.PendingAsync("manager", ct);
await tasks.CompleteAsync(new UserTaskCompletion(todo.ValueOrThrow()[0].Id, "manager", UserTaskOutcomes.Approved), ct);
```

节点以结果标签完成时（用户任务的 `approved` / `rejected` / `timeout`、`ContinueOnError` 的 `error`、活动自定的标签），标签写进实例变量 `outcome`，当前节点与后继决策节点的出边条件都按它选边；条件都不成立时才走默认出边。

定义不合法 → `WORKFLOW_VALIDATION_001`；定义未发布 → `WORKFLOW_NOTFOUND_001`；状态不允许 → `WORKFLOW_CONFLICT_001` / `_002`；操作者不是指派人 → `WORKFLOW_DENIED_001`；等锁超时 → `WORKFLOW_TIMEOUT_001`。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Messaging.Abstractions`。被 `Workflow.InMemory`、`Workflow.Conformance` 引用。

## 状态

5.x P0：可用（Preview）。
