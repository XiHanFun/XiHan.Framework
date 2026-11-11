来源：https://framework.docs.xihanfun.com/web/openapi

# 规范化接口文档（OpenAPI）

这页讲怎样从操作契约发布 OpenAPI 文档：`MapXiHanOpenApi()` 从哪些输入生成文档、文档里的权限与认证方案怎样表达、面向人的文案从哪里来、怎样把一个大应用的文档拆成几组、普通端点怎样进文档、怎样接 Scalar 或 Swagger UI 这类查看器，以及不启动 HTTP 服务怎样导出同一份文档。读完这页，我们能发布一份与实际路由逐字一致、带权限标注、按调用方语言翻译的接口文档，并把它交给前端与生成客户端代码的工具。

前置知识：[操作契约](/web/operations)与 [HTTP 路由与载荷](/web/routing)。

## 为什么需要

手写或用注解维护的接口文档总会和实现脱节：路由改了文档没改，字段加了必填没标，权限要求只写在代码里前端看不见。文档一旦不可信，前端就只能靠猜或者读后端源码。

框架的文档不是第二套描述。`MapXiHanOpenApi()` 读的是 `MapXiHanOperations()` 用的同一个操作登记表、`Kernel.Generators` 在编译期生成的同一份请求 / 响应 JSON Schema、同一个权限码注册表与宿主的端点表，路径与方法用同一条推导规则算出来。契约改了，文档跟着变，不需要人手同步；gRPC 的 `.proto` 与 MCP 的 tool 描述也来自这份 Schema，三种载体的字段定义天然一致。

## 文档从哪里来

`MapXiHanOpenApi(options, pattern)` 发布 OpenAPI 3.1 文档，`pattern` 缺省 `/openapi.json`。文档在第一次请求时生成并缓存，这时全部特性已装配、登记表与端点表都已定型。文档端点带 `PipelineBypassMetadata`，不经内核管道、不鉴权；需要限制访问时由宿主自己处理（见常见问题）。

| 文档内容 | 来源 |
| --- | --- |
| `paths` 与方法 | 与 `MapXiHanOperations()` 同一条路由推导（含覆盖与前缀） |
| `operationId`、`tags` | 操作 ID；资源段 |
| 路径参数、查询参数、请求体 | 请求 Schema：模板里的 `{name}` 是必填路径参数，GET / DELETE 的其余属性是查询参数，POST / PUT / PATCH 的请求 Schema 整体作 `requestBody` |
| 成功响应 | 状态码同 HTTP 映射（`create` 201，其余 200），正文是响应 Schema |
| `default` 响应 | `application/problem+json`，引用 `components/schemas/ProblemDetails` |
| `components/schemas` | Schema 里的 `$defs` 提升而来；字符串枚举也是一个组件 |
| `x-xihan-stability` | 稳定性不是 `Stable` 的操作 |
| `x-xihan-permission` / `x-xihan-policy` | 契约或端点上的权限码 / 策略名 |
| `summary`、`description`、字段说明 | 消息目录与 XML 文档注释，按区域翻译 |

没有 Schema 的操作只发布路径、方法与 `operationId`。只给 gRPC、MCP 的操作与 `Stability.Internal` 的操作不进文档。

同一份 Schema 的三个载体：

| 载体 | 从 Schema 得到什么 | 入口 |
| --- | --- | --- |
| OpenAPI | 参数、请求体、响应与组件 | `MapXiHanOpenApi()`，`GET /openapi.json` |
| gRPC | protobuf 消息与线格式，`.proto` 由同一份描述生成 | `MapXiHanProto()`，`GET /operations.proto`，见 [gRPC](/protocols/grpc) |
| MCP | tool 的 `inputSchema` / `outputSchema` | `MapXiHanMcp()`，`tools/list`，见 [MCP](/protocols/mcp) |

## 最小示例

