# 授权与权限

这一页讲一次调用怎样被判定能否执行：权限的定义与登记、操作契约与端点上怎样声明授权要求、Authorize 阶段的处理顺序、启动时怎样核对策略引用、怎样写自定义策略与资源名。读完你能给自己的操作加上权限码，让 SimpleApp 的 `alice` 能下单而 `bob` 只能查看，并在启动时就发现拼错的权限码。阅读前先读[认证](/security/authentication)与[操作契约](/web/operations)。授予怎样存、怎样收窄与扩展在下一页 [RBAC 授予与条件](/security/grants)。

## 为什么需要

认证回答「是谁」，授权回答「能不能」。把判定散在每个处理器开头的 `if (!user.IsInRole(...))` 里，规则会随代码漂移，审计时找不到全貌，换协议还得重写一遍。框架把授权做成管道的 Authorize 阶段：操作契约上声明一个权限码或策略名，同一份契约经 HTTP、gRPC、MCP 与命令行调用，判定都在这一步完成一次；权限码在特性里登记，契约引用了不存在的权限码或策略名，启动就失败，不等到线上 404。

## 判定模型

一次调用最多带一个授权要求 `AuthorizationRequirement`：策略名加可选的资源名。Authorize 阶段的 `AuthorizationMiddleware` 按策略名在 `PolicyCatalog` 里找到 `IPolicy`，以 `AuthorizationRequest`（安全上下文、策略名、资源）调用它，得到允许或带原因的拒绝。权限是一种特殊的策略：策略名 `permission:<code>` 映射为 `PermissionPolicy`，由 `IPermissionChecker` 按 RBAC 授予判定。

授权是能力 `security.authorization@1`。它读 Authenticate 阶段写入的主体与 Tenant 阶段写入的租户，所以租户专属的授予在判权时已经生效。

| 包 | 清单角色 | 授予存在哪里 |
| --- | --- | --- |
| `XiHan.Framework.Security.InMemory` | `default` | 配置 `XiHan:Security:InMemory:Grants` 与进程内存；`Profiles.Standard` 已包含 |
| `XiHan.Framework.Security.Database` | `provider` | 数据库表 `PermissionGrantRecord`，经装配的持久化 Provider 读写 |

两个 Provider 登记的是同一套组件：`PermissionChecker`、`authenticated` 策略、`PolicyCatalog` 与 Authorize 阶段的 `AuthorizationMiddleware`，区别只在授予存储。

内置两种策略：

| 策略名 | 类型 | 判定 |
| --- | --- | --- |
| `authenticated` | `AuthenticatedPolicy` | 主体已认证；匿名返回 `SECURITY_DENIED_001` |
| `permission:<code>` | `PermissionPolicy` | 匿名先返回 `SECURITY_DENIED_001`，调用方据此区分未登录与越权；已认证但不持有权限返回 `SECURITY_DENIED_004`；检查器失败（授予读不到）原样作为判定失败，调用以 `SECURITY_UNAVAILABLE_001`（503）结束 |

`AuthorizationRequirement.Authenticated` 与 `AuthorizationRequirement.Permission(code, resource?)` 是这两种要求的快捷写法，`PermissionPolicy.NameFor(code)` 给出权限码对应的策略名。

## 定义与登记权限

权限是 `PermissionDefinition`：权限码、显示名、可选说明。权限码小写、点分、至少两段，每段以小写字母开头，可含数字与连字符，如 `orders.read`。特性在 `Configure` 里用 `FeatureContext.AddPermission` 登记到内核的 `PermissionRegistry`；权限码重复（`KERNEL_SECURITY_001`）或登记表已满（容量 10000，`KERNEL_SECURITY_002`）在装配时抛出。

特性清单的 `Contributes.Permissions` 是声明，`AddPermission` 是登记，内核不核对两者是否一致，应用自己保持同步。插件不同：进程外与 Wasm 宿主按插件清单的 `contributes.permissions` 替插件登记，只登记清单声明了的，与已登记的码重复即冲突，见[隔离级别与清单](/plugins/overview)。

## 声明授权要求

