# XiHan.Framework.Workflow.Abstractions

曦寒框架工作流抽象包。

## 概述

定义通用工作流引擎的全部契约与被动模型，不含任何实现。业务包或第三方扩展包只需引用本包即可：

- 编写自定义活动（`IWorkflowActivity` / `IResumableWorkflowActivity` / `ICompensableWorkflowActivity`）；
- 订阅流程生命周期事件（`Events/` 下的事件记录）；
- 替换存储实现（`IWorkflowDefinitionStore` / `IWorkflowInstanceStore` / `IWorkflowBookmarkStore`）；
- 替换表达式求值器（`IWorkflowExpressionEvaluator`）。

## 核心能力

| 分组 | 内容 |
| --- | --- |
| Definitions | 流程定义模型：`WorkflowDefinition`（编码 + 版本 + 状态）、`WorkflowNode`（活动类型 + 属性 + 重试/超时/失败续行）、`WorkflowTransition`（条件 + 优先级 + 默认边）、`IWorkflowDefinitionManager`（草稿/发布/版本管理） |
| Runtime | 运行时模型：`WorkflowInstance`（变量 + 汇聚波次 + 父子链接）、`WorkflowNodeInstance`（执行历史 + 活动私有状态）、`WorkflowBookmark`（可恢复等待点）、`WorkflowVariables`（类型安全变量容器）、`WorkflowValueConverter`（JSON 持久化往返归一化） |
| Activities | 活动契约：执行/恢复/补偿接口、`WorkflowActivityAttribute`（类型编码 + 出边流转行为）、`ActivityExecutionResult`（完成/挂起/故障） |
| Engine | `IWorkflowEngine`：启动、书签恢复、信号发布、挂起/恢复、取消、终止、重试 |
| Stores | 三个存储契约，查询语义以 XML 注释形式随接口声明 |
| Expressions | `IWorkflowExpressionEvaluator`：表达式求值、条件求值（fail-closed）、`{{ }}` 模板渲染 |
| UserTasks | `IWorkflowUserTaskService`：待办查询、办理、转办、加签；`UserTaskCompletionPolicy`：或签/会签/依次审批 |
| Events | 实例/节点/人工任务生命周期事件记录 |

## 依赖关系

仅依赖 `XiHan.Framework.Core`。

## 配置与约定

本包不含配置项（选项类位于实现包 `XiHan.Framework.Workflow`）。契约层约定：

- 条件表达式求值 fail-closed：结果必须为布尔值，未知变量、非法语法一律抛出异常而非静默跳过；
- 存储接口的查询语义（过滤、排序、上限）以 XML 注释形式随方法声明，持久化实现必须遵守；
- 存储查询不做租户过滤，租户隔离由引擎与任务服务在查询结果上按环境租户执行；
- 书签一经消费即失效，恢复必须携带幂等语义（引擎在实例锁内二次校验）；
- 变量值经 JSON 持久化往返后为 `JsonElement`，统一经 `WorkflowValueConverter` 归一化后使用。

## 使用方式

业务模块声明依赖：

```csharp
[DependsOn(
    typeof(XiHanWorkflowAbstractionsModule)
)]
public class YourModule : XiHanModule
{
}
```

完整引擎请引用实现包 `XiHan.Framework.Workflow`。

## 扩展点

- 自定义活动：实现 `IWorkflowActivity` 并标注 `[WorkflowActivity("YourType")]`，在实现包选项中注册；
- 自定义存储：实现三个存储接口后以 `Replace` 方式覆盖默认内存实现；
- 自定义表达式语言：替换 `IWorkflowExpressionEvaluator` 注册。

## 目录结构

- `Definitions/`：流程定义模型与定义管理器契约
- `Runtime/`：实例、节点实例、书签、变量容器与值转换器
- `Activities/`：活动契约、执行上下文与执行结果
- `Engine/`：工作流引擎契约
- `Stores/`：定义/实例/书签存储契约
- `Expressions/`：表达式求值契约
- `UserTasks/`：人工任务服务契约与待办视图
- `Events/`：实例/节点/人工任务生命周期事件
- `Exceptions/`：工作流异常与定义校验异常
