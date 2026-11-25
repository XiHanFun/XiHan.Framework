# 装配入口

这一页讲 XiHan 应用是怎样被「装起来」的：`AddXiHan` 在两种宿主构建器上的写法、回调里拿到的 `AppBuilder` 能做什么、装配完成后容器里的 `App` 对象提供哪些信息、启动时打印的 Banner 怎样关闭，以及启动日志里那两段文本（能力图谱与管道）该怎么读。读完这页，你能从一个空的 ASP.NET Core 项目或通用宿主项目开始，把框架接进去，并确认装配结果与自己的预期一致。前置知识是[第一个应用](/getting-started/first-app)与[核心概念](/getting-started/concepts)。

## 为什么需要

一个应用由内核、宿主、若干子系统的实现与业务能力包组成。如果让每个包各自往 `IServiceCollection` 里注册服务、各自往 ASP.NET Core 管道里挂中间件，启动顺序、依赖冲突与「换一个实现」都要靠调用方自己维护，出了问题只能逐个翻注册代码。框架把装配收口到一个入口：在 `AddXiHan` 的回调里，应用只声明「用哪些特性、哪些 Provider、改哪些管道细节」；回调结束时内核求解能力图谱、按拓扑顺序调用每个特性的 `Configure`，再把结果注册进宿主的容器。装配的全部结果都能打印出来核对，不必猜。

## 两种宿主，一个入口

| 宿主 | 构建器 | `AddXiHan` 所在包与命名空间 | 之后还要做什么 |
| --- | --- | --- | --- |
| ASP.NET Core | `WebApplicationBuilder` | `XiHan.Framework.Hosting.AspNetCore` | `app.UseXiHan()` 把 HTTP 请求接进内核管道；按需 `app.MapXiHanHealth()`、`app.MapXiHanOperations()` |
| .NET 通用宿主（控制台、Worker、命令行） | `HostApplicationBuilder`；内核另有接受任何 `IHostApplicationBuilder` 的泛型重载 | `XiHan.Framework.Hosting.CommandLine`；泛型重载在内核包，命名空间 `XiHan.Framework.Kernel.Hosting` | 命令行宿主再调用 `builder.Services.AddXiHanCommandLine()`，并以 `RunXiHanCommandLineAsync(args)` 运行；Worker 场景由消息、作业或自己的后台服务发起调用 |

这些重载的回调参数都是 `Action<AppBuilder>`，回调体完全相同，所以同一组特性可以不改一行地在 Web 与命令行两种宿主之间复用。

`Hosting.AspNetCore` 与 `Hosting.CommandLine` 的 `AddXiHan` 都先做一件事：读环境变量 `XIHAN_CONFIG_PATH`，把它指向的目录里的 JSON 文件作为配置源插在 `appsettings.{环境}.json` 之后并监听变化（Kubernetes 里这就是 ConfigMap 的挂载点，见[配置目录与热更新](/configuration/config-directory)）；然后转调内核的泛型 `AddXiHan`，把宿主的 `Services` 与 `Configuration` 交给内核的 `AppBuilder`。只引用内核包、直接用泛型重载时不读这个目录。XiHan 应用与宿主共用同一个服务集合和配置，随宿主启动与停止，没有第二个容器。

## AppBuilder 能做什么

回调里拿到的 `AppBuilder` 是装配期唯一的入口，它的成员分三类：加特性、做选择、改管道。

