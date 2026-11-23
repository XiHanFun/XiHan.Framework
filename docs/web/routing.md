# HTTP 路由与载荷

这页讲 HTTP 协议适配器 `Protocols.Http` 怎样把操作契约发布成 REST 端点：路由由操作 ID 与请求形状推导、怎样覆盖与加前缀、一次请求的载荷从路由值、请求体与查询串怎样合并、成功与失败各写什么状态码。读完这页，我们能预判任何一个操作在 HTTP 上的路由，并且看懂一条 Problem Details 响应。

前置知识：[操作契约](/web/operations)与 [ASP.NET Core 宿主](/web/hosting)。从同一份契约生成 OpenAPI 文档放在[规范化接口文档](/web/openapi)。

## 为什么需要

操作契约与协议无关，可 HTTP 客户端要的是具体的方法、路径、状态码与 JSON 形状。如果这些由每个开发者手写，同一个团队里就会出现 `POST /order/add` 与 `POST /orders` 并存、404 与 200 加错误字段并存的局面，前端得为每个接口单独适配。

`Protocols.Http` 用一条推导规则回答这些问题：`orders.create` 就是 `POST /orders` 返回 201，`orders.get` 就是 `GET /orders/{id}`，失败一律是 RFC 9457 Problem Details，状态码由错误码的 KIND 段决定。规则是公开的，有例外时用配置覆盖，而不是在代码里另写一套。

## 适配器做什么

`Protocols.Http` 提供能力 `protocols.http@1`，入口是 `HttpProtocolFeature`。`MapXiHanOperations()` 遍历操作登记表，把 `Expose` 含 `Http`、稳定性不是 `Stability.Internal` 的每个操作映射成一个 ASP.NET Core 端点，路由由操作 ID 推导。端点元数据带着操作描述与载荷绑定委托：`UseXiHan()` 在进入内核管道前把描述放进调用上下文，Authorize 阶段按契约的权限或策略判定，Validate 阶段才读请求体、绑定成载荷，再按请求 Schema 校验并解码，端点内只剩执行。被限流、没有认证或没有权限的请求不会让宿主读请求体。失败结果留在上下文里，由 `UseXiHan()` 写成 Problem Details。

| 环节 | 在哪里发生 | 做什么 |
| --- | --- | --- |
| 路由推导 | `MapXiHanOperations()`，启动时 | 操作 ID 加请求形状 → 方法与模板；两个操作撞路由则启动失败 |
| 授权 | 内核管道，Authorize 阶段 | 按契约的 `Permission` / `Policy` 判定 |
| 载荷绑定、校验、事务 | 内核管道，Validate 阶段起 | 读请求体（有上限、必须是 JSON），与路由值、查询串合并成一个 JSON 对象，按 Schema 校验并解码 |
| 执行与写响应 | Handle 阶段 | `HandleAsync`；成功写响应 JSON，失败留在上下文由宿主写 Problem Details |

## 最小示例

1. 引用 `XiHan.Framework.Protocols.Http`（`Profiles.Full` 已包含）。
2. `UseFeature<HttpProtocolFeature>()`。
3. 在 `app.UseXiHan()` 之后调用 `app.MapXiHanOperations()`。

```bash
dotnet add package XiHan.Framework.Protocols.Http --prerelease
```

这三步就是[第一个应用](/getting-started/first-app)的 `Program.cs`，这里把组合包换成 Standard，业务特性换成 `OrdersFeature`：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<OrdersFeature>());

// builder.Build() 与 app.UseXiHan() 之后
app.MapXiHanOperations();   // orders.create → POST /orders，orders.list → GET /orders
```

`OrdersFeature` 是示例应用的订单能力包，登记了 `orders.create` 与 `orders.list` 两个操作（见[操作契约](/web/operations)）。`MapXiHanOperations()` 返回这组操作端点的约定构建器，要给全部操作端点统一加 ASP.NET Core 约定（CORS、限流、端点过滤器等）就写在返回值上，如 `app.MapXiHanOperations().RequireCors("web")`，路由模板不受影响。运行后：

```bash
curl -s -X POST localhost:5000/orders -H "Authorization: ApiKey dev-alice-key" \
  -H "Content-Type: application/json" -d '{"name":"widget","amount":3}'
