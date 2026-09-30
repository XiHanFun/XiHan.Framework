# XiHan.Framework.Tasks.SqlSugar

> 后台作业与定时任务的 SqlSugar 持久化提供程序：作业入队与业务同事务、多实例领取互斥，定时任务实例与执行历史落库。替换 [Tasks](./tasks) 的进程内存储后，作业与执行记录才能跨进程重启与多实例共享。

- **NuGet**：`XiHan.Framework.Tasks.SqlSugar`
- **模块类**：`XiHanTasksSqlSugarModule`
- **所在层**：基础设施层
- **关键依赖**：[Tasks](./tasks)（存储契约、轮询 Worker、调度器）、[Data](./data)（SqlSugar 客户端、工作单元连接登记、建表）

## 概述

[Tasks](./tasks) 的后台作业与定时任务各有一个存储契约：`IBackgroundJobStore` 与 `IJobStore`。两者的默认实现都是进程内字典——进程重启即丢，多实例之间也不共享。主包另带一个基于 Redis 的后台作业存储，但定时任务没有持久化选项。

本包把两者都落到数据表：

- **后台作业**：入队与业务同事务；N 个实例同时轮询，同一个作业只会被一个实例领走；执行中逐作业续租，失去租约的实例不会覆盖新持有者的结果；支持重试与取消管理
- **定时任务**：每次执行的实例与历史落库，可按任务名分页查询；执行途中崩溃遗留的「运行中」记录在超时后自动失效，不会让不允许并发的任务永久停摆

## 何时使用

- 后台作业不能因进程重启而丢失，或应用以多实例部署
- 没有 Redis，或不希望后台作业的可靠性依赖 Redis
- 需要在管理后台查询定时任务的执行历史
- 已在用 [Data](./data)，希望作业与业务数据走同一套连接与事务

## 安装与启用

```bash
dotnet add package XiHan.Framework.Tasks.SqlSugar
```

在启动模块上声明依赖：

```csharp
[DependsOn(typeof(XiHanTasksSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

建表需要开启 [Data](./data) 的库初始化与建表初始化，**两者默认都是关闭的**：

```json
{
  "XiHan": {
    "Data": {
      "SqlSugarCore": {
        "EnableDbInitialization": true,
        "EnableTableInitialization": true
      }
    }
  }
}
```

未开启又没有手工建表时，首次入队或首次执行定时任务即抛「表不存在」。

## 表结构

三张表都**不分表**，都写在默认布局的主库。

### `sys_background_job`

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Basic_Id` | `Guid`，主键 | 作业唯一标识 |
| `Row_Version` | `long` | 并发标识 |
| `Application_Name` | `string(128)`，非空 | 入队应用名，未指定时为空字符串 |
| `Tenant_Id` | `long`，可空 | 入队时的租户 |
| `Job_Name` | `string(256)`，非空 | 作业名称 |
| `Job_Args` | 大文本，非空 | 序列化后的作业参数 |
| `Try_Count` | `short` | 已尝试次数 |
| `Creation_Time` | `DateTime` | 创建时间 |
| `Next_Try_Time` | `DateTime` | 下次可执行时间 |
| `Last_Try_Time` | `DateTime`，可空 | 上次尝试时间 |
| `Is_Abandoned` | `bool` | 是否已放弃 |
| `Priority` | `int` | 优先级，值越大越优先 |
| `Claim_Token` | `string(64)`，可空 | 领取令牌 |
| `Claim_Time` | `DateTime`，可空 | 领取或最近一次续租的时刻，用于租约超时释放 |
| `Is_Cancellation_Requested` | `bool`，可空 | 是否已请求取消，空值与 `false` 均表示未请求 |

后台作业的时间与 `IClock.Now` 同一口径。

`Is_Cancellation_Requested` 是后加的可空列。已有 `sys_background_job` 表的库在 CodeFirst 建表初始化时只补这一列，不改动既有数据，可重复执行；自行维护表结构时手工补一个可空的布尔列。

### `sys_job_instance`

每次定时任务执行一行，主键为 `JobInstance.InstanceId`。主要列：`Job_Name`、`Job_Type_Name`、`Status`、`Trigger_Type`、`Tenant_Id`、`Scheduled_At`、`Started_At`、`Completed_At`、`Duration_Milliseconds`、`Running_Deadline`、`Retry_Count`、`Execution_Node`、`Trace_Id`、`Parameters_Json`、`Error_Message`、`Stack_Trace`。时间列均为协调世界时。

### `sys_job_history`

每次定时任务执行一行，主键为 `JobHistory.HistoryId`。主要列：`Instance_Id`、`Job_Name`、`Status`、`Started_At`、`Completed_At`、`Duration_Milliseconds`、`Tenant_Id`、`Trigger_Type`、`Is_Success`、`Error_Message`、`Stack_Trace`、`Retry_Count`、`Execution_Node`、`Trace_Id`、`Parameters_Json`、`Remarks`。时间列均为协调世界时。

## 工作原理

### 写库位置

