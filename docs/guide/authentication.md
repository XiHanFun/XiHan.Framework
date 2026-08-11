# 认证

认证解决「你是谁」。这一章讲令牌怎么签发与校验、密码怎么存、第三方登录与二次验证怎么接、会话失效怎么表达。

「你能做什么」在 [授权](./authorization)。

## 在管道里的位置

```text
… → UseAuthentication → 租户解析 → 会话闸门 → UseAuthorization → 端点
      ↑ 你是谁              ↑ 哪个租户   ↑ 会话还有效吗  ↑ 你能做什么
```

顺序有讲究：租户解析要读令牌里的租户 claim，所以排在认证之后；授权判定要在租户上下文里进行，所以排在租户解析之后。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Authentication
```

```csharp
[DependsOn(typeof(XiHanAuthenticationModule))]
public class MyAppModule : XiHanModule { }
```

包内分六块：`Jwt`（令牌）、`Users`（认证流程与用户存储）、`OAuth`（第三方登录）、`Oidc`（OIDC 签发）、`Otp`（TOTP）、`OneTimeCode`（一次性验证码）。

## JWT 令牌

### 配置

配置节 `XiHan:Authentication:Jwt`：

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `SecretKey` | — | 签名密钥。**生产务必改为高强度随机值**，走环境变量或密钥库 |
| `Issuer` / `Audience` | — | 签发者 / 受众，校验时比对 |
| `AccessTokenExpirationMinutes` | `60` | 访问令牌有效期 |
| `RefreshTokenExpirationDays` | `7` | 刷新令牌有效期 |
| `ClockSkewMinutes` | `5` | 允许的时钟偏差，多节点部署必需 |

### `IJwtTokenService`

| 成员 | 说明 |
| --- | --- |
| `GenerateAccessToken(List<Claim>)` | 签发访问令牌，返回 `JwtTokenResult` |
| `GenerateRefreshToken()` | 生成刷新令牌（不透明随机串） |
| `ValidateToken(string)` | 校验并返回 `ClaimsPrincipal`，失败返回 `null` |
| `GetClaimsFromToken(string)` | 取出 Claim 列表 |
| `IsTokenExpired(string)` | 是否已过期 |
| `RefreshAccessToken(accessToken, refreshToken)` | 用旧访问令牌 + 刷新令牌换新的一套 |

刷新令牌的持久化走 `IRefreshTokenStore`——框架给了默认实现，需要落库时 `Replace` 掉。

::: tip 别把权限码冻结进令牌
把权限清单写进 JWT 有两个后果：授予或回收后要等令牌过期才生效；权限清单随令牌一起泄露。

推荐只放**身份信息**（用户、会话、租户、角色），权限交给服务端实时判定——判定可以走缓存快照，性能不是问题。见 [授权](./authorization)。
:::

::: warning 刷新端点必须匿名
访问令牌过期后再带着它去刷新，如果刷新端点本身要求认证就永远进不去。刷新端点应当 `[AllowAnonymous]`，并**同时校验旧访问令牌与刷新令牌**——两者成对才算合法。
:::

## 密码

### 哈希算法

配置节 `XiHan:Authentication:PasswordHasher`，算法是 **PBKDF2**：

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `Version` | `1` | 方案版本号，参数升级后老密码仍按旧版本校验 |
| `Iterations` | `600000` | 迭代次数，OWASP 对 PBKDF2-SHA256 的推荐量级 |
| `SaltSize` | `32` | 盐长度（字节） |
| `HashSize` | `32` | 输出长度（字节） |
| `HashAlgorithm` | `SHA256` | 哈希算法 |

存储格式是自描述串：

```text
version:iterations:algorithm:base64(salt):base64(hash)
```

自描述的好处是**参数可以随时调大而不影响存量密码**——每条哈希都记着自己是用什么参数算的。校验用定长比较抵抗时序攻击；参数变更后 `NeedsRehash` 会提示该密码需要在下次登录成功时透明重算。

### 认证流程

`IAuthenticationService` 是认证主流程的门面：

| 方法 | 用途 |
| --- | --- |
| `AuthenticateAsync(username, password)` | 主认证，返回 `AuthenticationResult` |
| `ValidatePasswordStrengthAsync(password, customBlacklist?)` | 密码强度校验 |
| `ChangePasswordAsync` / `ResetPasswordAsync` | 改密 / 重置 |
| `EnableTwoFactorAuthenticationAsync` / `DisableTwoFactorAuthenticationAsync` | 开关二次验证 |
| `VerifyTwoFactorCodeAsync` | 校验二次验证码 |
| `GenerateRecoveryCodesAsync` / `VerifyRecoveryCodeAsync` | 恢复码 |
| `RecordFailedLoginAttemptAsync` | 记录失败尝试（锁定与风控的输入） |

用户数据的读写走 `IUserStore`——框架只定义契约，**业务侧必须 `Replace` 成自己的实现**（通常是数据库）。

## 一次性验证码

`IOneTimeCodeService` 用于「签发 → 一次性消费」的场景：邮箱/短信登录验证码、改绑联系方式验证码等。

四条设计特征：

| 特征 | 说明 |
| --- | --- |
| 加密安全随机 | 用 `RandomNumberGenerator` 生成，不是 `Random` |
| 存分布式缓存 | 状态放 `IDistributedCache`，宿主接了 Redis 就天然支持多实例 |
| **消费即销毁** | **无论校验成功与否，读取后即删除**——杜绝重放与暴力穷举 |
| 可携带负载 | 如改绑场景暂存待生效的新邮箱/新手机号，消费成功后取回 |

::: warning 同「用途 + 目标」重复签发会覆盖旧码
用途标识（如 `auth:email-login`）参与存储键隔离。对同一个邮箱重复请求验证码，后一次会把前一次覆盖掉——用户拿旧码来验会失败。前端要做发送频率限制。
:::

## 第三方登录

配置节 `XiHan:Authentication:OAuth`：

| 键 | 说明 |
| --- | --- |
| `Enabled` | 总开关 |
| `FrontendCallbackUrl` | 第三方授权完成后跳回的前端页面 |
| `Providers[]` | 各提供商配置 |

每个 provider 的字段：`Name`（**内部标识，参与回调路由，不要随意改**）、`DisplayName`（前端展示名）、`Enabled`、`ClientId`、`ClientSecret`、`Scopes[]`。

包内建 `github` / `gitee` / `google` / `qq` 四个，按同样的结构可以扩展。绑定关系的读写走 `IExternalLoginStore`。

::: danger 首次第三方登录不要按邮箱并号
拿第三方返回的邮箱去匹配既有账号并直接登录，等于把「谁控制这个邮箱」的判断外包给了第三方。第三方邮箱未必经过验证，这会成为账号接管的入口。

安全做法是按 `(Provider, ProviderKey)` 精确定位；找不到就新建账号，让用户在登录态下主动完成绑定。
:::

## TOTP 二次验证

`IOtpService` 遵循 RFC 6238：

| 成员 | 说明 |
| --- | --- |
| `GenerateTotpSecret()` | 生成 Base32 密钥 |
| 生成 provisioning URI | 供认证器扫码，形如 `otpauth://totp/{issuer}:{account}?secret=...` |
| 校验验证码 | 带窗口容差，容忍客户端与服务端的时钟差 |

