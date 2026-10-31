# XiHan.Framework.Persistence.Generators

持久化的 Roslyn 源生成器：从本程序集实现 `IEntity<TKey>` 的实体类生成仓储登记、EF Core 实体登记与软删除 / 租户过滤规约。以分析器形式引用，不进入运行时依赖。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Persistence.Generators` |
| 环层 | 工具链 |
| 能力 | — |
| 特性入口 | — |
| 配置节 | — |
| 依赖 | `Microsoft.CodeAnalysis.CSharp` 4.14.0（仅编译期）；生成的代码引用 `XiHan.Framework.Domain` 与 `XiHan.Framework.Persistence.Abstractions` |
| AOT | 不兼容（`IsAotCompatible` 为 `false`：生成器在编译期运行，以 `netstandard2.0` 发布）；生成的代码不含反射，可用于 AOT 应用 |
| 稳定性 | Preview |

## 概述

实体多了以后，逐个写 `AddRepository<TEntity, TKey>()` 与 `modelBuilder.Entity<T>()` 容易漏。生成器扫描当前项目里实现 `IEntity<TKey>`（直接实现或经 `IAggregateRoot<TKey>`）的非抽象、非静态的顶层类，生成一个文件 `GeneratedEntities.g.cs`：

| 生成物 | 位置 | 内容 |
| --- | --- | --- |
| `GeneratedEntities.AddGeneratedRepositories(this IServiceCollection)` | 项目 `RootNamespace`（没有时用程序集名） | 为每个实体调用一次 `AddRepository<TEntity, TKey>()` |
| `GeneratedEntities.ApplyGeneratedEntities(this ModelBuilder)` | 同上；项目引用了 EF Core 时才生成 | 为每个实体 `modelBuilder.Entity<T>()`；项目还引用了 `Persistence.EntityFrameworkCore` 时接着调用 `ApplyPersistenceConventions()` |
| `<实体>Filters` 静态类 | 实体所在命名空间，可访问性与实体相同；实体实现 `ISoftDeletable` 或 `IMultiTenant` 时才生成 | `NotDeleted`（属性）、`ForTenant(tenantId)`、两者都实现时还有 `ActiveFor(tenantId)`，都是 `Specification<T>` |

生成的登记代码与手写的 `AddRepository` 等价，仍是逐个闭合泛型的显式注册，Native AOT 可用。

## 何时使用

实体超过几个、或分散在多个功能区时用它代替手写登记。只有一两个实体的项目手写 `AddRepository` 即可，效果相同。

## 安装与启用

以分析器引用，不把生成器程序集带进运行时：

```xml
<PackageReference Include="XiHan.Framework.Persistence.Generators" Version="5.0.0-preview.1" PrivateAssets="all" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。在仓库里以项目引用时写成：

```xml
<ProjectReference Include="../../src/XiHan.Framework.Persistence.Generators/XiHan.Framework.Persistence.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

然后在特性的 `Configure` 里调用生成的登记方法（SimpleApp 的 `OrdersFeature`）：

```csharp
public void Configure(FeatureContext context)
{
    // 登记代码由 Persistence.Generators 从实体类生成（Order : IAggregateRoot<Guid>）
    context.Services.AddGeneratedRepositories();
}
```

EF Core 应用在 `OnModelCreating` 里调用生成的模型登记：

```csharp
using Microsoft.EntityFrameworkCore;

namespace SimpleApp;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // 登记本程序集全部实体，并应用持久化约定
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyGeneratedEntities();
}
```

## 主要类型

本包的公开类型只有生成器本身 `EntityGenerator`，应用不直接使用它。应用使用的是生成出来的代码：

| 生成的成员 | 说明 |
| --- | --- |
| `GeneratedEntities.AddGeneratedRepositories(services)` | 登记本程序集全部实体的仓储，返回 `services` |
| `GeneratedEntities.ApplyGeneratedEntities(modelBuilder)` | 把本程序集全部实体登记进 EF Core 模型，返回 `modelBuilder` |
| `<实体>Filters.NotDeleted` | 未软删除：`!entity.IsDeleted` |
| `<实体>Filters.ForTenant(tenantId)` | 属于指定租户：`entity.TenantId == tenantId` |
| `<实体>Filters.ActiveFor(tenantId)` | `NotDeleted.And(ForTenant(tenantId))` |

## 使用示例

仓储查询已经自动按当前租户与软删除过滤，生成的规约用于内存判定，或在 Provider 原生查询里复用同一条件：

```csharp
using Microsoft.EntityFrameworkCore;
using SimpleApp.Orders;

// 内存里筛一批已经取出的订单
var active = loaded.Where(OrderFilters.ActiveFor("acme").IsSatisfiedBy).ToList();

// 直接查 DbContext 时没有仓储的过滤，用同一条规约补上
var count = await db.Set<Order>().Where(OrderFilters.ActiveFor("acme").Criteria).CountAsync(cancellationToken);
```

`loaded` 是一组 `Order`，`db` 是应用的 `AppDbContext`。

## 行为与限制

- 只扫描当前项目里的 `class` 声明：抽象类、静态类不生成；`record` 声明不被识别，`record` 实体要手写 `AddRepository`。分部类的多个声明只登记一次。
- 实体按完整类型名排序后输出，生成结果稳定。
- 没有实体时不生成任何文件，引用生成器的项目也就没有 `GeneratedEntities`。
- `RootNamespace` 中的非法字符会被整理成合法的命名空间名。

| 诊断 | 级别 | 含义 | 处理 |
| --- | --- | --- | --- |
| `XHP001` | Error | 实体类经多个 `IEntity<TKey>` 声明了不同的键类型 | 只实现一个 `IEntity<TKey>`；该实体不生成 |
| `XHP002` | Error | 实体类是嵌套类型，或不是 `public` / `internal` | 改为顶层的 `public` 或 `internal` 类；该实体不生成 |
| `XHP003` | Warning | 要生成的 `<实体>Filters` 与已有类型同名 | 该实体的过滤规约不生成，仓储登记照常生成 |

## 相关

- [持久化](../guide/persistence#登记实体)
- [领域模型](../guide/domain#规约)
- [Persistence.Abstractions](./persistence-abstractions)
- [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)
- [Kernel.Generators 操作生成器](./kernel-generators)
- 规范：RFC-0005《领域协议与持久化》的「实体登记」一节
