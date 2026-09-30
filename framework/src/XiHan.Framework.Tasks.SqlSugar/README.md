# XiHan.Framework.Tasks.SqlSugar

## 概述

`XiHan.Framework.Tasks` 的 SqlSugar 持久化提供程序。主包的 `DefaultBackgroundJobStore` 与 `DefaultJobStore` 都是进程内实现，进程重启即丢、不跨实例；本包把后台作业、定时任务实例与执行历史落到数据库。

## 核心能力

- 后台作业实体 `sys_background_job`：入队参与当前工作单元的事务，多实例领取互斥（条件抢占 + 租约超时释放），不依赖分布式锁，也不依赖任何数据库方言特性
- 后台作业逐作业租约：执行中续租、按令牌完成与回写、释放租约，失去租约的 Worker 不会覆盖新持有者的结果
- 后台作业管理：重试已放弃的作业、请求取消作业，供主包的 `IBackgroundJobManagementService` 使用
- 定时任务实例 `sys_job_instance` 与执行历史 `sys_job_history`：执行记录落库，按任务名分页查询历史
- 运行中实例带截止时刻：执行途中崩溃遗留的「运行中」记录在超时后不再阻塞不允许并发的任务
- 以 `Replace` 顶替主包的 `IBackgroundJobStore` 与 `IJobStore`，生命周期保持单例

## 依赖关系

依赖 `XiHan.Framework.Tasks`（存储契约、轮询 Worker、调度器与执行器）与 `XiHan.Framework.Data`（SqlSugar 数据访问）。

## 配置与约定

表名 `sys_` 前缀、全小写下划线，不分表；列名 Pascal_Snake_Case。后台作业主键为作业自身的 `Guid`，任务实例与执行历史主键为契约自带的字符串标识。定时任务两张表的时间列均以协调世界时存储。

表结构由 `DbInitializer` 在应用启动时创建，这要求 `XiHan:Data:SqlSugarCore` 下的 `EnableDbInitialization` 与 `EnableTableInitialization` 均为 `true`（二者默认均为 `false`）。都没开启又没有手工建表时，首次写入即抛「表不存在」。自行维护表结构时按本包实体的列定义建表。

配置节 `XiHan:Tasks:SqlSugar`：

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `BackgroundJobLeaseTimeout` | `00:05:00` | 后台作业租约时长，必须大于零；领取与每次续租都把租约延长到当前时间加该时长，超过该时长未续租、未删除也未回写的作业可被重新领取 |
| `RunningInstanceGracePeriod` | `00:01:00` | 运行中任务实例的宽限期，开始时间加任务超时再加宽限期之后，实例不再视为运行中 |
| `MaxClaimBatchSize` | `50` | 单次领取的作业数量上限，必须大于零；实际领取数量取请求数量与该值的较小者 |

所有记录固定写入默认布局的主库，并在写库期间切换到宿主上下文。业务数据写在模块库或租户独立库时，后台作业的入队与业务不在同一个事务里，且不会报错。

后台作业：

- 执行语义为**至少一次**：进程在作业执行成功与删除之间退出，作业会在租约过期后再次执行，作业处理器需幂等
- 租约：本存储声明 `SupportsJobLease`。Worker 执行每个作业前先按令牌续租确认租约，执行中按主包的续租间隔（`XiHan:BackgroundJobs:JobLeaseRenewalIntervalSeconds`，默认取租约时长的四分之一）续租，结束后按令牌删除或回写；租约时长由 `BackgroundJobLeaseTimeout` 决定，主包的 `JobLeaseDurationSeconds` 只作用于进程内存储
- 续租以 `Claim_Token` 匹配且 `Claim_Time` 未早于「当前时间减租约时长」为条件，把 `Claim_Time` 推进到当前时间。续租未命中（租约已过期或已被另一实例领走）时 Worker 取消本地执行且不回写结果
- 同一轮领到的作业领取时刻相同，尚未执行到的作业不续租。一轮耗时超过租约时长时，这些作业在执行前的确认续租会失败而被跳过，等下一轮或由另一实例领取，不会被本实例重复执行
- Worker 因停机或锁续期失败提前结束一轮时，按令牌释放已领取但未执行的作业，作业立即可被再次领取
- 按令牌完成只看令牌：租约已过期但尚未被另一实例领取时，完成仍然命中
- 按令牌回写不会覆盖已登记的取消请求：取消标记取存储值与回写值的并集，并集为真时作业一律标记放弃
- SQL Server 未开启 RCSI 时，领取会被未提交的入队事务阻塞
- 放弃的作业保留在表里并标记 `Is_Abandoned = 1`，没有自动清理；可按标识查到，也可经管理服务重试
- 管理：本存储声明 `SupportsJobManagement`。重试只作用于已放弃的作业，清除放弃与取消标记、尝试次数归零、下次执行时间设为当前时间并结束租约，重复重试返回 `NoChange`。取消持有有效租约的作业时只登记取消请求（`CancellationRequested`），由持有租约的 Worker 在续租时得知并协作停止；其余未放弃的作业直接标记放弃并结束租约（`Cancelled`）；已放弃或已登记请求的作业返回 `NoChange`，不存在的返回 `NotFound`。取消是协作式的，不强制终止正在执行的代码
- 取消标记列 `Is_Cancellation_Requested` 可空，空值与 `0` 均表示未请求。框架的 `DbInitializer` 不修改已存在的表：新安装无需处理；若 `sys_background_job` 已由本包早期版本建立，需经 `IDbSchemaUpgrader` 或手工补这一列（示例见本节末尾），没有存在性检查的写法要先确认列不存在，以便重复执行。直接调用 SqlSugar `CodeFirst.InitTables` 的宿主会自动补列
- MySQL 连接串不要设 `UseAffectedRows=true`：同一秒内的续租会因列值未变返回 0 行而被判失去租约，保持 MySqlConnector 的默认
- 租约恰在到期那一刻本存储仍可续租（与领取的过期判定一致），进程内存储不可
- 多实例的时钟需要同步：时钟偏差大于续租间隔时，作业可能被另一实例提前重新领取
- 取消在两步条件更新之间作业恰被领取或释放时重新执行，最多三轮
- `UpdateAsync` 只更新已存在的作业，不存在时不插入；`InsertAsync` 遇主键重复时抛数据库异常；应用名为空与空字符串视为同一个应用
- 不要在事务型工作单元里调用领取：条件 `UPDATE` 持有的行锁要到工作单元提交才释放

