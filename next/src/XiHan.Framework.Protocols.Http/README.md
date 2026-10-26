# XiHan.Framework.Protocols.Http

HTTP 协议适配器（Ring 5）。提供 `protocols.http@1`，把登记表里暴露给 HTTP 的操作映射为 ASP.NET Core 端点（RFC-0011、`spec/conventions/operation-ids.md`）。

```csharp
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<HttpProtocolFeature>().UseFeature<OrdersFeature>());
var app = builder.Build();
app.UseXiHan();              // 构造上下文、把端点元数据里的操作描述放入上下文、回写 Problem Details
app.MapXiHanOperations();    // orders.create → POST /orders，orders.get → GET /orders/{id} …
app.MapXiHanOpenApi();       // /openapi.json：OpenAPI 3.1，从登记表与 OperationSchema 生成，带权限 / 策略扩展
```

## 规则

- 路由推导：`create` → `POST /{resource}`，`list` → `GET /{resource}`；请求声明了 `id` 属性时 `get` → `GET /{resource}/{id}`、`update` → `PUT /{resource}/{id}`、`delete` → `DELETE /{resource}/{id}`、其他动词 → `POST /{resource}/{id}:{verb}`，没有 `id` 时去掉 `/{id}` 段落在集合（或单例资源）上（`GET /{resource}`、`PUT /{resource}`、`DELETE /{resource}`、`POST /{resource}:{verb}`）；`get` / `list` / `delete` 的请求带嵌套对象时（GET / DELETE 不读请求体）改用 POST 自定义动词（`POST /{resource}:list`）。是否含 `id` 看请求的序列化元数据，不用反射；没有类型化元数据的插件代理看请求 Schema 的 `properties`。两个操作推导出同一条路由时 `MapXiHanOperations` 以 `PROTOCOLS_CONFLICT_002` 启动失败。显式覆盖：`XiHan:Protocols:Http:Routes:<操作 ID>` = `POST /orders:export`；前缀 `XiHan:Protocols:Http:RoutePrefix` = `/api`。
- 绑定：路由值（最优先）、JSON 请求体（POST/PUT/PATCH，必须是对象）、查询串合并成一个 JSON 对象交给 `IOperationInvoker`；路由值与查询串按请求类型序列化元数据里的属性类型写成 JSON 布尔、整数、浮点、数值枚举或数组（集合属性的单值也写成数组），属性名取序列化名、不分大小写匹配；找不到属性或没有元数据（插件代理）时写字符串，多值写字符串数组；属性名按请求类型序列化上下文的命名策略匹配，建议 `PropertyNamingPolicy = CamelCase` 加 `PropertyNameCaseInsensitive = true`（示例的 `SampleJsonContext`）。
- 响应：成功 `create` 为 201、其余 200，正文是响应类型的 JSON；失败由 `UseXiHan()` 写成 RFC 9457 Problem Details（`detail` 按 `Accept-Language` 本地化）。
- 授权：Authorize 阶段按契约的权限或策略判定（描述经端点元数据进入上下文）；`Stability.Internal` 的操作不发布；`Expose` 不含 `Http` 的操作不发布。
- OpenAPI：`MapXiHanOpenApi(pattern = "/openapi.json")` 在第一次请求时从登记表生成 OpenAPI 3.1 并缓存，绕过内核管道。路径与方法同路由推导；路由参数与（无请求体方法的）查询参数、请求体、成功响应都来自 `OperationSchema`，`$defs` 提升到 `components/schemas`；失败响应统一引用 `ProblemDetails` 组件。没有 Schema 的操作只发布路径与 `operationId`。
- OpenAPI 权限扩展（ADR-0031）：契约有 `Permission` 的操作带 `x-xihan-permission`（权限码），有 `Policy` 的带 `x-xihan-policy`（策略名），都没有则不带；不占用标准 `security` / `securitySchemes`（权限码不是 OAuth scope，那些字段留给认证方案）。`components.x-xihan-permissions` 按权限码（序数排序）给出权限定义，收录范围 = 文档里已发布操作引用的权限码 ∪ `PermissionRegistry` 登记的全部定义——只在 gRPC / MCP 暴露的操作或端点级 `RequireXiHanPermission` 用到的权限也在其中，授权界面据此生成；被引用但未登记的只有 `code`，`description` 为 null 时省略。`OpenApiDocumentBuilder.Build(registry, options, permissions, title, version)` 的 `permissions` 为 null 时只按引用写。

  ```json
  {
    "paths": { "/orders": { "post": { "operationId": "orders.create", "x-xihan-permission": "orders.write" } } },
    "components": {
      "schemas": { "...": {} },
      "x-xihan-permissions": {
        "orders.read": { "code": "orders.read", "displayName": "Read orders" },
        "orders.write": { "code": "orders.write", "displayName": "Place orders", "description": "Create orders for the current tenant." }
      }
    }
  }
  ```

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore` 与 `Microsoft.AspNetCore.App`。协议适配器在宿主之上（Ring 5）。

## 状态

P3：可用。OpenAPI 的 Schema 由 `Kernel.Generators` 产出。
