来源：https://framework.docs.xihanfun.com/testing/basics

# 单元与集成测试

这一页讲怎样给 XiHan 应用建测试项目，怎样用 `Testing` 包的测试宿主在内存里装配应用并经完整管道调用，怎样测一个中间件、测一个操作，以及用 SQLite 文件库时要注意什么。读完你能不起 HTTP 服务就把特性、中间件与操作的行为测出来。前置知识是[执行管道](/startup/pipeline)与[操作契约](/web/operations)：测试宿主调用的正是这两页讲的东西。

## 为什么需要

框架里的行为大多发生在管道里：认证、授权、校验、幂等、事务都在 Handle 之前，操作本身只是最后一步。只测操作类的 `HandleAsync` 测不到这些；起一个真实的 Web 服务器又慢、又要端口、又把协议层的噪音混进来。`Testing` 包的 `TestHost` 在两者之间：它用你的特性在内存里装配一个真实的应用，没有 HTTP，没有协议适配器，每次调用在新的服务作用域里走完整的十一个阶段，结果就是真实宿主里会得到的 `Result`。框架自己的单元与一致性测试都建在它上面。

## 测试项目

框架仓库的测试用 xUnit v3，由 Microsoft.Testing.Platform（MTP）驱动。应用的测试项目按同样的方式配置：仓库根的 `global.json` 选择 MTP 运行器，测试项目引用 `xunit.v3` 与 `XiHan.Framework.Testing`。

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
</Project>
```

```bash
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package xunit.v3
dotnet add package xunit.runner.visualstudio
dotnet add package Microsoft.Testing.Extensions.CodeCoverage
dotnet add package XiHan.Framework.Testing --prerelease
```

MTP 模式下 `dotnet test` 用 `--solution` 或 `--project` 指定目标：

```bash
# 整个解决方案
dotnet test --solution MyApp.slnx --configuration Release

# 单个测试项目
dotnet test --project tests/Orders.Tests/Orders.Tests.csproj --configuration Release

# 采集 Cobertura 覆盖率
dotnet test --solution MyApp.slnx --configuration Release -p:TestingPlatformCommandLineArguments="--coverage --coverage-output-format cobertura"
```

::: warning MTP 模式不要退回 VSTest
`global.json` 选了 MTP 之后，`dotnet test` 走 VSTest 目标会被 SDK 拒绝。不要删除 `global.json` 的 `test.runner`，也不要去掉 `--solution` / `--project` 来绕过运行错误。
:::

## 测试宿主

`TestHost.StartAsync(configure, configuration, services)` 在内存里装配并启动一个应用：自建服务集合（`ValidateScopes = true`，在根容器解析 Scoped 服务直接报错）与内存配置，调用 `AppBuilder` 上的 `UseFeature` 后启动全部特性；没有 HTTP，也不经过任何协议适配器。启动失败时先释放容器再抛出，`DisposeAsync` 按与启动相反的顺序停止特性，重复释放是空操作。

| 成员 | 用途 |
| --- | --- |
| `StartAsync(configure, configuration = null, services = null)` | 装配并启动；`configuration` 是键值对集合，`services` 可以在特性之外追加登记 |
| `InvokeAsync(handler, deadline, baggage)` | 以一个 `PipelineStep` 作为 Handle 阶段，返回调用结果；用来测中间件 |
| `InvokeOperationAsync<TRequest, TResponse>(operationId, request, baggage)` | 类型化调用操作，经完整管道 |
| `InvokeOperationRawAsync(OperationRequest, baggage)` | 以 XGI 消息（JSON 载荷）调用操作，返回 `OperationResponse` |
| `Services` / `Configuration` / `App` | 容器、配置与内核的 `App` |

配套三个工具：`StubFeature` 是可注入 `OnConfigure` / `OnStart` / `OnStop` 行为、按顺序记录 `configure` / `start` / `stop` 事件的特性桩；`TestManifests.Create(id, provides, requires, role, kind, bindings)` 用 `id@version` 文本构造清单；`TestInvocationContext.Create(services, deadline, baggage)` 给不需要宿主的单元测试一个独立的调用上下文。

## 最小示例

装配一个只依赖 `kernel.hosting@1` 的特性桩，读一个配置键，走一次管道：

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

public sealed class HostSmokeTests
{
    [Fact]
    public async Task Pipeline_RunsTheHandler()
    {
        var feature = new StubFeature(TestManifests.Create("xihan.contoso.stub", requires: ["kernel.hosting@1"]));
        await using var host = await TestHost.StartAsync(
            b => b.UseFeature(feature),
            configuration: [new KeyValuePair<string, string?>("XiHan:Kernel:StartTimeoutSeconds", "5")]);

        var result = await host.InvokeAsync(static context =>
        {
            context.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "configure", "start" }, feature.Events);
    }
}
```

