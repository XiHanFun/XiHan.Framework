# 授权

认证解决「你是谁」，授权解决「你能做什么」。这一章讲怎么用一个权限码把接口保护起来、权限判定实际走了哪条链路、以及为什么真实项目**几乎总要**换掉框架的默认判定器。

完整 API 清单见 [Authorization 包](../packages/authorization)。

## 和认证的分工

两件事分属两个包、在管道里也是两段，中间还夹着租户解析与会话闸门：

```text
UseAuthentication → 租户解析 → 会话闸门 → UseAuthorization → 端点
   你是谁              哪个租户    会话还有效吗   你能做什么
```

| 关注点 | 认证 | 授权 |
| --- | --- | --- |
| 回答的问题 | 你是谁 | 你能做什么 |
| 输入 | 令牌 / Cookie | 已解析的 `ClaimsPrincipal` + 权限数据 |
| 产物 | `ClaimsPrincipal` 上的身份声明 | 通过 / 不通过 |
| 失败状态码 | `401`（未认证） | `403`（已认证但权限不足） |
| 主要扩展点 | `IJwtTokenService` 等 | `IPermissionChecker` |

::: tip 身份放令牌，权限别放令牌
令牌里只放身份信息（用户、会话、租户、角色），权限一律服务端实时判定。框架的授权处理器就是这么做的——它不读令牌里的权限声明，每次都问 `IPermissionChecker`，所以授权/回收、账号禁用、会话注销都能立刻生效，不用等令牌过期。
:::

## 安装与启用

```bash
dotnet add package XiHan.Framework.Authorization
```

框架里没有任何模块自动依赖它，必须在你的模块上显式声明：

```csharp
[DependsOn(
    typeof(XiHanAuthorizationModule),
    typeof(XiHanWebApiModule)
)]
public class MyAppModule : XiHanModule
{
}
```

`XiHanAuthorizationModule` 依赖 `XiHanAuthenticationModule`，`ConfigureServices` 里调 `AddXiHanAuthorization(config)`，用 `TryAdd` 把整套判定件注册进容器：存储（`IPermissionStore` / `IRoleStore` / `IPolicyStore`）、判定（`IPermissionChecker` / `IPolicyEvaluator` / `IAbacAttributeCollector` / `IAbacEvaluator`）、门面（`IAuthorizationService`），以及 ASP.NET Core 集成件 `HybridPermissionPolicyProvider` 与 `HybridPermissionAuthorizationHandler`。

`UseAuthorization()` 由 `XiHanWebApiModule` 编排在管道里，不需要你自己调。

::: warning 漏掉 DependsOn 的表现很不直观
没依赖这个模块时，`HybridPermissionPolicyProvider` 不会注册，`[PermissionAuthorize]` 生成的策略名在默认提供器里找不到，请求期直接抛 `InvalidOperationException`（提示策略 `xihan.hybrid:p=...` 未找到），不是 403。
:::

## 用特性保护接口

`PermissionAuthorizeAttribute` 继承自 ASP.NET Core 的 `AuthorizeAttribute`，可以标在类或方法上，`AllowMultiple = true`、`Inherited = true`。动态 API 把应用服务本身当作 Controller，所以直接标在应用服务方法上即可：

```csharp
public class InvoiceAppService : ApplicationServiceBase, IInvoiceAppService
{
    [PermissionAuthorize(InvoicePermissionCodes.Read)]
    public Task<InvoiceDto> GetAsync(long id) { … }

    [PermissionAuthorize(InvoicePermissionCodes.Create)]
    public Task<InvoiceDto> CreateAsync(InvoiceCreateDto input) { … }
}
```

多个授权特性叠加是 **AND**：类上标一个、方法上再标一个，两个都得通过。

```csharp
// 类上打底：进这个服务先要有读权限
[PermissionAuthorize(InvoicePermissionCodes.Read)]
public class InvoiceAppService : ApplicationServiceBase
{
    // 这个方法额外要求导出权限：Read + Export 都要有
    [PermissionAuthorize(InvoicePermissionCodes.Export)]
    public Task<FileDto> ExportAsync(InvoiceQueryDto input) { … }
}
```

## 权限码约定

框架**不解析权限码的结构**——`DefaultPermissionChecker` 是按名称做序数比较（区分大小写），层级、通配、继承都不是框架行为。所以格式由你定，只要全项目一致即可。推荐 `资源:操作`，跨模块可能重名时加模块前缀：

| 形态 | 例子 | 适用 |
| --- | --- | --- |
| `资源:操作` | `invoice:create`、`code_gen:read` | 资源名在全局唯一时 |
| `模块:资源:操作` | `saas:tenant:create` | 同名资源分属多个模块时 |

