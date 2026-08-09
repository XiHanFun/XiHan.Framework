# XiHan.Framework.Application.Contracts

> 应用层对外契约：DTO 基类体系、应用服务接口与统一响应模型（`ApiResponse` + `ApiResponseCodes`）

- **NuGet**：`XiHan.Framework.Application.Contracts`
- **模块类**：`XiHanApplicationContractsModule`
- **所在层**：领域与应用层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（`XiHan.Framework.Domain.Shared`、`XiHan.Framework.Core`、`XiHan.Framework.Utils`）

## 概述

这个包定义应用层的**对外契约**：DTO 基类、应用服务接口和统一返回模型。它只声明「有什么」，不含具体实现——实现放在 [XiHan.Framework.Application](./application) 包。把契约单独抽出来，客户端、网关或其它模块可以只引用这一层拿到接口签名与数据形状，而不必依赖服务的实现细节，从而保持契约层的纯净与稳定。

包内三块内容：一是 `Dtos` 目录下的 DTO 基类体系（读取 / 创建 / 更新 / 删除 / 审计五种场景的抽象基类）与批量请求/响应模型；二是 `Services` 目录下的应用服务标记接口 `IApplicationService` 与 CRUD/批量 CRUD 契约；三是 `ApiResponse` 统一响应信封与 `ApiResponseCodes` 统一返回码枚举。

## 何时使用

- 定义应用服务接口与传输数据的 DTO，声明前后端约定
- 需要标准化的 CRUD 应用服务契约（`ICrudApplicationService<...>`）或批量版本（`IBatchCrudApplicationService<...>`）
- 需要统一的 API 响应信封（`ApiResponse` / `ApiResponse<T>`）与业务返回码（`ApiResponseCodes`）
- 定义 DTO 时希望复用带主键、带审计字段的抽象基类

## 安装与启用

```bash
dotnet add package XiHan.Framework.Application.Contracts
```

```csharp
[DependsOn(typeof(XiHanApplicationContractsModule))]
public class MyModule : XiHanModule { }
```

`XiHanApplicationContractsModule` 依赖 `XiHanDomainSharedModule`，`ConfigureServices` 中不注册任何服务——本包是纯契约层，不含运行时逻辑，启用它主要是把 `Domain.Shared`（分页 DTO）纳入模块依赖链。

## DTO 基类体系

DTO 基类按「读取 / 创建 / 更新 / 删除 / 审计」五种场景分类，每类都有一个非泛型基类和一个带 `TKey` 主键约束（`where TKey : IEquatable<TKey>`）的泛型基类。带主键的泛型基类统一用 `BasicId` 作为主键属性名。

| 基类 | 主键字段 | 说明 |
| --- | --- | --- |
| `DtoBase` | — | 所有 DTO 的根基类（空基类，仅作标记） |
| `DtoBase<TKey>` | `virtual TKey BasicId` | 读取/输出 DTO 基类，携带主键 |
| `CreationDtoBase` / `CreationDtoBase<TKey>` | 无字段 | 创建 DTO 基类（创建时通常无主键，泛型版仅约束 `TKey`） |
| `UpdateDtoBase` / `UpdateDtoBase<TKey>` | `virtual TKey BasicId` | 更新 DTO 基类，携带待更新实体主键 |
| `DeletionDtoBase` / `DeletionDtoBase<TKey>` | `virtual TKey BasicId` | 删除 DTO 基类，携带待删除主键 |
| `FullAuditedDtoBase` / `FullAuditedDtoBase<TKey>` | `virtual TKey BasicId` | 带完整审计字段的 DTO 基类 |

`FullAuditedDtoBase`（非泛型）的审计字段：

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `CreatedTime` | `DateTimeOffset[]?` | 创建时间 |
| `ModifiedTime` | `DateTimeOffset[]?` | 修改时间 |
| `IsDeleted` | `bool` | 软删除标记 |
| `DeletedTime` | `DateTimeOffset[]?` | 删除时间 |

> 说明：三个时间字段声明为 `DateTimeOffset[]?`（数组），用于承载「时间区间」查询/展示语义，而非单个时刻。

