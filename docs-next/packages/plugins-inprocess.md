# XiHan.Framework.Plugins.InProcess

进程内插件宿主（`isolation: in-process`）：用可收集的 `AssemblyLoadContext` 加载插件程序集，为每个插件建立独立的服务容器，负责启动、停止、卸载与状态上报。适用于受信任的 .NET 扩展，不适用于 Native AOT 应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Plugins.InProcess` |
| 环层 | Ring 5（协议与插件） |
| 能力 | —（插件清单依赖内核提供的 `kernel.plugins@1`） |
| 特性入口 | —（应用直接创建 `InProcessPluginHost`） |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Kernel`；`Microsoft.Extensions.DependencyInjection` 10.0.13 |
| AOT | 不兼容（框架里唯一允许运行期反射的包，构造函数标注 `RequiresDynamicCode` / `RequiresUnreferencedCode`） |
| 稳定性 | Preview |

## 概述

`InProcessPluginHost` 实现内核的 `IPluginHost`。`LoadAsync(PluginSource)` 的顺序是：读插件目录里的 `xihan.manifest.json`，要求 `kind: plugin` 与 `isolation: in-process`；核对清单 `permissions` 都在 `PluginSource.Permissions` 里；要求 `package` 与 `entry` 都有值且 `<package>.dll` 存在；在一个新的可收集加载上下文里加载程序集，按 `entry` 类型名实例化，要求它实现 `IPlugin` 且自带清单的 ID 与文件一致；调用 `IPlugin.Configure(PluginContext)`，构建插件专属的 `ServiceProvider`，再调用 `StartAsync`。成功后状态为 `Ready`。

加载上下文把名字以 `XiHan.Framework.`、`Microsoft.Extensions.`、`System.`、`netstandard`、`mscorlib` 开头的程序集交给默认上下文，保证 `IPlugin` 等类型与宿主是同一个类型；插件的私有托管与原生依赖按它自己的 `.deps.json` 在插件目录里解析。

插件拿到的 `PluginContext` 含清单、插件自己的 `IServiceCollection`、宿主配置的只读视图与授予的权限。插件容器不能解析宿主服务；`PluginContext.Pipeline` 恒为 `null`，插件不能贡献中间件，也不能向宿主登记操作。

## 何时使用

- 扩展代码是受信任的 .NET 程序集，要与主程序分开部署、按需装卸：用本包。
- 代码不受信任，或要在管道阶段插入逻辑：用 [Plugins.Wasm](./plugins-wasm)。
- 插件用其他语言写，或要独立进程与独立依赖、对外提供操作：用 [Plugins.OutOfProcess](./plugins-outofprocess)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Plugins.InProcess" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。本包没有特性入口，失败结果的文案要由宿主应用注册消息目录（RFC-0010）：

```csharp
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Plugins.InProcess;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("plugins", typeof(PluginMessages)));
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `InProcessPluginHost` | 宿主：`LoadAsync`、`UnloadAsync`、`Plugins`（状态快照）、`DisposeAsync`（停止并卸载全部插件） |
| `PluginErrors` | 本宿主的错误码常量 |
| `PluginMessages` | 对应的消息键，文案在本包 `Localization/<区域>.json` |
| `IPlugin`（`XiHan.Framework.Kernel.Plugins`） | 插件契约：`Manifest`、`Configure(PluginContext)`、`StartAsync`、`StopAsync` |
| `PluginContext`（同上） | 配置上下文：`Manifest`、`Services`、`Configuration`、`Permissions`、`Pipeline`（本宿主为 `null`） |
| `PluginSource`（同上） | 插件目录与授予的权限 |
| `PermissionGrant`（同上） | 授予集合：`IsGranted`、`Missing(manifest)`、`Empty` |

## 使用示例

插件一侧（类库项目设 `<EnableDynamicLoading>true</EnableDynamicLoading>`，引用 `XiHan.Framework.Kernel`，把 `xihan.manifest.json` 复制到输出目录）：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace ReportPlugin;

public sealed class ReportPlugin : IPlugin
{
    // 与插件目录里的 xihan.manifest.json 一致：宿主核对两者的 ID
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.reports.plugin",
        Package = "ReportPlugin",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Plugin,
        Isolation = PluginIsolation.InProcess,
        Requires = [new CapabilityRequirement(KernelCapabilities.Plugins, KernelCapabilities.Version)],
        Entry = "ReportPlugin.ReportPlugin",
    };

    public void Configure(PluginContext context) => context.Services.AddSingleton<ReportCounter>();

    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // 插件容器与宿主隔离，只能解析自己注册的服务
        services.GetRequiredService<ReportCounter>().Increment();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ReportCounter
{
    private int _count;

    public int Count => _count;

    public void Increment() => Interlocked.Increment(ref _count);
}
```

宿主一侧：

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Plugins.InProcess;

await using var host = new InProcessPluginHost(new ConfigurationBuilder().Build(), NullLoggerFactory.Instance);

var loaded = await host.LoadAsync(new PluginSource("plugins/report", PermissionGrant.Empty), CancellationToken.None);
Console.WriteLine(loaded.IsFailure ? loaded.Error!.Code : loaded.ValueOrThrow().State.ToString());

var unloaded = await host.UnloadAsync("xihan.reports.plugin", CancellationToken.None);
Console.WriteLine(unloaded.IsSuccess);
```

完整示例是 `samples/SamplePlugin`，测试 `XiHan.Framework.Plugins.InProcess.Tests` 以它的构建输出目录作为插件源。

## 行为与限制

| 错误码 | 常量 | 时机 |
| --- | --- | --- |
| `PLUGINS_VALIDATION_001` | `InvalidSource` | 清单不合法、不是进程内插件、缺 `package` 或 `entry` |
| `PLUGINS_VALIDATION_002` | `InvalidEntry` | 入口类型未实现 `IPlugin`，或实例清单 ID 与文件不一致 |
| `PLUGINS_NOTFOUND_001` | `NotFound` | 程序集或入口类型不存在；卸载未加载的插件 |
| `PLUGINS_DENIED_001` | `PermissionDenied` | 清单声明的权限未被授予（在加载任何代码之前判定） |
| `PLUGINS_CONFLICT_001` | `AlreadyLoaded` | 同一清单 ID 已加载 |
| `PLUGINS_UNEXPECTED_001` | `LoadFailed` | `Configure` / `StartAsync` 抛出异常；`StopAsync` 抛出异常时状态记为 `Failed`，错误同码 |

- 失败以 `Result` 返回，不抛出到宿主；加载失败时已建的容器被释放、加载上下文被卸载。取消异常原样传播。
- 加载、卸载与释放由一个互斥量串行化，同一清单 ID 不会被并发加载两次。
- `UnloadAsync` 先从表里移除再停止；停止成功为 `Stopped`，抛出异常为 `Failed`，两种情况都释放容器并卸载加载上下文。加载上下文能否真正被回收取决于宿主是否还持有插件类型的引用。
- 权限只在加载时核对，运行期不做强制；插件可用 `PluginContext.Permissions.IsGranted(...)` 自查。
- 不负责热重载、插件市场，也不做健康上报（没有实现 `IHealthContributor`）。

## 相关

- [插件](../guide/plugins#进程内宿主)
- [Plugins.OutOfProcess](./plugins-outofprocess)、[Plugins.Wasm](./plugins-wasm)
- [Kernel](./kernel)
- 规范：RFC-0002《清单》
