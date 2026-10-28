# XiHan.Framework.Security.Abstractions

安全契约（Ring 2）。能力 `security.authentication@1`（凭据 → 主体）与 `security.authorization@1`（策略判定）由 Provider 提供；主体、租户、授权请求与策略接口等原语在 `Kernel.Security`。

| 契约 | 用途 |
| --- | --- |
| `AuthenticationCredential`、`IAuthenticationHandler` | 宿主把协议凭据（如 `Authorization: Bearer …`）放进上下文；按方案匹配的处理器把它变成 `Principal` |
| `AuthenticationMiddleware` | Authenticate 阶段：无凭据则匿名，不支持的方案或无效凭据以结果拒绝，`RequireAuthentication` 时拒绝匿名 |
| `AuthorizationRequirement`、`PolicyCatalog`、`AuthenticatedPolicy`、`PermissionPolicy` | 调用要求的策略；按名查找，`permission:<code>` 动态映射为权限策略，未定义的权限码按策略不存在拒绝 |
| `AuthorizationMiddleware` | Authorize 阶段：有要求才判定，策略不存在或拒绝以结果返回 |
| `IPermissionGrantStore`、`PermissionGrant`、`GrantSubject`、`IPermissionChecker`、`PermissionChecker` | RBAC：用户与角色（来自 `role` 声明）的授予之并集，`*` 表示全部；租户专属授予只对该租户生效 |
| `IPermissionFilter` | 权限收窄（ADR-0039）：授予命中之后 `PermissionChecker` 按登记顺序逐个调用，任一拒绝即不持有（权限策略返回 `SECURITY_DENIED_004`）；匿名与未命中不调用；收到的是具体权限码，持 `*` 也会被收窄。按单例登记：`services.AddSingleton<IPermissionFilter, EditionPermissionFilter>()`。用于套餐白名单、显式拒绝、委托到期；替换 `IPermissionChecker` 的实现须同样施加 |
| `IPermissionGrantSource` | 授予来源（ADR-0045）：与收窄对称，给出主体在授予存储之外的额外授予（权限码或 `*`）。`PermissionChecker` 在用户与角色的存储授予都没命中时按登记顺序逐个查询，任一命中即算授予命中，之后照常经全部过滤器收窄；匿名不查询。按单例登记：`services.AddSingleton<IPermissionGrantSource, RoleInheritanceGrantSource>()`。用于角色继承、限时委托；替换 `IPermissionChecker` 的实现须同样并上 |
| `Conditions` | 条件引擎（ADR-0046，RFC-0008「条件」）：`ConditionSet` / `ConditionGroup` / `ConditionClause`（组内且、组间或），十五个运算符与七种值种类（整数值是持久化契约），`ConditionOperatorRules` 搭配规则；`IConditionAttributeRegistry`（精确名 → 别名 → 最长前缀的族）、`IConditionCompiler`（编译错误 `SECURITY_VALIDATION_002`，消息键说明原因，出错子句保留）、`IConditionEvaluator`（命中 / 不命中 / 无法判定 / 待定，说明模式逐组逐子句）；属性来源 `IConditionAttributeSource` 由应用提供，自带 `DictionaryConditionAttributeSource`（试算）与 `MemoizingConditionAttributeSource`。`services.AddConditionAttributes(...)` 登记属性并确保三项单例服务 |
| `Network` | IP 归属地契约（ADR-0047，能力 `security.ip-region@1`）：`IIpRegionResolver.Resolve(IPAddress)` 返回 `Maybe<IpRegion>`（国家、省份、城市、运营商、国家代码，`Location` 为展示文本），回环、私有网段、查不到都是无值；实现见 `Security.Ip2Region`，不装时容器里没有本服务，调用方把归属地当作取不到 |
| `SecurityOptions` | `RequireAuthentication`、`RoleClaim` |

```csharp
context.RequireAuthorization(AuthorizationRequirement.Permission("orders.read"));   // 宿主或操作契约在 Authorize 阶段之前设置
```

认证与授权失败都以 `Result` 返回，宿主翻译为 401 / 403：`SECURITY_DENIED_001`（未认证）与 `SECURITY_DENIED_003`（凭据无效、过期或签名不符）是 401 并带 `WWW-Authenticate`（Bearer 被拒时为 `Bearer error="invalid_token"`），其余 DENIED 是 403（ADR-0042）；处理器内部的基础设施失败抛异常。

## 依赖方向

依赖 `XiHan.Framework.Kernel`。被 `Security.InMemory`、`Security.Jwt`、`Hosting.AspNetCore` 引用。

## 状态

P2：可用。
