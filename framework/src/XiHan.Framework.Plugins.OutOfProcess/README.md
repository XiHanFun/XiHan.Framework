# XiHan.Framework.Plugins.OutOfProcess

## 关于

`XiHan.Framework.Plugins.OutOfProcess` 进程外插件宿主：插件是任意语言写的子进程，只需 protobuf、gRPC 与 JSON，经 `xihan.plugin.v1` 协议与宿主通信。插件声明的操作登记为代理操作，与本地操作一样经完整管道。

## 主要特性

* 任意语言写插件，独立进程与独立依赖
* 崩溃按退避自动重启，每个插件是一个健康条目
* 插件可回调宿主的操作调用与消息发布，需在清单声明并授予权限；发布在按元数据建的回调上下文里进行，信封缺的租户、主体与关联 ID 由发布器补齐；信封的 `time` 是空串或合 RFC 3339 的那一条文法（与截止时间相同，读成 UTC），不合的那一项以 `MESSAGING_VALIDATION_002` 不发布，其余照常
* 回调端点有两个：Windows 上是只允许当前用户的命名管道加回环 TCP，其他系统是私有目录里的 Unix 域套接字加回环 TCP，各语言的 gRPC 库至少能连上回环 TCP
* 清单声明并授予（`binding:<绑定名>`）的资源绑定随 `Configure` 下发，与内核按同一套来源解析：先读 Service Binding 目录（`SERVICE_BINDING_ROOT`，或应用经 `UseServiceBindingRoot` 指定的目录）、再读 `XiHan:Bindings`，同名时配置整体覆盖目录；每次配置插件时解析一遍，重启或运行期加载的插件拿到的是那一刻的值。绑定连同它的属性一起下发（`Binding.properties`）：属性名区分大小写、原样下发，只有属性、没有主值的绑定照常下发、主值为空。下发之前按清单的顺序逐个核对：清单标为必需（`required` 缺省为 `true`）的绑定解析不出（两种来源都没给，或只给了类型）、来源声明的类型与清单不同（必需与可选都核对），这个插件配置失败；可选的绑定解析不出不下发，只给了类型的另记警告。下发时属性名须是 1 到 253 个 ASCII 字母、数字、`.`、`_`、`-` 且不以 `.` 开头，一个绑定最多 64 个属性、属性名与属性值合计不超过 262144 个 UTF-8 字节，整个 `Configure` 请求按 protobuf 编码不超过 2097152 字节，不合时这个插件配置失败、不少发也不截断；绑定目录里的文件超过内核的大小上限同样配置失败。属性不单独授权，属性值不进日志与错误
* 每次调用插件的操作、向插件投递事件各记一条 `xihan.plugin.invoke` Span：它是内核 `xihan.invoke` 的子 Span，带 `xihan.plugin.id`，操作调用另带 `xihan.operation`；写给插件的 `trace_parent` 以它为父级。没有监听器时不开 Span

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
* `OutOfProcessPluginHost`：实现 `IPluginHost`，`FindStatus` 查找诊断快照（未加载时无值），`HostEndpoints` 是按偏好排序的回调端点
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
