# XiHan.Framework.Protocols.CloudEvents

## 关于

`XiHan.Framework.Protocols.CloudEvents` CloudEvents 1.0 的 HTTP 协议适配器：入站端点把 HTTP 上的事件变成消息信封交给消息子系统，出站 Webhook 发布者把信封推送到别的服务。需要消息队列能力（`UseStandardProfile()` 已提供）。

## 主要特性

* 入站支持 binary、structured、batch 三种内容模式
* binary 的 `ce-*` 头值照 CloudEvents HTTP 绑定 1.0.2 §3.1.3.2 严格读：先解开双引号括起的部分（`"order 42"` 读成 `order 42`，`\"` 是引号本身），再做一轮百分号解码；引号不配对、引号里有控制字符、`%` 后不是两位十六进制数字、解出的字节不是合法的 UTF-8 时以 400 `MESSAGING_VALIDATION_002` 拒绝（`CloudEventsHttpBinding.DecodeHeaderValue` 返回 `Maybe<string>`，读不出时无值）
* 出站 binary 的头值把空格、双引号、`%` 与可打印 ASCII 之外的字符都百分号编码（`EncodeHeaderValue`），写出的值不含空格与双引号
* binary 的空 `ce-dataschema`、`ce-subject`、`ce-time` 头与 structured 的空串一样不合法（400 `MESSAGING_VALIDATION_002`）；插件协议的 `data_schema`、`subject`、`time` 空串仍表示没有
* 入站可在请求内派发给处理器，也可发布到消息代理
* 出站 Webhook 按主题配置地址，不外推租户、主体等内部标识
* 出站写不出的信封在发出之前拒绝：信封的字符串属性有孤立的代理项时，binary（缺省）与 structured 一样抛出 `MESSAGING_VALIDATION_002`，不编成 U+FFFD 照发
* binary 模式声明为 JSON 的数据与 structured 同一套检查：重复的成员、嵌进结构化文档之后超过 64 层、不是 JSON 或字符串不是合法的 Unicode，出站在发出之前抛出 `MESSAGING_VALIDATION_002`，入站以 400 `MESSAGING_VALIDATION_002` 拒绝，`reason` 是数据在结构化文档里的位置

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
