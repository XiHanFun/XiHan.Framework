# XiHan.Framework.Security.WebAuthn

## 关于

`XiHan.Framework.Security.WebAuthn` 是通行密钥（WebAuthn）的依赖方实现：生成注册与认证仪式的选项，核对浏览器交回的响应。适用于要做免密码、免用户名登录的应用。它是无状态的：挑战由应用保存到仪式结束，凭据由应用存储。

## 主要特性

* 选项与响应和 WebAuthn Level 3 的 JSON 同形，浏览器用 `parseCreationOptionsFromJSON` 与 `toJSON()` 直接对接
* 支持 ES256（P-256）、PS256、RS256，只依赖 `System.Formats.Cbor` 与 BCL 的密码学类型
* 证明一律按 `none` 处理，不需要信任链与元数据服务
* 失败以 `Result` 返回，带机读的原因；签名计数不增单独报告

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Security.WebAuthn --prerelease
```

### 使用

```csharp
builder.AddXiHan(x => x.UseFeature<WebAuthnSecurityFeature>());
```

`RelyingPartyId` 与 `Origins` 必填：

```json
{ "XiHan": { "Security": { "WebAuthn": { "RelyingPartyId": "example.com", "Origins": [ "https://example.com" ] } } } }
```

注册：生成选项交给浏览器，保存挑战；浏览器交回响应后核对，存下凭据。

```csharp
var options = ceremonies.CreateRegistrationOptions(new WebAuthnUserEntity(userHandle, userName, displayName), existing);
// 保存 options.Challenge，把 options 发给浏览器
var registered = ceremonies.VerifyRegistration(response, savedChallenge);
```

认证：按响应的凭据 ID 找到存储的凭据，核对后更新签名计数。

```csharp
var verified = ceremonies.VerifyAuthentication(response, savedChallenge, new WebAuthnStoredCredential(id, publicKey, signCount, userHandle));
```

## 主要类型

此库提供的主要类型：

* `WebAuthnSecurityFeature`：启用通行密钥的特性
* `WebAuthnOptions`：配置项，对应 `XiHan:Security:WebAuthn`
* `WebAuthnCeremonies`：选项生成与响应核对
* `WebAuthnCreationOptions` / `WebAuthnRequestOptions`：交给浏览器的选项
* `WebAuthnRegistrationResponse` / `WebAuthnAuthenticationResponse`：浏览器交回的响应
* `WebAuthnRegisteredCredential` / `WebAuthnStoredCredential` / `WebAuthnVerifiedAssertion`：核对结果与存储的凭据

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/security-webauthn)

## 相关包

* 安全抽象：[XiHan.Framework.Security.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Security.Abstractions)
* 测试工具（含测试用认证器）：[XiHan.Framework.Testing](https://www.nuget.org/packages/XiHan.Framework.Testing)

## 反馈与贡献

`XiHan.Framework.Security.WebAuthn` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
