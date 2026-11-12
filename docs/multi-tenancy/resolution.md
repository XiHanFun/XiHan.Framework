# 租户解析与隔离

这一页讲框架怎样在一次调用里确定租户、租户信息存在哪里、怎样启用多租户，以及租户确定之后持久化层怎样把数据隔开。读完你能让 SimpleApp 这样的应用按调用方身份识别出租户，知道解析在管道的哪一步发生、失败时拿到什么错误码，并让自己的实体只对本租户可见。阅读前先了解[执行管道](/startup/pipeline)与[调用上下文](/startup/invocation-context)；租户来自主体的声明，[认证](/security/authentication)一章可以之后再读。

## 为什么需要

一个 SaaS 应用要同时服务多家客户，每家客户的数据不能被别家看见。如果把「当前租户」交给业务代码自己传，就会在每个仓储方法、每条查询上重复一遍，漏掉一处就是越权；如果用环境态的全局访问器，后台任务、消息处理与并发调用又容易拿到错的值。框架把租户当成调用上下文的一部分：管道的 Tenant 阶段在每次调用里解析一次、核对一次、写进安全上下文，之后的阶段只读它。持久化 Provider 据此自动过滤多租户实体，业务代码不需要再写一次 `WHERE TenantId = ...`。

## 租户在调用里的位置

管道的阶段顺序是 `Authenticate → Tenant → Authorize → ... → Transaction → Handle`。Tenant 阶段的 `TenantResolutionMiddleware` 解析并核对租户后写入 `SecurityContext.Tenant`；它在认证之后，所以能读到已认证主体的租户声明；在授权之前，所以租户专属的授予在判权时已经生效。Transaction 阶段用这个租户开工作单元，幂等键与审计记录也带上它。

框架没有环境态的「当前租户」访问器，读租户只有一个入口：

```csharp
var tenant = context.GetSecurity().Tenant;   // TenantContext?，没有租户时为 null
```

`TenantContext` 只有 `Id` 与 `DisplayName` 两个成员。

多租户分成两个能力，由同一个 Provider 提供：

| 能力 ID | 作用 | 这一章的位置 |
| --- | --- | --- |
| `multitenancy.context@1` | 解析、校验与存储租户 | 本页 |
| `multitenancy.sharding@1` | 按租户选数据连接（分库） | [租户分片与分库](/multi-tenancy/sharding) |

| 包 | 清单角色 | 租户存在哪里 |
| --- | --- | --- |
| `XiHan.Framework.MultiTenancy.InMemory` | `default` | 启动时从配置 `XiHan:MultiTenancy:Tenants` 装载，运行时可增删，有容量上限 |
| `XiHan.Framework.MultiTenancy.Database` | `provider` | 数据库表 `TenantRecord`，经装配的持久化 Provider 读写，见[数据库租户目录](/multi-tenancy/database) |

组合包 `Profiles.Standard` 与 `Profiles.Full` 已包含 `MultiTenancy.InMemory`；再 `UseFeature<DatabaseMultiTenancyFeature>()` 即由数据库目录顶替。

## 租户信息与 ITenantStore

一个租户是一条 `TenantInfo`：

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | `string` | 租户 ID，赋值时按文法校验 |
| `DisplayName` | `string?` | 显示名，解析后随租户进入安全上下文 |
| `IsEnabled` | `bool` | 缺省 `true`；禁用的租户在 Tenant 阶段被拒绝 |
| `Properties` | `IReadOnlyDictionary<string, string>` | 租户专属属性（连接串、配额等），键区分大小写 |

租户 ID 的文法是 `^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$`：字母或数字开头，之后是字母、数字、`.`、`_`、`-`，最长 64 个字符（`TenantIdentifier.MaxLength`），区分大小写。`TenantIdentifier.IsValid` 判断是否合法，`TenantIdentifier.Validate` 不合法时抛出 `ArgumentException`。

