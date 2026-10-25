# XiHan.Framework.Workflow.InMemory

内存工作流默认实现（Ring 3，`role: default`，RFC-0021）：提供 `workflow.execution@1`。

| 类型 | 用途 |
| --- | --- |
| `InMemoryWorkflowFeature` | 装配存储、实例锁与引擎，应用进入服务状态后启动 `DueBookmarkPoller`，停止时等进行中的轮询结束 |
| `InMemoryWorkflowDefinitionStore` / `InMemoryWorkflowInstanceStore` / `InMemoryWorkflowBookmarkStore` | 有界内存表，保存与返回都是深拷贝（读己之写）；容量满 → `WORKFLOW_UNAVAILABLE_002` |
| `InMemoryWorkflowInstanceLock` | 每个实例一个进程内信号量，没人用时回收；等待超时 → `WORKFLOW_TIMEOUT_001` |
| `InMemoryWorkflowOptions` | `XiHan:Workflow:InMemory`：`MaxDefinitions`（1000）、`MaxInstances`（10000）、`MaxNodeInstances`（100000）、`MaxBookmarks`（100000）、`PollIntervalSeconds`（1）、`BatchSize`（100） |

引擎选项在 `XiHan:Workflow`（`MaxNodeExecutionsPerBurst`、`MaxSubWorkflowDepth`、`LockTimeoutSeconds`、`NotResumableBackoffSeconds`、`ExpressionCacheSize`）。

单进程：多实例部署换 `Workflow.Database`（后续）。通过 `Workflow.Conformance` 全部用例。

## 依赖方向

依赖 `XiHan.Framework.Workflow.Abstractions`。被 `Profiles.Full` 引用。

## 状态

5.x P0：可用（Preview）。
