# XiHan.Framework.Plugins.Wasm

## 关于

`XiHan.Framework.Plugins.Wasm` Wasm 插件宿主（实验性）：插件是 WASI preview 1 的 reactor 模块，宿主用 wasmtime 在进程内沙箱里运行，限制内存、栈与 CPU 时间，没有网络，文件系统只有映射的目录。适合不受信任的插件，也是唯一支持插件贡献管道中间件的载体。

## 主要特性

* 沙箱隔离：内存与栈上限、按时限强制中断
* 插件可贡献管道中间件，运行期可加载与卸载
* 陷阱或超时后自动退避重建实例

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Plugins.Wasm --prerelease
```

### 使用

注册特性并配置插件目录。Windows 上宿主可执行项目要关闭 CET 兼容，否则 wasmtime 的陷阱会让进程退出。

```csharp
builder.AddXiHan(x => x
    .UseStandardProfile()
    .UseFeature<WasmPluginsFeature>());
```

```xml
<CETCompat>false</CETCompat>
```

```json
{ "XiHan": { "Plugins": { "Wasm": { "Directories": ["plugins/echo-wasm"] } } } }
```

## 主要类型

此库提供的主要类型：

* `WasmPluginsFeature`：特性入口
* `WasmPluginHost`：实现 `IPluginHost`，`Describe` 返回诊断快照
* `WasmPluginOptions`：目录、授予、挂载、资源上限与超时、重启策略

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/plugins-wasm)

## 相关包

* 协议类型：[XiHan.Framework.Plugins.Protocol](https://www.nuget.org/packages/XiHan.Framework.Plugins.Protocol)
* 进程外插件宿主：[XiHan.Framework.Plugins.OutOfProcess](https://www.nuget.org/packages/XiHan.Framework.Plugins.OutOfProcess)
* 一致性套件：[XiHan.Framework.Plugins.Conformance](https://www.nuget.org/packages/XiHan.Framework.Plugins.Conformance)

## 反馈与贡献

`XiHan.Framework.Plugins.Wasm` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
