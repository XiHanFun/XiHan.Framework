# XiHan.Framework.Protocols.Http

HTTP 协议适配器，提供 `protocols.http@1`：按操作 ID 推导路由（可显式覆盖），把路由值、JSON 请求体与查询串绑定成操作载荷，在 ASP.NET Core 宿主的内核管道内执行操作，并从同一份契约生成 OpenAPI 3.1 文档。适用于要经 REST 暴露操作契约的应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Protocols.Http` |
| 环层 | Ring 5（协议与插件） |
| 能力 | 提供 `protocols.http@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `HttpProtocolFeature` |
| 配置节 | `XiHan:Protocols:Http` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore`、`XiHan.Framework.Localization.Abstractions`；共享框架 `Microsoft.AspNetCore.App` |
| AOT | 兼容（`IsAotCompatible`） |
| 稳定性 | 清单 Stable；路由与绑定（`Routing.*`、`HttpProtocolOptions`、`HttpErrors`）Stable，OpenAPI（`OpenApi.*`）与 `HttpMessages` Preview |

## 概述

`HttpProtocolFeature` 只登记选项与本包的消息目录；端点由应用在 `UseXiHan()` 之后调用 `MapXiHanOperations()` 映射。每个 `Expose` 含 `Http`、稳定性不是 `Stability.Internal` 的操作对应一个端点，路由按 `spec/conventions/operation-ids.md` 推导：`create` → `POST /{resource}`，`list` → `GET /{resource}`，请求含 `id` 时 `get` / `update` / `delete` 带 `/{id}` 段，其他动词是 `POST …:{verb}` 自定义动词，带嵌套对象的 `get` / `list` / `delete` 改用 POST（ADR-0015），声明只读的自定义动词走 GET（ADR-0041）。是否含 `id`、是否有嵌套对象读源生成的序列化元数据，不用反射。

端点元数据带着操作描述、载荷绑定委托与响应写出委托（`OperationResponseWriter`），`UseXiHan()` 在进管道前绑定载荷，Authorize 阶段按契约判定，Validate 阶段按 Schema 校验。成功时 `create` 返回 201、其余 200；失败由宿主写成 Problem Details。带幂等键的请求重放时端点不执行，`UseXiHan()` 用同一个写出委托写出首次的状态码与正文（[缓存](/caching/locks)）。

`MapXiHanOpenApi()` 在第一次请求时从登记表、`Kernel.Generators` 生成的 Schema、权限码注册表、已装配的认证处理器与宿主的普通端点生成 OpenAPI 3.1 并缓存，带 `x-xihan-permission` / `x-xihan-policy` 扩展与权限定义组件（ADR-0031），按区域给出操作文案、标签与字段说明，支持分组文档（ADR-0052）。

## 何时使用

操作契约要给浏览器、前端或第三方经 HTTP 调用时用它；同一份契约的 gRPC、MCP 暴露见 [Protocols.Grpc](./protocols-grpc)、[Protocols.Mcp](./protocols-mcp)。不是操作的普通最小 API 端点不需要本包也能运行，但要进 OpenAPI 文档时由本包收录。本包不提供 HTTP 客户端，调用其他服务用 `IHttpClientFactory`。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Protocols.Http --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。`Profiles.Full` 已包含本包，`UseFullProfile()` 会装配 `HttpProtocolFeature`。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.OpenApi;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<HttpProtocolFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanOperations();
app.MapXiHanOpenApi();
app.Run();
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Protocols:Http:RoutePrefix` | string | 空 | 全部操作路由的前缀，以 `/` 开头、不以 `/` 结尾 |
| `XiHan:Protocols:Http:Routes:<操作 ID>` | string | — | `METHOD /template` 显式覆盖；方法限 `GET`、`POST`、`PUT`、`PATCH`、`DELETE` |

```json
{
  "XiHan": {
    "Protocols": {
      "Http": {
        "RoutePrefix": "/api",
        "Routes": { "orders.export": "POST /orders:export" }
      }
    }
  }
}
```