TOTP 由认证器本地生成，**服务端不需要下发**——这是它与邮箱/短信验证码在流程上的关键区别：判定需要二次验证时，TOTP 直接等用户输入，邮箱/短信则要先发码。

## 会话闸门：401 与 423

`XiHanSessionStateMiddleware` 位于认证之后、授权之前，判定委托给 `ISessionStateGate`（框架默认实现一律放行）。业务侧 `Replace` 后可产出两种结果：

| 结果 | 语义 | 客户端该怎么做 |
| --- | --- | --- |
| `401` | 会话已失效（登出 / 被踢 / 撤销 / 过期） | 尝试刷新令牌，失败则跳登录页 |
| **`423`** | **会话被锁定，身份仍然有效** | **引导解锁，不是跳登录页** |

::: tip 为什么框架不定义锁定原因
锁屏、风控挂起、强制改密、二次验证未完成都可能导致「身份有效但当前不能操作」。框架只提供 `423` 这个信号位，原因与解锁方式由应用侧定义。
:::

位置也是有讲究的：**在认证之后**（要读 `session_id` claim）、**在租户解析之后**（会话表通常是多租户实体，租户上下文没解析会被全局过滤器挡掉）、**在授权之前**（`423` / `401` 要先于权限评估短路，不能和 `403` 混淆）。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 多节点部署偶发令牌校验失败 | 时钟偏差；调大 `ClockSkewMinutes` 或给节点校时 |
| 刷新令牌一直失败 | 刷新端点没设匿名；或没有同时提交旧访问令牌与刷新令牌 |
| 验证码验不过 | 已被消费（读取即销毁）；或同目标重复签发把旧码覆盖了 |
| 匿名端点里拿不到用户 | 匿名端点不经过认证中间件，`ICurrentUser` 是空的 |
| 收到 `423` 却跳了登录页 | 客户端把 `423` 当成 `401` 处理了 |
| 改了 `Iterations` 后老用户登录失败 | 不应该失败——哈希串自描述，老密码按串里记录的旧参数校验。若真失败，检查是不是连存储格式一起改了 |

## 下一步

- [授权](./authorization)：权限码与判定链
- [数据加解密](./security)：哈希、签名、对称与非对称加密
- [Web 应用开发](./web)：中间件管道全貌
- [多租户](./multi-tenancy)：租户解析
- [Authentication 包](../packages/authentication)：完整 API 与配置项