存量表补 `Is_Cancellation_Requested` 列的示例：

```sql
-- MySQL：先查 information_schema.COLUMNS 确认列不存在再执行
ALTER TABLE sys_background_job ADD COLUMN Is_Cancellation_Requested TINYINT(1) NULL;
-- SQL Server：自带存在性检查
IF COL_LENGTH('sys_background_job', 'Is_Cancellation_Requested') IS NULL ALTER TABLE sys_background_job ADD Is_Cancellation_Requested BIT NULL;
-- PostgreSQL：自带存在性检查（列名按 SqlSugar 默认的自动小写）
ALTER TABLE sys_background_job ADD COLUMN IF NOT EXISTS is_cancellation_requested BOOLEAN NULL;
-- SQLite：先用 PRAGMA table_info(sys_background_job) 确认列不存在再执行
ALTER TABLE sys_background_job ADD COLUMN Is_Cancellation_Requested BIT NULL;
```

定时任务：

- 每次执行新增一行实例与一行历史。主包的 `JobHistoryCleanupService` 默认关闭；设 `XiHan:Tasks:ScheduledJobs:HistoryCleanupEnabled = true` 后，它按 `HistoryRetentionDays` 算出截止时间，调用本存储的 `CleanupHistoryAsync(cutoff, batchSize, ct)` 分批清理。未启用时需应用自行定期调用 `IJobStore.CleanupHistoryAsync`
- 分批清理每批对执行历史与任务实例各查出最多 `batchSize` 条主键再按主键删除，返回两类合计删除数；删除对象为开始时间早于截止时间的执行历史、完成时间早于截止时间的已结束实例，以及运行截止时刻早于截止时间的遗留运行中实例。等待中实例与运行截止时刻未到的运行中实例不删除。时间比较在 SQL 内完成；执行历史的 `Started_At`、任务实例的 `Completed_At` 上有索引，遗留运行中实例的查询不走索引
- 多个节点共用一个库时，某节点的运行中实例会让其他节点跳过不允许并发的任务
- 截止时刻依赖协作式超时：任务代码不响应取消时，可能在截止时刻之后仍在运行，此时调度器会再触发一份
- 超过截止时刻的遗留实例仍标为 `Running`，只是不再阻塞调度
- 任务超时小于等于 0（不限时）时，运行中实例在被显式结束之前一直算运行中。这类任务若不允许并发、又在执行途中崩溃，遗留实例会一直阻塞该任务的后续触发。清除方法：调用 `IJobStore.UpdateJobStatusAsync(实例标识, JobStatus.Failed)`，或直接执行 `UPDATE sys_job_instance SET Status = 3 WHERE Basic_Id = '实例标识'`（`3` 为 `JobStatus.Failed`）；遗留实例可按 `Status = 1` 且 `Running_Deadline` 为 `9999-12-31` 查出
- 经 `UpdateJobStatusAsync` 把实例改成 `Running` 不会写入截止时刻，这样的实例不算运行中；框架内没有这条调用路径
- `UpdateJobStatusAsync` 只写状态与完成时间，错误信息与耗时记录在执行历史里
- 读回的实例只还原任务信息的任务名、任务类型（能解析时）、触发类型与租户；执行参数的值读回为 `JsonElement`，无法序列化的参数存为空

## 使用方式

在应用启动模块上声明依赖 `XiHanTasksSqlSugarModule`。之后若再调用主包的 `UseRedisBackgroundJobStore()` 或 `XiHanJobBuilder.UseStore<T>()`，对应的存储会反过来覆盖本包——后调用者生效。

## 扩展点

需要自定义存储行为时，实现 `IBackgroundJobStore`（`XiHan.Framework.Tasks.BackgroundJobs.Abstractions`）或 `IJobStore`（`XiHan.Framework.Tasks.ScheduledJobs.Abstractions`）并在 DI 中 `Replace`。

## 目录结构

```
Entities/                        作业、任务实例与执行历史实体
Mapping/                         契约与实体的双向映射
Clients/                         宿主上下文的客户端访问器
BackgroundJobs/                  后台作业存储
ScheduledJobs/                   定时任务存储
Options/                         存储配置
Extensions/DependencyInjection/  服务注册扩展
```
