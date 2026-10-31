# 多租户

本章讲 5.0 怎样在一次调用里确定租户、租户存在哪里、租户数据怎样隔离到各自的库，适用于做 SaaS 或需要按租户隔离数据的应用。完整 API 见 [MultiTenancy.Abstractions](../packages/multitenancy-abstractions)、[MultiTenancy.InMemory](../packages/multitenancy-inmemory) 与 [MultiTenancy.Database](../packages/multitenancy-database) 三个包页。

## 租户在调用里的位置

租户是调用上下文的一部分。管道的 Tenant 阶段（`TenantResolutionMiddleware`）在每次调用里解析一次租户、核对后写进安全上下文 `SecurityContext.Tenant`，之后的阶段只读它：Authorize 阶段按租户取授予，Transaction 阶段用租户开工作单元，幂等键与审计记录带上租户。5.0 没有环境态的"当前租户"访问器，读租户只有一个入口：`context.GetSecurity().Tenant`。

阶段顺序决定了租户能用到什么：`Authenticate → Tenant → Authorize`。Tenant 阶段在认证之后，所以能读已认证主体的租户声明；在授权之前，所以租户专属的授予在判权时已经生效。

能力分两个：`multitenancy.context@1` 负责解析、校验与存储，`multitenancy.sharding@1` 负责按租户选数据连接（分库）。两个能力都由同一个多租户 Provider 提供。

| 包 | 清单角色 | 租户存在哪里 |
| --- | --- | --- |
| `XiHan.Framework.MultiTenancy.InMemory` | `default` | 启动时从配置 `XiHan:MultiTenancy:Tenants` 装载，运行时可增删，有容量上限 |
| `XiHan.Framework.MultiTenancy.Database` | `provider` | 数据库表（`TenantRecord`），经装配的持久化 Provider 读写 |

组合包 `Profiles.Standard` 与 `Profiles.Full` 已包含 `MultiTenancy.InMemory`；再 `UseFeature<DatabaseMultiTenancyFeature>()` 即由数据库目录顶替。

## 租户信息

一个租户是一条 `TenantInfo`：

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | `string` | 租户 ID，赋值时按文法校验 |
| `DisplayName` | `string?` | 显示名，解析后随租户进入安全上下文 |
| `IsEnabled` | `bool` | 缺省 `true`；禁用的租户在 Tenant 阶段被拒绝 |
| `Properties` | `IReadOnlyDictionary<string, string>` | 租户专属属性（连接串、配额等），键区分大小写 |

租户 ID 的文法是 `^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$`：字母或数字开头，之后是字母、数字、`.`、`_`、`-`，最长 64 个字符（`TenantIdentifier.MaxLength`），区分大小写。`TenantIdentifier.IsValid` 判断是否合法，`TenantIdentifier.Validate` 不合法时抛出 `ArgumentException`。

存储契约 `ITenantStore` 只有两个方法：`FindAsync` 按 ID 查找（不存在为无值）与 `ListAsync` 枚举。存储连不上这类基础设施故障以异常抛出，不伪装成"租户不存在"，Tenant 阶段据此让调用失败。

## 启用

内存实现从配置装载租户。下面是 `next/samples/SimpleApp` 的配置：`acme` 启用，`globex` 已禁用。

```json
{
  "XiHan": {
    "MultiTenancy": {
      "Tenants": {
        "acme": { "DisplayName": "Acme Corp" },
        "globex": { "DisplayName": "Globex", "IsEnabled": false }
      }
    }
  }
}
```

每个子节的节名就是租户 ID，`IsEnabled` 缺省视为启用，`Properties` 下的键值进 `TenantInfo.Properties`。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// Standard 组合包已含内存多租户与内存安全
builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();

// 端点在管道的 Handle 阶段内执行，租户已由 Tenant 阶段写进安全上下文
app.MapGet("/tenant", (HttpContext http) => http.GetXiHanContext().ValueOrThrow().GetSecurity().Tenant?.Id ?? "-");

