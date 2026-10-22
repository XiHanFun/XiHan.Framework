# XiHan.Framework.Persistence.Generators

Roslyn 源生成器（工具链）。扫描本程序集里实现 `IEntity<TKey>`（含经 `IAggregateRoot<TKey>`）的非抽象顶层类，生成：

- `GeneratedEntities` 静态类（命名空间为项目的 `RootNamespace`）：`AddGeneratedRepositories(this IServiceCollection)` 为每个实体调用 `AddRepository<TEntity, TKey>()`；项目引用了 EF Core 时再生成 `ApplyGeneratedEntities(this ModelBuilder)`，把每个实体 `Entity<T>()` 进模型，并在引用了 `Persistence.EntityFrameworkCore` 时接着 `ApplyPersistenceConventions()`。
- 每个实现 `ISoftDeletable` 或 `IMultiTenant` 的实体一个 `<实体>Filters` 静态类（与实体同命名空间、同可访问性）：`NotDeleted`、`ForTenant(tenantId)`、两者都有时 `ActiveFor(tenantId)`，都是 `Specification<T>`，可直接传给 `EntityQuery<T>.Where(...)`。

```csharp
public sealed class Order : IAggregateRoot<Guid>, IMultiTenant, ISoftDeletable { … }

public void Configure(FeatureContext context) => context.Services.AddGeneratedRepositories();   // 生成的登记代码

protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyGeneratedEntities();   // EF Core 应用

var active = await repository.ListAsync(EntityQuery.All<Order>().Where(OrderFilters.ActiveFor(tenantId)), ct);
```

| 规则 | 含义 |
| --- | --- |
| `XHP001` | 实体类通过多个 `IEntity<TKey>` 声明了不同的键类型 |
| `XHP002` | 实体类是嵌套类型或不是 public / internal |
| `XHP003` | 要生成的 `<实体>Filters` 与已有类型同名，跳过该实体的过滤规约（警告） |

引用方式：`<ProjectReference Include="…/XiHan.Framework.Persistence.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`，或 NuGet 包（分析器形式，不进入运行时依赖）。

## 依赖方向

只依赖 `Microsoft.CodeAnalysis.CSharp`；生成的代码引用 `XiHan.Framework.Domain` 与 `XiHan.Framework.Persistence.Abstractions`，EF Core 部分只在项目自己引用了 EF Core 时生成。

## 状态

P3：可用。
