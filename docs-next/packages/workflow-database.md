# XiHan.Framework.Workflow.Database

工作流的数据库 Provider，提供 `workflow.execution@1`。定义、实例、节点实例与书签存放在应用的关系库里，在调用方的工作单元里经 `persistence.relational@1` 的仓储读写，随业务提交或回滚；租户的记录随租户分库。实例锁与到期书签轮询的领导权都是平台库同一张表里的租约行，多个节点共用。

适用于多实例部署，以及实例需要跨进程重启保留的场景。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Workflow.Database` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `workflow.execution@1`（`role: provider`）；依赖 `kernel.hosting@1`、`persistence.relational@1`；可选依赖 `multitenancy.context@1` |
| 特性入口 | `DatabaseWorkflowFeature`（清单 ID `xihan.workflow.database`） |
| 配置节 | `XiHan:Workflow`（引擎选项）、`XiHan:Workflow:Database` |
| 依赖 | `XiHan.Framework.Workflow.Abstractions`、`XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.MultiTenancy.Abstractions`；不引用数据库驱动 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

引擎仍是抽象包的 `WorkflowEngine`，本包实现三个存储、事务与实例锁契约并驱动轮询：

| 组件 | 做法 |
| --- | --- |
| 三个存储（`Storage.DatabaseWorkflowDefinitionStore` 等） | 作用域服务；在所在作用域的工作单元里经仓储读写，不经管道、不自己提交。分区为空读写平台表，否则读写租户表；分区必须等于工作单元的租户 |
| 事务 `DatabaseWorkflowTransaction` | 在调用里且 Transaction 阶段已开启工作单元时可加入；只读工作单元里写入返回 `PERSISTENCE_UNEXPECTED_001` |
| 实例锁 `Locking.DatabaseWorkflowInstanceLock` | 租约式分布式锁：插入行即拿到；过期的行按并发戳接管；未过期就等待（50 毫秒起每次翻倍、封顶 500 毫秒），超过 `LockTimeout` 返回 `WORKFLOW_TIMEOUT_001`；持有期间每三分之一租约续约一次；释放只删自己的行。每一步在根提供者的新作用域里开可写工作单元自己提交，不经管道 |
| 轮询 `Polling.DueBookmarkPollingLoop` + `Polling.PollerLeadership` | 每个周期先拿或续资源 `workflow.poller` 的租约（全局一份），持有时才逐个分区（平台与租户目录里启用的租户）轮询到期书签，到了维护周期按保留期清理；应用不在服务状态（启动中、排空中）的周期不拿也不续，持有就立即释放，另一个在服务的实例下个周期接手 |

存储加入调用方的工作单元，租约行不加入（ADR-0077）：引擎把取得的实例锁持有到调用的服务作用域结束（事务提交或回滚之后）才释放，别的节点拿到锁时读到的一定是提交后的状态；租约行自己提交，别的节点立刻看得见、不随业务回滚。轮询的领导权只为节省资源：多个节点同时轮询也不会重复执行，到期书签的恢复由实例锁串行化。

每张表是过滤与排序用的列加一列整份模型的 JSON 文档（`Document`）。实例、节点实例与书签另有写入序号 `Sequence`，首次写入时分配、替换时保留，作同一时刻排序的次关键字（UUID v7 只到毫秒单调，不能当写入顺序用）。数据按分区落表：租户分区的四张表实现 `IMultiTenant`，持久化按工作单元的租户写入与过滤，租户有独立库时落它的分片连接；平台分区是四张 `Platform*` 表，没有租户列，落平台库。平台与租户互不可见。

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

用 EF Core 的应用在自己的 `DbContext` 里登记九个实体（四张租户表、四张平台表、租约表），并给 `Document` 列用长文本类型（平台表同样）：

```csharp
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Workflow.Database.Entities;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowDefinitionRecord>(static d => d.Property(static x => x.Document).HasColumnType("text"));
        modelBuilder.Entity<WorkflowInstanceRecord>(static i => i.Property(static x => x.Document).HasColumnType("text"));
        modelBuilder.Entity<WorkflowNodeInstanceRecord>(static n => n.Property(static x => x.Document).HasColumnType("text"));
        modelBuilder.Entity<WorkflowBookmarkRecord>(static b => b.Property(static x => x.Document).HasColumnType("text"));
        modelBuilder.Entity<WorkflowLockRecord>();
        // 索引由实体上的 [EntityIndex] 建出
        modelBuilder.ApplyPersistenceConventions(Database);
    }
}
```

到期书签与实例列表按时间过滤和排序，EF Core 的 SQLite Provider 把 `DateTimeOffset` 存成文本时翻译不了：这由框架代办，SQLite 上 `ApplyPersistenceConventions(Database)` 把它存成 UTC ticks 整数（ADR-0091，见 [EF Core 的行为与限制](./persistence-entityframeworkcore#行为与限制)），应用不用自己配；原来照旧文档在 `ConfigureConventions` 里配了 `DateTimeOffsetToBinaryConverter` 的，显式配置保留、库不用动。租约表照下文「SQLite 上的租约表」放到另一个库文件。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Workflow:Database:PollIntervalSeconds` | 正数 | 1 | 到期书签的轮询周期 |
| `XiHan:Workflow:Database:BatchSize` | 整数 ≥ 1 | 100 | 每次轮询最多恢复的书签数 |
| `XiHan:Workflow:Database:LockLeaseSeconds` | 正数 | 60 | 实例锁与轮询领导权的租约时长 |
| `XiHan:Workflow:Database:HolderId` | 字符串 | `机器名:进程 ID:8 位随机十六进制` | 租约持有者标识，每个节点唯一 |
| `XiHan:Workflow:Database:LockConnection` | 字符串 | — | 租约表的命名连接；SQLite 上必须配，见下文「SQLite 上的租约表」 |
| `XiHan:Workflow:Database:HealthProbeIntervalSeconds` | 正数 | 15 | 健康项重探间隔 |

