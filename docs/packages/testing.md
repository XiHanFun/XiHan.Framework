# XiHan.Framework.Testing

测试基座：没有 HTTP 的内存测试宿主、可注入行为的特性桩、按能力 ID 文本构造清单与独立调用上下文的工具，可以切换成后端不可达的缓存包装，以及把日志、Span 与指标收进内存的收集器和规范向量的严格读取。供框架自身、Provider 与应用的测试项目引用，也是各一致性套件装配宿主的基础。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Testing` |
| 环层 | 工具链 |
| 能力 | — |
| 特性入口 | —（`StubFeature` 是测试用的特性桩） |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Caching.Abstractions`、`XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.Security.WebAuthn`；`Microsoft.Extensions.Configuration`、`Microsoft.Extensions.DependencyInjection`、`System.Formats.Cbor` |
| AOT | 兼容 |
| 稳定性 | Stable；`FaultyCache`、`ScriptedUnitOfWork`、`ScriptedRepository<TEntity, TKey>`、`LogCapture`、`LogCaptureEntry`、`SpanCapture`、`SpanSamplingRequest`、`MetricCapture`、`MetricCaptureEntry`、`SpecVectors`、`FrameworkWorkspace` 为 Preview |

## 概述

`TestHost` 自建 `ServiceCollection` 与内存配置，把它们交给 `AppBuilder`，执行调用方的装配回调后构建容器（`ValidateScopes = true`）并启动 `App`；启动失败时先释放容器再抛出。它的调用方法都在新的服务作用域里经完整管道执行，不经过任何协议适配器，所以测的是能力图谱、管道阶段与操作契约本身，而不是 HTTP 绑定。`DisposeAsync` 停止应用并释放容器，幂等。

正式包不引用本包；一致性套件里以宿主装配被测特性的那些（持久化、消息、本地化）引用它。

## 何时使用