1. 引用 `XiHan.Framework.Protocols.Http`（`Profiles.Full` 已包含），`UseFeature<HttpProtocolFeature>()`。
2. 在 `app.UseXiHan()` 与 `app.MapXiHanOperations()` 之后调用 `app.MapXiHanOpenApi()`。

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
app.MapXiHanOperations();
app.MapXiHanOpenApi();      // GET /openapi.json
app.Run();
```

```bash
curl -s localhost:5000/openapi.json -H "Accept-Language: en-US"
```

示例应用的文档里 `/orders` 一节（截取）：

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

`summary` 与 `description` 取自示例应用消息目录里的 `orders.create.title` 与 `orders.create.description`，权限要求那一行由生成器按区域附上。把 `Accept-Language` 换成 `zh-CN`，文案就是中文，响应头 `Content-Language: zh-CN`。

## 文档选项

```csharp
using XiHan.Framework.Protocols.Http.OpenApi;

var openApi = new OpenApiOptions
{
    Title = "Orders API",
    Version = "v1",
    Servers = [new OpenApiServer("https://api.example.com") { Description = "生产" }],
};
app.MapXiHanOpenApi(openApi);
```

| 属性 | 缺省 | 说明 |
| --- | --- | --- |
| `Title` | 宿主的应用名 | `info.title` |
| `Version` | 框架版本 | `info.version`，写应用自己的接口版本 |
| `Servers` | 空，不写 `servers` | `OpenApiServer(Url, Description)` 列表 |
| `Groups` | 空 | 分组，见下文 |

`MapXiHanOpenApi` 的第二个参数 `pattern` 决定文档地址，缺省 `/openapi.json`；分组文档的地址由它派生。

## 权限与认证方案

契约有 `Permission` 的操作带 `x-xihan-permission`（权限码），有 `Policy` 的带 `x-xihan-policy`（策略名），都没有时不写。`components` 下的 `x-xihan-permissions` 按权限码（序数排序）列出权限定义，收录范围是文档里已发布操作引用的权限码与内核 `PermissionRegistry` 登记的全部权限的并集：只在 gRPC、MCP 上暴露的操作或端点级 `RequireXiHanPermission` 用到的权限也在里面，前端据此隐藏无权的入口、生成授权界面；被引用却没登记的只有 `code`。权限码不是 OAuth scope，所以不占用标准的 `security` / `securitySchemes`。

装配了以 `Bearer` 方案工作的认证 Provider（`Security.Jwt`、`Security.OpenIdConnect`）时，文档发布 `components.securitySchemes.bearer`（`type: http`、`scheme: bearer`、`bearerFormat: JWT`）并挂全局 `security: [{ "bearer": [] }]`，没有权限也没有策略要求的操作标 `security: []`。ApiKey 等其他方案不发布。查看器据此显示「授权」按钮，把令牌带进试调请求。权限码与策略的定义见[授权与权限](/security/authorization)。

## 文案、标签与字段说明

文档里面向人的文字都来自消息目录，装配了本地化时按 `Accept-Language` 协商出的区域分别生成、分别缓存，响应带 `Content-Language`，缺省区域是 `en-US`；没有装配本地化时只有一份文档，没有这些文案。键里的 `-` 一律换成 `_`。

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

字段说明的源语言文案是 DTO 的 XML 文档注释：`Kernel.Generators` 把类型与属性的 `<summary>`（位置记录取主构造参数的 `<param>`）折进 Schema 的 `description`，参数、请求体与成功响应据此写说明，成功响应没有根说明时写按区域的「成功」。项目要开启 `GenerateDocumentationFile`。某个区域要换文案时才写 `openapi.schemas.*` 覆盖，Schema 名与属性名都转成 snake_case（`ConstraintRuleDto` → `constraint_rule_dto`，`ruleType` → `rule_type`）。

应用的文案放在 `Localization/<区域>.json`，登记方式见 [ASP.NET Core 宿主](/web/hosting)的「区域协商与本地化」：

```json
{
  "orders.create.title": "下单",
  "orders.create.description": "按名称与金额下单。",
  "permissions.orders.write.name": "下单权限",
  "openapi.tags.orders.name": "订单"
}
```

## 分组

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
    // 交给文档查看器登记：link.Name、link.Title、link.Url
    app.Logger.LogInformation("OpenAPI document {Name}: {Url}", link.Name, link.Url);
}
```

