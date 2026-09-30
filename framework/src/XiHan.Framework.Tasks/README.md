# XiHan.Framework.Tasks

## 概述
XiHan.Framework.Tasks 提供任务调度与后台服务的基础能力，包括定时任务、任务锁与执行管道。

## 核心能力
- 定时任务与后台服务的统一注册
- 任务锁与执行管道的基础实现
- 与日志、配置等基础设施协同

## 依赖关系
- 通过 `XiHanTasksModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 任务调度配置通过 Options 类型承载
- 建议在启动模块统一配置调度器与执行策略
- 定时任务历史清理默认关闭；设 `XiHan:Tasks:ScheduledJobs:HistoryCleanupEnabled = true` 后，`JobHistoryCleanupService` 每 `HistoryCleanupIntervalMinutes`（默认 60）分钟清理一轮：删除早于 `HistoryRetentionDays` 天的执行历史与已终结实例，每批 `HistoryCleanupBatchSize`（默认 500）条、每轮最多 `HistoryCleanupMaxBatchesPerRun`（默认 10）批，等待中与运行中的实例不删除
- 上述三个清理数值必须大于 0、`HistoryRetentionDays` 不能小于 0，启动时校验，不合法直接启动失败

## 使用方式
```csharp
[DependsOn(typeof(XiHanTasksModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义任务调度器与执行策略
- 自定义任务锁实现与分布式协调
- 自定义 `IJobStore` 可实现 `CleanupHistoryAsync(DateTimeOffset cutoff, int batchSize, CancellationToken)` 支持分批清理；未实现时回退到 `CleanupHistoryAsync(int retentionDays)` 一次清完

## 目录结构
```text
XiHan.Framework.Tasks/
  README.md
  XiHanTasksModule.cs
```