运行 `dotnet test --project <测试项目>`：用例通过说明特性被配置并启动、调用经过了内核的全部内建阶段（截止时间、观测、校验等）并到达 Handle；`feature.Events` 记录了生命周期顺序，宿主释放后还会追加 `stop`。

## 测一个中间件

中间件在 `InvokeAsync` 里决定是否调用下一步；短路时把失败结果写进上下文，不进入下游。下面的中间件要求调用方带关联 ID：

```csharp
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace Contoso.Correlation;

public static class CorrelationMessages
{
    public const string Missing = "contoso.correlation.missing";
}

public sealed class RequireCorrelationMiddleware : IPipelineMiddleware
{
    public const string ErrorCode = "CONTOSO_VALIDATION_001";

    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        if (!context.TryGetBaggage(BaggageKeys.Correlation, out _))
        {
            // 短路：写入失败结果，不调用下游
            context.SetOutcome(Error.Validation(ErrorCode, CorrelationMessages.Missing));
            return ValueTask.CompletedTask;
        }

        return nextStep(context);
    }
}
```

用特性桩把它挂进 Validate 阶段，再分别以缺少和带有 Baggage 的调用验证：

```csharp
using Contoso.Correlation;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

public sealed class RequireCorrelationMiddlewareTests
{
    private static StubFeature Feature() => new(TestManifests.Create("xihan.contoso.correlation", requires: ["kernel.hosting@1"]))
    {
        OnConfigure = static context => context.AddMiddleware<RequireCorrelationMiddleware>(PipelineStage.Validate),
    };

    [Fact]
    public async Task MissingCorrelation_ShortCircuits()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature(Feature()));
        var reached = false;

        var result = await host.InvokeAsync(context =>
        {
            reached = true;
            context.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        });

        Assert.Equal(RequireCorrelationMiddleware.ErrorCode, result.Error?.Code);
        Assert.False(reached);
    }

    [Fact]
    public async Task Correlation_PassesThrough()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature(Feature()));

        var result = await host.InvokeAsync(
            static context =>
            {
                context.SetOutcome(Result.Success());
                return ValueTask.CompletedTask;
            },
            baggage: new Dictionary<string, string> { [BaggageKeys.Correlation] = "c-1" });

        Assert.True(result.IsSuccess);
    }
}
```

`deadline` 参数可以验证截止时间：传入已过去的时刻，结果是 `KERNEL_TIMEOUT_001`。测试里断言错误码而不是文案：文案由消息键在 Egress 翻译，测试宿主没有装配本地化时结果里只有键。

## 测一个操作

手写登记一个操作（不用生成器时，序列化元数据来自源生成的 `JsonSerializerContext`），再以类型化方式调用：

```csharp
using System.Text.Json.Serialization;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

public sealed record EchoRequest(string Text);

public sealed record EchoResponse(string Text, string? Tenant);

[JsonSerializable(typeof(EchoRequest))]
[JsonSerializable(typeof(EchoResponse))]
internal sealed partial class EchoJsonContext : JsonSerializerContext;

public sealed class EchoOperation : IOperation<EchoRequest, EchoResponse>
{
    public ValueTask<Result<EchoResponse>> HandleAsync(EchoRequest request, InvocationContext context)
    {
        var tenant = context.TryGetBaggage(BaggageKeys.Tenant, out var value) ? value : null;
        Result<EchoResponse> result = new EchoResponse(request.Text, tenant);
        return ValueTask.FromResult(result);
    }
}

public sealed class EchoOperationTests
{
    [Fact]
    public async Task Echo_ReturnsTextAndTenant()
    {
        var feature = new StubFeature(TestManifests.Create("xihan.contoso.echo", requires: ["kernel.hosting@1"]))
        {
            OnConfigure = static context => context.AddOperation<EchoOperation, EchoRequest, EchoResponse>(
                new OperationContract("echoes.create"), EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse),
        };
        await using var host = await TestHost.StartAsync(b => b.UseFeature(feature));

        var result = await host.InvokeOperationAsync<EchoRequest, EchoResponse>(
            "echoes.create", new EchoRequest("hi"), new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme" });

        var response = result.ValueOrThrow();
        Assert.Equal("hi", response.Text);
        Assert.Equal("acme", response.Tenant);
    }
}
```

