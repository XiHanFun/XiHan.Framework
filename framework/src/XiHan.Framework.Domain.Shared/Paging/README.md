# XiHan.Framework.Domain.Shared.Paging

企业级通用分页查询框架 - 基于"查询条件"与"查询行为"分离的架构设计

## 核心设计原则

### 类型层面的职责分离

```
查询条件（QueryConditions）  → 进入 Expression Tree
查询行为（QueryBehavior）    → 影响 Queryable 管道
分页参数（PageRequestMetadata）→ Skip/Take 计算
```

### 架构图

```
┌─────────────────────────────────────┐
│     PageRequestDtoBase              │
├─────────────────────────────────────┤
│ • QueryConditions  Conditions       │ ← 查询条件
│ • QueryBehavior    Behavior         │ ← 查询行为
│ • PageRequestMetadata  Page         │ ← 分页参数
└─────────────────────────────────────┘
           │
           ├─→ QueryConditions
           │   ├─ List<QueryFilter>  Filters
           │   ├─ List<QuerySort>    Sorts
           │   └─ QueryKeyword?      Keyword
           │
           ├─→ QueryBehavior
           │   ├─ bool DisablePaging
           │   ├─ bool DisableDefaultSort
           │   ├─ bool IgnoreTenant
           │   ├─ bool IgnoreSoftDelete
           │   ├─ bool EnableTracking
           │   ├─ bool EnableSplitQuery
           │   └─ int? QueryTimeout
           │
           └─→ PageRequestMetadata
               ├─ int PageIndex
               └─ int PageSize
```

## 目录结构

```
Paging/
├── Dtos/                           # 数据传输对象
│   ├── PageRequestDtoBase.cs      # 分页请求基类
│   └── PageResultDtoBase.cs       # 分页结果基类
│
├── Models/                         # 核心模型
│   ├── QueryConditions.cs         # 查询条件（Filters + Sorts + Keyword）
│   ├── QueryBehavior.cs           # 查询行为控制
│   ├── QueryFilter.cs             # 单个过滤条件
│   ├── QuerySort.cs               # 单个排序条件
│   ├── QueryKeyword.cs            # 关键字搜索
│   ├── PageRequestMetadata.cs     # 分页请求元数据
│   └── PageResultMetadata.cs      # 分页结果元数据
│
├── Builders/                       # 构建器
│   ├── QueryBuilder.cs            # 流式查询构建器
│   └── AutoQueryBuilder.cs        # 自动查询构建器（反射）
│
├── Executors/                      # 执行器
│   └── PageQueryExecutor.cs       # 分页查询执行器
│
├── Validators/                     # 验证器
│   ├── PageValidator.cs           # 分页验证器
│   ├── AttributeBasedValidator.cs # 基于特性的验证器
│   └── ValidationResult.cs        # 验证结果
│
├── Converters/                     # 转换器
│   └── PageConverter.cs           # 分页数据转换
│
├── Enums/                          # 枚举
│   ├── QueryOperator.cs           # 查询操作符（Equal/In/Between...）
│   ├── SortDirection.cs           # 排序方向（Ascending/Descending）
│   └── KeywordMatchMode.cs        # 关键字匹配模式
│
├── Attributes/                     # 特性标注
│   ├── QueryFieldAttribute.cs     # 查询字段特性
│   ├── KeywordSearchAttribute.cs  # 关键字搜索特性
│   └── QueryOperatorSupportAttribute.cs
│
├── Conventions/                    # 约定配置
│   └── QueryConvention.cs         # 查询约定（自动推断规则）
│
├── Helpers/                        # 辅助工具
│   └── PageHelper.cs              # 分页计算辅助
│
├── Reflection/                     # 反射工具
│   └── AttributeReader.cs         # 特性读取器
│
└── Extensions/                     # 扩展方法
    ├── PageExtensions.cs          # IQueryable 分页扩展
    ├── AutoQueryExtensions.cs     # 自动查询扩展
    └── AttributePageExtensions.cs # 基于特性的扩展
```

## 快速开始

### 1. 基本用法

```csharp
// 创建分页请求
var request = new PageRequestDtoBase
{
    Page = new PageRequestMetadata(1, 20),
    Conditions = new QueryConditions
    {
        Filters = [QueryFilter.Equal("Status", 1)],
        Sorts = [QuerySort.Descending("CreatedTime")]
    }
};

// 应用分页查询
var result = await query.ToPageResultAsync(request);
```

### 2. 流式 API

```csharp
var request = PageRequestDtoBase.Create(1, 20)
    .WithFilter("Status", 1, QueryOperator.Equal)
    .WithSort("CreatedTime", SortDirection.Descending)
    .WithKeyword("关键字", "Name", "Code");

var result = await query.ToPageResultAsync(request);
```

