# XiHan.Framework.Security.InMemory

## 关于

`XiHan.Framework.Security.InMemory` 是安全的默认实现：从配置装载 API Key 主体与权限授予，提供 `ApiKey` 方案的认证与 RBAC 授权。只用于开发、原型与测试。

## 主要特性

* 配置里写主体、API Key、角色与授予，零外部依赖
* 运行时可增删租户专属授予
* 装配 JWT 或 OpenID Connect 后只提供授权，装配数据库授权后只提供认证

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Security.InMemory --prerelease
```

### 使用

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<InMemorySecurityFeature>());
```

主体与授予写在配置里（API Key 明文随配置分发，只用开发值）：

```json
{
  "XiHan": { "Security": { "InMemory": {
    "Principals": { "alice": { "ApiKey": "dev-alice-key", "Roles": "admin,editor", "Tenant": "acme" } },
    "Grants": { "role": { "admin": "*", "editor": "orders.read,orders.write" }, "user": { "alice": "reports.read" } }
  } } }
}
```

调用时带 `Authorization: ApiKey dev-alice-key`。

## 主要类型

此库提供的主要类型：

* `InMemorySecurityFeature`：启用内存安全的特性
* `ApiKeyAuthenticationHandler`：`ApiKey` 方案的认证处理器
* `InMemoryPermissionGrantStore`：授予存储，可在运行时 `Grant` / `Revoke`
* `InMemorySecurityOptions`：配置项，对应 `XiHan:Security:InMemory`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/security-inmemory)

## 相关包

* 安全抽象：[XiHan.Framework.Security.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Security.Abstractions)
* JWT 认证：[XiHan.Framework.Security.Jwt](https://www.nuget.org/packages/XiHan.Framework.Security.Jwt)
* 数据库授权：[XiHan.Framework.Security.Database](https://www.nuget.org/packages/XiHan.Framework.Security.Database)

## 反馈与贡献

`XiHan.Framework.Security.InMemory` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
