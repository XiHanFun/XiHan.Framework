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

- 路由推导：`create` → `POST /{resource}`，`list` → `GET /{resource}`；请求声明了 `id` 属性时 `get` → `GET /{resource}/{id}`、`update` → `PUT /{resource}/{id}`、`delete` → `DELETE /{resource}/{id}`、其他动词 → `POST /{resource}/{id}:{verb}`（契约声明只读且请求没有嵌套对象时改用 `GET`，Google AIP-136，ADR-0041），没有 `id` 时去掉 `/{id}` 段落在集合（或单例资源）上（`GET /{resource}`、`PUT /{resource}`、`DELETE /{resource}`、`POST /{resource}:{verb}`）；`get` / `list` / `delete` 的请求带嵌套对象时（GET / DELETE 不读请求体）改用 POST 自定义动词（`POST /{resource}:list`）。是否含 `id` 看请求的序列化元数据，不用反射；没有类型化元数据的插件代理看请求 Schema 的 `properties`。两个操作推导出同一条路由时 `MapXiHanOperations` 以 `PROTOCOLS_CONFLICT_002` 启动失败。显式覆盖：`XiHan:Protocols:Http:Routes:<操作 ID>` = `POST /orders:export`；前缀 `XiHan:Protocols:Http:RoutePrefix` = `/api`。
- 绑定：路由值（最优先）、JSON 请求体（POST/PUT/PATCH，必须是对象）、查询串合并成一个 JSON 对象交给 `IOperationInvoker`；路由值与查询串按请求类型序列化元数据里的属性类型写成 JSON 布尔、整数、浮点、数值枚举或数组（集合属性的单值也写成数组），属性名取序列化名、不分大小写匹配；找不到属性或没有元数据（插件代理）时写字符串，多值写字符串数组；属性名按请求类型序列化上下文的命名策略匹配，建议 `PropertyNamingPolicy = CamelCase` 加 `PropertyNameCaseInsensitive = true`（示例的 `SampleJsonContext`）。
- 响应：成功 `create` 为 201、其余 200，正文是响应类型的 JSON；失败由 `UseXiHan()` 写成 RFC 9457 Problem Details（`detail` 按 `Accept-Language` 本地化；VALIDATION 错误带字段级 `errors`——字段键 → 本地化消息数组，所有错误带标量 `details`，见 `Hosting.AspNetCore`）。
- 授权：Authorize 阶段按契约的权限或策略判定（描述经端点元数据进入上下文）；`Stability.Internal` 的操作不发布；`Expose` 不含 `Http` 的操作不发布。
- OpenAPI：`MapXiHanOpenApi(options?, pattern = "/openapi.json")` 在第一次请求时从登记表、权限码注册表、已装配的认证处理器与宿主的全部端点生成 OpenAPI 3.1 并缓存，绕过内核管道。`OpenApiOptions` 指定 `info.title` / `info.version` / `servers`（缺省标题取应用名、版本取框架版本、不写 `servers`）。路径与方法同路由推导；路由参数与（无请求体方法的）查询参数、请求体、成功响应都来自 `OperationSchema`，`$defs` 提升到 `components/schemas`（字符串枚举定义也是一个组件，属性与查询参数以 `$ref` 引用）；失败响应统一引用 `ProblemDetails` 组件（标准成员加 `code`、`traceId`、`errors`：字段键 → 字符串数组、`details`：名字 → 标量）。没有 Schema 的操作只发布路径与 `operationId`。`OpenApiDocumentBuilder.Build(OpenApiDocumentSource)` 是不经宿主的生成入口（测试与工具用）；`OpenApiDocumentSource.FromServices(services, options, endpoints, locale)` 从容器组装与 `MapXiHanOpenApi` 相同的输入（登记表、权限码注册表、认证方案、普通端点、按区域的摘要），导出工具据此得到与 `/openapi.json` 逐字节相同的文档（ADR-0034）。
- OpenAPI 认证方案（ADR-0032）：容器里有 `Scheme == "Bearer"` 的 `IAuthenticationHandler`（`Security.Jwt`、`Security.OpenIdConnect`）时发布 `components.securitySchemes.bearer`（`http` / `bearer` / `JWT`）并挂全局 `security: [{ bearer: [] }]`，没有权限也没有策略要求的操作与端点标 `security: []`；权限码与策略名不进 scope，仍只走 `x-xihan-*`。ApiKey 等其他方案不发布。
- OpenAPI 操作文案（ADR-0052）：`summary` 取消息目录里 `<操作 ID>.title`，`description` 取 `<操作 ID>.description`（`-` 换 `_`，与 MCP `tools/list` 同两个键），各自没有就不写；授权要求由生成器按区域附在 `description` 末尾一行（`HttpMessages`：需要权限 <码>（<显示名>）/ 需要策略 <名> / 需要登录），权限的显示名先取 `permissions.<权限码>.name`（`-` 换 `_`），没有再取权限定义的；`x-xihan-permissions` 的 `displayName` / `description` 同样先取 `permissions.<权限码>.name` / `.description`。文案里不要再手写。装配了本地化时按 `Accept-Language` 协商出的区域分别生成与缓存（`Content-Language` 标出区域，缺省 `en-US`），没有装配时只有一份文档、没有目录文案。
- OpenAPI 标签与分组（ADR-0052）：顶层 `tags` 的 `x-displayName` / `description` 取 `openapi.tags.<资源段>.name` / `.description`，`name` 仍是资源码；`OpenApiOptions.Groups` 声明 `OpenApiGroup(名字, 排序, 所属特性, 额外标签)`，全量文档带 `x-tagGroups`（一个标签只归排序靠前的一组），每组另发 `/openapi/<组名>.json`（标题取 `openapi.groups.<组名>.name`），`GetXiHanOpenApiDocuments()` 列出全部文档给 Scalar：`foreach (var link in app.GetXiHanOpenApiDocuments(options)) scalar.AddDocument(link.Name, link.Title, link.Url);`。
- OpenAPI 字段说明（ADR-0052）：Schema 里 `Kernel.Generators` 从 XML 注释折进的 `description` 进参数、请求体与成功响应（没有根说明时是按区域的「成功」）；某个区域可用 `openapi.schemas.<snake_case 的 Schema 名>.description` 与 `.properties.<snake_case 的属性名>` 覆盖。
- OpenAPI 普通端点（ADR-0032）：`EndpointDataSource` 里没有 `OperationDescriptor`、有 HTTP 方法元数据、带 `IAcceptsMetadata` 或 `IProducesResponseTypeMetadata` 之一、没有 `ExcludeFromDescription()` 的路由端点进文档——Hub、探针、重定向、静态资源没有这两种元数据，自然不进。路由参数作 `path` 参数（`string`）；`WithName` 作 `operationId`，`WithSummary` / `WithDescription` / `WithTags` 照搬；`RequireXiHanPermission` / `RequireXiHanPolicy` / `RequireXiHanAuthentication` 出 `x-xihan-permission` / `x-xihan-policy`，权限码并入 `x-xihan-permissions`。`Accepts` / `Produces` 按内容类型写：JSON 只引用已登记操作的根 Schema（显式 `WithXiHanOperationSchema(request:, response:)`，或按 `Accepts<T>` / `Produces<T>` 的类型与操作的请求 / 响应类型自动匹配，取 ID 最小且有 Schema 的操作；被引用的根提升到 `components/schemas/<title>`，操作自己也改为 `$ref`），`multipart/form-data` 是含 `format: binary` 属性（`file` / `files`）的对象，表单是对象，`text/*` 与 `string` 是字符串，其余二进制是 `format: binary` 的字符串；失败响应同样引用 `ProblemDetails`。JSON 类型引用不到以 `PROTOCOLS_VALIDATION_010` 生成失败，不做第二套推导；与操作共用一条路由以 `PROTOCOLS_CONFLICT_002` 生成失败。
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
