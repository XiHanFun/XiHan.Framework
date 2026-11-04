# XiHan.Framework.Plugins.OutOfProcess

## 关于

`XiHan.Framework.Plugins.OutOfProcess` 进程外插件宿主：插件是任意语言写的子进程，只需 protobuf、gRPC 与 JSON，经 `xihan.plugin.v1` 协议与宿主通信。插件声明的操作登记为代理操作，与本地操作一样经完整管道。

## 主要特性

* 任意语言写插件，独立进程与独立依赖
* 崩溃按退避自动重启，每个插件是一个健康条目
* 插件可回调宿主的操作调用与消息发布，需在清单声明并授予权限

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Plugins.OutOfProcess --prerelease
```

### 使用

注册特性，在配置里列出插件目录与授予的权限。

```csharp
builder.AddXiHan(x => x
    .UseStandardProfile()
    .UseFeature<OutOfProcessPluginsFeature>());
```

```json
{
  "XiHan": { "Plugins": { "OutOfProcess": {
    "Directories": ["plugins/echo"],
    "Grants": { "xihan.samples.echo": ["capability:kernel.pipeline"] }
  } } }
}
```

## 主要类型

此库提供的主要类型：

* `OutOfProcessPluginsFeature`：特性入口
* `OutOfProcessPluginHost`：实现 `IPluginHost`，`Describe` 返回诊断快照
* `OutOfProcessPluginOptions`：目录、授予、超时、重启策略

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/plugins-outofprocess)

## 相关包

* 协议类型：[XiHan.Framework.Plugins.Protocol](https://www.nuget.org/packages/XiHan.Framework.Plugins.Protocol)
* Wasm 插件宿主：[XiHan.Framework.Plugins.Wasm](https://www.nuget.org/packages/XiHan.Framework.Plugins.Wasm)
* 进程内插件宿主：[XiHan.Framework.Plugins.InProcess](https://www.nuget.org/packages/XiHan.Framework.Plugins.InProcess)
* 一致性套件：[XiHan.Framework.Plugins.Conformance](https://www.nuget.org/packages/XiHan.Framework.Plugins.Conformance)

## 反馈与贡献

`XiHan.Framework.Plugins.OutOfProcess` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