所有写库都在宿主上下文里、对默认布局的主库进行。轮询 Worker 与调度器都运行在无租户上下文的后台作用域，只能看到宿主布局；作业若写进租户独立库，就永远不会被执行。

后台作业入队时若存在事务型工作单元，连接会登记进该工作单元，作业与业务数据同事务提交或回滚（业务也在主库时）。

### 后台作业的领取

分三步，全部使用方言无关的表达式 API：

1. 查出可领取作业（应用名相等、未放弃、已到期、未被领取或租约已过期）的主键，按优先级降序、已尝试次数升序、下次执行时间升序取一批
2. 条件 `UPDATE` 为这批主键盖上本次令牌与领取时刻，`WHERE` 里重复可领取条件
3. 按本次令牌取回真正抢到的作业

第 2 步的每行 `UPDATE` 是原子的，两个并发领取者只有一个能让某行在可领取状态下被改写。候选全被抢走时另选一批重试，最多三轮。

领取时每个作业的 `LeaseExpiresAt` 为领取时刻加 `BackgroundJobLeaseTimeout`。进程在执行途中退出时，租约在超时后过期、作业重新可领取。

本包不依赖 Worker 的分布式锁：未配置 Redis 时该锁只在进程内互斥。

### 后台作业的租约

本存储声明 `SupportsJobLease`，Worker 因此对每个作业走租约路径：执行前按令牌续租确认租约，执行中按续租间隔续租，结束后按令牌删除或回写。租约时长由 `BackgroundJobLeaseTimeout` 决定；续租间隔取主包的 `XiHan:BackgroundJobs:JobLeaseRenewalIntervalSeconds`（默认取租约时长的四分之一），主包的 `JobLeaseDurationSeconds` 只作用于进程内存储。

每个操作都是一条带条件的 `UPDATE` 或 `DELETE`，时间比较在 SQL 条件里完成：

| 操作 | 条件 | 效果 |
| --- | --- | --- |
| 续租 | 主键与 `Claim_Token` 匹配，`Claim_Time` 不早于「当前时间减租约时长」 | `Claim_Time` 推进到当前时间，返回的租约携带最新的取消请求标记 |
| 按令牌完成 | 主键与 `Claim_Token` 匹配 | 删除作业；不看租约是否到期，令牌被新的领取换掉即未命中 |
| 按令牌回写 | 主键与 `Claim_Token` 匹配 | 写入尝试次数、下次执行时间、上次尝试时间、放弃与取消标记，清空令牌与领取时刻 |
| 释放租约 | 主键与 `Claim_Token` 匹配 | 清空令牌与领取时刻，作业立即可被再次领取 |

续租未命中（租约已过期或已被另一实例领走）时，Worker 取消本地执行且不回写结果。

按令牌回写不会覆盖已登记的取消请求：取消标记取存储值与回写值的并集，并集为真时作业一律标记放弃。放弃的作业保留在表中。

同一轮领到的作业领取时刻相同，尚未执行到的作业不续租。一轮耗时超过租约时长时，这些作业在执行前的确认续租会失败而被跳过，等下一轮或由另一实例领取，不会被本实例重复执行。Worker 因停机或锁续期失败提前结束一轮时，按令牌释放已领取但未执行的作业。

### 后台作业的管理

本存储声明 `SupportsJobManagement`，主包的 `IBackgroundJobManagementService` 经授权后调用下列操作，重复调用是幂等的：

| 操作 | 结果 |
| --- | --- |
| 重试 | 已放弃的作业：清除放弃与取消标记、尝试次数归零、下次执行时间设为当前时间、结束租约，返回 `Rescheduled`；未放弃返回 `NoChange`；不存在返回 `NotFound` |
| 取消 | 持有有效租约且未登记请求：登记取消请求，返回 `CancellationRequested`，由持有租约的 Worker 在续租时得知并协作停止；其余未放弃的作业：标记放弃并结束租约，返回 `Cancelled`；已放弃或已登记请求返回 `NoChange`；不存在返回 `NotFound` |

取消是协作式的，不强制终止正在执行的代码。被取消的作业保留在表中，可以再重试。

### 定时任务的运行中实例

不允许并发的任务在触发前会查询「是否有运行中实例」。执行途中进程退出会留下一条永远是「运行中」的记录。本包为运行中实例记录截止时刻（开始时间 + 任务超时 + 宽限期），查询只认截止时刻未到的实例。任务超时小于等于 0（不限时）时截止时刻为 `9999-12-31`，实例在被显式结束之前一直算运行中。

### 定时任务的分批清理

主包的 [`JobHistoryCleanupService`](./tasks) 启用后（`XiHan:Tasks:ScheduledJobs:HistoryCleanupEnabled = true`），按 `HistoryRetentionDays` 算出截止时间，逐批调用本存储的 `CleanupHistoryAsync(cutoff, batchSize, ct)`。每一批：

