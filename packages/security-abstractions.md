来源：https://framework.docs.xihanfun.com/packages/security-abstractions

# XiHan.Framework.Security.Abstractions

安全的抽象包：凭据与认证处理器、授权要求与策略目录、RBAC 授予存储与检查器、收窄过滤器与授予来源、Authenticate / Authorize 阶段中间件，以及条件引擎与 IP 归属地契约。能力 `security.authentication@1`、`security.authorization@1`、`security.ip-region@1` 由安全 Provider 提供。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Security.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `security.authentication@1`、`security.authorization@1`、`security.ip-region@1`（`SecurityCapabilities`），本包不提供 |
| 特性入口 | — |
| 配置节 | `XiHan:Security`（`SecurityOptions`，由 Provider 读取） |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Stable；`Conditions` 与 `Network` 命名空间为 Preview |

## 概述

主体、租户、授权请求、策略接口与权限登记这些原语在内核的 `XiHan.Framework.Kernel.Security` 命名空间（`Principal`、`SecurityContext`、`TenantContext`、`AuthorizationRequest`、`AuthorizationResult`、`IPolicy`、`PermissionRegistry`、`PermissionDefinition`、`ResourceName`），本包在其上定义认证与授权的组件。

认证：宿主把协议凭据放进调用上下文，`AuthenticationMiddleware` 交给方案匹配的 `IAuthenticationHandler`，成功写入主体，失败以结果返回。授权：调用带 `AuthorizationRequirement` 时，`AuthorizationMiddleware` 按名在 `PolicyCatalog` 取策略判定；`permission:<code>` 在权限码已登记时映射为 `PermissionPolicy`，由 `PermissionChecker` 按用户与角色授予之并集判定，再经授予来源补充、经过滤器收窄。认证与授权失败都以 `Result` 返回，宿主翻译成 401 / 403；处理器内部的基础设施故障抛异常。

条件引擎（`Conditions`）与 IP 归属地契约（`Network`）不接入任何管道阶段，应用在自己的策略、过滤器或中间件里使用。

## 何时使用

- 写自己的认证处理器、授权策略、授予存储、收窄过滤器或授予来源时引用本包。
- 使用条件引擎或 IP 归属地契约时引用本包。
- 只是启用认证与授权时直接装 [Security.InMemory](./security-inmemory)、[Security.Jwt](./security-jwt)、[Security.OpenIdConnect](./security-openidconnect) 或 [Security.Database](./security-database)，它们会带上本包。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Security.Abstractions --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。本包没有特性入口，`Profiles.Standard` 与 `Profiles.Full` 已经引用它。

## 配置

`SecurityOptions.FromConfiguration` 读取 `XiHan:Security` 下的两个键：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `RequireAuthentication` | `bool` | `false` | 为真时匿名调用在 Authenticate 阶段被拒绝（`SECURITY_DENIED_001`） |
| `RoleClaim` | `string` | `role` | 承载角色的声明类型，不能为空 |

```json
{
  "XiHan": {
    "Security": { "RequireAuthentication": false, "RoleClaim": "role" }
  }
}
```

