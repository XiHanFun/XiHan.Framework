# XiHan.Framework.Authentication

> 认证：JWT 双令牌、OAuth2 外部登录（Google/GitHub/Gitee/QQ/微信/企业微信/飞书/钉钉，账号授权与扫码登录）、TOTP 两步验证、一次性验证码

- **NuGet**：`XiHan.Framework.Authentication`
- **模块类**：`XiHanAuthenticationModule`（`DependsOn` `XiHanSecurityModule`）
- **所在层**：基础设施层
- **关键依赖**：**Microsoft.AspNetCore.Authentication.JwtBearer**（唯一的 NuGet 依赖）+ ASP.NET Core 共享框架（`FrameworkReference`）

## 概述

这个包负责**认证**——确认“你是谁”。它在 [Security](./security) 基座之上提供：JWT 访问/刷新双令牌的签发与校验、OAuth2 第三方登录、TOTP 两步验证（2FA），以及邮箱/短信一次性验证码。认证通过后颁发携带身份声明的令牌，令牌里的声明再交给 [Authorization](./authorization) 判定“你能做什么”。

> 认证（Authentication）= 你是谁；授权（Authorization）= 你能做什么。两者分属不同的包。

## 何时使用

- 需要基于 JWT 的登录与令牌刷新（Access + Refresh 双令牌，HMAC-SHA256 签名）
- 需要接入 Google / GitHub / Gitee / QQ 第三方登录（OAuth2 外部登录）
- 需要接入微信 / 企业微信 / 飞书 / 钉钉登录，且账号授权与扫码登录都要覆盖
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
- **动态挂载 OAuth**：`AddXiHanOAuth` 仅在 `OAuthOptions.Enabled` 且存在提供商时，才注册 `IExternalLoginStore`，并按 `Providers[]` 逐条 `AddOAuth<TOptions, THandler>` 挂上对应方案（`ClientId` 为空的条目跳过，未知提供商类型跳过）

## 工作原理

**JWT 双令牌**：`GenerateAccessToken(claims)` 用对称密钥 HMAC-SHA256 签发访问令牌，同时生成一个 64 字节随机刷新令牌，并把 `(refreshToken → subject, 过期时间)` 存入 `IRefreshTokenStore`。刷新时 `RefreshAccessToken(accessToken, refreshToken)` 会以**忽略生命周期**的参数校验旧访问令牌拿回声明，再核对刷新令牌与 subject 绑定，通过后签发新令牌并**移除旧刷新令牌**（一次性轮换）。`ValidateToken` 额外强制算法必须是 HmacSha256，防算法混淆攻击。subject 取值优先级为 `sub` → `NameIdentifier` → `XiHanClaimTypes.UserId`。

**第三方登录**：八家提供商都是 ASP.NET Core 的 `OAuthHandler<TOptions>` 实现，由框架自研而非引入 provider 包。共同部分收在基类 `XiHanOAuthHandler<TOptions>`：默认的 `CreateTicketAsync` 走「Bearer 令牌 GET 用户信息接口 → `RunClaimActions` → `AfterClaimActionsAsync` 钩子 → `Events.CreatingTicket`」，另提供 JSON/表单请求与字段读取助手。偏离通用约定的提供商各自覆写 `BuildChallengeUrl`（授权地址参数）、`ExchangeCodeAsync`（令牌请求与响应字段名）、`CreateTicketAsync`（额外跳数与错误判定）。登录标识统一手动写入 `NameIdentifier`，优先取跨应用唯一的 union 类标识。

**一次性验证码**：`DistributedOneTimeCodeService` 用加密安全随机数生成纯数字码，以 `IDistributedCache`（接入 Redis 即多实例水平扩展）按 `xihan:auth:otc:{purpose}:{target}` 为键存储，可携带一段 `payload`（如换绑场景暂存的新邮箱）。消费时**先删除再校验**（消费即销毁），码不存在/过期/不匹配都返回失败，杜绝重放与穷举；比较用恒定时间函数。

## 核心能力