| 规则 | 说明 |
| --- | --- |
| 组名 | 匹配 `^[a-z][a-z0-9-]*$`、互不重复且不能是 `all`（全量文档的名字），违反时构造 `OpenApiOptions` 就抛出 `ArgumentException` |
| 操作入组 | 按归属特性的清单 ID；一个特性只能属于一个组 |
| 普通端点入组 | 按标签；组的标签是组内操作的资源段加额外标签 |
| 全量文档 | 带 `x-tagGroups`，按排序再按名字列出各组，一个标签只归排序靠前的那一组，没进任何组的标签归入最后的「其他」组 |
| 分组文档地址 | `pattern` 去掉 `.json` 后接 `/<组名>.json`，只含本组的操作、端点、组件与引用到的权限码 |

`GetXiHanOpenApiDocuments(options, pattern)` 列出全部文档的名字、缺省区域的标题与地址，全量文档排在最前，参数要与 `MapXiHanOpenApi` 一致。

## 普通端点

宿主里不是操作的最小 API 端点也能进文档。收录条件：有 HTTP 方法元数据，带 `Accepts` 或 `Produces` 描述元数据之一，没有 `ExcludeFromDescription()`。SignalR Hub、探针、重定向、静态资源没有描述元数据，不会进文档。路由参数写成 `string` 类型的路径参数；`WithName` 作 `operationId`，`WithSummary`、`WithDescription`、`WithTags` 照搬；`RequireXiHan*` 的要求写成 `x-xihan-permission` / `x-xihan-policy`。

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

`OrderResponse` 是示例应用里 `orders.create` 的响应类型。

JSON 类型引用不到任何操作的 Schema 时，文档生成以 `PROTOCOLS_VALIDATION_010` 失败；普通端点与操作共用一条路由时以 `PROTOCOLS_CONFLICT_002` 失败。两种失败都在第一次请求文档时暴露，修正办法是补 `WithXiHanOperationSchema`、登记对应的操作，或给端点加 `ExcludeFromDescription()`。端点自身的写法见[端点里使用框架](/web/endpoints)。

## 接入查看器

框架只发布文档，不内置文档界面。Scalar、Swagger UI、Redoc 这类查看器都只需要一个文档地址，把 `/openapi.json`（分组时是 `GetXiHanOpenApiDocuments` 列出的每个地址）交给它们即可。最简单的做法是放一个静态页面，在页面里引用查看器的脚本并指向文档地址，以 Scalar 为例：

```html
<!doctype html>
<html lang="zh-CN">
  <head>
    <meta charset="utf-8" />
    <title>Orders API</title>
  </head>
  <body>
    <script id="api-reference" data-url="/openapi.json"></script>
    <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
  </body>
</html>
```

把页面放在 `wwwroot/docs/index.html`，并在 `UseXiHan()` 之前启用静态文件，页面本身就不经内核管道：

```csharp
app.UseStaticFiles();
app.UseXiHan();
```

Swagger UI 的做法相同，把它的 `url` 配置项指向 `/openapi.json`。用 `Scalar.AspNetCore`、`Swashbuckle.AspNetCore.SwaggerUI` 这类 ASP.NET Core 集成包也可以，它们的登记方式按各自的文档，框架这一侧只提供文档地址与 `GetXiHanOpenApiDocuments` 给出的分组列表。查看器试调时发出的请求与普通请求一样经过内核管道：带 `Authorization` 头才能调用有权限要求的操作，`Accept-Language` 决定错误文案的语言。

生成客户端代码的工具（如 Kiota、NSwag、openapi-generator）直接读 `/openapi.json`；文档里的 `operationId` 是操作 ID，生成的方法名与后端一致。

## 不经宿主导出文档

`OpenApiDocumentSource.FromServices(services, options, endpoints, locale, group)` 从容器组装与 `MapXiHanOpenApi` 相同的输入，`OpenApiDocumentBuilder.Build(source)` 生成 UTF-8 字节，与 `/openapi.json` 逐字节一致。测试与导出工具用这一对入口，不必发 HTTP 请求：

