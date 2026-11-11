# XiHan.Framework.Security.OpenIdConnect

## 关于

`XiHan.Framework.Security.OpenIdConnect` 是认证的 OpenID Connect 提供程序：从签发者的发现文档与 JWKS 取签名密钥，校验 `Bearer` 令牌。适用于令牌由 Keycloak、Entra ID 这类外部身份提供方签发的应用。替换 `Security.InMemory` 的认证部分，授权不变。

## 主要特性

* 支持 RS256、ES256 等非对称签名，密钥来自签发者 JWKS
* 密钥轮换时按需刷新，并限制刷新频率
* 刷新失败沿用最后一份密钥
* 与 `Security.Jwt` 同一时刻只装配其一

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Security.OpenIdConnect --prerelease
```

### 使用

```csharp
builder.AddXiHan(x => x.UseFeature<InMemorySecurityFeature>().UseFeature<OpenIdConnectSecurityFeature>());
```

`Authority` 与 `Audience` 必填；受众缺省校验，只有签发者只为本 API 签发令牌时才设 `ValidateAudience: false`：

```json
{ "XiHan": { "Security": { "OpenIdConnect": { "Authority": "https://login.example.com/realms/shop", "Audience": "api" } } } }
```

## 主要类型

此库提供的主要类型：

* `OpenIdConnectSecurityFeature`：启用 OpenID Connect 认证的特性
* `OpenIdConnectOptions`：配置项，对应 `XiHan:Security:OpenIdConnect`
* `OpenIdConnectMetadataCache`：发现文档与 JWKS 的缓存
* `OpenIdConnectAuthenticationHandler`：`Bearer` 方案的认证处理器

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/security-openidconnect)

## 相关包

* 安全抽象：[XiHan.Framework.Security.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Security.Abstractions)
* JWT 认证：[XiHan.Framework.Security.Jwt](https://www.nuget.org/packages/XiHan.Framework.Security.Jwt)
* 内存实现：[XiHan.Framework.Security.InMemory](https://www.nuget.org/packages/XiHan.Framework.Security.InMemory)

## 反馈与贡献

`XiHan.Framework.Security.OpenIdConnect` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
