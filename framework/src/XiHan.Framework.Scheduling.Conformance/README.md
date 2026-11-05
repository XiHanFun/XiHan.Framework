# XiHan.Framework.Scheduling.Conformance

## 关于

`XiHan.Framework.Scheduling.Conformance` 是调度的一致性测试套件，给写调度提供程序的人验证实现用：继承抽象测试类、实现一个工厂方法，套件里的全部用例就会针对你的实现运行。普通应用不需要它。

## 主要特性

* 覆盖调度器整体行为、作业存储与领导选举三部分
* 用例用手动推进的时钟驱动，不依赖墙钟，结果与机器快慢无关

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Scheduling.Conformance --prerelease
```

### 使用

在提供程序的测试项目里继承对应基类，例如验证一个调度提供程序：

```csharp
public sealed class MyJobSchedulerConformanceTests : JobSchedulerConformance
{
    protected override async Task<JobSchedulerHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var settings = new List<KeyValuePair<string, string?>> { new("XiHan:Scheduling:PollIntervalSeconds", "3600") };
        settings.AddRange(configuration ?? []);
        var host = await TestHost.StartAsync(static b => b.UseFeature<MySchedulingFeature>(), settings, configure);
        var engine = host.Services.GetRequiredService<JobSchedulerEngine>();
        return new JobSchedulerHost(engine, engine.PollOnceAsync, host);
    }
}
```

## 主要类型

此库提供的主要类型：

* `JobSchedulerConformance`：调度器整体行为，实现 `StartAsync`
* `JobStoreConformance`：作业存储，实现 `CreateAsync`
* `LeaderElectionConformance`：领导选举，实现 `CreateElection` 与 `StartNodeAsync`
* `JobSchedulerHost`：套件使用的宿主、调度器与轮询入口

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 调度抽象与引擎：[XiHan.Framework.Scheduling.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Scheduling.Abstractions)
* 测试基座：[XiHan.Framework.Testing](https://www.nuget.org/packages/XiHan.Framework.Testing)

## 反馈与贡献

`XiHan.Framework.Scheduling.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
