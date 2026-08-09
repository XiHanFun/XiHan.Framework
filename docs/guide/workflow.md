# 工作流

审批、长流程编排这类「跑一半要停下来等人、等定时、等外部系统」的业务，用代码里的 `await` 是撑不住的——进程重启就全丢了。本章讲怎么用框架的图执行引擎把流程画成数据、把等待点落成书签，以及在集群里怎么保证不重复推进。

完整 API 与全部配置项见 [Workflow 包文档](../packages/workflow) 与 [Workflow.Abstractions](../packages/workflow-abstractions)。

## 什么时候用它

| 场景 | 选型 |
| --- | --- |
| 审批流：请假、报销、多级会签、依次审批、转办加签 | 工作流 |
| 长时间运行的业务编排：跨天跨周、中途要等人/等外部系统 | 工作流 |
| 流程要可配置、由管理端画出来，不改代码重新部署 | 工作流 |
| 「每天凌晨跑一段代码」 | [Tasks](../packages/tasks) 的调度器 |
| 「提交后异步做一件事」 | `IBackgroundJobManager` |

判断标准很简单：**中间是否存在需要外部输入才能继续的等待点**。有，就用工作流；没有，用任务或后台作业更轻。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Workflow
```

```csharp
[DependsOn(typeof(XiHanWorkflowModule))]
public class MyModule : XiHanModule { }
```

模块的 `ConfigureServices` 调用 `AddXiHanWorkflow(configuration)`，一次性接好：引擎 `IWorkflowEngine`、定义管理器 `IWorkflowDefinitionManager`、人工任务服务 `IWorkflowUserTaskService`、表达式求值器、活动注册表、事件发布器、三个存储端口、17 个内置活动、`Script` 活动用的 `IScriptEngine`、`Http` 活动用的命名 `HttpClient`（名称见 `HttpRequestActivity.HttpClientName`），以及托管服务 `WorkflowTimerWorker`。

所有注册都是 `TryAdd` 语义，业务侧可以整块换掉。

::: danger 默认存储在内存里
`InMemoryWorkflowDefinitionStore` / `InMemoryWorkflowInstanceStore` / `InMemoryWorkflowBookmarkStore` 是**进程内**实现：进程重启，全部定义、实例与书签消失，也不跨实例共享。

只适合开发和单测。生产环境必须实现三个 Store 并 `Replace` 掉默认注册，见[换成持久化存储](#换成持久化存储)。
:::

## 图执行模型

流程定义 `WorkflowDefinition` 就是一张有向图：

- **节点** `WorkflowNode`：`Id`（定义内唯一）+ `ActivityType`（活动类型编码）+ `Properties`（活动自定义属性字典）。
- **连线** `WorkflowTransition`：`SourceNodeId` → `TargetNodeId`，可带 `Condition`（条件表达式）、`Priority`（优先级）、`IsDefault`（兜底分支）。
- **变量声明** `Variables`：启动时校验必填项、填充默认值。

引擎不编译定义，直接解释执行。

### 令牌与执行批次

`StartAsync` / `ResumeBookmarkAsync` / `RetryAsync` 都是「尽可能往前推」：拿到实例分布式锁后，从当前节点开始沿连线推进令牌，同步链路能走多远走多远——一次调用可能直接从开始节点跑到实例完成，也可能一步就停在审批节点上。

一个批次内的节点执行数受 `MaxNodeExecutionsPerBurst`（默认 1000）限制，超限直接把实例置为故障，防止定义里的环路把线程转死。

::: tip 挂起不占线程
实例挂在书签上时状态仍是 `Running`（该枚举值的语义是「运行中，含等待恢复的空闲状态」），但没有任何线程、任何定时器为它常驻。等待成本只是数据库里的一行书签。
:::

### 出边怎么选

每个活动用 `[WorkflowActivity]` 声明自己的 `OutgoingBehavior`，引擎据此决定令牌沿哪些出边走：

| 行为 | 语义 | 谁用 |
| --- | --- | --- |
| `AllMatched` | 走所有条件满足（或无条件）的出边；一条都不满足则故障 | 默认值，普通活动 |
| `Exclusive` | 按 `Priority` 升序取**第一条**满足的，都不满足走默认边，仍没有则故障 | `Decision` |
| `All` | 忽略条件，所有出边全走（并行扇出） | `Parallel` |
| `None` | 不再流转 | `End` / `Terminate` / `Fault` |

条件表达式的求值上下文 = 实例变量 + 一个注入的 `outcome` 变量（当前活动的结果值）。节点没有出边时，该令牌隐式结束。

### 汇聚

`Join` 节点的到达计数由引擎按波次维护在实例的 `JoinStates` 上，节点属性 `Mode` 决定触发条件：

- `WaitAll`（默认）：所有入边都到齐才放行。
- `WaitAny`：首个到达即触发，同波次后续到达被吞掉。

::: warning 环路不要穿 WaitAny
`WaitAny` 会吞掉同波次的后续令牌，环路的回边正好落在这一类里，令牌会消失。**有环的定义请一律用 `WaitAll`。**
:::

批次收尾时，若队列空且实例已无任何书签，引擎才判定完成；此时如果还有未触发的汇聚波次，说明某条分支已经死掉，引擎按 fail-closed 把实例置为故障，而不是静默跳过汇聚之后的所有节点。

## 定义流程

### 用构建器写

```csharp
using XiHan.Framework.Workflow.Builders;