授权要求有三个来源，Authorize 阶段先看调用上下文里显式设置的要求，没有时再从当前操作的契约推导，都没有则直接放行：

| 来源 | 写法 | 说明 |
| --- | --- | --- |
| 操作契约 | `[Operation(Id, Permission = "orders.write")]` 或 `Policy = "<name>"` | 两者只能写一个，同时写在契约构造时抛出；契约推导出的要求不带资源 |
| 普通端点 | `RequireXiHanAuthentication()`、`RequireXiHanPermission(code, resource?)`、`RequireXiHanPolicy(name, resource?)` | 挂在端点元数据上，`UseXiHan()` 构造调用上下文时放进去 |
| 中间件 | `context.RequireAuthorization(requirement)` | Authorize 阶段之前的中间件设置 |

::: warning 没有授权要求的操作对匿名调用开放
契约既没写 `Permission` 也没写 `Policy`、端点也没挂要求时，Authorize 阶段直接放行。除非 `XiHan:Security:RequireAuthentication` 为真，匿名调用也能执行它。
:::

## 最小示例

下面的特性改写自 `framework/samples/SimpleApp/Orders/OrdersFeature.cs`，只保留授权相关的部分：

```csharp
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;

public sealed class OrdersFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.samples.orders",
        Package = "SimpleApp",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(SecurityCapabilities.Authorization, SecurityCapabilities.Version),
        ],
        // 清单声明本包贡献的权限码，供清单的读者与工具使用
        Contributes = new Contributions { Permissions = ["orders.read", "orders.write"] },
        Entry = typeof(OrdersFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        // 运行时只认登记表里的权限码
        context.AddPermission(new PermissionDefinition("orders.read", "Read orders"));
        context.AddPermission(new PermissionDefinition("orders.write", "Place orders"));

        // 操作契约的登记代码由 Kernel.Generators 从 [Operation] 生成
        context.AddGeneratedOperations();
    }
}
```

SimpleApp 的 `CreateOrder` 在契约上写了 `Permission = "orders.write"`，`ListOrders` 写了 `Permission = "orders.read"`，处理器里不再判权，能走到 `HandleAsync` 就说明 Authorize 阶段已经判定调用方持有权限：

```csharp
[Operation(Id, Expose = Expose.Http | Expose.Grpc | Expose.Mcp, Permission = "orders.read")]
public sealed class ListOrders(IRepository<Order, Guid> orders) : IOperation<ListOrdersRequest, OrderList> { /* 见操作契约一章 */ }
```

普通端点上的写法：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<OrdersFeature>());

var app = builder.Build();
app.UseXiHan();

app.MapGet("/profile", () => "ok").RequireXiHanAuthentication();
app.MapGet("/orders/summary", () => "ok").RequireXiHanPermission("orders.read");
app.MapGet("/orders/export", () => "ok").RequireXiHanPolicy(TenantMemberPolicy.PolicyName);

app.Run();
```

`TenantMemberPolicy` 见下文「自定义策略」。授予来自 SimpleApp 的配置（角色 `editor` 持有 `orders.read,orders.write`，`viewer` 只有 `orders.read`，见 [RBAC 授予与条件](/security/grants)），运行后的结果：

| 调用 | 结果 |
| --- | --- |
| `alice`（角色 `editor`）调用 `orders.create` | 通过 |
| `bob`（角色 `viewer`）调用 `orders.create` | `SECURITY_DENIED_004`，HTTP 403 |
| 匿名调用 `orders.create` | `SECURITY_DENIED_001`，HTTP 401，`WWW-Authenticate: ApiKey` |
| 匿名调用 `/profile` | `SECURITY_DENIED_001`，HTTP 401 |

```bash
curl -i -X POST -H "Authorization: ApiKey dev-bob-key" -H "Content-Type: application/json" \
     -d '{"name":"Desk","amount":199}' http://localhost:5000/orders    # 403 SECURITY_DENIED_004
