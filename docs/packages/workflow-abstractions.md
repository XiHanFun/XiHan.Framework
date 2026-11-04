# XiHan.Framework.Workflow.Abstractions

工作流子系统的契约与引擎：定义模型与校验、定义目录、19 个内置活动、实例与书签、用户任务与外部任务、表达式语言、存储契约，以及经内核管道执行活动的引擎与到期书签轮询器。能力 `workflow.execution@1` 由 Provider 提供，应用与 Provider 都引用本包。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Workflow.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `workflow.execution@1`（`WorkflowCapabilities.Execution`）的契约，本包不提供能力 |
| 特性入口 | — |
| 配置节 | `XiHan:Workflow`（`WorkflowOptions`，由 Provider 读取） |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Messaging.Abstractions`、`XiHan.Framework.MultiTenancy.Abstractions`（租户目录）、`XiHan.Framework.Scheduling.Abstractions`（cron）；`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions`、`Microsoft.Extensions.Logging.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

引擎、校验器、表达式求值器、活动登记表、指派解析、任务服务、外部任务服务、轮询器与全部内置活动都在本包里（ADR-0016）。Provider 只实现三个存储（定义、实例、书签）、事务与实例锁，再调用 `AddWorkflowEngine(options)` 装配引擎、在特性的生命周期里驱动 `DueBookmarkPoller`。内存实现与数据库实现因此语义一致，由 `Workflow.Conformance` 的存储套件、引擎套件与任务套件约束。

值模型是 JSON：变量、属性、输入输出、节点状态与书签载荷都是 `JsonObject`，`WorkflowJson` 用源生成的上下文做序列化与深拷贝，不靠反射。引擎在实例锁内以波次推进实例，一个波次结束时把实例、节点实例与书签一次写回。在调用里被调用时加入调用方的事务（ADR-0077）：存储写入、活动与结果处理器的写入、事件随业务提交或回滚，实例锁持有到调用的服务作用域结束；在调用之外被调用时自己经完整管道开一次调用（来源协议 `InvocationProtocols.Workflow`，目标是操作名）。活动在这次操作的调用里执行。数据按分区（平台、每个租户一个）隔离。

## 何时使用

- 定义流程、启动实例、处理待办：引用本包，再装配一个提供 `workflow.execution` 的 Provider（[Workflow.InMemory](./workflow-inmemory) 或 [Workflow.Database](./workflow-database)）。
- 编写自定义活动、指派规则或结果处理器：实现本包的 `IActivity`、`IAssigneeProvider`、`IWorkflowOutcomeHandler`。
- 实现新的存储 Provider：实现三个存储、事务与实例锁契约并继承 `Workflow.Conformance` 的套件。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Workflow.Abstractions --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](../quickstart#获取预览包)。各 Provider 包与 `Profiles.Full` 都传递引用本包。登记自定义活动与指派规则：

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Assignees;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddActivity<AuditActivity>("Audit");
builder.Services.AddAssigneeProvider<RoleAssigneeProvider>();

public sealed class AuditActivity : IActivity
{
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(ActivityOutcome.Complete()));
}

public sealed class RoleAssigneeProvider : IAssigneeProvider
{
    public string RuleType => "Role";

    public Result Validate(AssigneeRule rule) => Result.Success();

    // 示例：角色名直接当指派人；实际应用查自己的授予数据
    public ValueTask<Result<IReadOnlyList<string>>> ResolveAsync(AssigneeResolution resolution, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(resolution.Rule.Values));
}
```

## 配置

`WorkflowOptions.FromConfiguration` 读 `XiHan:Workflow`：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Workflow:MaxNodeExecutionsPerBurst` | 整数 ≥ 1 | 1000 | 一个波次最多执行的节点数，超过即实例故障 |
| `XiHan:Workflow:MaxSubWorkflowDepth` | 整数 ≥ 0 | 16 | 子流程嵌套深度上限 |
| `XiHan:Workflow:LockTimeoutSeconds` | 正数 | 10 | 等实例锁的上限 |
| `XiHan:Workflow:NotResumableBackoffSeconds` | 正数 | 300 | 不可恢复的到期书签后推多久 |
| `XiHan:Workflow:ExpressionCacheSize` | 整数 ≥ 1 | 10000 | 已解析表达式的缓存上限 |
| `XiHan:Workflow:RetentionDays` | ≥ 0 | 0 | 已结束实例的保留天数，0 为不自动清理 |
| `XiHan:Workflow:HousekeepingIntervalSeconds` | 正数 | 3600 | 自动清理周期 |
| `XiHan:Workflow:HttpTimeoutSeconds` | 正数 | 30 | `Http` 活动的缺省超时 |

