# XiHan.Framework.Domain.Shared

> 领域层共享包：跨领域层与应用层复用的分页查询子系统（声明式过滤/排序/关键字 + 分页 DTO），以及少量共享枚举与常量。

- **NuGet**：`XiHan.Framework.Domain.Shared`
- **模块类**：`XiHanDomainSharedModule`（`ConfigureServices` 为空，不注册任何服务；仅作依赖占位）
- **所在层**：领域与应用层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（`XiHan.Framework.Core` / `XiHan.Framework.Utils`）

## 概述

这个包放的是「领域层」与「应用层」都要用到的共享定义，核心是一整套 **分页查询子系统**：请求里怎么声明式地描述过滤、排序、关键字搜索与分页参数，响应里怎么组织分页结果与元数据。因为它同时被 `Domain`、`Application.Contracts`、`Application` 引用，所以放在这个 **不依赖任何持久化 / Web** 的共享包里，避免各层重复定义、互相耦合。

设计上把「查什么」（`QueryConditions`，永远可安全进入 `Expression`）与「怎么查」（`QueryBehavior`，只影响 `IQueryable` 管道、不进入表达式）拆开，是这套查询协议的关键约定。包里同时内置了一组基于 `IQueryable` / `IEnumerable` 的扩展方法，能把这些声明式条件直接翻译成 LINQ 查询，因此它既是「协议契约」，也是「内存 / 表达式层的默认执行器」。

## 何时使用

- 需要一个统一的分页请求/响应模型，让前后端约定一致的查询协议
- 想用「过滤条件 + 排序 + 关键字」这种声明式方式描述查询，而不是给每个接口手写一堆参数
- 定义应用服务/仓储的分页接口时，需要 `PageRequestDtoBase` / `` `PageResultDtoBase<T>` `` 作为基类
- 想从一个普通查询 DTO（按属性命名约定）自动构建查询条件（`AutoQueryBuilder` / `AutoBuild`）
- 需要在内存集合 / `IQueryable` 上就地套用这套条件（`PageExtensions` 的 `ApplyPageRequest` / `ApplyFilters` / `ApplySorts` / `ApplyKeywordSearch`）

## 安装与启用

```bash
dotnet add package XiHan.Framework.Domain.Shared
```

```csharp
[DependsOn(typeof(XiHanDomainSharedModule))]
public class MyModule : XiHanModule { }
```

`XiHanDomainSharedModule.ConfigureServices` 目前是空实现——本包不注册任何服务、不接任何中间件，纯粹是共享类型 + 扩展方法的集合。通常你不会直接依赖它，而是通过依赖 `XiHan.Framework.Domain` 间接引入。

## 工作原理

分页请求由三块正交的部分组成，理解它们的边界是用好本包的关键：

```text
PageRequestDtoBase
├─ Conditions : QueryConditions   —— “查什么”，可安全翻译为 Expression
│   ├─ Filters  : List<QueryFilter>   多字段过滤（AND 关系）
│   ├─ Sorts    : List<QuerySort>     多字段排序（按 Priority 升序，值越小越优先）
│   └─ Keyword  : QueryKeyword?       关键字 + 参与搜索的字段（字段间 OR 关系）
├─ Behavior   : QueryBehavior     —— “怎么查”，只影响 Queryable 管道，永不进入 Expression
│   （DisablePaging / DisableDefaultSort / IgnoreTenant / IgnoreSoftDelete / EnableSplitQuery / QueryTimeout）
└─ Page       : PageRequestMetadata   —— PageIndex / PageSize（带边界钳制）
```

`PageExtensions.ApplyPageRequest<T>` 的执行顺序是：先套 `Filters`（AND 聚合），再套关键字搜索（对每个字段 `Contains` 做 OR 合并），再按优先级套多字段排序（首个用 `OrderBy`/`OrderByDescending`，其余用 `ThenBy`/`ThenByDescending`），最后在 `Behavior.DisablePaging == false` 时套 `Skip/Take` 分页。

> 注意：本包内的表达式实现是「通用内存/`IQueryable` 版」。在真正的仓储层（`XiHan.Framework.Data`，基于 SqlSugar），过滤/排序/关键字/软删除/多租户/分表等会由持久化实现重新翻译，`QueryBehavior` 的 `IgnoreTenant`/`IgnoreSoftDelete`/`EnableSplitQuery`/`QueryTimeout` 等字段才真正生效。

## 核心能力

