# XiHan.Framework.Testing

测试基座：没有 HTTP 的内存测试宿主、可注入行为的特性桩、按能力 ID 文本构造清单与独立调用上下文的工具。供框架自身、Provider 与应用的测试项目引用，也是各一致性套件装配宿主的基础。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Testing` |
| 环层 | 工具链 |
| 能力 | — |
| 特性入口 | —（`StubFeature` 是测试用的特性桩） |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Kernel`；`Microsoft.Extensions.Configuration` 10.0.13、`Microsoft.Extensions.DependencyInjection` 10.0.13 |
| AOT | 兼容 |
| 稳定性 | Stable |

## 概述

`TestHost` 自建 `ServiceCollection` 与内存配置，把它们交给 `AppBuilder`，执行调用方的装配回调后构建容器（`ValidateScopes = true`）并启动 `App`；启动失败时先释放容器再抛出。它的调用方法都在新的服务作用域里经完整管道执行，不经过任何协议适配器，所以测的是能力图谱、管道阶段与操作契约本身，而不是 HTTP 绑定。`DisposeAsync` 停止应用并释放容器，幂等。

正式包不引用本包；一致性套件里以宿主装配被测特性的那些（持久化、消息、本地化）引用它。

## 何时使用

- 测自己的特性、中间件与操作：用 `TestHost` 装配，用 `StubFeature` 注入要测的那一小段装配逻辑。
- 测 HTTP 路由、模型绑定与 Problem Details：用 ASP.NET Core 的 `WebApplicationFactory` 测真实宿主，本包不覆盖协议层。
- 写 Provider：先继承对应的一致性套件（[Conformance](./conformance)），再用本包补 Provider 特有行为的测试。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Testing" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。测试项目的其余配置（xUnit v3、Microsoft.Testing.Platform）见[测试与一致性套件](../guide/testing#测试项目)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `TestHost` | `StartAsync(configure, configuration, services, cancellationToken)` 装配并启动；`App`、`Services`、`Configuration`；`InvokeAsync(handler, deadline, baggage)`、`InvokeOperationAsync<TRequest, TResponse>(operationId, request, baggage)`、`InvokeOperationRawAsync(request, baggage)`；`DisposeAsync` |
| `StubFeature` | 实现 `IHostedFeature` 的特性桩：构造时给清单，`OnConfigure` / `OnStart` / `OnStop` 注入行为，`Events` 按顺序记录 `configure`、`start`、`stop` |
| `TestManifests` | `Create(id, provides, requires, role, kind, bindings)`：能力写成 `id@version` / `id@^version?` 文本，版本取框架版本、原语层 1、稳定性 Stable |
| `TestInvocationContext` | `Create(services, deadline, baggage, cancellationToken)`：不依赖宿主的调用上下文，未给服务提供器时用什么都解析不到的空提供器 |

## 使用示例

装配一个特性桩、读配置、走一次管道：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

public sealed class TestHostSampleTests
{
    [Fact]
    public async Task StartsFeatures_AndRunsThePipeline()
    {
        var feature = new StubFeature(TestManifests.Create("xihan.test.stub", requires: ["kernel.hosting@1"]));
        await using var host = await TestHost.StartAsync(
            b => b.UseFeature(feature),
            configuration: [new KeyValuePair<string, string?>("XiHan:Kernel:StartTimeoutSeconds", "5")],
            services: static s => s.AddSingleton(TimeProvider.System));

        Assert.Equal(["configure", "start"], feature.Events);
        Assert.Equal(TimeSpan.FromSeconds(5), host.App.Options.StartTimeout);

        var result = await host.InvokeAsync(static context =>
        {
            context.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        });
        Assert.True(result.IsSuccess);

        // 已过期的截止时间在进入 Handle 之前就失败
        var expired = await host.InvokeAsync(static _ => ValueTask.CompletedTask, deadline: DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(KernelErrors.DeadlineExceeded, expired.Error!.Code);
    }

    [Fact]
    public async Task FailingStart_Throws()
    {
        var feature = new StubFeature(TestManifests.Create("xihan.test.broken"))
        {
            OnStart = static (_, _) => throw new InvalidOperationException("boom"),
        };

        var exception = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(b => b.UseFeature(feature)));
        Assert.Equal(KernelErrors.FeatureStartFailed, exception.Code);
    }
}
```

不需要宿主的单元测试直接构造上下文：

```csharp
using XiHan.Framework.Testing;

var context = TestInvocationContext.Create(deadline: DateTimeOffset.UtcNow.AddSeconds(5));
Console.WriteLine(context.TraceId.Length);
```

中间件与操作的完整测试写法见[测试与一致性套件](../guide/testing#测一个中间件)。

## 行为与限制

- 每次 `InvokeAsync` / `InvokeOperation*Async` 一个独立的服务作用域；释放后再调用抛 `ObjectDisposedException`。
- 特性启动抛出异常时 `StartAsync` 抛出内核的 `FrameworkException`（`KernelErrors.FeatureStartFailed`，即 `KERNEL_HOSTING_002`），容器已释放。
- 配置只有传入的内存键值对，不读 `appsettings.json` 与环境变量；需要时在 `configuration` 参数里给出。
- 调用不经过 Ingress 的协议绑定：`Accept-Language` 协商、HTTP 认证头之类由宿主适配器做的事不发生，Baggage 以 `baggage` 参数直接给出。

## 相关

- [测试与一致性套件](../guide/testing)
- [Conformance 一致性套件](./conformance)、[Kernel](./kernel)
