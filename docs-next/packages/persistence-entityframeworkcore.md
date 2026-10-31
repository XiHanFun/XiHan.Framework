# XiHan.Framework.Persistence.EntityFrameworkCore

`persistence.relational@1` 的 Entity Framework Core Provider：应用持有自己的 `DbContext`，框架在它之上提供仓储、事务型工作单元与协议约定。适合新项目与使用 EF Core 迁移管理表结构的应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Persistence.EntityFrameworkCore` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `persistence.relational@1`（`role: provider`）；依赖 `kernel.hosting@1`，可选依赖 `multitenancy.sharding@1`；绑定 `db`（`connection:relational`，可选） |
| 特性入口 | `EntityFrameworkCorePersistenceFeature<TContext>` |
| 配置节 | `XiHan:Persistence:EntityFrameworkCore` |
| 依赖 | `XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.MultiTenancy.Abstractions`、`Microsoft.EntityFrameworkCore` 10.0.13；数据库驱动包由应用引用 |
| AOT | 不兼容（EF Core 的运行时模型构建不满足 AOT 分析器） |
| 稳定性 | Preview |

## 概述

特性以 `AddDbContext<TContext>` 注册应用的 `DbContext`，每次构建选项时调用应用给的配置回调；同一作用域里的仓储、工作单元与应用自己注入的 `TContext` 是同一个实例。

工作单元在第一次写入时开启数据库事务，只读调用不开事务。仓储的每次写入立即 `SaveChanges`，所以同一事务里随后的查找与查询读得到它；提交即提交事务，回滚即回滚事务并清空变更跟踪器。批量方法用一次 `IN` 查询取回目标行、逐项校验后一次 `AddRange` / 跟踪修改，再一次 `SaveChanges`。

软删除与租户过滤由仓储用 `EF.Property` 显式施加在每个查询上，不用 EF Core 的全局查询过滤器，`FindAsync` 与 LINQ 查询行为一致；应用直接用 `DbContext` 查询时不带这两项过滤。更新游离实体时先比对并发戳，跟踪中的实体由 EF Core 的并发令牌保护。

## 何时使用

- 新项目，或已经用 EF Core 的应用。
- 表结构用 EF Core 迁移管理，复杂查询直接写在 `DbContext` 上。

已有大量 SqlSugar 代码、需要一次调用写多个库或按时间分表时用 [Persistence.SqlSugar](./persistence-sqlsugar)；两个关系型 Provider 的 `IsAotCompatible` 都是 `false`，5.0 里标注为 AOT 兼容的持久化实现只有 [Persistence.InMemory](./persistence-inmemory)，AOT 发布的整体要求见[序列化与 Native AOT](../guide/aot)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Persistence.EntityFrameworkCore" Version="5.0.0-preview.1" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.13" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。数据库驱动按所用数据库引用（Sqlite、Npgsql、SqlServer 等）。

1. 定义 `DbContext`：登记实体后调用 `ApplyPersistenceConventions()`。
2. 登记实体：`AddRepository<TEntity, TKey>()`。
3. 装配特性，在回调里用绑定 `db` 配置连接。

```csharp
using Microsoft.EntityFrameworkCore;
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.EntityFrameworkCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x
    .UseStandardProfile()
    // 显式 Provider 顶替 Standard 里的内存默认实现
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(
        static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value))));

var app = builder.Build();
app.UseXiHan();
app.Run();

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();
        // 在登记全部实体之后调用：并发戳设为并发令牌，租户与软删除标记建索引
        modelBuilder.ApplyPersistenceConventions();
    }
}
```

引用了 `Persistence.Generators` 时，`OnModelCreating` 里换成生成的 `modelBuilder.ApplyGeneratedEntities()`，它登记本程序集的全部实体并接着调用 `ApplyPersistenceConventions()`，见 [Persistence.Generators](./persistence-generators)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Persistence:EntityFrameworkCore:EnsureCreated` | 布尔 | `false` | 启动时对默认连接调用 `EnsureCreated` 建表；只用于开发与测试，生产用迁移 |
| `XiHan:Persistence:EntityFrameworkCore:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Bindings:db` | 字符串 | — | 连接绑定的主值，作为 `BindingValue.Value` 交给回调 |

```json
{
  "XiHan": {
    "Bindings": {
      "db": "Data Source=app.db"
    },
    "Persistence": {
      "EntityFrameworkCore": {
        "EnsureCreated": true
      }
    }
  }
}
```