```

## Authorize 阶段

`AuthorizationMiddleware` 的处理顺序：

1. 取授权要求：宿主写入的全部要求（端点与路由组上的每条约定，以及 `RequireAuthorization` 追加的），再加上操作契约推导出的那一条（权限码优先于策略名），按顺序逐条判定，全部满足才放行；都没有则放行。端点或组上的约定叠加在契约的权限之上，不顶替它：在 `MapXiHanOperations()` 上加 `RequireXiHanAuthentication()`，每个操作仍要检查自己的权限。
2. 在 `PolicyCatalog` 里按名查找策略。先找登记的 `IPolicy`（策略名区分大小写）；没有时，名字形如 `permission:<code>` 且权限码已登记，就动态创建 `PermissionPolicy`。
3. 找不到策略返回 `SECURITY_NOTFOUND_001`。配置错误按拒绝处理，不会变成越权。按名引用的策略大多已在启动时核对（见下一节），走到这里的只剩启动时看不到的要求：应用代码以 `RequireAuthorization` 显式写入的、启动之后才加进来的端点，以及插件卸载时撤回、还没重新登记的权限码。
4. 调用策略。`IPolicy.EvaluateAsync` 返回 `Result<AuthorizationResult>`：成功是允许或拒绝；失败是无法判定（授予存储读不到等），调用以该失败结束，既不放行，也不改写成拒绝。拒绝时返回策略给出的原因；策略返回未初始化的结果（`default`）按拒绝处理，原因为 `SECURITY_DENIED_002`。

## 启动时核对策略引用

引用了不存在的策略是编程错误，与未登记的权限码一样在启动时报出来，不等到调用时 404：

| 引用 | 什么时候核对 | 失败 |
| --- | --- | --- |
| 操作契约的 `Permission` | 装配完成时（`AppBuilder.Build`），之后登记的操作（插件握手之后）在登记时 | `KERNEL_VALIDATION_005`：一次列出全部未登记的权限码，应用启动失败 |
| 操作契约的 `Policy` | 授权能力（`Security.InMemory` / `Security.Database`）登记的启动核对：容器建好之后、任何特性启动之前，经 `PolicyCatalog.EnforceOn` 一次核对已登记的全部操作；之后登记的操作在登记时逐个核对 | `SECURITY_VALIDATION_004`：宿主启动失败，不论失败策略（`IsolateFeature` 也一样）；插件整体登记失败 |
| 端点上的 `RequireXiHanPermission` / `RequireXiHanPolicy` / `RequireXiHanAuthentication` | `UseXiHan()` 在宿主建请求管道时（路由已全部映射） | `SECURITY_VALIDATION_004`：宿主启动失败（`app.StartAsync` / `app.Run` 抛出） |
| 有要求却没有判定者：契约声明了权限码或策略而 Authorize 阶段没有中间件，或端点挂了要求而没有装配授权能力 | 装配完成时；宿主建请求管道时；之后登记的操作在登记时 | `KERNEL_VALIDATION_006`：启动失败，插件登记失败（ADR-0217；以前要求静默失效） |

- 异常一次列出全部违规项，如 `'orders.approve' of 'xihan.app.orders' requires policy 'approvers'`。
- 权限码在装配完成时核对，所以可以由别的特性登记；策略名在容器建好之后才知道（`IPolicy` 是容器里的服务），所以在启动核对里核对。
- 启动核对不归内核的失败策略管（见[生命周期与健康](/startup/lifecycle)）：坏的策略名是装配错误，隔离授权特性修不好它，所以 `XiHan:Kernel:FailurePolicy = IsolateFeature` 时同样拒绝启动。没有装配授权能力时都不核对，此时 Authorize 阶段也不判定任何要求。
- 自己实现 `security.authorization@1` 的 Provider，被选中时要在 `Configure` 里同样登记启动核对：

```csharp
context.AddStartupCheck(static services => services.GetRequiredService<PolicyCatalog>().EnforceOn(services.GetRequiredService<OperationRegistry>()));
```

端点上的要求不会被静默跳过：

- 绕过内核管道的端点（健康探针、`MapXiHanOpenApi`、`MapXiHanProto`、自己用 `BypassXiHanPipeline` 标注的）不会判定要求，挂了 `RequireXiHan*` 宿主启动即失败（`SECURITY_VALIDATION_008`）。要保护这类端点，用 ASP.NET Core 自己的授权。`MapXiHanGrpc()` 例外：gRPC 处理器每次调用都带着端点上的要求走管道，组上的约定与契约的权限叠加判定。
- `UseXiHan()` 没调，或者排在 `UseRouting()` 之前读不到端点时，带 `RequireXiHan*` 的端点不执行、抛出异常，不会放行。

## 自定义策略

权限之外的规则写成 `IPolicy`。下面的策略要求主体的租户声明与本次调用解析出的租户一致，用来挡住没有租户声明的主体借其他解析器进入某个租户（见[租户解析与隔离](/multi-tenancy/resolution)）：

```csharp
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