- **声明式分页请求**：`PageRequestDtoBase` 承载 `Conditions` / `Behavior` / `Page`，并提供 `WithFilter` / `WithSort` / `WithKeyword` / `WithPage` / `WithoutPaging` 链式方法
- **结构化查询条件**：`QueryConditions` 聚合多字段过滤、多字段排序、关键字搜索，并有 `AddFilter/AddSort/SetKeyword/RemoveFilter/Clear/Clone/IsValid` 等操作
- **丰富的查询操作符**：`QueryOperator` 覆盖等值/比较、`Contains`/`StartsWith`/`EndsWith`、`In`/`NotIn`、`Between`、`IsNull`/`IsNotNull`
- **分页结果封装**：`` `PageResultDtoBase<T>` `` 组织数据项、`PageResultMetadata`（总数/总页数/上一页下一页等派生属性）与可选 `ExtendDatas`
- **两套构建器**：手工链式的 `QueryBuilder`；以及按 DTO 属性 + 命名约定 **自动构建** 条件的 `AutoQueryBuilder`
- **命名约定**：`QueryConvention` 定义「哪些属性后缀是范围/列表/关键字」「字符串默认模糊还是精确」等推断规则
- **就地执行扩展**：`PageExtensions` 在 `IQueryable`/`IEnumerable`/`List` 上直接套过滤、排序、关键字、分页并产出 `` `PageResultDtoBase<T>` ``
- **基于特性的校验**：`QueryFieldAttribute` / `KeywordSearchAttribute` / `QueryOperatorSupportAttribute` 标注实体属性，配合 `AttributePageExtensions` 做白名单式校验（`ValidateRequest` / `EnsureValid` / `TryValidate`）
- **面向实体的执行引擎**：`` `PageQueryExecutor<T>` ``（`Paging.Executors`）是 `AttributePageExtensions` / `AutoQueryExtensions` 背后的真正实现——按 `QueryFieldAttribute` 解析字段别名、按 `QueryOperatorSupportAttribute` 校验操作符、按 `KeywordSearchAttribute` 回填默认关键字字段，再执行过滤/关键字/排序/分页，全程带白名单校验（可关闭）

## 主要 API / 类型

### 请求 / 结果 DTO

| 类型 | 说明 |
| --- | --- |
| `PageRequestDtoBase` | 分页请求基类。属性 `Conditions` / `Behavior` / `Page`；链式方法 `WithFilter(field, value, op)` / `WithSort(field, dir)` / `WithKeyword(keyword, params fields)` / `WithPage(index, size)` / `WithoutPaging()`；静态 `Default()` |
| `` `PageResultDtoBase<T>` `` | 分页响应基类。属性 `Items` / `Page` / `ExtendDatas?`；静态 `Empty(...)` / `Create(...)`；实例 `` `Map<TTarget>(Func<T,TTarget>)` `` |

### 查询条件模型（Models）

| 类型 | 说明 |
| --- | --- |
| `QueryConditions` | 条件容器：`Filters` / `Sorts` / `Keyword`；方法 `AddFilter` / `AddSort` / `SetKeyword` / `RemoveFilter` / `RemoveSort` / `Clear` / `IsValid` / `Clone` |
| `QueryFilter` | 单条过滤：`Field`（空白抛 `ArgumentException`）/ `Value` / `Values`（`In`/`Between` 多值）/ `Operator`；静态工厂 `Equal/NotEqual/GreaterThan/GreaterThanOrEqual/LessThan/LessThanOrEqual/Contains/StartsWith/EndsWith/In/NotIn/Between/IsNull/IsNotNull`；`IsValid()` 按操作符校验值 |
| `QuerySort` | 单条排序：`Field` / `Direction` / `Priority`（越小越优先，默认 0）；静态 `Ascending/Descending`；`IsValid()` |
| `QueryKeyword` | 关键字搜索：`Value?` / `Fields`（`List<string>`）/ `MatchMode`；方法 `AddField` / `AddFields` / `CleanEmptyFields` / `ClearFields` / `Clone` |
| `QueryBehavior` | 查询行为：`DisablePaging` / `DisableDefaultSort` / `IgnoreTenant` / `IgnoreSoftDelete` / `EnableSplitQuery` / `QueryTimeout?`（秒），均默认关闭 |
| `PageRequestMetadata` | 分页入参：`PageIndex`（<1 钳为 1）/ `PageSize`（<`MinPageSize` 回退默认、>`MaxPageSize` 钳到上限）；常量 `DefaultPageIndex=1` / `DefaultPageSize=20` / `MinPageSize=1` / `MaxPageSize=500` |
| `PageResultMetadata` | 分页出参：`PageIndex` / `PageSize` / `TotalCount`；派生只读 `TotalPages` / `HasPrevious` / `HasNext` / `IsFirstPage` / `IsLastPage` / `StartRecord` / `EndRecord` / `CurrentPageCount` |

