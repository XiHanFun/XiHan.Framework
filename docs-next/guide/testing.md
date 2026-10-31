# 测试与一致性套件

本页讲怎样用测试基座在内存里装配应用、测试特性、中间件与操作，怎样让 Provider 与插件跑一致性套件，以及框架仓库的架构测试与门禁，适用于应用开发者和写 Provider、插件、框架扩展的人。完整 API 见 [Testing 测试基座](../packages/testing) 与 [Conformance 一致性套件](../packages/conformance)。

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

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />
    <PackageReference Include="xunit.v3" Version="4.0.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="4.0.1" PrivateAssets="all" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.12.0" />
    <PackageReference Include="XiHan.Framework.Testing" Version="5.0.0-preview.1" />
  </ItemGroup>
</Project>
```

.NET 10 的 MTP 模式下，`dotnet test` 用 `--solution` 或 `--project` 指定目标（在仓库根执行）：

```bash
# 整个解决方案
dotnet test --solution next/XiHan.Framework.Next.slnx --configuration Release --no-build

# 单个测试项目
dotnet test --project next/test/XiHan.Framework.Caching.InMemory.Tests/XiHan.Framework.Caching.InMemory.Tests.csproj --configuration Release

# 与 CI 相同：采集 Cobertura 覆盖率
dotnet test --solution next/XiHan.Framework.Next.slnx --configuration Release --no-build -p:TestingPlatformCommandLineArguments="--coverage --coverage-output-format cobertura"
```

::: warning MTP 模式不要退回 VSTest
`global.json` 选了 MTP 之后，`dotnet test` 走 VSTest 目标会被 .NET 10 SDK 拒绝。不要删除 `global.json` 的 `test.runner`，也不要去掉 `--solution` / `--project` 来绕过运行错误。
:::

## 测试宿主

`TestHost.StartAsync(configure, configuration, services)` 在内存里装配并启动一个应用：自建服务集合（`ValidateScopes = true`，在根容器解析 Scoped 服务直接报错）与内存配置，调用 `AppBuilder` 上的 `UseFeature` 后启动全部特性；没有 HTTP，也不经过任何协议适配器。启动失败时先释放容器再抛出，`DisposeAsync` 按与启动相反的顺序停止特性，重复释放是空操作。

宿主上的调用都在新的服务作用域里、以完整管道执行，与真实宿主里一请求一作用域的语义一致：

| 方法 | 用途 |
| --- | --- |
| `InvokeAsync(handler, deadline, baggage)` | 以一个 `PipelineStep` 作为 Handle 阶段，返回调用结果；用来测中间件 |
| `InvokeOperationAsync<TRequest, TResponse>(operationId, request, baggage)` | 类型化调用操作，经完整管道 |
| `InvokeOperationRawAsync(OperationRequest, baggage)` | 以 XGI 消息（JSON 载荷）调用操作，返回 `OperationResponse` |

`StubFeature` 是可注入 `OnConfigure` / `OnStart` / `OnStop` 行为、按顺序记录 `configure` / `start` / `stop` 事件的特性桩；`TestManifests.Create(id, provides, requires, role, kind, bindings)` 用 `id@version` 文本构造清单；`TestInvocationContext.Create(...)` 给不需要宿主的单元测试一个独立的调用上下文。

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

用特性桩把它挂进管道，再分别以缺少和带有 Baggage 的调用验证：

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

`deadline` 参数可以验证截止时间：传入已过去的时刻，结果是 `KERNEL_TIMEOUT_001`。

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

用 `[Operation]` 与 `Kernel.Generators` 的项目，在特性的 `Configure` 里调用 `context.AddGeneratedOperations()`，测试照样经 `InvokeOperationAsync` 调用（[操作契约](./operations)）。要测完整的子系统组合，直接在 `TestHost.StartAsync` 里 `UseStandardProfile()` 再加被测特性，配置以键值对传入，如 `new KeyValuePair<string, string?>("XiHan:Kernel:StartTimeoutSeconds", "5")`。

## 一致性套件

每个子系统抽象都配一个 `<领域>.Conformance` 包：一组 xUnit 抽象测试类，Provider 的测试项目继承基类、实现工厂方法，套件里的全部用例随之执行。它是「换 Provider 只改包引用与配置」的保证：框架的每个默认实现与 Provider 都在仓库里跑同一套用例，自己写的 Provider 也应当继承它（[扩展与二次开发](./extending)）。

1. 新建测试项目，引用被测 Provider、对应的 `<领域>.Conformance` 包与 `xunit.v3`（套件包本身只引用 xUnit 的核心与断言包，不带运行器）。
2. 继承基类，实现工厂方法；按需覆写时限、配置等虚成员。
3. 运行 `dotnet test --project <测试项目>`。

`Caching.InMemory` 的测试项目就这样跑完整套缓存用例：

```csharp
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.Conformance;
using XiHan.Framework.Caching.InMemory;

