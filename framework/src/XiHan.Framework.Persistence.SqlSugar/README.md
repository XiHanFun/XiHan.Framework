# XiHan.Framework.Persistence.SqlSugar

## 关于

`XiHan.Framework.Persistence.SqlSugar` 关系型持久化的 SqlSugar Provider，面向已有 SqlSugar 代码的应用：实体不需要继承 SqlSugar 基类或标注 `[SugarColumn]`，框架按协议约定配置映射，应用仍可使用自己的 `ISqlSugarClient`。

## 主要特性

* 一次调用可写多个库，租户分库与 `[Tenant]` 指定落点
* 按时间分表、差异日志、读写分离
* 运行时数据源与库结构读取、实体目录
* `string.Contains` 等按字面匹配，规约里的可空比较与取反照常翻译

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Persistence.SqlSugar --prerelease
```

### 使用

登记实体后装配特性，在回调里设置数据库类型与连接串。

```csharp
builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x.UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    })));
```

连接串写在 `XiHan:Bindings:db`；`XiHan:Persistence:SqlSugar:InitTables` 设为 `true` 可在开发与测试时启动建表。应用自己注册 `ISqlSugarClient` 时，对每个连接配置先调用 `SqlSugarConventions.Apply`，再用无参的 `new SqlSugarPersistenceFeature()`。

## 主要类型

此库提供的主要类型：

* `SqlSugarPersistenceFeature`：特性入口，`DiffLog` 选出开启差异日志的实体
* `SqlSugarPersistenceOptions`：建表、健康探测、命名连接、命令超时、读写分离选项
* `SqlSugarConventions`：`Apply`、`InitTables` 等约定入口
* `IEntityConnectionSelector`：决定实体落在哪个连接
* `SqlSugarUnitOfWork`、`SqlSugarRepository<TEntity, TKey>`：工作单元与仓储实现

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/persistence-sqlsugar)

## 相关包

* 持久化抽象：[XiHan.Framework.Persistence.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Persistence.Abstractions)
* EF Core 实现：[XiHan.Framework.Persistence.EntityFrameworkCore](https://www.nuget.org/packages/XiHan.Framework.Persistence.EntityFrameworkCore)
* 一致性套件：[XiHan.Framework.Persistence.Conformance](https://www.nuget.org/packages/XiHan.Framework.Persistence.Conformance)

## 反馈与贡献

`XiHan.Framework.Persistence.SqlSugar` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