app.Run();
```

操作契约的处理器里直接用传进来的调用上下文：`context.GetSecurity().Tenant`。`TenantContext` 有 `Id` 与 `DisplayName` 两个成员；没有租户的调用为 `null`。

## 解析流程

Tenant 阶段按下面的顺序处理，任一步失败即以结果结束调用，后面的阶段不再执行：

1. 按注册顺序调用全部 `ITenantResolver`，第一个给出值的生效。
2. 都没有值时用 `DefaultTenant`；仍没有时，`RequireTenant` 为真返回 `MULTITENANCY_VALIDATION_002`，否则以无租户继续。
3. 校验租户 ID 文法，不合法返回 `MULTITENANCY_VALIDATION_001`。先校验再查存储，未经校验的外部输入不会交给 Provider。
4. `ValidateAgainstStore` 为真（缺省）时查 `ITenantStore`：不存在返回 `MULTITENANCY_NOTFOUND_001`，已禁用返回 `MULTITENANCY_DENIED_001`；存在且启用时以存储里的记录为准，显示名随之带入。关闭存储核对时只校验文法，安全上下文里没有显示名。
5. 写入 `SecurityContext.Tenant`。
6. 容器里有 `ITenantShardResolver` 时解析分片，写进调用上下文（见[租户分片](#租户分片)）。

多租户 Provider 注册两个解析器，注册顺序就是优先级：

| 顺序 | 解析器 | 读什么 |
| --- | --- | --- |
| 1 | `ClaimTenantResolver` | 已认证主体的租户声明（类型由 `TenantClaim` 决定，缺省 `tenant`），同类声明有多个时取第一个；未认证主体直接跳过 |
| 2 | `BaggageTenantResolver` | 调用上下文的 Baggage `xihan.tenant`，即调用方自报的租户 |

应用登记自己的 `ITenantResolver` 会排在这两个之后。解析器只负责给出候选 ID，校验都在中间件里。

调用方按协议把租户放进 Baggage：HTTP 用 W3C `baggage` 头，gRPC 用同名元数据，命令行宿主用 `--tenant`，CloudEvents 用扩展属性 `xihantenant`。宿主只搬运 Baggage，不直接写租户。

```bash
# SimpleApp 运行后，匿名调用自报租户
curl -H "baggage: xihan.tenant=acme" http://localhost:5000/whoami
```

## 配置

`MultiTenancyOptions` 从配置节 `XiHan:MultiTenancy` 读取（`MultiTenancyOptions.FromConfiguration`）：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `DefaultTenant` | `string` | 无 | 解析器都没有给出值时使用；同样经过文法与存储校验；空字符串表示清除 |
| `RequireTenant` | `bool` | `false` | 为真时没有租户的调用返回 `MULTITENANCY_VALIDATION_002` |
| `ValidateAgainstStore` | `bool` | `true` | 为真时租户必须在存储中存在且启用；为假时只校验文法 |
| `TenantClaim` | `string` | `tenant` | `ClaimTenantResolver` 读取的声明类型，不能为空 |

布尔值只接受 `true` / `false`，其他写法在启动时抛出 `ArgumentException`。

分片缓存容量 `MultiTenancyOptions.ShardCacheSize`（缺省 10000，最小 1）没有对应的配置键，`FromConfiguration` 不读取它。

```json
{
  "XiHan": {
    "MultiTenancy": {
      "RequireTenant": true,
      "TenantClaim": "tenant"
    }
  }
}
```

## 租户与主体声明

认证 Provider 把租户写成主体的声明，`ClaimTenantResolver` 再从声明里取出来：

| 认证方式 | 租户声明从哪来 |
| --- | --- |
| `Security.InMemory` 的 ApiKey | 主体记录的 `Tenant`，写成类型为 `tenant` 的声明 |
| `Security.Jwt` / `Security.OpenIdConnect` | 令牌里类型等于 `TenantClaim` 的声明，声明按原始类型名保留 |

因为声明解析器排在 Baggage 之前，已认证且带租户声明的主体不能用 Baggage 换到别的租户。以 SimpleApp 为例（`alice` 的记录是 `Tenant: acme`）：

| 调用 | 结果 |
| --- | --- |
| `alice` 的 ApiKey，不带 Baggage | 租户 `acme` |
| `alice` 的 ApiKey，Baggage `xihan.tenant=globex` | 租户仍是 `acme`，声明优先 |
| 匿名，Baggage `xihan.tenant=acme` | 租户 `acme` |
| 匿名，Baggage `xihan.tenant=globex` | `MULTITENANCY_DENIED_001`（租户已禁用，HTTP 403） |
| 匿名，不带 Baggage，`RequireTenant` 为假 | 无租户 |

主体没有租户声明时（匿名调用、平台级账号），Baggage 自报的租户只经过文法与存储核对，框架不判断这个主体是否属于该租户。需要限制时在授权里判断，[授权](./authorization#自定义策略)一章给出了一个按租户声明核对的策略。

::: warning ApiKey 处理器写死声明类型 tenant
`ApiKeyAuthenticationHandler` 总是把主体记录的租户写成类型为 `tenant` 的声明，不读 `XiHan:MultiTenancy:TenantClaim`。改了 `TenantClaim` 之后，内存 ApiKey 主体的租户不再被 `ClaimTenantResolver` 识别。
:::

## 持久化层的租户过滤

实体实现 `IMultiTenant`（`XiHan.Framework.Domain.Entities`）就成为租户数据：

```csharp
using XiHan.Framework.Domain.Entities;

