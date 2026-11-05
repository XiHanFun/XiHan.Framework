# XiHan.Framework.Workflow.Conformance

## 关于

`XiHan.Framework.Workflow.Conformance` 是工作流的一致性测试套件，给写工作流存储提供程序的人验证实现用：继承抽象测试类并实现夹具，套件里的全部用例就会针对你的实现运行。普通应用不需要它。

## 主要特性

* 三个套件：存储与实例锁、引擎、用户任务与外部任务
* 覆盖分区隔离、事务、排序与分页、故障本地化等行为
* 不加入调用方事务的提供程序（如内存实现）可覆写 `JoinsCallerTransaction`，用例照实断言

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Workflow.Conformance --prerelease
```

### 使用

在提供程序的测试项目里继承基类，例如验证引擎行为：

```csharp
public sealed class MyWorkflowEngineConformanceTests : WorkflowEngineConformance
{
    protected override async Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var host = await TestHost.StartAsync(static b => b.UseFeature<MyWorkflowFeature>(), configuration, configure);
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), host);
    }
}
```

## 主要类型

此库提供的主要类型：

* `WorkflowStoreConformance`：存储与实例锁，实现 `StartAsync`，返回 `WorkflowStoreHost`
* `WorkflowEngineConformance`：引擎，返回 `WorkflowEngineHost`
* `WorkflowTaskConformance`：用户任务与外部任务

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 工作流抽象与引擎：[XiHan.Framework.Workflow.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Workflow.Abstractions)
* 测试基座：[XiHan.Framework.Testing](https://www.nuget.org/packages/XiHan.Framework.Testing)

## 反馈与贡献

`XiHan.Framework.Workflow.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
