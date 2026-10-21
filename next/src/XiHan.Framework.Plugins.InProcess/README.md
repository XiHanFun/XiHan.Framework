# XiHan.Framework.Plugins.InProcess

进程内插件宿主（Ring 5，`isolation: in-process`）。

## 责任边界

- `InProcessPluginHost`：从目录读取 `xihan.manifest.json`，校验种类、隔离级别与授予的权限，用可收集的 `AssemblyLoadContext` 加载程序集，按 `entry` 实例化 `IPlugin`，为插件建立隔离的服务容器，启动、停止、卸载并上报状态。
- 框架类型（`XiHan.Framework.*`、`Microsoft.Extensions.*`、`System.*`）与宿主共享，插件的私有依赖隔离在自己的加载上下文中。
- 插件失败以 `Result` 返回，不抛出到宿主；崩溃的插件记为 `Failed`。失败结果的消息键在 `PluginMessages`，文案在本包的 `Localization/<区域>.json`；本包没有特性入口，宿主应用用 `services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("plugins", typeof(PluginMessages)))` 注册目录（RFC-0010）。

不负责：运行期向已构建的管道贡献中间件（在应用启动前加载的插件由 P3 的生成器路径支持）、热重载、插件市场。

## 依赖方向

依赖 `XiHan.Framework.Kernel` 与 `Microsoft.Extensions.DependencyInjection`。这是框架中唯一允许运行期反射的包，构造函数标注 `RequiresDynamicCode`，Native AOT 应用不应使用。

## 状态

P1：加载、启动、停止、卸载可用。