| 成员 | 作用 |
| --- | --- |
| `UseFeature<TFeature>()` | 加入一个特性，要求公开的无参构造函数。组合包的 `UseMinimalProfile()`、`UseStandardProfile()`、`UseFullProfile()` 就是预先写好的一串 `UseFeature`，见[组合包与选包](/getting-started/profiles) |
| `UseFeature(IFeature feature)` | 加入已创建的实例，特性需要构造参数时用 |
| `UseProvider(capabilityId, manifestId)` | 指定某个能力由哪份清单提供，优先于配置 `XiHan:Providers`，见[能力图谱与 Provider](/startup/capabilities) |
| `UseOptions(KernelOptions options)` | 用代码覆盖配置节 `XiHan:Kernel`，见下文「用代码覆盖内核选项」 |
| `ConfigurePipeline(Action<PipelineBuilder>)` 与 `Pipeline` | 不经特性直接登记中间件或次序约束，归属为 `app`，见[执行管道](/startup/pipeline) |
| `UseServiceBindingRoot(directory)` | 改用别的目录读取 Service Binding，见[资源绑定](/configuration/bindings) |
| `Services`、`Configuration` | 宿主的服务集合与配置，与 `builder.Services`、`builder.Configuration` 是同一个对象 |
| `Build()` | 求解能力图谱并完成装配，由 `AddXiHan` 在回调返回后调用，应用不自己调用 |

特性的书写顺序只决定同一拓扑层次内的次序；依赖关系由清单决定，与写法无关。

## 最小示例

ASP.NET Core 宿主上的 `Program.cs` 就是[第一个应用](/getting-started/first-app)第五步的那一份：回调里只声明组合包、协议适配器与业务特性，`builder.Build()` 之后依次调用 `app.UseXiHan()`（之后映射的端点都在内核管道的 Handle 阶段内执行）、`app.MapXiHanHealth()`（探针，不经内核管道）与 `app.MapXiHanOperations()`（按操作登记表映射路由）。

同一组特性放到通用宿主上，回调不变，只是构建器与运行方式不同（这是 `samples/SimpleCli` 的写法，去掉了消息目录登记；`GreetingsFeature` 是第一个应用里写的特性）：

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseMinimalProfile()
    .UseFeature<McpProtocolFeature>()
    .UseFeature<GreetingsFeature>());

builder.Services.AddXiHanCommandLine();

// 先启动宿主再执行命令，命令结束后按顺序停止宿主；返回值就是进程退出码
return await builder.Build().RunXiHanCommandLineAsync(args);
```

带 `using` 的完整文件与命令的用法见[命令行宿主](/protocols/command-line#最小示例)。

运行 Web 版本（`dotnet run --urls http://localhost:5000`），启动日志先打印 `Capability graph:`（能力图谱），再打印 `Pipeline:`（管道）；样例见[第一个应用](/getting-started/first-app)第六步与[执行管道](/startup/pipeline#十一个阶段)，各行的读法见下文「启动日志怎么读」。`curl -s localhost:5000/health/ready` 返回 `"status": "pass"` 与 `"state": "Ready"`，说明装配与启动都完成了；每个特性的状态由健康详情端点 `/health/details`（`MapXiHanHealthDetails()`）给出。

## 装配发生在什么时候

把启动过程摊开看，`AddXiHan` 只占前两步，之后的事由宿主与内核的宿主服务接手：

