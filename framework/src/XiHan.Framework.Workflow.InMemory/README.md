# XiHan.Framework.Workflow.InMemory

## 关于

`XiHan.Framework.Workflow.InMemory` 是工作流的默认实现：有界内存的定义、实例与书签存储，进程内实例锁，单进程轮询到期书签。没有外部依赖。

## 主要特性

* 零外部基础设施，适合单实例部署、开发与测试
* 容量满时写入失败而不是淘汰旧数据；可配置 `XiHan:Workflow:RetentionDays` 自动清理已结束实例
* 没有事务，也不跨进程：进程重启后定义与实例全部丢失，多实例或要求与业务写入原子请换 `Workflow.Database`

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Workflow.InMemory --prerelease
```

### 使用

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryWorkflowFeature>());
```

容量与轮询可调（以下为缺省值）：

```json
{ "XiHan": { "Workflow": { "InMemory": { "MaxInstances": 10000, "PollIntervalSeconds": 1, "BatchSize": 100 } } } }
```

## 主要类型

此库提供的主要类型：

* `InMemoryWorkflowFeature`：启用内存工作流的特性
* `InMemoryWorkflowOptions`：容量、轮询周期与批量，对应 `XiHan:Workflow:InMemory`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/workflow-inmemory)

## 相关包

* 工作流抽象与引擎：[XiHan.Framework.Workflow.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Workflow.Abstractions)
* 数据库实现：[XiHan.Framework.Workflow.Database](https://www.nuget.org/packages/XiHan.Framework.Workflow.Database)

## 反馈与贡献

`XiHan.Framework.Workflow.InMemory` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
