# XiHan.Framework.Workflow.Abstractions

## 关于

`XiHan.Framework.Workflow.Abstractions` 定义工作流的契约并包含工作流引擎：流程定义与校验、定义目录、内置活动、实例与书签、用户任务与外部任务、表达式语言，以及到期书签的轮询器。它不包含存储的实现，单独引用不能运行流程。

## 主要特性

* 流式构造流程定义：用户任务、决策、并行与汇合、延时、信号、子流程、HTTP 调用等
* 流程实例随业务事务提交或回滚，数据按平台与租户分区隔离
* 用户任务支持会签、转办、加签、认领与催办
* 存储、事务与实例锁是窄契约，由提供程序实现

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Workflow.Abstractions --prerelease
```

### 使用

一般由实现包带入，定义流程或写自定义活动与存储提供程序时才直接引用；运行流程需要同时装配 `Workflow.InMemory` 或 `Workflow.Database`。登记自定义活动，并定义、发布、启动流程：

```csharp
builder.Services.AddActivity<AuditActivity>(new ActivityType("Audit"));

var definition = WorkflowDefinitionBuilder.Create(new WorkflowName("demo.greeting"))
    .AddVariable("name", required: true)
    .AddStart()
    .AddNode("greet", new ActivityType(ActivityTypes.SetVariable), configure: node => node
        .WithProperty(SetVariableActivity.ExpressionsProperty, new JsonObject { ["greeting"] = "'hello ' + upper(name)" }))
    .AddEnd()
    .AddTransition("start", "greet").AddTransition("greet", "end")
    .Build();
var draft = (await catalog.CreateAsync(definition, ct)).ValueOrThrow();
await catalog.PublishAsync(draft.Id, ct);
var instance = await engine.StartAsync(new WorkflowStartRequest { Name = "demo.greeting", Variables = new JsonObject { ["name"] = "alice" } }, ct);
```

## 主要类型

此库提供的主要类型：

* `WorkflowDefinitionBuilder`：流式构造流程定义
* `IWorkflowDefinitionCatalog`：定义的创建、发布、停用与查询
* `IWorkflowEngine`：启动实例、发信号、挂起、恢复、取消与查询
* `IWorkflowTaskService`、`IWorkflowExternalTaskService`：用户任务与外部任务
* `IActivity`、`IAssigneeProvider`：自定义活动与指派规则
* `WorkflowOptions`：`XiHan:Workflow` 配置项

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/workflow-abstractions)

## 相关包

* 内存实现：[XiHan.Framework.Workflow.InMemory](https://www.nuget.org/packages/XiHan.Framework.Workflow.InMemory)
* 数据库实现：[XiHan.Framework.Workflow.Database](https://www.nuget.org/packages/XiHan.Framework.Workflow.Database)
* 一致性套件：[XiHan.Framework.Workflow.Conformance](https://www.nuget.org/packages/XiHan.Framework.Workflow.Conformance)

## 反馈与贡献

`XiHan.Framework.Workflow.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