1. `AddXiHan` 回调：收集 `UseFeature`、`UseProvider`、`UseOptions` 与管道改动。此时什么都还没有注册。
2. 回调返回，`AppBuilder.Build()` 执行：校验每份清单、求解能力图谱、拓扑排序、解析资源绑定，再按拓扑顺序调用各特性的 `Configure`，最后把 `App`、内置中间件、权限表、操作登记表与宿主服务注册进容器。求解失败在这里抛出 `FrameworkException`，进程不会带着错误的图谱继续，错误码见[能力图谱与 Provider](/startup/capabilities)。
3. 宿主的 `builder.Build()` 构建容器。
4. 宿主启动：先打印[启动 Banner](#启动-banner)，内核的宿主服务再把图谱与管道打进启动日志，执行启动核对，然后按拓扑顺序启动特性（实现了 `IHostedFeature` 的调用 `StartAsync`），应用进入 `Ready` 或 `Degraded`。细节见[生命周期与健康](/startup/lifecycle)。
5. 请求、命令、消息或作业进入，经 `App.InvokeAsync` 走完管道，见[执行管道](/startup/pipeline)。
6. 停止：按逆拓扑顺序停止特性。

所以：服务注册写在特性的 `Configure` 里或 `builder.Services` 上都可以，但 `UseFeature` 必须在回调里完成：回调返回时 `AppBuilder` 已经构建，之后再调用 `UseFeature`、`UseProvider` 这类方法抛出 `KERNEL_HOSTING_004`。需要按环境选特性时，在回调里按配置或环境变量分支，`AppBuilder.Configuration` 就是宿主配置。

## App 对象

装配完成后容器里有一个单例 `App`（命名空间 `XiHan.Framework.Kernel.Hosting`），它是应用在运行期的句柄：

| 成员 | 内容 |
| --- | --- |
| `State` | 应用的聚合状态（`LifecycleState`），只有 `Ready` 与 `Degraded` 接收调用 |
| `Graph` | 已解析的能力图谱（`ResolvedGraph`），可按清单 ID 取节点 |
| `DescribeGraph()` | 图谱的文本，与启动日志、命令行宿主的 `graph` 命令输出相同 |
| `DescribePipeline()` | 管道的文本，与启动日志、`pipeline` 命令输出相同 |
| `Features` | 每个特性的状态（`FeatureStatus`：清单、状态、失败时的错误、是否托管） |
| `Health()` | 健康快照（`HealthSnapshot`），探针端点读的就是它 |
| `Contributions` | 全部 `IHealthContributor` 最近一次上报的条目 |
| `Bindings` | 解析出的资源绑定 |
| `Options` | 生效的 `KernelOptions` |
| `InvokeAsync(context, handler)` | 让一次调用经完整管道执行，处理器作为 Handle 阶段；应用不在服务状态时抛出 `FrameworkException` |
| `CreateContext(scopedServices, cancellationToken)` / `CreateContext(scopedServices, options, cancellationToken)` | 静态方法，为自己发起的调用建一个 `InvocationContext`，选项 `InvocationContextOptions` 带截止时间、Baggage 与上游追踪，见[调用上下文](/startup/invocation-context) |

协议适配器与派发器用的就是 `InvokeAsync`；应用一般只读信息。下面把图谱与管道暴露成诊断端点，便于在运行中的实例上核对装配结果：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;

// 接上面的 Program.cs，放在 app.UseXiHan() 之后
var kernel = app.Services.GetRequiredService<App>();

app.MapGet("/diagnostics/graph", () => Results.Text(kernel.DescribeGraph()));
app.MapGet("/diagnostics/pipeline", () => Results.Text(kernel.DescribePipeline()));
```

这两个端点在 Handle 阶段内执行，生产环境要给它们挂授权要求，写法见[端点里使用框架](/web/endpoints)。

## 启动 Banner

宿主启动时，内核在任何特性启动之前向标准输出打印一段 Banner：

```text

   _  __ ______  _____    _   __
  | |/ //  _/ / / /   |  / | / /
  |   / / // /_/ / /| | /  |/ /
 /   |_/ // __  / ___ |/ /|  /
/_/|_/___/_/ /_/_/  |_/_/ |_/
XiHan Framework v<版本>
A fast, lightweight, efficient and thoughtful .NET framework: microkernel, capability graph, pipeline execution.

碧落降恩承淑颜，共挚崎缘挽曦寒。
迁般故事终成忆，谨此葳蕤换思短。
              —— 致她

Application: SimpleApp v<版本>
Environment: Development
Runtime: .NET <运行时版本> (X64)
OS: <操作系统描述>
```

| 行 | 来源 |
| --- | --- |
| Logo | 固定的字符画。写到没有重定向的控制台时按列做彩虹渐变（24 位色）；重定向到文件、管道或容器日志时不带颜色，设置了环境变量 `NO_COLOR` 也不带 |
| 框架名与版本 | `FrameworkInfo.Name` 与 `FrameworkInfo.Version`，版本是构建期生成的常量，与包版本一致 |
| 标语与各行的标签 | 内核的消息键 `kernel.banner.*`，按默认区域 `XiHan:Localization:DefaultLocale` 翻译，内置 10 种区域；没有装配本地化特性时显示键名 |
| 寄语 | 固定文本，与 Logo 一样不翻译 |
| 应用 | `IHostEnvironment.ApplicationName`；版本由 `Hosting.AspNetCore` 与 `Hosting.CommandLine` 的 `AddXiHan` 从入口程序集的 `AssemblyInformationalVersion` 读取，去掉 `+` 之后的提交哈希。直接用内核的泛型 `AddXiHan` 时只有应用名 |
| 环境 | `IHostEnvironment.EnvironmentName` |
| 运行时与操作系统 | `RuntimeInformation` 给出的 .NET 描述、进程架构与操作系统描述 |

Banner 在 `IHostedLifecycleService.StartingAsync` 里打印，早于所有宿主服务的 `StartAsync`，所以排在特性的启动日志与 Kestrel 的监听日志前面；整段一次写出，不会被并发的日志行拆开。`XiHan.Framework.Testing` 的 `TestHost` 不经宿主服务启动应用，不打印。

关闭它只要一个配置键，环境变量写成 `XiHan__Kernel__Banner__Enabled=false`：

```json
{ "XiHan": { "Kernel": { "Banner": { "Enabled": false } } } }
```

以 JSON 行收集 stdout 的部署（例如控制台日志用 JSON 格式化器）应关掉它，或者改写到别处：在 `AddXiHan` 之前登记自己的 `StartupBannerOptions`，宿主包与内核都不覆盖已有的登记。自己登记的实例要自己填 `ApplicationVersion`，不填时应用一行只有应用名：

```csharp
using XiHan.Framework.Kernel.Hosting;

// 写到 stderr，仍然遵从配置开关 XiHan:Kernel:Banner:Enabled
builder.Services.AddSingleton(StartupBannerOptions.FromConfiguration(
    builder.Configuration,
    new StartupBannerOptions { Output = Console.Error, ApplicationVersion = "1.0.0" }));
builder.AddXiHan(xihan => xihan.UseStandardProfile());
```

命令行宿主不同：一条命令启动一次宿主，stdout 只承载命令的 JSON 输出与 `mcp` 的 stdio 消息流，所以 `AddXiHanCommandLine()` 把选项换成缺省关闭、写 stderr，先于或晚于 `AddXiHan` 调用都一样。配置 `XiHan:Kernel:Banner:Enabled=true` 后 Banner 写到 stderr，stdout 仍然只有命令输出，见[命令行宿主](/protocols/command-line)。

## 启动日志怎么读

图谱一节一个节点，节点行是「清单 ID 版本」加标记，下面逐行列出提供与依赖：

| 文本 | 含义 |
| --- | --- |
| `(default)` | 清单的 `Role` 是 `Default`：没有显式 Provider 时才被选中 |
| `(shadowed)` | 声明了能力却一个都没被选中，不装配、不启动 |
| `provides caching.distributed@1 (not selected)` | 这项能力由别的节点提供，本节点只为其余被选中的能力注册服务 |
| `requires kernel.hosting@1 -> xihan.kernel.core` | 依赖的去向：箭头右边是被选中的提供者 |
| `requires messaging.queue@1? -> (missing, optional)` | 版本后的 `?` 表示可选依赖；缺失时声明它的特性进入 `Degraded` |

管道一行一个中间件：阶段名与类型全名，顺序就是执行顺序。标记的来由与排序规则分别见[能力图谱与 Provider](/startup/capabilities)和[执行管道](/startup/pipeline)。

## 用代码覆盖内核选项

内核自己的选项在配置节 `XiHan:Kernel`，键表见 [Kernel 包参考](/packages/kernel#配置)，超时与失败策略的取舍见[生命周期与健康](/startup/lifecycle)。代码里用 `UseOptions` 覆盖配置，属性是 `TimeSpan` 与枚举：

```csharp
using XiHan.Framework.Kernel.Hosting;

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseOptions(new KernelOptions
    {
        StartTimeout = TimeSpan.FromSeconds(60),
        FailurePolicy = FailurePolicy.IsolateFeature,
    }));
