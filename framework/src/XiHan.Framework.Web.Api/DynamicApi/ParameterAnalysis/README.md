# DynamicApi 参数分析系统

## 📖 概述

DynamicApi 参数分析系统是一个**可实现、可校验、可扩展**的智能参数处理框架，完全对齐 **ASP.NET Core / OpenAPI / ABP** 标准。

## 🎯 核心功能

1. **自动推断参数来源** - FromRoute / FromQuery / FromBody / FromServices
2. **保证 Swagger / OpenAPI 100% 可生成**
3. **参数数量、来源合法性校验**
4. **统一 CRUD 与自定义方法的规则**
5. **出现歧义时可解释、可报错**

## 🏗️ 架构设计

```text
MethodInfo
   ↓
DynamicApiParameterAnalyzer (总入口)
   ↓
├─ ParameterClassifier (参数分类器)
├─ ParameterSourceResolver (来源解析器)
└─ ParameterRuleValidator (规则校验器)
   ↓
ParameterDescriptor[] (分析结果)
   ↓
DynamicApiControllerFactory
```

## 📦 核心组件

### 1. ParameterKind（参数物理类型）

```csharp
public enum ParameterKind
{
    RouteKey,    // 路由键（id, xxxId）
    Simple,      // 简单类型（int, string, bool, DateTime）
    Complex,     // 复杂类型（class, record）
    Collection,  // 集合类型（IEnumerable<T>）
    Special      // 特殊类型（CancellationToken, HttpContext）
}
```

### 2. ParameterRole（参数语义角色）

```csharp
public enum ParameterRole
{
    Id,       // 主键
    Query,    // 查询条件
    Command,  // Create / Update DTO
    Batch,    // 批量操作
    Infra     // 基础设施参数
}
```

### 3. ParameterSource（参数来源）

```csharp
public enum ParameterSource
{
    Route,    // 从路由获取
    Query,    // 从查询字符串获取
    Body,     // 从请求体获取
    Services, // 从服务容器获取
    Header,   // 从请求头获取
    Form      // 从表单获取
}
```

## 🔍 参数来源推断算法

### Step 0：显式特性优先（最高优先级）

```csharp
[FromRoute] / [FromQuery] / [FromBody] / [FromServices]
```

**框架永远不覆盖用户显式标注**

### Step 1：基础设施参数直接识别

```csharp
CancellationToken / HttpContext / ClaimsPrincipal → FromServices
```

### Step 2：根据 HTTP Method 决策

```text
GET / DELETE → 不允许 FromBody
POST / PUT / PATCH → 允许一个 FromBody
```

### Step 3：Route 参数推断

```csharp
// Id 参数 + 简单类型 → FromRoute
Task<UserDto> GetByIdAsync(long id)  // id → FromRoute

// 识别规则：
// - 参数名 == "id"（忽略大小写）
// - 参数名以 "Id" 或 "ID" 结尾
// - 类型是 long / int / Guid / string
```

### Step 4：Body 参数推断（只能 1 个）

```csharp
// 复杂类型 → FromBody
Task<UserDto> CreateAsync(CreateUserDto dto)  // dto → FromBody
```

**前提条件：**
- HTTP Method ≠ GET / DELETE
- 当前还没有 Body 参数

### Step 5：Query 参数兜底规则

```csharp
// 所有其他情况 → FromQuery
Task<List<UserDto>> GetListAsync(string keyword, int pageSize)
// keyword → FromQuery, pageSize → FromQuery
```

## 🛡️ 参数校验规则

### 1. FromBody 数量校验

```csharp
❌ Task CreateAsync(CreateUserDto dto, UpdateUserDto update)  // 2 个 Body 参数

✅ Task CreateAsync(CreateUserDto dto)  // 1 个 Body 参数
```

**错误消息：**
> 方法 'CreateAsync' 只能有一个 FromBody 参数，当前有 2 个。请合并为单个 DTO 对象。

### 2. GET 不允许 Body

```csharp
❌ Task<UserDto> GetAsync([FromBody] QueryDto query)  // GET + Body

✅ Task<UserDto> GetAsync([FromQuery] QueryDto query)  // GET + Query
```

**错误消息：**
> 方法 'GetAsync' 使用 GET 请求，不允许 FromBody 参数。违规参数: query。请改用 FromQuery 或 FromRoute。

### 3. Route 参数过多

```csharp
❌ Task DeleteAsync(long id, long userId, long tenantId, long orgId)  // 4 个 Route 参数

✅ Task DeleteAsync(long id)  // 1 个 Route 参数
```

**错误消息：**
> 方法 'DeleteAsync' 的 Route 参数过多（4 个）。建议使用复合主键对象或改用 FromQuery。

### 4. 基础类型 FromBody 禁止

```csharp
❌ Task CreateAsync([FromBody] string name)  // 简单类型 + FromBody

✅ Task CreateAsync([FromBody] CreateUserDto dto)  // 复杂类型 + FromBody
```