public sealed class TenantMemberPolicy : IPolicy
{
    public const string PolicyName = "tenant-member";

    public string Name => PolicyName;

    public ValueTask<Result<AuthorizationResult>> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        var security = request.Security;
        if (!security.Principal.IsAuthenticated)
        {
            return ValueTask.FromResult(Result.Success(AuthorizationResult.Deny(Error.Denied(SecurityErrors.NotAuthenticated, SecurityMessages.AuthenticationRequired))));
        }

        // 声明类型与 XiHan:MultiTenancy:TenantClaim 保持一致（缺省 tenant）
        var member = security.Tenant is { } tenant && security.Principal.HasClaim("tenant", tenant.Id);
        return ValueTask.FromResult(Result.Success(member
            ? AuthorizationResult.Allow()
            : AuthorizationResult.Deny(Error.Denied(SecurityErrors.PolicyDenied, KernelMessages.DeniedByPolicy))));
    }
}
```

在特性里登记 `context.Services.AddSingleton<IPolicy, TenantMemberPolicy>()`，契约上写 `Policy = TenantMemberPolicy.PolicyName`，端点上写 `RequireXiHanPolicy(TenantMemberPolicy.PolicyName)`。

- 策略按名索引，名字重复在 `PolicyCatalog` 构造时抛出 `ArgumentException`。
- 策略是单例，判定时拿到的是 `AuthorizationRequest`：`Security`（主体与租户）、`Action`（策略名）、`Resource`（资源名），不是调用上下文。
- 拒绝原因用错误码加消息键（规范 RFC-0010），HTTP 状态码按错误码种类映射：`DENIED` 为 403（`SECURITY_DENIED_001` 与 `SECURITY_DENIED_003` 除外，为 401）。
- 判定依赖的数据读不到时返回失败（`Result` 的失败，不是 `Deny`），调用以该失败结束：`UNAVAILABLE` 种类为 503。

## 资源名

资源名 `ResourceName` 是 `xrn:<partition>:<service>:<tenant>:<type>/<id>`，用于策略判定与审计，例如 `xrn:cn:shop:acme:order/42`。分区、服务、类型段只允许小写字母、数字与连字符；租户段可以为空（全局资源），否则与租户 ID 同一文法：字母或数字开头，之后是字母、数字、`.`、`_`、`-`，不超过 64 个字符，区分大小写；ID 段不含 `/` 与空白。`ResourceName.Parse` 不合法时抛出 `ArgumentException`，`ResourceName.TryParse` 返回 `false`。

资源经 `RequireXiHanPermission(code, resource)`、`RequireXiHanPolicy(name, resource)` 或 `AuthorizationRequirement` 的构造参数进入请求，策略在 `request.Resource` 上读到它：

```csharp
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Security.Abstractions.Authorization;

