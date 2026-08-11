# XiHan.Framework.Authorization

> 授权：RBAC 角色权限、Policy 策略、ABAC 属性访问控制，并接入 ASP.NET Core 授权管道（实时校验）

- **NuGet**：`XiHan.Framework.Authorization`
- **模块类**：`XiHanAuthorizationModule`（`DependsOn` `XiHanAuthenticationModule`）
- **所在层**：基础设施层
- **关键依赖**：`FrameworkReference` **Microsoft.AspNetCore.App**（ASP.NET Core 授权管道）+ 框架内部依赖 Core / Authentication

## 概述

这个包负责**授权**——判定“你能做什么”。它把三种主流授权模型整合到一起：**RBAC**（基于角色的权限）、**Policy**（策略）与 **ABAC**（基于属性的访问控制），并接入 ASP.NET Core 的授权管道，让你用一个 `[PermissionAuthorize(...)]` 特性就能触发“权限码 + 属性约束”的混合判定。

> 认证（Authentication）= 你是谁；授权（Authorization）= 你能做什么。本包处理后者，建立在 [Authentication](./authentication) 颁发的身份声明之上。

关键设计取向是**实时校验**：授权处理器不信任 JWT 里冻结的权限声明，而是每次都走 `IPermissionChecker` 查当前授权快照，确保授权/撤销、用户禁用、会话注销即时生效。

## 何时使用

- 需要基于角色 / 权限码的访问控制（RBAC）
- 需要在权限之上叠加属性约束（同租户、仅本人、时间、IP、资源属性等 ABAC）
- 需要把权限判定接进 ASP.NET Core，用特性声明式地保护 Controller/Action
- 需要“实时”授权：权限授予/回收、用户禁用即时生效，而非等令牌过期

## 安装与启用

```bash
dotnet add package XiHan.Framework.Authorization
```

```csharp
[DependsOn(typeof(XiHanAuthorizationModule))]
public class MyModule : XiHanModule { }
```

模块 `ConfigureServices` 调用 `AddXiHanAuthorization(config)`，用 `TryAdd` 注册整套存储 / 评估器 / 服务，以及 ASP.NET Core 集成件：

- 存储：`IRoleStore` → `DefaultRoleStore`、`IPermissionStore` → `DefaultPermissionStore`、`IPolicyStore` → `DefaultPolicyStore`（均 Scoped）
- 评估：`IPermissionChecker` → `DefaultPermissionChecker`、`IPolicyEvaluator` → `DefaultPolicyEvaluator`、`IAbacAttributeCollector` → `DefaultAbacAttributeCollector`、`IAbacEvaluator` → `DefaultAbacEvaluator`（均 Scoped）
- 门面：`IAuthorizationService` → `DefaultAuthorizationService`（Scoped）
- ASP.NET Core：`IAuthorizationPolicyProvider` → `HybridPermissionPolicyProvider`（Singleton）、`IAuthorizationHandler` += `HybridPermissionAuthorizationHandler`（Scoped，`TryAddEnumerable`）

## 工作原理

**混合策略名（约定核心）**：特性把“权限码 + ABAC 策略码”编码进一个 ASP.NET Core 策略名，格式：

```text
xihan.hybrid:p={URL编码的权限码};a={URL编码的ABAC策略码}
```

`HybridPermissionPolicyProvider` 拦截以 `xihan.hybrid:` 开头的策略名，解析出两个编码后动态构建一条含 `HybridPermissionRequirement` 的策略（其余策略名回落到 `DefaultAuthorizationPolicyProvider`）。因此**无需预注册命名策略**，任意权限码/ABAC 组合即取即用。

**判定顺序（`HybridPermissionAuthorizationHandler`）**：

1. 从主体声明解析 userId（`NameIdentifier` / `sub` / `userid` / `user_id`）；解析不到直接不通过。
2. 若有权限码 → `IPermissionChecker.IsGrantedAsync` 实时校验（超管的通配 `*` 也由检查器的快照负责）；不通过则终止。
3. 若无 ABAC 策略码 → 直接 `Succeed`。
4. 若有 ABAC 策略码 → `IAbacAttributeCollector` 收集主体/资源/环境属性 → `IAbacEvaluator.EvaluateAsync` 评估 → 允许才 `Succeed`。

