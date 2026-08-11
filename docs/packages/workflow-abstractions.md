# XiHan.Framework.Workflow.Abstractions

> 工作流抽象包：流程定义模型、活动契约、运行时实例/书签模型、存储端口与人工任务契约。**不含任何执行实现。**

- **NuGet**：`XiHan.Framework.Workflow.Abstractions`
- **模块类**：`XiHanWorkflowAbstractionsModule`
- **所在层**：应用层（抽象）
- **关键依赖**：仅 `XiHan.Framework.Core`

## 概述

本包只定义**契约与数据模型**，让「定义流程」「实现活动」「实现存储」三件事可以独立于执行引擎完成。典型用法是：领域项目只引用本包声明流程定义与自定义活动，宿主项目才引用 [XiHan.Framework.Workflow](./workflow) 装配引擎。

## 何时使用

- 想在不引入执行引擎的项目（如领域层、契约包）里声明流程定义或自定义活动。
- 想实现自己的存储后端（数据库 / Redis），只需要实现三个 Store 端口。
- 想把工作流事件的订阅方与引擎解耦。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Workflow.Abstractions
```

```csharp
[DependsOn(typeof(XiHanWorkflowAbstractionsModule))]
public class MyModule : XiHanModule { }
```

模块本身不注册任何服务——它只是让本包的程序集参与模块化装配。

## 主要 API / 类型

### 流程定义

| 类型 | 说明 |
| --- | --- |
| `WorkflowDefinition` | 流程定义：编码、名称、版本、状态、节点、连线、变量定义 |
| `WorkflowNode` | 节点：节点编码、活动类型编码、属性、重试策略、超时 |
| `WorkflowTransition` | 连线：来源节点、目标节点、条件表达式、是否默认分支 |
| `WorkflowVariableDefinition` | 变量定义：名称、类型、是否必填、默认值 |
| `WorkflowRetryPolicy` | 节点级重试策略 |
| `WorkflowDefinitionStatus` | `Draft`(0) 草稿 / `Published`(1) 已发布（**只有已发布才能启动实例**）/ `Disabled`(2) 已停用（不可启动新实例，存量实例继续运行）/ `Archived`(3) 已归档 |
| `IWorkflowDefinitionManager` | 定义生命周期门面：`CreateAsync` / `UpdateDraftAsync` / `PublishAsync(id)` / `CreateNewVersionAsync(code)` / `DisableAsync(id)` / `ArchiveAsync(id)` / `GetAsync(id)` / `GetPublishedAsync(code, version)` / `GetListAsync(...)` |

### 活动契约

| 类型 | 说明 |
| --- | --- |
| `IWorkflowActivity` | 活动契约：给定 `ActivityExecutionContext` 返回 `ActivityExecutionResult` |
| `IResumableWorkflowActivity` | 可恢复活动：额外实现「书签被恢复时怎么处理」（`ActivityResumeContext`） |
| `ICompensableWorkflowActivity` | 可补偿活动：实例取消且定义启用补偿时按执行逆序回补 |
| `[WorkflowActivity]` | 标注活动的类型编码（定义里的 `ActivityType` 据此匹配） |
| `ActivityExecutionResult` | 执行结果，`ActivityExecutionResultKind` 区分「继续 / 挂起等待书签 / 故障 / 终止」 |
| `ActivityOutgoingBehavior` | 出向行为：走哪些连线（全部 / 按条件 / 指定） |
| `WorkflowActivityTypes` | 内置活动类型编码常量（见 [Workflow 包](./workflow#内置活动) 列表） |

### 运行时模型

| 类型 | 说明 |
| --- | --- |
| `WorkflowInstance` | 流程实例：定义引用、状态、变量、节点实例、父实例信息 |
| `WorkflowInstanceStatus` | `Running`(1) 运行中（含等待书签的空闲态）/ `Suspended`(2) 已挂起 / `Completed`(3) / `Canceled`(4) 已取消（可触发补偿）/ `Faulted`(5) 已故障（可重试）/ `Terminated`(6) 已终止（不补偿、不可恢复） |
| `WorkflowNodeInstance` / `WorkflowNodeInstanceStatus` | 节点实例与其状态 |
| `WorkflowBookmark` / `WorkflowBookmarkRequest` | **书签**：实例挂起后可被外界恢复的等待点 |
| `WorkflowBookmarkKinds` | 书签种类常量（见下表） |
| `WorkflowJoinState` | 汇聚网关的分支到达状态 |
| `WorkflowVariables` / `WorkflowValueConverter` | 变量容器与类型转换 |
| `WorkflowStartRequest` | 启动请求：定义编码/版本、初始变量、业务相关性标识 |

**书签种类**决定恢复来源与 `Key` 的语义：

| 种类 | `Key` / `DueTime` 语义 | 由谁恢复 |
| --- | --- | --- |
| `UserTask` | Key = 受理人标识 | 人工办理 |
| `Timer` | DueTime = 到期时间 | 定时器 Worker 轮询 |
| `Signal` | Key = 信号名称 | `PublishSignalAsync` |
| `SubWorkflow` | Key = 父节点实例标识 | 子流程终态回调 |
| `Retry` | DueTime = 下次重试时间 | 定时器 Worker 轮询 |
| `NodeTimeout` | DueTime = 节点超时时间 | 定时器 Worker 轮询 |

### 存储端口

| 端口 | 职责 |
| --- | --- |
| `IWorkflowDefinitionStore` | 流程定义的持久化 |
| `IWorkflowInstanceStore` | 流程实例的持久化 |
| `IWorkflowBookmarkStore` | 书签的持久化与到期检索 |

实现包默认提供内存实现，业务侧可用 `Replace` 换成数据库/Redis 实现。

### 人工任务

| 类型 | 说明 |
| --- | --- |
| `IWorkflowUserTaskService` | 待办查询与办理门面：`GetPendingAsync(assigneeId)`、`GetPendingByInstanceAsync(instanceId)`、`CompleteAsync(...)`、转办等 |
| `WorkflowUserTask` | 待办任务模型 |
| `UserTaskCompletionPolicy` | `Any`(1) 或签（任一同意即通过、任一拒绝即拒绝）/ `All`(2) 会签（全部同意才通过、任一拒绝一票否决）/ `Sequential`(3) 依次审批 |
| `WorkflowUserTaskOutcomes` | 办理结果常量：`approved` / `rejected` / `timeout`（**允许自定义结果**） |

### 事件与异常

| 类型 | 说明 |
| --- | --- |
| `WorkflowInstanceEvents` | 实例级事件（启动/完成/取消/故障等） |
| `WorkflowNodeEvents` | 节点级事件 |
| `WorkflowUserTaskEvents` | 人工任务事件（创建/办理/转办） |
| `WorkflowException` | 工作流基础异常 |
| `WorkflowDefinitionValidationException` | 定义校验失败（节点/连线/变量不合法） |
| `WorkflowLockTimeoutException` | 实例锁获取超时（并发冲突） |

### 引擎与表达式

| 类型 | 说明 |
| --- | --- |
| `IWorkflowEngine` | 实例生命周期统一入口（实现见 [Workflow 包](./workflow)） |
| `IWorkflowExpressionEvaluator` | 表达式求值端口（条件分支、变量赋值用） |

## 注意事项

- 本包**没有执行能力**：只引用它，流程定义能建、能校验模型，但跑不起来。执行要引 [XiHan.Framework.Workflow](./workflow)。
- 定义模型是**可序列化的纯数据**，流程定义可以落库、从库里读回来跑，不必编译进程序集。
- 书签是整套挂起/恢复机制的核心：实例挂起等待期间**不占用任何线程**。

## 依赖模块

- [XiHan.Framework.Core](./core) — 模块化与依赖注入基础，唯一依赖。

## 相关模块

- [XiHan.Framework.Workflow](./workflow) — 执行引擎与内置活动库。
- [XiHan.Framework.EventBus](./eventbus) — 工作流事件的默认发布通道。