public sealed class InMemoryCacheStoreConformanceTests : CacheStoreConformance
{
    protected override ICacheStore CreateStore() => new InMemoryCacheStore(new InMemoryCachingOptions(), TimeProvider.System);
}
```

以特性为入口的套件（持久化、消息）实现的是 `CreateFeature()`，套件自己用 `TestHost` 装配并走完整管道：

```csharp
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Messaging.InMemory;

public sealed class InMemoryMessageBrokerConformanceTests : MessageBrokerConformance
{
    protected override IFeature CreateFeature() => new InMemoryMessagingFeature();
}
```

各套件的基类、工厂方法与用例覆盖面见 [Conformance 一致性套件](../packages/conformance)。依赖真实服务的套件（Redis、RabbitMQ、Kafka、PostgreSQL、S3、Nacos 等）只在设置了对应环境变量（`XIHAN_TEST_REDIS`、`XIHAN_TEST_RABBITMQ`、`XIHAN_TEST_KAFKA`、`XIHAN_TEST_POSTGRES`、`XIHAN_TEST_S3`、`XIHAN_TEST_NACOS` 等）时运行，没有就跳过；CI 以服务容器提供 Redis、PostgreSQL 与 RabbitMQ。

## Plugins.Conformance 与 xihan-conformance

插件协议有两份黑盒套件，任何语言写的插件都用它们验证：.NET 的 `XiHan.Framework.Plugins.Conformance`，和规范仓库 [framework-spec](https://github.com/XiHanFun/framework-spec) 里语言中立的 Go 运行器 `xihan-conformance`。两者都以宿主身份启动插件、按 `spec/plugin/` 的规范核对，用例相同。

.NET 测试项目以插件的构建产物目录继承 `PluginProtocolConformance`（进程外）或 `WasmPluginConformance`（Wasm）：

```csharp
using XiHan.Framework.Plugins.Conformance;

public sealed class EchoPluginConformanceTests : PluginProtocolConformance
{
    protected override string PluginDirectory => Path.Combine(AppContext.BaseDirectory, "plugins", "echo");

