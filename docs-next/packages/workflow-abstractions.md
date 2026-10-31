# XiHan.Framework.Workflow.Abstractions

工作流子系统的契约与引擎：定义模型与校验、定义目录、19 个内置活动、实例与书签、用户任务与外部任务、表达式语言、存储契约，以及经内核管道执行活动的引擎与到期书签轮询器。能力 `workflow.execution@1` 由 Provider 提供，应用与 Provider 都引用本包。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Workflow.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `workflow.execution@1`（`WorkflowCapabilities.Execution`）的契约，本包不提供能力 |
| 特性入口 | — |
| 配置节 | `XiHan:Workflow`（`WorkflowOptions`，由 Provider 读取） |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Messaging.Abstractions`、`XiHan.Framework.Scheduling.Abstractions`（cron）；`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions`、`Microsoft.Extensions.Logging.Abstractions` 10.0.13 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

引擎、校验器、表达式求值器、活动登记表、指派解析、任务服务、外部任务服务、轮询器与全部内置活动都在本包里（ADR-0016）。Provider 只实现三个存储（定义、实例、书签）与实例锁，再调用 `AddWorkflowEngine(options)` 装配引擎、在特性的生命周期里驱动 `DueBookmarkPoller`。内存实现与数据库实现因此语义一致，由 `Workflow.Conformance` 的存储套件、引擎套件与任务套件约束。

值模型是 JSON：变量、属性、输入输出、节点状态与书签载荷都是 `JsonObject`，`WorkflowJson` 用源生成的上下文做序列化与深拷贝，不靠反射。引擎在实例锁内以波次推进实例，一个波次结束时把实例、节点实例与书签一次写回，事件在波次之后发布。每个节点的活动在新作用域、新调用上下文里经完整管道执行（关联 ID 是实例 ID，租户是实例租户，调用来源 `InvocationProtocols.Workflow`）。

## 何时使用

- 定义流程、启动实例、处理待办：引用本包，再装配一个提供 `workflow.execution` 的 Provider（[Workflow.InMemory](./workflow-inmemory) 或 [Workflow.Database](./workflow-database)）。
- 编写自定义活动、指派规则或结果处理器：实现本包的 `IActivity`、`IAssigneeProvider`、`IWorkflowOutcomeHandler`。
- 实现新的存储 Provider：实现四个存储契约并继承 `Workflow.Conformance` 的套件。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Workflow.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。各 Provider 包与 `Profiles.Full` 都传递引用本包。登记自定义活动与指派规则：

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

解析不了的值被忽略、沿用缺省值；能解析但超出范围的值启动时抛 `ArgumentOutOfRangeException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `WorkflowName`、`ActivityType`、`WorkflowIdentifiers` | 定义名（`^[a-z0-9][a-z0-9._-]{0,127}$`）、活动类型名、节点与转移 ID 文法与 UUID v7 标识 |
| `WorkflowDefinition`、`WorkflowNode`、`WorkflowTransition`、`WorkflowVariableDefinition`、`WorkflowRetryPolicy`、`WorkflowTimeoutAction`、`WorkflowDefinitionStatus` | 定义模型 |
| `WorkflowDefinitionBuilder`、`WorkflowNodeBuilder` | 流式构造定义 |
| `WorkflowDefinitionValidator` | 发布与启动前的校验，`Errors` 列出全部问题 |
| `WorkflowJson` | 定义、实例、节点实例、书签、事件数据的 JSON 往返与深拷贝 |
| `IWorkflowDefinitionCatalog`、`WorkflowDefinitionCatalogExtensions` | 定义生命周期：创建、更新、发布、新版本、停用、归档、删除、查询、活动描述符 |
| `IWorkflowEngine`、`WorkflowEngineExtensions`、`WorkflowStartRequest`、`WorkflowReusePolicy` | 启动、恢复、信号与实例控制；单条写法是扩展方法 |
| `WorkflowJump`、`WorkflowMigration`、`WorkflowVariableUpdate`、`WorkflowPurgeRequest` | 跳转、迁移、改变量、清理的请求 |
| `WorkflowInstance`、`WorkflowNodeInstance`、`WorkflowBookmark`、`BookmarkKinds`、`WorkflowInstanceStatus`、`WorkflowNodeInstanceStatus`、`WorkflowFault`、`WorkflowInstanceQuery` | 运行时模型与实例查询（`Limit` 缺省 100、最大 1000） |
| `IActivity`、`IResumableActivity`、`ICompensableActivity`、`ActivityContext`、`ActivityResumeContext`、`ActivityOutcome`、`ActivityOutgoing`、`ActivityDescriptor`、`ActivityRegistry` | 活动契约、执行上下文、返回值、出边模式与设计器描述符 |
| `ActivityTypes` 与 `StartActivity`、`UserTaskActivity`、`HttpActivity` 等 | 内置活动的类型名与属性名常量 |
| `WorkflowHttpClient` | `Http` 活动用的 HTTP 客户端（单例，可替换） |
| `IWorkflowOutcomeHandler`、`WorkflowOutcomeContext` | `End` 节点带结果时、结束实例之前调用 |
| `IWorkflowTaskService`、`WorkflowTaskServiceExtensions`、`UserTask`、`UserTaskQuery`、`UserTaskCompletion`、`UserTaskTransfer`、`UserTaskAddition`、`UserTaskClaim`、`UserTaskRelease`、`UserTaskReturn`、`UserTaskUrge`、`UserTaskCompletionPolicy`、`UserTaskOutcomes` | 用户任务 |
| `IWorkflowExternalTaskService`、`ExternalTask`、`ExternalTaskCompletion`、`ExternalTaskFailure`、`ExternalTaskLockExtension` | 外部任务 |
| `IAssigneeProvider`、`AssigneeRule`、`AssigneeResolution`、`AssigneeRuleTypes`、`EmptyAssigneeStrategy` | 指派规则；内置 `User`、`Starter`、`Expression` |
| `IWorkflowExpressionEvaluator`、`WorkflowExpressionEvaluator`、`ExpressionScope` | 表达式：`Evaluate` 返回 `Result<Maybe<JsonNode>>`、`EvaluateCondition`、`Render`、`Validate` |
| `WorkflowVariables` | `JsonObject` 变量的视图与类型化读取（`Get<T>`、`TryGet<T>`） |
| `IWorkflowDefinitionStore`、`IWorkflowInstanceStore`、`IWorkflowBookmarkStore`、`IWorkflowInstanceLock` | Provider 契约 |
| `DueBookmarkPoller`、`WorkflowOptions` | 到期书签轮询与保留期清理；引擎选项 |
| `WorkflowTopics`、`WorkflowEventData` | `workflow.*` 事件 |
| `WorkflowErrors`、`WorkflowMessages`、`WorkflowCapabilities` | 错误码、消息键（目录 `workflow`）、能力 ID |

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