**RBAC 权限来源（`DefaultPermissionChecker`）**：`IsGrantedAsync` 先查用户直接权限，再遍历用户的启用角色查角色权限，任一命中（且 `IsEnabled`）即通过。`GetGrantedPermissionsAsync` 合并去重用户+角色权限。真实权限数据由 `IPermissionStore` / `IRoleStore` 提供（默认实现为线程安全的纯内存实现，进程重启即丢失且不预置任何数据，生产需业务层替换为读库实现）。

## 核心能力

- **RBAC** `IRoleStore` + `RoleDefinition`：角色定义与用户-角色关系；`IAuthorizationService` 提供加入/移除角色、查询用户角色
- **权限检查** `IPermissionChecker`：`IsGrantedAsync`（单个）、`IsAnyGrantedAsync`（任一）、`IsAllGrantedAsync`（全部）、`GetGrantedPermissionsAsync`（列全部）、`PermissionExistsAsync`
- **Policy 策略** `IPolicyEvaluator` + `PolicyDefinition`：可组合所需角色（任一）、权限（全部）、声明键值、自定义 `IAuthorizationRequirement`；支持 `EvaluateAll` / `EvaluateAny`
- **ABAC 属性控制** `IAbacEvaluator` + `IAbacAttributeCollector`：评估上下文含主体 / 资源 / 环境三类属性（`AbacAttributeSet`）；默认评估器内置策略词汇（见下）
- **ASP.NET Core 集成**：`PermissionAuthorizeAttribute` / `AbacAuthorizeAttribute` + `HybridPermissionPolicyProvider` + `HybridPermissionAuthorizationHandler`，用标准 `[Authorize]` 管道跑混合判定
- **实时授权**：默认走 `IPermissionChecker` 实时校验，不信任 token 里冻结的权限声明；超管通配由检查器快照承载

## 主要 API / 类型

### 门面与结果

| 类型 | 说明 |
| --- | --- |
| `IAuthorizationService` / `DefaultAuthorizationService` | 授权总入口：`AuthorizeAsync`（权限）、`AuthorizePolicyAsync`（策略+资源）、`AuthorizeRoleAsync`、`AuthorizeAnyAsync` / `AuthorizeAllAsync`、`GetUserPermissionsAsync` / `GetUserRolesAsync`、`GrantPermissionAsync` / `RevokePermissionAsync`、`AddUserToRoleAsync` / `RemoveUserFromRoleAsync`；均返回 `AuthorizationResult` |
| `AuthorizationResult` | `Succeeded`、`FailureReason`、`FailedRequirements`、`AdditionalData`；工厂 `Success()` / `Failure(...)` / `PermissionDenied(name)` / `RoleDenied(name)` |

### RBAC

| 类型 | 关键方法 / 字段 |
| --- | --- |
| `IPermissionChecker` | `Task<bool> IsGrantedAsync(userId, permissionName, ct)`、`IsAnyGrantedAsync(userId, List<string>, ct)`、`IsAllGrantedAsync(...)`、`Task<List<string>> GetGrantedPermissionsAsync(userId, ct)`、`Task<bool> PermissionExistsAsync(name, ct)` |
| `IPermissionStore` | 用户/角色权限读取、授予/撤销、`GetAllPermissionsAsync`、`GetPermissionByNameAsync` |
| `PermissionDefinition` | `Name`（唯一）、`DisplayName`、`Description`、`ParentName`、`Tag`、`IsEnabled`、`Order`、`Properties` |
| `IRoleStore` | `GetUserRolesAsync`、`IsInRoleAsync`、`AddUserToRoleAsync` / `RemoveUserFromRoleAsync`、`GetAllRolesAsync`、`GetRoleByName/IdAsync`、`Create/Update/DeleteRoleAsync`、`GetUsersInRoleAsync` |
| `RoleDefinition` | `Id`、`Name`（唯一）、`DisplayName`、`Description`、`IsEnabled`、`IsDefault`、`IsStatic`、`Order`、`CreatedTime` / `LastModifiedTime`、`Properties` |

### Policy

