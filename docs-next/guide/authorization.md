# 授权

本章讲一次调用怎样被判定能否执行：权限的定义与登记、操作契约与端点上的授权要求、Authorize 阶段、RBAC 授予与授予存储、策略目录、收窄过滤器与授予来源、条件引擎与 IP 归属地，适用于需要按角色、权限或属性控制访问的应用。完整 API 见 [Security.Abstractions](../packages/security-abstractions)，授予存储见 [Security.InMemory](../packages/security-inmemory) 与 [Security.Database](../packages/security-database)。

## 判定模型

一次调用最多带一个授权要求 `AuthorizationRequirement`：策略名加可选的资源名。Authorize 阶段的 `AuthorizationMiddleware` 按策略名在 `PolicyCatalog` 里找到 `IPolicy`，以 `AuthorizationRequest`（安全上下文、策略名、资源）调用它，得到允许或带原因的拒绝。权限是一种特殊的策略：策略名 `permission:<code>` 映射为 `PermissionPolicy`，由 `IPermissionChecker` 按 RBAC 授予判定。

授权是能力 `security.authorization@1`。它读 Authenticate 阶段写入的主体与 Tenant 阶段写入的租户，所以租户专属的授予在判权时已经生效。

| 包 | 清单角色 | 授予存在哪里 |
| --- | --- | --- |
| `XiHan.Framework.Security.InMemory` | `default` | 配置 `XiHan:Security:InMemory:Grants` 与进程内存；`Profiles.Standard` 已包含 |
| `XiHan.Framework.Security.Database` | `provider` | 数据库表（`PermissionGrantRecord`），经装配的持久化 Provider 读写 |

两个 Provider 登记的是同一套组件：`PermissionChecker`、`authenticated` 策略、`PolicyCatalog` 与 Authorize 阶段的 `AuthorizationMiddleware`，区别只在授予存储。

## 定义与登记权限

权限是 `PermissionDefinition`：权限码、显示名、可选说明。权限码小写、点分、至少两段，每段以小写字母开头，可含数字与连字符，如 `orders.read`。特性在 `Configure` 里用 `FeatureContext.AddPermission` 登记到内核的 `PermissionRegistry`；权限码重复（`KERNEL_SECURITY_001`）或登记表已满（容量 10000，`KERNEL_SECURITY_002`）在装配时抛出。

