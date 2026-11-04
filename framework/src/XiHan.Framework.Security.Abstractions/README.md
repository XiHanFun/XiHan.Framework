# XiHan.Framework.Security.Abstractions

## 关于

`XiHan.Framework.Security.Abstractions` 定义认证与授权的契约：凭据与认证处理器、授权要求与策略目录、RBAC 授予存储与权限检查器、Authenticate / Authorize 阶段的中间件，以及条件引擎与 IP 归属地契约。它不包含具体的认证或授权实现。

## 主要特性

* 认证处理器按方案匹配凭据，把凭据变成主体
* 授权按策略名判定，`permission:<code>` 自动映射为权限策略
* 权限检查支持授予来源与收窄过滤器扩展
* 条件引擎与 IP 归属地契约，供应用自己的策略使用

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Security.Abstractions --prerelease
```

### 使用

一般由 `Security.InMemory`、`Security.Jwt`、`Security.OpenIdConnect`、`Security.Database` 带入，写自定义认证处理器或授权组件时才直接引用。例如实现一个认证方案，再登记为单例：

```csharp
public sealed class ServiceTokenHandler : IAuthenticationHandler
{
    public string Scheme => "Service";

    public ValueTask<Result<Principal>> AuthenticateAsync(AuthenticationCredential credential, InvocationContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(new Principal("svc-billing", Scheme)));
}

services.AddSingleton<IAuthenticationHandler, ServiceTokenHandler>();
```

## 主要类型

此库提供的主要类型：

* `IAuthenticationHandler`、`AuthenticationCredential`：认证处理器与凭据
* `AuthorizationRequirement`、`PolicyCatalog`：授权要求与策略目录
* `IPermissionGrantStore`、`IPermissionChecker`：授予存储与权限检查器
* `IPermissionFilter`、`IPermissionGrantSource`：收窄与额外授予的扩展点
* `IIpRegionResolver`：IP 归属地解析
* `SecurityOptions`：`XiHan:Security` 配置项

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/security-abstractions)

## 相关包

* 内存实现：[XiHan.Framework.Security.InMemory](https://www.nuget.org/packages/XiHan.Framework.Security.InMemory)
* JWT 认证：[XiHan.Framework.Security.Jwt](https://www.nuget.org/packages/XiHan.Framework.Security.Jwt)
* OpenID Connect 认证：[XiHan.Framework.Security.OpenIdConnect](https://www.nuget.org/packages/XiHan.Framework.Security.OpenIdConnect)
* 数据库授权：[XiHan.Framework.Security.Database](https://www.nuget.org/packages/XiHan.Framework.Security.Database)

## 反馈与贡献

`XiHan.Framework.Security.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
