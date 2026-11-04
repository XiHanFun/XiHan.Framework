# XiHan.Framework.Persistence.Generators

## 关于

`XiHan.Framework.Persistence.Generators` 持久化的 Roslyn 源生成器：从实现 `IEntity<TKey>` 的实体类生成仓储登记、EF Core 实体登记与软删除、租户过滤规约，省去逐个手写 `AddRepository`。以分析器形式引用，不进入运行时依赖。

## 主要特性

* `AddGeneratedRepositories`：为每个实体登记仓储
* `ApplyGeneratedEntities`：为每个实体配置 EF Core 模型（项目引用了 EF Core 时生成）
* `<实体>Filters`：软删除、多租户实体的 `NotDeleted`、`ForTenant`、`ActiveFor` 规约

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Persistence.Generators --prerelease
```

### 使用

引用本包后，实体类实现 `IEntity<TKey>`（或 `IAggregateRoot<TKey>`）即会触发生成。

```csharp
public sealed class Order : IAggregateRoot<Guid>, IMultiTenant, ISoftDeletable { … }

// 能力包的 Configure 里登记全部实体的仓储
context.Services.AddGeneratedRepositories();

// EF Core 应用的 OnModelCreating
modelBuilder.ApplyGeneratedEntities(Database);

var active = await repository.ListAsync(EntityQuery.All<Order>().Where(OrderFilters.ActiveFor(tenantId)), ct);
```

## 主要类型

此库提供的主要类型：

* `GeneratedEntities`：生成的静态类，含 `AddGeneratedRepositories`、`ApplyGeneratedEntities`
* `<实体>Filters`：生成的过滤规约，可传给 `EntityQuery<T>.Where`
* `XHP001` 到 `XHP004`：实体声明不合法时的编译诊断

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/persistence-generators)

## 相关包

* 持久化抽象：[XiHan.Framework.Persistence.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Persistence.Abstractions)
* EF Core 实现：[XiHan.Framework.Persistence.EntityFrameworkCore](https://www.nuget.org/packages/XiHan.Framework.Persistence.EntityFrameworkCore)

## 反馈与贡献

`XiHan.Framework.Persistence.Generators` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
