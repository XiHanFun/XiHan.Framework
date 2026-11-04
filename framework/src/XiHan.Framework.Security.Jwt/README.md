# XiHan.Framework.Security.Jwt

## 关于

`XiHan.Framework.Security.Jwt` 是认证的 JWT 提供程序：校验 HS256 对称密钥签名的 `Bearer` 令牌，把令牌映射为主体。它替换 `Security.InMemory` 的认证部分，授权仍由原授权提供程序承担。只校验令牌，不签发。

## 主要特性

* 校验签名、有效期，配置了才校验签发者与受众
* 声明按 JWT 原始类型名进入主体
* 校验失败统一返回 401，不对外暴露具体原因
* 需要非对称密钥或签发者发现时，改用 `Security.OpenIdConnect`（两者同一时刻只装配其一）

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Security.Jwt --prerelease
```

### 使用

```csharp
builder.AddXiHan(x => x.UseFeature<InMemorySecurityFeature>().UseFeature<JwtSecurityFeature>());
```

密钥至少 32 字节，生产环境从环境变量或密钥库注入，不要提交到仓库：

```json
{ "XiHan": { "Security": { "Jwt": { "Secret": "<至少 32 字节的密钥>", "Issuer": "https://issuer", "Audience": "api" } } } }
```

## 主要类型

此库提供的主要类型：

* `JwtSecurityFeature`：启用 JWT 认证的特性
* `JwtOptions`：配置项，对应 `XiHan:Security:Jwt`
* `JwtAuthenticationHandler`：`Bearer` 方案的认证处理器

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/security-jwt)

## 相关包

* 安全抽象：[XiHan.Framework.Security.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Security.Abstractions)
* OpenID Connect 认证：[XiHan.Framework.Security.OpenIdConnect](https://www.nuget.org/packages/XiHan.Framework.Security.OpenIdConnect)
* 内存实现：[XiHan.Framework.Security.InMemory](https://www.nuget.org/packages/XiHan.Framework.Security.InMemory)

## 反馈与贡献

`XiHan.Framework.Security.Jwt` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
