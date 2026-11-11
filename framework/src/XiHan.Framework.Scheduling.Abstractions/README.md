# XiHan.Framework.Scheduling.Abstractions

## 关于

`XiHan.Framework.Scheduling.Abstractions` 定义调度的契约并包含调度引擎：作业、触发器、计划、排除日历、失败重试，以及运行时管理作业的 `IJobScheduler`。它不包含作业存储与领导选举的实现，单独引用不会运行任何作业。

## 主要特性

* 计划支持固定间隔、cron（5 / 6 / 7 段、时区）、一次性与手动触发
* 排除日历、misfire 策略、失败重试与执行时限
* 运行时登记、暂停、恢复、触发、取消作业，并查询运行历史
* 作业存储与领导选举是窄契约，由提供程序实现

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Scheduling.Abstractions --prerelease
```

### 使用

一般由实现包带入，写作业或自定义提供程序时才直接引用；运行作业需要同时装配 `Scheduling.InMemory` 或 `Scheduling.Database`。写一个 `IJob` 并登记计划：

```csharp
builder.Services.AddJob<CleanupJob>(new JobName("cleanup.nightly"), JobSchedule.FromCron("0 3 * * *", "Asia/Shanghai"));

public sealed class CleanupJob : IJob
{
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success());
}
```

## 主要类型

此库提供的主要类型：

* `IJob`：作业，每次运行在新的作用域里执行
* `JobDefinition`、`JobTriggerDefinition`、`JobSchedule`：作业、触发器与计划
* `IJobScheduler`：运行时登记、暂停、恢复、触发与查询运行历史
* `IJobStore`、`ILeaderElection`：写自定义提供程序时实现的契约

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/scheduling-abstractions)

## 相关包

* 内存实现：[XiHan.Framework.Scheduling.InMemory](https://www.nuget.org/packages/XiHan.Framework.Scheduling.InMemory)
* 数据库实现：[XiHan.Framework.Scheduling.Database](https://www.nuget.org/packages/XiHan.Framework.Scheduling.Database)
* 一致性套件：[XiHan.Framework.Scheduling.Conformance](https://www.nuget.org/packages/XiHan.Framework.Scheduling.Conformance)

## 反馈与贡献

`XiHan.Framework.Scheduling.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