1. 查出开始时间早于截止时间的执行历史，最多 `batchSize` 条主键，按主键删除
2. 查出待清理的任务实例，最多 `batchSize` 条主键，按主键删除：成功、失败或已取消且完成时间早于截止时间的实例，以及运行截止时刻早于截止时间的遗留运行中实例
3. 返回两类合计删除数

等待中实例与运行截止时刻未到截止时间的运行中实例不删除。时间比较在 SQL 内完成；执行历史的 `Started_At`、任务实例的 `Completed_At` 上有索引，遗留运行中实例的查询不走索引。取消令牌在每类操作前检查。

## 配置

配置节 `XiHan:Tasks:SqlSugar`（`XiHanTasksSqlSugarOptions.SectionName`）。

| 配置项 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `BackgroundJobLeaseTimeout` | `TimeSpan` | `00:05:00` | 后台作业租约时长，必须大于零，否则启动校验失败；领取与每次续租都把租约延长到当前时间加该时长 |
| `RunningInstanceGracePeriod` | `TimeSpan` | `00:01:00` | 运行中任务实例的宽限期 |
| `MaxClaimBatchSize` | `int` | `50` | 单次领取的作业数量上限，必须大于零，否则启动校验失败 |

实际领取数量取调用方请求数量与 `MaxClaimBatchSize` 的较小者。一轮领到的作业领取时刻相同，Worker 又串行执行这一轮的全部作业，该上限限制了一轮里排在后面、租约可能在执行前过期的作业数。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanTasksSqlSugarModule` | 模块类，声明依赖即启用 |
| `SqlSugarBackgroundJobStore` | `IBackgroundJobStore` 的 SqlSugar 实现，单例 |
| `SqlSugarJobStore` | `IJobStore` 的 SqlSugar 实现，单例 |
| `TasksHostClientAccessor` | 在宿主上下文中取默认布局主库客户端的访问器 |
| `SysBackgroundJob` / `SysJobInstance` / `SysJobHistory` | 三个实体 |
| `BackgroundJobMapper` / `JobStoreMapper` | 契约与实体的双向映射 |
| `XiHanTasksSqlSugarOptions` | 租约、宽限期与领取批量上限配置 |

## 注意事项与最佳实践

- **后台作业的执行语义是至少一次**。作业处理器必须幂等。
- **执行中的作业靠续租保持租约**。续租间隔要明显小于租约时长；续租失败的作业会被取消本地执行且不回写。一轮里尚未执行到的作业不续租，租约过期后会被跳过、留到下一轮，调小 `XiHan:BackgroundJobs:MaxJobFetchCount` 可减少这种情况。
- **提前结束的一轮会释放租约**。Worker 因停机或锁续期失败提前结束一轮时，按令牌释放已领取但未执行的作业。
- **取消是协作式的**。执行中的作业只登记取消请求，作业处理器要响应取消令牌才会停止。
- **SQL Server 未开启 RCSI 时，领取会被未提交的入队事务阻塞**。默认的已提交读隔离下，未提交的入队事务持有的行锁会让领取的查询等待；在库上开启 `READ_COMMITTED_SNAPSHOT`（RCSI）可避免。
- **`GetWaitingJobsAsync` 是领取不是查询**。调用后作业已被盖上令牌，不要在别处当作只读查询复用，也不要在事务型工作单元里调用它。
- **执行记录默认只增不减**。主包的历史清理服务默认关闭；启用后按批清理，未启用时需应用定期调用 `IJobStore.CleanupHistoryAsync`。两种方式都会清掉运行截止时刻早于截止时间的遗留运行中实例。放弃的后台作业（含被取消的）保留在表中，需要应用自行清理。
- **运行中实例对所有节点可见**。多节点共用一个库时，不允许并发的任务在节点之间也互斥。
- **不限时的任务要留意遗留实例**。任务超时小于等于 0 时，运行中实例在被显式结束之前一直算运行中；这类任务若不允许并发、又在执行途中崩溃，会一直被跳过。用 `IJobStore.UpdateJobStatusAsync(实例标识, JobStatus.Failed)` 清除，遗留实例的 `Running_Deadline` 为 `9999-12-31`。
- **跨库写入不是一个事务**。业务数据在模块库或租户独立库时，作业的入队与业务各自提交。
- **两个存储的生命周期仍是单例**，与主包一致。之后调用 `UseRedisBackgroundJobStore()` 或 `XiHanJobBuilder.UseStore<T>()` 会覆盖本包。

## 扩展点 / 自定义

需要完全自定义存储行为时，实现 `IBackgroundJobStore` 或 `IJobStore` 并在 DI 中 `Replace`。

## 依赖模块

- [Tasks](./tasks)：存储契约、轮询 Worker、调度器与执行器
- [Data](./data)：SqlSugar 客户端解析、工作单元连接登记、建表初始化

## 相关模块

- [MultiTenancy](./multitenancy)：写库期间切换到的宿主上下文
- [Uow](./uow)：后台作业入队所参与的工作单元
- [EventBus](./eventbus)：事件总线与发件箱
- [Auditing](./auditing)：审计日志
