# XiHan.Framework.Workflow.Database

## 关于

`XiHan.Framework.Workflow.Database` 是工作流的数据库提供程序，替换默认的 `Workflow.InMemory`：定义、实例、节点实例与书签存放在应用的关系库里，随调用方的工作单元提交或回滚。

## 主要特性

* 多个节点共用一个库，任一节点都能推进任一实例，实例锁与轮询领导权都存在库里
* 流程实例随业务事务提交或回滚，进程重启后实例与待办继续有效
* 数据按平台与租户分区落表，租户记录随租户分库
* 不引用数据库驱动，数据库由所装配的持久化提供程序决定

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Workflow.Database --prerelease
dotnet add package XiHan.Framework.Persistence.SqlSugar --prerelease
```

### 使用

装配一个持久化提供程序和本特性；连接串来自绑定 `XiHan:Bindings:db`，表结构由应用管理：

```csharp
builder.AddXiHan(xihan => xihan
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseWorkflowFeature>());
```

每个节点的 `HolderId` 需要唯一：

```json
{ "XiHan": { "Workflow": { "Database": { "PollIntervalSeconds": 1, "BatchSize": 100, "HolderId": "node-1" } } } }
```

## 主要类型

此库提供的主要类型：

* `DatabaseWorkflowFeature`：启用数据库工作流的特性
* `DatabaseWorkflowOptions`：轮询周期、批量、租约时长与持有者，对应 `XiHan:Workflow:Database`
* `WorkflowLockRecord` 与四张租户表、四张平台表的实体：需要建表的九个实体

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/workflow-database)

## 相关包

* 工作流抽象与引擎：[XiHan.Framework.Workflow.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Workflow.Abstractions)
* 内存实现：[XiHan.Framework.Workflow.InMemory](https://www.nuget.org/packages/XiHan.Framework.Workflow.InMemory)
* SqlSugar 持久化：[XiHan.Framework.Persistence.SqlSugar](https://www.nuget.org/packages/XiHan.Framework.Persistence.SqlSugar)

## 反馈与贡献

`XiHan.Framework.Workflow.Database` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