- **JWT 双令牌** `IJwtTokenService`：签发访问令牌 + 刷新令牌、校验、提取声明、判过期、刷新轮换；HMAC-SHA256；配置节 `XiHan:Authentication:Jwt`
- **OAuth2 外部登录**：内置 Google / GitHub / Gitee / QQ / 微信 / 企业微信 / 飞书 / 钉钉，配置驱动按需注册；各家头像 JSON 字段统一映射到 `urn:xihan:avatar` Claim；`IExternalLoginStore` 映射外部身份到内部用户；配置节 `XiHan:Authentication:OAuth`
- **账号授权与扫码登录**：微信 / 企业微信 / 飞书 / 钉钉用 `Mode` 选授权页与权限范围，同一提供商可用不同 `Name` + 相同 `Provider` 注册成两个 AuthenticationScheme
- **零第三方 OAuth 依赖**：八家的处理器全部自研，共用基类 `XiHanOAuthHandler<TOptions>`；本包不引入任何 `AspNet.Security.OAuth.*` 或 `Microsoft.AspNetCore.Authentication.Google` 包
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
| `OAuthProviderConfig` | 单个提供商：`Name`（=scheme）、`Provider`（提供商类型，留空取 `Name`）、`Mode`、`DisplayName`、`Enabled`、`ClientId`、`ClientSecret`、`AgentId`、`LoadMemberProfile`、`CorpId`、`Scopes`、`AuthorizationEndpoint`、`AuthorizationParameters`、`CallbackPath`（默认 `/signin-{name}`）；`ResolveProviderType()` 返回小写的提供商类型 |
| `OAuthProviderNames` | 提供商类型常量：`google`、`github`、`gitee`、`qq`、`weixin`（别名 `wechat`）、`workweixin`（别名 `wecom`）、`feishu`（别名 `lark`）、`dingtalk` |
| `OAuthLoginMode` | `QrCode`（默认）/ `Account`，只对微信、企业微信、飞书、钉钉生效；对微信系与钉钉只换授权页与权限范围，飞书是授权/令牌/用户信息三个接口成套切换 |
| `OAuthProviderEndpoints` | 八家的全部接口地址常量，区分登录方式的按 `QrCode`/`Account` 各列一条；含微信系要求的 `#wechat_redirect` 锚点 |
| `OAuthClaimTypes` | 八家的私有声明类型常量，`urn:{provider}:{field}` 命名；含钉钉的 `urn:dingtalk:corpid`（权限范围含 `corpid` 时才有） |
| `XiHanOAuthServiceCollectionExtensions` | `AddXiHanOAuth(services, configuration)`；常量 `ExternalSignInScheme = "ExternalCookie"` |

**提供商处理器（`XiHan.Framework.Authentication.OAuth.Handlers`，八家全部自研）**

| 类型 | 说明 |
| --- | --- |
| `XiHanOAuthProviderOptions` | 提供商选项基类，屏蔽与框架 `OAuthOptions` 的同名冲突 |
| `XiHanOAuthHandler<TOptions>` | 处理器基类：默认实现「Bearer 令牌 GET 用户信息 → 跑声明映射」，并提供 HTTP/JSON 助手与 `AfterClaimActionsAsync` 钩子。Google 直接用它，无需单独处理器 |
| `GoogleAuthenticationOptions` | Google，启用 PKCE |
| `GitHubAuthenticationOptions` / `GitHubAuthenticationHandler` | GitHub，邮箱私密时从 `user/emails` 补取主邮箱 |
| `GiteeAuthenticationOptions` / `GiteeAuthenticationHandler` | Gitee，同上 |
| `QQAuthenticationOptions` / `QQAuthenticationHandler` | QQ，`fmt=json` + 先换 openid 再取资料 |
| `WeixinAuthenticationOptions` / `WeixinAuthenticationHandler` | 微信，`appid`/`secret` + `errcode` + `#wechat_redirect` + 账号授权的 state 搬运 |
| `WorkWeixinAuthenticationOptions` / `WorkWeixinAuthenticationHandler` | 企业微信，企业凭证 → 成员身份 → 敏感资料三跳合并 |
| `FeishuAuthenticationOptions` / `FeishuAuthenticationHandler` | 飞书，passport 与开放平台两套端点按 `Mode` 成套切换 |
| `DingTalkAuthenticationOptions` / `DingTalkAuthenticationHandler` | 钉钉，JSON 令牌请求 + 小驼峰字段改写 + 私有请求头 |
| `WeixinShortState`（internal） | 微信系网页授权页的 state 搬运与还原 |
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

> 提供商类型取自 `Provider`，留空时回退到 `Name`（大小写与首尾空白不敏感），未知类型跳过不注册；`Name` 始终作为 AuthenticationScheme 名。各 provider 会把自家头像字段（Google `picture`、GitHub / Gitee `avatar_url`、QQ `figureurl_qq_2`、微信 `headimgurl`、企业微信 `avatar`、飞书 `avatar_url`、钉钉 `avatarUrl`）映射到统一的 `urn:xihan:avatar` Claim，回调端只读这一个。

**微信 / 企业微信 / 飞书 / 钉钉**

```json
"Providers": [
  { "Name": "wechat-qr", "Provider": "wechat", "Mode": "QrCode",  "ClientId": "开放平台网站应用 AppId", "ClientSecret": "..." },
  { "Name": "wechat-mp", "Provider": "wechat", "Mode": "Account", "ClientId": "公众号 AppId",          "ClientSecret": "..." },
  { "Name": "wecom",     "Provider": "wecom",  "Mode": "QrCode",  "ClientId": "CorpId", "ClientSecret": "应用 Secret", "AgentId": "1000002" },
  { "Name": "feishu",    "Provider": "feishu", "ClientId": "cli_xxx", "ClientSecret": "..." },
  { "Name": "dingtalk",  "Provider": "dingtalk", "Mode": "QrCode", "ClientId": "AppKey", "ClientSecret": "AppSecret", "CorpId": "可选" }
]
```

