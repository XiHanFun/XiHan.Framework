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

端点跟随操作登记表（ADR-0251）：`MapXiHanOperations()` 映射的是一个随登记表变化的端点集合，插件运行期加载的操作登记之后立即有路由，撤回之后随即没有，返回值上的约定作用于此后出现的端点。映射时已登记的两个操作推导出同一条路由，启动失败；此后登记的一组与已有的或组内的冲突，整组不登记，登记返回 `PROTOCOLS_CONFLICT_002`。

`MapXiHanOpenApi()` 按需从登记表、`Kernel.Generators` 生成的 Schema、权限码注册表、已装配的认证处理器与宿主的普通端点生成 OpenAPI 3.1 并缓存，登记表或分组来源变了即重新生成。文档带 `x-xihan-permission` / `x-xihan-policy` 扩展与权限定义组件（ADR-0031），按区域给出操作文案、标签与字段说明，支持分组文档（ADR-0052）；运行期才有的归属由应用登记的 `IOpenApiGroupSource` 分组（ADR-0251）。

## 何时使用

操作契约要给浏览器、前端或第三方经 HTTP 调用时用它；同一份契约的 gRPC、MCP 暴露见 [Protocols.Grpc](./protocols-grpc)、[Protocols.Mcp](./protocols-mcp)。不是操作的普通最小 API 端点不需要本包也能运行，但要进 OpenAPI 文档时由本包收录。本包不提供 HTTP 客户端，调用其他服务用 `IHttpClientFactory`。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Protocols.Http --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[环境与安装](/getting-started/installation)。`Profiles.Full` 已包含本包，`UseFullProfile()` 会装配 `HttpProtocolFeature`。

```csharp
// using XiHan.Framework.Protocols.Http; .Routing; .OpenApi;
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<HttpProtocolFeature>());

// app.UseXiHan() 之后
app.MapXiHanOperations();
app.MapXiHanOpenApi();
```

