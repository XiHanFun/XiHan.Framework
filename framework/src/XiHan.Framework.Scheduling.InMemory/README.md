# XiHan.Framework.Scheduling.InMemory

## 关于

`XiHan.Framework.Scheduling.InMemory` 是调度的默认实现：作业、触发器、运行历史与领导租约都放在进程内存里，没有外部依赖。

## 主要特性

* 零外部基础设施，适合单实例部署、开发与测试
* 运行历史有界，条数上限可配置
* 进程重启后运行时登记的作业与运行历史会丢失；多实例或需要持久化时换 `Scheduling.Database`

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Scheduling.InMemory --prerelease
```

### 使用

登记作业，再启用特性：

```csharp
builder.Services.AddJob<HeartbeatJob>("heartbeat", JobSchedule.Every(TimeSpan.FromSeconds(10)));
builder.AddXiHan(xihan => xihan.UseFeature<InMemorySchedulingFeature>());
```

运行历史条数上限（缺省 1000）：

```json
{ "XiHan": { "Scheduling": { "InMemory": { "MaxRuns": 1000 } } } }
```

## 主要类型

此库提供的主要类型：

* `InMemorySchedulingFeature`：启用内存调度的特性
* `InMemoryJobStore`：进程内的作业存储
* `InMemoryLeaderElection`：进程内的领导选举

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/scheduling-inmemory)

## 相关包

* 调度抽象与引擎：[XiHan.Framework.Scheduling.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Scheduling.Abstractions)
* 数据库实现：[XiHan.Framework.Scheduling.Database](https://www.nuget.org/packages/XiHan.Framework.Scheduling.Database)
* Kubernetes 领导选举：[XiHan.Framework.Scheduling.Kubernetes](https://www.nuget.org/packages/XiHan.Framework.Scheduling.Kubernetes)

## 反馈与贡献

`XiHan.Framework.Scheduling.InMemory` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