下面的特性改写自 `next/samples/SimpleApp/Orders/OrdersFeature.cs`，只保留授权相关的部分：

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
    }
}
```

清单的 `Contributes.Permissions` 是声明，`AddPermission` 是登记，内核不核对两者是否一致，应用自己保持同步。策略目录只为登记过的权限码创建权限策略；HTTP 协议适配器发布 OpenAPI 时，`components.x-xihan-permissions` 也取登记表里的显示名与说明。

## 声明授权要求

授权要求有三个来源，Authorize 阶段先看调用上下文里显式设置的要求，没有时再从当前操作的契约推导，都没有则直接放行：

| 来源 | 写法 | 说明 |
| --- | --- | --- |
| 操作契约 | `[Operation(Id, Permission = "orders.write")]` 或 `Policy = "<name>"` | 两者只能写一个，同时写在契约构造时抛出；契约推导出的要求不带资源 |
| 普通端点 | `RequireXiHanAuthentication()`、`RequireXiHanPermission(code, resource?)`、`RequireXiHanPolicy(name, resource?)` | 挂在端点元数据上，`UseXiHan()` 构造调用上下文时放进去 |
| 中间件 | `context.RequireAuthorization(requirement)` | Authorize 阶段之前的中间件设置 |

`next/samples/SimpleApp` 的 `CreateOrder` 在契约上写了 `Permission = "orders.write"`，`ListOrders` 写了 `Permission = "orders.read"`；同一份契约经 HTTP、gRPC、MCP 与命令行调用，判定都在 Authorize 阶段完成一次。端点上的写法：

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

`TenantMemberPolicy` 见[自定义策略](#自定义策略)。

::: warning 没有授权要求的操作对匿名调用开放
契约既没写 `Permission` 也没写 `Policy`、端点也没挂要求时，Authorize 阶段直接放行。除非 `XiHan:Security:RequireAuthentication` 为真，匿名调用也能执行它。
:::

## Authorize 阶段

`AuthorizationMiddleware` 的处理顺序：

1. 取授权要求：显式要求优先，其次是操作契约（权限码优先于策略名）；都没有则放行。
2. 在 `PolicyCatalog` 里按名查找策略。先找登记的 `IPolicy`（策略名区分大小写）；没有时，名字形如 `permission:<code>` 且权限码已登记，就动态创建 `PermissionPolicy`。
3. 找不到策略返回 `SECURITY_NOTFOUND_001`。配置错误按拒绝处理，不会变成越权。
4. 调用策略。拒绝时返回策略给出的原因；策略返回未初始化的结果（`default`）按拒绝处理，原因为 `SECURITY_DENIED_002`。

内置两种策略：

| 策略名 | 类型 | 判定 |
| --- | --- | --- |
| `authenticated` | `AuthenticatedPolicy` | 主体已认证；匿名返回 `SECURITY_DENIED_001` |
| `permission:<code>` | `PermissionPolicy` | 匿名先返回 `SECURITY_DENIED_001`，调用方据此区分未登录与越权；已认证但不持有权限返回 `SECURITY_DENIED_004` |

`AuthorizationRequirement.Authenticated` 与 `AuthorizationRequirement.Permission(code, resource?)` 是这两种要求的快捷写法，`PermissionPolicy.NameFor(code)` 给出权限码对应的策略名。

## RBAC 授予与检查

授予是 `PermissionGrant`：主体、权限码、可选租户。主体是 `GrantSubject`，种类只有用户（`GrantSubject.ForUser(id)`，即 `user:<id>`）与角色（`GrantSubject.ForRole(name)`，即 `role:<name>`）。租户为 `null` 的授予对任何租户生效，租户专属授予只对该租户生效；权限码写 `*`（`PermissionGrant.All`）匹配任何权限。主体 ID、角色名与权限码都区分大小写。

`PermissionChecker` 判定"主体是否持有权限"的顺序：

1. 匿名主体直接不持有，不访问存储。
2. 查用户本身的授予（全局授予加当前租户的授予）。
3. 逐个查主体的角色授予，角色取类型为 `XiHan:Security:RoleClaim`（缺省 `role`）的声明值。
4. 存储都没命中时，按登记顺序查询各 `IPermissionGrantSource`。
5. 任一步命中即算授予命中，之后按登记顺序调用全部 `IPermissionFilter`，全部放行才算持有。

内存实现从配置装载授予。配置里的授予都是全局的，值是逗号分隔的权限码；下面是 SimpleApp 的授予：

```json
{
  "XiHan": {
    "Security": {
      "InMemory": {
        "Grants": {
          "role": {
            "editor": "orders.read,orders.write",
            "viewer": "orders.read"
          }
        }
      }
    }
  }
}
```

以 SimpleApp 为例：`alice`（角色 `editor`）调用 `orders.create` 通过；`bob`（角色 `viewer`）调用 `orders.create` 得到 `SECURITY_DENIED_004`（HTTP 403）；匿名调用得到 `SECURITY_DENIED_001`（HTTP 401）。

| | `Security.InMemory` | `Security.Database` |
| --- | --- | --- |
| 装载 | 启动时读配置，运行时 `InMemoryPermissionGrantStore.Grant` / `Revoke` 增删，租户专属授予只能这样添加 | 数据库表，管理操作经 `PermissionGrantRecordStore` 批量授予、撤销 |
| 上限 | `XiHan:Security:InMemory:MaxGrants`，缺省 10000 | 无条数上限；缓存 `CacheSize` 缺省 10000 条 |
| 读取 | 进程内查表 | 按（主体，租户）一次查询全局行加该租户的行，结果缓存 `CacheTimeToLiveSeconds`（缺省 30 秒） |
| 撤销不存在的授予 | `Revoke` 返回 `false` | 逐项结果 `SECURITY_NOTFOUND_002` |

数据库授予是平台数据，不受当前租户过滤；`PermissionGrantRecordStore` 写入后立即失效涉及主体的缓存，其他实例的缓存靠存活时间收敛。授予存储读取时的基础设施异常照常传播，`PermissionChecker` 不捕获，调用以失败结束而不是放行。装配与示例见 [Security.Database](../packages/security-database)。

## 收窄：IPermissionFilter

`IPermissionFilter`（ADR-0039）在授予命中之后再判定一次，返回 `false` 即视为不持有。它只能拿走权限、不能给出权限，用于套餐白名单、显式拒绝、委托到期这类授予之外的约束。被收窄的权限在权限策略上同样返回 `SECURITY_DENIED_004`，调用方不需要区分"没授予"与"被收窄"。

```csharp
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Security.Abstractions.Permissions;