完整的 `Program.cs` 见[第一个应用](/getting-started/first-app)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Protocols:Http:RoutePrefix` | string | 空 | 全部操作路由的前缀，以 `/` 开头、不以 `/` 结尾 |
| `XiHan:Protocols:Http:Routes:<操作 ID>` | string | — | `METHOD /template` 显式覆盖；方法限 `GET`、`POST`、`PUT`、`PATCH`、`DELETE` |
| `XiHan:Protocols:Http:MaxRequestBodyBytes` | long，大于 0 | 4194304（4 MiB） | 操作端点请求体的上限，超过即 400 `PROTOCOLS_VALIDATION_011`；只管操作端点，应用自己映射的端点照 Kestrel 的限制 |

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
| `HttpProtocolOptions` | `RoutePrefix`、`Routes`、`MaxRequestBodyBytes`；`RouteFor(descriptor)` 给出最终路由；`FromConfiguration` |
| `Routing.OperationEndpointRouteBuilderExtensions` | `MapXiHanOperations()`，返回这组操作端点的约定构建器（`IEndpointConventionBuilder`），加在上面的约定作用于每个操作端点 |
| `Routing.HttpRouteConvention` | 推导规则：`Derive`、`HasIdentifier`、`HasNestedFields`、`SuccessStatusCode`；常量 `IdentifierProperty` = `id` |
| `Routing.HttpRoute` | 方法与模板；`Parse("POST /orders:export")` |
| `Routing.OperationRequestBinder` | `BindAsync`：路由值、请求体、查询串合并成载荷 |
| `OpenApi.OpenApiEndpointRouteBuilderExtensions` | `MapXiHanOpenApi(options, pattern)`、`GetXiHanOpenApiDocuments(options, pattern)`、`GroupPattern`；常量 `DefaultPattern` = `/openapi.json`、`FullDocumentName` = `all` |
| `OpenApi.OpenApiOptions`、`OpenApi.OpenApiServer`、`OpenApi.OpenApiGroup` | 文档标题、版本、`servers` 与分组 |
| `OpenApi.IOpenApiGroupSource` | 运行期的分组：当前的分组与变更令牌，应用在容器里登记，给插件这类运行期才有的归属分组（ADR-0251） |
| `OpenApi.OpenApiDocumentLink` | 一份文档的名字、标题与地址，给文档界面登记 |
| `OpenApi.OpenApiEndpointConventionBuilderExtensions` | `WithXiHanOperationSchema(request, response)`：普通端点声明引用哪个操作的 Schema |
| `OpenApi.OpenApiDocumentSource`、`OpenApi.OpenApiDocumentBuilder` | 不经宿主生成文档：`FromServices(...)` 组装输入，`Build(source)` 生成字节 |
| `HttpErrors` | `PROTOCOLS_CONFLICT_002`、`PROTOCOLS_VALIDATION_010`、`PROTOCOLS_VALIDATION_011`、`PROTOCOLS_VALIDATION_012` |
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

路由推导与绑定的完整说明见 [HTTP 路由与载荷](/web/routing)；文档选项、分组、文案键与普通端点收录的写法见[规范化接口文档](/web/openapi)。

## 行为与限制

- `MapXiHanOperations()` 必须在 `UseXiHan()` 之后；端点内取不到调用上下文或载荷时抛出 `FrameworkException`（`KERNEL_HOSTING_004`）。
- 只有 `POST`、`PUT`、`PATCH` 读请求体，请求体必须是 JSON 对象；路由值优先于请求体，请求体优先于查询串。
- OpenAPI 文档端点绕过内核管道、不鉴权；文档按 `(分组, 区域)` 各缓存一份，装配了本地化时按 `Accept-Language` 协商区域。
- 普通端点的 JSON 类型只引用已登记操作的 Schema，不做第二套推导。
- 路由推导与载荷绑定的规则（实例级与集合级、嵌套对象改用 POST、只读的自定义动词走 GET、路由值与查询串按属性类型写成 JSON）见 [HTTP 路由与载荷](/web/routing)。是否含 `id`、是否有嵌套对象看请求的序列化元数据，没有类型化元数据的插件代理看请求 Schema 的 `properties`。
- 响应：幂等回放时端点不执行，由 `UseXiHan()` 用挂在端点元数据上的 `OperationResponseWriter` 写出首次的状态码与正文（ADR-0064）；失败写成 RFC 9457 Problem Details，见 [ASP.NET Core 宿主](/web/hosting#problem-details)。授权在 Authorize 阶段按契约的权限或策略判定（描述经端点元数据进入上下文）。
- OpenAPI：文档的输入、认证方案（ADR-0032）、权限扩展（ADR-0031）、按区域的文案与分组（ADR-0052）、普通端点的收录规则与不经宿主的导出入口（ADR-0034），都在[规范化接口文档](/web/openapi)里。

| 情形 | 何时 | 错误码 |
| --- | --- | --- |
| 两个操作推导出同一条路由 | `MapXiHanOperations()` | `PROTOCOLS_CONFLICT_002` |
| 普通端点与操作共用一条路由 | 第一次请求文档 | `PROTOCOLS_CONFLICT_002` |
| 普通端点的 JSON 类型引用不到操作的 Schema | 第一次请求文档 | `PROTOCOLS_VALIDATION_010` |
| 请求体超过 `MaxRequestBodyBytes` | 绑定 | `PROTOCOLS_VALIDATION_011`（400） |
| 有请求体而 `Content-Type` 不是 JSON 类型 | 绑定 | `PROTOCOLS_VALIDATION_012`（400） |
| 请求体不是 JSON 对象 | 绑定 | `KERNEL_VALIDATION_001`（400） |
| 前缀、路由文本或操作 ID 不合法 | 读取配置 | `ArgumentException` |

## 相关

- [HTTP 路由与载荷](/web/routing)、[操作契约](/web/operations)、[ASP.NET Core 宿主](/web/hosting)
- [Hosting.AspNetCore](./hosting-aspnetcore)、[Kernel.Generators](./kernel-generators)
- [Protocols.Grpc](./protocols-grpc)、[Protocols.Mcp](./protocols-mcp)、[Protocols.CloudEvents](./protocols-cloudevents)
- 规范：`spec/protocol/http.md`《HTTP 载体》、`spec/conventions/operation-ids.md`《操作 ID》、RFC-0011《操作契约与 XGI》
