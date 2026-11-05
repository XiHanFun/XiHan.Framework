# XiHan.Framework.Protocols.Http

## 关于

`XiHan.Framework.Protocols.Http` HTTP 协议适配器：把暴露给 HTTP 的操作映射为 ASP.NET Core 端点，路由按操作 ID 推导，失败写成 Problem Details，并从操作契约生成 OpenAPI 3.1 文档。

## 主要特性

* `orders.create` → `POST /orders`、`orders.get` → `GET /orders/{id}` 等路由推导，可显式覆盖
* 路由值、请求体与查询串合并成一个请求载荷
* `/openapi.json`：带权限与策略扩展、按区域本地化、支持分组文档

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Protocols.Http --prerelease
```

### 使用

注册特性并映射端点。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>());
var app = builder.Build();
app.UseXiHan();
app.MapXiHanOperations();
app.MapXiHanOpenApi();   // /openapi.json
```

可选配置路由前缀与单个操作的路由：

```json
{ "XiHan": { "Protocols": { "Http": {
  "RoutePrefix": "/api",
  "Routes": { "orders.export": "POST /orders:export" }
} } } }
```

## 主要类型

此库提供的主要类型：

* `HttpProtocolFeature`：特性入口
* `HttpProtocolOptions`：`RoutePrefix`、`Routes`
* `OperationEndpointRouteBuilderExtensions`：`MapXiHanOperations`
* `OpenApiEndpointRouteBuilderExtensions`、`OpenApiOptions`：`MapXiHanOpenApi` 与文档标题、版本、分组

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/protocols-http)

## 相关包

* gRPC 协议适配器：[XiHan.Framework.Protocols.Grpc](https://www.nuget.org/packages/XiHan.Framework.Protocols.Grpc)
* MCP 协议适配器：[XiHan.Framework.Protocols.Mcp](https://www.nuget.org/packages/XiHan.Framework.Protocols.Mcp)
* CloudEvents 协议适配器：[XiHan.Framework.Protocols.CloudEvents](https://www.nuget.org/packages/XiHan.Framework.Protocols.CloudEvents)

## 反馈与贡献

`XiHan.Framework.Protocols.Http` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