**错误消息：**
> 方法 'CreateAsync' 的参数 'name' 类型为 'String'，不能使用 FromBody。FromBody 参数必须是复杂类型（DTO / class / record）。

## 📝 使用示例

### 示例 1：标准 CRUD 方法

```csharp
public class UserService : CrudApplicationServiceBase<User, UserDto, long, CreateUserDto, UpdateUserDto>
{
    // GET api/User/get/{id}
    // id → FromRoute（自动推断）
    public override Task<UserDto> GetByIdAsync(long id)

    // POST api/User/create
    // dto → FromBody（自动推断）
    public override Task<UserDto> CreateAsync(CreateUserDto dto)

    // PUT api/User/update/{id}
    // id → FromRoute, dto → FromBody（自动推断）
    public override Task<UserDto> UpdateAsync(long id, UpdateUserDto dto)

    // DELETE api/User/delete/{id}
    // id → FromRoute（自动推断）
    public override Task<bool> DeleteAsync(long id)
}
```

### 示例 2：自定义查询方法

```csharp
// GET api/User/search?keyword=xxx&status=1&pageSize=10
// 所有参数 → FromQuery（自动推断）
public Task<List<UserDto>> SearchAsync(string keyword, int status, int pageSize)
```

### 示例 3：显式标注

```csharp
// POST api/User/batch-create
// dtos → FromBody（显式标注，优先级最高）
public Task<List<UserDto>> BatchCreateAsync([FromBody] List<CreateUserDto> dtos)

// GET api/User/by-name/{name}
// name → FromRoute（显式标注）
public Task<UserDto> GetByNameAsync([FromRoute] string name)
```

### 示例 4：混合参数

```csharp
// PUT api/User/update-status/{id}?force=true
// id → FromRoute（Id 参数）
// dto → FromBody（复杂类型）
// force → FromQuery（简单类型）
public Task<UserDto> UpdateStatusAsync(long id, UpdateStatusDto dto, bool force = false)
```

## 🔧 扩展点

### 1. 自定义参数分类

修改 `ParameterClassifier` 来支持自定义类型识别：

```csharp
public static bool IsSimpleType(Type type)
{
    // 添加自定义 ID 类型支持
    if (type == typeof(MyCustomId))
        return true;
    
    // ... 其他逻辑
}
```

### 2. 自定义来源解析

扩展 `ParameterSourceResolver` 来支持自定义解析规则：

```csharp
public ParameterSource Resolve(ParameterDescriptor descriptor)
{
    // 添加自定义规则
    if (descriptor.Name.StartsWith("header"))
        return ParameterSource.Header;
    
    // ... 其他逻辑
}
```

### 3. 自定义校验规则

扩展 `ParameterRuleValidator` 来添加自定义校验：

```csharp
public void Validate(IEnumerable<ParameterDescriptor> descriptors)
{
    // 添加自定义校验
    ValidateCustomRule(descriptors);
    
    // ... 其他逻辑
}
```

## ✅ Swagger / OpenAPI 兼容性

系统确保以下规则，保证 Swagger 100% 可生成：

1. ✅ 只允许 1 个 Body 参数
2. ✅ Query 参数全部可序列化
3. ✅ Route 参数全部 Required
4. ✅ Body DTO 必须是 class

## 🎓 最佳实践

### 1. DTO 设计

```csharp
// ✅ 推荐：职责清晰的 DTO
public record CreateUserDto(string Name, string Email);
public record UpdateUserDto(string Name, string Email);
public record QueryUserDto(string Keyword, int Status);

// ❌ 不推荐：混用参数
public Task CreateAsync(string name, string email)  // 应该封装为 DTO
```

### 2. 参数数量

```csharp
// ✅ 推荐：参数数量合理
public Task SearchAsync(QueryUserDto query)

// ❌ 不推荐：参数过多
public Task SearchAsync(string keyword, int status, int role, 
    DateTime startDate, DateTime endDate, int pageIndex, int pageSize)
```

### 3. 命名约定

```csharp
// ✅ 推荐：清晰的 Id 参数命名
public Task<UserDto> GetByIdAsync(long id)
public Task<UserDto> GetByUserIdAsync(long userId)

// ❌ 不推荐：模糊的参数命名
public Task<UserDto> GetAsync(long key)
```

## 📊 性能特性

- ✅ **零运行时反射** - 参数分析在启动时完成
- ✅ **高效缓存** - 分析结果被缓存和复用
- ✅ **编译时生成** - IL Emit 生成高性能控制器

## 🤝 与 ABP 对齐

本系统参考了 ABP Framework 的设计理念：

- ✅ 方法名参与推断（Create → Body, Get → Route）
- ✅ CRUD 方法特化处理
- ✅ 约定优于配置
- ✅ 显式标注优先

## 📚 相关资源

- [ASP.NET Core Model Binding](https://docs.microsoft.com/en-us/aspnet/core/mvc/models/model-binding)
- [OpenAPI Specification](https://swagger.io/specification/)
- [ABP Dynamic API](https://docs.abp.io/en/abp/latest/API/Auto-API-Controllers)