| 类型 | 关键方法 / 字段 |
| --- | --- |
| `IPolicyEvaluator` | `Task<PolicyEvaluationResult> EvaluateAsync(userId, policyName, resource?, ct)`、`EvaluateAllAsync(...)`、`EvaluateAnyAsync(...)` |
| `PolicyDefinition` | `Name`、`RequiredRoles`（任一）、`RequiredPermissions`（全部）、`RequiredClaims`（键值）、`CustomRequirements`（`List<IAuthorizationRequirement>`）、`IsEnabled` |
| `IAuthorizationRequirement` | 自定义要求：`string Name { get; }`、`Task<bool> EvaluateAsync(AuthorizationContext)` |
| `AuthorizationContext` | `UserId`、`UserRoles`、`UserPermissions`、`UserClaims`、`Resource`、`PolicyName`、`AdditionalData` |

> 注意：本包自有的 `IAuthorizationRequirement`（`XiHan.Framework.Authorization.Policies`）是 Policy 子系统的自定义要求接口，**不同于** ASP.NET Core 的同名接口 `Microsoft.AspNetCore.Authorization.IAuthorizationRequirement`（后者用于 `HybridPermissionRequirement`）。

### ABAC

| 类型 | 关键方法 / 字段 |
| --- | --- |
| `IAbacEvaluator` / `DefaultAbacEvaluator` | `Task<AbacEvaluationResult> EvaluateAsync(AbacEvaluationContext, ct)` |
| `IAbacAttributeCollector` / `DefaultAbacAttributeCollector` | `Task<AbacAttributeSet> CollectAsync(ClaimsPrincipal, resource, permissionCode, policyCode, ct)` |
| `AbacAttributeSet` | `SubjectAttributes` / `ResourceAttributes` / `EnvironmentAttributes`（三个 `Dictionary<string, object?>`，键不区分大小写） |
| `AbacEvaluationContext` | `UserId`、`PermissionCode`、`PolicyCode`、`Resource`、三类只读属性字典、`EvaluationTime` |
| `AbacEvaluationResult` | `IsAllowed`、`Reason`；工厂 `Allow(reason?)` / `Deny(reason?)` |

### ASP.NET Core 集成

| 类型 | 说明 |
| --- | --- |
| `PermissionAuthorizeAttribute` | `[PermissionAuthorize("权限码")]` 或 `[PermissionAuthorize("权限码", "abac策略码")]`；构造时即算出 `Policy = xihan.hybrid:p=...;a=...` |
| `AbacAuthorizeAttribute` | `[AbacAuthorize("abac策略码")]`——仅 ABAC，不校验 RBAC 权限码 |
| `HybridPermissionRequirement` | `PermissionCode`、`AbacPolicyCode`（实现 ASP.NET Core `IAuthorizationRequirement`） |
| `HybridPermissionPolicyProvider` | 拦截 `xihan.hybrid:` 前缀策略名，动态构建策略；其余回落默认提供器 |
| `HybridPermissionAuthorizationHandler` | 混合判定处理器（先权限码实时校验，再 ABAC 评估） |

## 默认 ABAC 策略词汇

`DefaultAbacEvaluator` 内置一套开箱即用的策略码解析（`policyCode` 大小写不敏感）：

| 策略码 | 判定 |
| --- | --- |
| 空 | 允许（未配置 ABAC 策略） |
| `allow` | 无条件允许 |
| `same_tenant` / `tenant_match` | 主体 `tenant_id` 与资源的 `tenant_id` / `route.tenant_id` / `query.tenant_id` 任一相等 |
| `self_only` / `owner_match` | 主体 `user_id` 与资源的 `user_id` / `owner_user_id` / `route.user_id` / `query.user_id` 任一相等 |
| 比较表达式 | 形如 `subject.x == resource.y`、`environment.utc_hour != "0"`；操作符 `==` / `!=` / `=`，操作数支持 `subject.` / `resource.` / `environment.` 前缀取值或字面量（字符串/布尔/数字） |
| 其它 | 拒绝（不支持的策略） |

`DefaultAbacAttributeCollector` 收集的属性包括：主体 `user_id`/`tenant_id`/`roles`/`is_authenticated` 及所有 `claim.*`；资源 `resource_type` 与反射得到的公共属性，若资源能解析出 `HttpContext` 还含 `route.*`/`query.*`/`http.method`/`http.path`/`http.client_ip`；环境 `permission_code`/`policy_code`/`utc_now`/`utc_hour`/`day_of_week`（有 `HttpContext` 时另含 `client_ip`/`request_path`/`request_method`/`user_agent`）。

## 使用示例

在服务里检查权限：

