# XiHan.Framework.Authorization

曦寒框架授权模块，提供完整的权限管理和授权功能。

## 功能特性

### 1. 权限管理

- **权限定义**: 支持层级化的权限结构
- **权限检查**: 灵活的权限验证机制
- **权限存储**: 可扩展的权限存储接口

### 2. 角色管理

- **角色定义**: 完整的角色信息管理
- **角色分配**: 用户角色关联
- **角色权限**: 角色与权限的关联管理

### 3. 策略管理

- **策略定义**: 基于策略的授权
- **自定义要求**: 支持自定义授权要求
- **策略评估**: 灵活的策略评估机制

### 4. 授权服务

- **统一授权**: 集中的授权检查入口
- **多维度授权**: 支持权限、角色、策略等多种授权方式
- **授权管理**: 权限和角色的动态管理

## 快速开始

### 1. 实现存储接口

授权模块需要你实现以下存储接口：

```csharp
// 权限存储
public class MyPermissionStore : IPermissionStore
{
    public async Task<IEnumerable<PermissionDefinition>> GetUserPermissionsAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        // 从数据库获取用户权限
    }

    // 实现其他接口方法...
}

// 角色存储
public class MyRoleStore : IRoleStore
{
    public async Task<IEnumerable<RoleDefinition>> GetUserRolesAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        // 从数据库获取用户角色
    }

    // 实现其他接口方法...
}

// 策略存储
public class MyPolicyStore : IPolicyStore
{
    public async Task<PolicyDefinition?> GetPolicyByNameAsync(
        string policyName, CancellationToken cancellationToken = default)
    {
        // 从配置或数据库获取策略定义
    }

    // 实现其他接口方法...
}

// 策略评估器
public class MyPolicyEvaluator : IPolicyEvaluator
{
    public async Task<PolicyEvaluationResult> EvaluateAsync(
        string userId, string policyName, object? resource = null,
        CancellationToken cancellationToken = default)
    {
        // 实现策略评估逻辑
    }

    // 实现其他接口方法...
}
```

### 2. 注册服务

```csharp
services.AddScoped<IPermissionStore, MyPermissionStore>();
services.AddScoped<IRoleStore, MyRoleStore>();
services.AddScoped<IPolicyStore, MyPolicyStore>();
services.AddScoped<IPolicyEvaluator, MyPolicyEvaluator>();
services.AddScoped<IRoleManager, MyRoleManager>();
```

### 3. 使用示例

#### 权限检查

```csharp
public class ProductService
{
    private readonly IAuthorizationService _authorizationService;

    public async Task<Product> CreateProduct(string userId, Product product)
    {
        // 检查权限
        var authResult = await _authorizationService.AuthorizeAsync(
            userId, "Products.Create");

        if (!authResult.Succeeded)
        {
            throw new UnauthorizedAccessException(authResult.FailureReason);
        }

        // 创建产品...
    }
}
```

#### 角色检查

```csharp
public class AdminService
{
    private readonly IAuthorizationService _authorizationService;

    public async Task<bool> IsAdmin(string userId)
    {
        var authResult = await _authorizationService.AuthorizeRoleAsync(
            userId, "Admin");

        return authResult.Succeeded;
    }
}
```

#### 策略授权

```csharp
public class DocumentService
{
    private readonly IAuthorizationService _authorizationService;

    public async Task<Document> UpdateDocument(string userId, Document document)
    {
        // 使用策略检查（例如：只能编辑自己的文档）
        var authResult = await _authorizationService.AuthorizePolicyAsync(
            userId, "DocumentOwnerPolicy", document);

        if (!authResult.Succeeded)
        {
            throw new UnauthorizedAccessException(authResult.FailureReason);
        }

        // 更新文档...
    }
}
```

#### 自定义授权要求

```csharp
public class DocumentOwnerRequirement : IAuthorizationRequirement
{
    public string Name => "DocumentOwner";

    public async Task<bool> EvaluateAsync(AuthorizationContext context)
    {
        if (context.Resource is not Document document)
            return false;

        // 检查用户是否是文档所有者
        return document.OwnerId == context.UserId;
    }
}

// 在策略中使用
var policy = new PolicyDefinition("DocumentOwnerPolicy", "文档所有者策略")
{
    CustomRequirements = new List<IAuthorizationRequirement>
    {
        new DocumentOwnerRequirement()
    }
};
```

#### 权限管理

```csharp
public class UserManagementService
{
    private readonly IAuthorizationService _authorizationService;

    public async Task GrantPermission(string userId, string permissionName)
    {
        var result = await _authorizationService.GrantPermissionAsync(
            userId, permissionName);

        if (!result.Succeeded)
        {
            throw new Exception(result.FailureReason);
        }
    }

    public async Task AddUserToRole(string userId, string roleName)
    {
        var result = await _authorizationService.AddUserToRoleAsync(
            userId, roleName);

        if (!result.Succeeded)
        {
            throw new Exception(result.FailureReason);
        }
    }
}
```

## 权限设计建议

### 1. 权限命名规范

使用层级化的命名方式：

```csharp
// 格式: {模块}.{操作}
"Users.Create"
"Users.Read"
"Users.Update"
"Users.Delete"

"Products.Manage"
"Products.View"

"Reports.View"
"Reports.Export"
```

### 2. 角色设计

```csharp
// 预定义角色
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string User = "User";
    public const string Guest = "Guest";
}

// 角色权限映射
Admin: 所有权限
Manager: Users.Read, Products.*, Reports.*
User: Products.View, Reports.View
Guest: Products.View
```

### 3. 策略设计

```csharp
// 基于资源所有权的策略
public class ResourceOwnerPolicy
{
    RequiredClaims = { { "resource_owner", "true" } }
    CustomRequirements = { new ResourceOwnerRequirement() }
}

// 基于时间的策略
public class WorkingHoursPolicy
{
    CustomRequirements = { new WorkingHoursRequirement() }
}
```

## 架构说明

### 层次结构

```
IAuthorizationService (统一授权入口)
    ├── IPermissionChecker (权限检查)
    │   └── IPermissionStore (权限存储)
    ├── IRoleStore (角色存储)
    │   └── IRoleManager (角色管理)
    └── IPolicyEvaluator (策略评估)
        └── IPolicyStore (策略存储)
```

### 授权流程

1. 调用 `IAuthorizationService` 的授权方法
2. 根据授权类型选择检查器（权限/角色/策略）
3. 从存储中获取相关数据
4. 执行授权逻辑
5. 返回授权结果

## 依赖项

- XiHan.Framework.Core
- XiHan.Framework.Authentication

## 许可证

MIT License - 详见 LICENSE 文件