```json
{ "XiHan": { "Workflow": { "LockTimeoutSeconds": 10, "MaxSubWorkflowDepth": 16, "RetentionDays": 90 } } }
```

配置值写错即启动失败，不按缺省处理：解析不了的值（如把数字写成文字、写成 `NaN`）抛 `ArgumentException`，能解析但超出取值范围的值（如 `LockTimeoutSeconds` 为 0）抛 `ArgumentOutOfRangeException`，消息都带出完整的配置键与原值。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `WorkflowName`、`ActivityType`、`WorkflowIdentifiers` | 定义名（`^[a-z0-9][a-z0-9._-]{0,127}$`）、活动类型名、节点与转移 ID 文法与 UUID v7 标识 |
| `WorkflowDefinition`、`WorkflowNode`、`WorkflowTransition`、`WorkflowVariableDefinition`、`WorkflowRetryPolicy`、`WorkflowTimeoutAction`、`WorkflowDefinitionStatus` | 定义模型 |
| `WorkflowDefinitionBuilder`、`WorkflowNodeBuilder` | 流式构造定义 |
| `WorkflowDefinitionValidator` | 发布与启动前的校验，`Errors` 列出全部问题 |
| `WorkflowJson` | 定义、实例、节点实例、书签、事件数据的 JSON 往返与深拷贝 |
| `IWorkflowDefinitionCatalog`、`WorkflowDefinitionCatalogExtensions` | 定义生命周期：创建、更新、发布、新版本、停用、归档、删除、查询、活动描述符 |
| `WorkflowDefinitionQuery`、`WorkflowDefinitionSummary`、`WorkflowDefinitionPage` | 定义列表 `QueryAsync`：按关键字（名字、显示名、说明）、名字、分类、状态、创建时间区间过滤，按排序项 `Sorts`（`WorkflowDefinitionSort`：白名单字段加方向，有序、可多个、最多 6 项）排序（空时名字升序、版本降序），返回摘要的一页与总数；`Limit` 缺省 100、最大 1000，分页或排序字段越界是 `WORKFLOW_VALIDATION_002`（ADR-0096、ADR-0102） |
| `WorkflowDefinitionSort`、`WorkflowInstanceSort`、`WorkflowDefinitionSortField`、`WorkflowInstanceSortField`、`WorkflowSortKeys` | 列表排序的白名单与排序键：定义 `Name` / `Title` / `Version` / `Status` / `CreatedAt` / `PublishedAt`，实例 `CreatedAt` / `EndedAt` / `Status` / `DefinitionName` / `Title`；标题按前 256 个字符（见 [工作流指南的列表排序](../guide/workflow#列表排序)，ADR-0102） |
| `WorkflowKeyword` | 列表查询关键字的折叠与匹配规则（见 [工作流指南的关键字](../guide/workflow#关键字)） |
| `IWorkflowEngine`、`WorkflowEngineExtensions`、`WorkflowStartRequest`、`WorkflowReusePolicy` | 启动、恢复、信号、实例控制与调用方分区的实例查询（`FindInstanceAsync`、`ListInstancesAsync`、`CountInstancesAsync`、`NodeInstancesAsync`、`BookmarksAsync`（实例全部等待点的摘要 `WorkflowBookmarkSummary`，不含载荷，ADR-0096）；`WorkflowInstanceQuery` 与 `WorkflowPurgeRequest` 的分页 / 条数越界不在构造时抛出，以 `WORKFLOW_VALIDATION_002` 返回）；单条写法是扩展方法 |
| `WorkflowJump`、`WorkflowMigration`、`WorkflowVariableUpdate`、`WorkflowPurgeRequest` | 跳转、迁移、改变量、清理的请求 |
| `WorkflowInstance`、`WorkflowNodeInstance`、`WorkflowBookmark`、`BookmarkKinds`、`WorkflowInstanceStatus`、`WorkflowNodeInstanceStatus`、`WorkflowFault`、`WorkflowInstanceQuery` | 运行时模型与实例查询（`Limit` 缺省 100、最大 1000；一组关联 ID `CorrelationIds` 最多 1000 个、空集合不匹配，只取顶层 `TopLevelOnly`，关键字 `Keyword` 匹配实例标题或关联 ID，排序项 `Sorts`（`WorkflowInstanceSort`，有序、可多个、最多 5 项）空时创建时间降序，ADR-0096、ADR-0099、ADR-0102） |
| `IActivity`、`IResumableActivity`、`ICompensableActivity`、`ActivityContext`、`ActivityResumeContext`、`ActivityOutcome`、`ActivityOutgoing`、`ActivityDescriptor`、`ActivityRegistry` | 活动契约、执行上下文、返回值、出边模式与设计器描述符 |
| `ActivityTypes` 与 `StartActivity`、`UserTaskActivity`、`HttpActivity` 等 | 内置活动的类型名与属性名常量 |
| `WorkflowHttpClient` | `Http` 活动用的 HTTP 客户端（单例，可替换；缺省的过[出站地址策略](../guide/outbound)）；`SendAsync(request, ct)` 不另计时，超时由活动按引擎时钟落成令牌传进来、按令牌归因（ADR-0097） |
| `IWorkflowOutcomeHandler`、`WorkflowOutcomeContext` | `End` 节点带结果时、结束实例之前调用 |
| `IWorkflowTaskService`、`WorkflowTaskServiceExtensions`、`UserTask`、`UserTaskQuery`、`UserTaskPage`、`UserTaskCompletion`、`UserTaskTransfer`、`UserTaskAddition`、`UserTaskClaim`、`UserTaskRelease`、`UserTaskReturn`、`UserTaskUrge`、`UserTaskCompletionPolicy`、`UserTaskOutcomes` | 用户任务；`QueryAsync` 的过滤（含关键字 `Keyword`：任务标题、实例标题、定义名或关联 ID，ADR-0096、ADR-0099）、排序与分页下推到存储，返回 `UserTaskPage(Items, Total)`，`Limit` 最大 1000，越界是 `WORKFLOW_VALIDATION_002` |
| `IWorkflowExternalTaskService`、`ExternalTask`、`ExternalTaskCompletion`、`ExternalTaskFailure`、`ExternalTaskLockExtension` | 外部任务 |
| `IAssigneeProvider`、`AssigneeRule`、`AssigneeResolution`、`AssigneeRuleTypes`、`EmptyAssigneeStrategy` | 指派规则；内置 `User`、`Starter`、`Expression` |
| `IWorkflowExpressionEvaluator`、`WorkflowExpressionEvaluator`、`ExpressionScope` | 表达式：`Evaluate` 返回 `Result<Maybe<JsonNode>>`、`EvaluateCondition`、`Render`、`Validate` |
| `WorkflowVariables` | `JsonObject` 变量的视图与类型化读取（`Get<T>`、`TryGet<T>`） |
| `IWorkflowDefinitionStore`、`IWorkflowInstanceStore`、`IWorkflowBookmarkStore`、`IWorkflowTransaction`、`IWorkflowInstanceLock` | Provider 契约；存储是作用域服务，每个方法的第一个参数是分区（`tenantId`，空为平台）；书签存储的 `AvailableAsync` 按分区、种类、键与可领取时刻限量查询，供外部任务领取下推条数；`ListUserTasksAsync` / `CountUserTasksAsync` 按待办查询的条件列出与计数运行中实例的用户任务书签；`IWorkflowTransaction` 回答能不能加入调用方事务（`IsActive`）、能不能写（`EnsureWritable`） |
| `DueBookmarkPoller`、`WorkflowOptions` | 到期书签轮询与保留期清理（应用不在服务状态的周期什么也不做、不记告警，周期中途进入排空即停下、不告警；手动的 `PollOnceAsync` / `HousekeepAsync` 照实告警）；引擎选项 |
| `WorkflowTopics`、`WorkflowEventData` | `workflow.*` 事件 |
| `WorkflowErrors`、`WorkflowMessages`、`WorkflowCapabilities` | 错误码、消息键（目录 `workflow`，故障原因也是消息键，`WorkflowFault.Localize()` / `ToError()` 按调用方的区域本地化，ADR-0105）、能力 ID |

`WorkflowServiceCollectionExtensions` 提供 `AddActivity<TActivity>`、`AddAssigneeProvider<TProvider>`、`AddWorkflowOutcomeHandler<THandler>`、`GetActivityRegistry`，以及给 Provider 用的 `AddWorkflowEngine(options)`。`AddWorkflowEngine` 把引擎、目录、任务服务、外部任务服务注册为作用域服务，表达式求值器、轮询器与 `WorkflowHttpClient` 注册为单例，并登记 19 个内置活动。

## 使用示例

定义并发布一个顺序流程，启动后读取结果：

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;

public sealed class GreetingFlow(IWorkflowDefinitionCatalog catalog, IWorkflowEngine engine)
{
    public async ValueTask<string?> RunAsync(string name, CancellationToken cancellationToken)
    {
        var definition = WorkflowDefinitionBuilder.Create("demo.greeting")
            .AddVariable("name", required: true)
            .AddStart()
            .AddNode("greet", ActivityTypes.SetVariable, configure: node => node
                .WithProperty(SetVariableActivity.ExpressionsProperty, new JsonObject { ["greeting"] = "'hello ' + upper(name)" }))
            .AddEnd()
            .AddTransition("start", "greet").AddTransition("greet", "end")
            .Build();
        var draft = (await catalog.CreateAsync(definition, cancellationToken)).ValueOrThrow();
        (await catalog.PublishAsync(draft.Id, cancellationToken)).ValueOrThrow();

        var instance = (await engine.StartAsync(new WorkflowStartRequest { Name = "demo.greeting", Variables = new JsonObject { ["name"] = name } }, cancellationToken)).ValueOrThrow();
        // 没有等待节点，第一个波次就走到了 End
        return instance.Status == WorkflowInstanceStatus.Completed ? instance.Variables["greeting"]?.GetValue<string>() : null;
    }
}
```

## 行为与限制

- 校验失败返回 `WORKFLOW_VALIDATION_001`，`details.errors` 列出全部问题（每条一个 `SchemaViolation`：定义 JSON 里的路径、消息键与参数，宿主写成 Problem Details 的字段级 `errors` 并按调用方区域翻译，ADR-0106）；创建草稿不校验，发布与启动时校验。
- 按名字启动取最新的已发布版本；发布新版本不改变旧版本状态，已在跑的实例留在原版本，用 `MigrateAsync` 迁移（只迁到同一名字空间里的同名版本）。
- 分区隔离（ADR-0077）：平台一个分区、每个租户一个分区，调用只看得到、改得动自己分区的定义、实例与书签，别的分区的按不存在处理；平台与租户对等，平台也看不到租户的。没有全局定义：名字与版本号在分区里编号，按名字解析只在自己的分区里找。`WorkflowStartRequest` 与 `WorkflowInstanceQuery` 没有租户字段，分区取当前调用。
- 存储契约每个方法带分区参数，写入的记录必须属于该分区（不等抛 `ArgumentException`）。
- 事务：在调用里加入调用方的事务，业务回滚时引擎写入一起回滚；只读调用里写入在执行活动之前返回 `PERSISTENCE_UNEXPECTED_001`；在调用之外自己开调用，应用不在服务状态时 `WORKFLOW_UNAVAILABLE_001`。事件经作用域的 `IMessagePublisher` 在事务里发出，装配了发件箱才随事务提交。
- 一个波次最多执行 `MaxNodeExecutionsPerBurst` 个节点；子流程最深 `MaxSubWorkflowDepth` 层；表达式缓存满了整体清空。
- 活动的取消令牌是调用的令牌，取消即回滚。
- 未知的根变量是表达式错误（`WORKFLOW_VALIDATION_003`），null 结果是成功的无值（ADR-0059）。
- 不提供 `Script` 活动与 BPMN XML 导入。
- 事件只在装配了消息能力时发布，发布失败只记日志。

| 错误码 | 含义 |
| --- | --- |
| `WORKFLOW_VALIDATION_001` | 定义不合法 |
| `WORKFLOW_VALIDATION_002` | 启动或操作的输入不合法（含待办 / 实例查询的分页与清理的条数越界，HTTP 400） |
| `WORKFLOW_VALIDATION_003` | 表达式语法不合法或求值失败 |
| `WORKFLOW_NOTFOUND_001` | 定义不存在、没有已发布版本或在别的分区 |
| `WORKFLOW_NOTFOUND_002` | 实例不存在或在别的分区 |
| `WORKFLOW_NOTFOUND_003` | 书签（用户任务、外部任务）不存在或在别的分区 |
| `WORKFLOW_NOTFOUND_004` | 活动类型或指派规则类型未登记 |
| `WORKFLOW_CONFLICT_001` | 实例状态不允许该操作 |
| `WORKFLOW_CONFLICT_002` | 定义状态不允许该操作 |
| `WORKFLOW_CONFLICT_003` | 书签不可恢复 |
| `WORKFLOW_CONFLICT_004` | 指派人已在任务上或任务已被认领 |
| `WORKFLOW_CONFLICT_005` | 同一关联 ID 下已有未结束的实例 |
| `WORKFLOW_DENIED_001` | 操作者不是任务的指派人 |
| `WORKFLOW_DENIED_002` | 工作者没有持有外部任务的锁 |
| `WORKFLOW_TIMEOUT_001` | 等实例锁超时 |
| `WORKFLOW_UNAVAILABLE_001` | 工作流存储不可达；或在调用之外被调用时应用不在服务状态 |
| `WORKFLOW_UNAVAILABLE_002` | 工作流存储容量已满 |
| `WORKFLOW_UNEXPECTED_001` | 活动抛出未处理异常或引擎内部故障 |

## 相关

- [工作流](../guide/workflow)
- [Workflow.InMemory](./workflow-inmemory)
- [Workflow.Database](./workflow-database)
- [Scheduling.Abstractions](./scheduling-abstractions)
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0021《工作流：持久化执行》；ADR-0016、ADR-0020、ADR-0059、ADR-0077
