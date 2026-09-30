# XiHan.Framework.Tasks

## 概述
XiHan.Framework.Tasks 提供任务调度与后台服务的基础能力，包括定时任务、任务锁与执行管道。

## 核心能力
- 定时任务与后台服务的统一注册
- 任务锁与执行管道的基础实现
- 与日志、配置等基础设施协同
- 后台作业租约：逐作业续租，失去租约即取消处理器令牌并放弃回写（存储支持时启用）
- 后台作业管理服务：重试已放弃作业、请求取消作业，授权默认拒绝，审计默认写日志

## 依赖关系
- 通过 `XiHanTasksModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 任务调度配置通过 Options 类型承载
- 建议在启动模块统一配置调度器与执行策略
- 定时任务历史清理默认关闭；设 `XiHan:Tasks:ScheduledJobs:HistoryCleanupEnabled = true` 后，`JobHistoryCleanupService` 每 `HistoryCleanupIntervalMinutes`（默认 60）分钟清理一轮：删除早于 `HistoryRetentionDays` 天的执行历史与已终结实例，每批 `HistoryCleanupBatchSize`（默认 500）条、每轮最多 `HistoryCleanupMaxBatchesPerRun`（默认 10）批，等待中与运行中的实例不删除
- 启用清理时启动校验：`HistoryCleanupIntervalMinutes` 须在 1 到 71582 之间，其余两个清理数值必须大于 0，`HistoryRetentionDays` 不能小于 0，不合法直接启动失败；未启用时不校验
- 后台作业配置节为 `XiHan:BackgroundJobs`（`BackgroundJobWorkerOptions`），与租约相关的两项：
  - `JobLeaseDurationSeconds`（默认 300，须大于 0）：仅作用于进程内默认存储，其它存储以各自配置为准
  - `JobLeaseRenewalIntervalSeconds`（默认 0）：0 表示取租约时长的 1/4；配置值不小于租约时长的 1/3 时同样取 1/4

作业租约支持矩阵（由 `IBackgroundJobStore.SupportsJobLease` 决定 Worker 走租约路径还是旧路径）：

| 存储 | 租约 | 说明 |
| --- | --- | --- |
| 进程内 `DefaultBackgroundJobStore` | 支持 | 租约时长取 `JobLeaseDurationSeconds` |
| SqlSugar（[`XiHan.Framework.Tasks.SqlSugar`](../XiHan.Framework.Tasks.SqlSugar/README.md)） | 支持 | 租约时长取该包的 `BackgroundJobLeaseTimeout`，同时支持重试与取消管理 |
| Redis（`RedisBackgroundJobStore`） | 不支持 | 维持分布式锁单活，不做续租 |
| 自定义存储 | 默认不支持 | 覆写 `SupportsJobLease` 及 `TryRenewLeaseAsync`、`TryCompleteAsync`、`TryUpdateAsync`、`ReleaseLeaseAsync` 才启用 |

失租语义：
- Worker 按间隔续租；续租未命中（令牌不匹配或租约已到期）即取消传给处理器的令牌，并且不再回写该作业的执行结果
- 取消是协作式的，不保证强制终止处理器；处理器需自行观察取消令牌
- 不承诺业务副作用 exactly-once，处理器应保持幂等
- 分布式锁仍只在作业之间续期；单个作业执行过长时锁可能过期，其它实例可据此领取其它作业，但同一作业受租约保护，不会被重复领取

## 使用方式
```csharp
[DependsOn(typeof(XiHanTasksModule))]
public class MyModule : XiHanModule
{
}
```

处理器可重写带取消令牌的重载，宿主停止、失去租约或管理端请求取消时令牌触发：
```csharp
public class SendMailJob : AsyncBackgroundJob<SendMailArgs>
{
    public override Task ExecuteAsync(SendMailArgs args)
    {
        return ExecuteAsync(args, CancellationToken.None);
    }

    public override async Task ExecuteAsync(SendMailArgs args, CancellationToken cancellationToken)
    {
        await SendAsync(args, cancellationToken);
    }
}
```

两个重载要么都重写（业务写在带令牌的重载里，单参数版本转调它，即上面的写法），要么只实现 `ExecuteAsync(args)` 并把业务写在里面。只让 `ExecuteAsync(args)` 转调带令牌的重载、却不重写后者，会与基类默认实现互相调用，造成无限递归。

管理服务 `IBackgroundJobManagementService`：
- `RetryAsync(jobId)`：把已放弃的作业重新排入待执行
- `RequestCancellationAsync(jobId)`：请求取消作业，协作式，不保证强制终止
- 结果状态 `BackgroundJobManagementStatus`：`Rescheduled`、`Cancelled`、`CancellationRequested`、`NoChange`、`NotFound`、`Denied`、`NotSupported`
- 授权器默认 `DenyAllBackgroundJobManagementAuthorizer` 拒绝全部操作，应用须替换 `IBackgroundJobManagementAuthorizer` 才可用
- 审计器默认以 Information 级别写日志；审计记录不含操作者身份，自定义审计器自行从环境上下文获取
- 进程内存储放弃的作业即被移除，对其重试通常返回 `NotFound`

## 扩展点
- 自定义任务调度器与执行策略
- 自定义任务锁实现与分布式协调
- 自定义 `IJobStore` 可实现 `CleanupHistoryAsync(DateTimeOffset cutoff, int batchSize, CancellationToken)` 支持分批清理；未实现时回退到 `CleanupHistoryAsync(int retentionDays)` 一次清完
- 自定义 `IBackgroundJobStore` 并覆写租约与管理相关的可选方法
- 替换 `IBackgroundJobManagementAuthorizer` 放行管理操作，替换 `IBackgroundJobManagementAuditor` 持久化审计

## 目录结构
```text
XiHan.Framework.Tasks/
  README.md
  XiHanTasksModule.cs
  BackgroundJobs/            后台作业（租约、处理器、存储、管理服务）
```