存储契约 `ITenantStore` 只有两个方法：`FindAsync(tenantId)` 按 ID 查找，返回 `Result<Maybe<TenantInfo>>`，不存在是成功的无值；`ListAsync(TenantQuery)` 返回 `Result<TenantPage>`，按 ID 一页一页读（`PageSize` 1～1000、不透明的 `ContinuationToken`、`EnabledOnly`），令牌为空即读完。存储连不上这类基础设施故障返回 `MULTITENANCY_UNAVAILABLE_003`，不伪装成「租户不存在」，Tenant 阶段据此以 503 结束调用而不是放行。

## 最小示例

内存实现从配置装载租户。下面是 `framework/samples/SimpleApp` 的配置：`acme` 启用，`globex` 已禁用。每个子节的节名就是租户 ID，`IsEnabled` 缺省视为启用，`Properties` 下的键值进 `TenantInfo.Properties`。

```json
{
  "XiHan": {
    "MultiTenancy": {
      "Tenants": {
        "acme": { "DisplayName": "Acme Corp" },
        "globex": { "DisplayName": "Globex", "IsEnabled": false }
      }
    },
    "Security": {
      "InMemory": {
        "Principals": {
          "alice": { "ApiKey": "dev-alice-key", "DisplayName": "Alice", "Roles": "editor", "Tenant": "acme" }
        }
      }
    }
  }
}
```

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
app.MapGet("/tenant", (HttpContext http) => http.FindXiHanContext().ValueOrThrow().GetSecurity().Tenant?.Id ?? "-");

app.Run();
```

运行后用 `alice` 的 ApiKey 调用，响应是 `acme`；匿名调用响应 `-`，因为匿名主体没有租户声明：

```bash
curl -H "Authorization: ApiKey dev-alice-key" http://localhost:5000/tenant   # acme
curl http://localhost:5000/tenant                                            # -
```

操作契约的处理器里同样从传进来的调用上下文读：`context.GetSecurity().Tenant`。

## 解析流程

Tenant 阶段按下面的顺序处理，任一步失败即以结果结束调用，后面的阶段不再执行：

1. 按注册顺序调用 `ITenantResolver`，第一个给出值的生效；某个解析器返回失败时以它的错误结束调用，后面的解析器不再调用，默认租户也不顶替。
2. 都没有值时用 `DefaultTenant`；仍没有时，`RequireTenant` 为真返回 `MULTITENANCY_VALIDATION_002`，否则以无租户继续。框架自己声明的平台调用（如工作流轮询平台分区）例外：不补缺省租户、不要求带租户（ADR-0192）。
3. 校验租户 ID 文法，不合法返回 `MULTITENANCY_VALIDATION_001`。先校验再查存储，未经校验的外部输入不会交给 Provider。
4. `ValidateAgainstStore` 为真（缺省）时查 `ITenantStore`：存储不可达返回 `MULTITENANCY_UNAVAILABLE_003`，不存在返回 `MULTITENANCY_NOTFOUND_001`，已禁用返回 `MULTITENANCY_DENIED_001`；存在且启用时以存储里的记录为准，显示名随之带入。关闭存储核对时只校验文法，安全上下文里没有显示名。
5. 写入 `SecurityContext.Tenant`。
6. 容器里有 `ITenantShardResolver` 时解析分片并写进调用上下文，见[租户分片与分库](/multi-tenancy/sharding)。

多租户 Provider 注册两个解析器，注册顺序就是优先级：

| 顺序 | 解析器 | 读什么 |
| --- | --- | --- |
| 1 | `ClaimTenantResolver` | 已认证主体的租户声明（类型由 `TenantClaim` 决定，缺省 `tenant`），同类声明有多个时取第一个；未认证主体直接跳过 |
| 2 | `BaggageTenantResolver` | 调用上下文的 Baggage `xihan.tenant`，由进程内建上下文的地方放进去 |

应用登记自己的 `ITenantResolver` 会排在这两个之后。解析器只负责给出候选 ID，校验都在中间件里。

### 网络入站不接受自报租户

Baggage 里的租户只来自进程内：消息派发时取信封的扩展属性 `xihantenant`，作业与工作流带上发起时的租户，命令行宿主用 `--tenant`。

HTTP、gRPC、MCP 的网络入站不收 `xihan.tenant` 与 `xihan.principal`：`baggage` 头是调用方自己写的，解析器只核对租户存在且启用，不核对调用方属不属于它。所以这些调用方的租户只来自认证（令牌或 ApiKey 的租户声明）。

CloudEvents 入站端点是例外：事件的 `xihantenant` 由发送方给，经消息派发进 Baggage，不和请求的身份核对。这个端点的权限要由宿主自己加，SimpleApp 把它授给专门的服务账号，见 [CloudEvents](/protocols/cloudevents)。

### 自定义解析器

要按主机名或请求头选租户时，登记自己的解析器，由它决定信什么、核对什么；中间件接着核对租户存在且启用。`ResolveAsync` 返回 `Result<Maybe<string>>`，有三种结果：

| 结果 | 含义 |
| --- | --- |
| 成功且有值 | 候选租户 ID，后面的解析器不再调用 |
| 成功且无值 | 这个解析器给不出租户，交给下一个解析器 |
| 失败 | 拒绝这次调用：Tenant 阶段以这个错误结束，后面的解析器不再调用，默认租户也不顶替 |

```csharp
using Microsoft.AspNetCore.Http;
using XiHan.Framework.MultiTenancy.Abstractions.Resolution;
using XiHan.Framework.Primitives;