### 3. QueryBuilder

```csharp
var request = QueryBuilder.Create()
    .WhereEqual("Status", 1)
    .WhereContains("Name", "admin")
    .OrderByDescending("CreatedTime")
    .SetPageIndex(1)
    .SetPageSize(20)
    .Build();
```

### 4. 自定义分页 DTO

```csharp
public class UserPageRequestDto : PageRequestDtoBase
{
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public YesOrNo? Status { get; set; }
}

// 控制器
[HttpPost]
public async Task<PageResultDtoBase<UserDto>> GetPage(UserPageRequestDto request)
{
    return await _repository.GetPagedAutoAsync(request);
}
```

## 核心类型

### 1. QueryConditions（查询条件）

**职责**：描述"查什么"，可安全进入 Expression Tree

```csharp
public sealed class QueryConditions
{
    public List<QueryFilter> Filters { get; set; } = [];
    public List<QuerySort> Sorts { get; set; } = [];
    public QueryKeyword? Keyword { get; set; }
    
    public bool IsEmpty { get; }
    public bool HasConditions { get; }
    
    // 链式调用
    public QueryConditions AddFilter(string field, object? value, QueryOperator op);
    public QueryConditions AddSort(string field, SortDirection direction);
    public QueryConditions SetKeyword(string? keyword, params string[] fields);
    
    // 批量操作
    public QueryConditions AddFilters(IEnumerable<QueryFilter> filters);
    public QueryConditions AddSorts(IEnumerable<QuerySort> sorts);
    
    // 移除操作
    public QueryConditions RemoveFilter(string field);
    public QueryConditions RemoveSort(string field);
    public QueryConditions Clear();
    
    // 深拷贝
    public QueryConditions Clone();
}
```

### 2. QueryBehavior（查询行为）

**职责**：控制"怎么查"，影响 Queryable 管道行为

```csharp
public sealed class QueryBehavior
{
    public bool DisablePaging { get; set; }         // 禁用分页
    public bool DisableDefaultSort { get; set; }    // 禁用默认排序
    public bool IgnoreTenant { get; set; }          // 忽略多租户过滤
    public bool IgnoreSoftDelete { get; set; }      // 忽略软删除过滤
    public bool EnableTracking { get; set; }        // 启用 EF Core 追踪
    public bool EnableSplitQuery { get; set; }      // 启用拆分查询
    public int? QueryTimeout { get; set; }          // 查询超时（秒）
    
    public bool IsDefault { get; }
    public QueryBehavior Clone();
}
```

### 3. QueryFilter（过滤条件）

**支持的操作符**：
- `Equal` / `NotEqual`
- `GreaterThan` / `GreaterThanOrEqual` / `LessThan` / `LessThanOrEqual`
- `Contains` / `StartsWith` / `EndsWith`
- `In` / `NotIn` (使用 `Values` 字段)
- `Between` (使用 `Values` 字段)
- `IsNull` / `IsNotNull`

```csharp
public sealed class QueryFilter
{
    public string Field { get; set; }
    public QueryOperator Operator { get; set; }
    public object? Value { get; set; }       // 单值（Equal/Contains...）
    public object[]? Values { get; set; }    // 多值（In/Between）
    
    // 静态工厂方法
    public static QueryFilter Equal(string field, object value);
    public static QueryFilter In(string field, params object[] values);
    public static QueryFilter Between(string field, object start, object end);
    
    public bool IsValid();
    public override string ToString();  // 调试输出
}
```

### 4. QueryKeyword（关键字搜索）

```csharp
public sealed class QueryKeyword
{
    public string? Value { get; set; }         // 关键字
    public List<string> Fields { get; set; }   // 搜索字段
    public KeywordMatchMode MatchMode { get; set; } = KeywordMatchMode.Contains;
    
    public bool IsValid { get; }
    public bool IsEmpty { get; }
    
    public QueryKeyword AddField(string field);
    public QueryKeyword AddFields(IEnumerable<string> fields);
    public QueryKeyword CleanEmptyFields();
    public QueryKeyword Clone();
}
```

## 前端请求示例

```json
{
  "conditions": {
    "filters": [
      { "field": "Status", "operator": 1000, "value": 1 },
      { "field": "Age", "operator": 4000, "values": [18, 65] },
      { "field": "RoleIds", "operator": 3000, "values": [1, 2, 3] }
    ],
    "sorts": [
      { "field": "CreatedTime", "direction": 1001, "priority": 1 }
    ],
    "keyword": {
      "value": "关键字",
      "fields": ["Name", "Code"],
      "matchMode": 1000
    }
  },
  "behavior": {
    "disablePaging": false,
    "ignoreTenant": false,
    "enableTracking": false
  },
  "page": {
    "pageIndex": 1,
    "pageSize": 20
  }
}
```

