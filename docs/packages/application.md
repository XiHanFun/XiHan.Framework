# XiHan.Framework.Application

> 应用层实现：应用服务基类、CRUD/批量 CRUD 基类（Mapster 映射 + 软删除感知 + DataAnnotations 校验）与动态 API 特性 `[DynamicApi]`

- **NuGet**：`XiHan.Framework.Application`
- **模块类**：`XiHanApplicationModule`
- **所在层**：领域与应用层
- **关键依赖**：Mapster（对象映射，经 `ObjectMapping` 引入）；其余为框架内部依赖

## 概述

这个包是应用层的**实现落点**，也是框架「动态 API」的关键一环。你写一个继承 `ApplicationServiceBase` 的应用服务，框架就能把它的公共方法自动暴露成 REST 接口，无需手写 Controller（配合 `[DynamicApi]`）。它还提供了开箱即用的 CRUD 基类，把「实体 ↔ DTO」的映射、分页查询、软删除识别、入参校验都封装好了——你只关心业务字段，增删改查的样板由基类代劳。

包内三块内容：`Services/ApplicationServiceBase`（服务根基类）、`Services/CrudApplicationServiceBase` 与 `Services/BatchCrudApplicationServiceBase`（CRUD 与批量 CRUD 基类）、`Attributes/DynamicApiAttribute`（动态 API 配置特性）。

## 何时使用

- 编写应用服务并希望它自动变成 REST API（配合 `[DynamicApi]`）
- 快速实现一个标准 CRUD 服务，只关心业务、不想重复写增删改查样板
- 需要实体与 DTO 之间的自动映射（基于 Mapster `Adapt`）
- 需要按 DataAnnotations 校验入参、以及基于软删除接口自动走软删除路径

## 安装与启用

```bash
dotnet add package XiHan.Framework.Application
```

```csharp
[DependsOn(typeof(XiHanApplicationModule))]
public class MyModule : XiHanModule { }
```

`XiHanApplicationModule` 依赖 `XiHanLoggingModule`、`XiHanApplicationContractsModule`、`XiHanDomainModule`、`XiHanDistributedIdsModule`、`XiHanObjectMappingModule`。`ConfigureServices` 中不额外注册服务——`ApplicationServiceBase` 实现了 `ITransientDependency`，由框架的约定式扫描自动注册为瞬时服务，无需手写 DI。

## 工作原理

- **自动注册**：`ApplicationServiceBase : IApplicationService, ITransientDependency`，其派生类被框架约定式装配为瞬时依赖。
- **动态 API**：应用服务实现的 `IApplicationService` 标记 + `[DynamicApi]` 特性，被框架的动态 API 机制识别并生成路由，替代手写 Controller。详见 [动态 API](../guide/dynamic-api)。
- **映射**：CRUD 基类内部用 Mapster 的 `Adapt` 在实体与 DTO 间转换，所有映射方法均 `virtual`，可重写定制。
- **软删除感知**：删除时若实体实现 `ISoftDelete` 且容器中存在对应的 `ISoftDeleteRepositoryBase<TEntity, TKey>`，则走软删除；否则物理删除。
- **校验**：创建/更新/分页入参统一经 `ValidateInputObject` 用 DataAnnotations 校验（`validateAllProperties: true`）。

## 核心类型

| 类型 | 说明 |
| --- | --- |
| `ApplicationServiceBase` | 应用服务基类，实现 `IApplicationService, ITransientDependency`；属性注入 `ICachedServiceProvider ServiceProvider`，提供懒加载 `protected ILogger Logger` |
| `CrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>` | 通用 CRUD 基类，封装映射、分页、软删除、校验 |
| `BatchCrudApplicationServiceBase<...>` | 批量 CRUD 基类，追加批量增删改查 |
| `DynamicApiAttribute`（`[DynamicApi]`） | 动态 API 配置特性，控制路由/名称/版本/分组/大小写等 |

### `ApplicationServiceBase`

```csharp
public abstract class ApplicationServiceBase : IApplicationService, ITransientDependency
{
    public ICachedServiceProvider ServiceProvider { get; set; } = null!;   // 属性注入
    protected ILogger Logger => LazyLogger.Value;                          // 懒加载日志
}
```

`Logger` 懒加载自 `ServiceProvider` 解析的 `ILogger<ApplicationServiceBase>`，解析不到时回退 `NullLogger`——因此在无日志环境下也不会抛异常。

### `CrudApplicationServiceBase<...>`

泛型约束：

```csharp
public abstract class CrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>
    : ApplicationServiceBase, ICrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>
    where TEntity : EntityBase<TKey>
    where TEntityDto : DtoBase<TKey>
    where TKey : IEquatable<TKey>
    where TCreateDto : CreationDtoBase<TKey>
    where TUpdateDto : UpdateDtoBase<TKey>
    where TPageRequestDto : PageRequestDtoBase
```

