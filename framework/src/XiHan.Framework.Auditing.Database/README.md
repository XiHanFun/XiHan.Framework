# XiHan.Framework.Auditing.Database

## 关于

`XiHan.Framework.Auditing.Database` 把审计记录存进数据库：写入先进有界队列、不阻塞调用，后台批量落库，查询按写入序号分页。装配后顶替默认的内存审计，适合需要持久化、多实例共享的应用。

## 主要特性

* 写入不阻塞调用路径，后台批量落库
* 队列满时丢弃新事件并计数，审计不拖垮业务
* 按租户、主体、时间、trace-id 分页查询
* 具体数据库由持久化提供程序决定

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Auditing.Database --prerelease
```

### 使用

与一个持久化提供程序一起装配（下例用 SqlSugar）：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseAuditingFeature>());
```

```json
{ "XiHan": { "Auditing": { "Database": { "QueueCapacity": 10000, "BatchSize": 100, "FlushIntervalSeconds": 1 } } } }
```

## 主要类型

此库提供的主要类型：

* `DatabaseAuditingFeature`：启用入口
* `DatabaseAuditingOptions`：队列容量、批大小、刷写间隔
* `DatabaseAuditTrail`：接收器与查询，`FlushAsync` 立即刷写
* `AuditRecord`：存储行

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/auditing-database)
* [审计指南](https://framework.docs.xihanfun.com/guide/auditing)

## 相关包

* 审计抽象：[XiHan.Framework.Auditing.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Auditing.Abstractions)
* 内存实现：[XiHan.Framework.Auditing.InMemory](https://www.nuget.org/packages/XiHan.Framework.Auditing.InMemory)
* 持久化抽象：[XiHan.Framework.Persistence.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Persistence.Abstractions)

## 反馈与贡献

`XiHan.Framework.Auditing.Database` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