值写错即启动失败：解析不了抛 `ArgumentException`，超出取值范围抛 `ArgumentOutOfRangeException`，消息带出配置键与原值；`HolderId` 与 `LockConnection` 空白视为未配置。引擎选项（`LockTimeoutSeconds`、`MaxNodeExecutionsPerBurst`、`RetentionDays` 等）在 `XiHan:Workflow`，见 [Workflow.Abstractions](./workflow-abstractions#配置)。

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
| `DatabaseWorkflowFeature` | 特性：登记九个实体的仓储，装配存储、事务、实例锁、引擎与健康项；启动时起轮询循环（应用不在服务状态的周期不轮询、不记告警、不持有领导权），停止时等进行中的轮询结束并释放领导权 |
| `DatabaseWorkflowOptions` | 轮询周期、批量、租约时长、持有者、健康探针间隔 |
| `Entities.WorkflowDefinitionRow` 等四个基类 | 两个分区共用的列 |
| `Entities.WorkflowDefinitionRecord` / `PlatformWorkflowDefinitionRecord` | 定义行：`Name`、`Version`、`Status`、`CreatedAt`、`PublishedAt`、`Document`；租户表另有 `TenantId`（`IMultiTenant`） |
| `Entities.WorkflowInstanceRecord` / `PlatformWorkflowInstanceRecord` | 实例行：定义引用、`Status`、`CorrelationId`、`StarterId`、`ParentInstanceId`、时间列、`Sequence`、`Document`；租户表另有 `TenantId` |
| `Entities.WorkflowNodeInstanceRecord` / `PlatformWorkflowNodeInstanceRecord` | 节点实例行：`InstanceId`、`StartedAt`、`Sequence`、`Document`；租户表另有 `TenantId` |
| `Entities.WorkflowBookmarkRecord` / `PlatformWorkflowBookmarkRecord` | 书签行：`InstanceId`、`NodeInstanceId`、`NodeId`、`Kind`、`Key`、`CorrelationId`、`DueAt`、`Sequence`、`Document`，以及所属实例的 `DefinitionName` 与 `InstanceStatus`（待办查询下推用）；租户表另有 `TenantId` |
| `Entities.WorkflowLockRecord` | 租约行：`Id` 是实例 ID 或资源名，`Holder`、`ExpiresAt`、并发戳 |
| `Storage.DatabaseWorkflowDefinitionStore` / `DatabaseWorkflowInstanceStore` / `DatabaseWorkflowBookmarkStore` | 三个存储实现 |
| `DatabaseWorkflowTransaction` | 与调用方事务的关系 |
| `Locking.DatabaseWorkflowInstanceLock` | 实例锁实现 |
| `Polling.PollerLeadership`、`Polling.DueBookmarkPollingLoop` | 轮询领导权（资源 `PollerLeadership.Resource` = `workflow.poller`）与轮询循环；`RunCycleAsync` 执行一个周期（测试推进注入的时钟后直接调用） |

## 使用示例

两个节点连同一个库、各自配置不同的 `HolderId` 即组成集群，应用代码与单实例相同。节点 A 上启动的审批实例停在用户任务上，审批人的请求落到节点 B 时，B 拿到实例锁、读到最新状态、完成任务并推进流程；定时与超时由持有 `workflow.poller` 租约的节点推进：A 正常停止时释放租约，B 下一个周期接管；A 宕机时，B 等租约过期（缺省 60 秒）后接管。

## 行为与限制

- 保存是「新增或整体替换」：按 ID 批量取回已有行（每 1000 个一批），有的整行更新、没有的新增，同一批里同一 ID 后者覆盖前者；保存在调用方的事务里，失败随调用一起回滚。写入的记录必须属于存储方法的分区。删实例级联删节点实例。
- 实例列表的每个过滤项都是 SQL 条件，按创建时间降序、同刻按写入序号降序分页；`CountAsync` 是 SQL 计数。
- 待办查询（`ListUserTasksAsync` / `CountUserTasksAsync`）同样是 SQL：书签行带所属实例的定义名与状态，写书签时从实例行取，实例新增或状态变化时同步到它的书签行（同一事务），只取状态为 `Running` 的。到期书签按到期时间升序，其余书签与节点实例按时间升序、同刻按写入序号。
- 仓储失败 → `WORKFLOW_UNAVAILABLE_001`，原错误在 `Inner`；只读工作单元的拒绝 `PERSISTENCE_UNEXPECTED_001` 原样返回。
- 续约失败只记日志。锁与租约的每一步都是独立事务，别的节点立刻看得见。
- 租约行与业务写入不在同一个事务里：SQLite 上要把租约表放到另一个库文件，见下文「SQLite 上的租约表」；PostgreSQL 等行锁数据库不需要。
- 表结构由应用管理，`InitTables` 只用于开发与测试。`Document` 列用长文本类型（`text` / `nvarchar(max)` / `longtext`）。索引由实体上的 `[EntityIndex]` 声明，SqlSugar 的 `InitTables` 与 EF Core 的 `ApplyPersistenceConventions(Database)` 都按它建（ADR-0072）：`UX_WorkflowDefinitionRecord_Na_TeId_Ve`（`(Name, TenantId, Version)` 唯一，租户列不为空）、`UX_PlatformWorkflowDefinitionRecord_Na_Ve`（`(Name, Version)` 唯一）——每一行都受约束；`IX_WorkflowInstanceRecord_PaInId`（`ParentInstanceId`）、`IX_WorkflowInstanceRecord_CoId`（`CorrelationId`）、`IX_WorkflowInstanceRecord_TeId_EnAt`（`(TenantId, EndedAt)`）、`IX_WorkflowInstanceRecord_TeId_CrAt`（`(TenantId, CreatedAt)`）、`IX_WorkflowNodeInstanceRecord_InId`（`InstanceId`）、`IX_WorkflowBookmarkRecord_InId`（`InstanceId`）、`IX_WorkflowBookmarkRecord_Ki_Ke`（`(Kind, Key)`）、`IX_WorkflowBookmarkRecord_TeId_DuAt`（`(TenantId, DueAt)`）；平台表有同样的索引，时间列不带租户前缀；租约表只按主键读写。有独立库的租户，四张租户表要建在它自己的库里（`InitTables` 只作用于默认连接）。实例列表的其他过滤组合由应用按查询习惯自己加。
- 健康项 `workflow.database.poller`：启动时同步读一次 `workflow.poller` 的租约行，之后按 `HealthProbeIntervalSeconds` 在后台重探（独立作用域、只读工作单元、读后回滚、不经管道）；租约表不可达时条目为 Degraded（`KERNEL_UNAVAILABLE_005`），应用仍在服务。
- 对 SQLite（`Persistence.SqlSugar`）通过 `Workflow.Conformance` 的存储与引擎全部用例；对 SQLite（`Persistence.EntityFrameworkCore`，租约表在命名连接的另一个库文件）通过引擎与任务全部用例。

## SQLite 上的租约表

租约行不随业务事务（ADR-0077）：引擎在调用方事务里写过实例、书签之后，再在自己的工作单元里写租约行。SQLite 只有一个写者（库级写锁），同一个库文件上这个工作单元要等调用方事务结束——等的正是自己，忙等超时后以 `WORKFLOW_TIMEOUT_001` 失败。所以 SQLite 上要把租约表放到另一个库文件（ADR-0084）：

```json
{
  "XiHan": {
    "Bindings": { "db": "Data Source=app.db" },
    "Persistence": {
      "SqlSugar": { "Connections": { "leases": "Data Source=app.leases.db" } }
    },
    "Workflow": { "Database": { "LockConnection": "leases" } }
  }
}
```

- `LockConnection` 指向持久化的命名连接（[SqlSugar 的命名连接](./persistence-sqlsugar#命名连接)、[EF Core 的命名连接](./persistence-entityframeworkcore#行为与限制)，RFC-0005）：特性把 `WorkflowLockRecord` 登记到这个连接（`InitTables` / `EnsureCreated` 把租约表建在那里），租约的每一步与健康探针开绑定这个连接的工作单元，事务只开在租约库上；业务调用不碰租约库，业务事务也就不锁它。
- SqlSugar 方式一在 `XiHan:Persistence:SqlSugar:Connections:<name>` 声明连接，回调按 `config.ConfigId` 区分；方式二由应用把同名 `ConfigId` 的连接加进自己注册的客户端。连接不在客户端里时启动即失败（`PERSISTENCE_VALIDATION_004`）。
- EF Core 在 `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` 声明这个连接（ADR-0087）：租约的每一步与健康探针本来就在自己的作用域里开绑定连接的工作单元，作用域里的 `DbContext` 按租约库的连接串建出；连接没有声明时装配即失败（`PERSISTENCE_VALIDATION_004`）。`DbContext` 里照常登记租约表 `WorkflowLockRecord`。
- 内存持久化不支持命名连接，配了 `LockConnection` 即装配失败（`PERSISTENCE_UNAVAILABLE_008`）。
- 不配置时租约表照旧在平台库（默认连接），行为与以前相同；PostgreSQL、SQL Server、MySQL 等行级锁的数据库不需要配。
- 多个节点共用租约库：租约库的连接串在各节点上要指向同一个库，SQLite 文件只适合单机的开发与集成测试。

## 从上一版升级

表结构与数据要迁移（ADR-0077）：

- 新建四张 `Platform*` 表；`WorkflowNodeInstanceRecord` 新增非空 `TenantId` 列（取所属实例的租户回填）。
- 两张书签表新增可空列 `NodeId`、`DefinitionName`、`InstanceStatus`，回填：`NodeId` 取 `Document` 里的 `nodeId`，后两列取所属实例行的 `DefinitionName` 与 `Status`。不回填的行在待办查询里看不到，直到所属实例下一次改状态。
- `TenantId` 为 NULL 的定义、实例、书签与这些实例的节点实例搬到对应的 `Platform*` 表（同列复制，不带 `TenantId`）。平台定义搬过去之前先查 `(Name, Version)` 重复：旧索引不约束 NULL 行，可能已有重复。
- 有独立库的租户，把它的行从平台库搬到它自己的库。
- 删掉旧的单列索引 `IX_WorkflowInstanceRecord_EnAt`、`IX_WorkflowInstanceRecord_CrAt`、`IX_WorkflowBookmarkRecord_DuAt`，建带租户前缀的新索引（`InitTables` 只建不删）。

## 相关

- [工作流](../guide/workflow)
- [Workflow.Abstractions](./workflow-abstractions)
- [Workflow.InMemory](./workflow-inmemory)
- [Scheduling.Database](./scheduling-database)
- [Persistence.SqlSugar](./persistence-sqlsugar)
- 规范：RFC-0021《工作流：持久化执行》；ADR-0020、ADR-0077
