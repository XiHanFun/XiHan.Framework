来源：https://framework.docs.xihanfun.com/packages/security-webauthn

# XiHan.Framework.Security.WebAuthn

通行密钥（WebAuthn）的依赖方实现：生成注册与认证仪式的选项，按 RFC-0008「WebAuthn」的顺序核对浏览器交回的响应，提供 `security.webauthn@1`。适用于要做免密码、免用户名登录的应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Security.WebAuthn` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `security.webauthn@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `WebAuthnSecurityFeature`（清单 `xihan.security.webauthn`，`role: provider`） |
| 配置节 | `XiHan:Security:WebAuthn` |
| 依赖 | `XiHan.Framework.Security.Abstractions`、`System.Formats.Cbor`；公开面不暴露第三方类型 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

`WebAuthnSecurityFeature` 登记单例 `WebAuthnCeremonies`（ADR-0170）。它是无状态的：

- 挑战由应用保存到仪式结束（一次性、有时限），凭据由应用存储；框架不定义凭据存储，凭据的归属、命名、启停与租户是应用的业务。
- 选项与响应和 WebAuthn Level 3 的 JSON 同形，二进制一律 base64url（无填充）；浏览器侧用 `PublicKeyCredential.parseCreationOptionsFromJSON` / `parseRequestOptionsFromJSON` 还原选项，用 `credential.toJSON()` 交回响应。
- 证明一律请求 `none`，证明声明不校验：公钥是否真由认证器持有，由之后每次认证的签名证明。
- 算法依次是 ES256（P-256）、PS256、RS256；EdDSA 不支持（BCL 没有 Ed25519）。

## 何时使用

- 登录页「用通行密钥登录」，免用户名、免密码：用本包，用户验证与可发现凭据都按缺省的 `Required`。
- 只想把通行密钥当第二因素：同样用本包，`ResidentKey` 设为 `Discouraged`，认证时把用户已有的凭据放进 `allowCredentials`。
- 需要证明信任链或元数据服务（只允许特定型号的安全密钥）：本包不做，自己在 `WebAuthnRegisteredCredential.Aaguid` 之上加校验并请求别的证明方式。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Security.WebAuthn --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.WebAuthn;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<WebAuthnSecurityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

## 配置

| 键（`XiHan:Security:WebAuthn`） | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `RelyingPartyId` | `string` | 必填 | 可注册的域名（如 `example.com`、本地开发的 `localhost`），不带协议、端口与路径 |
| `RelyingPartyName` | `string` | 同 `RelyingPartyId` | 认证器上显示的名称 |
| `Origins` | `string[]` | 必填 | 允许的来源，形如 `scheme://host[:port]`；主机必须等于 RP ID 或是它的子域，`https` 之外只允许 `localhost` 的 `http` |
| `UserVerification` | `Required` / `Preferred` / `Discouraged` | `Required` | 为 `Required` 时核对响应带 UV 标志 |
| `ResidentKey` | `Required` / `Preferred` / `Discouraged` | `Required` | 是否要求可发现凭据 |
| `TimeoutSeconds` | `int` | `300` | 仪式的超时，写进选项交给浏览器 |
| `ChallengeSize` | `int` | `32` | 挑战的字节数，16 到 64 |

```json
{
  "XiHan": {
    "Security": {
      "WebAuthn": {
        "RelyingPartyId": "example.com",
        "RelyingPartyName": "Example",
        "Origins": [ "https://example.com", "https://admin.example.com" ]
      }
    }
  }
}
```

