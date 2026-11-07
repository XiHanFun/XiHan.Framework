# 17.1 定义与发布

这一页讲工作流子系统的前半段：流程定义是什么模型、怎样启用引擎、怎样用构造器或 JSON 写出一份定义、怎样校验与发布、怎样管理版本与列出定义。读完后，我们能把一个请假审批流定义出来并发布到目录，供下一页启动实例、处理待办。前置知识是[特性与清单](/startup/features)、[操作契约](/web/operations)与[结果与错误处理](/errors/)；换用数据库实现时还要先读[持久化与 Provider](/persistence/overview)。

## 为什么需要

审批、订单履约、对账这类业务要跨越几小时到几天：中间要等人审批、等外部系统回调、等定时到点。把「走到哪一步、在等什么」写成业务表里的几个状态字段，分支一多就失控，服务重启后还要自己想办法接着跑。工作流子系统把这件事抽出来：定义是一张有向图，实例是定义的一次执行，所有状态落在存储里；需要等待的节点以书签挂起，到点或被唤醒时从存储恢复。引擎在抽象包里，开发与测试用内存实现，换成数据库实现就能跨进程重启长时间等待，业务代码不变。

## 模型

| 元素 | 类型 | 说明 |
| --- | --- | --- |
| 定义 | `WorkflowDefinition` | 一张有向图，按名字加版本管理，状态是 `Draft`、`Published`、`Disabled` 或 `Archived` |
| 节点 | `WorkflowNode` | 执行某种活动（`ActivityType`） |
| 转移 | `WorkflowTransition` | 连接节点，可带条件表达式 |
| 变量 | `WorkflowVariableDefinition` | 声明实例的输入 |
| 实例 | `WorkflowInstance` | 定义的一次执行，状态全部落在存储里 |
| 节点实例 | `WorkflowNodeInstance` | 每执行一个节点留一条 |
| 书签 | `WorkflowBookmark` | 等待中的节点（人工审批、定时、信号、子流程、外部任务、重试、超时）挂起的凭据，被消费时恢复执行 |

值模型是 JSON（规范 RFC-0021《工作流：持久化执行》）：变量、节点属性、输入、输出、节点状态与书签载荷都是 `JsonObject`，存储以 JSON 文本持久化，序列化走源生成，Native AOT 下可用。时间一律是 UTC 的 `DateTimeOffset`。

引擎在实例锁内按「波次」推进：从队列逐个执行节点，直到队列空或实例不再运行，然后一次写回实例、节点实例与书签。活动在引擎这次操作所在的调用里执行（同一作用域、同一事务），看到的主体、租户与追踪都是这次调用的。Provider 实现三个存储、事务、实例锁和到期书签的轮询驱动。

应用直接使用的四个服务都是作用域服务：

| 服务 | 用途 |
| --- | --- |
| `IWorkflowDefinitionCatalog` | 定义的生命周期：草稿、发布、新版本、停用、归档、删除、列表 |
| `IWorkflowEngine` | 启动、恢复书签、信号，以及挂起、恢复、取消、终止、重试、改变量、跳转、迁移、清理 |
| `IWorkflowTaskService` | 用户任务：待办查询、完成、转办、加签、认领与释放、退回、催办 |
| `IWorkflowExternalTaskService` | 外部任务：外部工作者按主题领取、完成、失败、延长锁 |

### 事务

在一次调用里使用这四个服务时，它们加入调用方的事务：存储写入、活动与结果处理器的写入、事件都随业务一起提交或回滚。「写申请并发起流程」「授予并完成实例」这样的组合是原子的：业务失败、调用回滚，实例、节点实例、书签都不留。

