# XiHan.Framework.Protocols.Http

HTTP 协议适配器（Ring 5）。提供 `protocols.http@1`，把登记表里暴露给 HTTP 的操作映射为 ASP.NET Core 端点（RFC-0011、`spec/conventions/operation-ids.md`）。

```csharp
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<HttpProtocolFeature>().UseFeature<OrdersFeature>());
var app = builder.Build();
app.UseXiHan();              // 构造上下文、把端点元数据里的操作描述放入上下文、回写 Problem Details
app.MapXiHanOperations();    // orders.create → POST /orders，orders.get → GET /orders/{id} …
app.MapXiHanOpenApi();       // /openapi.json：OpenAPI 3.1，从登记表与 OperationSchema 生成
```

## 规则

- 路由推导：`create` → `POST /{resource}`，`get` → `GET /{resource}/{id}`，`list` → `GET /{resource}`，`update` → `PUT /{resource}/{id}`，`delete` → `DELETE /{resource}/{id}`，其他动词按请求是否声明 `id` 属性分实例级 `POST /{resource}/{id}:{verb}` 与集合级 `POST /{resource}:{verb}`（看请求的序列化元数据，不用反射；没有类型化元数据的插件代理看请求 Schema 的 `properties`）。显式覆盖：`XiHan:Protocols:Http:Routes:<操作 ID>` = `POST /orders:export`；前缀 `XiHan:Protocols:Http:RoutePrefix` = `/api`。
- 绑定：路由值（最优先）、JSON 请求体（POST/PUT/PATCH，必须是对象）、查询串（多值成数组）合并成一个 JSON 对象交给 `IOperationInvoker`；路由值与查询串是字符串，请求类型要从中绑定数字需启用 `NumberHandling = AllowReadingFromString`；属性名按请求类型序列化上下文的命名策略匹配，建议 `PropertyNamingPolicy = CamelCase` 加 `PropertyNameCaseInsensitive = true`（示例的 `SampleJsonContext`）。
- 响应：成功 `create` 为 201、其余 200，正文是响应类型的 JSON；失败由 `UseXiHan()` 写成 RFC 9457 Problem Details（`detail` 按 `Accept-Language` 本地化）。
- 授权：Authorize 阶段按契约的权限或策略判定（描述经端点元数据进入上下文）；`Stability.Internal` 的操作不发布；`Expose` 不含 `Http` 的操作不发布。
- OpenAPI：`MapXiHanOpenApi(pattern = "/openapi.json")` 在第一次请求时从登记表生成 OpenAPI 3.1 并缓存，绕过内核管道。路径与方法同路由推导；路由参数与（无请求体方法的）查询参数、请求体、成功响应都来自 `OperationSchema`，`$defs` 提升到 `components/schemas`；失败响应统一引用 `ProblemDetails` 组件。没有 Schema 的操作只发布路径与 `operationId`。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore` 与 `Microsoft.AspNetCore.App`。协议适配器在宿主之上（Ring 5）。

## 状态

P3：可用。OpenAPI 的 Schema 由 `Kernel.Generators` 产出。
