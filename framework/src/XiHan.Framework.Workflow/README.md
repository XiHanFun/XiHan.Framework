# XiHan.Framework.Workflow

曦寒框架工作流库。

## 概述

通用工作流引擎的完整实现：令牌驱动的图解释执行、企业审批（或签/会签/依次/转办/加签/超时）、
并行分支与汇聚、延时与信号等待、子流程与遍历编排、失败重试与补偿、版本化流程定义与 JSON 设计器格式。
持久化默认使用进程内内存存储，应用侧可替换为数据库/Redis 实现获得崩溃恢复能力。

## 核心能力

| 能力 | 说明 |
| --- | --- |
| 执行引擎 | `IWorkflowEngine`：启动/书签恢复/信号/挂起/取消/终止/重试；实例级分布式锁单写者；执行批次逐节点持久化；防失控环路步数上限 |
| 标准活动库 | Start、End、Terminate、Fault、Log、SetVariable、Decision（独占网关）、Parallel、Join（WaitAll/WaitAny）、Delay、WaitSignal、UserTask、Http、Script（Roslyn）、PublishEvent、SubWorkflow、ForEach |
| 审批 | 或签/会签/依次审批、拒绝一票否决、超时结果流转（`outcome == 'timeout'`）、转办、加签、抄送事件、审批轨迹 |
| 表达式 | 内置轻量表达式语言（变量导航/算术/比较/逻辑/内置函数），出边条件 fail-closed，`{{ }}` 模板插值 |
| 定义管理 | `IWorkflowDefinitionManager`：草稿 → 发布 → 停用 → 归档；同编码多版本，实例绑定版本；发布前结构校验（唯一开始节点、引用完整、表达式语法、可达性） |
| 调度 | `WorkflowTimerWorker`：轮询到期书签（延时/重试/节点超时），分布式锁集群单活，租户上下文还原 |
| 定义方式 | `WorkflowDefinitionBuilder` 代码流式构建；`WorkflowDefinitionJsonSerializer` 设计器 JSON 往返 |
| 事件 | 实例/节点/人工任务生命周期事件经本地事件总线发布（发布失败不阻断引擎） |

## 依赖关系

Caching（分布式锁）、Core、DistributedIds（标识）、EventBus（事件）、MultiTenancy（租户）、Script（脚本活动）、Timing（时钟）、Workflow.Abstractions。

## 配置与约定

```json
{
  "XiHan": {
    "Workflow": {
      "MaxNodeExecutionsPerBurst": 1000,
      "InstanceLockExpirySeconds": 120,
      "InstanceLockAcquireTimeoutSeconds": 10,
      "Worker": {
        "IsTimerEnabled": true,
        "PollPeriodMilliseconds": 5000,
        "MaxBookmarkFetchCount": 100
      }
    }
  }
}
```

- 引擎不接管数据库事务，业务活动自行管理事务边界；
- 内存存储仅适用单实例部署，多实例集群须替换三个存储为共享持久化实现；
- 活动输出合并进实例顶层变量，`outcome` 仅在出边条件求值时可见。

## 使用方式

```csharp
[DependsOn(
    typeof(XiHanWorkflowModule)
)]
public class YourModule : XiHanModule
{
}
```

```csharp
// 定义并发布
var definition = WorkflowDefinitionBuilder.Create("leave-request", "请假审批")
    .AddVariable("amount", required: true)
    .AddStart()
    .AddDecision("gateway")
    .AddUserTask("manager", "经理审批", node => node
        .WithProperty("Assignees", new List<string> { "1001", "1002" })
        .WithProperty("CompletionPolicy", "All")
        .WithTimeout(86400))
    .AddEnd()
    .AddTransition("start", "gateway")
    .AddTransition("gateway", "manager", "amount > 10000")
    .AddDefaultTransition("gateway", "end")
    .AddTransition("manager", "end")
    .Build();

var created = await definitionManager.CreateAsync(definition);
await definitionManager.PublishAsync(created.Id);

// 启动实例
var instance = await engine.StartAsync(new WorkflowStartRequest
{
    DefinitionCode = "leave-request",
    Variables = new() { ["amount"] = 20000 },
    CorrelationId = "LEAVE-2026-001"
});

// 审批
var tasks = await userTaskService.GetPendingAsync("1001");
await userTaskService.CompleteAsync(tasks[0].TaskId, "1001", WorkflowUserTaskOutcomes.Approved, "同意");
```

## 扩展点

- 自定义活动：实现 `IWorkflowActivity`（可选 `IResumableWorkflowActivity`/`ICompensableWorkflowActivity`），标注 `[WorkflowActivity("YourType")]`，`services.AddXiHanWorkflowActivity<YourActivity>()` 注册；
- 持久化：以 `Replace` 覆盖 `IWorkflowDefinitionStore`/`IWorkflowInstanceStore`/`IWorkflowBookmarkStore`；
- 表达式语言：替换 `IWorkflowExpressionEvaluator`；
- 事件出口：替换 `IWorkflowEventPublisher`（如改发分布式总线）。

## 已知限制

- 内存存储仅进程内有效，崩溃即丢失全部运行状态；生产环境请替换为共享持久化存储；
- 子流程终态回调依赖后置动作投递（锁竞争自动重试），进程在"子实例已终态、父实例未回调"窗口内崩溃时需人工核对父实例等待点；
- `WaitAny` 汇聚在环路回边场景会吞掉补齐波次的回边令牌，环路请使用 `WaitAll`。

## 目录结构

- `Activities/`：活动注册表、活动基类与 `BuiltIn/` 内置活动库
- `Builders/`：流程定义流式构建器与设计器 JSON 序列化
- `Definitions/`：定义管理器与发布前结构校验器
- `Engine/`：执行引擎、定义图索引、实例锁获取器
- `Events/`：工作流事件发布器（本地事件总线出口）
- `Expressions/`：内置表达式求值器与解析器
- `Extensions/`：依赖注入扩展方法
- `Options/`：工作流选项与定时器 Worker 选项
- `Stores/`：三个存储契约的内存默认实现
- `UserTasks/`：人工任务服务、待办映射与载荷键常量
- `Workers/`：定时器轮询 Worker
