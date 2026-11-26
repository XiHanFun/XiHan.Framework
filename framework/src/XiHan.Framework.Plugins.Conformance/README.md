# XiHan.Framework.Plugins.Conformance

## 关于

`XiHan.Framework.Plugins.Conformance` 插件协议 `xihan.plugin.v1` 的黑盒一致性套件，给写插件的人用：以插件的构建产物目录为输入，任何语言写的插件都可以用它验证。

## 主要特性

* `PluginProtocolConformance`：进程外插件，套件以宿主身份启动进程并核对握手、会话令牌、调用次序、生命周期、操作、订阅、流式调用、配置节、停止与健康；用例与 framework-spec 的运行器 `xihan-conformance plugin` 一一对应（`handshake`、`refusal`、`session`、`lifecycle`、`order`、`unknown-operation`、`declared-operations`、`unavailable`、`event`、`event-envelope`、`unsubscribed`、`stream`、`configuration`、`stop-early`、`stdin`），插件以两项的 `XIHAN_PLUGIN_HOST_ADDRESS` 启动；`unsubscribed` 另核对信封的 `time` 读不出的投递以 `InvalidArgument` 拒绝，用到的文本取自随包内嵌的规范向量
* `WasmPluginConformance`：Wasm 插件，套件在进程内实例化模块并核对 ABI、操作、中间件与陷阱后重建

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Plugins.Conformance --prerelease
```

### 使用

测试项目继承套件基类并指向插件目录，全部用例随之执行。Wasm 套件在 Windows 上要求测试项目设 `<CETCompat>false</CETCompat>`。

```csharp
public sealed class EchoPluginConformanceTests : PluginProtocolConformance
{
    protected override string PluginDirectory => "<EchoPlugin 的输出目录>";
    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string> { "echoes.crash" };
}
```

## 主要类型

此库提供的主要类型：

* `PluginProtocolConformance`：进程外插件套件
* `WasmPluginConformance`：Wasm 插件套件，`TrapOperation` 指定故意陷阱的操作
* `SampleRequests`、`ExcludedOperations`：为操作提供样例请求、排除故意崩溃的操作
* `VerifiesSessionToken`：缺省为真，只在插件 SDK 还没有发布校验会话令牌的版本时临时关掉，令牌用例随之跳过
* `EnvelopeOperation`：回显交给处理器的信封的操作（以 `{}` 调用，载荷是 `{"envelopes":[{"id":…,"dataSchema":…}]}`），用例 `event-envelope` 用它核对 `dataschema` 原样交给处理器；缺省为空，那个用例跳过

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 进程外插件宿主：[XiHan.Framework.Plugins.OutOfProcess](https://www.nuget.org/packages/XiHan.Framework.Plugins.OutOfProcess)
* Wasm 插件宿主：[XiHan.Framework.Plugins.Wasm](https://www.nuget.org/packages/XiHan.Framework.Plugins.Wasm)
* 协议类型：[XiHan.Framework.Plugins.Protocol](https://www.nuget.org/packages/XiHan.Framework.Plugins.Protocol)

## 反馈与贡献

`XiHan.Framework.Plugins.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