var definition = WorkflowDefinitionBuilder.Create("leave-request", "请假审批")
    .WithCategory("行政")
    .AddVariable("amount", required: true)
    .AddVariable("applicant")
    .AddStart()
    .AddDecision("gateway", "金额判定")
    .AddUserTask("manager", "经理审批", node => node
        .WithProperty("Assignees", new List<string> { "1001" })
        .WithProperty("CompletionPolicy", "Any")
        .WithTimeout(3 * 24 * 3600))
    .AddEnd()
    .AddTransition("start", "gateway")
    .AddTransition("gateway", "manager", "amount > 10000")   // 独占网关：条件分支
    .AddDefaultTransition("gateway", "end")                  // 独占网关：兜底分支
    .AddTransition("manager", "end")
    .Build();
```

节点构建器上还有 `WithRetry(maxAttempts, firstDelaySeconds, backoffFactor)`、`WithTimeout(timeoutSeconds)`、`WithContinueOnError()`、`WithProperties(...)`。

::: warning 默认边只对独占网关有意义
`AddDefaultTransition` 的兜底语义靠 `Exclusive` 行为实现。普通节点（含人工任务）是 `AllMatched`：默认边**永远被选中**，再叠一条满足条件的边就会同时放出两个令牌。给审批结果分流时，请写成条件互斥且穷尽的若干条普通连线，例如 `outcome == 'approved'` 与 `outcome != 'approved'`。
:::

### 用 JSON 存和改

`WorkflowDefinitionJsonSerializer.Serialize` / `Deserialize` 提供稳定的 camelCase + 枚举字符串格式，是落库和前端设计器的交换格式。定义存进数据库、由管理端可视化编辑，就靠这一对方法。

::: tip JSON 里的属性值是 JsonElement
从 JSON 反序列化回来的 `Node.Properties` 值是 `JsonElement`，直接 `is string` 会失配。活动里读属性一律走 `GetProperty<T>(context, name)`（内部经 `WorkflowValueConverter` 归一化），别自己拆字典。
:::

### 版本与发布

`IWorkflowDefinitionManager` 管定义的生命周期，状态流转是 `Draft` → `Published` → `Disabled` / `Archived`：

| 方法 | 约束 |
| --- | --- |
| `CreateAsync` | 版本号自动取该编码下最大版本 + 1，落为草稿 |
| `UpdateDraftAsync` | **仅草稿可改**；编码、版本、创建信息不可变 |
| `PublishAsync` | 发布前跑结构校验，失败抛 `WorkflowDefinitionValidationException` |
| `CreateNewVersionAsync` | 基于最新版本深拷贝出新草稿 |
| `DisableAsync` | 仅已发布可停用；停用后不能启动新实例，**存量实例继续跑** |
| `DeleteAsync` | 仅草稿可删 |

::: warning 已发布定义是不可变的
实例永远绑定它启动时那个具体版本（`DefinitionId` + `DefinitionVersion`）。改流程的正确姿势是 `CreateNewVersionAsync` 出新草稿、改完发布，而不是去动已发布版本——存量实例还在按老图跑。
:::

发布校验（`WorkflowDefinitionValidator`）会一次性报出所有问题，覆盖：节点标识唯一且非空、活动类型已注册、**有且仅有一个开始节点**、开始节点无入边、连线两端存在、条件表达式语法合法、独占网关至多一条默认边、`Join` 的 `Mode` 与人工任务的 `CompletionPolicy` 取值合法、人工任务必须配 `Assignees` 或 `AssigneesExpression`、**所有节点从开始节点可达**。

## 启动与驱动实例

```csharp
public class LeaveAppService(
    IWorkflowDefinitionManager definitions,
    IWorkflowEngine engine) : ApplicationServiceBase
{
    public async Task<string> SubmitAsync(decimal amount)
    {
        var instance = await engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "leave-request",     // 不指定版本 = 取最新已发布版本
            Variables = new Dictionary<string, object?> { ["amount"] = amount },
            CorrelationId = "leave-2026-0001",    // 业务单据号，信号按它定向
            StarterId = CurrentUser.Id?.ToString()
        });

        return instance.Id;
    }
}
```

`IWorkflowEngine` 是实例生命周期的唯一入口：

| 方法 | 要点 |
| --- | --- |
| `StartAsync` | **仅已发布定义可启动**；必填变量缺失直接抛异常 |
| `ResumeBookmarkAsync` | 消费书签并继续推进 |
| `PublishSignalAsync` | 恢复所有匹配的信号书签，返回恢复数量 |
| `SuspendAsync` / `ResumeAsync` | 人工暂停/恢复；挂起期间书签保留但拒绝恢复 |
| `CancelAsync` | 删书签、取消挂起节点；定义启用补偿时逆序补偿；级联结束子实例；已终态幂等 |
| `TerminateAsync` | 强制结束，**不补偿**；已终态幂等 |
| `RetryAsync` | 仅故障实例可用，从记录的故障节点重新执行 |

## 书签与四种恢复来源

需要等待的活动返回 `ActivityExecutionResult.Suspend(bookmarkRequests)`，引擎把请求落成书签、节点实例置为 `Suspended`。书签是流程的**唯一等待点**，六种种类归到四条恢复来源：

| 书签种类 | 索引键/到期 | 谁来唤醒 |
| --- | --- | --- |
| `UserTask` | `Key` = 受理人标识 | `IWorkflowUserTaskService.CompleteAsync` |
| `Signal` | `Key` = 信号名，`CorrelationId` 定向 | `IWorkflowEngine.PublishSignalAsync` |
| `SubWorkflow` | `Key` = 父节点实例标识 | 子实例进入终态后回调父节点 |
| `Timer` / `Retry` / `NodeTimeout` | `DueTime` = 到期时间 | `WorkflowTimerWorker` 轮询 |

```csharp
// 外部系统回调时唤醒等待中的实例
var resumed = await engine.PublishSignalAsync(
    "payment-received",
    payload: new Dictionary<string, object?> { ["paidAt"] = DateTime.UtcNow },
    correlationId: "leave-2026-0001");   // 为空表示广播