::: tip 权限码必须有单一事实源
把权限码定义成常量集中在一个类里，代码里一律引用常量：

```csharp
public static class InvoicePermissionCodes
{
    public const string Resource = "invoice";

    public const string Read = "invoice:read";
    public const string Create = "invoice:create";
    public const string Update = "invoice:update";
    public const string Delete = "invoice:delete";
    public const string Export = "invoice:export";
}
```

内联字符串拼错不会编译报错，只会在运行时静默鉴权失败——排查成本极高。
:::

超级管理员的通配 `*` **不是框架能力**：`DefaultPermissionChecker` 里没有任何通配处理。要支持它，就在你自己的 `IPermissionChecker` 实现里判定（例如权限集合里含 `*` 即放行）。

## 在代码里手动判定

特性覆盖不到的场景（判定结果要影响返回内容、要按数据行判定、要在领域服务里判定）直接注入 `IPermissionChecker`：

```csharp
public class InvoiceExporter
{
    private readonly IPermissionChecker _permissionChecker;

    public InvoiceExporter(IPermissionChecker permissionChecker)
    {
        _permissionChecker = permissionChecker;
    }

    public async Task<ExportResult> ExportAsync(string userId, CancellationToken ct)
    {
        // 单个
        var canExport = await _permissionChecker.IsGrantedAsync(userId, InvoicePermissionCodes.Export, ct);

        // 任一：审计员或财务都能看金额列
        var canSeeAmount = await _permissionChecker.IsAnyGrantedAsync(
            userId,
            [InvoicePermissionCodes.Read, "audit:read"],
            ct);

        // 全部
        var canArchive = await _permissionChecker.IsAllGrantedAsync(
            userId,
            [InvoicePermissionCodes.Read, InvoicePermissionCodes.Delete],
            ct);

        // 该用户全部权限码（做前端菜单/按钮级控制时用）
        var all = await _permissionChecker.GetGrantedPermissionsAsync(userId, ct);
        …
    }
}
```

契约共五个方法，判定用户的四个都按 `userId` 字符串传入，不依赖当前请求上下文；`PermissionExistsAsync` 不接收 `userId`：

| 方法 | 语义 |
| --- | --- |
| `IsGrantedAsync(userId, permissionName, ct)` | 单个权限码 |
| `IsAnyGrantedAsync(userId, names, ct)` | 任一命中即通过 |
| `IsAllGrantedAsync(userId, names, ct)` | 全部命中才通过 |
| `GetGrantedPermissionsAsync(userId, ct)` | 该用户的全部权限码 |
| `PermissionExistsAsync(permissionName, ct)` | 权限定义是否存在（判定定义，不判定授予） |

::: warning 空列表一律返回 false
`IsAnyGrantedAsync` 和 `IsAllGrantedAsync` 传入空列表都返回 `false`（fail-closed）。别把「没配权限要求」写成传空列表，那是拒绝，不是放行。
:::

想要带失败原因的结果对象（`Succeeded` / `FailureReason` / `FailedRequirements`）而不是裸 `bool`，用门面 `IAuthorizationService`，它内部就是转调 `IPermissionChecker`：

```csharp
var result = await _authorizationService.AuthorizeAsync(userId, InvoicePermissionCodes.Export);
if (!result.Succeeded)
{
    _logger.LogWarning("拒绝导出：{Reason}", result.FailureReason);
}
```

## 换成自己的判定器

框架默认的 `DefaultPermissionChecker` 读 `IPermissionStore` / `IRoleStore`，而这两个默认实现是**纯内存**的（`ConcurrentDictionary`）：不持久化、不预置任何数据。而且它们注册为 **Scoped**，字典是实例字段，随请求作用域新建——这一个请求里写进去的授权，下一个请求就读不到了。也就是说，**不换实现的话，对真实用户的所有判定都返回 false**。它的定位是参考实现与开发期占位。

生产做法是自己实现 `IPermissionChecker`，读一份按用户缓存的授权快照：

```csharp
public sealed class SnapshotPermissionChecker : IPermissionChecker
{
    private const string Wildcard = "*";

    private readonly IAuthorizationSnapshotService _snapshots;

    public SnapshotPermissionChecker(IAuthorizationSnapshotService snapshots)
    {
        _snapshots = snapshots;
    }

    public async Task<bool> IsGrantedAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return false;
        }

        var permissions = await _snapshots.GetPermissionsAsync(userId, cancellationToken);
        return permissions.Contains(Wildcard, StringComparer.OrdinalIgnoreCase)
            || permissions.Contains(permissionName.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    // IsAnyGrantedAsync / IsAllGrantedAsync / GetGrantedPermissionsAsync / PermissionExistsAsync 同理
    …
}
```