`FullAuditedDtoBase<TKey>` 在此基础上追加 `BasicId` 及操作者信息：`CreatedId` / `CreatedBy`、`ModifiedId` / `ModifiedBy`、`DeletedId` / `DeletedBy`（`Id` 为 `TKey?`，`By` 为 `string?`）。

所有字段均为 `virtual`，子类可重写。

## 应用服务接口

| 类型 | 说明 |
| --- | --- |
| `IApplicationService : IRemoteService` | 应用服务标记接口，实现它的服务会被动态 API 暴露为 REST 接口 |
| `ICrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>` | 标准 CRUD 契约（创建/更新 DTO 分离） |
| `IBatchCrudApplicationService<...>` | 在 CRUD 之上追加批量操作契约 |

`IApplicationService` 本身没有成员，仅继承自 [XiHan.Framework.Core](./core) 的 `IRemoteService`（同样是空接口）——它是一个纯标记，用于让框架的动态 API 机制识别「哪些类要暴露成接口」。

`ICrudApplicationService<...>` 的泛型参数与约束：

```csharp
public interface ICrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>
    : IApplicationService
    where TEntityDto : DtoBase<TKey>
    where TKey : IEquatable<TKey>
    where TCreateDto : CreationDtoBase<TKey>
    where TUpdateDto : UpdateDtoBase<TKey>
    where TPageRequestDto : PageRequestDtoBase
```

方法：

| 方法 | 签名 | 说明 |
| --- | --- | --- |
| 获取单个 | `Task<TEntityDto?> GetByIdAsync(TKey id)` | 按主键查询，未找到返回 `null` |
| 分页 | `Task<PageResultDtoBase<TEntityDto>> PageAsync(TPageRequestDto input)` | 分页查询 |
| 创建 | `Task<TEntityDto> CreateAsync(TCreateDto input)` | 创建并返回结果 DTO |
| 更新 | `Task<TEntityDto> UpdateAsync(TUpdateDto input)` | 更新并返回结果 DTO |
| 删除 | `Task<bool> DeleteAsync(TKey id)` | 删除，返回是否成功 |

`IBatchCrudApplicationService<...>` 继承 `ICrudApplicationService<...>`（相同泛型参数与约束），追加：

| 方法 | 签名 |
| --- | --- |
| 批量获取 | `Task<List<TEntityDto>> BatchGetAsync(List<TKey> ids)` |
| 批量创建 | `Task<BatchOperationResponse<TEntityDto>> BatchCreateAsync(BatchOperationRequest<TCreateDto> request)` |
| 批量更新 | `Task<BatchOperationResponse<TEntityDto>> BatchUpdateAsync(BatchUpdateRequest<TUpdateDto> request)` |
| 批量删除 | `Task<BatchOperationResponse<bool>> BatchDeleteAsync(BatchDeleteRequest<TKey> request)` |

> 分页请求/响应基类（`PageRequestDtoBase` / `PageResultDtoBase<T>`）来自依赖包 [XiHan.Framework.Domain.Shared](./domain-shared)，CRUD 契约的分页参数即基于它们。

## 批量操作模型

批量请求/响应模型（位于 `Dtos`）：

| 类型 | 关键字段 | 说明 |
| --- | --- | --- |
| `BatchOperationRequest<T>` | `List<T> Items`、`bool ContinueOnError`（默认 `false`）、`bool UseTransaction`（默认 `true`） | 通用批量请求（用于创建） |
| `BatchDeleteRequest<TKey>` | `List<TKey> Ids`、`ContinueOnError`、`UseTransaction`、`bool SoftDelete`（默认 `true`） | 批量删除请求 |
| `BatchUpdateRequest<TUpdateDto>` | `List<BatchUpdateItem<TUpdateDto>> Items`、`ContinueOnError`、`UseTransaction` | 批量更新请求 |
| `BatchUpdateItem<TUpdate>` | `TUpdate Data` | 单个更新项 |
| `BatchOperationResponse<T>` | `SuccessCount` / `FailureCount` / `TotalCount`、`bool IsAllSuccess`（=`FailureCount == 0`）、`List<BatchOperationResult<T>> Results`、`List<string> Errors` | 批量操作响应 |
| `BatchOperationResult<T>` | `Index`、`bool IsSuccess`、`T? Data`、`ErrorMessage` / `ErrorCode` | 单条结果 |

