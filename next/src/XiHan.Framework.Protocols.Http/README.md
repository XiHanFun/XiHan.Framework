# XiHan.Framework.Protocols.Http

HTTP 协议适配器（Ring 5）。提供 `protocols.http@1`，把登记表里暴露给 HTTP 的操作映射为 ASP.NET Core 端点（RFC-0011、`spec/conventions/operation-ids.md`）。

```csharp
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<HttpProtocolFeature>().UseFeature<OrdersFeature>());
var app = builder.Build();
app.UseXiHan();              // 构造上下文、把端点元数据里的操作描述放入上下文、回写 Problem Details
app.MapXiHanOperations();    // orders.create → POST /orders，orders.get → GET /orders/{id} …
```

## 规则

- 路由推导：`create` → `POST /{resource}`，`get` → `GET /{resource}/{id}`，`list` → `GET /{resource}`，`update` → `PUT /{resource}/{id}`，`delete` → `DELETE /{resource}/{id}`，其他动词 → `POST /{resource}/{id}:{verb}`。显式覆盖：`XiHan:Protocols:Http:Routes:<操作 ID>` = `POST /orders:export`；前缀 `XiHan:Protocols:Http:RoutePrefix` = `/api`。
- 绑定：路由值（最优先）、JSON 请求体（POST/PUT/PATCH，必须是对象）、查询串（多值成数组）合并成一个 JSON 对象交给 `IOperationInvoker`；路由值与查询串是字符串，请求类型要从中绑定数字需启用 `NumberHandling = AllowReadingFromString`；属性名按请求类型序列化上下文的命名策略匹配，建议 `PropertyNamingPolicy = CamelCase` 加 `PropertyNameCaseInsensitive = true`（示例的 `SampleJsonContext`）。
- 响应：成功 `create` 为 201、其余 200，正文是响应类型的 JSON；失败由 `UseXiHan()` 写成 RFC 9457 Problem Details（`detail` 按 `Accept-Language` 本地化）。
- 授权：Authorize 阶段按契约的权限或策略判定（描述经端点元数据进入上下文）；`Stability.Internal` 的操作不发布；`Expose` 不含 `Http` 的操作不发布。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore` 与 `Microsoft.AspNetCore.App`。协议适配器在宿主之上（Ring 5）。

## 状态

P3：可用。OpenAPI 描述由 `Kernel.Generators`（P3-02）产出。