注册**必须用 `Replace`**：

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

public class MyAppModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(
            ServiceDescriptor.Scoped<IPermissionChecker, SnapshotPermissionChecker>());
    }
}
```

::: danger 用 TryAdd 会被静默忽略
`AddXiHanAuthorization` 已经用 `TryAddScoped` 占了位。你再 `TryAdd` 一次不会报错、不会告警，鉴权照旧走默认实现——线上表现就是「权限配了但一直 403」或「换了实现毫无变化」。同理适用于 `IPermissionStore` / `IRoleStore` / `IPolicyStore` / `IAbacEvaluator`。
:::

::: tip 判定器是请求期热路径
默认注册是 Scoped。一次请求里可能触发多次判定，实现里做请求级记忆化（字段缓存本次请求已构建的快照），避免同一请求反复查库。
:::

如果只想换数据来源、保留框架的「用户直授 + 角色继承」合并逻辑，那就不动 `IPermissionChecker`，改为 `Replace` 掉 `IPermissionStore` 和 `IRoleStore`：`DefaultPermissionChecker.IsGrantedAsync` 会先查用户直接权限，再遍历用户的启用角色查角色权限，任一命中且 `IsEnabled` 即通过。

## 关键机制

### 混合策略名

`[PermissionAuthorize]` 在构造时就把「权限码 + ABAC 策略码」编码进一个 ASP.NET Core 策略名：

```text
xihan.hybrid:p={URL编码的权限码};a={URL编码的ABAC策略码}
```

`HybridPermissionPolicyProvider` 拦截 `xihan.hybrid:` 前缀的策略名，解析出两个编码后**动态构建**一条含 `HybridPermissionRequirement` 的策略；其余策略名回落到 `DefaultAuthorizationPolicyProvider`。

这带来一个实用后果：**任何权限码都不需要预先 `AddPolicy` 注册**，写上特性就能用。代价是策略名里出现了 `;`、`=` 等字符（含这些字符的权限码会被 `Uri.EscapeDataString` 转义），所以别手写策略名，用特性。

### 判定顺序

`HybridPermissionAuthorizationHandler` 的执行顺序是固定的：

1. 从主体声明取 userId：取声明集合里**第一条**类型命中 `NameIdentifier` / `sub` / `userid` / `user_id`（大小写不敏感）的声明。取不到就直接不通过。
2. 若有权限码，调 `IPermissionChecker.IsGrantedAsync` 实时判定；不通过则终止（不再评估 ABAC）。
3. 若没有 ABAC 策略码，到此通过。
4. 若有 ABAC 策略码，`IAbacAttributeCollector` 收集主体 / 资源 / 环境属性，交 `IAbacEvaluator` 评估，允许才通过。

全程 fail-closed：任何一步拿不到结论都是拒绝。传给判定器的 `userId` 是声明里的**原始字符串**，实现里自己转成业务的 ID 类型。

### 叠加属性约束（ABAC）

权限码只能回答「能不能做这类事」，回答不了「能不能对**这一条**做」。`PermissionAuthorizeAttribute` 的第二个参数接一个 ABAC 策略码，在权限码之上再加一层属性约束：

```csharp
// 有读权限，且资源与当前用户同租户
[PermissionAuthorize(InvoicePermissionCodes.Read, "same_tenant")]
public Task<InvoiceDto> GetAsync(long id) { … }

