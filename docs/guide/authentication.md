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

每个 provider 的字段：

| 字段 | 说明 |
| --- | --- |
| `Name` | **AuthenticationScheme 名，参与回调路由（默认 `/signin-{Name}`），不要随意改** |
| `Provider` | 提供商类型；留空时取 `Name`。同一家要同时开两种登录方式时，两条配置用不同的 `Name`、相同的 `Provider` |
| `Mode` | `QrCode`（默认）/ `Account`，只对微信、企业微信、飞书、钉钉生效 |
| `DisplayName` | 前端展示名 |
| `Enabled` / `ClientId` / `ClientSecret` / `CallbackPath` | 同标准 OAuth2 |
| `Scopes[]` | 申请的权限范围，在提供商默认值之外**追加**；微信与企业微信例外，两种登录方式的范围互斥，配置非空时整体替换 |
| `AgentId` | 企业微信自建应用 AgentId |
| `LoadMemberProfile` | 企业微信是否额外读通讯录补姓名 |
| `CorpId` | 钉钉企业 CorpId，随授权请求带出；**要拿到用户选定的组织，还须在 `Scopes` 里加 `corpid`**，钉钉只在权限范围含它时才回传，框架据此写出 `urn:dingtalk:corpid` 声明 |
| `AuthorizationEndpoint` | 逃生舱：直接指定授权页地址，覆盖按 `Provider` + `Mode` 的推导 |
| `AuthorizationParameters` | 逃生舱：追加到授权地址上的任意参数，如 Google 的 `access_type` |

包内建八个：`google` / `github` / `gitee` / `qq` / `weixin` / `workweixin` / `feishu` / `dingtalk`（微信、企业微信、飞书分别可写成 `wechat` / `wecom` / `lark`）。绑定关系的读写走 `IExternalLoginStore`。

### 账号授权与扫码登录

两种方式**只差授权页地址与申请的权限范围**，换令牌与拉用户信息的接口是同一套，所以不需要另建一条登录路径——同一家注册两个 scheme 就行：

```json
"Providers": [
  { "Name": "wechat-qr", "Provider": "wechat", "Mode": "QrCode",  "ClientId": "开放平台网站应用 AppId", "ClientSecret": "..." },
  { "Name": "wechat-mp", "Provider": "wechat", "Mode": "Account", "ClientId": "公众号 AppId",          "ClientSecret": "..." }
]
```

各家两种方式落到的授权页：

| 提供商 | 扫码登录 | 账号授权 |
| --- | --- | --- |
| 微信 | 开放平台网站应用 `connect/qrconnect`，`scope=snsapi_login` | 公众号网页授权 `connect/oauth2/authorize`，`scope=snsapi_userinfo` |
| 企业微信 | `login.work.weixin.qq.com/wwlogin/sso/login` | 应用内网页授权 `connect/oauth2/authorize`，`scope=snsapi_privateinfo` |
| 钉钉 | `login.dingtalk.com/oauth2/challenge.htm` | `login.dingtalk.com/oauth2/auth` |
| 飞书 | `passport.feishu.cn/suite/passport/oauth/authorize` | `accounts.feishu.cn/open-apis/authen/v1/authorize` |

::: warning 微信两种方式用的是两个应用
扫码登录属于**开放平台网站应用**，账号授权属于**公众号**，AppId / AppSecret 不通用，所以要写成两条配置。
:::

::: warning 飞书两套端点不可交叉
扫码走 `passport.*`、账号授权走 `accounts/open-apis`，各自的授权、令牌、用户信息三个接口成套；一套拿到的授权码不能拿去另一套换令牌。框架按 `Mode` 整套切换，不必手工拼。
:::

::: tip 账号授权的地址里 `state=_oauthstate` 不是 bug
微信公众号与企业微信应用内的网页授权页限制 `state` 长度，容不下受保护的认证属性。框架把真实状态挪进回调地址的 `_oauthstate` 参数、`state` 位上只留一个哨兵，回调时再还原。只在账号授权时出现，扫码链路不受影响。
:::

### 实现来源

八家**全部由框架自研**，不依赖任何第三方 OAuth 提供商包——认证包现在只剩 `Microsoft.AspNetCore.Authentication.JwtBearer` 一个 NuGet 依赖。

| 分类 | 提供商 | 说明 |
| --- | --- | --- |
| 走通用形态 | Google | 直接用基类 `XiHanOAuthHandler<TOptions>`，无需单独的处理器 |
| 只多一步 | GitHub、Gitee | 覆写 `AfterClaimActionsAsync`，在用户信息没给出邮箱时补取一次 |
| 协议有偏离 | QQ、微信、企业微信、飞书、钉钉 | 各自覆写 `BuildChallengeUrl` / `ExchangeCodeAsync` / `CreateTicketAsync` |

新增一家的成本按偏离程度递增：只有端点不同 → 只写一个 `Options`；多一跳或字段名不同 → 再写一个 `Handler`；最后在 `RegisterProvider` 里加一个 `case`。

### 各家偏离 OAuth2 通用约定的地方

| 提供商 | 偏离点 |
| --- | --- |
| 微信 | 令牌接口用 `appid`/`secret` 而非 `client_id`/`client_secret`；用 `errcode` 而非 HTTP 状态码表达失败；授权地址必须以 `#wechat_redirect` 结尾 |
| 企业微信 | 换到的是**企业凭证**而不是用户令牌，成员身份要再用授权码换一次，敏感资料还要凭 `user_ticket` 取第三次 |
| 飞书 | 用响应体里的 `code`/`msg`（开放平台）或 `error`/`error_description`（passport）表达失败；开放平台把用户信息包在 `data` 节点里 |
| 钉钉 | 令牌接口收 JSON 体、返回 `accessToken`/`expireIn` 小驼峰字段；用户信息接口用 `x-acs-dingtalk-access-token` 私有头而非 `Authorization` |
| QQ | 令牌与用户标识接口默认返回表单文本与 JSONP，靠 `fmt=json` 换成纯 JSON；要先换 `openid` 再取资料 |

### 已知边界

| 边界 | 说明 |
| --- | --- |
| 企业微信要填 `AgentId` | 扫码页与应用内授权页都要带 |
| 企业微信默认拿不到姓名 | 授权链路只给 `userid`。要姓名就开 `LoadMemberProfile` 走通讯录读取；读不到时姓名为空，**不影响登录本身** |
| 登录标识优先取 union 类字段 | 微信 `unionid`、钉钉 `unionId`、飞书 `union_id`，缺失时退回 `openid`；企业微信取 `userid`，非企业成员退回 `openid` |
| 远端失败会抛异常 | 授权码无效、用户信息拉取失败等由 `RemoteAuthenticationHandler` 抛出；要变成跳转到错误页，需在 provider 的 `Events.OnRemoteFailure` 里处理 |
| `Scopes` 默认是追加语义 | 在提供商默认值之外追加，配置里只写增量即可；微信与企业微信例外，两种方式的范围互斥所以整体替换 |

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
