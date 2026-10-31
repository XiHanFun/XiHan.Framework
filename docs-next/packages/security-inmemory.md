# XiHan.Framework.Security.InMemory

安全的默认实现：从配置装载 API Key 主体与权限授予，提供 `security.authentication@1`（`ApiKey` 方案）与 `security.authorization@1`（RBAC 授予存储）。只用于开发、原型与测试。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Security.InMemory` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `security.authentication@1`、`security.authorization@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `InMemorySecurityFeature`（清单 `xihan.security.inmemory`，`role: default`） |
| 配置节 | `XiHan:Security`（含 `InMemory` 子节） |
| 依赖 | `XiHan.Framework.Security.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

特性只为解析器选中自己的能力注册服务与中间件：

| 能力被本包提供时 | 注册 |
| --- | --- |
| `security.authentication@1` | `InMemoryPrincipalStore`、`ApiKeyAuthenticationHandler`、Authenticate 阶段的 `AuthenticationMiddleware` |
| `security.authorization@1` | `InMemoryPermissionGrantStore` 与 `IPermissionGrantStore`、`PermissionChecker`、`authenticated` 策略、`PolicyCatalog`、Authorize 阶段的 `AuthorizationMiddleware` |

清单角色是 `default`：装配 `Security.Jwt` 或 `Security.OpenIdConnect` 后本包让出认证、只提供授权；装配 `Security.Database` 后让出授权、只提供认证；两边都被顶替时本包不装配。健康项 `security.inmemory.capacity` 只看本包仍在提供的那部分存储。

## 何时使用

- 本地开发、演示、自动化测试，主体与授予随配置提交：用本包。`Profiles.Standard` 已包含它。
- 生产环境：认证换 [Security.Jwt](./security-jwt) 或 [Security.OpenIdConnect](./security-openidconnect)，授予换 [Security.Database](./security-database)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Security.InMemory" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。单独装配时：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Security.InMemory;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<InMemorySecurityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

## 配置

| 键（`XiHan:Security:InMemory`） | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Principals:<id>:ApiKey` | `string` | 必填 | API Key，至少 8 个字符；节名 `<id>` 是主体 ID |
| `Principals:<id>:DisplayName` | `string` | 无 | 显示名 |
| `Principals:<id>:Roles` | `string` | 无 | 逗号分隔的角色 |
| `Principals:<id>:Tenant` | `string` | 无 | 所属租户，写成 `tenant` 声明 |
| `Principals:<id>:Claims:<type>` | `string` | 无 | 附加声明 |
| `Grants:role:<role>` | `string` | 无 | 授予角色的权限码，逗号分隔，`*` 为全部 |
| `Grants:user:<id>` | `string` | 无 | 授予用户的权限码，逗号分隔 |
| `MaxPrincipals` | `int` | 1000 | 主体上限 |
| `MaxGrants` | `int` | 10000 | 授予上限，按（主体，权限码，租户）计数 |

`XiHan:Security:RequireAuthentication` 与 `XiHan:Security:RoleClaim` 见 [Security.Abstractions](./security-abstractions#配置)。

```json
{
  "XiHan": {
    "Security": {
      "InMemory": {
        "Principals": {
          "alice": { "ApiKey": "dev-alice-key", "DisplayName": "Alice", "Roles": "admin,editor", "Tenant": "acme", "Claims": { "email": "alice@example.com" } },
          "bob": { "ApiKey": "dev-bob-key", "DisplayName": "Bob", "Roles": "viewer", "Tenant": "acme" }
        },
        "Grants": {
          "role": { "admin": "*", "editor": "orders.read,orders.write", "viewer": "orders.read" },
          "user": { "alice": "reports.read" }
        }
      }
    }
  }
}
```

配置里的授予都是全局的（对任何租户生效）；租户专属授予只能在运行时经 `InMemoryPermissionGrantStore.Grant` 添加。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `InMemorySecurityFeature` | 特性入口；`HealthName` = `security.inmemory.capacity` |
| `InMemorySecurityOptions` | `MaxPrincipals`、`MaxGrants`、`Principals`、`Grants`；`FromConfiguration` 读取上面的配置 |
| `PrincipalRecord` | 配置里的一个主体：`Id`、`ApiKey`、`DisplayName`、`Roles`、`Tenant`、`Claims` |
| `InMemoryPrincipalStore` | 主体存储：`FindByApiKey`（常量时间比较）、`Set`、`Remove`、`Capacity`、`Count` |
| `ApiKeyAuthenticationHandler` | `ApiKey` 方案的处理器；`SchemeName` = `ApiKey` |
| `InMemoryPermissionGrantStore` | 授予存储：`GetGrantsAsync`、`Grant`、`Revoke`、`Capacity`、`Count` |
| `InMemorySecurityHealthContributor` | 健康项的贡献者 |

## 使用示例

带 API Key 调用 `next/samples/SimpleApp`：

```bash
curl -H "Authorization: ApiKey dev-alice-key" http://localhost:5000/whoami
```

运行时添加租户专属授予：

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.InMemory;

public sealed class TrialGrants(InMemoryPermissionGrantStore grants)
{
    // 只在 acme 租户里给 bob 导出权限；配置文件里写不了租户专属授予
    public Result GrantExport() => grants.Grant(new PermissionGrant(GrantSubject.ForUser("bob"), "reports.export", "acme"));

    public bool RevokeExport() => grants.Revoke(new PermissionGrant(GrantSubject.ForUser("bob"), "reports.export", "acme"));
}
```

## 行为与限制

- 认证成功的主体按顺序带上：每个角色一条类型为 `XiHan:Security:RoleClaim` 的声明、一条 `tenant` 声明（有 `Tenant` 时）、附加声明。租户声明的类型固定为 `tenant`，不跟随 `XiHan:MultiTenancy:TenantClaim`。
- API Key 逐条做常量时间比较；找不到返回 `SECURITY_DENIED_003`。
- 容量：新增主体超过 `MaxPrincipals`、新增授予超过 `MaxGrants` 时返回 `SECURITY_UNAVAILABLE_002`；替换已有主体、重复授予不占容量。配置里的条目超过上限时，构造存储即抛出 `FrameworkException`。
- 健康项 `security.inmemory.capacity`：主体存储或授予存储任一达到上限即 Degraded（`SECURITY_UNAVAILABLE_002`，两者都满时报主体存储），否则 Ready。
- 数据只在进程内存里：运行时的增删不跨实例同步，重启后恢复为配置内容。
- 通过 `Security.Conformance` 的 `PermissionGrantStoreConformance`。

::: warning API Key 明文写在配置里
主体与 API Key 随配置文件分发，任何能读配置的人都能冒用这些主体。本包只用于开发与测试，示例值用 `dev-…-key` 这类明显的开发值。
:::

## 相关

- [认证](../guide/authentication)
- [授权](../guide/authorization)
- [Security.Abstractions](./security-abstractions)
- [Security.Jwt](./security-jwt)
- [Security.Database](./security-database)
- 规范：RFC-0008《认证与授权》