### 枚举（Enums）

| 枚举 | 值（`= 数值`，带 `[Description]`） |
| --- | --- |
| `QueryOperator` | `Equal=1000` `NotEqual=1001` `GreaterThan=1002` `GreaterThanOrEqual=1003` `LessThan=1004` `LessThanOrEqual=1005` / `Contains=2000` `StartsWith=2001` `EndsWith=2002` / `In=3000` `NotIn=3001` / `Between=4000` / `IsNull=5000` `IsNotNull=5001` |
| `SortDirection` | `Ascending=1000` `Descending=1001` |
| `KeywordMatchMode` | `Contains=1000` `StartsWith=1001` `EndsWith=1002` `Exact=1003` |

### 构建器 / 约定（Builders / Conventions）

| 类型 | 说明 |
| --- | --- |
| `QueryBuilder` | 手工链式构建器。静态 `Create()` / `FromRequest(request)`；`WhereEqual/WhereNotEqual/WhereGreaterThan/WhereLessThan/WhereContains/WhereIn/WhereBetween/WhereNull/WhereNotNull`、`OrderBy/OrderByDescending`、`SetKeyword/AddKeywordField/SetKeywordSearch`、`SetPaging`；`Build()` 产出 `PageRequestDtoBase` |
| `AutoQueryBuilder` | 反射 DTO 属性 + 命名约定自动构建条件。静态 `BuildFrom(dto)` / `FromDto(dto)`；`Build()`。规则：字符串默认 `Contains`；`*Range` 且数组长度 2 → `Between`；集合/`*List`/`*Ids` → `In`；带 `Search`/`Key`/`Keyword` 命名的属性作关键字输入 |
| `QueryConvention` | 自动推断规则配置。`RangeSuffixes=["Range"]` / `ListSuffixes=["List","Ids","s"]` / `KeywordPatterns=["Search","Key","Keyword"]` / `StringDefaultContains=true` / `StringDefaultKeywordSearch=false` 等；`Default` 单例；`InferOperators(type,name)` 按类型推断合法操作符集合 |

### 特性（Attributes，标注在实体属性上）

| 特性 | 说明 |
| --- | --- |
| `QueryFieldAttribute` | 标记属性可过滤/排序。`Alias`（前端字段名→属性名）/ `AllowFilter=true` / `AllowSort=true` / `Priority=0` |
| `KeywordSearchAttribute` | 标记属性参与关键字搜索。`Enabled=true` / `MatchMode=Contains` / `Priority=0` / `IncludeInDefault=true` / `Alias` |
| `QueryOperatorSupportAttribute` | 白名单：`SupportedOperators`（构造入参 `params QueryOperator[]`）；`IsSupported(op)` / `GetSupportedOperatorsDescription()`，用于拦截非法操作符 |

### 执行引擎 / 反射解析 / 校验器（Executors / Reflection / Validators）

这一组类型是 `AttributePageExtensions` 与 `AutoQueryExtensions`（当 `validate: true`）背后真正的执行者，属于文档此前未展开的一层：

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `` `PageQueryExecutor<T>` `` | `Paging.Executors` | 面向实体 `T` 的分页查询执行器。构造时通过 `AttributeReader.GetQueryFields<T>()` 建立字段信息表；`Execute(query, request, validate=true)` / `ExecuteAsync(...)`：先按 `validate` 调 `AttributeBasedValidator.ValidatePageRequest<T>`（失败抛 `InvalidOperationException`），再解析 `Filter`/`Sort` 的字段别名到真实属性名、按 `KeywordSearchAttribute` 回填未指定字段时的默认关键字搜索字段，最后执行过滤→关键字→排序→分页；另有 `GetQueryFields()` / `GetDefaultKeywordFields()` |
| `AttributeReader` | `Paging.Reflection` | 静态反射工具：`GetQueryFields<T>()` / `GetQueryFields(Type)` 读取每个公开属性上的 `QueryFieldAttribute` / `KeywordSearchAttribute` / `QueryOperatorSupportAttribute`，产出 `Dictionary<string, QueryFieldInfo>`（属性名与 `Alias` 都作为键，大小写不敏感）；`GetDefaultKeywordFields<T>()` / `(Type)` 按 `KeywordSearchAttribute.Priority` 排序返回参与默认关键字搜索的属性；`IsFilterAllowed` / `IsSortAllowed` / `IsOperatorSupported`（无特性时按属性类型推断默认支持的操作符） |
| `QueryFieldInfo` | `Paging.Reflection` | `AttributeReader` 的产出模型：`PropertyName` / `PropertyType` / `Alias` / `AllowFilter` / `AllowSort` / `Priority` / `KeywordSearchEnabled` / `KeywordMatchMode` / `KeywordPriority` / `IncludeInDefaultKeywordSearch` / `KeywordAlias` / `SupportedOperators` |
| `PageValidator` | `Paging.Validators` | 不依赖特性的基础校验：`ValidatePageRequest` / `ValidatePageMetadata` / `ValidateFilter` / `ValidateSort`，均返回 `ValidationResult` |
| `AttributeBasedValidator` | `Paging.Validators` | 基于实体特性的校验，是 `AttributePageExtensions.ValidateRequest/EnsureValid/TryValidate` 与 `PageQueryExecutor<T>` 内部校验的实现：`ValidatePageRequest<T>(request)` / `ValidatePageRequest(entityType, request)`（先套 `PageValidator` 基础校验，再逐条检查字段是否存在/允许过滤或排序/操作符是否受支持、关键字字段是否为字符串类型）；`GetFieldValidationInfo<T>(fieldName)` / `(entityType, fieldName)` 输出单个字段的可读校验信息（调试/错误提示用） |

