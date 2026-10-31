# XiHan.Framework.Workflow.InMemory

工作流的默认实现，提供 `workflow.execution@1`：有界内存的定义、实例与书签存储，进程内实例锁，单进程轮询到期书签。适用于单实例部署、开发与测试；`Profiles.Full` 已经包含它。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Workflow.InMemory` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `workflow.execution@1`（`role: default`）；依赖 `kernel.hosting@1` |
| 特性入口 | `InMemoryWorkflowFeature`（清单 ID `xihan.workflow.inmemory`） |
| 配置节 | `XiHan:Workflow`（引擎选项）、`XiHan:Workflow:InMemory` |
| 依赖 | `XiHan.Framework.Workflow.Abstractions`；无第三方依赖 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

引擎、目录、任务服务与内置活动都来自抽象包，本包实现四个存储契约：

| 类型 | 做法 |
| --- | --- |
| `InMemoryWorkflowDefinitionStore`、`InMemoryWorkflowInstanceStore`、`InMemoryWorkflowBookmarkStore` | 有界内存表，保存与返回都是深拷贝（读己之写，调用方改不到存储里的对象）；容量满时写入返回 `WORKFLOW_UNAVAILABLE_002`，不淘汰旧数据 |
| `InMemoryWorkflowInstanceLock` | 每个实例一个进程内信号量，没人用时回收；等待超过 `LockTimeout` 返回 `WORKFLOW_TIMEOUT_001` |

特性被能力求解选中提供 `workflow.execution` 时，把四个契约指向本包的实现并调用 `AddWorkflowEngine`；被 `Workflow.Database` 顶替时只注册本包自己的具体类型。应用进入服务状态后，特性按 `PollIntervalSeconds` 启动 `DueBookmarkPoller`，每次取 `BatchSize` 条到期书签逐条恢复；停止时等进行中的轮询结束。

## 何时使用

- 单实例部署、开发环境、测试、演示：零外部基础设施。
- 实例需要跨重启保留，或部署多个实例：换 [Workflow.Database](./workflow-database)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Workflow.InMemory" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Full` 已经装配本包；不用组合包时单独装配：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Workflow.InMemory;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryWorkflowFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Workflow:InMemory:MaxDefinitions` | 整数 ≥ 1 | 1000 | 定义（所有版本）上限 |
| `XiHan:Workflow:InMemory:MaxInstances` | 整数 ≥ 1 | 10000 | 实例上限 |
| `XiHan:Workflow:InMemory:MaxNodeInstances` | 整数 ≥ 1 | 100000 | 节点实例上限 |
| `XiHan:Workflow:InMemory:MaxBookmarks` | 整数 ≥ 1 | 100000 | 书签上限 |
| `XiHan:Workflow:InMemory:PollIntervalSeconds` | 正数 | 1 | 到期书签的轮询周期 |
| `XiHan:Workflow:InMemory:BatchSize` | 整数 ≥ 1 | 100 | 每次轮询最多恢复的书签数 |

引擎选项（锁超时、波次上限、子流程深度、保留期等）在 `XiHan:Workflow`，见 [Workflow.Abstractions](./workflow-abstractions#配置)。

```json
{
  "XiHan": {
    "Workflow": {
      "RetentionDays": 30,
      "InMemory": { "MaxInstances": 10000, "PollIntervalSeconds": 1, "BatchSize": 100 }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `InMemoryWorkflowFeature` | 特性：注册存储与锁，被选中时装配引擎；启动与停止轮询器 |
| `InMemoryWorkflowDefinitionStore` | `IWorkflowDefinitionStore` 实现 |
| `InMemoryWorkflowInstanceStore` | `IWorkflowInstanceStore` 实现 |
| `InMemoryWorkflowBookmarkStore` | `IWorkflowBookmarkStore` 实现 |
| `InMemoryWorkflowInstanceLock` | `IWorkflowInstanceLock` 实现 |
| `InMemoryWorkflowOptions` | 容量、轮询周期与批量 |

## 使用示例

不需要额外代码：装配特性后注入 `IWorkflowDefinitionCatalog`、`IWorkflowEngine`、`IWorkflowTaskService` 使用，写法见[工作流](../guide/workflow)。测试里想立即推进定时与超时，可以从容器取 `DueBookmarkPoller` 手动轮询一次：

```csharp
using XiHan.Framework.Workflow.Abstractions.Engine;

public sealed class WorkflowClock(DueBookmarkPoller poller)
{
    // 返回本次恢复的书签数
    public ValueTask<int> TickAsync(CancellationToken cancellationToken) => poller.PollOnceAsync(100, cancellationToken);
}
```

## 行为与限制

- 进程重启后，定义、实例、书签全部丢失；代码里创建并发布定义的应用每次启动要重新发布。
- 容量满时写入失败（`WORKFLOW_UNAVAILABLE_002`），不会淘汰旧实例；长期运行的应用配置 `XiHan:Workflow:RetentionDays` 自动清理已结束的实例。
- 实例锁只在本进程内互斥，多个实例各用一份本包时彼此看不到对方的数据。
- 本包不注册健康项。
- 通过 `Workflow.Conformance` 的存储、引擎与任务套件。

## 相关

- [工作流](../guide/workflow)
- [Workflow.Abstractions](./workflow-abstractions)
- [Workflow.Database](./workflow-database)
- [Profiles.Full](./profiles-full)
- 规范：RFC-0021《工作流：持久化执行》；ADR-0016
