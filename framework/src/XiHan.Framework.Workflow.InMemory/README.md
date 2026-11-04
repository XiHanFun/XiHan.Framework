# XiHan.Framework.Workflow.InMemory

内存工作流默认实现（Ring 3，`role: default`，RFC-0021）：提供 `workflow.execution@1`。

| 类型 | 用途 |
| --- | --- |
| `InMemoryWorkflowFeature` | 装配存储、实例锁与引擎，启动时起 `DueBookmarkPoller` 的循环（应用不在服务状态的周期什么也不做、不记告警），停止时等进行中的轮询结束 |
| `InMemoryWorkflowDefinitionStore` / `InMemoryWorkflowInstanceStore` / `InMemoryWorkflowBookmarkStore` | 有界内存表，按分区（平台 / 租户）存放，互不可见；保存与返回都是深拷贝（读己之写）；容量满 → `WORKFLOW_UNAVAILABLE_002` |
| `InMemoryWorkflowTransaction` | `IWorkflowTransaction`：在调用里即可加入；没有事务，写入立即生效、调用回滚不撤销；只读的调用（操作契约只读）里写入在取锁、执行活动之前以 `PERSISTENCE_UNEXPECTED_001` 拒绝，与数据库实现相同，没装持久化也一样（ADR-0099） |
| `InMemoryWorkflowInstanceLock` | 每个实例一个进程内信号量，没人用时回收；等待超时 → `WORKFLOW_TIMEOUT_001` |
| `InMemoryWorkflowOptions` | `XiHan:Workflow:InMemory`：`MaxDefinitions`（1000）、`MaxInstances`（10000）、`MaxNodeInstances`（100000）、`MaxBookmarks`（100000）、`PollIntervalSeconds`（1）、`BatchSize`（100） |

引擎选项在 `XiHan:Workflow`（`MaxNodeExecutionsPerBurst`、`MaxSubWorkflowDepth`、`LockTimeoutSeconds`、`NotResumableBackoffSeconds`、`ExpressionCacheSize`、`RetentionDays`、`HousekeepingIntervalSeconds`、`HttpTimeoutSeconds`），见 `Workflow.Abstractions`。两处选项的值写错都在启动时抛出。

单进程：实例锁与轮询只在本进程内有效，多实例部署换 `Workflow.Database`（租约行实例锁、领导节点轮询）。非事务：业务回滚不撤销引擎写入，要求"写申请并发起流程"原子的应用换 `Workflow.Database`。列表排序在内存里做，文本按序数比较、状态按生命周期的先后（ADR-0103）、空时间当作最小（数据库实现按列的排序规则与库的空值规则，ADR-0102）。通过 `Workflow.Conformance` 全部用例（事务相关的用例以 `JoinsCallerTransaction = false` 断言，只读调用的用例与数据库实现同一断言）。特性另登记持久化的消息目录（`PersistenceMessages.Catalog`），只读拒绝的消息在没装持久化的宿主上也能翻译。

## 依赖方向

依赖 `XiHan.Framework.Workflow.Abstractions` 与 `XiHan.Framework.Persistence.Abstractions`（只读拒绝的错误码与消息键）。被 `Profiles.Full` 引用。

## 状态

P0：可用（Preview）。通过 `Workflow.Conformance` 的存储、引擎与任务全部用例；加内存持久化、内存消息与发件箱的宿主也通过引擎与任务全部用例（ADR-0096 第六节）。