```csharp
using Microsoft.AspNetCore.Routing;
using XiHan.Framework.Protocols.Http.OpenApi;

// 应用已启动（特性装配完成、端点表定型），options 与 MapXiHanOpenApi 用的同一个
var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(static source => source.Endpoints).ToList();
var document = OpenApiDocumentBuilder.Build(OpenApiDocumentSource.FromServices(app.Services, openApi, endpoints, "zh-CN"));
await File.WriteAllBytesAsync("openapi.zh-CN.json", document);
```

| 参数 | 说明 |
| --- | --- |
| `options` | `OpenApiOptions`，为 `null` 时标题取应用名、版本取框架版本 |
| `endpoints` | 宿主的端点表，普通端点与认证方案由此而来 |
| `locale` | 文案的区域；为 `null` 时取本地化的默认区域 |
| `group` | 分组名；为 `null` 时是全量文档，不存在的组名抛出 `ArgumentException` |

把导出的文件提交进仓库并在 CI 里与新生成的文件比对，契约变更就会出现在代码评审的差异里。另一个重载 `OpenApiDocumentBuilder.Build(registry, options, permissions, title, version)` 只看登记表，不含普通端点、认证方案与文案，适合单元测试里核对路由推导。测试里启动应用的写法见[单元与集成测试](/testing/basics)。

## 常见问题

### 文档端点不鉴权，怎样限制访问

文档端点带 `PipelineBypassMetadata`，与[端点里使用框架](/web/endpoints)里绕过管道的端点是同一类：`MapXiHanOpenApi()` 返回 `IEndpointConventionBuilder`，可以用 ASP.NET Core 自己的 `RequireAuthorization` 配合认证方案 `XiHan` 保护，或者在反向代理上只对内网放行。生产环境不需要对外公开文档时，也可以只在开发环境调用 `MapXiHanOpenApi()`。

### 改了消息目录或权限定义，文档没变

文档在第一次请求时生成并缓存，按区域分别缓存，进程不重启不会重新生成。开发时重启应用即可；配置目录热更新改变的是选项，不会让文档缓存失效。

### 怎样让某个操作不出现在文档里

三种办法：`Expose` 里去掉 `Http`（它就不在 HTTP 上发布，自然也不进文档）；`Stability = Stability.Internal`（任何协议都不发布）；或者把它放进一个单独的分组，前端只登记其他组的文档。没有「只隐藏文档、保留端点」的开关，文档与路由始终一致。

### 为什么请求体的属性名是 camelCase，而我的 C# 属性是 PascalCase

Schema 的属性名按序列化上下文的命名策略生成，示例用的是 `JsonKnownNamingPolicy.CamelCase`。文档与实际绑定用的是同一份序列化元数据，所以客户端按文档发请求一定能被绑定；要换命名风格，改 `JsonSourceGenerationOptions`，文档随之变化。

### gRPC 客户端和 MCP 客户端看到的字段会和 OpenAPI 不一致吗

不会。三者读同一份由 `Kernel.Generators` 生成的 Schema：OpenAPI 把它提升成组件，gRPC 由它推出 protobuf 消息（字段名转 lower_snake_case，`json_name` 保持属性名），MCP 原样作为 `inputSchema` / `outputSchema`。改一个 DTO，三处同时变，见 [gRPC](/protocols/grpc)与 [MCP](/protocols/mcp)。

## 相关包与下一步

- 相关包：[Protocols.Http](/packages/protocols-http)、[Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Kernel](/packages/kernel)、[Kernel.Generators](/packages/kernel-generators)
- 下一页：[结果与错误处理](/web/errors)，文档里的 `default` 响应就是它的 Problem Details 形状
- 同一份契约的其他载体：[gRPC](/protocols/grpc)、[MCP](/protocols/mcp)、[命令行宿主](/protocols/command-line)
- 相关章节：[操作契约](/web/operations)、[HTTP 路由与载荷](/web/routing)、[端点里使用框架](/web/endpoints)、[授权与权限](/security/authorization)、[全球化和本地化](/common/localization)
- 规范：HTTP 载体规范的 OpenAPI 一节、RFC-0011《操作契约与 XGI》