- 测自己的特性、中间件与操作：用 `TestHost` 装配，用 `StubFeature` 注入要测的那一小段装配逻辑。
- 测 HTTP 路由、模型绑定与 Problem Details：用 ASP.NET Core 的 `WebApplicationFactory` 测真实宿主，本包不覆盖协议层。
- 写 Provider：先继承对应的一致性套件（[Conformance](./conformance)），再用本包补 Provider 特有行为的测试。
- 测组件在缓存后端故障时的行为（读取是否绕过缓存、写入是否拒绝）：用 `FaultyCache` 包住一个 `ICache`。
- 测自己开工作单元的代码（[管道外的单例存储、后台循环](/persistence/unit-of-work#在管道外开工作单元)）在数据库故障时怎样收尾：用 `ScriptedUnitOfWork` 与 `ScriptedRepository<TEntity, TKey>`。
- 断言组件写了哪条日志（告警有没有记、异常有没有随日志带出、敏感文本有没有进消息）：用 `LogCapture`。
- 断言组件开了哪条 Span、记了哪个指标（名字、标签、状态、采样器看到的创建）：用 `SpanCapture` 与 `MetricCapture`。
- 在框架仓库（或带着规范子模块的 Provider、SDK 仓库）里回放规范的一致性向量：用 `SpecVectors` 严格读成对象模型。应用自己的测试用不上它。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Testing --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[环境与安装](/getting-started/installation)。测试项目的其余配置（xUnit v3、Microsoft.Testing.Platform）见[测试与一致性套件](/testing/basics)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `TestHost` | `StartAsync(configure, configuration, services, cancellationToken)` 装配并启动；`App`、`Services`、`Configuration`；`InvokeAsync(handler, deadline, baggage)`、`InvokeOperationAsync<TRequest, TResponse>(operationId, request, baggage)`、`InvokeOperationRawAsync(request, baggage)`；`DisposeAsync` |
| `StubFeature` | 实现 `IHostedFeature` 的特性桩：构造时给清单，`OnConfigure` / `OnStart` / `OnStop` 注入行为，`Events` 按顺序记录 `configure`、`start`、`stop` |
| `TestManifests` | `Create(id, provides, requires, role, kind, bindings)`：能力写成 `id@version` / `id@^version?` 文本，版本取框架版本、原语层 1、稳定性 Stable |
| `TestInvocationContext` | `Create(services, deadline, baggage, cancellationToken)`：不依赖宿主的调用上下文，未给服务提供器时用什么都解析不到的空提供器 |
| `WebAuthnTestAuthenticator` | 测试用的通行密钥认证器：构造时选算法（ES256、PS256、RS256），`Register` / `Authenticate` 按 WebAuthn 的格式生成响应，`SignCount` 原样写进认证器数据；`CredentialId`、`PublicKey` 与注册核对得到的相同 |
| `WebAuthnTestCeremony` | 一次仪式的输入（挑战、来源、RP ID）与篡改项（仪式类型、跨源、UP / UV / BE / BS 标志、证明格式、改坏签名、用户句柄）；缺省值产生合法的响应 |
| `FaultyCache` | 包住一个 `ICache`（构造参数 `inner`）：`Failing` 为真时每个操作都抛出携带 `CACHING_UNAVAILABLE_001` 的 `FrameworkException`，与 Redis 断线时一样；为假时原样转给内层缓存，`Store` 是内层的存储 |
| `ScriptedUnitOfWork` | 按脚本失败的 `IUnitOfWork`：`BeginFailure`、`CommitFailure`、`RollbackFailure`、`DisposeFailure` 预设各步抛出的异常，`CommitResult` 预设提交返回的结果，`StaysActiveAfterFailedCommit` 模拟提交失败后没有自行回滚的实现；`Calls` 按顺序记下 `begin`、`commit`、`rollback`、`dispose`，`Tokens` 记下各步收到的取消令牌 |
| `ScriptedRepository<TEntity, TKey>` | 按脚本返回或抛出的 `IRepository<TEntity, TKey>`：`Failure` 让每个操作都抛出它，`Found` 是查找与查询返回的那一行（为空即查不到），`WriteResult` 是每条写入返回的结果 |
| `LogCapture` | 把日志收进内存的日志工厂，同时是 `ILoggerFactory` 与 `ILoggerProvider`：`Entries` 是按写入顺序的快照，`Capacity` 是条数上限（缺省 `DefaultCapacity` = 10000，构造时可给） |
| `LogCaptureEntry` | 一条日志：`Level`、`Category`、`Message`（格式化后的文本）、`Exception`（没有带异常时为空） |
| `SpanCapture` | 只听给定源名的 `ActivityListener`，创建时登记、释放时移除：`Started`、`Stopped` 是开始与结束的 Span 的快照，`SamplingRequests` 是采样器被询问的创建；`Spans(traceId[, name])` 取一条链路上结束的 Span；`Sampler`（`init`）换采样器，缺省同 OpenTelemetry 的 ParentBased(AlwaysOn)；`Capacity`（缺省 `DefaultCapacity` = 10000，构造时可给） |
| `SpanSamplingRequest` | 采样器被询问的一次创建：`Source`、`Name`、`Kind`、`TraceId`、`Tags`（创建时给出的标签） |
| `MetricCapture` | 只打开给定 Meter 名的仪表的 `MeterListener`，创建时启动、释放时停止：`Entries` 是测量值的快照，`RestrictToCurrentFlow()` 之后只收当前异步流（及由它派生的流）里记下的；`Capacity`（缺省 `DefaultCapacity` = 10000，构造时可给） |
| `MetricCaptureEntry` | 一个测量值：`Meter`、`Instrument`、`Value`（换成 `double`）、`Tags`（重复的标签键取最后一个） |
| `SpecVectors` | 规范一致性向量的读取：`Directory` 是 `<工作区>/spec/conformance/vectors`；`PathOf(area, fileName)` 只拼路径，给要按 `JsonDocument` 自己读的测试；`Read<T>(area, fileName)` 按反射严格读，`Read<T>(area, fileName, resolver)` 按源生成的上下文严格读 |
| `FrameworkWorkspace` | `Root`：从测试的输出目录逐级向上第一个含 `XiHan.Framework.slnx` 的目录（仓库里的 `framework/`），第一次读取时找、之后缓存，找不到抛 `InvalidOperationException` |

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

中间件与操作的完整测试写法见[测试与一致性套件](/testing/basics)。

`FaultyCache` 让缓存后端在测试中途「断线」。下面验证一个[按范围失效的缓存区](/caching/basics#按范围失效的缓存区)在缓存故障时读取照常、失效报错（内层缓存来自 `Caching.InMemory`）：

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

public sealed class PriceCacheOutageTests
{
    [Fact]
    public async Task CacheOutage_ReadsTheSource_AndInvalidationThrows()
    {
        var options = new InMemoryCachingOptions();
        var cache = new FaultyCache(new InMemoryObjectCache(new InMemoryCacheStore(options, TimeProvider.System), options, TimeProvider.System));
        var prices = new GenerationCache<string>(cache, "test:prices", TimeSpan.FromMinutes(5), NullLogger.Instance);
        var loads = 0;
        ValueTask<Result<IReadOnlyList<string>>> LoadAsync(IReadOnlyList<GenerationCacheKey> keys, CancellationToken cancellationToken)
        {
            loads++;
            return ValueTask.FromResult(Result.Success<IReadOnlyList<string>>([.. keys.Select(static key => key.Entry)]));
        }

        // 缓存后端不可达：读取绕过缓存直接查源，结果仍然正确
        cache.Failing = true;
        var read = await prices.GetOrLoadManyAsync([new GenerationCacheKey("acme", "books")], LoadAsync, CancellationToken.None);
        Assert.Equal(["books"], read.ValueOrThrow());
        Assert.Equal(1, loads);

        // 失效把故障抛给调用方，由它决定拒绝写入还是记日志
        var failed = await Assert.ThrowsAsync<FrameworkException>(async () => await prices.InvalidateAsync(["acme"], CancellationToken.None));
        Assert.Equal(CachingErrors.StoreUnavailable, failed.Code);
    }
}
```

测宿主里的组件时，在装配特性之前把它登记为 `ICache` 单例（`TestHost.StartAsync(..., services: s => s.AddSingleton<ICache>(cache))`）。几个宿主登记同一个 `FaultyCache`（或同一个 `ICache`）实例时，它们共用一份缓存，可以模拟多实例共用的缓存后端：一个宿主写入，另一个宿主应当立即看到变化。

两个替身验证管道外的代码在数据库断开时的收尾：读取抛出、回滚再抛，被测代码交还的应当是原来的失败。

```csharp
var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed.") };
await using var services = new ServiceCollection()
    .AddScoped<IUnitOfWork>(_ => unitOfWork)
    .AddScoped<IRepository<Order, Guid>>(_ => new ScriptedRepository<Order, Guid> { Failure = new TimeoutException() })
    .BuildServiceProvider();

// OrderPurger 见「在管道外开工作单元」：读取抛出的 TimeoutException 原样抛出，没有被回滚的异常盖掉
var purger = new OrderPurger(services, TimeProvider.System, NullLogger<OrderPurger>.Instance);
await Assert.ThrowsAsync<TimeoutException>(() => purger.PurgeAsync("acme", CancellationToken.None));
Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
```

`LogCapture` 断言日志。登记为 `ILoggerFactory` 单例时宿主里的组件都写到这里；只测一个对象时直接从它取日志器：

```csharp
using var logs = new LogCapture();
await using var host = await TestHost.StartAsync(b => b.UseFeature(feature), services: s => s.AddSingleton<ILoggerFactory>(logs));

// 跑被测的调用之后：告警记在组件自己的类别下，异常随日志带出
var warning = Assert.Single(logs.Entries, e => e.Level == LogLevel.Warning && e.Category == typeof(OrderPurger).FullName);
Assert.IsType<TimeoutException>(warning.Exception);
```

要经日志构建器接入时（`LoggerFactory.Create(b => b.AddProvider(logs))`、`builder.Logging.AddProvider(logs)`）它也是 `ILoggerProvider`；构建器设的最低级别照常过滤。

`SpanCapture` 与 `MetricCapture` 断言组件发出的遥测。监听器是进程级的：Span 按本用例的 trace-id 取，指标限定到本用例的异步流；靠采样结果断言的用例与别的挂监听器的用例放进同一个不并行的集合。下面核对一次调用在 Observe 阶段开了 `xihan.invoke`、记了一次 `xihan.invocations`：

```csharp
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

[Collection("telemetry")]
public sealed class InvocationTelemetryTests
{
    [Fact]
    public async Task Invocation_RecordsTheSpan_AndCountsIt()
    {
        using var spans = new SpanCapture([FrameworkDiagnostics.SourceName]);
        using var metrics = new MetricCapture([FrameworkDiagnostics.SourceName]);
        metrics.RestrictToCurrentFlow();

        var feature = new StubFeature(TestManifests.Create("xihan.test.stub", requires: ["kernel.hosting@1"]));
        await using var host = await TestHost.StartAsync(b => b.UseFeature(feature));
        var traceId = "";
        var result = await host.InvokeAsync(context =>
        {
            traceId = context.TraceId;
            context.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        });

        Assert.True(result.IsSuccess);
        var span = Assert.Single(spans.Spans(traceId, "xihan.invoke"));
        Assert.Equal("success", span.GetTagItem("xihan.outcome"));
        var counted = Assert.Single(metrics.Entries, static m => m.Instrument == "xihan.invocations");
        Assert.Equal(1, counted.Value);
    }
}
```

`SpecVectors` 回放规范的一致性向量。读取是严格的：对象模型要认得文件里的每个成员（顶层的 `$comment` 除外），没有缺省值的构造参数与 `required` 成员都要出现。下面回放缓存键的向量（`caching/cache-keys.json`）：

```csharp
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Testing;
using Xunit;

public sealed class CacheKeyVectorTests
{
    private static readonly CacheKeyVectors _vectors = SpecVectors.Read<CacheKeyVectors>("caching", "cache-keys.json");

    [Fact]
    public void MaxBytes_IsTheOneInTheVectors() => Assert.Equal(CacheKey.MaxBytes, _vectors.MaxBytes);

    [Fact]
    public void IsValid_AcceptsTheValidKeys()
        => Assert.All(_vectors.Keys.Valid, static key => Assert.True(CacheKey.IsValid(key.Text), key.Name));

    // 文件里的每个成员都要在模型里：utf8Bytes 与 utf16Units 这里不用，也得写上
    private sealed record CacheKeyVectors(int MaxBytes, CacheKeyCases Keys, CacheKeyCases Prefixes);

    private sealed record CacheKeyCases(CacheKeyCase[] Valid, CacheKeyCase[] Invalid);

    private sealed record CacheKeyCase(string Name, string Text, int Utf8Bytes, int Utf16Units);
}
```

## 行为与限制

- 每次 `InvokeAsync` / `InvokeOperation*Async` 一个独立的服务作用域；释放后再调用抛 `ObjectDisposedException`。
- 特性启动抛出异常时 `StartAsync` 抛出内核的 `FrameworkException`（`KernelErrors.FeatureStartFailed`，即 `KERNEL_HOSTING_002`），容器已释放。
- 配置只有传入的内存键值对，不读 `appsettings.json` 与环境变量；需要时在 `configuration` 参数里给出。
- 调用不经过 Ingress 的协议绑定：`Accept-Language` 协商、HTTP 认证头之类由宿主适配器做的事不发生，Baggage 以 `baggage` 参数直接给出。
- `FaultyCache` 只模拟 `ICache` 层面的故障：`Store` 原样返回内层存储，直接经 `ICacheStore` 或 `ISharedCache` 的读写不受 `Failing` 影响。`Failing` 可以在任何线程随时切换。
- `ScriptedUnitOfWork` 不写入、不派发领域事件，`HasTransaction` 恒为假；抛出的步骤让状态停在原处（开始抛出仍是未开始，提交与回滚抛出仍是活动），与关系型 Provider 一致。登记成作用域服务时随作用域释放。`ScriptedRepository<TEntity, TKey>` 每个操作先看取消令牌，再看 `Failure`，不核对工作单元是否活动。
- `LogCapture` 全部级别都启用，不支持作用域（`BeginScope` 返回空）；最多留 `Capacity` 条，满了丢掉最早的。`Entries` 每次读取是一份新副本，可以多线程同时写入与读取。`AddProvider` 抛出 `NotSupportedException`（日志只收在这里，不转发）；释放之后写入的日志照样记下。
- `SpanCapture` 与 `MetricCapture` 的监听器是进程级的：同一进程里并行的测试经过同一个源或 Meter 时，它们的 Span 与测量值也收在这里。Span 用 `Spans(traceId)` 只取本用例那条链路的；指标在 `RestrictToCurrentFlow()` 之后只收当前异步流里记下的，同一条流可以绑几个收集器，另起的后台线程、不带执行上下文的回调记下的这时收不到。源的采样结果取全部监听器里最宽的那个，靠采样结果断言的用例与别的挂监听器的用例放进同一个不并行的集合。
- 两个收集器每样最多留 `Capacity` 条（Span 的开始、结束与采样询问各算各的），满了丢掉最早的；读到的都是快照，可以多线程同时写入与读取。释放之后不再收，正在途中的回调也不收，已经收到的仍可读取（与释放后照样收的 `LogCapture` 不同）。
- `SpecVectors` 与 `FrameworkWorkspace` 只在框架仓库的检出里（或带着规范子模块、目录布局相同的仓库里）找得到目标。读法一律严格：成员名按 Web 缺省（camelCase、不区分大小写），文件里有、模型里没有的成员失败，同一对象里重名的成员失败，没有缺省值的构造参数与 `required` 成员缺了失败，只去掉顶层的 `$comment`（别处的 `$comment` 是向量的内容）。同一个文件由几个测试类分别回放时，各自把别的类回放的成员写成 `JsonElement`，可以不出现的成员给缺省值。
- `SpecVectors.Read` 的报错都带文件路径：文件不存在抛 `FileNotFoundException`（多半是没有检出规范子模块，`git submodule update --init framework/spec`），内容为空、只有 `null` 或顶层不是对象抛 `InvalidOperationException`，与模型对不上抛 `JsonException`；目录名与文件名必须是单独一段，空白、`.`、`..`、含 `/` 或 `\` 抛 `ArgumentException`。按反射读的重载标了 `RequiresUnreferencedCode` / `RequiresDynamicCode`，AOT 的测试项目改用带 `resolver` 的重载。

## 相关

- [测试与一致性套件](/testing/basics)
- [Conformance 一致性套件](./conformance)、[Kernel](./kernel)
