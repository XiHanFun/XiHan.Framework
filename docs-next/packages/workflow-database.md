# XiHan.Framework.Workflow.Database

工作流的数据库 Provider，提供 `workflow.execution@1`。定义、实例、节点实例与书签存放在应用的关系库里，经 `persistence.relational@1` 的仓储读写；实例锁与到期书签轮询的领导权都是同一张表里的租约行，多个节点共用一个库。

适用于多实例部署，以及实例需要跨进程重启保留的场景。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Workflow.Database` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `workflow.execution@1`（`role: provider`）；依赖 `kernel.hosting@1`、`persistence.relational@1` |
| 特性入口 | `DatabaseWorkflowFeature`（清单 ID `xihan.workflow.database`） |
| 配置节 | `XiHan:Workflow`（引擎选项）、`XiHan:Workflow:Database` |
| 依赖 | `XiHan.Framework.Workflow.Abstractions`、`XiHan.Framework.Persistence.Abstractions`；不引用数据库驱动 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

引擎仍是抽象包的 `WorkflowEngine`，本包实现四个存储契约并驱动轮询：

| 组件 | 做法 |
| --- | --- |
| 三个存储（`Storage.DatabaseWorkflowDefinitionStore` 等） | 单例；每个操作在根提供者的新作用域里经 `App.InvokeAsync` 走完整管道，仓储在 Transaction 阶段内使用，返回即提交 |
| 实例锁 `Locking.DatabaseWorkflowInstanceLock` | 租约式分布式锁：插入行即拿到；过期的行按并发戳接管；未过期就等待（50 毫秒起每次翻倍、封顶 500 毫秒），超过 `LockTimeout` 返回 `WORKFLOW_TIMEOUT_001`；持有期间每三分之一租约续约一次；释放只删自己的行 |
| 轮询 `Polling.DueBookmarkPollingLoop` + `Polling.PollerLeadership` | 每个周期先拿或续资源 `workflow.poller` 的租约，持有时才轮询到期书签 |

存储不加入调用方的工作单元，这是实例锁成立的前提：锁释放之前，实例的写入已经提交，别的节点拿到锁时读到的就是最新状态（ADR-0020）。轮询的领导权只为节省资源：多个节点同时轮询也不会重复执行，到期书签的恢复由实例锁串行化。

每张表是过滤与排序用的列加一列整份模型的 JSON 文档（`Document`）。实例、节点实例与书签另有写入序号 `Sequence`，首次写入时分配、替换时保留，作同一时刻排序的次关键字（UUID v7 只到毫秒单调，不能当写入顺序用）。租户只是普通列，实体不实现 `IMultiTenant`，存储不按租户过滤。

## 何时使用

- 多实例部署：实例锁保证一个实例同时只有一个执行者，任一节点都能推进任一实例。
- 单实例但要求进程重启后实例、待办与定时继续有效。
- 开发、测试与演示用 [Workflow.InMemory](./workflow-inmemory) 即可。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Workflow.Database" Version="5.0.0-preview.1" />
<PackageReference Include="XiHan.Framework.Persistence.SqlSugar" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。装配一个持久化 Provider 和本包；装配后顶替默认的 `Workflow.InMemory`：

```csharp
using SqlSugar;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Workflow.Database;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseWorkflowFeature>());
```

用 EF Core 的应用在自己的 `DbContext` 里登记五个实体，并给 `Document` 列用长文本类型：

```csharp
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Workflow.Database.Entities;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowDefinitionRecord>(static d =>
        {
            d.Property(static x => x.Document).HasColumnType("text");
            d.HasIndex(static x => new { x.Name, x.Version });
        });
        modelBuilder.Entity<WorkflowInstanceRecord>(static i =>
        {
            i.Property(static x => x.Document).HasColumnType("text");
            i.HasIndex(static x => new { x.DefinitionName, x.Status, x.CreatedAt });
        });
        modelBuilder.Entity<WorkflowNodeInstanceRecord>(static n =>
        {
            n.Property(static x => x.Document).HasColumnType("text");
            n.HasIndex(static x => x.InstanceId);
        });
        modelBuilder.Entity<WorkflowBookmarkRecord>(static b =>
        {
            b.Property(static x => x.Document).HasColumnType("text");
            b.HasIndex(static x => x.InstanceId);
            b.HasIndex(static x => new { x.Kind, x.Key });
            b.HasIndex(static x => x.DueAt);
        });
        modelBuilder.Entity<WorkflowLockRecord>();
    }
}
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Workflow:Database:PollIntervalSeconds` | 正数 | 1 | 到期书签的轮询周期 |
| `XiHan:Workflow:Database:BatchSize` | 整数 ≥ 1 | 100 | 每次轮询最多恢复的书签数 |
| `XiHan:Workflow:Database:LockLeaseSeconds` | 正数 | 60 | 实例锁与轮询领导权的租约时长 |
| `XiHan:Workflow:Database:HolderId` | 字符串 | `机器名:进程 ID:8 位随机十六进制` | 租约持有者标识，每个节点唯一 |
| `XiHan:Workflow:Database:HealthProbeIntervalSeconds` | 正数 | 15 | 健康项重探间隔 |

引擎选项（`LockTimeoutSeconds`、`MaxNodeExecutionsPerBurst`、`RetentionDays` 等）在 `XiHan:Workflow`，见 [Workflow.Abstractions](./workflow-abstractions#配置)。

```json
{
  "XiHan": {
    "Bindings": { "db": "Host=localhost;Database=app;Username=app" },
    "Workflow": {
      "LockTimeoutSeconds": 10,
      "RetentionDays": 180,
      "Database": { "PollIntervalSeconds": 1, "BatchSize": 100, "LockLeaseSeconds": 60, "HolderId": "node-1" }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `DatabaseWorkflowFeature` | 特性：登记五个实体的仓储，装配存储、实例锁、引擎与健康项；应用进入服务状态后启动轮询，停止时等进行中的轮询结束并释放领导权 |
| `DatabaseWorkflowOptions` | 轮询周期、批量、租约时长、持有者、健康探针间隔 |
| `Entities.WorkflowDefinitionRecord` | 定义行：`Name`、`Version`、`Status`、`TenantId`、`CreatedAt`、`PublishedAt`、`Document` |
| `Entities.WorkflowInstanceRecord` | 实例行：定义引用、`Status`、`CorrelationId`、`StarterId`、`ParentInstanceId`、`TenantId`、时间列、`Sequence`、`Document` |
| `Entities.WorkflowNodeInstanceRecord` | 节点实例行：`InstanceId`、`StartedAt`、`Sequence`、`Document` |
| `Entities.WorkflowBookmarkRecord` | 书签行：`InstanceId`、`NodeInstanceId`、`Kind`、`Key`、`CorrelationId`、`DueAt`、`TenantId`、`Sequence`、`Document` |
| `Entities.WorkflowLockRecord` | 租约行：`Id` 是实例 ID 或资源名，`Holder`、`ExpiresAt`、并发戳 |
| `Storage.DatabaseWorkflowDefinitionStore` / `DatabaseWorkflowInstanceStore` / `DatabaseWorkflowBookmarkStore` | 三个存储实现 |
| `Locking.DatabaseWorkflowInstanceLock` | 实例锁实现 |
| `Polling.PollerLeadership`、`Polling.DueBookmarkPollingLoop` | 轮询领导权（资源 `PollerLeadership.Resource` = `workflow.poller`）与轮询循环 |

## 使用示例

两个节点连同一个库、各自配置不同的 `HolderId` 即组成集群，应用代码与单实例相同。节点 A 上启动的审批实例停在用户任务上，审批人的请求落到节点 B 时，B 拿到实例锁、读到最新状态、完成任务并推进流程；定时与超时由持有 `workflow.poller` 租约的节点推进：A 正常停止时释放租约，B 下一个周期接管；A 宕机时，B 等租约过期（缺省 60 秒）后接管。

## 行为与限制

- 保存是「新增或整体替换」：按 ID 批量取回已有行（每 1000 个一批），有的整行更新、没有的新增，同一批里同一 ID 后者覆盖前者；一次保存一个事务，任一项失败整批回滚。删实例级联删节点实例。
- 实例列表的每个过滤项都是 SQL 条件，按创建时间降序、同刻按写入序号降序分页；`CountAsync` 是 SQL 计数。到期书签按到期时间升序，其余书签与节点实例按时间升序、同刻按写入序号。
- 管道失败（应用不在服务状态、仓储异常、提交失败）→ `WORKFLOW_UNAVAILABLE_001`，原错误在 `Inner`。
- 续约失败只记日志。锁与租约的每一步都是独立事务，别的节点立刻看得见。
- 在一个持有写锁的事务里调用引擎时，SQLite 这类库级写锁的数据库会等到忙等超时；行锁数据库不受影响。
- 表结构由应用管理，`InitTables` 只用于开发与测试。`Document` 列用长文本类型（`text` / `nvarchar(max)` / `longtext`）。建议索引：`WorkflowDefinitionRecord(Name, Version)`、`WorkflowInstanceRecord(DefinitionName, Status, CreatedAt)`、`WorkflowNodeInstanceRecord(InstanceId)`、`WorkflowBookmarkRecord(InstanceId)`、`WorkflowBookmarkRecord(Kind, Key)`、`WorkflowBookmarkRecord(DueAt)`。
- 健康项 `workflow.database.poller`：启动时同步读一次 `workflow.poller` 的租约行，之后按 `HealthProbeIntervalSeconds` 在后台重探（独立作用域、只读工作单元、读后回滚、不经管道）；租约表不可达时条目为 Degraded（`KERNEL_UNAVAILABLE_005`），应用仍在服务。
- 对 SQLite（`Persistence.SqlSugar`）通过 `Workflow.Conformance` 的存储与引擎全部用例。

## 相关

- [工作流](../guide/workflow)
- [Workflow.Abstractions](./workflow-abstractions)
- [Workflow.InMemory](./workflow-inmemory)
- [Scheduling.Database](./scheduling-database)
- [Persistence.SqlSugar](./persistence-sqlsugar)
- 规范：RFC-0021《工作流：持久化执行》；ADR-0020
