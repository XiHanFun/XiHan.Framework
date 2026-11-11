# 通行密钥

这一页讲怎样让用户用通行密钥（WebAuthn）登录：依赖方要做的两次仪式、框架替你做的核对、应用要自己保存的状态，以及浏览器侧的写法。读完你能在应用里加上「用通行密钥登录」，并知道每种失败意味着什么。阅读前先读[认证](/security/authentication)。

## 为什么需要

密码会被撞库、钓鱼和泄露，验证码依赖短信与邮件。通行密钥把一对密钥放在用户的设备里（或随账号同步），登录时设备先验证本人（指纹、面容、PIN），再用私钥签一个一次性的挑战；服务端只存公钥，被拖库也没有可用的秘密。浏览器把签名限定在注册时的域名下，钓鱼站点拿不到可用的签名。

服务端的工作是协议层的：生成选项、核对客户端数据、解析 CBOR 编码的认证器数据与 COSE 公钥、验签、看签名计数。这些与业务无关，框架做成能力 `security.webauthn@1`，参考实现 `Security.WebAuthn`；凭据存在哪里、属于谁、能不能停用，是应用的业务。

## 两次仪式

| 仪式 | 服务端第一步 | 浏览器 | 服务端第二步 |
| --- | --- | --- | --- |
| 注册 | `CreateRegistrationOptions(user, existing)`，保存挑战 | `navigator.credentials.create` | `VerifyRegistration(response, challenge)`，存下凭据 ID、公钥与签名计数 |
| 认证 | `CreateAuthenticationOptions(allowed)`，保存挑战 | `navigator.credentials.get` | 按凭据 ID 找到存储的凭据，`VerifyAuthentication(response, challenge, stored, expectedUserHandle)`（先认出用户时传他，免用户名传 `null`），更新签名计数 |

挑战是一次性的秘密：存进有时限的缓存，第二步取出即删，过期或取不到就让用户重来。时限不短于 `Timeout`（缺省 5 分钟）。

免用户名登录时 `allowed` 为空，浏览器列出本站的全部通行密钥让用户选；响应必须带 `userHandle`，核对要求它等于存储的凭据所属用户，登录的用户取核对结果的 `UserHandle`，不直接取响应里的句柄。先填账号或作为第二因素时，把已认出的用户传给 `expectedUserHandle`，凭据不属于他即拒绝。用户句柄用不含个人信息的标识（如用户 ID 的字节），1 到 64 字节。

## 浏览器侧

选项按 camelCase 序列化后直接交给浏览器，响应用 `toJSON()` 交回，不需要手写 base64url 转换：

```javascript
const options = PublicKeyCredential.parseRequestOptionsFromJSON(await (await fetch("/api/passkeys:login-options", { method: "POST" })).json());
const credential = await navigator.credentials.get({ publicKey: options });
await fetch("/api/passkeys:login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(credential.toJSON()) });
```

## 失败与原因

核对按 RFC-0008「WebAuthn」的顺序进行，第一处不符即返回，错误参数 `reason` 说明是哪一处：

| 错误码 | 含义 | 常见原因 |
| --- | --- | --- |
| `SECURITY_VALIDATION_006` | 响应解析不了 | 前端改动了响应，或把 `rawId` 当作二进制再编码了一遍 |
| `SECURITY_VALIDATION_007` | 公钥算法不支持 | 认证器只给了 EdDSA；选项里列出的算法它都不支持时，浏览器本该拒绝 |
| `SECURITY_DENIED_005` | 核对不通过 | `challenge`：挑战过期后重放或串了会话；`origin`：来源没配进 `Origins`；`rp_id`：域名与 RP ID 不配套；`user_verified`：认证器没做本机验证；`signature`：响应被篡改 |
| `SECURITY_DENIED_006` | 签名计数没有增加 | 认证器可能被复制：提醒本人，必要时停用这把凭据 |

对外给用户的提示不必区分这些原因；日志里记下 `reason` 便于排查。

## 测试

[Testing](/packages/testing) 的 `WebAuthnTestAuthenticator` 在内存里持有一把私钥，按真实格式生成注册与认证响应；`WebAuthnTestCeremony` 的每一项都能单独篡改（挑战、来源、RP ID、标志位、签名、用户句柄），用来覆盖每种失败。

```csharp
using var authenticator = new WebAuthnTestAuthenticator();
var options = ceremonies.CreateRegistrationOptions(user, []);
var response = authenticator.Register(new WebAuthnTestCeremony { Challenge = options.Challenge, Origin = "https://example.com", RelyingPartyId = "example.com" });
var registered = ceremonies.VerifyRegistration(response, options.Challenge).ValueOrThrow();
```

## 相关包与下一步

- 包参考：[Security.WebAuthn](/packages/security-webauthn)、[Testing](/packages/testing)
- 规范：RFC-0008《认证与授权》「WebAuthn」；ADR-0170
- 下一步：[授权与权限](/security/authorization)