public sealed class EditionPermissionFilter : IPermissionFilter
{
    // 基础版套餐只开放查询；真实应用按租户从套餐表读取并缓存
    private static readonly HashSet<string> _basicEdition = new(StringComparer.Ordinal) { "orders.read" };

    public ValueTask<bool> AllowsAsync(SecurityContext security, string permission, CancellationToken cancellationToken)
        => ValueTask.FromResult(security.Tenant is null || _basicEdition.Contains(permission));
}
```

在特性的 `Configure` 里按单例登记：`context.Services.AddSingleton<IPermissionFilter, EditionPermissionFilter>()`。

- 检查器按登记顺序逐个调用，任一返回 `false` 即不持有，后面的不再调用。
- 匿名主体与授予未命中时不调用过滤器，过滤器里查库或查缓存的开销只花在命中的判定上。
- 过滤器收到的是具体权限码；主体持有 `*` 时同样会被收窄。
- 过滤器抛出的异常照常传播，不当作放行或拒绝。

## 授予来源：IPermissionGrantSource

`IPermissionGrantSource`（ADR-0045）与过滤器对称：给出主体在授予存储之外的额外授予（权限码或 `*`），用于角色继承、限时委托这类不落在主体自己授予行上的授予。来源只能加，给出的授予（包括 `*`）同样经过全部过滤器收窄。

```csharp
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;

public sealed class ManagerInheritsEditorGrantSource(SecurityOptions options) : IPermissionGrantSource
{
    // manager 角色继承 editor 的权限；继承关系通常来自应用的角色表
    private static readonly IReadOnlyCollection<string> _editorPermissions = ["orders.read", "orders.write"];

    public ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(SecurityContext security, CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyCollection<string>>(security.Principal.HasClaim(options.RoleClaim, "manager") ? _editorPermissions : []);
}
```

同样按单例登记：`context.Services.AddSingleton<IPermissionGrantSource, ManagerInheritsEditorGrantSource>()`。检查器只在用户与角色的存储授予都没命中时才查询来源，任一来源命中即止；匿名主体不查询。来源拿到整个安全上下文，继承关系或委托一改就生效，不依赖令牌刷新。

应用替换 `IPermissionChecker` 时，必须同样并上全部授予来源、并在授予命中之后施加全部过滤器（RFC-0008）。

## 自定义策略

权限之外的规则写成 `IPolicy`。下面的策略要求主体的租户声明与本次调用解析出的租户一致，用来挡住没有租户声明的主体借 Baggage 进入某个租户（见[多租户](./multi-tenancy#租户与主体声明)）：

```csharp
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

public sealed class TenantMemberPolicy : IPolicy
{
    public const string PolicyName = "tenant-member";

    public string Name => PolicyName;

    public ValueTask<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        var security = request.Security;
        if (!security.Principal.IsAuthenticated)
        {
            return ValueTask.FromResult(AuthorizationResult.Deny(Error.Denied(SecurityErrors.NotAuthenticated, SecurityMessages.AuthenticationRequired)));
        }

