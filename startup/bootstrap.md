来源：https://framework.docs.xihanfun.com/startup/bootstrap

# 装配入口

这一页讲 XiHan 应用是怎样被「装起来」的：`AddXiHan` 在两种宿主构建器上的写法、回调里拿到的 `AppBuilder` 能做什么、装配完成后容器里的 `App` 对象提供哪些信息，以及启动日志里那两段文本（能力图谱与管道）该怎么读。读完这页，你能从一个空的 ASP.NET Core 项目或通用宿主项目开始，把框架接进去，并确认装配结果与自己的预期一致。前置知识是[第一个应用](/getting-started/first-app)与[核心概念](/getting-started/concepts)。

## 为什么需要

一个应用由内核、宿主、若干子系统的实现与业务能力包组成。如果让每个包各自往 `IServiceCollection` 里注册服务、各自往 ASP.NET Core 管道里挂中间件，启动顺序、依赖冲突与「换一个实现」都要靠调用方自己维护，出了问题只能逐个翻注册代码。框架把装配收口到一个入口：在 `AddXiHan` 的回调里，应用只声明「用哪些特性、哪些 Provider、改哪些管道细节」；回调结束时内核求解能力图谱、按拓扑顺序调用每个特性的 `Configure`，再把结果注册进宿主的容器。装配的全部结果都能打印出来核对，不必猜。

## 两种宿主，一个入口

| 宿主 | 构建器 | `AddXiHan` 所在包与命名空间 | 之后还要做什么 |
| --- | --- | --- | --- |
| ASP.NET Core | `WebApplicationBuilder` | `XiHan.Framework.Hosting.AspNetCore` | `app.UseXiHan()` 把 HTTP 请求接进内核管道；按需 `app.MapXiHanHealth()`、`app.MapXiHanOperations()` |
| .NET 通用宿主（控制台、Worker、命令行） | `HostApplicationBuilder`，泛型重载接受任何 `IHostApplicationBuilder` | 内核包，命名空间 `XiHan.Framework.Kernel.Hosting` | 命令行宿主再调用 `builder.Services.AddXiHanCommandLine()`，并以 `RunXiHanCommandLineAsync(args)` 运行；Worker 场景由消息、作业或自己的后台服务发起调用 |

两个重载的回调参数都是 `Action<AppBuilder>`，回调体完全相同，所以同一组特性可以不改一行地在 Web 与命令行两种宿主之间复用。

两种宿主的 `AddXiHan` 都先做一件事：读环境变量 `XIHAN_CONFIG_PATH`，把它指向的目录里的 JSON 文件作为配置源插在 `appsettings.{环境}.json` 之后并监听变化（Kubernetes 里这就是 ConfigMap 的挂载点，见[配置目录与热更新](/configuration/config-directory)）；然后把宿主的 `Services` 与 `Configuration` 交给内核的 `AppBuilder`。XiHan 应用与宿主共用同一个服务集合和配置，随宿主启动与停止，没有第二个容器。

## AppBuilder 能做什么

回调里拿到的 `AppBuilder` 是装配期唯一的入口，它的成员分三类：加特性、做选择、改管道。

| 成员 | 作用 |
| --- | --- |
| `UseFeature<TFeature>()` | 加入一个特性，要求公开的无参构造函数。组合包的 `UseMinimalProfile()`、`UseStandardProfile()`、`UseFullProfile()` 就是预先写好的一串 `UseFeature`，见[组合包与选包](/getting-started/profiles) |
| `UseFeature(IFeature feature)` | 加入已创建的实例，特性需要构造参数时用 |
| `UseProvider(capabilityId, manifestId)` | 指定某个能力由哪份清单提供，优先于配置 `XiHan:Providers`，见[能力图谱与 Provider](/startup/capabilities) |
| `UseOptions(KernelOptions options)` | 用代码覆盖配置节 `XiHan:Kernel`，见下文「内核选项概览」 |
| `ConfigurePipeline(Action<PipelineBuilder>)` 与 `Pipeline` | 不经特性直接登记中间件或次序约束，归属为 `app`，见[执行管道](/startup/pipeline) |
| `UseServiceBindingRoot(directory)` | 改用别的目录读取 Service Binding，见[资源绑定](/configuration/bindings) |
| `Services`、`Configuration` | 宿主的服务集合与配置，与 `builder.Services`、`builder.Configuration` 是同一个对象 |
| `Build()` | 求解能力图谱并完成装配，由 `AddXiHan` 在回调返回后调用，应用不自己调用 |

特性的书写顺序只决定同一拓扑层次内的次序；依赖关系由清单决定，与写法无关。

## 最小示例

