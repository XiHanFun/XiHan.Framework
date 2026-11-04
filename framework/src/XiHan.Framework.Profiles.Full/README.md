# XiHan.Framework.Profiles.Full

## 关于

`XiHan.Framework.Profiles.Full` Full 组合包：在 Standard 之上加上对象存储、调度、审计、工作流的默认实现，AI 工具目录，OpenTelemetry 导出与运行信息，以及 HTTP、gRPC、MCP、CloudEvents 四种协议适配器。一份操作契约同时经多种协议服务，零外部基础设施即可运行，适合中后台与 SaaS 服务。

## 主要特性

* `UseFullProfile()` 一次装配以上全部能力
* 默认实现都可被显式 Provider 顶替
* 协议适配器只登记能力，端点仍要在宿主上映射

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Profiles.Full --prerelease
```

### 使用

装配组合并映射各协议端点。

```csharp
builder.AddXiHan(xihan => xihan
    .UseFullProfile()
    .UseFeature<OrdersFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.MapXiHanOperations();   // HTTP
app.MapXiHanOpenApi();      // /openapi.json
app.MapXiHanGrpc();         // gRPC，需要 Kestrel 的 HTTP/2 端点
app.MapXiHanMcp();          // /mcp
app.MapXiHanCloudEvents();  // /events
```

## 主要类型

此库提供的主要类型：

* `FullProfile`：静态类，扩展方法 `UseFullProfile`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/profiles-full)

## 相关包

* 标准组合：[XiHan.Framework.Profiles.Standard](https://www.nuget.org/packages/XiHan.Framework.Profiles.Standard)
* 最小组合：[XiHan.Framework.Profiles.Minimal](https://www.nuget.org/packages/XiHan.Framework.Profiles.Minimal)

## 反馈与贡献

`XiHan.Framework.Profiles.Full` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