```

几条必须知道的机制：

- **消费即删除、批次必收尾**。书签一经消费就删掉，之后整个批次以引擎内部令牌推进并保证落盘。调用方的 `CancellationToken` 只在消费书签**之前**生效；批次途中的取消按节点故障处理（可重试），不会留下「书签没了但状态没写」的悬空实例。
- **`expectedBookmarkKey` 在锁内二次校验**。人工任务办理时传的是办理人标识，如果任务已被转办、书签 `Key` 已变，恢复直接被拒——旧受理人的办理动作不会生效。
- **节点超时是另一张书签**。节点首次挂起时若 `TimeoutSeconds` 有值，引擎额外写一张 `NodeTimeout` 书签；节点一旦离开挂起态，同节点实例下的兄弟书签（含超时书签）一并清掉。
- **没实现 `IResumableWorkflowActivity` 的活动**被恢复时走默认语义：恢复输入合并为输出、节点完成；但如果唤醒它的是 `NodeTimeout` 书签，则按故障处理。

## 内置活动

17 个开箱活动，节点的 `ActivityType` 填下表的类型编码（常量见 `WorkflowActivityTypes`）：

| 类型编码 | 说明 | 关键节点属性 |
| --- | --- | --- |
| `Start` / `End` | 流程入口 / 消耗当前令牌 | — |
| `Terminate` | 强制结束整个实例，不补偿 | `Reason` |
| `Fault` | 主动置为故障 | `Message` |
| `Log` | 记一条运行日志 | `Message`、`Level` |
| `SetVariable` | 写流程变量 | `Values`（字面量）、`Expressions`（表达式） |
| `Decision` | 独占网关 | 逻辑全在出边条件上 |
| `Parallel` | 并行网关，忽略条件全部扇出 | — |
| `Join` | 汇聚网关 | `Mode`（`WaitAll` / `WaitAny`） |
| `Delay` | 延时挂起，写 `Timer` 书签 | `DurationSeconds`（数值或表达式） |
| `WaitSignal` | 等待信号挂起 | `SignalName`、`AcceptAnyCorrelation` |
| `UserTask` | 人工审批 | 见下一节 |
| `Http` | 调外部接口 | `Url`、`Method`、`Headers`、`Body`、`ContentType`、`TimeoutSeconds`、`ResultVariable`、`FailOnErrorStatus` |
| `Script` | C# 脚本 | `Code`、`ResultVariable` |
| `PublishEvent` | 向事件总线发 `WorkflowCustomEventData` | `EventName`、`Payload` |
| `SubWorkflow` | 起子实例，可选等其终态 | `DefinitionCode`、`Variables`、`VariableExpressions`、`WaitForCompletion`、`ResultVariable`、`FailOnChildFault` |
| `ForEach` | 对集合逐项起子流程 | `ItemsExpression`、`DefinitionCode`、`ItemVariableName`、`Parallel`、`FailFast`、`ResultVariable` |

字符串型属性普遍支持 `{{ 表达式 }}` 模板插值。

::: warning WaitSignal 默认要求相关性匹配
`AcceptAnyCorrelation` 不为 true 且实例没有 `CorrelationId` 时，节点直接故障。这是有意的：空相关性的信号书签会命中任意定向信号，把别的业务单据的载荷合并进本实例。真要接收任意同名信号，显式配 `AcceptAnyCorrelation = true`。
:::

::: tip 子流程的深度闸门
`MaxSubWorkflowDepth`（默认 16）是递归定义的硬上限。超深的子实例会被拒绝启动，并以故障回调父节点，而不是把调用栈打爆。子实例统一在父批次结束、释放实例锁之后才启动，避免父子锁重入死锁。
:::

## 人工任务

审批节点的行为由节点属性决定：

| 属性 | 说明 |
| --- | --- |
| `Assignees` | 受理人标识列表 |
| `AssigneesExpression` | 求值为列表或逗号分隔字符串的表达式（与上一条二选一） |
| `CompletionPolicy` | `Any` 或签（默认）/ `All` 会签 / `Sequential` 依次审批 |
| `Title` | 任务标题，支持模板，默认取节点名称 |
| `FormData` | 表单数据字典，随待办一起给前端 |
| `CcUserIds` | 抄送人，随任务创建事件发出 |
| `AllowedOutcomes` | 办理结果白名单 |

三种完成策略的语义：

| 策略 | 通过条件 | 待办生成方式 |
| --- | --- | --- |
| `Any` 或签 | 任一人同意即通过 | 一次性给全部受理人建待办 |
| `All` 会签 | 全部同意才通过，任一非同意结果立即结束节点 | 一次性给全部受理人建待办 |
| `Sequential` 依次 | 按列表顺序逐个审批，全部同意才通过 | 每次只给下一位建待办 |

### 办理

```csharp
public class ApprovalAppService(IWorkflowUserTaskService userTasks) : ApplicationServiceBase
{
    public Task<List<WorkflowUserTask>> GetMyTodoAsync(string userId)
        => userTasks.GetPendingAsync(userId);

