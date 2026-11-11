# ADR-0170 WebAuthn 校验进框架

## 状态

已采纳（RFC-0008 增补「WebAuthn」一节，新能力 `security.webauthn@1`）。

## 日期

2026-11-12

## 背景

BasicApp 要做通行密钥登录：登录页免用户名、免密码，凭认证器里的私钥签名登录。

- 服务端要做两件事：生成注册与认证的选项（挑战、依赖方、算法），核对浏览器交回的响应（客户端数据、认证器数据、CBOR 编码的证明对象与 COSE 公钥、签名、签名计数）。
- 这是协议层的通用能力，与业务无关；框架只提供通用能力与 Provider，令牌校验（`Security.Jwt`、`Security.OpenIdConnect`）也在框架里。
- 框架里没有 WebAuthn，.NET 内建库也没有（ASP.NET Core Identity 10 的通行密钥绑在 `UserManager` 上，框架不用它）。

## 备选

1. **应用直接引用 Fido2NetLib**：否决。
   - 带进 NSec（原生 libsodium）与 Microsoft.IdentityModel，还有全套证明格式与元数据服务，依赖方不要求证明时都用不上。
   - 每个应用各自接一遍，校验顺序与失败语义没有统一的契约与一致性向量。
2. **放进 `Security.Abstractions`**：否决。核对要解析 CBOR，抽象包零第三方依赖。
3. **新实现包 `Security.WebAuthn`，只依赖 `System.Formats.Cbor`**：采纳。
   - CBOR 用微软自己的包，签名用 BCL 的 `ECDsa` 与 `RSA`，不带原生库，可 AOT。
   - 只做依赖方不要求证明时需要的部分：证明一律请求 `none`，证明声明不校验，公钥的持有由之后每次认证的签名证明。

## 决策

1. **能力与包**：`security.webauthn@1`（`SecurityCapabilities.WebAuthn`），由 `Security.WebAuthn` 的 `WebAuthnSecurityFeature` 提供，可不装。
2. **无状态**：`WebAuthnCeremonies` 只生成选项与核对响应。
   - 挑战由应用保存到仪式结束（一次性、有时限），凭据由应用存储。
   - 框架不定义凭据存储：凭据的归属、命名、启停与租户都是应用的业务。
3. **JSON 形状对齐 WebAuthn Level 3**：选项与响应对象和 `PublicKeyCredentialCreationOptionsJSON`、`RegistrationResponseJSON` 等同形，二进制一律 base64url（无填充）。浏览器侧用 `parseCreationOptionsFromJSON` 与 `toJSON()`，不用手写转换。
4. **核对顺序与原因**：按 RFC-0008「WebAuthn」逐条核对，第一处不符即返回，`reason` 是机读的原因。
   - 解析不了是 `SECURITY_VALIDATION_006`，算法不支持是 `SECURITY_VALIDATION_007`，核对不通过是 `SECURITY_DENIED_005`。
   - 签名计数没有增加是 `SECURITY_DENIED_006`：它是认证器可能被复制的信号，应用要单独处理（提醒本人、停用凭据），所以与一般的核对不通过分开。
5. **算法**：ES256（P-256）、PS256、RS256，RSA 模长至少 2048 位。EdDSA 不支持：BCL 没有 Ed25519，所有主流平台认证器都支持 ES256。
6. **配置**：`XiHan:Security:WebAuthn`，`RelyingPartyId` 与 `Origins` 必填。
   - 来源的主机必须等于 RP ID 或是它的子域，`https` 之外只允许 `localhost` 的 `http`：配错的来源在启动时暴露，而不是等到浏览器拒绝。
   - 用户验证与可发现凭据缺省都要求：免用户名、免密码登录的前提。
7. **测试工具**：`Testing` 包提供 `WebAuthnTestAuthenticator`，按同一套规则生成注册与认证响应（可指定算法、标志位、签名计数并篡改各字段），框架与应用的测试共用；一致性向量也由它生成。

## 影响

- 新包 `XiHan.Framework.Security.WebAuthn`（`Preview`）；`Security.Abstractions` 加能力常量、四个错误码与对应的消息键（10 种区域）。
- `XiHan.Framework.Testing` 新增对 `Security.WebAuthn` 与 `System.Formats.Cbor` 的引用。
- 一致性向量 `conformance/vectors/security/webauthn.json`。
- 新增能力，不破坏已有 API。