## 操作符枚举值

### QueryOperator

| 操作符 | 值 | 使用字段 | 说明 |
|--------|------|----------|------|
| Equal | 1000 | Value | 等于 |
| NotEqual | 1001 | Value | 不等于 |
| GreaterThan | 1002 | Value | 大于 |
| GreaterThanOrEqual | 1003 | Value | 大于等于 |
| LessThan | 1004 | Value | 小于 |
| LessThanOrEqual | 1005 | Value | 小于等于 |
| Contains | 2000 | Value | 包含（字符串） |
| StartsWith | 2001 | Value | 开头匹配 |
| EndsWith | 2002 | Value | 结尾匹配 |
| In | 3000 | Values | 在集合中 |
| NotIn | 3001 | Values | 不在集合中 |
| Between | 4000 | Values | 区间查询 |
| IsNull | 5000 | - | 为空 |
| IsNotNull | 5001 | - | 不为空 |

### SortDirection

| 方向 | 值 |
|------|------|
| Ascending | 1000 |
| Descending | 1001 |

### KeywordMatchMode

| 模式 | 值 |
|------|------|
| Contains | 1000 |
| StartsWith | 1001 |
| EndsWith | 1002 |
| Exact | 1003 |

## 扩展方法

### IQueryable 扩展

```csharp
// 应用分页
query.ApplyPaging(request);

// 应用完整查询并返回结果
var result = await query.ToPageResultAsync(request);

// 带验证的查询
var result = query.ToPageResultWithValidation<User>(request);
```

### SqlSugar 扩展

```csharp
// 自动查询（DTO 继承 PageRequestDtoBase）
var result = await query.ToPageResultAutoAsync(dto);

// 应用查询条件
query.ApplyPageRequest(request);
query.ApplyFilters(filters);
query.ApplySorts(sorts);
query.ApplyKeywordSearch(keyword, fields);
```

## 高级特性

### 1. 特性标注

```csharp
public class User
{
    [QueryField(Operators = [QueryOperator.Equal, QueryOperator.In])]
    public long Id { get; set; }
    
    [KeywordSearch]  // 参与关键字搜索
    public string Name { get; set; }
    
    [QueryField(MaxLength = 100)]
    public string Email { get; set; }
}
```

### 2. AutoQueryBuilder（反射构建）

```csharp
// 普通 DTO 自动推断查询条件
public class UserQueryDto
{
    public string? UserName { get; set; }     // → Equal "UserName"
    public int[] StatusList { get; set; }     // → In "Status"
    public DateTime[] CreateTimeRange { get; set; }  // → Between "CreateTime"
}

var request = AutoQueryBuilder.BuildFrom(dto);
```

### 3. 验证

```csharp
// 基础验证
var result = PageValidator.ValidateRequest(request);

// 基于实体特性的验证
var result = AttributeBasedValidator.ValidatePageRequest<User>(request);

// 自动验证并抛出异常
var validated = request.EnsureValid<User>();
```

## 与 ORM 集成

### SqlSugar

```csharp
// Repository 层
public async Task<PageResultDtoBase<User>> GetPagedAutoAsync(
    PageRequestDtoBase request)
{
    var query = _dbClient.Queryable<User>();
    return await query.ToPageResultAutoAsync(request);
}
```

**ApplyFilters** - 自动构建 Expression Tree：
```csharp
query = query.ApplyFilters(request.Conditions.Filters);
// 生成 SQL: WHERE Status = 1 AND Age BETWEEN 18 AND 65
```

**ApplySorts** - 字符串排序语法：
```csharp
query = query.ApplySorts(request.Conditions.Sorts);
// 生成 SQL: ORDER BY CreatedTime DESC, Sort ASC
```

### EF Core（通过 IQueryable 扩展）

```csharp
var query = _dbContext.Users.AsQueryable();
var result = await query.ToPageResultAsync(request);
```

## 性能优化

### 1. 避免 N+1 查询

```csharp
request.Behavior.EnableSplitQuery = true;  // EF Core 拆分查询
```

### 2. 禁用追踪

```csharp
request.Behavior.EnableTracking = false;  // 只读查询
```

### 3. 查询超时控制

```csharp
request.Behavior.QueryTimeout = 30;  // 30秒超时
```

### 4. 条件短路

```csharp
if (request.Conditions.IsEmpty)
{
    return PageResultDtoBase<T>.Empty(1, 20);
}
```

## 类型安全保证

### 编译时检查

```csharp
// ✅ 类型安全
request.Conditions.Filters  // List<QueryFilter>
request.Behavior.DisablePaging  // bool

// ❌ 编译错误
request.Conditions.DisablePaging  // 编译不通过
request.Behavior.Filters  // 编译不通过
```

### 运行时隔离