- 事件在事务里经作用域的 `IMessagePublisher` 发出。装配了发件箱时随事务提交、回滚即丢弃；没有发件箱时直接发布，可能早于提交、回滚不撤回。要求一致的应用要装配发件箱，见[领域事件与发件箱](/messaging/domain-events)。
- 引擎取得的实例锁持有到调用的服务作用域结束（事务提交或回滚之后）才释放，别的节点拿到锁时读到的一定是提交后的状态。同一调用里再操作同一个实例直接复用这把锁。
- 只读调用（规范 RFC-0005「只读工作单元」，如 `get` / `list` 操作）里只能查询；写入在取锁、执行活动之前返回 `PERSISTENCE_UNEXPECTED_001`（操作实现的缺陷，HTTP 500）。内存实现与数据库实现相同：数据库实现看工作单元，内存实现看调用的操作契约，没装持久化也一样拒绝。
- 在调用之外使用（后台任务、托管服务、轮询器、定时器）时，引擎自己开一次经完整管道的调用（来源协议 `workflow`，目标是操作名，如 `workflow.start`），在那次调用的事务里执行；租户取所在作用域的调用，没有即平台。应用不在服务状态时返回 `WORKFLOW_UNAVAILABLE_001`。
- 活动失败按重试 / 继续 / 实例故障处理，它在失败之前做的写入留在同一事务里、不单独回滚：会写入的活动与结果处理器先校验、后写入。
- `Workflow.InMemory` 没有事务：写入立即生效，调用回滚不撤销。

### 分区

工作流的数据分成平台分区与每个租户一个分区。一次调用只读写自己的分区：分区是当前调用的租户，没有租户的调用（平台）是平台分区。平台与租户互不可见：租户看不到平台和别的租户的定义、实例与任务，平台也看不到任何租户的。看不到的一律按不存在处理（定义 `WORKFLOW_NOTFOUND_001`、实例 `_002`、书签 `_003`）。平台要管理某个租户的流程，以那个租户的身份发起调用。用 `Workflow.Database` 时租户的记录随租户分库，见[租户分片与分库](/multi-tenancy/sharding)。

## 启用

| 实现 | 存储 | 实例锁 | 适用 |
| --- | --- | --- | --- |
| [Workflow.InMemory](/packages/workflow-inmemory)（默认实现，`Profiles.Full` 已包含） | 有界内存表 | 进程内信号量 | 单实例、开发、测试；重启后实例丢失 |
| [Workflow.Database](/packages/workflow-database) | 应用的关系库（`persistence.relational@1`） | 数据库租约行 | 多实例、需要持久化 |

```bash
dotnet add package XiHan.Framework.Workflow.InMemory --prerelease
```

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Workflow.InMemory;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryWorkflowFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

换成数据库实现时装配一个持久化 Provider 和 `DatabaseWorkflowFeature`，连接串来自绑定 `db`（见[资源绑定](/configuration/bindings)）；装配后顶替内存实现，业务代码注入的四个服务不变：

```csharp
using SqlSugar;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Workflow.Database;

builder.AddXiHan(xihan => xihan
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseWorkflowFeature>());
```

特性启动时起到期书签的轮询（缺省每秒一次），定时、超时、重试、提醒、定时启动都靠它推进。每个周期先看应用是否在服务状态：启动中与排空中管道不接受调用，这时的周期什么也不做、不记告警，进入服务状态后照常轮询与清理。数据库实现的轮询领导权也只在服务状态拿与续：排空中的节点在下一个周期就释放，另一个在服务的实例随即接手。

## 最小示例

下面定义一个请假审批：主管审批，超过 3 天的再由人事三人会签、60% 通过即可，主管 3 天不处理视为超时。先用构造器写出定义：

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

