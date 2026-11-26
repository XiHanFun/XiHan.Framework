# XiHan.Framework.Plugins.Wasm

## 关于

`XiHan.Framework.Plugins.Wasm` Wasm 插件宿主（实验性）：插件是 WASI preview 1 的 reactor 模块，宿主用 wasmtime 在进程内沙箱里运行，限制内存、栈与 CPU 时间，没有网络，文件系统只有映射的目录。适合不受信任的插件，也是唯一支持插件贡献管道中间件的载体。

## 主要特性

* 沙箱隔离：内存与栈上限、按时限强制中断
* 插件可贡献管道中间件，运行期可加载与卸载
* 陷阱或超时后自动退避重建实例
* 客体写来的消息按严格的 UTF-8 解码：有非法字节序列的响应与不是合法 JSON 的一样按崩溃处理（`PLUGINS_UNEXPECTED_001`），导入函数的请求以 `PLUGINS_VALIDATION_005` 应答，不换成 U+FFFD 再读
* 插件交回的操作应答（`payload`）不为空时先过 JSON 文本的共同检查（语法、64 层、合法的 UTF-8 与 Unicode、重复成员），没过时这次调用是 `PLUGINS_UNEXPECTED_001`，载荷不交给调用方；消息本身合法，不按崩溃处理、实例不重建。空的载荷表示没有响应体，不做这道检查，开了幂等的调用照常记录、回放出同样的空载荷
* 清单声明的配置节写成 JSON 下发；键或值里有孤立的代理项时不调用 `xihan_configure`，插件配置失败，下发的绑定的主值或属性值有孤立的代理项时同样如此（ADR-0321）。启动时的配置失败不在后台重试，下一次调用再试；崩溃之后重建时的配置失败按退避再试（ADR-0260、ADR-0325）
* 插件经 `publish` 导入发布的信封，`time` 是空串或合 RFC 3339 的那一条文法（与截止时间相同，读成 UTC），不合的那一项以 `MESSAGING_VALIDATION_002` 不发布，其余照常；`dataSchema` 是信封的 `dataschema`，两个方向都原样传递，缺省表示没有
* 清单声明并授予（`binding:<绑定名>`）的资源绑定随 `Configure` 下发，与内核按同一套来源解析：先读 Service Binding 目录（`SERVICE_BINDING_ROOT`，或应用经 `UseServiceBindingRoot` 指定的目录）、再读 `XiHan:Bindings`，同名时配置整体覆盖目录；每次配置插件时解析一遍，重启或运行期加载的插件拿到的是那一刻的值。绑定连同它的属性一起下发（`Binding.properties`）：属性名区分大小写、原样下发，只有属性、没有主值的绑定照常下发、主值为空。下发之前按清单的顺序逐个核对：清单标为必需（`required` 缺省为 `true`）的绑定解析不出（两种来源都没给，或只给了类型）、来源声明的类型与清单不同（必需与可选都核对），这个插件配置失败；可选的绑定解析不出不下发，只给了类型的另记警告。下发时属性名须是 1 到 253 个 ASCII 字母、数字、`.`、`_`、`-` 且不以 `.` 开头，一个绑定最多 64 个属性、属性名与属性值合计不超过 262144 个 UTF-8 字节，整个 `Configure` 请求按 protobuf 编码不超过 2097152 字节，不合时这个插件配置失败、不少发也不截断；绑定目录里的文件超过内核的大小上限、一个绑定目录的文件多于 128 个同样配置失败。属性不单独授权，属性值不进日志与错误
* 每次调用插件的操作、向插件投递事件各记一条 `xihan.plugin.invoke` Span：它是内核 `xihan.invoke` 的子 Span，带 `xihan.plugin.id`，操作调用另带 `xihan.operation`；写给插件的 `trace_parent` 以它为父级。没有监听器时不开 Span

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
* `WasmPluginHost`：实现 `IPluginHost`，`FindStatus` 查找诊断快照（未加载时无值）
* `WasmPluginOptions`：目录、授予、挂载、资源上限与超时、重启策略

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/plugins-wasm)

## 相关包

* 协议类型：[XiHan.Framework.Plugins.Protocol](https://www.nuget.org/packages/XiHan.Framework.Plugins.Protocol)
* 进程外插件宿主：[XiHan.Framework.Plugins.OutOfProcess](https://www.nuget.org/packages/XiHan.Framework.Plugins.OutOfProcess)
* 一致性套件：[XiHan.Framework.Plugins.Conformance](https://www.nuget.org/packages/XiHan.Framework.Plugins.Conformance)

## 反馈与贡献

`XiHan.Framework.Plugins.Wasm` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