```csharp
public class DocumentService(IPermissionChecker permissions)
{
    public async Task DeleteAsync(long userId, long docId)
    {
        if (!await permissions.IsGrantedAsync(userId.ToString(), "document:delete"))
            throw new UnauthorizedAccessException();
        // ...
    }
}
```

用特性声明式保护 Controller/Action（无需预注册命名策略）：

```csharp
// 仅权限码
[PermissionAuthorize("document:delete")]
public IActionResult Delete(long id) => ...;

// 权限码 + ABAC：既要有权限，又要同租户
[PermissionAuthorize("document:read", "same_tenant")]
public IActionResult Get(long id) => ...;

// 仅 ABAC：只能操作本人资源
[AbacAuthorize("self_only")]
public IActionResult UpdateProfile(long userId) => ...;
```

用策略门面判定（可带资源对象供 ABAC/Policy 使用）：

```csharp
public class ReportService(IAuthorizationService authz)
{
    public async Task<bool> CanExportAsync(string userId, ReportEntity report)
    {
        var r = await authz.AuthorizePolicyAsync(userId, "report:export-policy", report);
        return r.Succeeded;
    }
}
```

> `XiHan.BasicApp` 在此基础上落地了完整的权限码 `resource:action:scope`、数据范围与字段级脱敏（FLS），见 [BasicApp 权限模型](https://basicapp.docs.xihanfun.com/backend/permission)。

## 扩展点 / 自定义

- **权限 / 角色 / 策略存储**：默认实现（`DefaultPermissionStore` / `DefaultRoleStore` / `DefaultPolicyStore`）基于 `ConcurrentDictionary` 的线程安全内存实现，可正常增删查（含各自的 `ClearAsync` 及 `AddRolesAsync` / `AddPermissionsAsync` / `AddPoliciesAsync` 等批量播种方法，便于开发/测试播种数据），但不持久化、不预置数据；生产必须实现 `IPermissionStore` / `IRoleStore` / `IPolicyStore` 读真实库，DI 覆盖（扩展方法用 `TryAddScoped`，先注册即生效）。
- **超管通配 `*`**：由你实现的 `IPermissionChecker` / `IPermissionStore` 快照负责（如超管权限集含 `*`），本包不硬编码超管身份。
- **自定义 ABAC 逻辑**：实现 `IAbacEvaluator` 覆盖 `DefaultAbacEvaluator`，接你自己的策略引擎（如 OPA/规则 DSL）；或实现 `IAbacAttributeCollector` 补充领域特有的资源属性。
- **自定义 Policy 要求**：实现 `IAuthorizationRequirement`（Policies 版）放进 `PolicyDefinition.CustomRequirements`。

## 注意事项与最佳实践

- **实时校验优先，不信任冻结声明**：`HybridPermissionAuthorizationHandler` 一律以 `IPermissionChecker` 为准，故授权/撤销、禁用、注销即时生效——不要退回到只读 token 里的权限声明。
- **策略名有 URL 编码**：权限码/ABAC 码里含 `;`、`=`、空格等会被 `Uri.EscapeDataString` 编码进策略名；直接手写策略名时须遵循 `xihan.hybrid:p=...;a=...` 格式（一般用特性即可，无需手写）。
- **ABAC 需要资源对象**：`same_tenant`/`self_only`/比较表达式依赖资源属性；Controller 上要让 ABAC 拿到 `HttpContext`（`route.*`/`query.*`）或显式把资源对象作为授权 `resource` 传入。
- **两个同名 `IAuthorizationRequirement`**：Policy 子系统的自定义要求 vs ASP.NET Core 的标记接口，命名空间不同、用途不同，勿混用。
- **默认存储不可用于生产**：默认内存存储不持久化、不预置数据；若不接真实 `IPermissionStore`/`IRoleStore` 且从未通过其 API 授予过数据，所有权限判定都会失败。

## 依赖模块

- [XiHan.Framework.Core](./core)
- [XiHan.Framework.Authentication](./authentication)（并传递依赖 [Security](./security)）
- 框架引用：**Microsoft.AspNetCore.App**（ASP.NET Core 授权管道）

## 相关模块

- [XiHan.Framework.Authentication](./authentication)
- [XiHan.Framework.Security](./security)
- [BasicApp 权限模型](https://basicapp.docs.xihanfun.com/backend/permission)（权限码 / 数据范围 / 字段脱敏的应用层落地）
