# XiHan.Framework.Tasks.SqlSugar

## 概述

`XiHan.Framework.Tasks` 的 SqlSugar 持久化提供程序。主包的 `DefaultBackgroundJobStore` 与 `DefaultJobStore` 都是进程内实现，进程重启即丢、不跨实例；本包把后台作业、定时任务实例与执行历史落到数据库。

## 核心能力

- 后台作业实体 `sys_background_job`：入队参与当前工作单元的事务，多实例领取互斥（条件抢占 + 租约超时释放），不依赖分布式锁，也不依赖任何数据库方言特性
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
| `BackgroundJobLeaseTimeout` | `00:05:00` | 后台作业租约时长，必须大于零；领取后超过该时长仍未删除或更新的作业可被重新领取 |
| `RunningInstanceGracePeriod` | `00:01:00` | 运行中任务实例的宽限期，开始时间加任务超时再加宽限期之后，实例不再视为运行中 |
| `MaxClaimBatchSize` | `50` | 单次领取的作业数量上限，必须大于零；实际领取数量取请求数量与该值的较小者，一轮共用同一个租约的作业数因此受限 |

所有记录固定写入默认布局的主库，并在写库期间切换到宿主上下文。业务数据写在模块库或租户独立库时，后台作业的入队与业务不在同一个事务里，且不会报错。

后台作业：

- 执行语义为**至少一次**：进程在作业执行成功与删除之间退出，作业会在租约过期后再次执行，作业处理器需幂等
- Worker 串行执行一轮领到的全部作业。未配置 Redis 时分布式锁只在进程内互斥，若一轮耗时超过租约时长，本轮尚未执行到的作业可能被另一实例领走并重复执行——此时调大 `BackgroundJobLeaseTimeout`，或调小 `XiHan:BackgroundJobs:MaxJobFetchCount`
- Worker 因停机或锁续期失败提前结束一轮时，已领取但未执行的作业要等租约过期才会被再次领取；提前结束的一轮不会释放租约
- SQL Server 未开启 RCSI 时，领取会被未提交的入队事务阻塞
- 放弃的作业保留在表里并标记 `Is_Abandoned = 1`，没有自动清理
- `UpdateAsync` 只更新已存在的作业，不存在时不插入；`InsertAsync` 遇主键重复时抛数据库异常；应用名为空与空字符串视为同一个应用
- 不要在事务型工作单元里调用领取：条件 `UPDATE` 持有的行锁要到工作单元提交才释放

定时任务：

- 每次执行新增一行实例与一行历史。框架不会自动清理，`XiHanJobOptions.HistoryRetentionDays` 也未被读取——应用需自行定期调用 `IJobStore.CleanupHistoryAsync`，它同时删除过期的执行历史、已结束的实例与运行截止时刻早于保留期的遗留运行中实例
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
