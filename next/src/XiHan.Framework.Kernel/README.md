# XiHan.Framework.Kernel

微内核（Ring 1）。单包，子命名空间表达职责：

| 命名空间 | 职责 |
| --- | --- |
| `Hosting` | `AppBuilder`、`App`、`IFeature`、`IHostedFeature`、`FeatureContext`、生命周期状态机、探针聚合、资源绑定 |
| `Capabilities` | `[Capability]`、清单读取、能力图谱、版本约束求解、已解析图 |
| `Pipeline` | `IPipelineMiddleware`、`PipelineBuilder`、`PipelineDelegate`、`Stage`、`Placement`、内置中间件 |
| `Diagnostics` | `ActivitySource` / `Meter` 桥、`IAuditSink` |
| `Security` | `IPrincipal`、租户上下文、资源名、`IPolicy`、`AuthorizationResult`、权限注册表 |
| `Plugins` | `IPlugin`、`IPluginHost`、`IPluginContext` 抽象；具体隔离机制在 `Plugins.*` 包 |

## 责任边界

- 不处理 HTTP，不含任何子系统抽象，不含具体插件加载器。
- 零反射；零第三方依赖，只引用 `Microsoft.Extensions.*` 抽象包。

## 依赖方向

依赖 `XiHan.Framework.Primitives`。被子系统、宿主、协议、插件包引用。

## 预算

公开类型 ≤ 120；启动额外开销 < 50 ms；空应用常驻内存 < 30 MB。

## 状态

P0：空包，P1 填充。
