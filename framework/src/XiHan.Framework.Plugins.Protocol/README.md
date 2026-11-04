# XiHan.Framework.Plugins.Protocol

## 关于

`XiHan.Framework.Plugins.Protocol` 插件协议 `xihan.plugin.v1` 的 .NET 类型：本机端点与握手行、版本协商、环境变量名、`PLUGINS_*` 错误码与消息键。进程外宿主、Wasm 宿主与一致性套件共用，应用通常不直接引用。

## 主要特性

* 解析与生成握手行，端点只接受本机回环地址
* 协议版本协商
* `PluginErrors` 常量，便于按错误码区分插件失败

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Plugins.Protocol --prerelease
```

### 使用

写宿主之外的工具（握手行检查、端点解析）或按错误码判断插件失败时才需要直接引用。

```csharp
if (PluginEndpoint.TryParseHandshakeLine("xihan-plugin/1 tcp 127.0.0.1:50123", out var endpoint))
{
    Console.WriteLine($"{endpoint.Network} {endpoint.Address}");
}

static bool IsTransient(Error error)
    => error.Code is PluginErrors.Unavailable or PluginErrors.Crashed;
```

## 主要类型

此库提供的主要类型：

* `PluginEndpoint`：本机 gRPC 端点，解析与生成握手行
* `PluginProtocol`：协议版本、版本协商、元数据键与环境变量名
* `PluginErrors`、`PluginMessages`：错误码与消息键

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/plugins-protocol)

## 相关包

* 进程外插件宿主：[XiHan.Framework.Plugins.OutOfProcess](https://www.nuget.org/packages/XiHan.Framework.Plugins.OutOfProcess)
* Wasm 插件宿主：[XiHan.Framework.Plugins.Wasm](https://www.nuget.org/packages/XiHan.Framework.Plugins.Wasm)

## 反馈与贡献

`XiHan.Framework.Plugins.Protocol` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
