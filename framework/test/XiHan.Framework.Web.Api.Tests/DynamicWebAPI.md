# 动态 WebAPI 功能文档

## 📖 概述

动态 WebAPI 是 XiHan.Framework 的核心功能之一，它能够自动将应用服务转换为 REST API，无需手动编写控制器代码。这大大提升了开发效率，减少了重复代码。

## ✨ 核心特性

### 1. 自动路由生成

- 根据应用服务类和方法自动生成 REST API 路由
- 支持自定义路由规则和命名约定
- 支持路由参数自动绑定

### 2. 智能 HTTP 方法识别

- 根据方法名自动推断 HTTP 方法（GET/POST/PUT/DELETE/PATCH）
- 支持自定义 HTTP 方法映射规则
- 支持通过特性显式指定 HTTP 方法

### 3. CRUD 操作支持

- 提供完整的 CRUD 应用服务基类
- 支持分页查询、条件过滤、排序
- 自动处理实体与 DTO 的转换

### 4. 批量操作

- 支持批量创建、更新、删除操作
- 支持事务控制和错误处理
- 可配置批量操作的最大数量

### 5. API 版本控制

- 支持多版本 API 并存
- 灵活的版本号配置
- 支持版本弃用标记

### 6. 高度可配置

- 丰富的配置选项
- 支持全局和局部配置
- 灵活的约定规则

## 🚀 快速开始

### 1. 安装 NuGet 包

```bash
dotnet add package XiHan.Framework.Web.Api
dotnet add package XiHan.Framework.Application
```

### 2. 定义实体

```csharp
using XiHan.Framework.Domain.Entities;

namespace MyApp.Domain.Entities;

/// <summary>
/// 用户实体
/// </summary>
public class User : FullAuditedEntityBase<long>
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
```

### 3. 定义 DTO

```csharp
namespace MyApp.Application.Dtos;

/// <summary>
/// 用户 DTO
/// </summary>
public class UserDto
{
    public long Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
}

/// <summary>
/// 创建用户 DTO
/// </summary>
public class CreateUserDto
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

/// <summary>
/// 更新用户 DTO
/// </summary>
public class UpdateUserDto
{
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
```

### 4. 创建应用服务

```csharp
using XiHan.Framework.Application.Services;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;

namespace MyApp.Application.Services;

/// <summary>
/// 用户应用服务
/// </summary>
[DynamicApi] // 标记为动态 API
public class UserAppService : CrudApplicationServiceBase<User, UserDto, long, CreateUserDto, UpdateUserDto>
{
    public UserAppService(IRepositoryBase<User, long> repository) : base(repository)
    {
    }

    protected override async Task<UserDto> MapToEntityDtoAsync(User entity)
    {
        return new UserDto
        {
            Id = entity.BasicId,
            UserName = entity.UserName,
            Email = entity.Email,
            PhoneNumber = entity.PhoneNumber,
            IsActive = entity.IsActive,
            CreatedTime = entity.CreatedTime
        };
    }

    protected override async Task<User> MapToEntityAsync(CreateUserDto createDto)
    {
        return new User
        {
            UserName = createDto.UserName,
            Email = createDto.Email,
            PhoneNumber = createDto.PhoneNumber,
            IsActive = true
        };
    }

    protected override async Task MapToEntityAsync(UpdateUserDto updateDto, User entity)
    {
        entity.Email = updateDto.Email;
        entity.PhoneNumber = updateDto.PhoneNumber;
        entity.IsActive = updateDto.IsActive;
    }

    /// <summary>
    /// 自定义方法：根据用户名查询
    /// </summary>
    [HttpGet("by-username/{username}")]
    public async Task<UserDto?> GetByUserNameAsync(string username)
    {
        var user = await Repository.FirstOrDefaultAsync(u => u.UserName == username);
        return user == null ? null : await MapToEntityDtoAsync(user);
    }

    /// <summary>
    /// 自定义方法：激活用户
    /// </summary>
    [HttpPost("{id}/activate")]
    public async Task<bool> ActivateAsync(long id)
    {
        var user = await Repository.GetAsync(id);
        if (user == null) return false;

        user.IsActive = true;
        await Repository.UpdateAsync(user);
        return true;
    }
}
```

### 5. 配置模块

```csharp
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api.DynamicApi;

namespace MyApp.Web;

[DependsOn(typeof(XiHanDynamicApiModule))]
public class MyAppWebModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 配置动态 API
        services.AddDynamicApi(options =>
        {
            options.DefaultRoutePrefix = "api";
            options.EnableBatchOperations = true;
            options.MaxBatchSize = 100;

            // 配置约定
            options.Conventions.UseLowercaseRoutes = true;
            options.Conventions.RouteSeparator = "-";

            // 配置路由
            options.Routes.UseModuleNameAsRoute = true;
        });
    }
}
```

### 6. 自动生成的 API

以上配置会自动生成以下 REST API：