再写一个作用域服务，把定义建成草稿并发布：

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

        // 发布前先校验；不合法返回 WORKFLOW_VALIDATION_001，details.errors 列出全部问题
        return await catalog.PublishAsync(draft.Id, cancellationToken);
    }
}
```

在上面「启用」的 `Program.cs` 里登记 `builder.Services.AddScoped<LeaveSetup>()`，在一次调用里（例如一个操作或启动种子）调用 `PublishAsync`。运行后：`CreateAsync` 返回版本 1 的 `Draft`，`PublishAsync` 返回同一条记录、状态变为 `Published`，`FindPublishedAsync("hr.leave")` 能找到它；再调一次 `CreateAsync` 得到版本 2 的草稿，旧版本不受影响。下一页用 `IWorkflowEngine.StartAsync` 按名字 `hr.leave` 启动它。

## 用构造器定义

`WorkflowDefinitionBuilder.Create(name, displayName)` 以流式 API 生成定义。构造器为 `Start`、`End`、`Decision`、`Parallel`、`Join`、`Delay`、`UserTask`、`EventGateway`、`Http`、`ExternalTask`、`Compensate` 提供专用方法（`AddStart`、`AddEnd`、`AddDecision`、`AddUserTask`、`AddExternalTask` 等），其余活动用 `AddNode(id, activityType, displayName, configure)` 加 `WithProperty` 设置属性。

| 层级 | 方法 | 作用 |
| --- | --- | --- |
| 节点 | `WithProperty(name, value)` | 设置活动属性，字符串值支持模板 |
| 节点 | `WithAssigneeRule(type, values...)` | 用户任务的指派规则 |
| 节点 | `WithRetry(maxAttempts, firstDelay, backoffFactor)` | 失败重试 |
| 节点 | `WithTimeout(timeout)` | 挂起后多久超时 |
| 节点 | `WithContinueOnError()` | 失败不让实例故障，以 `error` 标签流出 |
| 节点 | `WithOutputMapping(variable, expression)` | 节点输出映射到变量 |
| 转移 | `AddTransition(source, target, condition, priority)` / `AddDefaultTransition(source, target)` | 带条件或默认的出边 |
| 定义 | `WithCompensation()` / `WithCompensateOnFault()` | 取消时补偿、故障时也补偿 |
| 定义 | `WithTimeout(timeout, action)` | 实例级超时，`Fault` 或 `Terminate` |
| 定义 | `WithCategory`、`WithDescription`、`WithProperties` | 分类、说明与扩展属性 |

## 用 JSON 定义

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

`id`、`version`、`status` 在 `CreateAsync` 时由目录重新分配，JSON 里的值只是占位。必填的成员只有：定义的 `id`、`name`，节点的 `id`、`activityType`，转移的 `id`、`sourceNodeId`、`targetNodeId`，变量的 `name`；没写报 `workflow.definition.member_missing`。其余成员都可省略，取模型的初值：`version` 1、`status` `Draft`、`nodes` / `transitions` / `variables` 空列表、节点的 `properties` 空对象、重试策略的 `maxAttempts` 1、`firstDelay` 10 秒、`backoffFactor` 2，其余为空、`false` 或 0；写成 `null` 与不写相同。`WorkflowJson.Serialize` 写出全部成员（只省略值为 `null` 的），等于初值的也写出，读回来的值不变。

## 节点、转移与变量

::: v-pre
| 元素 | 字段 |
| --- | --- |
| `WorkflowNode` | `Id`（`^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$`，定义内唯一）、`ActivityType`、`DisplayName`、`Properties`（字符串值支持 `{{ 表达式 }}` 模板）、`RetryPolicy`、`Timeout`（挂起后多久超时）、`ContinueOnError`、`OutputMappings`（变量名 → 表达式） |
| `WorkflowTransition` | `Id`、`SourceNodeId`、`TargetNodeId`、`Condition`（表达式，空为无条件）、`Priority`（小者先）、`IsDefault` |
| `WorkflowVariableDefinition` | `Name`、`Required`、`DefaultValue`、`Description` |
:::

定义名 `WorkflowName` 用主题文法 `^[a-z0-9][a-z0-9._-]{0,127}$`，一个名字下有多个版本。活动类型 `ActivityType` 的文法是 `^[A-Za-z][A-Za-z0-9._-]{0,63}$`，内置活动用 PascalCase 名。`Category` 最多 128 个字符（`WorkflowDefinition.MaxCategoryLength`），超长构造即抛 `ArgumentException`，从 JSON 读入时返回 `WORKFLOW_VALIDATION_001`。表达式的文法见[活动与扩展](/workflow/activities#表达式)。

## 出边与结果标签

节点以结果标签完成时（用户任务的 `approved` / `rejected` / `timeout`、事件网关的信号名、`ContinueOnError` 的 `error`、活动自定的标签），标签写进节点输出，也写进实例变量 `outcome`；当前节点与后继决策节点的出边条件都按它选边。没有标签的节点不覆盖 `outcome`。

出边选择由活动登记时的 `ActivityOutgoing` 决定：

| 模式 | 行为 | 使用者 |
| --- | --- | --- |
| `AllMatched`（缺省） | 取全部无条件或条件成立的出边；一条都没有时取默认出边 | 大多数活动 |
| `Exclusive` | 按优先级取第一条无条件或条件成立的出边；没有时取默认出边 | `Decision` |
| `All` | 取全部出边 | `Parallel` |
| `None` | 不流出 | `End`、`Terminate`、`Fault` |

`AllMatched` 与 `Exclusive` 一条也选不到时实例故障。默认出边只作兜底，有条件成立时不走它。条件引用了未知变量是表达式错误，实例故障，不会悄悄当成假。

## 校验

`WorkflowDefinitionValidator` 在发布与启动前执行，失败返回 `WORKFLOW_VALIDATION_001`，`details.errors` 列出全部问题（每条的路径、消息键与参数见[设计期诊断](/workflow/activities#设计期诊断)）。检查项：名字与版本合法；至少一个节点；节点 ID 唯一；活动类型都已登记；恰好一个 `Start` 且没有入边；转移两端存在；条件与输出映射的表达式语法合法；每个节点最多一条默认出边；`Join` 的 `Mode`、`UserTask` 的 `CompletionPolicy`、`EmptyAssigneeStrategy` 与 `ApprovalThreshold` 取值合法；`End` 的 `Outcome` 不是空串；每个 `UserTask` 至少一个指派来源且规则类型已登记；`Start.Cron` 是合法 cron；`EventGateway` 至少一个信号；`Http` 的 `Url` 非空、`Method` 合法；所有节点从 `Start` 可达。

## 发布与版本

目录按版本管理定义。同名定义的每个版本是一条独立记录：

| 方法 | 允许的状态 | 作用 |
| --- | --- | --- |
| `CreateAsync(definition)` | — | 新建草稿：新 ID，租户取当前调用的租户（写了别的租户 → `WORKFLOW_NOTFOUND_001`），版本号 = 该分区里同名最大版本 + 1 |
| `UpdateAsync(definition)` | `Draft` | 整体替换草稿，名字、版本、租户不变 |
| `PublishAsync(ids)` | `Draft` | 先校验再发布；带 `Start.Cron` 的定义同时排一个定时启动书签 |
| `NewVersionAsync(name)` | — | 以当前分区里的最新版本为底复制出一个草稿 |
| `DisableAsync(ids)` | `Published` | 停用，删除定时启动书签 |
| `ArchiveAsync(ids)` | 任意 | 归档，删除定时启动书签 |
| `DeleteAsync(ids)` | `Draft` | 删除草稿 |
| `FindAsync(id)` / `FindPublishedAsync(name, version)` | — | 查询；`version` 为空取最新已发布版本，只在当前分区里找 |
| `QueryAsync(WorkflowDefinitionQuery)` | — | 定义列表：摘要的一页与总数（`WorkflowDefinitionPage`） |
| `ActivityTypes()` | — | 已登记活动的描述符（类型、显示名、分类、输入属性），供设计器列出可用节点；显示名、分类显示名、说明与输入说明按调用方的区域渲染 |

状态不允许时返回 `WORKFLOW_CONFLICT_002`。按标识的操作都是批量、逐项结果，单条写法是 `WorkflowDefinitionCatalogExtensions` 里的扩展方法（最小示例里的 `PublishAsync(draft.Id, …)` 就是）。

发布新版本不会改变旧版本的状态。按名字启动时取最新的已发布版本，已经在跑的实例留在原来的版本上，需要时用 `MigrateAsync` 迁到新版本（见[控制实例](/workflow/instances#控制实例)）。只想让新实例停用旧版本时，对旧版本调用 `DisableAsync`。

## 定义列表

`QueryAsync(WorkflowDefinitionQuery)` 列当前分区的定义，返回这一页的摘要与同条件的总数。摘要 `WorkflowDefinitionSummary` 只有列表要的字段：`Id`、`Name`、`Version`、`DisplayName`、`Description`、`Category`、`Status`、`TenantId`、`CreatedAt`、`PublishedAt`，以及显示名或名字 `Title`；节点、转移、变量与扩展属性不在里面，要整份定义按 `Id` 调 `FindAsync`。

| 条件 | 说明 |
| --- | --- |
| `Keyword` | 关键字：不区分大小写地包含在名字、显示名或说明里即匹配 |
| `Name` | 定义名 |
| `Category` | 分类，精确匹配 |
| `Status` | 状态 |
| `CreatedFrom` / `CreatedTo` | 创建时间区间：下界含、上界不含 |
| `Sorts` | 排序项（字段加方向），有序、可多个；空或没给时名字升序、版本降序 |
| `Skip` / `Limit` | 缺省 0 与 100，`Limit` 最大 1000（`WorkflowDefinitionQuery.MaxLimit`） |

条件同时给出时取交集。过滤、排序、分页与计数都由存储执行，数据库实现是 SQL。分页越界（`Skip` 为负、`Limit` 不在 1～1000）不在构造时抛出，`QueryAsync` 返回 `WORKFLOW_VALIDATION_002`（HTTP 400，消息键 `workflow.definition.query_page_invalid`），不进存储；排序字段越界同样是 400。

```csharp
var page = (await catalog.QueryAsync(new WorkflowDefinitionQuery
{
    Keyword = "报销",
    Category = "finance",
    Status = WorkflowDefinitionStatus.Published,
    CreatedFrom = monthStart,
    CreatedTo = monthStart.AddMonths(1),
    // 状态升序（生命周期），同状态的创建时间降序
    Sorts = [new(WorkflowDefinitionSortField.Status), new(WorkflowDefinitionSortField.CreatedAt, Descending: true)],
    Skip = 0,
    Limit = 20,
}, cancellationToken)).ValueOrThrow();
// page.Items：这一页的摘要；page.Total：同条件的总数
```

### 列表排序

定义查询与实例查询各有一个有序的排序项列表 `Sorts`（`WorkflowDefinitionSort` / `WorkflowInstanceSort`：白名单里的字段 `Field` 加方向 `Descending`，缺省升序），可以多个字段：第一项是主排序字段，第一项相同的按第二项，依此类推。排序在存储里执行（数据库实现是 SQL 的 `ORDER BY`），全部排序项之后接固定的次序键，同值的行在各页之间不重不漏。

| 查询 | 排序项的字段 | `Sorts` 为空或没给 | 次序键（排序项里已有的跳过） | 项数上限 |
| --- | --- | --- | --- | --- |
| `WorkflowDefinitionQuery` | `Name`（名字）、`Title`（显示名，没有取名字）、`Version`、`Status`、`CreatedAt`、`PublishedAt` | 名字升序、版本降序 | 名字升序、版本降序（数据库实现最后再按 ID） | `WorkflowDefinitionQuery.MaxSorts` = 6 |
| `WorkflowInstanceQuery` | `CreatedAt`、`EndedAt`、`Status`、`DefinitionName`、`Title`（实例标题） | 创建时间降序 | 创建时间降序、写入先后降序；排序项里有创建时间时写入先后随它的方向 | `WorkflowInstanceQuery.MaxSorts` = 5 |

- 空列表与没给一样是缺省排序；每一项的方向缺省升序，实例列表要最新的在前得写 `new(WorkflowInstanceSortField.CreatedAt, Descending: true)`。
- 排序项不合法时 `QueryAsync` / `ListInstancesAsync` 返回 `WORKFLOW_VALIDATION_002`（HTTP 400），不进存储；按这个顺序检查、报第一个：项数超过上限（消息键 `workflow.definition.query_sorts_too_many` / `workflow.instance.query_sorts_too_many`，参数 `count`、`max`），某项为空或字段不在白名单（`…query_sort_invalid`，参数 `sort`），同一字段出现两次、不论方向（`…query_sort_duplicate`，参数 `sort`）。`CountInstancesAsync` 不看排序。请求里给不认识的字段名在反序列化时就被拒绝。
- 文本（名字、标题、定义名）按数据库列的排序规则比较，内存实现按序数：大小写、标点与中文的先后可能随库不同。
- 状态按生命周期的先后：定义 `Draft` → `Published` → `Disabled` → `Archived`；实例 `Running` → `Suspended` → `Completed` → `Canceled` → `Faulted` → `Terminated`。升序是这个顺序、降序反过来；状态变了位置随之变化。
- 没发布过的定义 `PublishedAt` 为空、没结束的实例 `EndedAt` 为空：空值排在哪头照数据库（PostgreSQL、Oracle 当作最大，SQLite、MySQL、SQL Server 当作最小），内存实现当作最小。
- 标题按前 256 个字符比较（`WorkflowSortKeys.Title`）。

### 关键字

列表查询的 `Keyword` 都按同一套规则匹配（`WorkflowKeyword`）：去掉首尾空白，为空或只有空白不过滤；关键字与被搜的文本先折叠成不变文化的小写，再判断包含，任一被搜字段包含即匹配，不跨字段。关键字里的 `%`、`_`、`[`、`\` 按字面匹配，与全角的 `％`、`＿`、`［`、`＼` 是不同的字符；内存实现与各数据库 Provider 的结果一致，按字面由持久化保证（见[仓储读写](/persistence/repositories)），小写折叠抹平各库大小写规则的差异。数据库的排序规则另有放宽（如 MySQL 缺省排序规则不区分重音）时以排序规则为准。SqlSugar 在兼容库（达梦、人大金仓、openGauss、TiDB 等）上维持原来的翻译，那里关键字里的 `%`、`_` 会当通配，与应用自己实体的查询相同。

三处查询各自匹配的字段：

| 查询 | 匹配的字段 |
| --- | --- |
| 定义列表 `WorkflowDefinitionQuery` | 名字、显示名、说明 |
| 实例列表 `WorkflowInstanceQuery` | 实例标题、关联 ID |
| 待办 `UserTaskQuery` | 任务标题、实例标题、定义名、关联 ID（所属实例的） |

关联 ID 是按包含匹配的，搜索框里输单据号的一段就能找到；要按一组单据号精确查进度，用实例查询的 `CorrelationIds`。

## 定义的租户

定义属于一个分区（`TenantId` 是分区的租户，平台为空）。每个分区是一个名字空间：同名定义在不同分区里各自从版本 1 编号，互不占号。调用只看得到、改得动自己分区的定义，按名字解析（启动、`FindPublishedAsync`、子流程与 `ForEach`）也只在自己的分区里找。看不到的定义一律按不存在处理，返回 `WORKFLOW_NOTFOUND_001`，不暴露别的分区有没有这个定义。

没有全局定义：平台建的定义对租户不可见。各租户共用的流程模板要在每个租户里各建一份，由应用的租户初始化（种子）负责。

## 常见问题

### 构造器与 JSON 该用哪个

代码里固定的流程用构造器，编译期就能发现属性名写错；设计器产出、要存库、要让运营改的流程用 JSON。两者生成的是同一个 `WorkflowDefinition`，`WorkflowJson.Serialize` 可以把构造器的结果导出给设计器当模板。

### 发布失败只报 `WORKFLOW_VALIDATION_001`，怎么知道哪里错了

错误的 `details.errors` 是逐条的违规列表，每条带定义 JSON 里的路径（如 `$.nodes[2].properties.Cron`）、消息键与参数；经 HTTP 时宿主写成 Problem Details 的字段级 `errors`，设计器按路径里的下标定位节点或转移。进程内用 `validator.Diagnose(definition)` 拿同一组问题，详见[设计期诊断](/workflow/activities#设计期诊断)。

### 改了已发布的流程，正在跑的实例怎么办

`UpdateAsync` 只能改草稿。要改已发布的流程，用 `NewVersionAsync(name)` 复制出新草稿、改好后发布；旧版本状态不变，正在跑的实例留在旧版本上，新启动的取最新已发布版本。要让旧实例也走新版本，对它们调 `MigrateAsync`；要让旧版本不再被新实例使用，调 `DisableAsync`。

### 为什么平台建的定义租户看不到

分区是硬隔离：没有全局定义，平台与租户互不可见。各租户共用的模板由应用在租户初始化时各建一份；平台要管理某租户的流程时，以那个租户的身份发起调用。

### 决策节点一条出边都没选到，实例为什么故障了

`Decision` 的出边是 `Exclusive`：没有条件成立又没有默认出边时实例故障，不会悄悄停下。给每个决策节点加一条 `AddDefaultTransition` 作兜底；条件引用了启动时没给的变量同样是故障，可选变量在 `AddVariable` 里给默认值。

## 相关包与下一步

- [Workflow.Abstractions](/packages/workflow-abstractions)：契约、引擎、构造器、校验与 JSON
- [Workflow.InMemory](/packages/workflow-inmemory)：默认实现与容量配置
- [Workflow.Database](/packages/workflow-database)：数据库实现、表结构与租约
- 下一步：[实例与用户任务](/workflow/instances)——启动实例、处理待办、等待外部事件与控制实例
