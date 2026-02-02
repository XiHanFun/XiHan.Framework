# XiHan.Framework.Domain.Shared.Paging

强大、灵活、易用的**企业级分页查询模块**，支持 **⭐ 零配置自动查询** 和 Attribute 驱动的自动验证

## 🚀 快速体验

**一行代码完成查询！**

```csharp
// ⭐ 定义查询DTO（无需任何配置）
public class OrderQueryDto
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchKey { get; set; }        // 关键字搜索
    public long? UserId { get; set; }             // 精确查询
    public decimal[]? AmountRange { get; set; }   // 范围查询
    public List<int>? StatusList { get; set; }    // 多选查询
}

// ⭐ 一行搞定！自动识别所有查询类型
var result = dbContext.Orders.ToPageResultAuto(queryDto);
```

**自动识别：** string→模糊, List→In, 数组→Between, 数值→Equal...  
**代码减少：** 95%+  
**[查看快速开始文档](QUICKSTART.md)** →

## 📋 目录

- [功能特性](#功能特性)
- [模块结构](#模块结构)
- [快速开始](#快速开始)
- [自动查询](#自动查询)
- [核心组件](#核心组件)
- [Attribute 集成](#attribute-集成)
- [使用示例](#使用示例)
- [最佳实践](#最佳实践)

## ✨ 功能特性

### 🌟 自动查询（最新特性）
- ✅ **⭐ 零配置** - 无需手动 Build，自动根据 DTO 属性构建查询
- ✅ **⭐ 智能识别** - 自动识别 List→In, 数组→Between, string→Contains
- ✅ **⭐ 约定优于配置** - 属性名包含 "Range"→Between, "List/Ids"→In
- ✅ **⭐ 一行代码** - `ToPageResultAuto(dto)` 搞定所有查询
- ✅ **⭐ 手动优先** - 如果指定 Attribute，则以手动配置为准
- ✅ **⭐ 自定义约定** - 可自定义识别规则
- ✅ **⭐ 减少 95%代码** - 告别重复的 if 判断和手动构建

### 核心功能
- ✅ **灵活的分页支持** - 支持标准分页和禁用分页模式
- ✅ **多条件过滤** - 支持等于、不等于、大于、小于、包含、In、Between 等 13 种操作符
- ✅ **多字段排序** - 支持多字段排序，可指定优先级
- ✅ **关键字搜索** - 支持多字段模糊搜索，OR 关系
- ✅ **数据验证** - 完整的参数验证机制
- ✅ **类型转换** - 分页结果类型映射支持

### 扩展功能
- ✅ **流式 API** - Fluent API 风格的查询构建器
- ✅ **扩展方法** - 丰富的 IQueryable/IEnumerable 扩展
- ✅ **辅助工具** - 分页计算、验证、转换等工具类
- ✅ **特性标注** - 通过 Attribute 配置查询字段行为

### Attribute 驱动
- ✅ **自动验证** - 根据实体类上的 Attribute 自动验证查询请求
- ✅ **字段别名** - 支持字段别名映射
- ✅ **操作符限制** - 限制字段支持的查询操作符
- ✅ **关键字配置** - 配置关键字搜索字段和匹配模式
- ✅ **权限控制** - 控制字段是否可过滤、排序
- ✅ **智能推断** - 根据字段类型自动推断支持的操作符

## 📁 模块结构

```
Paging/
├── Attributes/              # 特性标注
│   ├── KeywordSearchAttribute.cs       # 关键字搜索配置
│   ├── QueryFieldAttribute.cs          # 查询字段配置
│   └── QueryOperatorSupportAttribute.cs # 支持的操作符配置
├── Builders/               # 构建器
│   └── QueryBuilder.cs                 # 查询构建器（Fluent API）
├── Converters/             # 转换器
│   └── PageConverter.cs                # 分页数据转换器
├── Dtos/                   # 数据传输对象
│   ├── BasePageRequestDto.cs           # 分页请求 DTO
│   └── BasePageResultDto.cs            # 分页结果 DTO
├── Enums/                  # 枚举
│   ├── KeywordMatchMode.cs             # 关键字匹配模式
│   ├── QueryOperator.cs                # 查询操作符
│   └── SortDirection.cs                # 排序方向
├── Examples/               # 使用示例
│   ├── PagingUsageExamples.cs          # 基础使用示例
│   └── IntegratedUsageExample.cs       # ⭐ 完整集成示例
├── Executors/              # ⭐ 查询执行器
│   └── PageQueryExecutor.cs            # 自动查询执行器
├── Helpers/                # 辅助工具
│   └── PageHelper.cs                   # 分页辅助方法
├── Models/                 # 模型
│   ├── PageRequestMetadata.cs          # 分页请求元数据
│   ├── PageResultMetadata.cs           # 分页结果元数据
│   ├── QueryFilter.cs                  # 查询过滤条件
│   └── QuerySort.cs                    # 查询排序条件
├── Reflection/             # ⭐ 反射工具
│   └── AttributeReader.cs              # Attribute 读取器
├── Validators/             # 验证器
│   ├── PageValidator.cs                # 基础分页验证器
│   └── AttributeBasedValidator.cs      # ⭐ 基于 Attribute 的验证器
├── AttributePageExtensions.cs          # ⭐ Attribute 扩展方法
├── PageExtensions.cs                   # 基础扩展方法
└── README.md                           # 本文档

⭐ 标记的是 Attribute 集成的核心组件
```

## 🚀 快速开始

### ⭐ 方式 1: 自动查询（推荐，零配置）

**最简单的方式，一行代码搞定！**

```csharp
// 定义查询DTO（无需任何配置）
public class UserQueryDto
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchKey { get; set; }        // 关键字搜索
    public string? Name { get; set; }             // 模糊查询
    public int? Status { get; set; }              // 精确查询
    public int[]? AgeRange { get; set; }          // 范围查询
    public List<string>? DepartmentList { get; set; }  // 多选查询
}

// ⭐ 一行搞定！自动识别所有查询类型
var result = dbContext.Users.ToPageResultAuto(queryDto);
```

**[查看完整自动查询文档](QUICKSTART.md)** →

### 方式 2: 使用查询构建器

```csharp
var request = QueryBuilder.Create()
    .WhereEqual("Status", 1)              // 状态=1
    .WhereContains("Name", "张")          // 名字包含"张"
    .WhereBetween("Age", 18, 60)          // 年龄18-60
    .OrderByDescending("CreateTime")      // 按创建时间降序
    .SetPaging(1, 20)                     // 第1页，每页20条
    .Build();

var result = users.ToPageResult(request);
```

### 方式 3: 手动构建

```csharp
var request = new BasePageRequestDto(1, 20);

// 添加多个过滤条件
request.AddFilter("Status", 1)
    .AddFilter("Age", 18, QueryOperator.GreaterThanOrEqual)
    .AddFilter(QueryFilter.In("Department", "IT", "HR", "Finance"));

// 添加多级排序
request.AddSort("Priority", SortDirection.Ascending, priority: 0)
    .AddSort("CreateTime", SortDirection.Descending, priority: 1);

// 设置关键字搜索
request.SetKeyword("张三", "Name", "Email", "Phone");

var result = users.ToPageResult(request);
```

## 🌟 自动查询

**⭐ 最新特性：零配置自动查询！**

根据 DTO 属性类型和命名约定，**自动构建查询条件**，无需手动 Build！

### 自动识别规则

| 属性类型/命名 | 自动识别为 | 示例 |
|-------------|----------|------|
| `string` | Contains（模糊） | `Name` → `WHERE Name LIKE '%value%'` |
| `int/long/enum` | Equal（精确） | `UserId` → `WHERE UserId = value` |
| `List<T>` | In（多选） | `StatusList` → `WHERE Status IN (...)` |
| `T[]` (长度=2) | Between（范围） | `AgeRange` → `WHERE Age BETWEEN min AND max` |
| 属性名含"Range" | Between | `CreateTimeRange` → Between |
| 属性名含"List/Ids" | In | `UserIds` → In |
| 属性名含"Search/Key" | 关键字搜索 | `SearchKey` → OR 搜索多字段 |

### 完整示例

```csharp
// 1. 定义查询DTO（无需任何配置）
public class OrderQueryDto
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    
    // ⭐ 自动识别为关键字搜索
    public string? SearchKey { get; set; }
    
    // ⭐ 自动识别为精确查询 (Equal)
    public long? UserId { get; set; }
    
    // ⭐ 自动识别为模糊查询 (Contains)
    public string? Account { get; set; }
    
    // ⭐ 自动识别为范围查询 (Between)
    public decimal[]? AmountRange { get; set; }
    
    // ⭐ 自动识别为范围查询 (Between)
    public DateTime[]? CreateTimeRange { get; set; }
    
    // ⭐ 自动识别为多选查询 (In)
    public List<OrderStatusEnum>? StatusList { get; set; }
    
    // ⭐ 自动识别为多选查询 (In)
    public List<long>? ChannelIds { get; set; }
}

// 2. 实体类（只在需要关键字搜索的字段上标注）
public class OrderEntity
{
    [KeywordSearch]  // 参与关键字搜索
    public string? OrderNo { get; set; }
    
    [KeywordSearch]  // 参与关键字搜索
    public string? Account { get; set; }
    
    // 其他字段无需配置
    public long UserId { get; set; }
    public decimal Amount { get; set; }
    public OrderStatusEnum Status { get; set; }
    public DateTime CreateTime { get; set; }
}

// 3. API 控制器 - 一行搞定！
[HttpGet]
public IActionResult GetOrders([FromQuery] OrderQueryDto query)
{
    // ⭐ 自动识别所有查询类型！
    var result = _dbContext.Orders.ToPageResultAuto(query);
    return Ok(result);
}
```

### 手动配置优先

如果需要特殊处理，添加 Attribute，**手动配置优先**：

```csharp
public class OrderQueryDto
{
    // ⭐ 手动禁止过滤
    [QueryField(AllowFilter = false)]
    public string? Password { get; set; }
    
    // ⭐ 手动限制操作符
    [QueryOperatorSupport(QueryOperator.Equal, QueryOperator.NotEqual)]
    public int? Status { get; set; }
    
    // 其他字段自动处理
    public decimal[]? AmountRange { get; set; }  // 自动 Between
    public List<long>? UserIds { get; set; }     // 自动 In
}
```

### 自定义约定

```csharp
var convention = new QueryConvention
{
    StringDefaultContains = true,         // 字符串默认模糊搜索
    ArrayAsBetween = true,               // 数组自动Between
    ListAsIn = true,                     // List自动In
    RangeSuffixes = ["Range", "范围"],
    ListSuffixes = ["List", "Ids", "列表"]
};

var result = dbContext.Orders.ToPageResultAuto(query, convention);
```

**[查看完整自动查询文档](QUICKSTART.md)** →

## 🎯 Attribute 集成

**通过在实体类上使用 Attribute，可以实现：**
- 自动验证查询请求
- 控制字段的查询权限
- 配置关键字搜索行为
- 限制支持的操作符
- 支持字段别名

### 1. 实体类配置示例

```csharp
public class UserEntity
{
    public int Id { get; set; }

    // 可过滤、排序、关键字搜索，支持别名
    [QueryField(Alias = "userName", AllowFilter = true, AllowSort = true)]
    [KeywordSearch(KeywordMatchMode.Contains, priority: 0)]
    public string Name { get; set; }

    // 限制支持的操作符，前缀匹配搜索
    [QueryOperatorSupport(
        QueryOperator.Equal,
        QueryOperator.Contains,
        QueryOperator.StartsWith)]
    [KeywordSearch(KeywordMatchMode.StartsWith, priority: 1)]
    public string Email { get; set; }

    // 只支持数值比较操作符
    [QueryOperatorSupport(
        QueryOperator.Equal,
        QueryOperator.GreaterThan,
        QueryOperator.LessThan,
        QueryOperator.Between)]
    public int Age { get; set; }

    // 不允许过滤和排序
    [QueryField(AllowFilter = false, AllowSort = false)]
    [KeywordSearch(Enabled = false)]
    public string Password { get; set; }
}
```

### 2. 使用自动验证查询

```csharp
// 方式 1: 使用扩展方法（推荐）
var result = dbContext.Users
    .AsQueryable()
    .ToPageResultWithValidation(request, validate: true);

// 方式 2: 使用查询执行器
var executor = new PageQueryExecutor<UserEntity>();
var result = executor.Execute(query, request, validate: true);

// 方式 3: 手动验证
var validationResult = request.ValidateRequest<UserEntity>();
if (!validationResult.IsValid)
{
    throw new InvalidOperationException(validationResult.GetErrorMessage());
}
```

### 3. 自动关键字搜索

```csharp
// 不指定搜索字段，自动使用标注了 KeywordSearchAttribute 的字段
var request = new BasePageRequestDto(1, 10)
{
    Keyword = "张三"
    // KeywordFields 为空，会自动填充为 [Name, Email, ...]
};

var result = users.ToPageResultWithValidation(request);
// 自动在 Name, Email 等字段中搜索 "张三"
```

### 4. 读取实体配置

```csharp
// 获取所有可查询字段
var queryFields = AttributeReader.GetQueryFields<UserEntity>();

// 获取默认关键字搜索字段
var keywordFields = AttributeReader.GetDefaultKeywordFields<UserEntity>();

// 验证字段权限
bool canFilter = AttributeReader.IsFilterAllowed<UserEntity>("Password"); // false
bool canSort = AttributeReader.IsSortAllowed<UserEntity>("Name"); // true

// 验证操作符支持
bool supported = AttributeReader.IsOperatorSupported<UserEntity>(
    "Age", QueryOperator.GreaterThan); // true
```

### 5. 完整的 Attribute 说明

#### QueryFieldAttribute
配置字段的基本查询行为
```csharp
[QueryField(
    Alias = "fieldAlias",        // 字段别名
    AllowFilter = true,          // 是否允许过滤
    AllowSort = true,            // 是否允许排序
    Priority = 0                 // 优先级
)]
```

#### KeywordSearchAttribute
配置关键字搜索行为
```csharp
[KeywordSearch(
    MatchMode = KeywordMatchMode.Contains,  // 匹配模式
    Priority = 0,                           // 搜索优先级
    Enabled = true,                         // 是否启用
    IncludeInDefault = true,                // 是否参与默认搜索
    Alias = "searchAlias"                   // 搜索别名
)]
```

匹配模式：
- `Contains` - 包含 (LIKE %x%)
- `StartsWith` - 前缀匹配 (LIKE x%)
- `EndsWith` - 后缀匹配 (LIKE %x)
- `Exact` - 完全匹配 (=)

#### QueryOperatorSupportAttribute
限制字段支持的查询操作符
```csharp
[QueryOperatorSupport(
    QueryOperator.Equal,
    QueryOperator.NotEqual,
    QueryOperator.Contains
)]
```

如果不指定，系统会根据字段类型自动推断：
- 字符串: Equal, NotEqual, Contains, StartsWith, EndsWith, In, NotIn, IsNull, IsNotNull
- 数值: Equal, NotEqual, GreaterThan, LessThan, Between, In, NotIn
- 日期: Equal, NotEqual, GreaterThan, LessThan, Between
- 布尔: Equal, NotEqual

## 🔧 核心组件

### 1. 查询操作符 (QueryOperator)

支持以下 13 种操作符：

**基础比较（单值）**
- `Equal` - 等于
- `NotEqual` - 不等于
- `GreaterThan` - 大于
- `GreaterThanOrEqual` - 大于等于
- `LessThan` - 小于
- `LessThanOrEqual` - 小于等于

**字符串匹配**
- `Contains` - 包含 (LIKE %x%)
- `StartsWith` - 以...开始 (LIKE x%)
- `EndsWith` - 以...结束 (LIKE %x)

**集合比较**
- `In` - 在集合中
- `NotIn` - 不在集合中

**区间/范围**
- `Between` - 在区间内

**空值判断**
- `IsNull` - 为空
- `IsNotNull` - 不为空

### 2. 查询过滤 (QueryFilter)

```csharp
// 方式1: 构造函数
var filter1 = new QueryFilter("Name", "张三", QueryOperator.Equal);

// 方式2: 静态工厂方法（推荐）
var filter2 = QueryFilter.Equal("Name", "张三");
var filter3 = QueryFilter.Contains("Email", "@gmail");
var filter4 = QueryFilter.Between("Age", 18, 60);
var filter5 = QueryFilter.In("Status", 1, 2, 3);

// 验证过滤条件
if (filter1.IsValid())
{
    // 执行查询
}
```

### 3. 查询排序 (QuerySort)

```csharp
// 方式1: 构造函数
var sort1 = new QuerySort("CreateTime", SortDirection.Descending);

// 方式2: 静态工厂方法（推荐）
var sort2 = QuerySort.Ascending("Name");
var sort3 = QuerySort.Descending("CreateTime", priority: 1);

// 验证排序条件
if (sort1.IsValid())
{
    // 执行查询
}
```

### 4. 分页元数据 (PageResultMetadata)

```csharp
var metadata = new PageResultMetadata(
    pageIndex: 3,
    pageSize: 20,
    totalCount: 150
);

// 使用计算属性
Console.WriteLine($"总页数: {metadata.TotalPages}");        // 8
Console.WriteLine($"有上一页: {metadata.HasPrevious}");      // true
Console.WriteLine($"有下一页: {metadata.HasNext}");          // true
Console.WriteLine($"起始记录: {metadata.StartRecord}");      // 41
Console.WriteLine($"结束记录: {metadata.EndRecord}");        // 60
Console.WriteLine($"当前页记录数: {metadata.CurrentPageCount}"); // 20
```

## 📝 使用示例

### 示例 0: 使用 Attribute 的完整流程（推荐）

```csharp
// 1. 定义实体（配置 Attribute）
public class UserEntity
{
    [QueryField(Alias = "userName")]
    [KeywordSearch(KeywordMatchMode.Contains)]
    public string Name { get; set; }

    [QueryOperatorSupport(QueryOperator.Equal, QueryOperator.NotEqual)]
    public int Status { get; set; }

    [QueryField(AllowFilter = false, AllowSort = false)]
    public string Password { get; set; }
}

// 2. API 端点
[HttpGet]
public IActionResult GetUsers(
    [FromQuery] string? keyword,
    [FromQuery] int? status,
    [FromQuery] int pageIndex = 1,
    [FromQuery] int pageSize = 20)
{
    // 构建请求
    var request = QueryBuilder.Create()
        .SetPaging(pageIndex, pageSize);

    if (status.HasValue)
        request.WhereEqual("Status", status.Value);

    if (!string.IsNullOrWhiteSpace(keyword))
        request.SetKeyword(keyword); // 自动使用 KeywordSearchAttribute 配置的字段

    var requestDto = request.Build();

    // 执行查询（自动验证）
    var result = _dbContext.Users
        .AsQueryable()
        .ToPageResultWithValidation(requestDto); // ⭐ 自动验证 Attribute 配置

    return Ok(result);
}

// 3. 如果请求验证失败，会自动抛出异常
// 例如：尝试过滤 Password 字段会失败
// 例如：对 Status 使用 GreaterThan 操作符会失败
```

### 示例 1: API 端点中使用（传统方式）

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers(
    [FromQuery] string? keyword,
    [FromQuery] int? status,
    [FromQuery] int pageIndex = 1,
    [FromQuery] int pageSize = 20)
{
    // 构建查询
    var builder = QueryBuilder.Create()
        .SetPaging(pageIndex, pageSize);

    if (!string.IsNullOrWhiteSpace(keyword))
    {
        builder.SetKeyword(keyword)
            .AddKeywordField("Name", "Email", "Phone");
    }

    if (status.HasValue)
    {
        builder.WhereEqual("Status", status.Value);
    }

    builder.OrderByDescending("CreateTime");

    var request = builder.Build();

    // 执行查询
    var result = await _dbContext.Users
        .AsQueryable()
        .ToPageResultAsync(request);

    // 映射为 DTO
    var dtoResult = result.Map(user => new UserDto
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email
    });

    return Ok(dtoResult);
}
```

### 示例 2: 使用 Attribute 配置实体

```csharp
public class UserEntity
{
    public int Id { get; set; }

    [QueryField(Alias = "userName")]
    [KeywordSearch(KeywordMatchMode.Contains)]
    public string Name { get; set; }

    [QueryField(AllowFilter = true, AllowSort = true)]
    [KeywordSearch(KeywordMatchMode.StartsWith, priority: 1)]
    public string Email { get; set; }

    [QueryOperatorSupport(
        QueryOperator.Equal,
        QueryOperator.GreaterThan,
        QueryOperator.LessThan,
        QueryOperator.Between)]
    public int Age { get; set; }

    [QueryField(AllowFilter = false, AllowSort = false)]
    public string Password { get; set; }
}
```

### 示例 3: 数据转换和映射

```csharp
// 获取分页结果
var userResult = await _dbContext.Users
    .ToPageResultAsync(request);

// 同步映射
var dtoResult = userResult.Map(user => new UserDto
{
    Id = user.Id,
    Name = user.Name,
    DisplayAge = $"{user.Age}岁"
});

// 异步映射
var dtoResult2 = await userResult.ConvertItemsAsync(async user =>
{
    var avatar = await GetUserAvatarAsync(user.Id);
    return new UserDto
    {
        Id = user.Id,
        Name = user.Name,
        Avatar = avatar
    };
});
```

### 示例 4: 分页辅助方法

```csharp
// 获取分页摘要
var summary = PageHelper.GetPageSummary(3, 20, 150);
// 输出: 第 41-60 条，共 150 条记录，第 3/8 页

// 获取页码范围（用于显示页码按钮）
var pageRange = PageHelper.GetPageRange(5, 10, rangeSize: 2);
// 输出: [3, 4, 5, 6, 7]

// 计算总页数
var totalPages = PageHelper.CalculateTotalPages(150, 20); // 8

// 验证页码
var isValid = PageHelper.IsValidPageIndex(5, totalPages); // true

// 修正页码
var fixedIndex = PageHelper.FixPageIndex(100, totalPages); // 8
```

### 示例 5: 请求验证

```csharp
var request = new BasePageRequestDto(1, 20);
request.AddFilter("Age", -1, QueryOperator.Equal); // 无效条件

// 验证请求
var validationResult = PageValidator.ValidatePageRequest(request);

if (!validationResult.IsValid)
{
    return BadRequest(new
    {
        Message = "请求参数验证失败",
        Errors = validationResult.Errors
    });
}

// 执行查询...
```

## 💡 最佳实践

### 0. 使用 Attribute 配置实体（强烈推荐）

**推荐** ✅ 使用 Attribute 配置，自动验证
```csharp
// 1. 在实体类上配置 Attribute
public class UserEntity
{
    [QueryField(Alias = "userName", AllowSort = true)]
    [KeywordSearch(KeywordMatchMode.Contains)]
    public string Name { get; set; }

    [QueryOperatorSupport(QueryOperator.Equal, QueryOperator.NotEqual)]
    public int Status { get; set; }
}

// 2. 使用自动验证的扩展方法
var result = users.ToPageResultWithValidation(request);
```

**不推荐** ❌ 不配置 Attribute，手动验证
```csharp
// 需要手动验证每个字段和操作符
if (request.Filters.Any(f => f.Field == "Password"))
    throw new Exception("不允许过滤 Password");
// 大量手动验证代码...
```

### 1. 使用查询构建器

**推荐** ✅
```csharp
var request = QueryBuilder.Create()
    .WhereEqual("Status", 1)
    .WhereContains("Name", "张")
    .OrderByDescending("CreateTime")
    .SetPaging(1, 20)
    .Build();
```

**不推荐** ❌
```csharp
var request = new BasePageRequestDto(1, 20);
request.Filters.Add(new QueryFilter { Field = "Status", Value = 1, Operator = QueryOperator.Equal });
request.Filters.Add(new QueryFilter { Field = "Name", Value = "张", Operator = QueryOperator.Contains });
request.Sorts.Add(new QuerySort { Field = "CreateTime", Direction = SortDirection.Descending });
```

### 2. 使用静态工厂方法

**推荐** ✅
```csharp
var filter = QueryFilter.Equal("Name", "张三");
var sort = QuerySort.Descending("CreateTime");
```

**不推荐** ❌
```csharp
var filter = new QueryFilter("Name", "张三", QueryOperator.Equal);
var sort = new QuerySort("CreateTime", SortDirection.Descending);
```

### 3. 使用扩展方法

**推荐** ✅
```csharp
var result = users.ToPageResult(request);
```

**不推荐** ❌
```csharp
var totalCount = users.Count();
var items = users.Skip((request.PageIndex - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToList();
var result = new BasePageResultDto<User>(items, request.PageIndex, request.PageSize, totalCount);
```

### 4. 验证输入参数

```csharp
// 始终验证外部输入
var validationResult = PageValidator.ValidatePageRequest(request);
if (!validationResult.IsValid)
{
    // 处理验证错误
    return BadRequest(validationResult.Errors);
}
```

### 5. 合理设置分页大小限制

```csharp
// PageRequestMetadata 已内置限制
// MinPageSize = 1
// MaxPageSize = 500
// DefaultPageSize = 20

// 如需自定义限制，可以修改 PageRequestMetadata 的常量
```

### 6. 使用 DisablePaging 获取全部数据

```csharp
var request = new BasePageRequestDto(1, 20)
{
    DisablePaging = true  // 返回所有数据，但仍可使用过滤和排序
};
```

### 7. 链式调用提高可读性

```csharp
var result = dbContext.Users
    .AsQueryable()
    .ApplyFilters(request.Filters)
    .ApplyKeywordSearch(request.Keyword, request.KeywordFields.ToArray())
    .ApplySorts(request.Sorts)
    .ToPageResult(request);
```

## 📚 更多示例

### 基础使用示例
`Examples/PagingUsageExamples.cs` - 基础功能示例：
- ✅ 基础分页查询
- ✅ 查询构建器使用
- ✅ 复杂过滤和排序
- ✅ 数据转换和映射
- ✅ 请求验证
- ✅ 辅助方法使用

### ⭐ 完整集成示例（强烈推荐）
`Examples/IntegratedUsageExample.cs` - Attribute 驱动的完整示例：
- ✅ 实体类 Attribute 配置
- ✅ 基于 Attribute 的自动验证
- ✅ AttributeReader 元数据读取
- ✅ 自动关键字搜索
- ✅ 字段别名支持
- ✅ 完整的 API 端点实现
- ✅ 动态查询配置

**运行示例：**
```csharp
var example = new IntegratedUsageExample();
example.RunAllExamples(); // 运行所有示例
```

## 🔗 相关文档

- [QueryOperator 操作符说明](Enums/QueryOperator.cs)
- [QueryBuilder API 文档](Builders/QueryBuilder.cs)
- [PageExtensions 扩展方法](PageExtensions.cs)
- [AttributePageExtensions Attribute扩展](AttributePageExtensions.cs)
- [PageQueryExecutor 查询执行器](Executors/PageQueryExecutor.cs)
- [AttributeReader 反射工具](Reflection/AttributeReader.cs)
- [基础使用示例](Examples/PagingUsageExamples.cs)
- [⭐ 完整集成示例](Examples/IntegratedUsageExample.cs)

## 🎉 总结

### 为什么选择这个分页模块？

1. **功能完善** - 支持所有常见分页查询场景
2. **Attribute 驱动** - 通过配置实体类实现自动验证和查询
3. **类型安全** - 强类型设计，编译时检查
4. **易于使用** - Fluent API、扩展方法、静态工厂
5. **性能优异** - 使用 IQueryable 延迟执行
6. **可扩展** - 清晰的架构，易于扩展
7. **文档完善** - 详细的文档和丰富的示例

### 推荐使用流程

1. **配置实体** - 在实体类上添加 Attribute
2. **构建请求** - 使用 QueryBuilder 构建查询
3. **执行查询** - 使用 `ToPageResultWithValidation()` 自动验证并执行
4. **处理结果** - 使用 `Map()` 转换为 DTO

### 快速开始

```csharp
// 1. 配置实体
public class User
{
    [QueryField][KeywordSearch]
    public string Name { get; set; }
}

// 2. 查询
var result = dbContext.Users
    .ToPageResultWithValidation(
        QueryBuilder.Create()
            .SetKeyword("张三")
            .SetPaging(1, 20)
            .Build()
    );
```

就这么简单！🚀

## 📄 许可证

MIT License - Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