        // 声明类型与 XiHan:MultiTenancy:TenantClaim 保持一致（缺省 tenant）
        var member = security.Tenant is { } tenant && security.Principal.HasClaim("tenant", tenant.Id);
        return ValueTask.FromResult(member
            ? AuthorizationResult.Allow()
            : AuthorizationResult.Deny(Error.Denied(SecurityErrors.PolicyDenied, KernelMessages.DeniedByPolicy)));
    }
}
```

在特性里登记 `context.Services.AddSingleton<IPolicy, TenantMemberPolicy>()`，契约上写 `Policy = TenantMemberPolicy.PolicyName`，端点上写 `RequireXiHanPolicy(TenantMemberPolicy.PolicyName)`。

- 策略按名索引，名字重复在 `PolicyCatalog` 构造时抛出 `ArgumentException`。
- 策略是单例，判定时拿到的是 `AuthorizationRequest`：`Security`（主体与租户）、`Action`（策略名）、`Resource`（资源名），不是调用上下文。
- 拒绝原因用错误码加消息键（RFC-0010），HTTP 状态码按错误码种类映射：`DENIED` 为 403（`SECURITY_DENIED_001` 与 `SECURITY_DENIED_003` 除外，为 401）。

## 资源名

资源名 `ResourceName` 是 `xrn:<partition>:<service>:<tenant>:<type>/<id>`，用于策略判定与审计，例如 `xrn:cn:shop:acme:order/42`。分区、服务、类型段只允许小写字母、数字与连字符；租户段同样如此且可以为空（全局资源）；ID 段不含 `/` 与空白。`ResourceName.Parse` 不合法时抛出 `ArgumentException`，`ResourceName.TryParse` 返回 `false`。

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

内置的 `PermissionPolicy` 只看权限码、不看资源；按资源判定要写自定义策略。操作契约推导出的要求不带资源。租户 ID 的文法允许大写字母与 `.`、`_`，这样的租户 ID 放不进资源名的租户段，`Parse` 会拒绝。

## 条件引擎

条件（ADR-0046）是访问策略、权限条件与约束规则共用的属性判定，引擎在 `XiHan.Framework.Security.Abstractions.Conditions`，与存储无关：应用提供属性来源与条件的存储，引擎负责登记、编译、求值。框架没有把条件接进 Authorize 阶段，应用在自己的策略、过滤器或中间件里调用编译器与求值器。

条件集 `ConditionSet` 由组 `ConditionGroup` 构成，组由子句 `ConditionClause`（属性名、运算符、条件值原文、是否取反、标识）构成；组内是"且"，组间是"或"。没有非空组的条件集是"无条件"，求值恒为命中，需要拒绝空条件的场景在保存时拦下。

运算符与值种类的整数值是持久化契约，不会调整：

| 运算符 | 值 | 可用于 |
| --- | --- | --- |
| `Equals`、`NotEquals` | 0、1 | 任何属性 |
| `GreaterThan`、`GreaterThanOrEquals`、`LessThan`、`LessThanOrEquals` | 2、3、4、5 | 数字、日期时间、日期、时刻 |
| `Contains`、`NotContains` | 6、7 | 多值属性与字符串（单值字符串按子串） |
| `In`、`NotIn` | 8、9 | 任何属性 |
| `Between` | 10 | 数字、日期时间、日期、时刻 |
| `StartsWith`、`EndsWith` | 11、12 | 字符串 |
| `IsNull`、`IsNotNull` | 13、14 | 任何属性 |

| 值种类 | 值 | 比较与写法 |
| --- | --- | --- |
| `String` | 0 | 不区分大小写的序数比较 |
| `Number` | 1 | 十进制 |
| `Boolean` | 2 | `true` / `false` / `1` / `0` |
| `DateTime` | 3 | ISO 8601；不带偏移的按编译时区的墙钟解释 |
| `Date` | 4 | 日期 |
| `TimeOfDay` | 5 | `HH:mm[:ss]`；区间下限大于上限表示跨零点 |
| `IpAddress` | 6 | IPv4 只收四段点分，IPv4 映射的 IPv6 还原为 IPv4；集合可写 CIDR，主机位必须为零 |

集合写 JSON 数组，或用逗号、全角逗号、换行分隔；区间写 `下限,上限`、`[下限,上限]` 或 `{"min":下限,"max":上限}`。`ConditionOperatorRules.AllowedFor(descriptor)` 给出某个属性能用的运算符，条件编辑界面据此列出选项。

属性用 `services.AddConditionAttributes(...)` 登记，它同时确保 `IConditionAttributeRegistry`、`IConditionCompiler`、`IConditionEvaluator` 三项单例已登记，各特性可以各自调用。登记名不区分大小写，可带别名；以 `.*` 结尾的是一族属性（如 `resource.route.*`），按最长前缀匹配，精确登记优先；重名与别名冲突在登记表首次解析时抛出。

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Security.Abstractions.Conditions;

public static class OfficeHoursCondition
{
    public static void Register(IServiceCollection services)
        => services.AddConditionAttributes(
            new ConditionAttributeDescriptor("environment.ip", ConditionValueKind.IpAddress),
            new ConditionAttributeDescriptor("environment.time", ConditionValueKind.TimeOfDay),
            new ConditionAttributeDescriptor("subject.roleCodes", ConditionValueKind.String, IsMultiValued: true));

    // 内网或 auditor 角色，且在 09:00–18:00 之间：组内为且，组间为或
    public static readonly ConditionSet Conditions = new(
    [
        new ConditionGroup([
            new ConditionClause("environment.ip", ConditionOperator.In, "10.0.0.0/8, 192.168.0.0/16"),
            new ConditionClause("environment.time", ConditionOperator.Between, "09:00,18:00"),
        ]),
        new ConditionGroup([
            new ConditionClause("subject.roleCodes", ConditionOperator.Contains, "auditor"),
            new ConditionClause("environment.time", ConditionOperator.Between, "09:00,18:00"),
        ]),
    ]);

    public static async ValueTask<bool> TryAsync(IConditionCompiler compiler, IConditionEvaluator evaluator, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var compiled = compiler.Compile(Conditions, new ConditionCompileOptions { TimeZone = TimeZoneInfo.Local });
        if (compiled.Errors.Count > 0)
        {
            // 保存前据编译错误拒绝；每条错误的 Error 带 SECURITY_VALIDATION_002 与说明原因的消息键
            return false;
        }

        var result = await evaluator.EvaluateAsync(compiled, new DictionaryConditionAttributeSource(values), explain: false, cancellationToken);
        return result.IsMatched;
    }
}
```