    // 故意崩溃的操作不调用
    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal) { "echoes.crash" };

    protected override IReadOnlyDictionary<string, string> SampleRequests => new Dictionary<string, string>(StringComparer.Ordinal) { ["echoes.create"] = """{"text":"conformance"}""" };
}
```

跑 Wasm 套件的测试项目在 Windows 上要设 `<CETCompat>false</CETCompat>`：套件会故意触发陷阱，影子栈强制时整个测试进程会 fail-fast（[插件](./plugins#wasm-插件贡献中间件)）。

非 .NET 的插件用 Go 运行器，不需要 .NET 环境：

```bash
go install github.com/xihanfun/framework-spec/conformance/cmd/xihan-conformance@latest
xihan-conformance plugin --dir ./bin --exclude echoes.crash --sample 'echoes.create={"text":"hi"}'
xihan-conformance plugin --dir ./bin --json
xihan-conformance vectors
```

| 选项 | 含义 |
| --- | --- |
| `--dir` | 插件目录（含 `xihan.manifest.json` 与 `entry` 指向的可执行文件），必填 |
| `--timeout` | 每一步的截止时间，缺省 `15s` |
| `--config` | `Configure` 下发的配置节 JSON，缺省 `{}` |
| `--sample op=json` | 某个操作的请求样例，可重复；没有样例的操作以 `{}` 调用 |
| `--exclude op` | 不调用的操作，可重复 |
| `--case name` | 只跑指定用例，可重复 |
| `--json` | 结果输出为 JSON |
| `--quiet` | 不转发插件的 stderr |

用例：`handshake`（握手行、版本协商、原语层版本、清单一致、声明在清单范围内）、`refusal`（没有共同版本时以 `FailedPrecondition` 拒绝）、`lifecycle`（`Configure` / `Start` / `Health` / `WatchHealth` / `Stop`，进程在宽限内退出）、`unknown-operation`（`Unimplemented`）、`declared-operations`（每个声明的操作都返回 `status`）、`event`（订阅投递得到确认，没有订阅时跳过）、`stdin`（关闭 stdin 后进程自行退出）。每个用例单独启动一次插件进程。退出码：`0` 全部通过（含跳过），`1` 有用例失败，`2` 参数或目录错误。`xihan-conformance vectors` 自检 `conformance/vectors/` 下的数据向量。

两份套件都不提供宿主回调服务，`Host.Invoke` / `Host.Publish` 的令牌与权限、崩溃重启由宿主实现的测试覆盖（`Plugins.OutOfProcess.Tests`、`Plugins.Wasm.Tests`）。

## 架构测试与门禁

这一节面向向框架仓库提交扩展的人：`next/` 下的每个正式包都要通过下列门禁，CI（`.github/workflows/next-ci.yml`）全部是必过项，不放宽、不绕过、不加 `NoWarn`。

| 门禁 | 位置 | 检查什么 |
| --- | --- | --- |
| 编译 | `next/Directory.Build.props` | `TreatWarningsAsErrors`、Nullable、`AnalysisLevel=latest-recommended`、`EnforceCodeStyleInBuild`（命名 IDE1006 与未使用 using IDE0005 在构建期即错误）、NuGet 高危 / 严重漏洞（NU1903 / NU1904）中断构建 |
| 公开面与包 | `next/src/Directory.Build.props` | `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` 必须与公开面一致、包验证、AOT 分析（`IsAotCompatible` 缺省为真） |
| 分析器 | `Kernel.Analyzers` | XH0001–XH0005：`[ApiLevel]`、`XiHan` 前缀、禁用词、Internal API、Ring 0/1 零反射（[Kernel.Analyzers](../packages/kernel-analyzers)） |
| 架构测试 | `next/test/XiHan.Framework.Architecture.Tests` | 见下表 |
| 冒烟 | CI | `dotnet pack` 触发包验证；`samples/SimpleApp` 以 Native AOT 发布 |

架构测试扫描源码目录与项目文件，按包 ID 推出环层再判定：

| 测试 | 规则 |
| --- | --- |
| `ProjectReferences_PointToLowerRingsOrSameRingAbstractions` | 只能引用更低环层的包；同环只能引用对方的 `.Abstractions`；工具链与组合包例外 |
| `Primitives_HasNoPackageOrProjectReferences` | 原语层没有任何包或项目引用 |
| `KernelAndAbstractions_ReferenceOnlyMicrosoftExtensionsAbstractions` | Ring 0/1 与抽象包只引用 `Microsoft.Extensions.*` 抽象包或白名单里的包 |
| `PublicSignatures_DoNotLeakThirdPartyTypes` | 公开签名只出现 BCL、`Microsoft.Extensions` 与框架自身的类型 |
| `EverySourceProject_IsReferencedByArchitectureTests` | 新增的正式包要加进架构测试项目的引用 |
| `PackageIds_FollowGrammar`、`Vocabulary_IsWellFormed` | 包 ID 由术语表与 `spec/conventions/package-ids.md` 的文法推出 |
| `Namespaces_MatchFolders`、`Namespaces_EqualPackageIdWithAtMostOneSegment` | 命名空间等于包 ID，子命名空间最多一层且与目录同名 |
| `PublicTypes_DoNotCarryXiHanPrefix` | 公开类型名不带 `XiHan` 前缀 |
| `SourceFiles_DeclareOneTopLevelType_NamedLikeTheFile` | 一个文件一个顶层类型，文件名与类型名一致（泛型加 `OfT`） |
| `SourceFiles_StartWithCopyrightHeader` | 版权头 |
| `XmlSummaries_UseTheThreeLineForm`、`XmlSummaries_AreOneConciseSentence` | `<summary>` 三行写法、一句话，细节放 `<remarks>` |
| `PrimitivesAndKernel_DoNotUseReflection` | Ring 0/1 源码不使用反射 |
| `EverySourceProject_HasReadmeAndPublicApiFiles` | 每个正式包都有 `README.md` 与两个 PublicAPI 文件 |
| `EveryLocalizedPackage_HasOneFilePerLocale_WithConsistentKeys` 等四项 | 10 种区域的语言文件齐全、占位符一致、按约定嵌入、消息键常量与 `en-US.json` 一一对应、错误调用点只用常量（RFC-0010） |
| `DeclaredHealthItems_AreImplementedInTheSamePackage`、`PackagesRegisteringContributors_DeclareHealthItems` | 清单 `contributes.health` 的条目由同一个包实现，登记了 `IHealthContributor` 的包必须声明条目（插件宿主例外） |
| `SourceProjects_DoNotCallSingleItemMethodsInsideLoops` | 不在循环里调用仓储、缓存存储、消息发布的单条方法，改用批量方法 |
| `DatabaseRecords_DeclareEveryStringColumnLength` | 数据库记录的字符串列都声明 `[StringLength(n)]` 或 `[LongText]` |

## 相关

- [Testing 测试基座](../packages/testing)、[Conformance 一致性套件](../packages/conformance)
- [Kernel.Analyzers](../packages/kernel-analyzers)、[Kernel.Generators](../packages/kernel-generators)
- [扩展与二次开发](./extending)、[插件](./plugins)、[执行管道](./pipeline)、[操作契约](./operations)
- 规范：RFC-0017《进程外插件协议》、RFC-0018《Wasm 插件 ABI》；`spec/conformance/README.md`