多个安全 Provider 同时装配时，`SecurityOptions` 只注册一次。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `Authentication.AuthenticationCredential` | 凭据：`Scheme`（不区分大小写）与 `Value`；`TryParse("<scheme> <value>")`；`ToString()` 隐藏值 |
| `Authentication.IAuthenticationHandler` | 某一方案的处理器：`Scheme`、`AuthenticateAsync` → `Result<Principal>` |
| `Authentication.AuthenticationMiddleware` | Authenticate 阶段的中间件 |
| `Authorization.AuthorizationRequirement` | 授权要求：`Policy`、`Resource`；`Authenticated`、`Permission(code, resource?)` |
| `Authorization.PolicyCatalog` | 策略目录：`Find(name)`、`Names`；策略名重复在构造时抛出 `ArgumentException` |
| `Authorization.AuthenticatedPolicy` | 策略 `authenticated` |
| `Authorization.PermissionPolicy` | 权限策略：`Prefix`（`permission:`）、`NameFor(code)`、`CodeOf(name)` |
| `Authorization.AuthorizationMiddleware` | Authorize 阶段的中间件；策略判定失败以该失败结束调用（ADR-0071） |
| `Permissions.GrantSubject` | 授予的主体：`ForUser(id)`、`ForRole(name)`，种类常量 `User`、`Role` |
| `Permissions.PermissionGrant` | 一条授予：主体、权限码（`All` = `*`）、范围（`null` 为平台，`AnyTenant` = `*` 为每个租户，否则为该租户，ADR-0197）；`VisibleScopes(tenantId)` 给出一次调用看得到的范围 |
| `Permissions.IPermissionGrantStore`、`PermissionGrantStoreExtensions` | 授予存储：`GetManyAsync(subjects, tenantId)` → 与入参等长的 `Result<IReadOnlyList<IReadOnlyCollection<string>>>`，单主体写法是扩展方法 `GetGrantsAsync(subject, tenantId)`；每项是该主体对这次调用生效的授予（不带租户是平台授予，带租户是每个租户的授予加该租户的授予，`PermissionGrant.VisibleScopes`）；存储不可达是 `SECURITY_UNAVAILABLE_001`，不是空集合 |
| `Permissions.IPermissionGrantAdministration`、`PermissionGrantAdministrationExtensions`、`PermissionGrantQuery`、`GrantTenantFilter`、`PermissionGrantPage` | 可选的授予管理：`GrantAsync` / `RevokeAsync` 与入参同序的逐项结果（撤销不存在为 `SECURITY_NOTFOUND_002`），`ListAsync(query)` 按主体与范围过滤（`Any` / `Platform` / `Of(tenantId)`，`Of("*")` 是每个租户的授予）、偏移分页并计数，越界为 `SECURITY_VALIDATION_005`；单条写法是扩展方法 |
| `Permissions.IPermissionChecker` / `PermissionChecker` | RBAC 检查器：`HasPermissionAsync` → `Result<bool>`，存储、来源、过滤器的失败原样返回 |
| `Permissions.IPermissionFilter` | 授予命中后的收窄（ADR-0039）：`AllowsAsync` → `Result<bool>` |
| `Permissions.IPermissionGrantSource` | 存储之外的额外授予（ADR-0045）：`GetGrantsAsync` → `Result<IReadOnlyCollection<string>>` |
| `SecurityInvocationExtensions` | `SetCredential` / `FindCredential`、`RequireAuthorization`（追加一条要求）/ `GetAuthorizationRequirements` |
| `SecurityOptions` | 选项，见上表 |
| `Conditions.*` | 条件引擎（ADR-0046）：`ConditionSet` / `ConditionGroup` / `ConditionClause`、`ConditionOperator`、`ConditionValueKind`、`ConditionAttributeDescriptor`、`IConditionAttributeRegistry`、`IConditionCompiler`、`IConditionEvaluator`、`IConditionAttributeSource`、`DictionaryConditionAttributeSource`、`MemoizingConditionAttributeSource`、`ConditionOperatorRules`、`AddConditionAttributes` |
| `Network.IIpRegionResolver` / `IpRegion` | IP 归属地（ADR-0047）：`Resolve(IPAddress)` → `Maybe<IpRegion>` |
| `SecurityCapabilities` / `SecurityErrors` / `SecurityMessages` | 能力 ID、错误码与消息键常量，消息目录 `SecurityMessages.Catalog` |

## 使用示例

自定义认证处理器，例如服务间调用用的共享令牌方案：

```csharp
using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;

public sealed class ServiceTokenHandler : IAuthenticationHandler
{
    // 开发值；真实令牌从密钥库读取
    private static readonly byte[] _expected = Encoding.UTF8.GetBytes("dev-service-token-0001");

    public string Scheme => "Service";

    public ValueTask<Result<Principal>> AuthenticateAsync(AuthenticationCredential credential, InvocationContext context, CancellationToken cancellationToken)
    {
        var presented = Encoding.UTF8.GetBytes(credential.Value);
        // 常量时间比较，耗时不随匹配位置泄露信息
        var valid = presented.Length == _expected.Length && CryptographicOperations.FixedTimeEquals(presented, _expected);
        return ValueTask.FromResult(valid
            ? Result.Success(new Principal("svc-billing", Scheme) { Claims = [new("role", "service")] })
            : Result.Failure<Principal>(Error.Denied(SecurityErrors.InvalidCredential, SecurityMessages.InvalidToken)));
    }
}
```

