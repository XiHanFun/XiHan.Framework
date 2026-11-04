# XiHan.Framework.Persistence.Abstractions

## 关于

`XiHan.Framework.Persistence.Abstractions` 定义持久化的契约：仓储 `IRepository<TEntity, TKey>`、查询 `EntityQuery<T>`、工作单元 `IUnitOfWork`、实体登记与标识生成。一般由 Provider 包带入，业务代码只依赖本包，换 Provider 不改代码；写自定义 Provider 时直接引用。

## 主要特性

* 仓储是聚合的集合视图，可恢复的失败以 `Result` 返回，批量方法逐项返回结果
* 工作单元由框架在 Transaction 阶段自动开启、提交或回滚，数据库事务延迟到第一次写入才开
* 用 `[EntityIndex]` 在实体类上声明索引，各 Provider 建出同名索引
* 标识生成：GUID v7，可选雪花 `long` 标识

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Persistence.Abstractions --prerelease
```

### 使用

先登记实体并装配一个 Provider（Standard 自带内存实现），再注入仓储读写。仓储只在调用的工作单元里工作。

```csharp
builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x.UseStandardProfile());

public sealed class OrderCatalog(IRepository<Order, Guid> orders)
{
    public Task<IReadOnlyList<Order>> LargestAsync(int minAmount, CancellationToken ct)
        => orders.ListAsync(
            EntityQuery.All<Order>()
                .Where(Specification.Of<Order>(o => o.Amount >= minAmount))
                .OrderBy(static o => o.Amount, descending: true)
                .Page(0, 20),
            ct);
}
```

需要 `long` 标识时显式装配雪花特性；多实例部署要给每个实例配置互不相同的 `XiHan:Persistence:Snowflake:WorkerId`，或装配跨进程的分布式缓存：

```csharp
builder.AddXiHan(x => x.UseStandardProfile().UseFeature<SnowflakeIdFeature>());
```

## 主要类型

此库提供的主要类型：

* `IRepository<TEntity, TKey>`：查找、查询、计数、新增、更新、删除及批量版本
* `EntityQuery<T>`：过滤、排序、分页
* `IUnitOfWork`：事务边界与当前租户
* `EntityIndexAttribute`：声明索引
* `IIdGenerator<TKey>`、`SnowflakeIdFeature`：标识生成与雪花标识

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/persistence-abstractions)

## 相关包

* 内存实现：[XiHan.Framework.Persistence.InMemory](https://www.nuget.org/packages/XiHan.Framework.Persistence.InMemory)
* EF Core 实现：[XiHan.Framework.Persistence.EntityFrameworkCore](https://www.nuget.org/packages/XiHan.Framework.Persistence.EntityFrameworkCore)
* SqlSugar 实现：[XiHan.Framework.Persistence.SqlSugar](https://www.nuget.org/packages/XiHan.Framework.Persistence.SqlSugar)
* 一致性套件：[XiHan.Framework.Persistence.Conformance](https://www.nuget.org/packages/XiHan.Framework.Persistence.Conformance)

## 反馈与贡献

`XiHan.Framework.Persistence.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