用 `[Operation]` 与 `Kernel.Generators` 的项目，在特性的 `Configure` 里调用 `context.AddGeneratedOperations()`，测试照样经 `InvokeOperationAsync` 调用。要测完整的子系统组合，直接在 `TestHost.StartAsync` 里 `UseStandardProfile()` 再加被测特性，配置以键值对传入；要测契约上的权限与策略，装配授权能力并在 Baggage 里给出主体（[授权与权限](/security/authorization)）。

## SQLite 文件库

用例用临时的 SQLite 文件库时，宿主停止后先清这个库的连接池再删文件：

```csharp
using var connection = new SqliteConnection(connectionString);
SqliteConnection.ClearPool(connection);
File.Delete(path);
```

连接池按连接串分组，用例自己在连接串后追加了参数（如 `Default Timeout`）的，那个连接串也清一次。不要在用例里调 `SqliteConnection.ClearAllPools()`：它清的是整个进程的连接池，xUnit 并行跑的别的用例此刻正在打开的连接会被当作泄漏回收并关掉句柄，那个用例随即以 `ObjectDisposedException` 失败，负载越高越容易碰到。给连接串加 `Pooling=False` 也能避开，但每次开连接都要重新打开文件，并行负载下明显变慢。用 SqlSugar 的宿主停止后，清池之前先 `GC.Collect(); GC.WaitForPendingFinalizers();`：它在 SQLite 上读数据后不释放命令，预编译语句要等终结器才释放，在此之前库仍占着文件，删除会偶发「文件被另一进程使用」。框架自己的数据库测试项目都按这两条做。

测试里 `Http` 活动、CloudEvents Webhook 或 S3 命名实例要连本机的服务时，配置 `XiHan:Security:Outbound:AllowLoopback=true`（[出站地址策略](/security/outbound)）。用 `ConfigurePrimaryHttpMessageHandler` 换主处理器的替身不经过策略，不用配。

## 常见问题

### 测试宿主和 WebApplicationFactory 怎么选

要测的是管道、特性、操作的行为时用 `TestHost`：快、没有端口、断言的是 `Result`。要测 HTTP 路由、状态码、Problem Details 的形状时用 `WebApplicationFactory` 起真实宿主，两者可以并存。

### 用例之间互相影响

每个用例起自己的 `TestHost`，用完 `await using` 释放；不要在类级别共享宿主。同一进程里的静态状态（如内存 Provider 的静态字典）才会串，框架的默认实现都是容器内实例，不会。

### 在根容器解析 Scoped 服务报错

`TestHost` 开了 `ValidateScopes`，这是在模拟真实宿主一调用一作用域的语义。Scoped 服务（仓储、工作单元）要在调用里从 `context.Services` 解析，或在测试里显式 `CreateScope()`。

### 怎样给操作传截止时间或租户

`InvokeAsync` 有 `deadline` 参数；租户、主体、关联 ID 这类上下文都经 `baggage` 传入，键用 `BaggageKeys` 常量。`InvokeOperationAsync` 没有 `deadline`，要测操作的超时用 `InvokeAsync` 包一层或在配置里把 `XiHan:Kernel` 的时限调小。

### 要不要测框架自己的中间件

不要重复测认证、校验、幂等这些内建阶段，一致性套件与框架仓库已经覆盖；测你的特性在它们之上的行为，比如契约的权限码、你的校验规则、你的事件处理器。

## 相关包与下一步

- 包参考：[Testing](/packages/testing)、[Kernel](/packages/kernel)、[Kernel.Generators](/packages/kernel-generators)
- 相关章节：[执行管道](/startup/pipeline)、[操作契约](/web/operations)、[结果与错误处理](/web/errors)、[出站地址策略](/security/outbound)
- 下一步：[一致性套件与门禁](/testing/conformance)讲怎样让 Provider 与插件跑同一套用例，以及框架仓库的架构门禁