构造签名：`protected CrudApplicationServiceBase(IRepositoryBase<TEntity, TKey> repository)`——注入仓储即可，保存在 `protected readonly IRepositoryBase<TEntity, TKey> Repository`。

内置方法（全部 `virtual`，可重写）：

| 方法 | 行为 |
| --- | --- |
| `GetByIdAsync(TKey id)` | `Repository.GetByIdAsync` 取实体，`null` 直接返回 `null`，否则映射为 DTO |
| `PageAsync(TPageRequestDto input)` | 校验入参 → 调 `BuildAdditionalFilterPredicate` 取额外谓词 → `Repository.GetPagedAutoAsync(input[, predicate])` → 映射为 `PageResultDtoBase<TEntityDto>`；`Filters`/`Sorts` 由仓储层自动处理 |
| `CreateAsync(TCreateDto input)` | 校验 → `MapDtoToEntityAsync(create)` → `Repository.AddAsync` → 回映射为 DTO |
| `UpdateAsync(TUpdateDto input)` | 校验 → 按 `input.BasicId` 取实体（找不到抛 `KeyNotFoundException`）→ `MapDtoToEntityAsync(update, entity)` 覆盖 → `Repository.UpdateAsync` → 回映射 |
| `DeleteAsync(TKey id)` | 取实体，`null` 返回 `false`；若实体 `is ISoftDelete` 且能解析 `ISoftDeleteRepositoryBase<,>` 则软删除，否则 `Repository.DeleteAsync` 物理删除 |

扩展点（`protected virtual`）：

| 成员 | 用途 |
| --- | --- |
| `BuildAdditionalFilterPredicate(PageRequestDtoBase input)` | 返回 `Expression<Func<TEntity, bool>>?`，默认 `null`；子类叠加权限/租户等业务过滤 |
| `MapEntityToDtoAsync` / `MapEntitiesToDtosAsync` | 实体→DTO（Mapster `Adapt`） |
| `MapDtoToEntityAsync`（多重载） | DTO→新实体、DTO→现有实体、CreateDto→实体、UpdateDto→现有实体 |
| `ValidateInputObject(object input)` | DataAnnotations 校验（`Validator.ValidateObject`，`validateAllProperties: true`） |
| `EnsureNotNullMapping<TResult>(...)`（`protected static`） | 映射结果为空时抛 `InvalidOperationException` |

### `BatchCrudApplicationServiceBase<...>`

继承 `CrudApplicationServiceBase<...>`（相同泛型参数与约束）并实现 `IBatchCrudApplicationService<...>`，构造签名同样是 `IRepositoryBase<TEntity, TKey> repository`。

- `BatchGetAsync(List<TKey> ids)`：空列表返回空；否则 `Repository.GetByIdsAsync` 批量取后映射。
- `BatchCreateAsync` / `BatchUpdateAsync` / `BatchDeleteAsync`：委托给内部 `ExecuteBatchOperationAsync` 逐项执行——按 `ContinueOnError` 决定遇错是否继续，成功/失败计数与错误列表汇总进 `BatchOperationResponse<T>`。批量删除同样识别 `SoftDelete && ISoftDelete` 走软删除路径。

## 动态 API 特性 `[DynamicApi]`

`DynamicApiAttribute`（命名空间 `XiHan.Framework.Application.Attributes`）标记类或方法以配置动态 API 行为。`[AttributeUsage(Class | Method, AllowMultiple = true)]`——可多次标注。

**合并优先级：全局配置 < 类级特性 < 方法级特性**；同一层级按 `Order` 升序合并（数值越大越后应用、优先级越高，后写入覆盖先写入）。

可配置属性：

| 属性 | 类型 / 默认 | 语义 | 合并方式 |
| --- | --- | --- | --- |
| `IsEnabled` | `bool` = `true` | 是否启用动态 API | 单值；任一层级设 `false` 即禁用 |
| `RouteTemplate` | `string` = `""` | 路由模板 | 单值，方法级优先、类级兜底 |
| `Name` | `string` = `""` | API 名称 | 单值，方法级优先、类级兜底 |
| `Version` | `string` = `""` | API 版本 | 可叠加（多次标注叠多版本） |
| `Description` | `string` = `""` | API 描述 | 单值，方法级优先、类级兜底 |
| `Tag` | `string` = `""` | API 标签 | 可叠加 |
| `Group` | `string` = `""` | 分组（文档键，用于 OpenApi 文档路径/标识） | 可叠加 |
| `GroupName` | `string` = `""` | 分组显示名（文档 UI 分组标题） | 单值 |
| `Order` | `int` = `0` | 同层级合并顺序（越大越后应用） | 单值 |
| `PreserveRoutePredicate` | `bool` = `false` | 是否保留路由谓词（Get/Create/Delete 前缀） | 单值，方法级优先→类级→全局回退 |
| `UsePascalCaseRoutes` | `bool` = `true` | 是否使用 PascalCase 路由 | 单值，同上回退链 |
| `UseLowercaseRoute` | `bool` = `false` | 是否使用小写路由 | 单值，同上回退链 |
| `VisibleInApiExplorer` | `bool` = `true` | 是否在 API 浏览器中显示 | 单值；任一层级 `false` 即隐藏 |
| `CustomProperties` | `string` = `""` | 自定义属性（建议 `key=value`，重复 key 后写覆盖前写） | 可叠加 |