### 扩展方法（Extensions）

| 静态类 | 关键方法 |
| --- | --- |
| `PageExtensions` | `` `ApplyFilter<T>` `` / `` `ApplyFilters<T>` `` / `` `ApplySort<T>` `` / `` `ApplySorts<T>` `` / `` `ApplyKeywordSearch<T>` `` / `` `ApplyPaging<T>` `` / `` `ApplyPageRequest<T>` `` / `` `ToPageResult<T>` `` / `` `ToPageResultAsync<T>` ``（`IQueryable`/`IEnumerable`/`List`） |
| `AttributePageExtensions` | `` `ToPageResultWithValidation<T>` `` / `` `ToPageResultWithValidationAsync<T>` ``（内部委托给 `` `PageQueryExecutor<T>` ``）/ `` `ValidateRequest<T>` `` / `` `EnsureValid<T>` ``（校验失败抛 `InvalidOperationException`）/ `` `TryValidate<T>` ``（均委托给 `AttributeBasedValidator`） |
| `AutoQueryExtensions` | `AutoBuild(this object dto)` / `` `ToPageResultAuto<T>` `` / `` `ToPageResultAutoAsync<T>` ``（`validate: true` 时同样走 `` `PageQueryExecutor<T>` ``） |
| `PageConverter` | `ToMetadata` / `ToDto` / `` `ConvertItems<TSource,TTarget>` `` / `ConvertItemsAsync` / `Clone` / `Merge`（第二个请求覆盖第一个）/ `CreateEmptyResult` |
| `ValidationResult` | 校验结果：`IsValid` / `Errors`；静态 `Success()` / `Failure(params errors)`；`GetErrorMessage()` |

## 使用示例

### 手工构造分页请求

```csharp
// 按状态过滤 + 关键字搜索 + 排序 + 取第 1 页 10 条
var request = new PageRequestDtoBase()
    .WithFilter("Status", 1, QueryOperator.Equal)
    .WithKeyword("张三", "Name", "Remark")
    .WithSort("CreationTime", SortDirection.Descending)
    .WithPage(1, 10);
```

### 用 QueryBuilder 组装复杂条件

```csharp
var request = QueryBuilder.Create()
    .WhereEqual("TenantId", 1)
    .WhereContains("Name", "曦寒")
    .WhereBetween("Age", 18, 60)
    .OrderByDescending("CreatedTime")
    .SetPaging(pageIndex: 1, pageSize: 20)
    .Build();
```

### 在内存/IQueryable 上就地执行

```csharp
// source 可以是 IQueryable<T> / IEnumerable<T> / List<T>
PageResultDtoBase<User> result = source.ToPageResult(request);
Console.WriteLine($"{result.Page.PageIndex}/{result.Page.TotalPages}，共 {result.Page.TotalCount} 条");
```

### 由普通查询 DTO 自动构建条件

```csharp
public class UserQueryDto
{
    public string? Keyword { get; set; }          // 命中关键字约定 → 作关键字输入
    public string? Name { get; set; }             // 字符串 → 默认 Contains
    public int? Status { get; set; }              // 值类型 → Equal
    public List<long>? RoleIds { get; set; }      // *Ids/集合 → In("RoleId", ...)
    public DateTime[]? CreatedTimeRange { get; set; } // *Range 且长度2 → Between("CreatedTime", start, end)
    public PageRequestMetadata Page { get; set; } = new();
}

var request = queryDto.AutoBuild();               // 反射 + 约定 → PageRequestDtoBase
var page = query.ToPageResultAuto(queryDto);      // 一步到位（默认带校验）
```