```

其余配置节的写法见[配置模型与选项](/configuration/options)。

## 常见问题

### 组合包已经包含的特性，我再 `UseFeature` 一次会怎样

同一特性加入两次意味着两份相同的清单 ID，`Build()` 报 `KERNEL_CAPABILITY_005`。要换实现时不是移除组合包里的默认实现，而是加入 Provider 特性，让能力图谱按角色规则顶替它，见[能力图谱与 Provider](/startup/capabilities)。

### 忘了调用 `app.UseXiHan()` 会怎样

应用照常启动，探针与操作端点也能映射，但 HTTP 请求不会进入内核管道：认证、租户、授权、事务都不执行，端点里 `HttpContext.FindXiHanContext()` 为无值。`UseXiHan()` 要放在路由之后、端点之前，`WebApplication` 已经在管道开头加了路由，按示例的位置调用即可。

### 没有 Web 也没有命令行，Worker 服务怎么发起调用

在 `HostApplicationBuilder` 上 `AddXiHan` 即可。进入管道的方式有三种：装配消息能力，让消息派发器为每条消息建调用；装配调度能力，让作业派发器为每次运行建调用；或者在自己的后台服务里创建服务作用域，用 `App.CreateContext` 建上下文并调用 `App.InvokeAsync`，写法见[调用上下文](/startup/invocation-context)。

### 装配失败时怎么定位

`Build()` 抛出的 `FrameworkException` 带错误码与冲突双方的清单 ID。能力求解类错误是 `KERNEL_CAPABILITY_001` 到 `005`，清单声明的绑定缺失或类型不符是 `KERNEL_HOSTING_001`（消息 `Feature '<清单 ID>' is not configured: <reason>.`），契约引用了未登记的权限码是 `KERNEL_VALIDATION_005`。把特性逐个加回去并用命令行宿主的 `graph` 命令对照，通常几分钟内能定位。

### Banner 里的中文寄语显示成问号

框架不改 `Console.OutputEncoding`。Windows 上控制台的输出代码页没有中文字符（例如英文系统的 437）时，寄语与中文标签会显示成 `?`；stdout 重定向到文件或管道时也按这个代码页编码（中文系统是 GBK），按 UTF-8 读就是乱码。在 `Program.cs` 开头写 `Console.OutputEncoding = System.Text.Encoding.UTF8;`，或者在终端里先执行 `chcp 65001`。Linux、macOS 与容器里是 UTF-8，不受影响。

### Banner 的 Logo 显示成一串转义字符

Logo 的颜色是 ANSI 24 位色序列。Windows Terminal、VS Code 终端与 Linux、macOS 的终端都能显示；旧版 conhost 没有开启虚拟终端处理时会原样显示序列。换用 Windows Terminal，或设置环境变量 `NO_COLOR=1` 去掉颜色。

## 相关包与下一步

- 包参考：[Kernel](/packages/kernel)、[Primitives](/packages/primitives)、[Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Hosting.CommandLine](/packages/hosting-commandline)、[Profiles.Minimal](/packages/profiles-minimal)、[Profiles.Standard](/packages/profiles-standard)、[Profiles.Full](/packages/profiles-full)
- 下一页：[特性与清单](/startup/features)，看 `UseFeature` 加入的东西长什么样
- 宿主细节：[ASP.NET Core 宿主](/web/hosting)、[命令行宿主](/protocols/command-line)