public sealed class SubdomainTenantResolver(IHttpContextAccessor http) : ITenantResolver
{
    public ValueTask<Result<Maybe<string>>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken)
    {
        var host = http.HttpContext?.Request.Host.Host;
        // 不是 HTTP 调用（消息派发、作业）：给不出租户，交给后面的解析器
        if (host is null)
        {
            return ValueTask.FromResult(Result.Success(Maybe.None<string>()));
        }

        // 不在本站域名下：拒绝调用（ShopErrors、ShopMessages 是应用自己的错误码与消息键）
        if (!host.EndsWith(".example.com", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(Result.Failure<Maybe<string>>(Error.Validation(ShopErrors.UnknownHost, ShopMessages.UnknownHost)));
        }

        // acme.example.com → acme
        return ValueTask.FromResult(Result.Success(Maybe.Some(host[..host.IndexOf('.')])));
    }
}
```

在特性的 `Configure` 里按单例登记：`context.Services.AddSingleton<ITenantResolver, SubdomainTenantResolver>()`。按主机名选租户时要自己判断「这个调用方能不能进这个租户」，否则任何人都能用子域名进任何租户；一种做法是再配一个要求主体租户声明与解析结果一致的策略，见[授权与权限](/security/authorization)。

## 配置

`MultiTenancyOptions` 从配置节 `XiHan:MultiTenancy` 读取：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `DefaultTenant` | `string` | 无 | 解析器都没有给出值时使用；同样经过文法与存储校验；空字符串表示清除 |
| `RequireTenant` | `bool` | `false` | 为真时没有租户的调用返回 `MULTITENANCY_VALIDATION_002` |
| `ValidateAgainstStore` | `bool` | `true` | 为真时租户必须在存储中存在且启用；为假时只校验文法 |
| `TenantClaim` | `string` | `tenant` | `ClaimTenantResolver` 读取的声明类型，不能为空 |
| `ShardCacheSize` | `int` | 10000 | 分片解析器按租户缓存结果的条目上限，满时整体清空；必须是正整数 |

布尔值只接受 `true` / `false`，`ShardCacheSize` 只接受正整数，其他写法在启动时抛出 `ArgumentException`。

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
| `Security.InMemory` 的 ApiKey | 主体记录的 `Tenant`，写成类型为 `TenantClaim` 的声明（读同一个配置键 `XiHan:MultiTenancy:TenantClaim`） |
| `Security.Jwt` / `Security.OpenIdConnect` | 令牌里类型等于 `TenantClaim` 的声明，声明按原始类型名保留 |

HTTP 调用方的租户只来自声明。以 SimpleApp 为例（`alice` 的记录是 `Tenant: acme`）：

| 调用 | 结果 |
| --- | --- |
| `alice` 的 ApiKey | 租户 `acme` |
| `alice` 的 ApiKey，请求头 `baggage: xihan.tenant=globex` | 租户仍是 `acme`，这个键入站不收 |
| 匿名，请求头 `baggage: xihan.tenant=acme` | 无租户，这个键入站不收 |
| 匿名，`RequireTenant` 为真 | `MULTITENANCY_VALIDATION_002` |

主体没有租户声明时（匿名调用、平台级账号）就没有租户。平台账号要进入某个租户，由应用换发带租户声明的令牌，并在换发时核对成员关系。

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
- 更新不能把实体改到别的租户：租户列由 Provider 写，调用方实例上的值不生效。

租户目录（`TenantRecord`）、权限授予（`PermissionGrantRecord`）、审计记录这类平台数据不实现 `IMultiTenant`，不受当前租户过滤，租户在它们自己的列里。仓储与工作单元的完整用法见[仓储读写](/persistence/repositories)与[工作单元与事务](/persistence/unit-of-work)。

## 错误码

| 错误码 | 含义 | HTTP |
| --- | --- | --- |
| `MULTITENANCY_VALIDATION_001` | 租户 ID 不合法 | 400 |
| `MULTITENANCY_VALIDATION_002` | 调用必须带租户但没有解析到 | 400 |
| `MULTITENANCY_NOTFOUND_001` | 租户不存在 | 404 |
| `MULTITENANCY_DENIED_001` | 租户已禁用 | 403 |
| `MULTITENANCY_UNAVAILABLE_001` | 内存租户存储容量已满（`InMemoryTenantStore.Set` 与健康项 `multitenancy.inmemory.capacity`） | 503 |
| `MULTITENANCY_UNAVAILABLE_002` | 租户分片无法解析（租户存储不可达） | 503 |
| `PERSISTENCE_VALIDATION_001` | 多租户实体在没有租户的调用中新增 | 400 |

HTTP 状态码按错误码的种类段映射，与其他子系统一致，见[结果与错误处理](/web/errors)。

## 常见问题

### 为什么请求头里带了 `baggage: xihan.tenant=acme` 却没有租户

HTTP、gRPC、MCP 入站不收这个键。网络调用方的租户只来自认证：ApiKey 主体记录的 `Tenant`，或令牌里类型为 `TenantClaim` 的声明。要按主机名、请求头选租户，登记自己的 `ITenantResolver`，并自己核对调用方属于该租户。

### 平台管理员怎样进入某个租户

平台账号没有租户声明，解析不出租户。由应用的登录服务换发一张带目标租户声明的令牌（换发时核对成员关系或管理员身份），再用新令牌调用。不要通过 `DefaultTenant` 或关闭 `ValidateAgainstStore` 绕过。

### 禁用一个租户后它的调用会怎样

Tenant 阶段核对存储时发现 `IsEnabled` 为假，调用以 `MULTITENANCY_DENIED_001`（HTTP 403）结束，不进入授权与业务代码。内存实现运行时用 `InMemoryTenantStore.Set` 改写记录即可生效；数据库目录的生效时机受缓存存活时间影响，见[数据库租户目录](/multi-tenancy/database)。

### 不需要多租户的应用要关掉它吗

不用。没有租户声明的调用就是无租户调用，`RequireTenant` 缺省为假，不实现 `IMultiTenant` 的实体不受过滤。不想装配时用 `Profiles.Minimal` 再按需 `UseFeature`，Tenant 阶段就是空的。

### 后台作业里怎样拿到租户

作业没有网络调用方，租户随作业在发起时记下并放进 Baggage，`BaggageTenantResolver` 在作业的调用里读出来；见[作业与计划](/scheduling/jobs)。自己建服务作用域、不经管道的代码没有安全上下文，租户与分片都要显式给出，见[租户分片与分库](/multi-tenancy/sharding)的「管道外绑定分片」。

## 相关包与下一步

- 包参考：[MultiTenancy.Abstractions](/packages/multitenancy-abstractions)、[MultiTenancy.InMemory](/packages/multitenancy-inmemory)、[MultiTenancy.Database](/packages/multitenancy-database)
- 规范：RFC-0007《多租户》
- 相关章节：[执行管道](/startup/pipeline)、[认证](/security/authentication)、[授权与权限](/security/authorization)、[仓储读写](/persistence/repositories)、[审计](/common/auditing)
- 下一步：[租户分片与分库](/multi-tenancy/sharding)
