# XiHan.Framework.Security

> 安全基座：当前用户/客户端/主体上下文、密码哈希与策略、BouncyCastle 加密算法

- **NuGet**：`XiHan.Framework.Security`
- **模块类**：`XiHanSecurityModule`
- **所在层**：基础设施层
- **关键依赖**：**BouncyCastle.Cryptography**（Blowfish / 国密 SM2）+ 框架内部依赖 `XiHan.Framework.Core`

## 概述

这个包是框架认证授权的公共基座，聚合三类能力：**主体上下文**（在任意服务里安全地读取“当前是谁 / 哪个客户端”）、**密码安全**（PBKDF2-SHA256 哈希、强度策略、历史复用校验）、以及**加密算法**（基于 BouncyCastle 的 Blowfish 对称加密与国密 SM2 椭圆曲线签名）。

它本身不做“登录”或“鉴权判定”，而是提供二者共同依赖的原语：统一的 `ClaimsPrincipal` 访问方式、统一的声明类型常量 `XiHanClaimTypes`，以及可注入的密码服务。[Authentication](./authentication)（你是谁）与 [Authorization](./authorization)（你能做什么）都 `DependsOn` 它。

## 何时使用

- 需要在任意服务里读取当前登录用户的身份、声明、租户（`ICurrentUser`）或客户端（`ICurrentClient`）
- 需要临时以另一主体身份执行一段逻辑（`ICurrentPrincipalAccessor.Change`，如模拟登录、后台任务附身）
- 需要对密码做安全哈希与校验、执行密码强度评分与历史复用检查
- 需要 Blowfish 对称加密或国密 SM2 签名/验签
- 构建自定义认证/授权逻辑时，复用统一的主体访问与声明类型常量

## 安装与启用

```bash
dotnet add package XiHan.Framework.Security
```

```csharp
[DependsOn(typeof(XiHanSecurityModule))]
public class MyModule : XiHanModule { }
```

模块 `ConfigureServices` 调用 `AddXiHanSecurityServices()`：

- `Configure<PasswordHasherOptions>` / `Configure<PasswordPolicyOptions>`——绑定配置节（节名沿用历史前缀 `XiHan:Authentication:*`，但绑定逻辑已归属本包，见下方“配置”一节说明）
- `IPasswordHasher` → `PasswordHasher`（`TryAddSingleton`）
- `IPasswordPolicyService` → `PasswordPolicyService`（`TryAddScoped`）
- `IPasswordHistoryStore` → `DefaultPasswordHistoryStore`（`TryAddScoped`，内存实现，生产需替换）

`ICurrentUser` / `ICurrentClient` / `ICurrentPrincipalAccessor` 的实现类通过框架的**标记接口约定**自动注册（见下方“工作原理”），无需在扩展方法里手写。[Authentication](./authentication) 包 `DependsOn` 本包、先加载后消费，不再重复注册密码哈希器或绑定 `Options`，只在其之上构建登录/令牌等能力。

## 工作原理

**主体访问（`ICurrentPrincipalAccessor`）**：抽象基类 `CurrentPrincipalAccessorBase` 用一个 `AsyncLocal<ClaimsPrincipal>` 保存“临时覆盖的主体”；`Principal` 属性优先返回该覆盖值，否则回落到抽象方法 `GetClaimsPrincipal()`。`Change(principal)` 设置覆盖值并返回一个 `IDisposable`，释放时自动恢复上一个主体——因此可以用 `using` 安全地做主体切换：

```text
using (accessor.Change(otherPrincipal))
{
    // 此作用域内 ICurrentUser 看到的是 otherPrincipal
}   // 离开作用域自动还原
```

框架默认实现 `ThreadCurrentPrincipalAccessor`（`ISingletonDependency`）从 `Thread.CurrentPrincipal` 取默认主体；Web 宿主通常另行提供基于 `HttpContext.User` 的实现覆盖它。`CurrentPrincipalAccessorBase.Principal` 在覆盖值与 `GetClaimsPrincipal()` 都为空时兜底返回一个匿名空 `ClaimsPrincipal`，保证 `Principal` 永不为 `null`，避免下游 `ICurrentUser` 在匿名请求下抛异常。`CurrentPrincipalAccessorExtensions` 还提供 `Change(Claim)` / `Change(IEnumerable<Claim>)` / `Change(ClaimsIdentity)` 三个便捷重载，内部统一包装成 `ClaimsPrincipal` 后调用 `Change`。