```
GET    /api/users                          # 获取用户列表（分页）
GET    /api/users/{id}                     # 获取单个用户
POST   /api/users                          # 创建用户
PUT    /api/users/{id}                     # 更新用户
DELETE /api/users/{id}                     # 删除用户
GET    /api/users/by-username/{username}   # 根据用户名查询
POST   /api/users/{id}/activate            # 激活用户
```

## 📋 完整功能示例

### 批量操作示例

```csharp
using XiHan.Framework.Application.Services;
using XiHan.Framework.Web.Api.DynamicApi.Batch;

/// <summary>
/// 支持批量操作的用户服务
/// </summary>
[DynamicApi]
public class UserBatchAppService : BatchCrudApplicationServiceBase<User, UserDto, long, CreateUserDto, UpdateUserDto>
{
    public UserBatchAppService(IRepositoryBase<User, long> repository) : base(repository)
    {
    }

    // 映射方法实现...
}
```

自动生成的批量操作 API：

```
POST   /api/users/batch-create    # 批量创建用户
POST   /api/users/batch-update    # 批量更新用户
POST   /api/users/batch-delete    # 批量删除用户
POST   /api/users/batch-get       # 批量获取用户
```

### API 版本控制示例

```csharp
using XiHan.Framework.Web.Api.DynamicApi.Versioning;

[DynamicApi]
[ApiVersion("1.0")]
public class UserV1AppService : CrudApplicationServiceBase<User, UserDto, long>
{
    // V1 实现
}

[DynamicApi]
[ApiVersion("2.0")]
public class UserV2AppService : CrudApplicationServiceBase<User, UserDtoV2, long>
{
    // V2 实现
}
```

生成的版本化 API：

```
GET /api/v1/users      # V1 API
GET /api/v2/users      # V2 API
```

### 禁用特定方法

```csharp
[DynamicApi]
public class UserAppService : CrudApplicationServiceBase<User, UserDto, long>
{
    /// <summary>
    /// 删除方法（禁用动态 API）
    /// </summary>
    [DisableDynamicApi]
    public override async Task<bool> DeleteAsync(long id)
    {
        // 此方法不会暴露为 API
        return await base.DeleteAsync(id);
    }
}
```

## ⚙️ 配置选项详解

### 基本配置

```csharp
services.AddDynamicApi(options =>
{
    // 是否启用动态 API
    options.IsEnabled = true;

    // 默认路由前缀
    options.DefaultRoutePrefix = "api";

    // 默认 API 版本
    options.DefaultApiVersion = "1.0";

    // 是否启用 API 版本控制
    options.EnableApiVersioning = true;

    // 是否启用批量操作
    options.EnableBatchOperations = true;

    // 批量操作最大数量
    options.MaxBatchSize = 100;

    // 是否移除服务名称后缀
    options.RemoveServiceSuffix = true;

    // 要移除的后缀列表
    options.ServiceSuffixes = new List<string>
    {
        "AppService",
        "ApplicationService",
        "Service"
    };
});
```

### 约定配置

```csharp
services.ConfigureDynamicApiConventions(conventions =>
{
    // HTTP 方法约定映射
    conventions.HttpMethodConventions = new Dictionary<string, string>
    {
        { "Get", "GET" },
        { "Create", "POST" },
        { "Update", "PUT" },
        { "Delete", "DELETE" },
        { "Patch", "PATCH" }
    };

    // 是否使用 PascalCase 路由
    conventions.UsePascalCaseRoutes = false;

    // 是否使用小写路由
    conventions.UseLowercaseRoutes = true;

    // 路由分隔符
    conventions.RouteSeparator = "-";
});
```

### 路由配置

```csharp
services.ConfigureDynamicApiRoutes(routes =>
{
    // 是否使用命名空间作为路由
    routes.UseNamespaceAsRoute = false;

    // 要排除的命名空间前缀
    routes.NamespacePrefixesToExclude = new List<string>
    {
        "MyApp.Application.Services"
    };

    // 是否将模块名称作为路由
    routes.UseModuleNameAsRoute = true;

    // 模块名称提取正则表达式
    routes.ModuleNamePattern = @"\.(\w+)\.Application";
});
```

## 🎯 最佳实践

### 1. 服务命名规范

```csharp
// 推荐
public class UserAppService { }      // 自动转换为 /api/user
public class OrderAppService { }     // 自动转换为 /api/order

// 不推荐
public class UserService { }         // 可能与其他服务冲突
public class UserManager { }         // 不会被识别为应用服务
```

### 2. 方法命名规范

```csharp
// 自动识别为 GET
public Task<UserDto> GetAsync(long id) { }
public Task<List<UserDto>> GetListAsync() { }
public Task<UserDto> FindAsync(long id) { }

// 自动识别为 POST
public Task<UserDto> CreateAsync(CreateUserDto input) { }
public Task<UserDto> AddAsync(CreateUserDto input) { }

// 自动识别为 PUT
public Task<UserDto> UpdateAsync(long id, UpdateUserDto input) { }
public Task<UserDto> EditAsync(long id, UpdateUserDto input) { }

// 自动识别为 DELETE
public Task<bool> DeleteAsync(long id) { }
public Task<bool> RemoveAsync(long id) { }
```

