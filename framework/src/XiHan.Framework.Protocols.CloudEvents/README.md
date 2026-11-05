# XiHan.Framework.Protocols.CloudEvents

## 关于

`XiHan.Framework.Protocols.CloudEvents` CloudEvents 1.0 的 HTTP 协议适配器：入站端点把 HTTP 上的事件变成消息信封交给消息子系统，出站 Webhook 发布者把信封推送到别的服务。需要消息队列能力（`UseStandardProfile()` 已提供）。

## 主要特性

* 入站支持 binary、structured、batch 三种内容模式
* 入站可在请求内派发给处理器，也可发布到消息代理
* 出站 Webhook 按主题配置地址，不外推租户、主体等内部标识

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Protocols.CloudEvents --prerelease
```

### 使用

注册特性、映射端点，并在配置里把事件类型映射到主题、给主题配置 Webhook。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<CloudEventsProtocolFeature>());
var app = builder.Build();
app.UseXiHan();
app.MapXiHanCloudEvents();   // POST /events
```

```json
{ "XiHan": { "Protocols": { "CloudEvents": {
  "Topics": { "orders.placed": "orders" },
  "Webhooks": { "orders": "https://hooks.example.com/orders" }
} } } }
```

入站端点框架不加任何权限要求，需要由宿主自己 `RequireXiHanPermission(...)`。

## 主要类型

此库提供的主要类型：

* `CloudEventsProtocolFeature`：特性入口
* `CloudEventsProtocolOptions`：`Inbound`、`Outbound`、`Topics`、`Webhooks`
* `CloudEventsEndpointRouteBuilderExtensions`：`MapXiHanCloudEvents`
* `CloudEventsWebhookPublisher`：出站 Webhook 发布者

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/protocols-cloudevents)

## 相关包

* HTTP 协议适配器：[XiHan.Framework.Protocols.Http](https://www.nuget.org/packages/XiHan.Framework.Protocols.Http)
* gRPC 协议适配器：[XiHan.Framework.Protocols.Grpc](https://www.nuget.org/packages/XiHan.Framework.Protocols.Grpc)
* MCP 协议适配器：[XiHan.Framework.Protocols.Mcp](https://www.nuget.org/packages/XiHan.Framework.Protocols.Mcp)

## 反馈与贡献

`XiHan.Framework.Protocols.CloudEvents` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