各提供商在两种登录方式下的授权页：

| 提供商 | `QrCode` | `Account` |
| --- | --- | --- |
| 微信 | `open.weixin.qq.com/connect/qrconnect`，`scope=snsapi_login` | `open.weixin.qq.com/connect/oauth2/authorize`，`scope=snsapi_userinfo`，带 `#wechat_redirect` |
| 企业微信 | `login.work.weixin.qq.com/wwlogin/sso/login`，带 `login_type=CorpApp` 与 `agentid` | `open.weixin.qq.com/connect/oauth2/authorize`，`scope=snsapi_privateinfo`，带 `agentid` 与 `#wechat_redirect` |
| 钉钉 | `login.dingtalk.com/oauth2/challenge.htm` | `login.dingtalk.com/oauth2/auth` |
| 飞书 | `passport.feishu.cn/suite/passport/oauth/authorize`（不分 `Mode`） | 同左 |

> 微信与企业微信的 `Scopes` 是**覆盖**语义——两种登录方式的 scope 互斥，不能在按登录方式推导出的范围上追加；其余提供商的 `Scopes` 是**追加**。`AuthorizationEndpoint` 可直接覆盖上表推导出的地址。

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
- **新增 OAuth 提供商**：内置分支覆盖 google/github/gitee/qq/weixin/workweixin/feishu/dingtalk。接入其它家按偏离程度递增：只有端点与声明映射不同 → 只写一个继承 `XiHanOAuthProviderOptions` 的 Options，直接搭 `XiHanOAuthHandler<TOptions>`；要多调一次接口补声明 → 覆写 `AfterClaimActionsAsync`；令牌或用户信息形态不同 → 覆写 `ExchangeCodeAsync` / `CreateTicketAsync`。最后在 `RegisterProvider` 加一个 `case`。
- **改写某一家的授权地址构造**：覆写 `BuildChallengeUrl`，参考 `WeixinAuthenticationHandler`（锚点 + state 搬运）与 `WorkWeixinAuthenticationHandler`（两种登录方式两套参数）。
- **远端失败的落地方式**：授权码无效等错误由 `RemoteAuthenticationHandler` 抛出，需要跳错误页时在对应 provider 的 `Events.OnRemoteFailure` 里处理。

## 注意事项与最佳实践

- **无向后兼容**：JWT/验证码格式为前向单一格式，异常输入一律 fail-closed（`ValidateToken`/`TryConsumeAsync` 返回 null/false 而非抛出）。
- **一次性验证码是“消费即销毁”**：`TryConsumeAsync` 读取后立即从缓存删除，校验失败也无法用同一枚码重试——UI 侧应引导重新发码。
- **多实例部署一次性验证码依赖分布式缓存**：宿主未接 Redis 时退化为进程内存，重启丢码、跨实例不共享；生产接 Redis。
- **`SecretKey` 必须配置且足够长**：为空或过短会导致签名不安全或运行期异常。
- **密码相关 Options 归属**：`PasswordHasherOptions` / `PasswordPolicyOptions` 类型在 [Security](./security)，但配置绑定在本包完成，节名同为 `XiHan:Authentication:*`。
- **微信两种登录方式是两个应用**：扫码属于开放平台网站应用、账号授权属于公众号，`ClientId` / `ClientSecret` 不通用，必须写成两条配置。
- **企业微信要填 `AgentId`**：扫码页与应用内授权页都要带。资料走 `auth/getuserdetail`（凭 `user_ticket`，需 `snsapi_privateinfo`）；姓名不在其中，要姓名得开 `LoadMemberProfile` 走 `cgi-bin/user/get`，读不到时姓名为空但**不影响登录**。
- **`Scopes` 默认是追加语义**：在提供商默认值之外追加，所以配置里只写增量（如 Gitee 只写 `user_info` 不会把默认的 `emails` 挤掉）。微信与企业微信例外：两种登录方式的 scope 互斥，配置非空时整体替换。
- **账号授权时微信授权地址里 `state=_oauthstate` 是正常的**：微信限制 `state` 长度，框架把真实状态挪进回调地址的 `_oauthstate` 参数，回调时还原。

## 依赖模块

- [XiHan.Framework.Core](./core)
- [XiHan.Framework.Security](./security)（密码哈希、当前用户、`XiHanClaimTypes`）
- 第三方核心：**Microsoft.AspNetCore.Authentication.JwtBearer** `10.0.11`（唯一的 NuGet 依赖）
- 其余 ASP.NET Core 能力（`OAuthHandler`、`IDistributedCache` 等）来自 `FrameworkReference Microsoft.AspNetCore.App`，因此本包只能被 ASP.NET Core 应用引用
- 八家 OAuth 提供商全部自研，不引入任何 `AspNet.Security.OAuth.*` 或 `Microsoft.AspNetCore.Authentication.Google` 包

## 相关模块

- [XiHan.Framework.Authorization](./authorization)（消费令牌声明做授权判定）
- [XiHan.Framework.Security](./security)
