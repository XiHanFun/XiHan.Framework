# HTTP 与 OpenAPI

本页讲 HTTP 协议适配器怎样把操作契约发布成 REST 端点：路由推导与覆盖、载荷绑定、状态码与错误映射，以及从同一份契约生成 OpenAPI 3.1 文档，适用于要经 HTTP 暴露操作的 Web 应用。完整 API 见 [Protocols.Http 包](../packages/protocols-http)，操作契约本身（`[Operation]`、`IOperation<TRequest, TResponse>`、`Expose`、生成器）见[操作契约](./operations)。

## 适配器做什么

`Protocols.Http` 提供 `protocols.http@1`。`MapXiHanOperations()` 遍历操作登记表，把 `Expose` 含 `Http`、稳定性不是 `Stability.Internal` 的每个操作映射成一个 ASP.NET Core 端点，路由由操作 ID 推导。端点元数据带着操作描述与载荷绑定委托：`UseXiHan()` 在进入内核管道前把描述放进调用上下文、把请求绑定成载荷，Authorize 阶段按契约的权限或策略判定，Validate 阶段按请求 Schema 校验并解码，端点内只剩执行。失败结果留在上下文里，由 `UseXiHan()` 写成 Problem Details。宿主这一侧的行为见[宿主与 Web 应用](./web)。

OpenAPI 文档不是第二套描述：`MapXiHanOpenApi()` 从同一个登记表、`Kernel.Generators` 生成的请求 / 响应 JSON Schema、权限码注册表与宿主的端点表生成，路径与方法和 `MapXiHanOperations()` 用同一条推导规则。

## 启用