**声明读取**：`CurrentUser` / `CurrentClient` 都只依赖 `ICurrentPrincipalAccessor`，把 `Principal.Claims` 按 `XiHanClaimTypes` 里的类型解析成强类型属性（内部通过 `XiHanClaimsIdentityExtensions` 的 `FindUserId` / `FindTenantId` / `FindClientId` 等 `ClaimsPrincipal` 扩展方法完成，`UserId`/`TenantId` 解析为 `long?`，`EditionId`/模仿者用户标识解析为 `Guid?`）。这些扩展方法本身是 public 的，也可以在业务代码里直接对任意 `ClaimsPrincipal` / `IIdentity` 调用。因此“临时切换主体”会立即反映到 `ICurrentUser` 的所有属性。

**密码格式**：`PasswordHasher` 输出自描述的字符串 `version:iterations:hashAlgorithm:salt:hash`（后两段 Base64）。校验时从串里解析出当时的参数重算比对，用 `CryptographicOperations.FixedTimeEquals` 做恒定时间比较防时序攻击。`NeedsRehash` 比较存储参数与当前 `Options`，参数升级后可在下次登录成功时无感重哈希。

## 核心能力

- **当前用户上下文** `ICurrentUser`：`IsAuthenticated`、`UserId`（`long?`）、`UserName`、`Name`、`SurName`、`PhoneNumber` / `PhoneNumberVerified`、`Email` / `EmailVerified`、`TenantId`（`long?`）、`Roles`（`string[]`），以及 `FindClaim` / `FindClaims` / `GetAllClaims` / `IsInRole`
- **当前客户端上下文** `ICurrentClient`：`Id`、`IsAuthenticated`（从 `client_id` 声明解析）
- **主体访问器** `ICurrentPrincipalAccessor`：`Principal` 读取，`Change(ClaimsPrincipal)` 临时切换（基于 `AsyncLocal`，`using` 作用域内生效）
- **统一声明类型** `XiHanClaimTypes`：20+ 声明常量（用户、角色、权限、租户、版本、客户端、模拟登录、会话、设备指纹等）
- **密码哈希** `IPasswordHasher`：PBKDF2-SHA256，随机盐 + 可配置迭代（默认 60 万），自描述哈希格式支持版本平滑升级
- **密码策略** `IPasswordPolicyService`：长度/字符类型校验、弱密码黑名单（内置 30 个 + 自定义）、重复/连续字符检测、0–100 强度评分，以及历史密码复用检查
- **加密算法**：`BlowfishHelper`（Blowfish 对称，CBC + PKCS 填充，密钥 ≤ 56 字节）、`Sm2Helper`（国密 SM2 密钥对生成 / SM3WITHSM2 签名验签）

## 主要 API / 类型

### 主体与声明

| 类型 | 说明 |
| --- | --- |
| `ICurrentUser` / `CurrentUser` | 当前登录用户上下文；`Claim? FindClaim(string)`、`Claim[] FindClaims(string)`、`Claim[] GetAllClaims()`、`bool IsInRole(string)` |
| `ICurrentClient` / `CurrentClient` | 当前客户端；`string? Id`、`bool IsAuthenticated` |
| `ICurrentPrincipalAccessor` | `ClaimsPrincipal Principal { get; }`、`IDisposable Change(ClaimsPrincipal principal)` |
| `CurrentPrincipalAccessorBase` | 抽象基类，子类实现 `ClaimsPrincipal GetClaimsPrincipal()` |
| `ThreadCurrentPrincipalAccessor` | 默认实现，从 `Thread.CurrentPrincipal` 取主体 |
| `XiHanClaimTypes` | 声明类型常量集合（见下表） |
| `CurrentUserExtensions` | `ICurrentUser` 扩展：`FindClaimValue`、`FindClaimValue<T>`、`GetUserId`、`FindImpersonator*`、`FindPicture`、`GetSessionId` / `FindSessionId` |
| `XiHanClaimsIdentityExtensions` | `ClaimsPrincipal` / `IIdentity` 扩展：`FindUserId`、`FindTenantId`（均 `long?`）、`FindClientId`（`string?`）、`FindEditionId`、`FindImpersonatorUserId`（均 `Guid?`）、`FindImpersonatorTenantId`（`long?`）、`FindSessionId`；另有 `ClaimsIdentity` 编辑助手 `AddIfNotContains` / `RemoveAll` / `AddOrReplace` 与 `ClaimsPrincipal.AddIdentityIfNotContains` |
| `CurrentPrincipalAccessorExtensions` | `ICurrentPrincipalAccessor` 扩展：`Change(Claim)` / `Change(IEnumerable<Claim>)` / `Change(ClaimsIdentity)` 便捷重载 |

`XiHanClaimTypes` 关键常量（均为可 `set` 的静态属性，默认值如下）：

