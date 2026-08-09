# XiHan.Framework.Authentication

> 认证：JWT 双令牌、OAuth2 外部登录（Google/GitHub/Gitee/QQ）、TOTP 两步验证、一次性验证码

- **NuGet**：`XiHan.Framework.Authentication`
- **模块类**：`XiHanAuthenticationModule`（`DependsOn` `XiHanSecurityModule`）
- **所在层**：基础设施层
- **关键依赖**：**Microsoft.AspNetCore.Authentication.JwtBearer** / **.Google**、**AspNet.Security.OAuth.GitHub** / **.Gitee** / **.QQ**、**Microsoft.Extensions.Caching.Abstractions**

## 概述

这个包负责**认证**——确认“你是谁”。它在 [Security](./security) 基座之上提供：JWT 访问/刷新双令牌的签发与校验、OAuth2 第三方登录、TOTP 两步验证（2FA），以及邮箱/短信一次性验证码。认证通过后颁发携带身份声明的令牌，令牌里的声明再交给 [Authorization](./authorization) 判定“你能做什么”。

> 认证（Authentication）= 你是谁；授权（Authorization）= 你能做什么。两者分属不同的包。

## 何时使用

- 需要基于 JWT 的登录与令牌刷新（Access + Refresh 双令牌，HMAC-SHA256 签名）
- 需要接入 Google / GitHub / Gitee / QQ 第三方登录（OAuth2 外部登录）
- 需要两步验证（TOTP，兼容 Google Authenticator 等）
- 需要邮箱/短信一次性验证码（登录码、换绑验证等，签发一次消费即销毁）
- 需要账号密码认证编排（复用 Security 的哈希与策略，含失败锁定）

::: warning 能力范围
本包实现的第三方登录是标准 **OAuth2 外部登录**，**不包含** OIDC 联合登录，也**不包含 SSO 单点登录服务端**（本框架不充当 IdP/授权服务器）。请勿据模板化 README 假设存在 OIDC/SSO。
:::

## 安装与启用

```bash
dotnet add package XiHan.Framework.Authentication
```

```csharp
[DependsOn(typeof(XiHanAuthenticationModule))]
public class MyModule : XiHanModule { }
```

模块 `ConfigureServices` 调用 `AddXiHanAuthentication(config)`，完成：

- **绑定配置**：`PasswordHasherOptions` / `PasswordPolicyOptions`（来自 Security）、`JwtOptions`、`OtpOptions`、`OAuthOptions`
- **注册服务（`TryAdd`）**：
  - `IPasswordHasher` → `PasswordHasher`（Singleton）
  - `IRefreshTokenStore` → `InMemoryRefreshTokenStore`（Singleton）
  - `IJwtTokenService` → `JwtTokenService`（Singleton）
  - `IOtpService` → `OtpService`（Singleton）
  - `IOneTimeCodeService` → `DistributedOneTimeCodeService`（Singleton）
  - `IUserStore` → `DefaultUserStore`（Scoped）
  - `IAuthenticationService` → `DefaultAuthenticationService`（Scoped）
- **动态挂载 OAuth**：`AddXiHanOAuth` 仅在 `OAuthOptions.Enabled` 且存在提供商时，才注册 `IExternalLoginStore` 与各 provider 的 `AuthenticationBuilder`

## 工作原理

**JWT 双令牌**：`GenerateAccessToken(claims)` 用对称密钥 HMAC-SHA256 签发访问令牌，同时生成一个 64 字节随机刷新令牌，并把 `(refreshToken → subject, 过期时间)` 存入 `IRefreshTokenStore`。刷新时 `RefreshAccessToken(accessToken, refreshToken)` 会以**忽略生命周期**的参数校验旧访问令牌拿回声明，再核对刷新令牌与 subject 绑定，通过后签发新令牌并**移除旧刷新令牌**（一次性轮换）。`ValidateToken` 额外强制算法必须是 HmacSha256，防算法混淆攻击。subject 取值优先级为 `sub` → `NameIdentifier` → `XiHanClaimTypes.UserId`。

**一次性验证码**：`DistributedOneTimeCodeService` 用加密安全随机数生成纯数字码，以 `IDistributedCache`（接入 Redis 即多实例水平扩展）按 `xihan:auth:otc:{purpose}:{target}` 为键存储，可携带一段 `payload`（如换绑场景暂存的新邮箱）。消费时**先删除再校验**（消费即销毁），码不存在/过期/不匹配都返回失败，杜绝重放与穷举；比较用恒定时间函数。