在特性里登记 `context.Services.AddSingleton<IAuthenticationHandler, ServiceTokenHandler>()`。认证中间件由认证 Provider 加进管道；方案名不与已有处理器重名时，`Authorization: Service <token>` 就交给它。

在 Authorize 阶段之前的中间件里设置授权要求，例如让没有写授权要求的操作至少要求登录：

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authorization;

public sealed class AuthenticatedByDefaultMiddleware : IPipelineMiddleware
{
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        // 端点没挂要求、契约也没写权限或策略时才补；写入的要求与契约的要求叠加判定
        if (context.GetAuthorizationRequirements().Count == 0
            && context.FindOperation().TryGetValue(out var operation)
            && operation.Contract is { Permission: null, Policy: null })
        {
            context.RequireAuthorization(AuthorizationRequirement.Authenticated);
        }

        return nextStep(context);
    }
}
```

在特性里加到 Authorize 之前的阶段：`context.AddMiddleware<AuthenticatedByDefaultMiddleware>(PipelineStage.Tenant)`。

收窄过滤器、授予来源、自定义策略与条件引擎的示例见[授权](/security/authorization)。

## 行为与限制

- 认证：没有凭据保持匿名；方案找不到返回 `SECURITY_VALIDATION_001`；认证成功保留已有租户；`RequireAuthentication` 在认证之后检查。
- 策略查找：注册的策略按名区分大小写匹配；`permission:<code>` 只为 `PermissionRegistry` 里登记过的权限码创建；找不到返回 `SECURITY_NOTFOUND_001`，宁可拒绝不可放行。操作契约的权限码在装配时由内核核对（`KERNEL_VALIDATION_005`），契约的 `Policy` 在授权能力启动时经 `PolicyCatalog.EnforceOn` 核对、端点元数据在 `UseXiHan()` 建请求管道时核对（`SECURITY_VALIDATION_004`，启动失败，ADR-0080），这条路径只剩启动时看不到的要求（`RequireAuthorization` 显式写入的、启动之后才加进来的端点）。
- `PolicyCatalog.EnforceOn(OperationRegistry)`：核对已登记的全部操作契约的 `Policy` 都在目录里（`permission:<code>` 要求权限码已登记），不在即抛出 `SECURITY_VALIDATION_004`（`FrameworkException`，一次列出全部）；同时经 `OperationRegistry.Enforce` 挂到登记表上，此后登记的操作（插件握手之后）引用了不存在的策略，登记时返回同一错误码（消息键 `security.policy.operation_unregistered`，参数 `id`、`owner`、`policy`），整组不登记。`security.authorization@1` 的 Provider 被选中时在 `Configure` 里经 `FeatureContext.AddStartupCheck` 把它登记为启动核对（容器建好之后、任何特性启动之前执行，不论失败策略，抛出即终止启动，ADR-0085）；`Security.InMemory` 与 `Security.Database` 被选为授权能力时这样登记。
- 权限判定：匿名主体不持有任何权限，不访问存储；授予按序数精确匹配或 `*`；顺序是用户授予 → 角色授予 → 授予来源 → 过滤器，命中即止。存储、来源或过滤器返回的失败原样返回（不当作不持有，ADR-0071），抛出的异常照常传播。替换 `IPermissionChecker` 的实现须同样并上全部来源并施加全部过滤器。
- `PermissionChecker` 的构造函数接收全部登记的 `IPermissionFilter` 与 `IPermissionGrantSource`（可为空集合），集合里出现 `null` 抛出 `ArgumentException`。
- 失败到 HTTP 的映射（ADR-0042、ADR-0071）：`SECURITY_DENIED_001` 与 `SECURITY_DENIED_003` 是 401 并带 `WWW-Authenticate`（Bearer 被拒时为 `Bearer error="invalid_token"`），其余 `DENIED` 是 403，`SECURITY_UNAVAILABLE_001` 是 503，任何一环读不到都不能落到 403；认证处理器内部的基础设施失败抛异常。
- 收窄过滤器与授予来源：过滤器收到的是具体权限码，持 `*` 也会被收窄；匿名与未命中授予不调用过滤器；收窄拒绝时权限策略返回 `SECURITY_DENIED_004`。授予来源在用户与角色的存储授予都没命中时按登记顺序逐个查询，任一命中即算授予命中，之后照常经全部过滤器；匿名不查询。两者都按单例登记（如 `services.AddSingleton<IPermissionFilter, EditionPermissionFilter>()`），用于套餐白名单、显式拒绝、委托到期、角色继承、限时委托。
- 条件引擎（ADR-0046）：`ConditionSet` / `ConditionGroup` / `ConditionClause`，组内且、组间或；十五个运算符与七种值种类（整数值是持久化契约），`ConditionOperatorRules` 给出搭配规则；属性按精确名、别名、最长前缀的族解析；编译错误是 `SECURITY_VALIDATION_002`（消息键说明原因，出错子句保留）；求值结果为命中、不命中、无法判定或待定，说明模式逐组逐子句；属性来源 `IConditionAttributeSource` 由应用提供，自带 `DictionaryConditionAttributeSource`（试算）与 `MemoizingConditionAttributeSource`；`AddConditionAttributes(...)` 登记属性并确保三项单例服务。
- `IIpRegionResolver.Resolve(IPAddress)` 对回环、私有网段与查不到返回无值；不装 Provider 时容器里没有本服务，调用方把归属地当作取不到。
- 条件引擎的登记表在首次解析时建好，重名与别名冲突即抛出；`MemoizingConditionAttributeSource` 按单次调用使用，不是线程安全的。

| 错误码 | 含义 |
| --- | --- |
| `SECURITY_DENIED_001` | 未认证 |
| `SECURITY_DENIED_002` | 策略拒绝 |
| `SECURITY_DENIED_003` | 凭据无效、过期或签名不符 |
| `SECURITY_DENIED_004` | 缺少权限 |
| `SECURITY_VALIDATION_001` | 不支持的认证方案 |
| `SECURITY_VALIDATION_002` | 条件无效（属性未登记、运算符与值种类不搭配、条件值解析不了） |
| `SECURITY_VALIDATION_003` | 条件属性的值不是登记的值种类 |
| `SECURITY_VALIDATION_004` | 按名引用的策略不存在：操作契约的 `Policy`（授权能力的启动核对里，不论失败策略；之后登记的在登记时）、端点元数据（`UseXiHan()` 建请求管道时）引用了未注册的策略或未登记的权限码；启动失败 |
| `SECURITY_NOTFOUND_001` | 调用时才看到的要求（显式写入的、启动之后加进来的端点）引用的策略不存在，或其中 `permission:<code>` 的权限码未登记 |
| `SECURITY_NOTFOUND_002` | 要撤销的授予不存在 |
| `SECURITY_NOTFOUND_003` | 取不到条件属性的值 |
| `SECURITY_UNAVAILABLE_001` | 身份或授予存储不可达：授予存储、授予来源或收窄过滤器读不到数据（HTTP 503，不当作不持有） |
| `SECURITY_UNAVAILABLE_002` | 身份或授予存储容量已满 |
| `SECURITY_UNAVAILABLE_003` | 签发者的发现文档或 JWKS 不可达且没有缓存 |
| `SECURITY_UNAVAILABLE_004` | 条件属性暂不可得 |

消息键（`security.*`）在本包的 `Localization/<区域>.json` 里有 10 种区域的文案，Provider 装配时注册 `SecurityMessages.Catalog`。

## 相关

- [认证](/security/authentication)
- [授权](/security/authorization)
- [Security.InMemory](./security-inmemory)
- [Security.Database](./security-database)
- [Kernel 内核](./kernel)
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0008《认证与授权》；ADR-0039、ADR-0042、ADR-0045、ADR-0046、ADR-0047