ASP.NET Core 宿主上的完整 `Program.cs`。`GreetingsFeature` 是[第一个应用](/getting-started/first-app)里写的特性，这里不重复：

```csharp
using HelloXiHan;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);

// 回调里只声明：组合包、协议适配器、业务特性。回调返回时内核求解图谱并调用各特性的 Configure
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<GreetingsFeature>());

var app = builder.Build();

app.UseXiHan();            // 之后映射的端点都在内核管道的 Handle 阶段内执行
app.MapXiHanHealth();      // /health/startup、/health/ready、/health/live，不经内核管道
app.MapXiHanOperations();  // 按操作登记表映射路由

app.Run();
```

同一组特性放到通用宿主上，只是构建器与运行方式不同（这是 `samples/SimpleCli` 的写法，去掉了消息目录登记）：

```csharp
using Microsoft.Extensions.Hosting;
using SimpleCli;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.Mcp;

var builder = Host.CreateApplicationBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseMinimalProfile()
    .UseFeature<McpProtocolFeature>()
    .UseFeature<GreetingsFeature>());

builder.Services.AddXiHanCommandLine();

// 先启动宿主再执行命令，命令结束后按顺序停止宿主；返回值就是进程退出码
return await builder.Build().RunXiHanCommandLineAsync(args);
```

运行 Web 版本（`dotnet run --urls http://localhost:5000`），启动日志先打印能力图谱，再打印管道：

```text
Capability graph:
xihan.kernel.core <版本>
  provides kernel.hosting@1
  ...
xihan.persistence.inmemory <版本> (default)
  provides persistence.relational@1
  requires kernel.hosting@1 -> xihan.kernel.core
xihan.protocols.http <版本>
  provides protocols.http@1
  requires kernel.hosting@1 -> xihan.kernel.core
xihan.hello.greetings 1.0.0
  requires kernel.hosting@1 -> xihan.kernel.core

Pipeline:
observe      XiHan.Framework.Kernel.Diagnostics.ObserveMiddleware
resilience   XiHan.Framework.Kernel.Pipeline.DeadlineMiddleware
resilience   XiHan.Framework.Kernel.Resilience.ResilienceMiddleware
authenticate XiHan.Framework.Security.Abstractions.Authentication.AuthenticationMiddleware
tenant       XiHan.Framework.MultiTenancy.Abstractions.Resolution.TenantResolutionMiddleware
authorize    XiHan.Framework.Security.Abstractions.Authorization.AuthorizationMiddleware
validate     XiHan.Framework.Kernel.Validation.ValidationMiddleware
idempotency  XiHan.Framework.Caching.Abstractions.Idempotency.IdempotencyMiddleware
transaction  XiHan.Framework.Persistence.Abstractions.UnitOfWork.UnitOfWorkMiddleware
```

`curl -s localhost:5000/health/ready` 返回 `"state": "Ready"` 与每个特性的状态，说明装配与启动都完成了。

## 装配发生在什么时候

把启动过程摊开看，`AddXiHan` 只占前两步，之后的事由宿主与内核的宿主服务接手：

1. `AddXiHan` 回调：收集 `UseFeature`、`UseProvider`、`UseOptions` 与管道改动。此时什么都还没有注册。
2. 回调返回，`AppBuilder.Build()` 执行：校验每份清单、求解能力图谱、拓扑排序、解析资源绑定，再按拓扑顺序调用各特性的 `Configure`，最后把 `App`、内置中间件、权限表、操作登记表与宿主服务注册进容器。求解失败在这里抛出 `FrameworkException`，进程不会带着错误的图谱继续，错误码见[能力图谱与 Provider](/startup/capabilities)。
3. 宿主的 `builder.Build()` 构建容器。
4. 宿主启动：内核的宿主服务先执行启动核对，再按拓扑顺序启动特性（实现了 `IHostedFeature` 的调用 `StartAsync`），应用进入 `Ready` 或 `Degraded`，启动日志打印图谱与管道。细节见[生命周期与健康](/startup/lifecycle)。
5. 请求、命令、消息或作业进入，经 `App.InvokeAsync` 走完管道，见[执行管道](/startup/pipeline)。
6. 停止：按逆拓扑顺序停止特性。

所以：服务注册写在特性的 `Configure` 里或 `builder.Services` 上都可以，但 `UseFeature` 必须在回调里完成，回调之外再加特性没有效果。

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
| `CreateContext(scopedServices, cancellationToken, ...)` | 静态方法，为自己发起的调用建一个 `InvocationContext`，见[调用上下文](/startup/invocation-context) |

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

## 启动日志怎么读

图谱一节一个节点，节点行是「清单 ID 版本」加标记，下面逐行列出提供与依赖：