OpenAPI 的标题、版本、`servers` 与分组不走配置，由传给 `MapXiHanOpenApi` 的 `OpenApiOptions` 指定。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `HttpProtocolFeature` | 特性入口 |
| `HttpProtocolOptions` | `RoutePrefix`、`Routes`；`RouteFor(descriptor)` 给出最终路由；`FromConfiguration` |
| `Routing.OperationEndpointRouteBuilderExtensions` | `MapXiHanOperations()`，返回这组操作端点的约定构建器（`IEndpointConventionBuilder`），加在上面的约定作用于每个操作端点 |
| `Routing.HttpRouteConvention` | 推导规则：`Derive`、`HasIdentifier`、`HasNestedFields`、`SuccessStatusCode`；常量 `IdentifierProperty` = `id` |
| `Routing.HttpRoute` | 方法与模板；`Parse("POST /orders:export")` |
| `Routing.OperationRequestBinder` | `BindAsync`：路由值、请求体、查询串合并成载荷 |
| `OpenApi.OpenApiEndpointRouteBuilderExtensions` | `MapXiHanOpenApi(options, pattern)`、`GetXiHanOpenApiDocuments(options, pattern)`、`GroupPattern`；常量 `DefaultPattern` = `/openapi.json`、`FullDocumentName` = `all` |
| `OpenApi.OpenApiOptions`、`OpenApi.OpenApiServer`、`OpenApi.OpenApiGroup` | 文档标题、版本、`servers` 与分组 |
| `OpenApi.OpenApiDocumentLink` | 一份文档的名字、标题与地址，给文档界面登记 |
| `OpenApi.OpenApiEndpointConventionBuilderExtensions` | `WithXiHanOperationSchema(request, response)`：普通端点声明引用哪个操作的 Schema |
| `OpenApi.OpenApiDocumentSource`、`OpenApi.OpenApiDocumentBuilder` | 不经宿主生成文档：`FromServices(...)` 组装输入，`Build(source)` 生成字节 |
| `HttpErrors` | `PROTOCOLS_CONFLICT_002`、`PROTOCOLS_VALIDATION_010` |
| `HttpMessages` | 生成器文案的消息键（`protocols.http.openapi_*`） |

## 使用示例

覆盖一个冲突的路由：没有 `id` 的 `things.get` 与 `things.list` 都推导为 `GET /things`，启动时以 `PROTOCOLS_CONFLICT_002` 失败，给其中一个换路由即可：

```json
{
  "XiHan": {
    "Protocols": {
      "Http": {
        "Routes": { "things.get": "GET /things:current" }
      }
    }
  }
}
```

发布带分组的文档，并把全部文档地址交给文档界面：

```csharp
using XiHan.Framework.Protocols.Http.OpenApi;

var openApi = new OpenApiOptions
{
    Title = "Orders API",
    Version = "v1",
    Groups = [new OpenApiGroup("orders", 1, ["xihan.samples.orders"])],
};
app.MapXiHanOpenApi(openApi);   // /openapi.json 与 /openapi/orders.json

foreach (var link in app.GetXiHanOpenApiDocuments(openApi))
{
    app.Logger.LogInformation("OpenAPI document {Name}: {Url}", link.Name, link.Url);
}
```

路由推导、绑定、文案键与普通端点收录的完整说明见 [HTTP 与 OpenAPI](/web/routing)。

## 行为与限制

