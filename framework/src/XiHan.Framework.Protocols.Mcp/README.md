# XiHan.Framework.Protocols.Mcp

## 关于

`XiHan.Framework.Protocols.Mcp` MCP（Model Context Protocol）协议适配器：把标记为 `Expose.Mcp` 的操作发布为 MCP tool，JSON-RPC 2.0，Streamable HTTP 传输，命令行宿主里也可经 stdio 提供。AI 客户端据此直接调用你的业务操作。

## 主要特性

* `tools/list` 的输入输出 Schema 取自操作契约，说明文案按区域翻译
* `tools/call` 经完整内核管道，认证、授权、事务照常生效
* HTTP 与 stdio 两种传输

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Protocols.Mcp --prerelease
```

### 使用

注册特性并映射端点。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<McpProtocolFeature>());
var app = builder.Build();
app.UseXiHan();
app.MapXiHanMcp();   // POST /mcp
```

命令行宿主里 `builder.Services.AddXiHanCommandLine()` 后运行 `app mcp`，MCP 客户端以子进程方式连接。可选配置在 `XiHan:Protocols:Mcp`：`ServerName`、`ServerVersion`、`Instructions`、`Tools:<操作 ID>`。

## 主要类型

此库提供的主要类型：

* `McpProtocolFeature`：特性入口
* `McpProtocolOptions`：服务名、版本、说明与 tool 名覆盖
* `McpStdioCommand`、`McpStdioTransport`：stdio 传输

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/protocols-mcp)

## 相关包

* HTTP 协议适配器：[XiHan.Framework.Protocols.Http](https://www.nuget.org/packages/XiHan.Framework.Protocols.Http)
* gRPC 协议适配器：[XiHan.Framework.Protocols.Grpc](https://www.nuget.org/packages/XiHan.Framework.Protocols.Grpc)
* CloudEvents 协议适配器：[XiHan.Framework.Protocols.CloudEvents](https://www.nuget.org/packages/XiHan.Framework.Protocols.CloudEvents)

## 反馈与贡献

`XiHan.Framework.Protocols.Mcp` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