1. 引用 `XiHan.Framework.Protocols.Http`（`Profiles.Full` 已包含）。
2. `UseFeature<HttpProtocolFeature>()`。
3. 在 `app.UseXiHan()` 之后调用 `app.MapXiHanOperations()`，需要文档时再调用 `app.MapXiHanOpenApi()`。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.OpenApi;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<OrdersFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanOperations();   // orders.create → POST /orders，orders.list → GET /orders
app.MapXiHanOpenApi();      // GET /openapi.json
app.Run();
```

`OrdersFeature` 是 `samples/SimpleApp` 的订单能力包，登记了 `orders.create` 与 `orders.list` 两个操作。`MapXiHanOperations()` 返回映射的端点数。

## 路由推导

操作 ID 是 `<资源>.<动词>`（`orders.create`、`order-lines.list`），路由由动词与请求的形状决定。形状看两点：请求是否声明了 `id` 属性（按序列化名、不区分大小写），以及请求是否有嵌套对象字段（对象、字典、元素是对象的集合）。判断读源生成的序列化元数据，不用反射；进程外插件这类没有类型化元数据的操作看请求 Schema 的 `properties`，两者都没有时按集合级、无嵌套处理。

| 动词 | 请求含 `id` | 请求不含 `id` | 请求带嵌套对象 |
| --- | --- | --- | --- |
| `create` | `POST /{resource}` | `POST /{resource}` | 不变 |
| `list` | `GET /{resource}` | `GET /{resource}` | `POST /{resource}:list` |
| `get` | `GET /{resource}/{id}` | `GET /{resource}` | `POST /{resource}/{id}:get`、`POST /{resource}:get` |
| `update` | `PUT /{resource}/{id}` | `PUT /{resource}` | 不变 |
| `delete` | `DELETE /{resource}/{id}` | `DELETE /{resource}` | `POST /{resource}/{id}:delete`、`POST /{resource}:delete` |
| 其他动词 | `POST /{resource}/{id}:{verb}` | `POST /{resource}:{verb}` | 不变 |
| 其他动词，契约声明只读 | `GET /{resource}/{id}:{verb}` | `GET /{resource}:{verb}` | `POST`，同上一行 |

规则背后的考虑有三条：

- 实例级与集合级（ADR-0015）：带 `id` 的请求是对某个实例的动作，路径带 `/{id}` 段；不带的落在集合或单例资源上，如 `GET /profile`、`POST /orders:export`。`create` 与 `list` 恒为集合级。
- GET 与 DELETE 不读请求体：`get`、`list`、`delete` 的请求有嵌套对象（分页请求的 `page`、复合筛选）时进不了查询串，改用 POST 自定义动词。
- 只读的自定义动词（ADR-0041）：`[Operation(..., ReadOnly = true)]` 声明的非标准动词（`departments.tree`、`users.list-online`）与 `get` 同规则，只有标量字段时走 GET（Google AIP-136）。只读必须显式声明，不按动词前缀猜。

| 操作 | 请求 | 路由 |
| --- | --- | --- |
| `orders.get` | `{ id }` | `GET /orders/{id}` |
| `profile.get` | 没有 `id` | `GET /profile` |
| `orders.cancel` | `{ id, reason }` | `POST /orders/{id}:cancel` |
| `orders.export` | 没有 `id` | `POST /orders:export` |
| `orders.list` | `{ page: { index, size }, keyword }` | `POST /orders:list` |
| `departments.tree`，`ReadOnly = true` | 没有 `id`，只有标量 | `GET /departments:tree` |

两个操作推导出同一条路由（方法与模板都相同，如没有 `id` 的 `things.get` 与 `things.list` 都是 `GET /things`）时，`MapXiHanOperations()` 抛出 `FrameworkException`，错误码 `PROTOCOLS_CONFLICT_002`，应用启动失败。用下一节的覆盖给其中一个换路由。

## 覆盖与前缀

路由覆盖与统一前缀写在配置节 `XiHan:Protocols:Http`：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `RoutePrefix` | string | 空 | 加在全部操作路由前，如 `/api`；必须以 `/` 开头、不能以 `/` 结尾 |
| `Routes:<操作 ID>` | string | — | `METHOD /template`，如 `POST /orders:export`；方法限 `GET`、`POST`、`PUT`、`PATCH`、`DELETE`（不区分大小写），模板以 `/` 开头 |

```json
{
  "XiHan": {
    "Protocols": {
      "Http": {
        "RoutePrefix": "/api",
        "Routes": {
          "orders.export": "POST /orders:export"
        }
      }
    }
  }
}
```

覆盖优先于推导，前缀在两者之后加上：上例的 `orders.export` 最终是 `POST /api/orders:export`，`orders.get` 是 `GET /api/orders/{id}`。模板用 ASP.NET Core 路由语法，`{name}` 参数绑定到请求对象的同名属性。前缀或路由文本不合法、操作 ID 不合法时，读取配置就抛出 `ArgumentException`。

## 载荷绑定

一次请求的载荷是一个 JSON 对象，由三处合并而成，同名时前者优先：

1. 路由值：URL 里的标识是权威，请求体里的同名字段被它覆盖。
2. JSON 请求体：只有 `POST`、`PUT`、`PATCH` 读请求体，必须是 JSON 对象；空请求体按没有处理。
3. 查询串。

路由值与查询串都是字符串，绑定器按请求类型序列化元数据里的属性类型写成 JSON：布尔、整数、浮点与数值枚举写成字面量，集合属性写成数组（单个值也写成一元数组），其余写字符串；属性名不区分大小写匹配并改写成序列化名。找不到属性或没有类型化元数据（插件代理）时按字符串写，多值写成字符串数组。解析不了的值（如布尔属性收到 `maybe`）照样按字符串写，由反序列化报出 `KERNEL_VALIDATION_001`。

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

// 请求声明了 id：实例级自定义动词，HTTP 上是 POST /orders/{id}:cancel
[Operation(Id, Expose = Expose.Http, Permission = "orders.write")]
public sealed class CancelOrder : IOperation<CancelOrderRequest, CancelOrderResponse>
{
    public const string Id = "orders.cancel";

    public ValueTask<Result<CancelOrderResponse>> HandleAsync(CancelOrderRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(new CancelOrderResponse(request.Id, request.Reason)));
}

public sealed record CancelOrderRequest(Guid Id, string Reason);

public sealed record CancelOrderResponse(Guid Id, string Reason);
```

