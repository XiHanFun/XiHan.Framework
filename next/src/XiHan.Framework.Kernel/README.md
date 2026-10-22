# XiHan.Framework.Kernel

微内核（Ring 1）。单包，子命名空间表达职责：

| 命名空间 | 职责 |
| --- | --- |
| `Hosting` | `AppBuilder`、`App`、`IFeature`、`IHostedFeature`、`FeatureContext`、`KernelOptions`、`BindingResolver`、`HealthSnapshot`、`AddXiHan()` |
| `Capabilities` | `[Capability]`、`CapabilityResolver`、`ResolvedGraph`、`ResolvedNode` |
| `Pipeline` | `IPipelineMiddleware`、`PipelineBuilder`、`PipelineStep`、`PipelineStage`（原语层）、`Placement`、内置中间件 `ObserveMiddleware`、`DeadlineMiddleware` |
| `Diagnostics` | `ActivitySource` / `Meter` 桥、`IAuditSink` |
| `Security` | `Principal`、`TenantContext`、`SecurityContext`、`ResourceName`、`IPolicy`、`AuthorizationResult`、`PermissionRegistry` |
| `Plugins` | `IPlugin`、`IPluginHost`、`PluginContext`、`PermissionGrant` 抽象；具体隔离机制在 `Plugins.*` 包 |
| `Operations` | 操作契约与 XGI（RFC-0011）：`[Operation]`、`IOperation<TRequest, TResponse>`、`OperationContract`、`OperationDescriptor`、`OperationRegistry`、`OperationRequest` / `OperationResponse`、`IOperationInvoker`；特性用 `FeatureContext.AddOperation` 登记，协议适配器按登记表发布 |

## 责任边界

- 不处理 HTTP，不含任何子系统抽象，不含具体插件加载器；协议适配器把线上消息翻译成 `OperationRequest` 交给 `IOperationInvoker`，内核只负责解码、经管道执行与编码。
- 零反射；零第三方依赖，只引用 `Microsoft.Extensions.*` 抽象包。
- 失败结果只携带消息键（`KernelMessages`）与参数，文案在 `Localization/<区域>.json`（10 种区域各一个文件，由 `Localization.InMemory` 装载）；`App.InvokeAsync` 按 Baggage `xihan.locale` 安装 `MessageScope`，让 `key.L()` 在调用链里得到调用方语言（RFC-0010）。启动期的 `FrameworkException` 不本地化。

## 依赖方向

依赖 `XiHan.Framework.Primitives`。被子系统、宿主、协议、插件包引用。

## 预算

公开类型 ≤ 120；启动额外开销 < 50 ms；空应用常驻内存 < 30 MB。

## 状态

P1：内核已实现，公开面记录在 `PublicAPI.Unshipped.txt`。