配置在装配时读一次，缺少必填项或值不合法即启动失败。前端开发服务器与后端端口不同时，来源写浏览器地址栏里的那个（如 `http://localhost:9100`）。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `WebAuthnSecurityFeature` | 特性入口 |
| `WebAuthnOptions` | 上表的配置；`FromConfiguration` 读取并核对 |
| `WebAuthnCeremonies` | `CreateRegistrationOptions`、`CreateAuthenticationOptions`、`VerifyRegistration`、`VerifyAuthentication` |
| `WebAuthnCreationOptions` / `WebAuthnRequestOptions` | 交给浏览器的选项 |
| `WebAuthnRegistrationResponse` / `WebAuthnAuthenticationResponse` | 浏览器交回的响应 |
| `WebAuthnRegisteredCredential` | 注册核对通过的凭据：凭据 ID、COSE 公钥、算法、签名计数、AAGUID、BE / BS / UV、传输方式 |
| `WebAuthnStoredCredential` | 应用存储的凭据，认证时交给核对 |
| `WebAuthnVerifiedAssertion` | 认证核对通过的结果：新的签名计数、UV、BS |

测试里用 [Testing](./testing) 的 `WebAuthnTestAuthenticator` 生成合法或篡改过的响应。

## 使用示例

注册：选项交给浏览器，挑战存进缓存；响应回来后取出挑战（只用一次）再核对。

```csharp
public sealed class PasskeyRegistration(WebAuthnCeremonies ceremonies, IPasskeyStore store, IChallengeStore challenges)
{
    public async Task<WebAuthnCreationOptions> BeginAsync(User user, CancellationToken ct)
    {
        var existing = await store.ListAsync(user.Id, ct);
        var options = ceremonies.CreateRegistrationOptions(
            new WebAuthnUserEntity(Base64Url.EncodeToString(user.Id.ToByteArray()), user.UserName, user.DisplayName),
            [.. existing.Select(c => new WebAuthnCredentialDescriptor("public-key", c.CredentialId, c.Transports))]);
        await challenges.SaveAsync(user.Id, options.Challenge, ceremonies.Options.Timeout, ct);
        return options;
    }

    public async Task<Result> CompleteAsync(User user, WebAuthnRegistrationResponse response, CancellationToken ct)
    {
        var challenge = await challenges.TakeAsync(user.Id, ct);
        if (challenge is null)
        {
            return Error.Validation("APP_VALIDATION_001", "app.passkey.expired");
        }

        var registered = ceremonies.VerifyRegistration(response, challenge);
        return registered.TryGetValue(out var credential) ? await store.AddAsync(user.Id, credential, ct) : registered.Error!;
    }
}
```

认证：免用户名时 `allowCredentials` 为空；响应回来后按凭据 ID 找到存储的凭据，核对通过即更新签名计数。

```csharp
var stored = await store.FindAsync(response.Id, ct);
var verified = ceremonies.VerifyAuthentication(response, challenge, new WebAuthnStoredCredential(stored.CredentialId, stored.PublicKey, stored.SignCount, stored.UserHandle));
if (verified.TryGetValue(out var assertion))
{
    await store.UpdateSignCountAsync(stored.CredentialId, assertion.SignCount, ct);
}
```

## 行为与限制

- 核对的顺序与每条失败的 `reason` 见 RFC-0008「WebAuthn」：解析不了是 `SECURITY_VALIDATION_006`，算法不支持是 `SECURITY_VALIDATION_007`，核对不通过是 `SECURITY_DENIED_005`。
- 签名计数：新旧值有一个不为 0 时新值必须大于旧值，否则 `SECURITY_DENIED_006`，参数 `stored`、`received`。它是认证器可能被复制的信号，应用应提醒本人或停用这把凭据；都为 0 的认证器（多数同步的通行密钥）不计数，放行。
- 挑战定长比较；客户端数据的哈希在签名里，挑战与来源受签名保护。
- 存储的凭据带用户句柄、响应也带 `userHandle` 时两者必须相同，拿别人的凭据 ID 冒充会被拒绝。
- 客户端扩展结果不读；`crossOrigin` 为真（跨源的 iframe 里发起）一律拒绝。
- 应用给的期望挑战或存储的凭据 ID 不是 base64url 抛 `ArgumentException`：那是调用方的错误。

## 相关

- [安全鉴权 · 通行密钥](/security/webauthn)
- [Security.Abstractions](./security-abstractions)
- 规范：RFC-0008《认证与授权》；ADR-0170；一致性向量 `conformance/vectors/security/webauthn.json`