public sealed class Invoice : IEntity<Guid>, IMultiTenant
{
    public Guid Id { get; set; }

    // 新增时由持久化 Provider 写入工作单元的租户，调用方不用赋值
    public string TenantId { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
```

Transaction 阶段的 `UnitOfWorkMiddleware` 用安全上下文里的租户开工作单元（`UnitOfWorkOptions.TenantId`），持久化 Provider 据此施加规则：

- 新增多租户实体时写入工作单元的租户；工作单元没有租户时返回 `PERSISTENCE_VALIDATION_001`。
- 查询只返回 `TenantId` 等于当前租户的行；没有租户的调用看不到任何多租户实体。
- 更新、删除不属于当前租户的实体，与实体不存在一样返回 `PERSISTENCE_NOTFOUND_001`。

租户目录（`TenantRecord`）、权限授予（`PermissionGrantRecord`）、审计记录这类平台数据不实现 `IMultiTenant`，不受当前租户过滤，租户在它们自己的列里。仓储与工作单元的完整用法见[持久化](./persistence)与[工作单元与事务](./uow)。

## 租户分片

分片（ADR-0018）是 `multitenancy.sharding@1`：按租户把数据落到租户自己的库。5.0 只做库级隔离；行级隔离就是上一节的租户过滤，Schema 级隔离由应用经透传属性自行处理。

分片解析器契约是 `ITenantShardResolver`：`ResolveAsync(tenantId)` 返回 `Result<Maybe<TenantShard>>`，共库租户与未知租户是成功且无值，租户存储不可达返回 `MULTITENANCY_UNAVAILABLE_002`；`Invalidate(tenantId?)` 丢弃缓存，参数为 `null` 时清空全部。`TenantShard` 带租户 ID、连接串、可选的提供者名（转成小写，如 `postgresql`）与透传属性。

两个多租户 Provider 在分片能力被选中时都注册缺省解析器 `TenantStoreShardResolver`，它从租户属性推出分片：

| 租户属性键 | 常量 | 含义 |
| --- | --- | --- |
| `ConnectionString` | `TenantShardKeys.ConnectionString` | 有值即该租户分库；没有或为空白是共库租户 |
| `DatabaseProvider` | `TenantShardKeys.DatabaseProvider` | 数据库提供者名，可选；空表示与应用默认连接同一种 |
| `Shard.<key>` | `TenantShardKeys.PropertyPrefix` | 去掉前缀后作为 `<key>` 透传给持久化 Provider；用点不用冒号，因为属性来自配置时冒号是节分隔符 |

```json
{
  "XiHan": {
    "MultiTenancy": {
      "Tenants": {
        "acme": {
          "DisplayName": "Acme Corp",
          "Properties": {
            "ConnectionString": "Host=localhost;Database=acme",
            "DatabaseProvider": "postgresql"
          }
        },
        "beta": { "DisplayName": "Beta" }
      }
    }
  }
}
```

解析结果按租户缓存，"无分片"也缓存；缓存条目数达到 `ShardCacheSize`（10000）时整体清空。租户连接变更后调用 `ITenantShardResolver.Invalidate(tenantId)`；经 `TenantRecordStore` 写入的数据库目录会自动失效。

分片在 Tenant 阶段解析一次，写进调用上下文（`context.GetTenantShard()` 可读）；解析失败即调用失败，不降级为共库，因为降级会把租户数据写进平台库。无租户的调用没有分片。

持久化 Provider（`Persistence.SqlSugar`、`Persistence.EntityFrameworkCore`）以可选依赖声明 `multitenancy.sharding@1`，在作用域内创建客户端时经 `TenantShardBinding.Resolve` 读分片，不访问存储、不阻塞。分片是实体级的落点，不是整个作用域的切换：带分片的调用里，实现 `IMultiTenant` 的实体走分片连接，平台表仍走默认连接。

| Provider | 带分片的调用 |
| --- | --- |
| `Persistence.SqlSugar` | 客户端同时持有默认连接与分片连接（`ConfigId`），工作单元经 `AsTenant()` 在两个连接上开事务、依次提交或回滚（不是两阶段提交），见 [Persistence.SqlSugar · 多库落点](../packages/persistence-sqlsugar#多库落点) |
| `Persistence.EntityFrameworkCore` | 一个 `DbContext` 一个连接，整个上下文切到分片连接；平台表要另起 `DbContext`，由应用处理 |

::: warning 分片库的表结构由应用管理
框架的建表开关（SqlSugar 的 `InitTables`、EF Core 的 `EnsureCreated`）只作用于默认连接。租户库的建表与升级由应用自己执行，下一节的 `TenantShardScope` 用于在管道外连到租户库。
:::

## 管道外绑定分片

后台写入、调度作业、租户库初始化这类代码自己建服务作用域，不经 `App.InvokeAsync`，作用域里没有调用上下文，也就没有分片。作用域服务 `TenantShardScope`（ADR-0057）给这类作用域显式绑定分片：

| 成员 | 说明 |
| --- | --- |
| `BindTenantAsync(tenantId, cancellationToken)` | 经 `ITenantShardResolver` 解析并绑定，返回解析结果；不看租户是否启用，也不受应用是否在服务状态（启动、排空）影响；共库租户、未知租户与没有装配分片解析器时成功且不绑定；解析失败返回错误、不绑定 |
| `Bind(shard)` | 直接绑定一个分片 |
| `Shard` | 已绑定的分片，没有绑定为无值 |
| `IsConnected` | 作用域的持久化客户端是否已经取过连接 |

绑定必须发生在作用域内首次创建持久化客户端之前。客户端建好之后再绑定，或改绑到另一个分片，抛出 `InvalidOperationException`；同一个分片重复绑定没有副作用。显式绑定优先于调用上下文里的分片。

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

// Invoice 见上一节，仓储已经 AddRepository<Invoice, Guid>() 登记
public sealed class InvoiceCounter(IServiceScopeFactory scopes)
{
    public async Task<Result<long>> CountAsync(string tenantId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // 先绑定再解析任何持久化服务：客户端在作用域里只建一次，建好后改不了连接
        var bound = await services.GetRequiredService<TenantShardScope>().BindTenantAsync(tenantId, cancellationToken);
        if (bound.IsFailure)
        {
            return bound.Error!;
        }

        // 没有 Transaction 阶段代劳，工作单元自己开；租户由调用方给出，与绑定的租户一致
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(tenantId, IsReadOnly: true), cancellationToken);
        try
        {
            var invoices = services.GetRequiredService<IRepository<Invoice, Guid>>();
            var count = await invoices.CountAsync(EntityQuery.All<Invoice>(), cancellationToken);
            var committed = await unitOfWork.CommitAsync(cancellationToken);
            return committed.IsFailure ? committed.Error! : count;
        }
        catch
        {
            if (unitOfWork.State == UnitOfWorkState.Active)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
    }
}
```

工作单元的租户不会从绑定推导出来，两者都由调用方给出；管道外开工作单元、派发领域事件的完整写法见[工作单元与事务](./uow#在管道外开工作单元)。两个多租户 Provider 都登记 `TenantShardScope`，与是否选中分片能力无关；应用自带分片 Provider 时同样要登记它。

## 数据库租户目录

`MultiTenancy.Database`（ADR-0026）把租户目录放进数据库：每个租户一行 `TenantRecord`（主键是租户 ID，显示名、启用标记、属性 JSON、创建与修改时间、并发戳），经 `persistence.relational@1` 的仓储读写，所以还要装配一个持久化 Provider。解析顺序、`DefaultTenant` / `RequireTenant` / `ValidateAgainstStore` 与分片语义与内存实现相同，只换了存储。

读写分成两个类型：

- `DatabaseTenantStore`（单例 `ITenantStore`）：Tenant 阶段查它。读取不经管道，而是在根提供者的新作用域里开只读工作单元，因为嵌套调用会再过一遍 Tenant 阶段，`RequireTenant` 为真时会自我拒绝，启动期也读不了。查找结果按租户缓存，"不存在"也缓存。
- `TenantRecordStore`（作用域服务）：管理操作在 Transaction 阶段内用它批量新增、覆盖、删除租户，逐项返回结果；写入后立即失效目录与分片解析器的缓存。

缓存与枚举的配置在 `XiHan:MultiTenancy:Database`：

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `CacheSize` | 10000 | 缓存条目上限，"不存在"也占一条；满时整体清空 |
| `CacheTimeToLiveSeconds` | 30 | 每条缓存的存活时间；`0` 表示不过期，只靠 `Invalidate` 失效 |
| `MaxListedTenants` | 10000 | `ListAsync` 最多返回的租户数，超出部分不返回并记警告 |

写入在调用方事务提交后才对其他调用可见，失效与提交之间的窗口靠存活时间收敛；多实例部署时其他实例的缓存同样靠存活时间收敛。绕过 `TenantRecordStore` 直接改表时，自行调用 `DatabaseTenantStore.Invalidate` 与 `ITenantShardResolver.Invalidate`。装配与示例代码见 [MultiTenancy.Database](../packages/multitenancy-database)。

## 错误码

| 错误码 | 含义 | HTTP |
| --- | --- | --- |
| `MULTITENANCY_VALIDATION_001` | 租户 ID 不合法 | 400 |
| `MULTITENANCY_VALIDATION_002` | 调用必须带租户但没有解析到 | 400 |
| `MULTITENANCY_NOTFOUND_001` | 租户不存在；`TenantRecordStore.RemoveAsync` 删除不存在的租户也返回它 | 404 |
| `MULTITENANCY_DENIED_001` | 租户已禁用 | 403 |
| `MULTITENANCY_UNAVAILABLE_001` | 内存租户存储容量已满（`InMemoryTenantStore.Set` 与健康项） | 503 |
| `MULTITENANCY_UNAVAILABLE_002` | 租户分片无法解析（租户存储不可达） | 503 |
| `PERSISTENCE_VALIDATION_001` | 多租户实体在没有租户的调用中新增 | 400 |
| `KERNEL_CAPABILITY_001` | 装配了 `MultiTenancy.Database` 却没有持久化 Provider，启动中断 | — |

HTTP 状态码按错误码的种类段映射，与其他子系统一致，见[结果与错误](./errors)。

## 与其他能力的关系

- [认证](./authentication)：认证 Provider 把租户写成主体声明，Tenant 阶段在认证之后读它；认证阶段写安全上下文时保留已有租户。
- [授权](./authorization)：`PermissionChecker` 按安全上下文里的租户取授予，租户专属授予只对该租户生效。
- [缓存与分布式锁](./caching)：Idempotency 阶段的幂等键按租户与主体隔离。
- [审计](./auditing)：审计记录带租户 ID；审计表是平台数据，独立库租户的记录同样落默认连接。
- [调度与后台作业](./scheduling)：作业里访问租户库时用 `TenantShardScope` 绑定分片。

## 相关

- [MultiTenancy.Abstractions](../packages/multitenancy-abstractions)
- [MultiTenancy.InMemory](../packages/multitenancy-inmemory)
- [MultiTenancy.Database](../packages/multitenancy-database)
- [持久化](./persistence)
- 规范：RFC-0007《多租户》；ADR-0018、ADR-0026、ADR-0057