| 文本 | 含义 |
| --- | --- |
| `(default)` | 清单的 `Role` 是 `Default`：没有显式 Provider 时才被选中 |
| `(shadowed)` | 声明了能力却一个都没被选中，不装配、不启动 |
| `provides caching.distributed@1 (not selected)` | 这项能力由别的节点提供，本节点只为其余被选中的能力注册服务 |
| `requires kernel.hosting@1 -> xihan.kernel.core` | 依赖的去向：箭头右边是被选中的提供者 |
| `requires messaging.queue@1 -> (missing, optional)` | 可选依赖缺失，声明它的特性进入 `Degraded` |

管道一行一个中间件：阶段名与类型全名，顺序就是执行顺序。没有装配某个子系统时对应阶段就是空的，Minimal 组合只有 `observe`、`resilience`、`validate` 三个阶段有中间件。标记的含义与排序规则分别见[能力图谱与 Provider](/startup/capabilities)和[执行管道](/startup/pipeline)。

## 内核选项概览

内核自己的选项在配置节 `XiHan:Kernel`（`KernelOptions.ConfigurationSection`），四个键：

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `StartTimeoutSeconds` | `30` | 每个特性 `StartAsync` 的超时 |
| `StopTimeoutSeconds` | `30` | 每个特性 `StopAsync` 的超时 |
| `FailurePolicy` | `TerminateStartup` | 特性启动失败时终止启动，或改为 `IsolateFeature` 隔离该特性 |
| `UseBuiltInMiddleware` | `true` | 是否登记内核内置中间件（Observe、截止时间、Resilience、Validate） |

代码里用 `UseOptions` 覆盖，属性是 `TimeSpan` 与枚举：

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

每个键的语义、非法值的处理与失败策略的取舍放在[生命周期与健康](/startup/lifecycle)；其余配置节的写法见[配置模型与选项](/configuration/options)。

## 常见问题

### `UseFeature` 的书写顺序重要吗

不重要。被依赖的特性总是先 `Configure`、先启动，顺序由清单的 `Requires` 推出；书写顺序只在同一层次内决定次序。中间件属于哪个阶段也只取决于登记时给的阶段。

### 组合包已经包含的特性，我再 `UseFeature` 一次会怎样

同一特性加入两次意味着两份相同的清单 ID，`Build()` 报 `KERNEL_CAPABILITY_005`。要换实现时不是移除组合包里的默认实现，而是加入 Provider 特性，让能力图谱按角色规则顶替它，见[能力图谱与 Provider](/startup/capabilities)。

### 忘了调用 `app.UseXiHan()` 会怎样

应用照常启动，探针与操作端点也能映射，但 HTTP 请求不会进入内核管道：认证、租户、授权、事务都不执行，端点里 `HttpContext.GetXiHanContext()` 为无值。`UseXiHan()` 要放在路由之后、端点之前，`WebApplication` 已经在管道开头加了路由，按示例的位置调用即可。

### 没有 Web 也没有命令行，Worker 服务怎么发起调用

在 `HostApplicationBuilder` 上 `AddXiHan` 即可。进入管道的方式有三种：装配消息能力，让消息派发器为每条消息建调用；装配调度能力，让作业派发器为每次运行建调用；或者在自己的后台服务里创建服务作用域，用 `App.CreateContext` 建上下文并调用 `App.InvokeAsync`，写法见[调用上下文](/startup/invocation-context)。

### 能在 `AddXiHan` 回调之外再加特性吗

不能。回调返回时 `Build()` 已经执行，图谱已经定型。需要按环境选特性时，在回调里按配置或环境变量分支即可，`AppBuilder.Configuration` 就是宿主配置。

### 装配失败时怎么定位

`Build()` 抛出的 `FrameworkException` 带错误码与冲突双方的清单 ID。能力求解类错误是 `KERNEL_CAPABILITY_001` 到 `005`，必需绑定缺失是 `KERNEL_HOSTING_001`，契约引用了未登记的权限码是 `KERNEL_VALIDATION_005`。把特性逐个加回去并用命令行宿主的 `graph` 命令对照，通常几分钟内能定位。

## 相关包与下一步

- 包参考：[Kernel](/packages/kernel)、[Primitives](/packages/primitives)、[Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Hosting.CommandLine](/packages/hosting-commandline)、[Profiles.Minimal](/packages/profiles-minimal)、[Profiles.Standard](/packages/profiles-standard)、[Profiles.Full](/packages/profiles-full)
- 下一页：[特性与清单](/startup/features)，看 `UseFeature` 加入的东西长什么样
- 宿主细节：[ASP.NET Core 宿主](/web/hosting)、[命令行宿主](/protocols/command-line)