```bash
curl -s -X POST "localhost:5000/orders/0f8fad5b-d9cb-469f-a165-70867728950e:cancel" \
  -H "Authorization: ApiKey dev-alice-key" -H "Content-Type: application/json" \
  -d '{"id":"00000000-0000-0000-0000-000000000000","reason":"late"}'
```

交给操作的载荷是 `{"id":"0f8fad5b-d9cb-469f-a165-70867728950e","reason":"late"}`：请求体里的 `id` 被路由值覆盖。请求类型与响应类型要登记进源生成的序列化上下文；绑定按 JSON 约定匹配属性，上下文建议用 `PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase`、`PropertyNameCaseInsensitive = true` 与 `NumberHandling = JsonNumberHandling.AllowReadingFromString`，写法见[宿主与 Web 应用](./web#native-aot)。

绑定发生在进入管道之前，校验在管道里：

| 阶段 | 拒绝的情形 | 错误码 |
| --- | --- | --- |
| 绑定（进管道前） | 请求体不是 JSON 或不是 JSON 对象 | `KERNEL_VALIDATION_001` |
| Validate | 载荷不满足请求 Schema（类型、必填、范围、长度、模式等） | `KERNEL_VALIDATION_003` |
| Validate | 请求对象自校验（`IValidatableObject`）失败 | `KERNEL_VALIDATION_004` |
| Validate | 载荷解码失败（类型不符、`null`） | `KERNEL_VALIDATION_001` |

没有登记 Schema 的操作不做 Schema 校验，解码失败仍是 `KERNEL_VALIDATION_001`。字段级 `errors` 的形状见[宿主与 Web 应用](./web#problem-details)，校验规则见[数据校验](./validation)。

## 状态码与错误映射

成功时 `create` 返回 201，其余动词返回 200，正文是响应类型的 JSON，`Content-Type: application/json; charset=utf-8`。失败时写 RFC 9457 Problem Details，状态码由错误码的 KIND 段决定（`ProblemResponse.StatusCodeFor`）：

| KIND | HTTP 状态码 |
| --- | --- |
| `VALIDATION` | 400 |
| `NOTFOUND` | 404 |
| `CONFLICT` | 409 |
| `DENIED` | 403；`SECURITY_DENIED_001`（未认证）与 `SECURITY_DENIED_003`（凭据无效、过期或签名不符）为 401 |
| `UNAVAILABLE` | 503 |
| `TIMEOUT` | 504 |
| `CANCELLED` | 408 |
| 其他（含 `UNEXPECTED`） | 500 |

401 与 403 的分界按 RFC 6750（ADR-0042）：凭据无效是认证失败，客户端与网关据 401 刷新令牌或重新登录，据 403 判定越权。每个 401 都带 `WWW-Authenticate` 质询：

- 请求出示了凭据但无效：只质询被拒的那个方案，Bearer 写成 `Bearer error="invalid_token"`，其他方案只写方案名（如 `ApiKey`）。
- 请求没有凭据：按已装配认证处理器的方案逐个质询，不带错误码；一个处理器都没有时不写。

`samples/SimpleApp` 里 `bob` 只有 `orders.read`，下单得到 403：

```bash
curl -s -X POST localhost:5000/orders -H "Authorization: ApiKey dev-bob-key" \
  -H "Content-Type: application/json" -d '{"name":"x","amount":1}'
```

```json
{
  "type": "urn:xihan:error:SECURITY_DENIED_004",
  "title": "DENIED",
  "status": 403,
  "detail": "Permission 'orders.write' is missing.",
  "instance": "/orders",
  "code": "SECURITY_DENIED_004",
  "traceId": "0af7651916cd43dd8448eb211c80319c",
  "details": { "permission": "orders.write" }
}
```

不带 `Authorization` 头时同一请求得到 401 `SECURITY_DENIED_001`，响应头 `WWW-Authenticate: ApiKey`。访问日志等需要状态码的地方用框架写响应时的同一映射：失败按 `ProblemResponse.StatusCodeFor(error)`，成功按 `HttpRouteConvention.SuccessStatusCode(contract)`。

## OpenAPI

`MapXiHanOpenApi(options, pattern)` 发布 OpenAPI 3.1 文档，`pattern` 缺省 `/openapi.json`。文档在第一次请求时生成并缓存，这时全部特性已装配、登记表与端点表都已定型。文档端点带 `PipelineBypassMetadata`，不经内核管道、不鉴权；需要限制访问时由宿主自己处理。

| 文档内容 | 来源 |
| --- | --- |
| `paths` 与方法 | 与 `MapXiHanOperations()` 同一条路由推导（含覆盖与前缀） |
| `operationId`、`tags` | 操作 ID；资源段 |
| 路径参数、查询参数、请求体 | 请求 Schema：模板里的 `{name}` 是必填路径参数，GET / DELETE 的其余属性是查询参数，POST / PUT / PATCH 的请求 Schema 整体作 `requestBody` |
| 成功响应 | 状态码同上一节，正文是响应 Schema |
| `default` 响应 | `application/problem+json`，引用 `components/schemas/ProblemDetails` |
| `components/schemas` | Schema 里的 `$defs` 提升而来；字符串枚举也是一个组件（ADR-0034） |
| `x-xihan-stability` | 稳定性不是 `Stable` 的操作 |

没有 Schema 的操作只发布路径、方法与 `operationId`。只给 gRPC、MCP 的操作与 `Stability.Internal` 的操作不进文档。

### 文档选项

```csharp
using XiHan.Framework.Protocols.Http.OpenApi;

var openApi = new OpenApiOptions
{
    Title = "Orders API",
    Version = "v1",
    Servers = [new OpenApiServer("https://api.example.com", "生产")],
};
app.MapXiHanOpenApi(openApi);
```

`Title` 缺省取宿主的应用名，`Version` 缺省取框架版本，`Servers` 为空时不写 `servers`。

### 权限与认证方案

契约有 `Permission` 的操作带 `x-xihan-permission`（权限码），有 `Policy` 的带 `x-xihan-policy`（策略名），都没有时不写（ADR-0031）。`components` 下的 `x-xihan-permissions` 按权限码（序数排序）列出权限定义，收录范围是文档里已发布操作引用的权限码与内核 `PermissionRegistry` 登记的全部权限的并集：只在 gRPC、MCP 上暴露的操作或端点级 `RequireXiHanPermission` 用到的权限也在里面，前端据此隐藏无权的入口、生成授权界面；被引用却没登记的只有 `code`。权限码不是 OAuth scope，所以不占用标准的 `security` / `securitySchemes`。

装配了以 `Bearer` 方案工作的认证 Provider（`Security.Jwt`、`Security.OpenIdConnect`）时，文档发布 `components.securitySchemes.bearer`（`type: http`、`scheme: bearer`、`bearerFormat: JWT`）并挂全局 `security: [{ "bearer": [] }]`，没有权限也没有策略要求的操作标 `security: []`（ADR-0032）。ApiKey 等其他方案不发布。

`samples/SimpleApp` 的文档里 `/orders` 一节（截取）：

```json
{
  "paths": {
    "/orders": {
      "post": {
        "operationId": "orders.create",
        "tags": ["orders"],
        "summary": "Place an order",
        "description": "Place an order with a name and an amount.\n\nRequires permission orders.write (Place orders).",
        "x-xihan-stability": "Experimental",
        "x-xihan-permission": "orders.write"
      }
    }
  },
  "components": {
    "x-xihan-permissions": {
      "orders.read": { "code": "orders.read", "displayName": "Read orders" },
      "orders.write": { "code": "orders.write", "displayName": "Place orders" }
    }
  }
}
```

### 文案、标签与字段说明

文档里面向人的文字都来自消息目录，装配了本地化时按 `Accept-Language` 协商出的区域分别生成、分别缓存，响应带 `Content-Language`，缺省区域是 `en-US`；没有装配本地化时只有一份文档，没有这些文案（ADR-0052）。键里的 `-` 一律换成 `_`。

| 键 | 用于 |
| --- | --- |
| `<操作 ID>.title` | 操作的 `summary`；MCP `tools/list` 的 `title` 用同一个键 |
| `<操作 ID>.description` | 操作的 `description`；MCP 的 `description` 用同一个键 |
| `permissions.<权限码>.name` / `.description` | 授权行里的显示名，`x-xihan-permissions` 的 `displayName` / `description`；没有时取权限定义的 |
| `openapi.tags.<资源段>.name` / `.description` | 顶层 `tags` 的 `x-displayName` / `description`；`name` 仍是资源码 |
| `openapi.groups.<组名>.name` / `.description` | 分组文档的 `info.title` / `info.description` 与 `x-tagGroups` 的组名 |
| `openapi.schemas.<Schema 名>.description` | 覆盖根或组件的说明 |
| `openapi.schemas.<Schema 名>.properties.<属性名>` | 覆盖属性的说明 |

`summary` 与 `description` 各自没有就不写，不互相顶替。授权要求由生成器附在 `description` 末尾，空一行：权限是「需要权限 <码>（<显示名>）」，命名策略是「需要策略 <名>」，`authenticated` 策略是「需要登录」，文案在框架目录 `protocols.http.openapi_*`。应用的 `.description` 里不要再手写权限要求。

字段说明的源语言文案是 DTO 的 XML 文档注释：`Kernel.Generators` 把类型与属性的 `<summary>`（位置记录取主构造参数的 `<param>`）折进 Schema 的 `description`，参数、请求体与成功响应据此写说明，成功响应没有根说明时写按区域的「成功」。某个区域要换文案时才写 `openapi.schemas.*` 覆盖，Schema 名与属性名都转成 snake_case（`ConstraintRuleDto` → `constraint_rule_dto`，`ruleType` → `rule_type`）。

### 分组

`OpenApiOptions.Groups` 声明分组，每组另发一份文档：

```csharp
using XiHan.Framework.Protocols.Http.OpenApi;

var openApi = new OpenApiOptions
{
    Groups =
    [
        // 名字、排序、所属特性的清单 ID、额外标签（让没有操作的普通端点入组）
        new OpenApiGroup("orders", 1, ["xihan.samples.orders"]),
        new OpenApiGroup("system", 2, [], ["health"]),
    ],
};
app.MapXiHanOpenApi(openApi);   // /openapi.json、/openapi/orders.json、/openapi/system.json

foreach (var link in app.GetXiHanOpenApiDocuments(openApi))
{
    // 交给文档界面登记，如 Scalar 的 AddDocument(link.Name, link.Title, link.Url)
    app.Logger.LogInformation("OpenAPI document {Name}: {Url}", link.Name, link.Url);
}
```

组名匹配 `^[a-z][a-z0-9-]*$`、互不重复且不能是 `all`（全量文档的名字），一个特性只能属于一个组，违反时构造 `OpenApiOptions` 就抛出 `ArgumentException`。操作按归属特性入组，普通端点按标签入组：组的标签是组内操作的资源段加额外标签。全量文档带 `x-tagGroups`，按排序再按名字列出各组，一个标签只归排序靠前的那一组，没进任何组的标签归入最后的「其他」组。分组文档的地址是 `pattern` 去掉 `.json` 后接 `/<组名>.json`，只含本组的操作、端点、组件与引用到的权限码。`GetXiHanOpenApiDocuments` 的参数要与 `MapXiHanOpenApi` 一致，全量文档排在最前。

### 普通端点

宿主里不是操作的最小 API 端点也能进文档（ADR-0032）。收录条件：有 HTTP 方法元数据，带 `Accepts` 或 `Produces` 描述元数据之一，没有 `ExcludeFromDescription()`。SignalR Hub、探针、重定向、静态资源没有描述元数据，不会进文档。路由参数写成 `string` 类型的路径参数；`WithName` 作 `operationId`，`WithSummary`、`WithDescription`、`WithTags` 照搬；`RequireXiHan*` 的要求写成 `x-xihan-permission` / `x-xihan-policy`。

请求体与响应按内容类型写：`multipart/form-data` 是含 `format: binary` 属性（`file`，多文件为 `files`）的对象，表单是对象，`text/*` 与 `string` 是字符串，其余二进制是 `format: binary` 的字符串。JSON 类型不做第二套 Schema 推导，只引用已登记操作的根 Schema：用 `WithXiHanOperationSchema(request:, response:)` 显式声明同哪个操作的请求或响应，没有声明时按 `Accepts<T>` / `Produces<T>` 的类型与操作的请求、响应类型自动匹配。

```csharp
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Protocols.Http.OpenApi;

app.MapGet("/orders/{id}/receipt", (Guid id) => Results.Text($"receipt {id}"))
    .WithName("orders.receipt")
    .WithTags("orders")
    .Produces<string>(StatusCodes.Status200OK, "text/plain")
    .RequireXiHanPermission("orders.read");

// 响应正文同 orders.create 的响应 Schema：文档里引用同一个组件，不另推导
app.MapGet("/orders/sample", () => new OrderResponse(Guid.NewGuid(), "widget", 3, DateTimeOffset.UtcNow))
    .WithName("orders.sample")
    .Produces<OrderResponse>()
    .WithXiHanOperationSchema(response: "orders.create");
```

`OrderResponse` 是 `samples/SimpleApp` 里 `orders.create` 的响应类型。

JSON 类型引用不到任何操作的 Schema 时，文档生成以 `PROTOCOLS_VALIDATION_010` 失败；普通端点与操作共用一条路由时以 `PROTOCOLS_CONFLICT_002` 失败。两种失败都在第一次请求文档时暴露，修正办法是补 `WithXiHanOperationSchema`、登记对应的操作，或给端点加 `ExcludeFromDescription()`。

### 不经宿主导出文档

`OpenApiDocumentSource.FromServices(services, options, endpoints, locale, group)` 从容器组装与 `MapXiHanOpenApi` 相同的输入，`OpenApiDocumentBuilder.Build(source)` 生成 UTF-8 字节，与 `/openapi.json` 逐字节一致。契约导出工具与测试用这一对入口，不必启动 HTTP 服务。

## HTTP 客户端

5.0 不提供 HTTP 客户端封装。调用其他服务用 ASP.NET Core 的 `IHttpClientFactory`（`AddHttpClient`），超时、重试与熔断按 .NET 的方式配置；服务之间的事件推送见 [CloudEvents](./protocols#cloudevents)。

## 相关

- [Protocols.Http 包](../packages/protocols-http)、[Hosting.AspNetCore 包](../packages/hosting-aspnetcore)
- [操作契约](./operations)、[宿主与 Web 应用](./web)
- [gRPC、MCP 与 CloudEvents](./protocols)：同一份契约的其他载体
- [数据校验](./validation)、[授权](./authorization)、[国际化](./localization)
- 规范：`spec/protocol/http.md`《HTTP 载体》、`spec/conventions/operation-ids.md`《操作 ID》、RFC-0011《操作契约与 XGI》