```csharp
// QueryConditions → 进入 Expression Tree（类型安全）
query.Where(BuildExpression(conditions.Filters));

// QueryBehavior → 不进入 Expression Tree（逻辑控制）
if (!behavior.DisablePaging) { query.Skip(...).Take(...); }
```

## 常见问题

### Q1: 为什么 QueryFilter 有 Value 和 Values 两个字段？

**A**: 根据操作符类型区分：
- `Value` - 单值操作符（Equal、Contains、GreaterThan...）
- `Values` - 多值操作符（In、NotIn、Between）

```csharp
// 单值
QueryFilter.Equal("Status", 1)  → Value = 1

// 多值
QueryFilter.In("Status", 1, 2, 3)  → Values = [1, 2, 3]
QueryFilter.Between("Age", 18, 65)  → Values = [18, 65]
```

### Q2: 为什么要分离 QueryConditions 和 QueryBehavior？

**A**: 
- `QueryConditions` 描述数据筛选逻辑，可以安全转换为 Expression Tree
- `QueryBehavior` 控制查询管道行为（分页、租户、缓存），不应进入 Expression
- 类型层面的隔离避免了反序列化时将控制标志错误映射到实体字段

### Q3: Skip 和 Take 为什么不在 PageRequestMetadata 中？

**A**: 它们是计算属性，应该在使用时动态计算：
```csharp
var skip = (pageIndex - 1) * pageSize;
query.Skip(skip).Take(pageSize);
```
避免序列化冗余字段，保持 DTO 简洁。

### Q4: 为什么排序不生效？

**A**: 确保：
1. DTO 继承自 `PageRequestDtoBase` 时，`ToPageResultAutoAsync` 会直接使用 `Conditions.Sorts`
2. SqlSugar 使用字符串排序：`query.OrderBy("Field1 DESC, Field2 ASC")`
3. 字段名大小写应与实体属性一致

### Q5: AutoQueryBuilder 什么时候使用？

**A**: 
- DTO **继承** `PageRequestDtoBase` → **不使用** AutoQueryBuilder（直接取 Conditions）
- DTO **不继承** `PageRequestDtoBase` → 使用 AutoQueryBuilder 反射推断

## 扩展点

### 1. 自定义操作符

```csharp
public enum CustomOperator
{
    JsonContains = 2000,
    FullTextSearch = 2001
}
```

### 2. 自定义验证规则

```csharp
public class CustomValidator : IPageValidator
{
    public ValidationResult Validate(PageRequestDtoBase request)
    {
        // 自定义验证逻辑
    }
}
```

### 3. 自定义查询约定

```csharp
var convention = new QueryConvention
{
    StringDefaultContains = false,  // 字符串默认精确匹配
    ArrayAsBetween = true           // 数组自动识别为区间
};
```

## 最佳实践

### 1. DTO 设计

```csharp
// ✅ 推荐：继承 PageRequestDtoBase
public class UserPageRequestDto : PageRequestDtoBase
{
    public string? UserName { get; set; }
    public YesOrNo? Status { get; set; }
}

// ❌ 不推荐：不继承基类（需要手动构建 Conditions）
public class UserQueryDto
{
    public string? UserName { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}
```

### 2. 前端集成

```typescript
// TypeScript 类型定义
interface PageRequest<T = any> {
  conditions: {
    filters?: QueryFilter[];
    sorts?: QuerySort[];
    keyword?: QueryKeyword;
  };
  behavior?: {
    disablePaging?: boolean;
    ignoreTenant?: boolean;
  };
  page: {
    pageIndex: number;
    pageSize: number;
  };
}
```

### 3. 性能建议

- 索引字段：过滤和排序字段应建立索引
- 分页大小：限制在 1-500 范围（默认 20）
- 关键字搜索：限制搜索字段数量（建议 ≤ 5）
- 避免深度分页：使用游标分页或 Keyset Pagination

## 架构演进

### v1.0 - 扁平结构（已废弃）
```csharp
public class PageRequest
{
    public int PageIndex { get; set; }
    public List<QueryFilter> Filters { get; set; }
    public bool DisablePaging { get; set; }  // ❌ 混在一起
}
```

### v2.0 - 当前架构
```csharp
public class PageRequestDtoBase
{
    public QueryConditions Conditions { get; set; }  // ✅ 查询条件
    public QueryBehavior Behavior { get; set; }      // ✅ 查询行为
    public PageRequestMetadata Page { get; set; }    // ✅ 分页参数
}
```

**优势**：
- ✅ 类型安全：编译时防止条件与行为混淆
- ✅ 扩展性：每个类型独立扩展，互不影响
- ✅ 可读性：语义清晰，职责明确
- ✅ 性能：避免反射错误（如将 Conditions 映射到实体字段）

## 许可证

MIT License - Copyright ©2021-Present ZhaiFanhua