## 核心能力

- **JWT 双令牌** `IJwtTokenService`：签发访问令牌 + 刷新令牌、校验、提取声明、判过期、刷新轮换；HMAC-SHA256；配置节 `XiHan:Authentication:Jwt`
- **OAuth2 外部登录**：内置 Google / GitHub / Gitee / QQ，配置驱动按需注册；各家头像 JSON 字段统一映射到 `urn:xihan:avatar` Claim；`IExternalLoginStore` 映射外部身份到内部用户；配置节 `XiHan:Authentication:OAuth`
- **两步验证（TOTP/HOTP）** `IOtpService`：生成 TOTP 密钥与 `otpauth://` 二维码 URI、生成/校验动态码、HOTP 计数器变体、生成备用恢复码；配置节 `XiHan:Authentication:Otp`
- **一次性验证码** `IOneTimeCodeService`：邮箱/短信验证码签发与一次性消费，基于 `IDistributedCache`，可携带 payload
- **密码认证编排** `IAuthenticationService`：账号密码认证、改密/重置、2FA 启停与校验、恢复码、失败锁定，复用 Security 的哈希与策略

## 主要 API / 类型

### JWT

| 类型 | 关键方法 / 说明 |
| --- | --- |
| `IJwtTokenService` / `JwtTokenService` | `JwtTokenResult GenerateAccessToken(List<Claim>)`、`string GenerateRefreshToken()`、`ClaimsPrincipal? ValidateToken(string)`、`List<Claim>? GetClaimsFromToken(string)`、`bool IsTokenExpired(string)`、`JwtTokenResult? RefreshAccessToken(string accessToken, string refreshToken)` |
| `JwtTokenResult` | `AccessToken`、`RefreshToken`、`TokenType`（`"Bearer"`）、`ExpiresIn`（秒）、`IssuedAt`、`ExpiresAt` |
| `JwtOptions` | JWT 配置（配置节 `XiHan:Authentication:Jwt`） |
| `IRefreshTokenStore` / `InMemoryRefreshTokenStore` | `void Save(string token, string? subject, DateTime expiresAt)`、`bool Validate(string token, string? subject = null)`、`void Remove(string token)`（默认内存实现，生产替换） |

### OAuth2

| 类型 | 说明 |
| --- | --- |
| `OAuthOptions` | 全局配置：`Enabled`、`FrontendCallbackUrl`、`Providers`；常量 `AvatarClaimType = "urn:xihan:avatar"`（配置节 `XiHan:Authentication:OAuth`） |
| `OAuthProviderConfig` | 单个提供商：`Name`（=scheme）、`DisplayName`、`Enabled`、`ClientId`、`ClientSecret`、`Scopes`、`CallbackPath`（默认 `/signin-{name}`） |
| `IExternalLoginStore` | `Task<long?> FindUserIdAsync(provider, providerKey, tenantId?)`、`Task CreateAsync(userId, ExternalLoginInfo, tenantId?)`、`Task RemoveAsync(userId, provider)`（业务层实现数据库持久化） |
| `ExternalLoginInfo` | `Provider`、`ProviderKey`、`DisplayName`、`Email`、`AvatarUrl` |

### MFA / 一次性验证码

| 类型 | 关键方法 / 说明 |
| --- | --- |
| `IOtpService` / `OtpService` | `string GenerateTotpSecret()`、`string GenerateTotpUri(secret, issuer, account)`、`string GenerateTotpCode(secret)`、`bool VerifyTotpCode(secret, code)`、`GenerateHotpCode(secret, counter)` / `VerifyHotpCode(...)`、`List<string> GenerateRecoveryCodes(int count = 10)` |
| `OtpOptions` | TOTP 参数（配置节 `XiHan:Authentication:Otp`） |
| `TwoFactorSetupResult` | `Secret`、`QrCodeUri`、`RecoveryCodes`、`ManualEntryKey` |
| `IOneTimeCodeService` / `DistributedOneTimeCodeService` | `Task<OneTimeCodeIssueResult> IssueAsync(purpose, target, payload?, OneTimeCodeOptions?, ct)`、`Task<OneTimeCodeConsumeResult> TryConsumeAsync(purpose, target, code, ct)` |
| `OneTimeCodeOptions` | `CodeLength`（默认 6，允许 4–10）、`ExpiresInSeconds`（默认 600） |
| `OneTimeCodeIssueResult` / `OneTimeCodeConsumeResult` | `record`：`(Code, ExpiresInSeconds)` / `(Succeeded, Payload)` |

