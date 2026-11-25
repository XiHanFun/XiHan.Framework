# XiHan.Framework.Plugins.InProcess

## 关于

`XiHan.Framework.Plugins.InProcess` 进程内插件宿主：从目录读取 `xihan.manifest.json`，用可收集的 `AssemblyLoadContext` 加载受信任的 .NET 插件程序集，为每个插件建立隔离的服务容器，负责启动、停止、卸载与状态上报。不适用于 Native AOT 应用。

## 主要特性

* 插件的私有依赖隔离在自己的加载上下文，框架类型与宿主共享
* 加载前核对清单权限，失败以 `Result` 返回、不抛到宿主
* 加载前核对清单声明的绑定，与内核、进程外与 Wasm 宿主同一条规则：必需的绑定（`required` 缺省为 `true`）在宿主配置与 Service Binding 目录里都没有能用的内容，或来源声明的类型与清单不同，即加载失败（`PLUGINS_UNEXPECTED_001`）；宿主不向插件下发绑定，插件自己经 `BindingResolver.Resolve(context.Configuration, context.ServiceBindingRoot)` 读
* 绑定的根目录是构造时给的 `serviceBindingRoot`（没给时取环境变量 `SERVICE_BINDING_ROOT`），插件经 `PluginContext.ServiceBindingRoot` 拿到同一个值：应用经 `UseServiceBindingRoot` 指定了根目录时把同一个值传进来，宿主核对的、插件读到的与内核读的是同一个目录
* 进程内插件不能贡献中间件，需要时用 Wasm 插件

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Plugins.InProcess --prerelease
```

### 使用

本包没有特性入口：应用注册消息目录后直接创建 `InProcessPluginHost` 加载插件。

```csharp
builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("plugins", typeof(PluginMessages)));

// 应用经 UseServiceBindingRoot 指定了绑定根目录时，把同一个值作第三个参数传进来；不传即取环境变量 SERVICE_BINDING_ROOT
await using var host = new InProcessPluginHost(configuration, loggerFactory, serviceBindingRoot);
var loaded = await host.LoadAsync(new PluginSource("plugins/report", PluginPermissions.Empty), ct);
var unloaded = await host.UnloadAsync("xihan.reports.plugin", ct);
```

## 主要类型

此库提供的主要类型：

* `InProcessPluginHost`：`LoadAsync`、`UnloadAsync`、`Plugins`；构造时可给 Service Binding 根目录
* `PluginErrors`、`PluginMessages`：错误码与消息键
* `IPlugin`、`PluginContext`、`PluginSource`、`PluginPermissions`：来自 `Kernel`，插件契约与授予的权限；`PluginContext.ServiceBindingRoot` 是宿主解析绑定所用的根目录

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/plugins-inprocess)

## 相关包

* 进程外插件宿主：[XiHan.Framework.Plugins.OutOfProcess](https://www.nuget.org/packages/XiHan.Framework.Plugins.OutOfProcess)
* Wasm 插件宿主：[XiHan.Framework.Plugins.Wasm](https://www.nuget.org/packages/XiHan.Framework.Plugins.Wasm)

## 反馈与贡献

`XiHan.Framework.Plugins.InProcess` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