public static class OrderRequirements
{
    public static AuthorizationRequirement ReadOrder(string tenantId, string orderId)
        => AuthorizationRequirement.Permission("orders.read", ResourceName.Parse($"xrn:cn:shop:{tenantId}:order/{orderId}"));
}
```

内置的 `PermissionPolicy` 只看权限码、不看资源；按资源判定要写自定义策略。操作契约推导出的要求不带资源。

## 错误码

| 错误码 | 含义 | HTTP |
| --- | --- | --- |
| `SECURITY_DENIED_001` | 未认证：权限策略或 `authenticated` 策略遇到匿名主体 | 401 |
| `SECURITY_DENIED_002` | 策略拒绝（策略返回未初始化的结果，或自定义策略以它为原因） | 403 |
| `SECURITY_DENIED_004` | 缺少权限，含被 `IPermissionFilter` 收窄 | 403 |
| `SECURITY_NOTFOUND_001` | 调用时才看到的要求引用的策略不存在，或其中 `permission:<code>` 的权限码未登记 | 404 |
| `SECURITY_VALIDATION_004` | 操作契约的 `Policy`、端点上的要求引用了不存在的策略或未登记的权限码：启动时抛出、启动失败；授权能力启动之后登记的操作在登记时返回，插件登记失败 | — |
| `SECURITY_UNAVAILABLE_001` | 授予存储、授予来源或收窄过滤器读不到数据，不当作不持有 | 503 |
| `KERNEL_SECURITY_001` / `KERNEL_SECURITY_002` | 权限码重复 / 权限登记表已满，装配时抛出 | — |
| `KERNEL_VALIDATION_005` | 操作契约引用了未登记的权限码：装配完成时抛出、启动失败；插件握手之后登记的操作在登记时返回，插件登记失败 | — |
| `KERNEL_VALIDATION_006` | 要求授权而没有判定者：契约声明了权限码或策略而 Authorize 阶段没有中间件，或端点挂了要求而没有装配授权能力：启动时抛出；之后登记的操作登记失败 | — |

## 常见问题

### 启动时报 `KERNEL_VALIDATION_005`，说权限码没登记

某个操作契约的 `Permission` 写了一个没有经 `AddPermission` 登记的码。异常一次列出全部，按清单逐个在对应特性的 `Configure` 里补登记，或改正拼写。权限码可以由别的特性登记，但要保证那个特性确实被装配了。

### 启动时报 `KERNEL_VALIDATION_006`，说没有中间件判定授权

操作契约声明了 `Permission` 或 `Policy`、或端点挂了 `RequireXiHan*`，而应用没有装配授权能力（`security.authorization@1`）。要求没人判定就等于没有，框架宁可启动失败。装配一个授权 Provider（`Security.InMemory` 或 `Security.Database`）；确实不需要授权的应用，去掉这些要求。测试里只想要契约上的权限、不想验授权时，在 Authorize 阶段挂一个放行的测试替身中间件。

### 403 和 401 怎么区分

`SECURITY_DENIED_001`（匿名）与 `SECURITY_DENIED_003`（凭据无效）是 401，客户端应该登录或刷新凭据；`SECURITY_DENIED_002` / `SECURITY_DENIED_004` 是 403，已登录但无权，再试也没用。权限策略对匿名主体先返回 `SECURITY_DENIED_001`，所以前端不用自己判断「是没登录还是没权限」。

### 怎样按资源（只能改自己的订单）判定

内置权限策略只看权限码。把资源名经 `RequireXiHanPermission(code, resource)` 或 `AuthorizationRequirement` 放进请求，写一个自定义 `IPolicy` 读 `request.Resource` 与 `request.Security.Principal` 判定；或者在处理器里按仓储查到的所有者判断后返回 `Error.Denied`。操作契约推导的要求不带资源，所以资源级判定通常在处理器或显式要求里做。授予存储读不到时为什么是 503 而不是 403，见 [RBAC 授予与条件](/security/grants)。

### OpenAPI 文档里能看到授权要求吗

能。HTTP 协议适配器以 `x-xihan-permission`、`x-xihan-policy` 发布每个操作的要求，`components.x-xihan-permissions` 列出登记表里的权限码与显示名，见[规范化接口文档](/web/openapi)。

## 相关包与下一步

- 包参考：[Security.Abstractions](/packages/security-abstractions)、[Security.InMemory](/packages/security-inmemory)、[Security.Database](/packages/security-database)
- 规范：RFC-0008《认证与授权》、RFC-0010《消息与本地化》
- 相关章节：[操作契约](/web/operations)、[端点里使用框架](/web/endpoints)、[规范化接口文档](/web/openapi)、[审计](/common/auditing)
- 下一步：[RBAC 授予与条件](/security/grants)