**关键陷阱**：`PreserveRoutePredicate` / `UsePascalCaseRoutes` / `UseLowercaseRoute` 三个布尔项内部用**可空后备字段**区分「未设置」与「设为默认值」。合并逻辑读取 `PreserveRoutePredicateOrNull` / `UsePascalCaseRoutesOrNull` / `UseLowercaseRouteOrNull`（`null` = 未显式设置，回退下一层级）——只为设 `Group`/`Tag` 而标注特性时，不会以编译期默认值意外覆盖全局配置（该陷阱曾导致整类路由 404）。

## 使用示例

### 示例一：`ApplicationServiceBase` + `[DynamicApi]`

继承基类并标注 `[DynamicApi]`，公共方法即自动暴露为 REST API：

```csharp
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Services;

[DynamicApi]
public class WeatherAppService : ApplicationServiceBase
{
    public Task<string> GetForecastAsync(string city)
    {
        Logger.LogInformation("查询 {City} 天气", city);
        return Task.FromResult($"{city}: 晴");
    }
}
```

### 示例二：`CrudApplicationServiceBase`

标准 CRUD 服务只需注入仓储，即可获得全套增删改查：

```csharp
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Services;
using XiHan.Framework.Domain.Repositories;

[DynamicApi]
public class ProductAppService
    : CrudApplicationServiceBase<Product, ProductDto, long, ProductCreateDto, ProductUpdateDto, ProductPageRequestDto>
{
    public ProductAppService(IRepositoryBase<Product, long> repository) : base(repository) { }

    // 叠加租户/权限过滤：重写扩展点，基类分页会自动应用
    protected override Expression<Func<Product, bool>>? BuildAdditionalFilterPredicate(PageRequestDtoBase input)
        => p => !p.IsInternal;
}
```

> 动态 API 的工作原理与更多配置见 [动态 API](../guide/dynamic-api)。

## 注意事项与最佳实践

- CRUD 基类要求 `TEntity : EntityBase<TKey>`、DTO 继承对应基类（`DtoBase<TKey>` / `CreationDtoBase<TKey>` / `UpdateDtoBase<TKey>`）、分页 DTO 继承 `PageRequestDtoBase`——约束不满足编译期即报错。
- 软删除路径依赖容器中注册了 `ISoftDeleteRepositoryBase<TEntity, TKey>`；若实体实现 `ISoftDelete` 但未注册软删除仓储，则回退为物理删除。
- 定制映射（如忽略字段、平铺嵌套）请重写 `MapXxxAsync` 系列 `virtual` 方法，而非在业务方法里手动改字段。
- `[DynamicApi]` 的三个可空布尔项要用 `...OrNull` 语义理解合并——不显式设置就不会覆盖全局；不要因为「看着默认值一样」而随手标注它们。
- 分页查询里 `Filters`/`Sorts` 由仓储层自动处理，`BuildAdditionalFilterPredicate` 只用于叠加额外业务过滤（权限、租户），不要重复实现前端已下发的过滤条件。

## 依赖模块

- [XiHan.Framework.Application.Contracts](./application-contracts) — 应用契约（接口与 DTO 基类）
- [XiHan.Framework.Domain](./domain) — 领域层（实体 `EntityBase<TKey>`、仓储 `IRepositoryBase<,>` / `ISoftDeleteRepositoryBase<,>`）
- [XiHan.Framework.DistributedIds](./distributed-ids) — 分布式 ID 生成
- [XiHan.Framework.Logging](./logging) — 日志
- [XiHan.Framework.ObjectMapping](./objectmapping) — 对象映射（Mapster）

## 相关模块

- [XiHan.Framework.Application.Contracts](./application-contracts) — 本包实现的契约层
- [XiHan.Framework.Domain](./domain) — 被编排的领域对象
- [动态 API](../guide/dynamic-api) — 应用服务如何自动变成 REST 接口