- 校验失败返回 `WORKFLOW_VALIDATION_001`，`details.errors` 列出全部问题；创建草稿不校验，发布与启动时校验。
- 按名字启动取最新的已发布版本；发布新版本不改变旧版本状态，已在跑的实例留在原版本，用 `MigrateAsync` 迁移。
- 存储契约不按租户过滤，按租户的过滤由调用方负责；任务服务与信号按当前调用的租户过滤。
- 一个波次最多执行 `MaxNodeExecutionsPerBurst` 个节点；子流程最深 `MaxSubWorkflowDepth` 层；表达式缓存满了整体清空。
- 活动的取消令牌是宿主停止令牌，调用方的令牌只约束等锁与首次写入。
- 未知的根变量是表达式错误（`WORKFLOW_VALIDATION_003`），null 结果是成功的无值（ADR-0059）。
- 不提供 `Script` 活动与 BPMN XML 导入。
- 事件只在装配了消息能力时发布，发布失败只记日志。

| 错误码 | 含义 |
| --- | --- |
| `WORKFLOW_VALIDATION_001` | 定义不合法 |
| `WORKFLOW_VALIDATION_002` | 启动或操作的输入不合法 |
| `WORKFLOW_VALIDATION_003` | 表达式语法不合法或求值失败 |
| `WORKFLOW_NOTFOUND_001` | 定义不存在或没有已发布版本 |
| `WORKFLOW_NOTFOUND_002` | 实例不存在 |
| `WORKFLOW_NOTFOUND_003` | 书签（用户任务）不存在 |
| `WORKFLOW_NOTFOUND_004` | 活动类型或指派规则类型未登记 |
| `WORKFLOW_CONFLICT_001` | 实例状态不允许该操作 |
| `WORKFLOW_CONFLICT_002` | 定义状态不允许该操作 |
| `WORKFLOW_CONFLICT_003` | 书签不可恢复 |
| `WORKFLOW_CONFLICT_004` | 指派人已在任务上或任务已被认领 |
| `WORKFLOW_CONFLICT_005` | 同一关联 ID 下已有未结束的实例 |
| `WORKFLOW_DENIED_001` | 操作者不是任务的指派人 |
| `WORKFLOW_DENIED_002` | 工作者没有持有外部任务的锁 |
| `WORKFLOW_TIMEOUT_001` | 等实例锁超时 |
| `WORKFLOW_UNAVAILABLE_001` | 工作流存储不可达 |
| `WORKFLOW_UNAVAILABLE_002` | 工作流存储容量已满 |
| `WORKFLOW_UNEXPECTED_001` | 活动抛出未处理异常或引擎内部故障 |

## 相关

- [工作流](../guide/workflow)
- [Workflow.InMemory](./workflow-inmemory)
- [Workflow.Database](./workflow-database)
- [Scheduling.Abstractions](./scheduling-abstractions)
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0021《工作流：持久化执行》；ADR-0016、ADR-0020、ADR-0059