| 常量 | 默认值 |
| --- | --- |
| `UserId` | `ClaimTypes.NameIdentifier` |
| `UserName` | `ClaimTypes.Name` |
| `Name` / `SurName` | `ClaimTypes.GivenName` / `ClaimTypes.Surname` |
| `Role` | `ClaimTypes.Role` |
| `Permission` | `"permission"` |
| `Email` / `EmailVerified` | `ClaimTypes.Email` / `"email_verified"` |
| `PhoneNumber` / `PhoneNumberVerified` | `"phone_number"` / `"phone_number_verified"` |
| `TenantId` / `EditionId` | `"tenantid"` / `"editionid"` |
| `ClientId` | `"client_id"` |
| `ImpersonatorTenantId` / `ImpersonatorUserId` | `"impersonator_tenantid"` / `"impersonator_userid"` |
| `ImpersonatorTenantName` / `ImpersonatorUserName` | `"impersonator_tenantname"` / `"impersonator_username"` |
| `Picture` / `RememberMe` | `"picture"` / `"remember_me"` |
| `SessionId` / `DeviceFingerprint` | `"session_id"` / `"device_fingerprint"` |

### 密码

| 类型 | 说明 |
| --- | --- |
| `IPasswordHasher` / `PasswordHasher` | `string HashPassword(string)`、`bool VerifyPassword(string hashed, string provided)`、`bool NeedsRehash(string hashed)` |
| `PasswordHasherOptions` | 哈希参数（配置节 `XiHan:Authentication:PasswordHasher`） |
| `IPasswordPolicyService` / `PasswordPolicyService` | `PasswordValidationResult Validate(string)`、`Task<bool> IsPasswordReusedAsync(string newPassword, long userId, int historyCount, CancellationToken)` |
| `PasswordPolicyOptions` | 策略选项（配置节 `XiHan:Authentication:PasswordPolicy`） |
| `PasswordValidationResult` | `IsValid`、`Score`（0–100）、`Message`、`Errors`；工厂 `Success(score)` / `Failure(...)` |
| `IPasswordHistoryStore` / `DefaultPasswordHistoryStore` | 接口仅 `Task<IReadOnlyList<string>> GetRecentPasswordHashesAsync(long userId, int count, CancellationToken)`；默认实现另有 `static void RecordPassword(long userId, string passwordHash, int maxHistoryCount = 10)` 用于写入内存历史（非接口成员，框架不会自动调用，见下方“扩展点”） |

### 加密

| 类型 | 关键方法 |
| --- | --- |
| `BlowfishHelper` | `string Encrypt(string data, string key)` / `string Decrypt(string, string)`；字节版 `EncryptBytes` / `DecryptBytes`（CBC + PKCS 填充，密钥 ≤ 56 字节抛 `ArgumentException`） |
| `Sm2Helper` | `(string publicKey, string privateKey) GenerateKeys()`、`string SignData(string data, string privateKey)`、`bool VerifyData(string data, string signature, string publicKey)`（曲线 `sm2p256v1`，`SM3WITHSM2`，密钥/签名 Base64） |

## 配置

密码哈希与策略的 `Options` 定义、DI 注册与配置绑定均在本包完成（`AddXiHanSecurityServices` 里 `Configure`）；配置节前缀沿用历史命名 `XiHan:Authentication:*`（保留该前缀是为了不破坏已部署的配置文件，并不代表绑定逻辑仍在 Authentication 包）。

