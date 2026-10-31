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

端点元数据带着操作描述与载荷绑定委托，`UseXiHan()` 在进管道前绑定载荷，Authorize 阶段按契约判定，Validate 阶段按 Schema 校验。成功时 `create` 返回 201、其余 200；失败由宿主写成 Problem Details。

`MapXiHanOpenApi()` 在第一次请求时从登记表、`Kernel.Generators` 生成的 Schema、权限码注册表、已装配的认证处理器与宿主的普通端点生成 OpenAPI 3.1 并缓存，带 `x-xihan-permission` / `x-xihan-policy` 扩展与权限定义组件（ADR-0031），按区域给出操作文案、标签与字段说明，支持分组文档（ADR-0052）。

## 何时使用

操作契约要给浏览器、前端或第三方经 HTTP 调用时用它；同一份契约的 gRPC、MCP 暴露见 [Protocols.Grpc](./protocols-grpc)、[Protocols.Mcp](./protocols-mcp)。不是操作的普通最小 API 端点不需要本包也能运行，但要进 OpenAPI 文档时由本包收录。本包不提供 HTTP 客户端，调用其他服务用 `IHttpClientFactory`。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Protocols.Http" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Full` 已包含本包，`UseFullProfile()` 会装配 `HttpProtocolFeature`。

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
| `Routing.OperationEndpointRouteBuilderExtensions` | `MapXiHanOperations()`，返回映射的端点数 |
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

路由推导、绑定、文案键与普通端点收录的完整说明见 [HTTP 与 OpenAPI](../guide/http)。

## 行为与限制

- `MapXiHanOperations()` 必须在 `UseXiHan()` 之后；端点内取不到调用上下文或载荷时抛出 `FrameworkException`（`KERNEL_HOSTING_004`）。
- 只有 `POST`、`PUT`、`PATCH` 读请求体，请求体必须是 JSON 对象；路由值优先于请求体，请求体优先于查询串。
- OpenAPI 文档端点绕过内核管道、不鉴权；文档按 `(分组, 区域)` 各缓存一份，装配了本地化时按 `Accept-Language` 协商区域。
- 普通端点的 JSON 类型只引用已登记操作的 Schema，不做第二套推导。

| 情形 | 何时 | 错误码 |
| --- | --- | --- |
| 两个操作推导出同一条路由 | `MapXiHanOperations()` | `PROTOCOLS_CONFLICT_002` |
| 普通端点与操作共用一条路由 | 第一次请求文档 | `PROTOCOLS_CONFLICT_002` |
| 普通端点的 JSON 类型引用不到操作的 Schema | 第一次请求文档 | `PROTOCOLS_VALIDATION_010` |
| 请求体不是 JSON 对象 | 绑定 | `KERNEL_VALIDATION_001`（400） |
| 前缀、路由文本或操作 ID 不合法 | 读取配置 | `ArgumentException` |

## 相关

- [HTTP 与 OpenAPI](../guide/http)、[操作契约](../guide/operations)、[宿主与 Web 应用](../guide/web)
- [Hosting.AspNetCore](./hosting-aspnetcore)、[Kernel.Generators](./kernel-generators)
- [Protocols.Grpc](./protocols-grpc)、[Protocols.Mcp](./protocols-mcp)、[Protocols.CloudEvents](./protocols-cloudevents)
- 规范：`spec/protocol/http.md`《HTTP 载体》、`spec/conventions/operation-ids.md`《操作 ID》、RFC-0011《操作契约与 XGI》