- `MapXiHanOperations()` 必须在 `UseXiHan()` 之后；端点内取不到调用上下文或载荷时抛出 `FrameworkException`（`KERNEL_HOSTING_004`）。
- 只有 `POST`、`PUT`、`PATCH` 读请求体，请求体必须是 JSON 对象；路由值优先于请求体，请求体优先于查询串。
- OpenAPI 文档端点绕过内核管道、不鉴权；文档按 `(分组, 区域)` 各缓存一份，装配了本地化时按 `Accept-Language` 协商区域。
- 普通端点的 JSON 类型只引用已登记操作的 Schema，不做第二套推导。
- 路由推导的细节：请求声明了 `id` 属性时 `get` → `GET /{resource}/{id}`、`update` → `PUT /{resource}/{id}`、`delete` → `DELETE /{resource}/{id}`、其他动词 → `POST /{resource}/{id}:{verb}`；契约声明只读且请求没有嵌套对象的自定义动词改用 `GET`（Google AIP-136，ADR-0041）；没有 `id` 时去掉 `/{id}` 段，落在集合（或单例资源）上（`GET /{resource}`、`PUT /{resource}`、`DELETE /{resource}`、`POST /{resource}:{verb}`）；`get` / `list` / `delete` 的请求带嵌套对象时（GET / DELETE 不读请求体）改用 POST 自定义动词（`POST /{resource}:list`）。是否含 `id` 看请求的序列化元数据，没有类型化元数据的插件代理看请求 Schema 的 `properties`。
- 绑定的细节：路由值与查询串按请求类型序列化元数据里的属性类型写成 JSON 布尔、整数、浮点、数值枚举或数组（集合属性的单值也写成数组），属性名取序列化名、不分大小写匹配；找不到属性或没有元数据（插件代理）时写字符串，多值写字符串数组。属性名按请求类型序列化上下文的命名策略匹配，建议 `PropertyNamingPolicy = CamelCase` 加 `PropertyNameCaseInsensitive = true`（示例里的 `SampleJsonContext`）。
- 响应：幂等回放时端点不执行，由 `UseXiHan()` 用挂在端点元数据上的 `OperationResponseWriter` 写出首次的状态码与正文（ADR-0064）；失败写成 RFC 9457 Problem Details，`detail` 按 `Accept-Language` 本地化，VALIDATION 错误带字段级 `errors`（字段键 → 本地化消息数组），所有错误带标量 `details`，见 `Hosting.AspNetCore`。授权在 Authorize 阶段按契约的权限或策略判定（描述经端点元数据进入上下文）。
- OpenAPI 生成：`MapXiHanOpenApi(options?, pattern = "/openapi.json")` 在第一次请求时生成 OpenAPI 3.1 并缓存；`OpenApiOptions` 缺省标题取应用名、版本取框架版本、不写 `servers`。路径与方法同路由推导；路由参数与（无请求体方法的）查询参数、请求体、成功响应都来自 `OperationSchema`，`$defs` 提升到 `components/schemas`（字符串枚举定义也是一个组件，属性与查询参数以 `$ref` 引用）；失败响应统一引用 `ProblemDetails` 组件（标准成员加 `code`、`traceId`、`errors`：字段键 → 字符串数组、`details`：名字 → 标量）；没有 Schema 的操作只发布路径与 `operationId`。`OpenApiDocumentBuilder.Build(OpenApiDocumentSource)` 是不经宿主的生成入口（测试与工具用），`OpenApiDocumentSource.FromServices(services, options, endpoints, locale)` 从容器组装与 `MapXiHanOpenApi` 相同的输入（登记表、权限码注册表、认证方案、普通端点、按区域的摘要），导出工具据此得到与 `/openapi.json` 逐字节相同的文档（ADR-0034）。
- OpenAPI 认证方案（ADR-0032）：容器里有 `Scheme == "Bearer"` 的 `IAuthenticationHandler`（`Security.Jwt`、`Security.OpenIdConnect`）时发布 `components.securitySchemes.bearer`（`http` / `bearer` / `JWT`）并挂全局 `security: [{ bearer: [] }]`，没有权限也没有策略要求的操作与端点标 `security: []`；权限码与策略名不进 scope，仍只走 `x-xihan-*`；ApiKey 等其他方案不发布。
- OpenAPI 操作文案（ADR-0052）：`summary` 取消息目录里 `<操作 ID>.title`，`description` 取 `<操作 ID>.description`（`-` 换 `_`，与 MCP `tools/list` 同两个键），各自没有就不写；授权要求由生成器按区域附在 `description` 末尾一行（`HttpMessages`：需要权限 <码>（<显示名>）/ 需要策略 <名> / 需要登录），权限的显示名先取 `permissions.<权限码>.name`（`-` 换 `_`），没有再取权限定义的；`x-xihan-permissions` 的 `displayName` / `description` 同样先取 `permissions.<权限码>.name` / `.description`，文案里不要再手写。装配了本地化时按 `Accept-Language` 协商出的区域分别生成与缓存（`Content-Language` 标出区域，缺省 `en-US`），没有装配时只有一份文档、没有目录文案。
- OpenAPI 标签、分组与字段说明（ADR-0052）：顶层 `tags` 的 `x-displayName` / `description` 取 `openapi.tags.<资源段>.name` / `.description`，`name` 仍是资源码；`OpenApiOptions.Groups` 声明 `OpenApiGroup(名字, 排序, 所属特性, 额外标签)`，全量文档带 `x-tagGroups`（一个标签只归排序靠前的一组），每组另发 `/openapi/<组名>.json`（标题取 `openapi.groups.<组名>.name`），`GetXiHanOpenApiDocuments()` 列出全部文档给 Scalar：`foreach (var link in app.GetXiHanOpenApiDocuments(options)) scalar.AddDocument(link.Name, link.Title, link.Url);`。Schema 里 `Kernel.Generators` 从 XML 注释折进的 `description` 进参数、请求体与成功响应（没有根说明时是按区域的「成功」）；某个区域可用 `openapi.schemas.<snake_case 的 Schema 名>.description` 与 `.properties.<snake_case 的属性名>` 覆盖。
- OpenAPI 普通端点（ADR-0032）：`EndpointDataSource` 里没有 `OperationDescriptor`、有 HTTP 方法元数据、带 `IAcceptsMetadata` 或 `IProducesResponseTypeMetadata` 之一、没有 `ExcludeFromDescription()` 的路由端点进文档，Hub、探针、重定向、静态资源没有这两种元数据，自然不进。路由参数作 `path` 参数（`string`）；`WithName` 作 `operationId`，`WithSummary` / `WithDescription` / `WithTags` 照搬；`RequireXiHanPermission` / `RequireXiHanPolicy` / `RequireXiHanAuthentication` 出 `x-xihan-permission` / `x-xihan-policy`，权限码并入 `x-xihan-permissions`。`Accepts` / `Produces` 按内容类型写：JSON 只引用已登记操作的根 Schema（显式 `WithXiHanOperationSchema(request:, response:)`，或按 `Accepts<T>` / `Produces<T>` 的类型与操作的请求 / 响应类型自动匹配，取 ID 最小且有 Schema 的操作；被引用的根提升到 `components/schemas/<title>`，操作自己也改为 `$ref`），`multipart/form-data` 是含 `format: binary` 属性（`file` / `files`）的对象，表单是对象，`text/*` 与 `string` 是字符串，其余二进制是 `format: binary` 的字符串；失败响应同样引用 `ProblemDetails`。
- OpenAPI 权限扩展（ADR-0031）：契约有 `Permission` 的操作带 `x-xihan-permission`（权限码），有 `Policy` 的带 `x-xihan-policy`（策略名），都没有则不带；不占用标准 `security` / `securitySchemes`（权限码不是 OAuth scope）。`components.x-xihan-permissions` 按权限码（序数排序）给出权限定义，收录范围是文档里已发布操作引用的权限码加上 `PermissionRegistry` 登记的全部定义：只在 gRPC / MCP 暴露的操作或端点级 `RequireXiHanPermission` 用到的权限也在其中，授权界面据此生成；被引用但未登记的只有 `code`，`description` 为 null 时省略。`OpenApiDocumentBuilder.Build(registry, options, permissions, title, version)` 的 `permissions` 为 null 时只按引用写。形如：`{ "paths": { "/orders": { "post": { "operationId": "orders.create", "x-xihan-permission": "orders.write" } } }, "components": { "x-xihan-permissions": { "orders.write": { "code": "orders.write", "displayName": "Place orders" } } } }`。

| 情形 | 何时 | 错误码 |
| --- | --- | --- |
| 两个操作推导出同一条路由 | `MapXiHanOperations()` | `PROTOCOLS_CONFLICT_002` |
| 普通端点与操作共用一条路由 | 第一次请求文档 | `PROTOCOLS_CONFLICT_002` |
| 普通端点的 JSON 类型引用不到操作的 Schema | 第一次请求文档 | `PROTOCOLS_VALIDATION_010` |
| 请求体不是 JSON 对象 | 绑定 | `KERNEL_VALIDATION_001`（400） |
| 前缀、路由文本或操作 ID 不合法 | 读取配置 | `ArgumentException` |

## 相关

- [HTTP 与 OpenAPI](/web/routing)、[操作契约](/web/operations)、[宿主与 Web 应用](/web/hosting)
- [Hosting.AspNetCore](./hosting-aspnetcore)、[Kernel.Generators](./kernel-generators)
- [Protocols.Grpc](./protocols-grpc)、[Protocols.Mcp](./protocols-mcp)、[Protocols.CloudEvents](./protocols-cloudevents)
- 规范：`spec/protocol/http.md`《HTTP 载体》、`spec/conventions/operation-ids.md`《操作 ID》、RFC-0011《操作契约与 XGI》