## 统一响应模型

`ApiResponse` 是所有接口的统一响应信封。

字段：

| 字段 | 类型 | 默认值 / 语义 |
| --- | --- | --- |
| `Code` | `ApiResponseCodes` | 业务码，默认 `Success`；**序列化到 JSON 为 int** |
| `Message` | `string` | 提示信息，默认取业务码的 `DescriptionAttribute` 描述 |
| `Data` | `object?` | 成功时为业务数据，失败时可承载错误明细 |
| `TraceId` | `string?` | 请求追踪 ID，用于跨日志/链路定位 |
| `Timestamp` | `DateTimeOffset` | 服务端时间，默认 `DateTimeOffset.UtcNow` |
| `IsSuccess` | `bool`（只读） | `Code` 落在 2xx（`>= 200 and < 300`）视为成功 |

推荐用静态工厂方法构造，保证 Code 与 Message 语义一致：

- 成功类：`Success(object? data, string? traceId)`、`Created(...)`、`Continue()`
- 客户端错误：`BadRequest(...)`、`Unauthorized(...)`、`Forbidden()`、`NotFound()`、`UnprocessableEntity(...)`、`TooManyRequests()`
- 服务端错误：`InternalServerError(...)`、`ServiceUnavailable()`
- 通用失败：`Failure(ApiResponseCodes code, string? errorMessage, string? traceId)`——按指定业务码构造（适用于 10000+ 业务码及未内置工厂的协议码）

泛型版 `ApiResponse<T> : ApiResponse` 用 `new T? Data` 遮蔽父类 `Data`，提供强类型数据以便客户端代码生成与 OpenAPI 精确表达；提供 `Success(T? data, string? traceId)` 与 `InternalServerError(T? data, string? traceId)` 工厂。

## 统一返回码 `ApiResponseCodes`

枚举 `ApiResponseCodes` 的每个成员带 `DescriptionAttribute` 中文描述，`Message` 默认即取自该描述。

`ApiResponse.Code` **属性**上标了 `[JsonConverter(typeof(NumericEnumConverter<ApiResponseCodes>))]`（枚举类型上也标了同一个转换器），**强制序列化为 int**——即使 Web 管道全局启用了 `JsonStringEnumConverter`，`code` 仍输出数字，方便前端统一判断。

转换器标在属性上是必需的：System.Text.Json 的优先级为「属性特性 > `Converters` 集合 > 类型特性」，只标类型会被管道加进集合的 `JsonStringEnumConverter` 压过。

分两个区段：

**协议状态（100～599）**：与 HTTP Status Code 一致，成员采用 HTTP 官方名称；该区段同样适用于微服务、消息队列、RPC 等非 HTTP 场景。

| 区段 | 成员 | 值 | 描述 |
| --- | --- | --- | --- |
| 1xx 信息 | `Continue` | 100 | 继续请求 |
| | `SwitchingProtocols` | 101 | 切换协议 |
| 2xx 成功 | `Success` | 200 | 请求成功 |
| | `Created` | 201 | 资源创建成功（通常用于 POST 创建操作） |
| | `Accepted` | 202 | 请求已接受但尚未处理完成（异步任务提交，如导出、批量作业，结果需另行查询） |
| | `NoContent` | 204 | 无内容返回（删除操作或无需响应体的更新） |
| 3xx 重定向 | `MultipleChoices` | 300 | 多种响应可选 |
| | `MovedPermanently` | 301 | 永久重定向 |
| | `Found` | 302 | 临时重定向 |
| | `NotModified` | 304 | 资源未修改（配合 ETag / If-Modified-Since 条件请求） |
| 4xx 客户端错误 | `BadRequest` | 400 | 请求错误（参数/格式错误或缺少必要参数） |
| | `Unauthorized` | 401 | 未授权（未通过身份认证） |
| | `Forbidden` | 403 | 禁止访问（已认证但无权限） |
| | `NotFound` | 404 | 资源不存在 |
| | `MethodNotAllowed` | 405 | 请求方法不允许 |
| | `RequestTimeout` | 408 | 请求超时 |
| | `Conflict` | 409 | 请求冲突（重复创建、乐观锁版本冲突、防重放校验失败等） |
| | `Gone` | 410 | 资源已永久删除（区别于 404，明确曾经存在） |
| | `UnsupportedMediaType` | 415 | 媒体类型不支持 |
| | `UnprocessableEntity` | 422 | 请求语义错误（参数格式正确但业务语义校验未通过） |
| | `TooManyRequests` | 429 | 请求过于频繁（限流/防刷） |
| 5xx 服务端错误 | `InternalServerError` | 500 | 服务器内部错误 |
| | `NotImplemented` | 501 | 功能未实现 |
| | `BadGateway` | 502 | 网关错误（从上游服务收到无效响应） |
| | `ServiceUnavailable` | 503 | 服务不可用（维护、过载或依赖服务不可用） |
| | `GatewayTimeout` | 504 | 网关超时（等待上游服务响应超时） |