// 只判属性、不判权限码，用 AbacAuthorize
// 参数名就是属性键，必须写成 user_id（见下方属性键说明）
[AbacAuthorize("self_only")]
public Task<ProfileDto> GetMyProfileAsync(long user_id) { … }
```

`DefaultAbacEvaluator` 内置的常用策略码（大小写不敏感）：

| 策略码 | 判定 |
| --- | --- |
| `allow` | 无条件允许 |
| `same_tenant` / `tenant_match` | 主体 `tenant_id` 与资源的 `tenant_id` 等任一来源相等 |
| `self_only` / `owner_match` | 主体 `user_id` 与资源的 `user_id` / `owner_user_id` 等任一来源相等 |
| 比较表达式 | 如 `subject.tenant_id == resource.tenant_id`，支持 `==` / `!=` / `=` |
| 其它 | 拒绝（不支持的策略码） |

属性从哪来：主体属性来自声明；资源属性来自授权 `resource` 对象的公共属性，若能从中解析出 `HttpContext` 还会带上 `route.*` / `query.*`；环境属性含权限码、策略码、UTC 时间、请求路径等。完整属性键与策略词汇见[包文档](../packages/authorization#默认-abac-策略词汇)。

::: warning ABAC 拿不到资源属性就等于拒绝
`same_tenant` / `self_only` 依赖资源侧的属性。如果既没有可解析的 `HttpContext`（路由/查询参数里没有 `tenant_id`、`user_id`），又没有把资源对象传进授权流程，属性字典里就没有对应键，判定结果是拒绝。要么把标识放进路由（`/invoices/{tenant_id}/...`），要么实现 `IAbacAttributeCollector` 补齐领域属性。

属性键只做 `ToLowerInvariant()` 归一化，不做驼峰转下划线：路由参数写成 `{tenantId}` 得到的键是 `route.tenantid`，与默认评估器要找的 `route.tenant_id` 对不上，判定照样拒绝。走默认策略词汇时，路由/查询参数就得直接命名为 `tenant_id`、`user_id`。
:::

需要更复杂的规则（时间窗、IP 段、组织树、外部策略引擎）时，实现 `IAbacEvaluator` 并 `Replace` 掉默认实现，特性侧的写法完全不变。

### Policy 子系统

除了「权限码 + ABAC」这条主线，包里还有一套独立的 Policy 判定：`PolicyDefinition` 可以组合所需角色（任一命中）、所需权限（全部命中）、所需声明（键值匹配）和自定义要求，由 `IPolicyEvaluator` 评估，入口是 `IAuthorizationService.AuthorizePolicyAsync(userId, policyName, resource)`。策略定义放在 `IPolicyStore` 里，适合把「一组判定条件」做成可配置的命名策略。

::: warning 两个同名的 IAuthorizationRequirement
`XiHan.Framework.Authorization.Policies.IAuthorizationRequirement` 是 Policy 子系统的自定义要求接口（有 `Name` 和 `EvaluateAsync`），和 ASP.NET Core 的标记接口 `Microsoft.AspNetCore.Authorization.IAuthorizationRequirement` 同名不同物。写自定义要求时看清 using。
:::

自定义要求里读声明要注意：`DefaultPolicyEvaluator` 的声明来自 `ICurrentUser`，也就是**当前请求的主体**，不是参数里那个 `userId` 的声明。给别人的 userId 做离线判定时，声明类要求会判失败。

## 配置

这个包没有配置节。`AddXiHanAuthorization` 虽然接收 `IConfiguration` 参数，但注册过程不读任何配置键——所有行为差异都通过替换实现来表达，不通过配置开关。

需要留意的只有两处环境前提：

| 前提 | 说明 |
| --- | --- |
| 模块依赖 | 你的模块必须 `[DependsOn(typeof(XiHanAuthorizationModule))]` |
| 管道顺序 | `UseAuthorization()` 由 `XiHanWebApiModule` 编排在认证、租户解析、会话闸门之后，自己插中间件时别插到它后面还指望能读到授权结果 |

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 换了 `IPermissionChecker` 毫无变化 | 用了 `TryAdd`，框架已占位；必须用 `Replace` |
| 所有权限判定恒为 `false` | 没换实现，默认内存存储不预置任何数据 |
| 权限码明明配了还是 403 | 默认判定器按序数比较、区分大小写；权限码常量与种子数据里的字符串必须逐字一致 |
| 超管也被拦 | 通配 `*` 不是框架行为，要在自己的 `IPermissionChecker` 实现里判 |
| 抛 `InvalidOperationException` 说策略未找到 | 模块没 `DependsOn(typeof(XiHanAuthorizationModule))`，策略提供器未注册 |
| 该 403 却收到 401 | 请求根本没通过认证；授权只在认证成功后才有 403 可言 |
| 授权改了要重新登录才生效 | 判定器读的是登录时冻结的数据；改成读可失效的授权快照 |
| 传空列表却被拒绝 | `IsAnyGrantedAsync` / `IsAllGrantedAsync` 对空列表返回 `false` |
| `same_tenant` 恒拒绝 | 资源属性里没有 `tenant_id`：没传资源对象，路由/查询里也没有该参数 |
| 类和方法上各标一个特性后接口进不去 | 多个授权特性是 AND 关系，两个权限码都要有 |

## 下一步

- [认证](./authentication)：身份、令牌与会话闸门
- [多租户](./multi-tenancy)：租户上下文，ABAC 的 `same_tenant` 依赖它
- [Web 应用开发](./web)：中间件管道全貌
- [扩展与二次开发](./extending)：替换框架默认实现的通用套路
- [Authorization 包](../packages/authorization)：完整 API 与配置清单
- [BasicApp 权限模型](https://basicapp.docs.xihanfun.com/backend/permission)：权限码、数据范围与字段脱敏的应用层落地