curl -s localhost:5000/orders -H "Authorization: ApiKey dev-alice-key"
```

第一条返回 201 与 `{"id":"...","name":"widget","amount":3,"createdAt":"..."}`，第二条返回 200 与 `{"items":[...]}`。响应的属性名是 camelCase，因为示例的序列化上下文声明了 `JsonKnownNamingPolicy.CamelCase`。

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

- 实例级与集合级：带 `id` 的请求是对某个实例的动作，路径带 `/{id}` 段；不带的落在集合或单例资源上，如 `GET /profile`、`POST /orders:export`。`create` 与 `list` 恒为集合级。
- GET 与 DELETE 不读请求体：`get`、`list`、`delete` 的请求有嵌套对象（分页请求的 `page`、复合筛选）时进不了查询串，改用 POST 自定义动词。
- 只读的自定义动词：`[Operation(..., ReadOnly = true)]` 声明的非标准动词（`departments.tree`、`users.list-online`）与 `get` 同规则，只有标量字段时走 GET。只读必须显式声明，不按动词前缀猜。

| 操作 | 请求 | 路由 |
| --- | --- | --- |
| `orders.get` | `{ id }` | `GET /orders/{id}` |
| `profile.get` | 没有 `id` | `GET /profile` |
| `orders.cancel` | `{ id, reason }` | `POST /orders/{id}:cancel` |
| `orders.export` | 没有 `id` | `POST /orders:export` |
| `orders.list` | `{ page: { index, size }, keyword }` | `POST /orders:list` |
| `departments.tree`，`ReadOnly = true` | 没有 `id`，只有标量 | `GET /departments:tree` |

两个操作推导出同一条路由（方法与模板都相同，如没有 `id` 的 `things.get` 与 `things.list` 都是 `GET /things`）时，`MapXiHanOperations()` 抛出 `FrameworkException`，错误码 `PROTOCOLS_CONFLICT_002`，应用启动失败。用下一节的覆盖给其中一个换路由。

推导规则是公开的：静态方法 `HttpRouteConvention.Derive(descriptor)` 给出某个操作的 `HttpRoute`（方法与模板），`HttpProtocolOptions` 的实例方法 `RouteFor(descriptor)` 在此之上套用这份选项的覆盖与前缀。测试里核对路由时用它们，不必启动宿主。

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

覆盖优先于推导，前缀在两者之后加上：上例的 `orders.export` 最终是 `POST /api/orders:export`，`orders.get` 是 `GET /api/orders/{id}`。模板用 ASP.NET Core 路由语法，`{name}` 参数绑定到请求对象的同名属性。前缀或路由文本不合法、操作 ID 不合法时，读取配置就抛出 `ArgumentException`，应用启动失败。这一节的其余键见[包参考](/packages/protocols-http#配置)，配置节的绑定方式见[配置模型与选项](/configuration/options)。

## 载荷绑定

一次请求的载荷是一个 JSON 对象，由三处合并而成，同名时前者优先：

1. 路由值：URL 里的标识是权威，请求体里的同名字段被它覆盖。
2. JSON 请求体：只有 `POST`、`PUT`、`PATCH` 读请求体，必须是 JSON 对象；空请求体按没有处理。有请求体时 `Content-Type` 必须是 `application/json` 或 `+json` 结尾的类型，否则 400 `PROTOCOLS_VALIDATION_012`；超过 `XiHan:Protocols:Http:MaxRequestBodyBytes`（缺省 4 MiB）是 400 `PROTOCOLS_VALIDATION_011`。
3. 查询串。

路由值与查询串都是字符串，绑定器按请求类型序列化元数据里的属性类型写成 JSON：布尔、整数、浮点与数值枚举写成字面量，集合属性写成数组（单个值也写成一元数组），其余写字符串；属性名不区分大小写匹配并改写成序列化名。找不到属性或没有类型化元数据（插件代理）时按字符串写，多值写成字符串数组。解析不了的值（如布尔属性收到 `maybe`）照样按字符串写，由 Validate 阶段报出：Schema 校验在解码之前，登记了请求 Schema 时是 `KERNEL_VALIDATION_003`，没有 Schema 时由反序列化报 `KERNEL_VALIDATION_001`。

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace Shop.Orders;

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

交给操作的载荷是 `{"id":"0f8fad5b-d9cb-469f-a165-70867728950e","reason":"late"}`：请求体里的 `id` 被路由值覆盖。请求类型与响应类型要登记进源生成的序列化上下文；绑定按 JSON 约定匹配属性，上下文建议用 `PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase`、`PropertyNameCaseInsensitive = true` 与 `NumberHandling = JsonNumberHandling.AllowReadingFromString`，写法见 [JSON 序列化与 Native AOT](/common/serialization#登记-jsonserializercontext)。

绑定与校验都在 Validate 阶段：请求体不是 JSON 或不是 JSON 对象是 `KERNEL_VALIDATION_001`，之后的 Schema 校验（`KERNEL_VALIDATION_003`）、解码与自校验（`KERNEL_VALIDATION_004`）见[数据校验](/web/validation#两步校验)。没有登记 Schema 的操作不做 Schema 校验，解码失败仍是 `KERNEL_VALIDATION_001`。字段级 `errors` 的形状见 [ASP.NET Core 宿主](/web/hosting)的「Problem Details」，校验规则见[数据校验](/web/validation)。

## 状态码与错误映射

成功时 `create` 返回 201，其余动词返回 200，正文是响应类型的 JSON，`Content-Type: application/json; charset=utf-8`。失败时写 RFC 9457 Problem Details，状态码由错误码的 KIND 段决定（`ProblemResponse.StatusCodeFor`），对照表见[结果与错误处理](/web/errors#错误码)。`DENIED` 是 403，只有 `SECURITY_DENIED_001`（未认证）与 `SECURITY_DENIED_003`（凭据无效、过期或签名不符）是 401。

401 与 403 的分界按 RFC 6750：凭据无效是认证失败，客户端与网关据 401 刷新令牌或重新登录，据 403 判定越权。每个 401 都带 `WWW-Authenticate` 质询：

- 请求出示了凭据但无效：只质询被拒的那个方案，Bearer 写成 `Bearer error="invalid_token"`，其他方案只写方案名（如 `ApiKey`）。
- 请求没有凭据：按已装配认证处理器的方案逐个质询，不带错误码；一个处理器都没有时不写。

示例应用里 `bob` 只有 `orders.read`，下单得到 403：

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

不带 `Authorization` 头时同一请求得到 401 `SECURITY_DENIED_001`，响应头 `WWW-Authenticate: ApiKey`。访问日志等需要状态码的地方用框架写响应时的同一映射：失败按 `ProblemResponse.StatusCodeFor(error)`，成功按 `HttpRouteConvention.SuccessStatusCode(contract)`。错误码的文法与 `Error` 的构造见[结果与错误处理](/web/errors)。

## HTTP 客户端

框架不提供 HTTP 客户端封装。调用其他服务用 ASP.NET Core 的 `IHttpClientFactory`（`AddHttpClient`），超时、重试与熔断按 .NET 的方式配置。框架在出站方向只管一件事：目标地址是否允许访问，用来挡住访问内网与元数据地址的请求，见[出站地址策略](/security/outbound)。服务之间的事件推送见 [CloudEvents](/protocols/cloudevents)。

## 常见问题

### 我想要 `GET /orders/search?keyword=...`，推导出来却是 `POST /orders:search`

两种办法。一是让请求只有标量字段并声明只读：`[Operation("orders.search", ReadOnly = true)]`，请求 `{ keyword, page, size }` 没有嵌套对象，推导结果是 `GET /orders:search`。二是用配置覆盖 `Routes:orders.search` 写成 `GET /orders/search`。注意 GET 不读请求体，所有字段都要能放进查询串。

### 怎样给全部接口加 `/api` 前缀，又保留探针与 OpenAPI 的路径

`RoutePrefix` 只作用于 `MapXiHanOperations()` 映射的操作端点，`MapXiHanHealth()`、`MapXiHanOpenApi()` 与应用自己的最小 API 端点不受影响，所以把 `XiHan:Protocols:Http:RoutePrefix` 设成 `/api` 即可。要让 OpenAPI 文档也换路径，给 `MapXiHanOpenApi` 传 `pattern` 参数，见[规范化接口文档](/web/openapi)。

## 相关包与下一步

- 相关包：[Protocols.Http](/packages/protocols-http)、[Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Kernel](/packages/kernel)、[Kernel.Generators](/packages/kernel-generators)
- 下一步：[端点里使用框架](/web/endpoints)，在不是操作的最小 API 端点里读调用上下文、返回框架错误、挂授权要求
- 同一份契约的其他载体：[规范化接口文档](/web/openapi)、[gRPC](/protocols/grpc)、[MCP](/protocols/mcp)
- 相关章节：[数据校验](/web/validation)、[授权与权限](/security/authorization)、[结果与错误处理](/web/errors)
- 规范：HTTP 载体规范、`conventions/operation-ids.md`《操作 ID》、RFC-0011《操作契约与 XGI》