    public Task<WorkflowInstance> ApproveAsync(string taskId, string userId, string? comment)
        => userTasks.CompleteAsync(taskId, userId, WorkflowUserTaskOutcomes.Approved, comment);

    public Task TransferAsync(string taskId, string userId, string targetUserId)
        => userTasks.TransferAsync(taskId, userId, targetUserId);

    public Task CountersignAsync(string taskId, string userId, List<string> addedUserIds)
        => userTasks.AddAssigneesAsync(taskId, userId, addedUserIds);
}
```

- `outcome` 标准值是 `approved` / `rejected` / `timeout`（`WorkflowUserTaskOutcomes`），也**允许自定义任意字符串**——它会作为 `outcome` 变量参与出边条件，是做多路分支（打回、转下一级、加急）的正规手段。
- **任一非 `approved` 的结果立即结束节点**，会签也不例外（一票否决）。
- 办理人必须与书签上的受理人严格一致，否则拒绝。
- `TransferAsync` 拒绝转办给该节点链上已有的受理人（重复受理人会破坏会签与依次审批的进度推算）。
- `AddAssigneesAsync` 在或签/会签下立即给新人建待办，在依次审批下把人排到剩余队列末尾。
- 待办查询只返回实例仍在运行、且租户匹配的任务。

### fail-closed 的两处拒绝

::: danger 裸恢复不会被当成同意
恢复输入里没带 `outcome`（比如运维手工踢了一下书签），或者 `outcome` 不在 `AllowedOutcomes` 白名单里，人工任务活动会**拒绝本次恢复并原样重建待办**，而不是默认通过、也不是把实例打成故障丢掉会签进度。
:::

### 通知怎么接

订阅事件即可，引擎在书签落地时就发：

| 事件 | 时机 |
| --- | --- |
| `WorkflowUserTaskCreatedEventData` | 待办生成（含抄送人列表） |
| `WorkflowUserTaskCompletedEventData` | 办理完成 |
| `WorkflowUserTaskTransferredEventData` | 转办 |

实例级还有 `WorkflowInstanceStartedEventData` / `Completed` / `Faulted` / `Canceled` / `Terminated` / `Suspended` / `Resumed`，以及 `PublishEvent` 活动产生的 `WorkflowCustomEventData`。

## 表达式求值器的边界

内置 `WorkflowExpressionEvaluator` 是一门**轻量表达式语言**，不是 C# 脚本。它能做的仅限于：

| 能力 | 内容 |
| --- | --- |
| 取值 | 变量引用、点号导航、`[...]` 索引 |
| 字面量 | 数字、字符串、`true` / `false` / `null` |
| 运算 | `+ - * / %`、`== != < <= > >=`、`&& \|\| !`（短路） |
| 函数 | `len` `contains` `startsWith` `endsWith` `upper` `lower` `trim` `isNullOrEmpty` `abs` `min` `max` `round` `toNumber` `toString` `now` `date` |
| 模板 | `RenderTemplateAsync` 把文本里的 `{{ 表达式 }}` 替换为求值结果 |

几个容易踩的点：

- **引用不存在的变量会抛异常**，不是返回 `null`。条件表达式里用到的变量，要么在定义的 `Variables` 里声明默认值，要么保证前序节点一定写过。
- **条件表达式必须求值为布尔**，返回别的类型直接抛 `WorkflowException`。
- 数字统一以 `decimal` 求值；语法树按表达式文本缓存复用。
- 没有赋值、没有循环、没有自定义方法调用。

超出边界的逻辑有三条出路：用 `Script` 活动跑 C# 脚本、写一个自定义活动、或者整个 `Replace` 掉 `IWorkflowExpressionEvaluator`。

## 自定义活动

```csharp
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Activities;