**业务状态（10000～99999）**：表达更细粒度的业务语义，按千位分类留段：

  - `10xxx` 认证与授权：`LoginExpired = 10001`、`TokenInvalid = 10002`、`TokenExpired = 10003`、`PermissionDenied = 10004`
  - `11xxx` 数据校验：`ValidationFailed = 11000`
  - `12xxx` 业务处理：`BusinessFailed = 12000`
  - `13xxx` 数据访问：`DatabaseError = 13000`
  - `14xxx` 外部依赖：`ThirdPartyServiceError = 14000`

> 业务码与协议码的取舍：`401` 表达「未认证」，而 `LoginExpired = 10001` 明确「曾登录、现已过期」，便于前端引导重新登录；`403` 表达「无权限」，而 `PermissionDenied = 10004` 面向按钮/字段级细粒度权限点。

## 使用示例

定义一套 CRUD DTO 并声明契约：

```csharp
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Domain.Shared.Paging.Dtos;

public class ProductDto : DtoBase<long>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductCreateDto : CreationDtoBase<long>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductUpdateDto : UpdateDtoBase<long>   // 自带 BasicId
{
    public string Name { get; set; } = string.Empty;
}

public class ProductPageRequestDto : PageRequestDtoBase { }

public interface IProductAppService
    : ICrudApplicationService<ProductDto, long, ProductCreateDto, ProductUpdateDto, ProductPageRequestDto>
{
}
```

统一响应（在自定义端点或过滤器中）：

```csharp
// 成功（泛型强类型）
var ok = ApiResponse<ProductDto>.Success(dto, traceId);

// 业务失败（业务码 + 明细）
var fail = ApiResponse.Failure(ApiResponseCodes.PermissionDenied, "缺少 product:delete 权限");
```

## 注意事项与最佳实践

- 创建/更新用**分离的 DTO**（`TCreateDto` / `TUpdateDto`），不要复用同一个 DTO——创建时不含主键，更新时 `BasicId` 必填，语义不同。
- `ApiResponse.Code` 永远是 int（属性级 `NumericEnumConverter` 保证），前端按数字判断即可；判定成功优先用 `IsSuccess`，它已按 `[200, 300)` 算好。
- 判断成功用 `IsSuccess`（2xx 区段），而非 `Code == 200` 单值——`Created`(201) 等也应视为成功。
- 业务失败优先用 10000+ 业务码而非直接套用 HTTP 4xx，以携带更明确的业务语义。

## 依赖模块

仅依赖 [XiHan.Framework.Domain.Shared](./domain-shared)（复用分页查询 DTO `PageRequestDtoBase` / `PageResultDtoBase<T>`）。`IRemoteService` 与 `NumericEnumConverter` 分别来自 [XiHan.Framework.Core](./core) 与 [XiHan.Framework.Utils](./utils)（经传递引用）。不含任何实现代码，保持契约层纯净。

## 相关模块

- [XiHan.Framework.Application](./application) — 本契约的实现层（应用服务基类 + CRUD 基类 + 动态 API 特性）
- [XiHan.Framework.Domain.Shared](./domain-shared) — 提供分页查询 DTO
- [XiHan.Framework.Domain](./domain) — 领域层，DTO 通常由领域实体映射而来
- [动态 API](../guide/dynamic-api) — `IApplicationService` 如何被自动暴露为 REST 接口
