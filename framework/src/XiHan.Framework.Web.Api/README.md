# XiHan.Framework.Web.Api

## 概述
XiHan.Framework.Web.Api 提供 Web REST API 基础能力与动态 API 支持，统一控制器生成、路由约定与 API 行为。

## 核心能力
- 动态 API 生成与约定路由
- API 行为与异常处理的统一配置
- 与序列化、多租户等模块协同

## 依赖关系
- 通过 `XiHanWebApiModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 动态 API 配置通过 `DynamicApiOptions` 进行统一管理
- API 行为与 JSON 序列化策略由模块内统一配置

## 使用方式
```csharp
[DependsOn(typeof(XiHanWebApiModule))]
public class MyModule : XiHanModule
{
}
```

## 接口幂等

对 MVC 控制器动作与动态 API 应用服务方法提供可选的幂等保护：相同幂等键、相同内容的重复请求直接重播首次响应，不再执行动作。

### 用法

在动作、应用服务方法或其所在类上标 `[Idempotent]`（`XiHan.Framework.Application.Attributes` 命名空间，位于 `XiHan.Framework.Application` 包）；需要事务语义时同时标 `[UnitOfWork(true)]`。调用方通过请求头 `Idempotency-Key` 传入幂等键（可见 ASCII 字符，长度不超过 128）。

```csharp
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Uow.Attributes;

public sealed class OrderAppService : IApplicationService
{
    // POST /api/Order
    [Idempotent]
    [UnitOfWork(true)]
    public Task<OrderDto> CreateAsync(CreateOrderDto input) => /* ... */;
}
```

```http
POST /api/Order HTTP/1.1
Authorization: Bearer <token>
Idempotency-Key: 6f1c2a7e-3b4d-4c1a-9e55-0a1b2c3d4e5f
Content-Type: application/json

{ "productId": 1, "quantity": 2 }
```

- 幂等键按 租户、用户、HTTP 方法、请求路径、幂等键 隔离，不同用户或不同路径使用相同的键互不影响；请求路径按小写参与记录键与请求摘要，仅大小写不同的路径视为同一路径。
- 请求摘要由 HTTP 方法、路径、查询串与模型绑定后的参数计算，不是原始请求体；相同键但摘要不同视为内容冲突。
- 重播响应带 `Idempotency-Replayed: true`，并照常经过统一 `ApiResponse` 包装。

### 状态码

| 状态码 | 场景 |
| --- | --- |
| 400 | 幂等键缺失或无效（含超长、含非可见 ASCII 字符）；表单请求内容无法读取。 |
| 401 | 调用方未认证。 |
| 409 | 相同键的请求正在处理；相同键但请求内容不同；该键对应的结果不确定。 |
| 413 | 参与摘要的参数序列化后超过 `MaxRequestBytes`。 |
| 415 | 动作含文件或流参数。 |
| 503 | 进程内存储已满（记录数达到 `MaxEntries` 或快照总字节达到 `MaxTotalResponseBytes`）。 |

### 配置：`XiHan:Web:Api:Idempotency`（`XiHanIdempotencyOptions`）

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `HeaderName` | `string` | `"Idempotency-Key"` | 携带幂等键的请求头名称。 |
| `MaxKeyLength` | `int` | `128` | 幂等键最大长度（字符）。 |
| `MaxRequestBytes` | `int` | `1048576`（1 MiB） | 参与摘要的请求参数序列化后的最大字节数。 |
| `MaxResponseBytes` | `int` | `1048576`（1 MiB） | 单个响应快照的最大字节数。 |
| `MaxEntries` | `int` | `10000` | 进程内存储的最大记录数。 |
| `MaxTotalResponseBytes` | `long` | `67108864`（64 MiB） | 进程内存储的响应快照总字节上限。 |
| `CompletedRetention` | `TimeSpan` | `24:00:00` | 完成记录与结果不确定记录的保留时长。 |
| `ProcessingLease` | `TimeSpan` | `00:05:00` | 事务型端点处理中记录的租约时长，仅落库存储使用。 |

除 `HeaderName` 外的各字段必须大于零，否则应用启动失败。

```json
{
  "XiHan": {
    "Web": {
      "Api": {
        "Idempotency": {
          "HeaderName": "Idempotency-Key",
          "MaxKeyLength": 128,
          "MaxRequestBytes": 1048576,
          "MaxResponseBytes": 1048576,
          "MaxEntries": 10000,
          "MaxTotalResponseBytes": 67108864,
          "CompletedRetention": "1.00:00:00",
          "ProcessingLease": "00:05:00"
        }
      }
    }
  }
}
```

### 失败与重试语义

- 事务型的判定与工作单元过滤器一致：动作或应用服务方法、或其所在类标注 `[UnitOfWork]`（或类实现 `IUnitOfWorkEnabled`）时才开启工作单元；`[UnitOfWork]` 未显式指定是否事务时，方法名不以 `Get` 开头即为事务型（受 `XiHan:Uow:Default:TransactionBehavior` 影响）；未标注 `[UnitOfWork]` 的控制器动作与应用服务方法为非事务型。
- 事务型端点：动作抛出异常时释放幂等键，允许用同一个键重试。
- 事务型端点写入完成后、工作单元提交成功前，同一个键的请求得到 409（处理中）；提交成功后才重播。
- 非事务型端点抛出异常，或动作成功但没有保存快照（返回值类型不支持，或快照超过 `MaxResponseBytes`）：该键标记为结果不确定，之后携带同一个键的请求返回 409，不会自动重新执行。
- 写入完成时进程内存储的快照总字节会超过 `MaxTotalResponseBytes`（清理过期记录后仍超过）：请求返回 500；事务型端点的业务一并回滚并释放幂等键，非事务型端点标记为结果不确定。
- 明确返回的 4xx/5xx 结果同样会被保存并重播。

### 限制

- 重播只保留状态码与动作返回值的 JSON（支持 `ObjectResult`、`EmptyResult`、`StatusCodeResult`）；`Set-Cookie`、`Location` 等响应头及非 JSON 格式化器的输出不会重现。
- 默认的进程内存储只在当前进程有效，重启即失，也不跨实例共享；完成记录与结果不确定记录在保留期后过期，处理中记录在进程内不会过期。
- 多实例部署需要跨实例一致性时，替换 `IIdempotencyStore`；使用 SqlSugar 存储包 [`XiHan.Framework.Web.Api.SqlSugar`](../XiHan.Framework.Web.Api.SqlSugar/README.md)。
- 不支持 Minimal API、文件上传与流式响应。

## 扩展点
- 自定义动态 API 约定与路由策略
- 自定义 API 过滤器与异常处理

## 目录结构
```text
XiHan.Framework.Web.Api/
  README.md
  XiHanWebApiModule.cs
```