### 密码认证编排

| 类型 | 说明 |
| --- | --- |
| `IAuthenticationService` / `DefaultAuthenticationService` | `AuthenticateAsync`、`ValidatePasswordStrengthAsync`、`ChangePasswordAsync`、`ResetPasswordAsync`、`EnableTwoFactorAuthenticationAsync`、`VerifyTwoFactorCodeAsync`、`DisableTwoFactorAuthenticationAsync`、`GenerateRecoveryCodesAsync`、`VerifyRecoveryCodeAsync`、`RecordFailedLoginAttemptAsync`、`ResetFailedLoginAttemptsAsync`、`IsAccountLockedAsync` |
| `AuthenticationResult` | `Succeeded`、`UserId`、`Username`、`TokenResult`、`RequiresTwoFactor`、`IsLockedOut`、`LockoutEnd`、`ErrorMessage`；工厂 `Success` / `Failure` / `RequiresTwoFactorAuthentication` / `LockedOut` |
| `IUserStore` / `DefaultUserStore` | 用户读写、失败次数与锁定时间读写（默认内存实现，生产替换为数据库实现） |
| `UserInfo` | 用户数据载体：`UserId`、`Username`、`PasswordHash`、`Email`、`PhoneNumber`、`TwoFactorEnabled` / `TwoFactorSecret`、`RecoveryCodes`、`IsLocked` / `LockoutEnd` / `FailedLoginAttempts`、`LastLoginTime`、`PasswordChangedTime`、`IsActive` |

## 配置

**`JwtOptions`（节 `XiHan:Authentication:Jwt`）**

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `SecretKey` | `string` | `""` | 对称签名密钥（HMAC-SHA256，务必配置足够长度） |
| `Issuer` / `Audience` | `string` | `""` | 发行者 / 受众 |
| `AccessTokenExpirationMinutes` | `int` | `60` | 访问令牌有效期（分钟） |
| `RefreshTokenExpirationDays` | `int` | `7` | 刷新令牌有效期（天） |
| `ValidateIssuer` / `ValidateAudience` / `ValidateLifetime` | `bool` | `true` | 校验开关 |
| `ClockSkewMinutes` | `int` | `5` | 允许时钟偏移（分钟） |

**`OtpOptions`（节 `XiHan:Authentication:Otp`）**

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `SecretKeyLength` | `int` | `32` | 密钥字节长度 |
| `Digits` | `int` | `6` | 动态码位数 |
| `TimeStep` | `int` | `30` | 时间步长（秒） |
| `AllowedSkew` | `int` | `1` | 允许前后偏移窗口数 |
| `EnableRecoveryCodes` | `bool` | `true` | 是否启用恢复码 |
| `RecoveryCodesCount` | `int` | `10` | 恢复码数量 |

**`OAuthOptions`（节 `XiHan:Authentication:OAuth`）**

```json
{
  "XiHan": {
    "Authentication": {
      "Jwt": {
        "SecretKey": "请替换为足够长的随机密钥",
        "Issuer": "XiHan",
        "Audience": "XiHanClient",
        "AccessTokenExpirationMinutes": 60,
        "RefreshTokenExpirationDays": 7
      },
      "OAuth": {
        "Enabled": true,
        "FrontendCallbackUrl": "https://app.example.com/oauth/callback",
        "Providers": [
          {
            "Name": "github",
            "DisplayName": "GitHub",
            "Enabled": true,
            "ClientId": "xxx",
            "ClientSecret": "yyy",
            "Scopes": ["read:user"]
          }
        ]
      }
    }
  }
}
```

> OAuth 提供商名 `Name` 仅识别 `google` / `github` / `gitee` / `qq`（大小写不敏感），未知名称跳过；`Name` 同时作为 AuthenticationScheme 名。各 provider 会把自家头像字段（Google `picture`、GitHub `avatar_url`、Gitee `avatar_url`、QQ `figureurl_qq_2`）映射到统一的 `urn:xihan:avatar` Claim，回调端只读这一个。