`DictionaryConditionAttributeSource` 用于试算与测试；运行时应用实现 `IConditionAttributeSource`，按规范名返回 `ConditionAttributeValue.Of(value)`、`ConditionAttributeValue.Missing`（取不到）或 `ConditionAttributeValue.Deferred`（暂不可得），`MemoizingConditionAttributeSource` 让同一次求值里每个属性只取一次。求值只向来源取被引用的属性（`CompiledConditionSet.ReferencedAttributes`）。

求值结果：

| 结果 | 子句 | 组 | 条件集 |
| --- | --- | --- | --- |
| `Matched` 命中 | 判定成立 | 全部子句命中 | 任一组命中 |
| `NotMatched` 不命中 | 判定不成立 | 任一子句不命中或无法判定 | 没有组命中也没有组待定 |
| `Indeterminate` 无法判定 | 属性取不到（`SECURITY_NOTFOUND_003`）或值不是登记的种类（`SECURITY_VALIDATION_003`）；按不命中计，取反不翻转 | — | — |
| `Pending` 待定 | 属性暂不可得（`SECURITY_UNAVAILABLE_004`） | 没有不命中、有待定 | 没有组命中、有组待定 |

`IsNull` / `IsNotNull` 把取不到、空列表、全是空白的字符串当空，可以取反。多值属性按任一元素判定，否定运算符按"没有元素满足"判定。编译出错的子句保留在编译结果里、求值恒为无法判定，库里的脏数据不会让整个条件集失效。`explain: true` 时逐组逐子句给出结果、取到的属性值与原因，用于条件编辑界面的试算。