### 基于实体特性做白名单校验 + 别名解析

```csharp
public class User
{
    [QueryField(Priority = 1)]
    public long Id { get; set; }

    [QueryField("userName")]                       // 前端传 userName，映射到 Name 属性
    [KeywordSearch]
    public string Name { get; set; } = string.Empty;

    [QueryOperatorSupport(QueryOperator.Equal, QueryOperator.In)] // 只允许 Equal / In
    public int Status { get; set; }
}

// 前端字段名用别名 userName，实体属性是 Name
var request = new PageRequestDtoBase()
    .WithFilter("userName", "张", QueryOperator.Contains)
    .WithFilter("Status", 5, QueryOperator.GreaterThan); // 未在 QueryOperatorSupport 白名单中

// 校验失败会抛 InvalidOperationException（Status 不支持 GreaterThan）
var page = query.ToPageResultWithValidation<User>(request);
```

## 扩展点 / 自定义

- **调整自动推断约定**：构造自定义 `QueryConvention`（改 `RangeSuffixes`/`ListSuffixes`/`KeywordPatterns`/`StringDefaultContains` 等）以适配你的 DTO 命名习惯；`AutoQueryBuilder` 默认使用 `QueryConvention.Default`。
- **字段白名单与别名**：在实体属性上加 `QueryFieldAttribute`（`AllowFilter`/`AllowSort`/`Alias`）与 `QueryOperatorSupportAttribute` 限制可用操作符，再用 `AttributePageExtensions` 的校验方法拦截越权/非法查询。
- **自定义关键字参与字段**：用 `KeywordSearchAttribute` 精细控制哪些属性进入关键字搜索、匹配模式与是否计入默认搜索集。
- **结果映射**：`` `PageResultDtoBase<T>.Map<TTarget>` `` 或 `PageConverter.ConvertItems/ConvertItemsAsync` 把实体分页结果映射为 DTO 分页结果，保留分页元数据。

## 注意事项与最佳实践

- **`Conditions` 与 `Behavior` 边界**：`Conditions` 永远可安全翻译为 `Expression`；`Behavior`（分页开关、忽略租户/软删除、拆分查询、超时）只作用于查询管道，别把它当过滤条件。二者混用会破坏这套协议的安全性假设。
- **`QueryFilter.Field` 不能为空**：赋空白字符串会立即抛 `ArgumentException`；`QuerySort.Field` 同理。
- **`IsValid()` 的语义**：`IsNull`/`IsNotNull` 不需要值；`In`/`NotIn` 需要 `Values` 非空；`Between` 需要 `Values` 恰好 2 个；其余操作符需要 `Value` 非空。无效的过滤/排序会在 `ApplyFilters`/`ApplySorts` 中被静默跳过。
- **`PageSize` 会被钳制**：入参 `PageSize` 超过 `MaxPageSize=500` 会被截断，小于 `MinPageSize=1` 会回退到默认 20；`PageIndex` 小于 1 会被钳为 1。
- **本包表达式实现的局限**：`PageExtensions.ApplyFilter` 只处理到 `Equal/NotEqual/比较/Contains/StartsWith/EndsWith/IsNull/IsNotNull`，`In/NotIn/Between` 在这里会走 `default` 分支被忽略——它面向内存/通用 `IQueryable`；真正的数据库翻译在 `XiHan.Framework.Data` 仓储层完成，务必以仓储实现为准。**`` `PageQueryExecutor<T>` ``（`AttributePageExtensions`/`AutoQueryExtensions` 的底层执行器）的过滤实现是同一套逻辑，同样不处理 `In/NotIn/Between`**——它带来的增量只是字段别名解析与基于特性的白名单校验，不是更强的表达式翻译能力。
- **关键字搜索是 OR、过滤是 AND**：多个 `Filters` 之间是 AND，关键字对多个字段之间是 OR；排序按 `Priority` 升序稳定应用。

## 依赖模块

- [XiHan.Framework.Core](./core)
- [XiHan.Framework.Utils](./utils)

不引用任何持久化 / Web 相关包，保持共享层的轻量与稳定。

## 相关模块

- [XiHan.Framework.Domain](./domain) — 领域层，直接依赖本包（仓储抽象大量复用 `PageRequestDtoBase` / `` `PageResultDtoBase<T>` ``）
- [XiHan.Framework.Application.Contracts](./application-contracts) — 应用契约层，复用本包的分页 DTO
- [XiHan.Framework.Application](./application) — 应用层实现
- [XiHan.Framework.Data](./data) — 基础设施层，提供分页查询协议在数据库层的真实翻译