回调签名是 `Action<Maybe<BindingValue>, DbContextOptionsBuilder>`。绑定缺失时 `Maybe` 为空，回调自己决定连接；上例的 `ValueOrThrow()` 在缺失时抛出。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `EntityFrameworkCorePersistenceFeature<TContext>` | 特性入口；构造参数是配置回调；常量 `ConnectionBinding`（`db`）与 `HealthName`（`persistence.entityframeworkcore.database`） |
| `EntityFrameworkCorePersistenceOptions` | 选项：`EnsureCreated`、`HealthProbeInterval`；`FromConfiguration(configuration)` |
| `ModelBuilderExtensions.ApplyPersistenceConventions()` | 对模型里已有的每个实体：`ConcurrencyStamp` 设为并发令牌、最大长度 32；`TenantId` 与 `IsDeleted` 建索引 |
| `EntityFrameworkCoreUnitOfWork` | 事务型工作单元；`Context` 是本作用域的 `DbContext` |
| `EntityFrameworkCoreRepository<TEntity, TKey>` | 仓储实现，由特性按登记注册 |

## 使用示例

仓储覆盖不了的查询直接注入应用的 `DbContext`。它与仓储共享同一个实例和同一个事务，但不带软删除与租户过滤，要自己加：

```csharp
using Microsoft.EntityFrameworkCore;

namespace SimpleApp.Orders;

public sealed class OrderReports(AppDbContext db)
{
    // 直接查询绕过仓储的过滤：软删除与租户条件要自己写
    public Task<int> TotalAmountAsync(string tenantId, CancellationToken cancellationToken)
        => db.Set<Order>()
            .Where(o => !o.IsDeleted && o.TenantId == tenantId)
            .SumAsync(static o => o.Amount, cancellationToken);
}
```

用 `builder.Services.AddScoped<OrderReports>()` 注册后在操作里注入。在管道外使用时同样要先开工作单元，见[工作单元与事务](../guide/uow#在管道外开工作单元)。

## 行为与限制

- 读己之写：每次写入立即 `SaveChanges`，在本事务内可见，提交前对其他连接不可见。
- 延迟开启事务：只读过数据的调用不开事务，多个只读调用与一个写入调用能在 SQLite 这类单写者数据库上并存。
- 游离实例更新：先比对并发戳，不符返回 `PERSISTENCE_CONFLICT_001`；通过后把游离实例的值整体复制到跟踪实例上（含 `CreatedAt`、`TenantId`），保存成功后把新的修改时间与并发戳同步回游离实例。
- 提交时还有残余变更（绕过仓储直接改了跟踪实体）会先保存再提交；并发令牌不匹配返回 `PERSISTENCE_CONFLICT_001` 并回滚。
- 只读工作单元：仓储写入时返回 `PERSISTENCE_VALIDATION_002`；绕过仓储改了跟踪实体的，提交时同样拒绝。
- 作用域结束时事务仍未提交（中间件之外直接使用），释放即回滚。
- 租户分库：装配了 `multitenancy.sharding@1` 时，`DbContext` 选项按作用域构建；带分片的调用把分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）交给同一个回调，整个 `DbContext` 连到分片库，其余调用用默认连接。分片库的表结构由应用管理，`EnsureCreated` 只作用于默认连接。没有 SqlSugar Provider 那样按实体分库的落点规则。
- 健康项 `persistence.entityframeworkcore.database`：启动时同步探一次，之后按间隔在后台重探并缓存结果；探测在独立作用域里对默认连接 `CanConnect`，不开工作单元、不经管道。连不上为 Degraded（`PERSISTENCE_UNAVAILABLE_003`），应用仍在服务。
- 整数主键按 EF Core 约定由数据库生成；应用自己生成整数标识时配 `ValueGeneratedNever()`，见[标识生成](../guide/distributed-ids)。

## 相关

- [持久化](../guide/persistence)
- [工作单元与事务](../guide/uow)
- [多租户](../guide/multi-tenancy)
- [Persistence.Abstractions](./persistence-abstractions)
- [Persistence.SqlSugar](./persistence-sqlsugar)
- [Persistence.Generators](./persistence-generators)
- 规范：RFC-0005《领域协议与持久化》
- 决策：ADR-0005