## IP 归属地

IP 归属地（ADR-0047）是能力 `security.ip-region@1`：`IIpRegionResolver.Resolve(IPAddress)` 返回 `Maybe<IpRegion>`，含国家、省份、城市、运营商与两位国家代码，各段可能缺失，`Location` 是展示文本。条件属性 `environment.geo.*`、登录日志与命中记录的归属地列用它。

宿主可以不装。容器里没有 `IIpRegionResolver` 时，调用方把归属地当作取不到，引用它的条件子句为无法判定，`IsNull` / `IsNotNull` 照常判定。参考实现是离线库 Provider [Security.Ip2Region](../packages/security-ip2region)，不访问网络；回环、私有网段、保留地址与查不到的地址都返回无值，不抛出。

```csharp
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Security.Abstractions.Network;

public static class GeoAttributes
{
    // 给条件属性来源用：没装归属地 Provider 或查不到时返回 null，属性按取不到处理
    public static string? CountryCode(IServiceProvider services, IPAddress address)
        => services.GetService<IIpRegionResolver>() is { } resolver && resolver.Resolve(address).TryGetValue(out var region)
            ? region.CountryCode
            : null;
}
```

## 错误码

| 错误码 | 含义 | HTTP |
| --- | --- | --- |
| `SECURITY_DENIED_001` | 未认证：权限策略或 `authenticated` 策略遇到匿名主体 | 401 |
| `SECURITY_DENIED_002` | 策略拒绝（策略返回未初始化的结果，或自定义策略以它为原因） | 403 |
| `SECURITY_DENIED_004` | 缺少权限，含被 `IPermissionFilter` 收窄 | 403 |
| `SECURITY_NOTFOUND_001` | 策略不存在，或 `permission:<code>` 的权限码未登记 | 404 |
| `SECURITY_NOTFOUND_002` | 要撤销的授予不存在（`Security.Database` 的逐项结果） | — |
| `SECURITY_UNAVAILABLE_002` | 内存授予存储容量已满（`Grant` 与健康项 `security.inmemory.capacity`） | — |
| `SECURITY_VALIDATION_002` | 条件无效：属性未登记、运算符与值种类不搭配、条件值解析不了 | — |
| `SECURITY_VALIDATION_003` | 条件属性的值不是登记的值种类 | — |
| `SECURITY_NOTFOUND_003` | 取不到条件属性的值 | — |
| `SECURITY_UNAVAILABLE_004` | 条件属性暂不可得，补齐后再求值 | — |
| `KERNEL_SECURITY_001` / `KERNEL_SECURITY_002` | 权限码重复 / 权限登记表已满，装配时抛出 | — |

条件相关的四个错误码出现在编译结果与说明里，不直接成为调用结果。

## 与其他能力的关系

- [认证](./authentication)：策略读认证写入的主体，角色来自 `RoleClaim` 声明。
- [多租户](./multi-tenancy)：检查器按安全上下文里的租户取租户专属授予。
- [操作契约](./operations)：契约的 `Permission` / `Policy` 是授权要求的主要来源。
- [HTTP 与 OpenAPI](./http)：OpenAPI 文档以 `x-xihan-permission`、`x-xihan-policy` 与 `components.x-xihan-permissions` 发布授权要求。
- [审计](./auditing)：Full 组合包的审计中间件缺省只记声明了权限的操作。

## 相关

- [Security.Abstractions](../packages/security-abstractions)
- [Security.InMemory](../packages/security-inmemory)
- [Security.Database](../packages/security-database)
- [Security.Ip2Region](../packages/security-ip2region)
- 规范：RFC-0008《认证与授权》；ADR-0027、ADR-0039、ADR-0045、ADR-0046、ADR-0047