**`PasswordHasherOptions`（节 `XiHan:Authentication:PasswordHasher`）**

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Version` | `int` | `1` | 哈希算法版本号，用于平滑升级 |
| `Iterations` | `int` | `600000` | PBKDF2 迭代次数（OWASP 推荐 SHA256 ≥ 60 万） |
| `SaltSize` | `int` | `32` | 盐字节数 |
| `HashSize` | `int` | `32` | 输出哈希字节数 |
| `HashAlgorithm` | `HashAlgorithmName` | `SHA256` | 摘要算法 |

**`PasswordPolicyOptions`（节 `XiHan:Authentication:PasswordPolicy`）**

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `MinimumLength` / `MaximumLength` | `int` | `8` / `128` | 长度上下限 |
| `RequireUppercase` / `RequireLowercase` | `bool` | `true` / `true` | 需大写 / 小写 |
| `RequireDigit` / `RequireSpecialCharacter` | `bool` | `true` / `true` | 需数字 / 特殊字符 |
| `PasswordExpirationDays` | `int` | `0` | 密码过期天数（0 = 不过期） |
| `PasswordHistoryCount` | `int` | `5` | 禁止复用的历史密码条数 |
| `CustomBlacklist` | `List<string>` | `[]` | 自定义禁用词 |
| `MaxFailedAccessAttempts` | `int` | `5` | 允许的最大连续失败次数 |
| `LockoutDurationMinutes` | `int` | `30` | 锁定时长（分钟） |

```json
{
  "XiHan": {
    "Authentication": {
      "PasswordHasher": {
        "Iterations": 600000,
        "HashAlgorithm": "SHA256"
      },
      "PasswordPolicy": {
        "MinimumLength": 10,
        "RequireSpecialCharacter": true,
        "PasswordHistoryCount": 5,
        "MaxFailedAccessAttempts": 5,
        "LockoutDurationMinutes": 30
      }
    }
  }
}
```

## 使用示例

在任意服务里读取当前用户：

```csharp
public class OrderService(ICurrentUser currentUser)
{
    public void PlaceOrder()
    {
        if (!currentUser.IsAuthenticated) throw new InvalidOperationException("未登录");
        var userId = currentUser.UserId;      // long?
        var tenantId = currentUser.TenantId;  // long?
        var isAdmin = currentUser.IsInRole("admin");
    }
}
```

临时以另一主体身份执行（模拟登录 / 后台附身）：

```csharp
public class ImpersonationService(ICurrentPrincipalAccessor accessor)
{
    public void RunAs(ClaimsPrincipal target, Action action)
    {
        using (accessor.Change(target))   // 离开 using 自动恢复原主体
        {
            action();
        }
    }
}
```

哈希、校验与策略：

```csharp
public class AccountService(IPasswordHasher hasher, IPasswordPolicyService policy)
{
    public async Task<string> RegisterAsync(long userId, string password)
    {
        var check = policy.Validate(password);           // 强度校验 + 评分
        if (!check.IsValid) throw new InvalidOperationException(string.Join(";", check.Errors));

        if (await policy.IsPasswordReusedAsync(password, userId, historyCount: 5))
            throw new InvalidOperationException("不能复用最近使用过的密码");

        return hasher.HashPassword(password);            // "1:600000:SHA256:...:..."
    }
}
```

## 扩展点 / 自定义

- **主体来源**：Web 宿主一般会注册基于 `HttpContext.User` 的 `ICurrentPrincipalAccessor` 覆盖默认的 `ThreadCurrentPrincipalAccessor`。
- **密码历史持久化**：`DefaultPasswordHistoryStore` 是**进程内存实现**（静态 `ConcurrentDictionary`），仅适合开发/测试。生产务必自实现 `IPasswordHistoryStore`（读数据库最近 N 条哈希）并在 DI 里覆盖——因扩展方法用 `TryAddScoped`，只要你先注册就会生效。
- **默认实现是只读的**：`IPasswordHistoryStore` 接口只定义了读取方法，写入靠 `DefaultPasswordHistoryStore` 上的静态方法 `RecordPassword(userId, passwordHash, maxHistoryCount = 10)`——框架不会在密码修改成功后自动调用它，若使用默认内存实现，需应用层显式调用该静态方法落库，否则历史队列永远为空、`IsPasswordReusedAsync` 恒返回 `false`。
- **声明类型定制**：`XiHanClaimTypes` 的常量是可 `set` 的静态属性，可在应用启动早期改写以对齐外部身份提供方的声明命名（全局生效，务必在读取任何主体前设置）。

## 注意事项与最佳实践

- **`UserId`/`TenantId` 是 `long?`**：源自框架实体主键类型。读取时判空或用 `CurrentUserExtensions.GetUserId()`（内部 `Debug.Assert` 非空）。
- **历史复用检查传明文**：`IsPasswordReusedAsync` 入参是**新密码明文**——因为历史里存的是加盐哈希，同一明文每次哈希都不同，必须用 `VerifyPassword(历史哈希, 明文)` 逐条比对，不能直接比哈希字符串。
- **`NeedsRehash` 用于平滑升级**：调高 `Iterations` 或换 `HashAlgorithm` 后，旧密码仍可校验通过；在登录成功且 `NeedsRehash` 为真时用新参数重哈希并落库。
- **Blowfish 密钥长度**：> 56 字节（448 位）会抛 `ArgumentException`。Blowfish 属老算法，新业务优先选 AES；此处保留主要用于兼容既有数据。
- **加密辅助类是静态方法**：`BlowfishHelper` / `Sm2Helper` 不走 DI，直接静态调用；密钥管理由调用方负责。

## 依赖模块

- [XiHan.Framework.Core](./core)
- 第三方核心：**BouncyCastle.Cryptography** `2.6.2`（Blowfish / SM2 / SM3）

## 相关模块

- [XiHan.Framework.Authentication](./authentication)（在此之上实现 JWT / OAuth2 / MFA / 一次性验证码）
- [XiHan.Framework.Authorization](./authorization)（RBAC / Policy / ABAC 授权）