## 使用示例

签发访问令牌：

```csharp
public class LoginService(IJwtTokenService jwt)
{
    public JwtTokenResult IssueToken(long userId, string userName)
    {
        var claims = new List<Claim>
        {
            new(XiHanClaimTypes.UserId, userId.ToString()),
            new(XiHanClaimTypes.UserName, userName),
        };
        return jwt.GenerateAccessToken(claims);  // 内含 AccessToken + RefreshToken
    }

    public JwtTokenResult? Refresh(string accessToken, string refreshToken)
        => jwt.RefreshAccessToken(accessToken, refreshToken);  // 失败返回 null
}
```

签发并消费一次性验证码（邮箱登录码）：

```csharp
public class EmailLoginService(IOneTimeCodeService otc /*, 你的邮件发送器 */)
{
    public async Task SendAsync(long tenantId, string email)
    {
        var result = await otc.IssueAsync("auth:email-login", $"{tenantId}:{email}");
        // 通过邮件通道下发 result.Code，有效 result.ExpiresInSeconds 秒
    }

    public async Task<bool> VerifyAsync(long tenantId, string email, string code)
    {
        var r = await otc.TryConsumeAsync("auth:email-login", $"{tenantId}:{email}", code);
        return r.Succeeded;   // 无论成败，该码此刻已销毁
    }
}
```

启用 TOTP 两步验证：

```csharp
public class MfaService(IOtpService otp)
{
    public TwoFactorSetupResult Setup(string account)
    {
        var secret = otp.GenerateTotpSecret();
        return new TwoFactorSetupResult
        {
            Secret = secret,
            ManualEntryKey = secret,
            QrCodeUri = otp.GenerateTotpUri(secret, "XiHan", account),  // otpauth://...
            RecoveryCodes = otp.GenerateRecoveryCodes(10),
        };
    }
}
```

## 扩展点 / 自定义

- **刷新令牌存储**：`InMemoryRefreshTokenStore` 是进程内存实现，多实例部署或需要吊销时应自实现 `IRefreshTokenStore`（Redis/数据库）并在 DI 覆盖（扩展方法用 `TryAddSingleton`）。
- **用户存储**：`DefaultUserStore` 仅供开发/测试；生产必须实现 `IUserStore`（读写真实用户表、失败次数、锁定时间）覆盖它，否则 `IAuthenticationService` 无真实数据可依。
- **外部登录持久化**：`IExternalLoginStore` 需业务层实现，把 `(provider, providerKey)` 映射到内部用户并记录绑定。
- **新增 OAuth 提供商**：内置分支只覆盖 google/github/gitee/qq；接入其它家需扩展 `RegisterProvider` 逻辑或另建注册路径。

## 注意事项与最佳实践

- **无向后兼容**：JWT/验证码格式为前向单一格式，异常输入一律 fail-closed（`ValidateToken`/`TryConsumeAsync` 返回 null/false 而非抛出）。
- **一次性验证码是“消费即销毁”**：`TryConsumeAsync` 读取后立即从缓存删除，校验失败也无法用同一枚码重试——UI 侧应引导重新发码。
- **多实例部署一次性验证码依赖分布式缓存**：宿主未接 Redis 时退化为进程内存，重启丢码、跨实例不共享；生产接 Redis。
- **`SecretKey` 必须配置且足够长**：为空或过短会导致签名不安全或运行期异常。
- **密码相关 Options 归属**：`PasswordHasherOptions` / `PasswordPolicyOptions` 类型在 [Security](./security)，但配置绑定在本包完成，节名同为 `XiHan:Authentication:*`。

## 依赖模块

- [XiHan.Framework.Core](./core)
- [XiHan.Framework.Security](./security)（密码哈希、当前用户、`XiHanClaimTypes`）
- 第三方核心：**Microsoft.AspNetCore.Authentication.JwtBearer** / **.Google** `10.0.9`、**AspNet.Security.OAuth.GitHub** / **.Gitee** / **.QQ** `10.0.0`、**Microsoft.Extensions.Caching.Abstractions** `10.0.9`

## 相关模块

- [XiHan.Framework.Authorization](./authorization)（消费令牌声明做授权判定）
- [XiHan.Framework.Security](./security)