[WorkflowActivity("SendSms", DisplayName = "发送短信", Category = "集成")]
public class SendSmsActivity(ISmsSender sms) : WorkflowActivityBase
{
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        // 节点属性：定义里 node.WithProperty("Phone", ...) 配的值
        var phone = await GetTemplatedStringAsync(context, "Phone");
        // 流程变量：类型安全读写
        var applicant = context.Variables.Get<string>("applicant");

        if (string.IsNullOrWhiteSpace(phone))
        {
            return ActivityExecutionResult.Fault($"短信节点 {context.Node.Id} 未配置 Phone");
        }

        await sms.SendAsync(phone, $"{applicant} 的申请已通过");

        // outputs 会合并进实例变量，outcome 参与出边条件
        return ActivityExecutionResult.Complete(
            new Dictionary<string, object?> { ["smsSentTo"] = phone },
            outcome: "sent");
    }
}
```

```csharp
// 注册：登记进 DI 的同时加入 XiHanWorkflowOptions.Activities
services.AddXiHanWorkflowActivity<SendSmsActivity>();
```

三种收尾结果：`Complete(outputs, outcome)` 继续推进、`Suspend(bookmarks)` 挂起等书签、`Fault(message)` 置为故障；要起子流程另有 `CompleteWithChildren` / `SuspendWithChildren`。

两个可选接口：

- `IResumableWorkflowActivity` — 活动挂起后自己接管恢复逻辑（会签的进度推算、遍历的下一项调度都靠它）。
- `ICompensableWorkflowActivity` — 实例被**取消**时按执行逆序回补（发货、扣款这类有外部副作用的活动），需要定义上 `EnableCompensation = true`（构建器的 `WithCompensation()`）。补偿只在 `CancelAsync` 触发，`TerminateAsync` 不补偿；单个补偿失败只记日志，不中断补偿链。

## 节点级容错

三种手段，作用点各不相同：

| 手段 | 配置 | 行为 |
| --- | --- | --- |
| 重试 | `WithRetry(maxAttempts, firstDelaySeconds, backoffFactor)` | 节点失败时写 `Retry` 书签指数退避（`MaxAttempts` **含首次执行**，默认首次等待 10 秒、倍率 2.0），到期由定时器唤醒重跑 |
| 失败续行 | `WithContinueOnError()` | 重试耗尽后不故障实例：把错误写进 `lastError` 变量，以 `error` 结果继续流转（出边写 `outcome == 'error'` 接异常分支） |
| 挂起超时 | `WithTimeout(timeoutSeconds)` | 仅对挂起型节点生效；到期按各活动的超时语义处理（人工任务走 `timeout` 结果，延时/子流程/遍历按故障） |

都没配则节点故障会把整个实例置为故障，记录故障节点，之后可以 `RetryAsync` 从那个节点重来。重试算**新波次**：旧书签与旧子实例登记会被清掉，老波次子实例的回调随之失效，不会串进新一轮。

## 换成持久化存储

三个端口各自 `Replace` 即可（框架用 `TryAddSingleton` 注册了内存实现，再 `TryAdd` 会被静默忽略，**必须用 `Replace`**）：

```csharp
services.Replace(ServiceDescriptor.Singleton<IWorkflowDefinitionStore, DbWorkflowDefinitionStore>());
services.Replace(ServiceDescriptor.Singleton<IWorkflowInstanceStore, DbWorkflowInstanceStore>());
services.Replace(ServiceDescriptor.Singleton<IWorkflowBookmarkStore, DbWorkflowBookmarkStore>());
```

实现时要守住的语义契约：

| 方法 | 契约 |
| --- | --- |
| `IWorkflowBookmarkStore.GetDueAsync` | 过滤 `DueTime` 非空且 `<= now`，按 `DueTime` 升序，最多返回 `maxResultCount` 条 |
| `IWorkflowBookmarkStore.GetBySignalAsync` | `Kind == Signal` 且 `Key == signalName`；`correlationId` 非空时额外要求书签相关性为空或相等 |
| `IWorkflowDefinitionStore.FindLatestPublishedAsync` | 同编码下 `Status == Published`，按版本降序取第一条 |
| `IWorkflowInstanceStore.GetNodeInstancesAsync` | **按开始时间升序**——补偿的逆序执行依赖这个顺序 |

两件不用做的事：存储实现**不需要**乐观并发控制（引擎已用实例级分布式锁串行化同一实例的读写），书签存储**不需要**原子领取（定时器 Worker 已由分布式锁保证集群单活）。另外，存储查询层**不做租户过滤**——租户隔离由引擎与人工任务服务在查询结果上按环境租户执行。

## 并发与集群

引擎对同一实例的所有写操作都先拿实例级分布式锁（键前缀 `WorkflowConsts.InstanceLockKeyPrefix`），单写者、可安全并发调用：

- 锁过期 `InstanceLockExpirySeconds`（默认 120 秒），持锁期间按过期时间的三分之一自动续期，长批次（HTTP、脚本、补偿链）不会中途被第二个写者插进来。
- 获取超时 `InstanceLockAcquireTimeoutSeconds`（默认 10 秒）后抛 `WorkflowLockTimeoutException`，按并发冲突处理即可（定时器与信号投递内部已带重试）。

定时器 Worker 每轮先抢一把名为 `DistributedLockName`（默认 `XiHanWorkflowTimerWorker`）的锁，抢不到就跳过本轮，从而在集群内**单活轮询**；恢复每张书签前会切到书签所属租户上下文。实例处于不可恢复状态（挂起/故障）时，到期的定时类书签会把 `DueTime` 推后 `NotResumableTimerBackoffSeconds`（默认 300 秒），避免同一张书签每轮占满取回配额。

::: danger 集群部署必须接 Redis
两把锁都来自 Caching 模块的 `IDistributedLock`。**没有配置 Redis 时它是进程内实现**，多个实例各锁各的：定时器会重复轮询、同一实例可能被两个进程同时推进。集群环境务必确认 Redis 已配好，见[缓存与分布式锁](./caching)。
:::

## 配置

主配置节 `XiHan:Workflow`，Worker 配置节 `XiHan:Workflow:Worker`：

```json
{
  "XiHan": {
    "Workflow": {
      "MaxNodeExecutionsPerBurst": 1000,
      "InstanceLockExpirySeconds": 120,
      "MaxSubWorkflowDepth": 16,
      "NotResumableTimerBackoffSeconds": 300,
      "Worker": {
        "IsTimerEnabled": true,
        "PollPeriodMilliseconds": 5000,
        "MaxBookmarkFetchCount": 100
      }
    }
  }
}
```

::: warning 关掉 IsTimerEnabled 等于砍掉一条恢复来源
Worker 会直接退出，延时、节点重试、节点超时三类书签**永不自动恢复**——流程只剩人工办理和信号能推动。单机调试时可以关，生产别关。
:::

全部字段与默认值见[包文档的配置表](../packages/workflow#配置)。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 启动实例报「未发布」 | 定义还是草稿，得先 `PublishAsync` |
| 重启服务后实例全没了 | 还在用默认的内存存储，换持久化实现 |
| 延时节点永远不往下走 | `IsTimerEnabled` 被关了，或 Worker 所在进程没跑起来 |
| 条件表达式报「引用了不存在的变量」 | 变量没声明默认值，且前序节点没写过它 |
| 条件表达式报「必须返回布尔值」 | 写成了取值表达式，比如漏了比较运算符 |
| 独占网关故障「无匹配分支」 | 所有条件都不满足且没配默认分支，补一条 `AddDefaultTransition` |
| 普通节点故障「存在出边但所有条件均不满足」 | `AllMatched` 行为下出边条件没覆盖全，补一条无条件出边或放宽条件 |
| 一个审批结果走出了两条分支 | 普通节点上混用了默认边与条件边，改成互斥且穷尽的条件边 |
| 实例故障「分支已死亡，无法汇合」 | 并行分支中有一条提前终结，汇聚等不到；检查分支上的 `End` 与条件 |
| 环路里令牌莫名消失 | 汇聚网关用了 `WaitAny`，换成 `WaitAll` |
| 批次超上限故障 | 定义里有失控环路，或 `MaxNodeExecutionsPerBurst` 设得太小 |
| 转办后原受理人还能办理 | 不会——书签键已变，恢复会被拒绝；如果真发生了，检查是否绕过 `IWorkflowUserTaskService` 直接调了引擎 |
| 集群里定时书签被处理多次 | 分布式锁退化成了进程内实现，接上 Redis |
| 自定义活动报「未注册的活动类型」 | 只做了 `AddTransient`，没走 `AddXiHanWorkflowActivity<T>()` |

## 下一步

- [缓存与分布式锁](./caching)：实例锁与 Worker 单活锁的底座
- [多租户](./multi-tenancy)：书签、实例与待办的租户隔离口径
- [扩展与二次开发](./extending)：替换框架默认实现的通用做法
- [Workflow 包文档](../packages/workflow)：完整 API 清单与全部配置项
- [Workflow.Abstractions 包文档](../packages/workflow-abstractions)：契约与运行时模型
- [Tasks 包文档](../packages/tasks)：定时任务与后台作业，和工作流的分工