### 3. DTO 设计建议

```csharp
// 查询 DTO - 用于接收参数
public class GetUsersInput : PageQuery
{
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
}

// 结果 DTO - 用于返回数据
public class UserDto
{
    public long Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// 创建 DTO - 用于创建实体
public class CreateUserDto
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

// 更新 DTO - 用于更新实体
public class UpdateUserDto
{
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
```

### 4. 分离读写服务

```csharp
// 查询服务
[DynamicApi]
public class UserQueryService : ApplicationServiceBase
{
    // 只包含查询方法
    public async Task<UserDto> GetAsync(long id) { }
    public async Task<PageResponseDto<UserDto>> GetListAsync(PageQuery input) { }
}

// 命令服务
[DynamicApi]
public class UserCommandService : ApplicationServiceBase
{
    // 只包含修改方法
    public async Task<UserDto> CreateAsync(CreateUserDto input) { }
    public async Task<UserDto> UpdateAsync(long id, UpdateUserDto input) { }
    public async Task<bool> DeleteAsync(long id) { }
}
```

## 🔧 高级功能

### 自定义约定

```csharp
public class CustomDynamicApiConvention : IDynamicApiConvention
{
    public void Apply(DynamicApiConventionContext context)
    {
        // 自定义路由生成逻辑
        if (context.ServiceType.Namespace?.Contains("Admin") == true)
        {
            context.RouteTemplate = $"api/admin/{context.ControllerName}";
        }

        // 自定义 HTTP 方法映射
        if (context.MethodInfo?.Name.StartsWith("Search") == true)
        {
            context.HttpMethod = "POST"; // 搜索使用 POST
        }
    }
}

// 注册自定义约定
services.AddSingleton<IDynamicApiConvention, CustomDynamicApiConvention>();
```

### 动态 API 元数据

```csharp
[DynamicApi(Name = "User Management", Version = "2.0")]
public class UserAppService : CrudApplicationServiceBase<User, UserDto, long>
{
    /// <summary>
    /// 获取用户详情
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <returns>用户信息</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(404)]
    public override async Task<UserDto?> GetAsync(long id)
    {
        return await base.GetAsync(id);
    }
}
```

## 📊 性能优化

### 1. 使用异步方法

所有应用服务方法都应该使用异步模式：

```csharp
public async Task<UserDto> GetAsync(long id) { }
public async Task<List<UserDto>> GetListAsync() { }
```

### 2. 分页查询优化

```csharp
public override async Task<PageResponseDto<UserDto>> GetListAsync(PageQuery input)
{
    // 先获取总数（避免加载所有数据）
    var totalCount = await Repository.CountAsync();

    // 应用分页
    var query = Repository.GetQueryableAsync().Result
        .Skip((input.PageInfo.PageIndex - 1) * input.PageInfo.PageSize)
        .Take(input.PageInfo.PageSize);

    var entities = await Repository.GetListAsync(query);
    var dtos = await MapToEntityDtosAsync(entities);

    return new PageResponseDto<UserDto>(pageData, dtos);
}
```

### 3. 批量操作优化

```csharp
public override async Task<BatchOperationResponse<UserDto>> BatchCreateAsync(BatchOperationRequest<CreateUserDto> request)
{
    // 使用事务
    if (request.UseTransaction)
    {
        using var transaction = await Repository.BeginTransactionAsync();
        try
        {
            var response = await base.BatchCreateAsync(request);
            await transaction.CommitAsync();
            return response;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    return await base.BatchCreateAsync(request);
}
```

## 🔒 安全建议

### 1. 添加认证授权

```csharp
[DynamicApi]
[Authorize] // 需要认证
public class UserAppService : CrudApplicationServiceBase<User, UserDto, long>
{
    [Authorize(Roles = "Admin")] // 需要 Admin 角色
    public override async Task<bool> DeleteAsync(long id)
    {
        return await base.DeleteAsync(id);
    }
}
```

### 2. 输入验证

```csharp
public class CreateUserDto
{
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(50, MinimumLength = 3)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
```

### 3. 限流保护

```csharp
[DynamicApi]
[RateLimit(PermitLimit = 100, Window = 60)] // 每分钟最多 100 次请求
public class UserAppService : CrudApplicationServiceBase<User, UserDto, long>
{
    // ...
}
```

## 🎓 总结

动态 WebAPI 功能让开发者可以专注于业务逻辑，而无需关心 REST API 的实现细节。通过合理的配置和约定，可以快速构建出高质量、易维护的 Web API 应用。

### 关键优势

✅ **开发效率高** - 无需手动编写控制器代码  
✅ **易于维护** - 统一的代码结构和约定  
✅ **功能完整** - 支持 CRUD、批量操作、版本控制  
✅ **灵活可扩展** - 丰富的配置选项和扩展点  
✅ **符合最佳实践** - 遵循 REST 规范和 DDD 原则

### 下一步

- 查看 [应用服务开发指南](./ApplicationServices.md)
- 了解 [仓储模式](./Repository.md)
- 学习 [领域驱动设计](./DomainDrivenDesign.md)
