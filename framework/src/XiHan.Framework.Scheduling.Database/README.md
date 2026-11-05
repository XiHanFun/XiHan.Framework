# XiHan.Framework.Scheduling.Database

## 关于

`XiHan.Framework.Scheduling.Database` 是调度的数据库提供程序，替换默认的 `Scheduling.InMemory`：作业、触发器、运行历史、节点与领导租约存放在应用的关系库里。

## 主要特性

* 多个节点共用一个库，每条到期触发器只由先抢到的节点运行
* 运行时登记的作业与运行历史跨重启保留
* 节点宕机后由领导者自动恢复其运行中的作业
* 不引用数据库驱动，数据库由所装配的持久化提供程序决定

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Scheduling.Database --prerelease
dotnet add package XiHan.Framework.Persistence.SqlSugar --prerelease
```

### 使用

装配一个持久化提供程序和本特性，连接串来自绑定 `XiHan:Bindings:db`：

```csharp
builder.AddXiHan(xihan => xihan
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseSchedulingFeature>());
```

每个节点的 `XiHan:Scheduling:NodeId` 需要唯一；表结构由应用管理：

```json
{ "XiHan": { "Bindings": { "db": "Host=localhost;Database=app;Username=app" }, "Scheduling": { "NodeId": "node-1" } } }
```

## 主要类型

此库提供的主要类型：

* `DatabaseSchedulingFeature`：启用数据库调度的特性
* `DatabaseJobStore`、`DatabaseLeaderElection`：基于数据库的作业存储与领导选举
* `JobEntity`、`TriggerEntity`、`JobRunEntity`、`SchedulerNodeEntity`、`SchedulerLeaseEntity`：需要建表的五个实体

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/scheduling-database)

## 相关包

* 调度抽象与引擎：[XiHan.Framework.Scheduling.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Scheduling.Abstractions)
* 内存实现：[XiHan.Framework.Scheduling.InMemory](https://www.nuget.org/packages/XiHan.Framework.Scheduling.InMemory)
* SqlSugar 持久化：[XiHan.Framework.Persistence.SqlSugar](https://www.nuget.org/packages/XiHan.Framework.Persistence.SqlSugar)

## 反馈与贡献

`XiHan.Framework.Scheduling.Database` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
