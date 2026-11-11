# XiHan.Framework.Persistence.EntityFrameworkCore

## 关于

`XiHan.Framework.Persistence.EntityFrameworkCore` 关系型持久化的 Entity Framework Core Provider：应用持有自己的 `DbContext`，框架提供仓储、工作单元与协议约定（审计字段、并发戳、租户、软删除）。新项目或已经用 EF Core 的应用选它，表结构用 EF Core 迁移管理。

## 主要特性

* 仓储与工作单元基于你自己的 `DbContext`，软删除与租户过滤在仓储查询里显式施加
* `ApplyPersistenceConventions` 一行配好并发令牌、租户与软删除索引、`[EntityIndex]`
* 支持租户分库、命名连接与健康项
* 不支持运行时数据源与库结构读取，需要时用 SqlSugar 实现

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Persistence.EntityFrameworkCore --prerelease
```

### 使用

定义 `DbContext`、登记实体，再装配特性并在回调里配置连接（数据库驱动包如 `Microsoft.EntityFrameworkCore.Sqlite` 或 Npgsql 由应用引用）。

```csharp
builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x.UseStandardProfile()
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(
        static (binding, options) => options.UseNpgsql(binding.ValueOrThrow().Value))));

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();
        modelBuilder.ApplyPersistenceConventions(Database);
    }
}
```

连接串写在 `XiHan:Bindings:db`；`XiHan:Persistence:EntityFrameworkCore:EnsureCreated` 设为 `true` 可在开发与测试时启动建表，生产用迁移。

## 主要类型

此库提供的主要类型：

* `EntityFrameworkCorePersistenceFeature<TContext>`：特性入口，构造参数是配置回调
* `EntityFrameworkCorePersistenceOptions`：`EnsureCreated`、健康探测间隔、命名连接
* `ModelBuilderExtensions.ApplyPersistenceConventions`：在模型上应用协议约定
* `EntityFrameworkCoreUnitOfWork`：工作单元实现；仓储实现不公开，经 `IRepository<TEntity, TKey>` 使用

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/persistence-entityframeworkcore)

## 相关包

* 持久化抽象：[XiHan.Framework.Persistence.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Persistence.Abstractions)
* SqlSugar 实现：[XiHan.Framework.Persistence.SqlSugar](https://www.nuget.org/packages/XiHan.Framework.Persistence.SqlSugar)
* 实体登记生成器：[XiHan.Framework.Persistence.Generators](https://www.nuget.org/packages/XiHan.Framework.Persistence.Generators)

## 反馈与贡献

`XiHan.Framework.Persistence.EntityFrameworkCore` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
