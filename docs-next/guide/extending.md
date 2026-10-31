# 扩展与二次开发

本页讲 5.0 的三条扩展轴各写什么，以及写一个 Provider、一个管道中间件、一个应用能力包的完整步骤和命名规则，适用于给框架补实现、写业务能力包或维护框架扩展包的人。清单与能力图谱见[能力包与清单](./modularity)，测试做法见[测试与一致性套件](./testing)。

## 三条扩展轴

| 轴 | 写什么 | 怎样装配 | 适用 |
| --- | --- | --- | --- |
| 能力包 | 实现 `IFeature` 的特性与它的清单：子系统的 Provider、应用自己的业务能力包（操作、中间件、权限、消息处理） | 随应用编译，`UseFeature<T>()`；内核按清单的能力 ID 求解能力图谱后显式装配 | 绝大多数扩展 |
| 插件 | `kind: plugin` 的独立制品：进程内程序集、Wasm 模块或任意语言的子进程 | 运行期按目录发现、宿主授予权限、隔离运行（[插件](./plugins)） | 第三方代码、不可信代码、跨语言实现、远程 Provider |
| 协议 | 协议适配器 `Protocols.*`：把操作登记表里的契约发布到一种线协议上 | 作为能力包装配，在宿主上映射端点 | 需要 HTTP、gRPC、MCP、CloudEvents 之外的线协议 |

能力包是主轴：Provider 与业务代码都是特性，差别只在清单。插件不进入能力图谱，靠握手声明的操作、订阅与（Wasm 的）中间件接进宿主。协议适配器读 `OperationRegistry` 里的 `OperationDescriptor`（契约与 JSON Schema），按 `OperationContract.IsExposedTo(...)` 或 `OperationRegistry.ExposedTo(...)` 过滤，把请求解码为 XGI 的 `OperationRequest`，经 `IOperationInvoker.InvokeAsync` 走完整管道，再把 `OperationResponse` 编码回线协议；`Expose` 的协议标志是固定枚举（`Http`、`Grpc`、`Mcp`、`CommandLine`、`Ai`），5.0 没有为第三方协议预留标志位。

## 写一个 Provider

Provider 是声明 `role: provider`、提供某个子系统能力的特性。下面以一个从 JSON 文件读授予的 `security.authorization@1` Provider 为例，结构照搬框架里的 `Security.Database` 与 `Caching.Redis`。

### 1. 实现抽象接口

抽象接口在 `<领域>.Abstractions` 里，Provider 只依赖它，不依赖其他 Provider。授权的存储契约是 `IPermissionGrantStore`：

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace Contoso.Security.Files;

public sealed record GrantEntry(string Kind, string Subject, string Permission, string? Tenant = null);

// 源生成的序列化上下文：不走反射，包可以标为 AOT 兼容
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<GrantEntry>))]
internal sealed partial class GrantFileJsonContext : JsonSerializerContext;

public sealed class FileGrantStore(IReadOnlyList<PermissionGrant> grants) : IPermissionGrantStore
{
    public static FileGrantStore Load(string path)
    {
        using var stream = File.OpenRead(path);
        var entries = JsonSerializer.Deserialize(stream, GrantFileJsonContext.Default.ListGrantEntry) ?? [];
        return new FileGrantStore(entries.Select(static e => new PermissionGrant(new GrantSubject(e.Kind, e.Subject), e.Permission, e.Tenant)).ToList());
    }

    public ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
    {
        // 全局授予（TenantId 为空）对任何租户生效，租户授予只对该租户生效
        IReadOnlyCollection<string> permissions = grants
            .Where(g => g.Subject == subject && (g.TenantId is null || (tenantId is not null && g.TenantId == tenantId)))
            .Select(static g => g.Permission)
            .ToHashSet(StringComparer.Ordinal);
        return ValueTask.FromResult(permissions);
    }
}
```

语义以对应的一致性套件为准（这里是主体与权限区分大小写、全局授予对任何租户生效、通配授予 `*` 原样返回）。批量优先：子系统契约里有批量方法的（如 `ICacheStore.SetManyAsync`、`RemoveManyAsync`），Provider 要给出明确的逐项结果、空集合与重复项语义，能用后端原生批量接口时覆写成一次请求。

### 2. 写特性与清单

```csharp
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authorization;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace Contoso.Security.Files;

public sealed record FileSecurityOptions(string Path, TimeSpan HealthProbeInterval)
{
    public static FileSecurityOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SecurityOptions.ConfigurationSection + ":Files");
        var path = section["Path"] ?? throw new FrameworkException("CONTOSO_VALIDATION_001", "XiHan:Security:Files:Path is required.");
        var seconds = double.TryParse(section["HealthProbeIntervalSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : 15;
        return new FileSecurityOptions(path, TimeSpan.FromSeconds(seconds));
    }
}

public sealed class FileSecurityFeature : IHostedFeature
{
    public const string HealthName = "security.files.grants";

    private PeriodicHealthContributor? _health;

    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.security.files",
        Package = "Contoso.Security.Files",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(SecurityCapabilities.Authorization, SecurityCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(AuthorizationMiddleware).FullName!, PipelineStage.Authorize)],
            Config = new ConfigContribution(SecurityOptions.ConfigurationSection),
            Health = [HealthName],
        },
        Entry = typeof(FileSecurityFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        // 子系统错误码的消息目录；同名目录只注册一次
        context.Services.AddMessageCatalog(SecurityMessages.Catalog);
        context.Services.TryAddSingleton(SecurityOptions.FromConfiguration(context.Configuration));
        var options = FileSecurityOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);

        // 只为能力求解选中本特性的能力注册服务与中间件
        if (context.Node.IsSelectedFor(SecurityCapabilities.Authorization))
        {
            context.Services.TryAddSingleton<IPermissionGrantStore>(static sp => FileGrantStore.Load(sp.GetRequiredService<FileSecurityOptions>().Path));
            context.Services.TryAddSingleton<IPermissionChecker, PermissionChecker>();
            context.Services.AddSingleton<IPolicy, AuthenticatedPolicy>();
            context.Services.TryAddSingleton<PolicyCatalog>();
            context.AddMiddleware<AuthorizationMiddleware>(PipelineStage.Authorize);
        }

        // 清单 contributes.health 里的条目由本包实现：周期探测文件是否可读
        context.Services.AddKeyedSingleton(HealthName, (sp, _) => new PeriodicHealthContributor(
            HealthName, ct => Probe(sp, ct), options.HealthProbeInterval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>(), "Grants file is readable"));
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName));
    }

    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _health = services.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName);
        await _health.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_health is not null)
        {
            await _health.StopAsync(cancellationToken);
            _health = null;
        }
    }

    private static ValueTask<Result> Probe(IServiceProvider services, CancellationToken cancellationToken)
    {
        // 打不开就抛出，PeriodicHealthContributor 把条目记为 Degraded 并带上异常消息
        using var stream = File.OpenRead(services.GetRequiredService<FileSecurityOptions>().Path);
        return ValueTask.FromResult(Result.Success());
    }
}
```

清单上的几个要点：

| 字段 | 写法 | 原因 |
| --- | --- | --- |
| `Kind` / `Role` | `Provider` / `Provider` | 同一能力有多个候选时，`XiHan:Providers:<能力ID>` 指定的优先，其次是唯一的 `role: provider`，再次是唯一的 `role: default`；所以 Provider 一装配就顶替内存默认实现 |
| `Provides` | 能力 ID 加接口版本 | 依赖按能力 ID 声明，不按包名；只提供真正实现了的能力，`Security.Database` 就只提供授权、不提供认证 |
| `Requires` | 用到的能力 | 缺失报 `KERNEL_CAPABILITY_001`，循环报 `KERNEL_CAPABILITY_002`；可选依赖写 `optional: true` |
| `Contributes.Pipeline` | 中间件类型全名与阶段 | 与 `AddMiddleware` 的阶段一致，`xihan graph` / `pipeline` 据此展示 |
| `Contributes.Config` | `XiHan:<领域>` | 配置节文法 `^XiHan:[A-Z][A-Za-z0-9]*$`，子键由 Provider 自己约定 |
| `Contributes.Health` | 条目名 `<领域>.<机制>.<对象>` | 声明的条目必须由同一个包以 `IHealthContributor` 实现，反之亦然 |

`context.Node.IsSelectedFor(...)` 是关键：同一能力的多个实现都可能被引用，只有被选中的那个注册服务与中间件，被顶替的特性不装配、不启动（RFC-0002 第 4 条）。

### 3. 消息目录、健康与 AOT

- 文案不写在代码里（RFC-0010）：错误只携带消息键常量与参数。Provider 用的是子系统已有的错误码时，注册子系统的目录（如 `SecurityMessages.Catalog`）；有自己的错误码时，定义 `<领域>Messages` 常量类，在包里放 10 种区域的 `Localization/<区域>.json`，以 `LogicalName="Localization/%(Filename)%(Extension)"` 嵌入，用 `MessageCatalog.FromEmbeddedResources(name, typeof(...))` 建目录并 `AddMessageCatalog`。配置错误与编程错误（如上面缺少 `Path`）抛 `FrameworkException`，消息用英文、不本地化。
- 连接类 Provider（数据库、缓存、消息代理、对象存储）用 `PeriodicHealthContributor(name, probe, interval, clock, loggerFactory, description)`：启动时同步探一次，之后按间隔重探，探针失败或抛出记 `KERNEL_UNAVAILABLE_005` 并报 Degraded，恢复后报 Ready，应用只从 Ready 压成 Degraded 而不退出。容量类实现（内存存储、队列）同步上报，达到容量即 Degraded。间隔的配置键按惯例叫 `HealthProbeIntervalSeconds`，缺省 15。
- AOT：包缺省按 AOT 兼容写（`<IsAotCompatible>true</IsAotCompatible>`，序列化走源生成）。依赖的第三方库不兼容时设为 `false` 并在 README 写明；必须用反射的入口标 `RequiresDynamicCode` / `RequiresUnreferencedCode`，像 `Plugins.InProcess` 的构造函数那样把警告交给调用方。

### 4. 通过一致性套件

为 Provider 建一个测试项目，继承对应的套件基类，全部用例通过才算语义与默认实现一致（[Conformance 一致性套件](../packages/conformance)）：

```csharp
using Contoso.Security.Files;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Conformance;

public sealed class FileGrantStoreConformanceTests : PermissionGrantStoreConformance
{
    protected override ValueTask<IPermissionGrantStore> CreateStoreAsync(IReadOnlyList<PermissionGrant> grants)
        => ValueTask.FromResult<IPermissionGrantStore>(new FileGrantStore(grants));
}
```

再用测试宿主验证能力求解确实选中了它：

```csharp
using Contoso.Security.Files;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Testing;
using Xunit;

public sealed class FileSecurityFeatureTests
{
    [Fact]
    public async Task Provider_SupersedesTheInMemoryDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), $"grants-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """[{"kind":"role","subject":"editor","permission":"orders.write"}]""");

        await using var host = await TestHost.StartAsync(
            static b => b.UseStandardProfile().UseFeature<FileSecurityFeature>(),
            [new KeyValuePair<string, string?>("XiHan:Security:Files:Path", path)]);

        Assert.IsType<FileGrantStore>(host.Services.GetRequiredService<IPermissionGrantStore>());
    }
}
```

### 5. 让应用使用

```csharp
using Contoso.Security.Files;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 认证仍由 Standard 的内存默认实现提供，授权由文件 Provider 顶替
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<FileSecurityFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.Run();
```

```json
{
  "XiHan": {
    "Security": {
      "Files": { "Path": "grants.json", "HealthProbeIntervalSeconds": 15 }
    }
  }
}
```

用其他语言实现 Provider 的做法是远程 Provider：宿主侧写一个 `<领域>.Remote` 适配器声明能力，插件只实现规范规定的操作集（RFC-0019，[Caching.Remote](../packages/caching-remote)）。

## 写一个管道中间件

中间件实现 `IPipelineMiddleware.InvokeAsync(InvocationContext context, PipelineStep nextStep)`：调用 `nextStep(context)` 进入下游，不调用即短路；短路时用 `context.SetOutcome(...)` 写入失败结果，预期内的失败不抛异常。管道固定十一个阶段 `Ingress → Observe → Resilience → Authenticate → Tenant → Authorize → Validate → Idempotency → Transaction → Handle → Egress`，中间件挂在 Handle 之外的某个阶段（[执行管道](./pipeline)）。

1. 写中间件类。它以单例注册，构造函数只注入单例；要用 Scoped 服务时在 `InvokeAsync` 里从 `context.Services` 解析。
2. 在特性的 `Configure` 里 `context.AddMiddleware<T>(stage, placement)`：注册为单例并加入指定阶段，归属为特性的清单 ID；`Placement.Beginning` / `Placement.End` 决定在阶段内的位置，同阶段内的相对顺序用 `PipelineBuilder.Before<TMiddleware, TOther>()` / `After<TMiddleware, TOther>()` 约束。
3. 在清单 `Contributes.Pipeline` 里声明 `new PipelineContribution(typeof(T).FullName!, stage)`，与第 2 步的阶段一致。
4. 用测试宿主分别验证放行与短路（[测一个中间件](./testing#测一个中间件)）。

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

`Error.Validation(code, messageKey, details)` 只携带键与参数，宿主在 Egress 按调用方区域翻译；应用的 `CorrelationMessages.Missing` 要在应用自己的消息目录里有译文（[国际化](./localization)）。Wasm 插件也能贡献中间件，语义是 before / after 两次钩子、看不到载荷、失败即关闭（[插件](./plugins#wasm-插件贡献中间件)）。

## 写一个应用能力包

业务代码同样以特性组织：清单写依赖的能力与贡献点，`Configure` 里登记服务、权限、中间件、操作与消息处理。下面的能力包依赖持久化与授权，贡献一个中间件、一个权限码和生成的操作登记：

```csharp
using Contoso.Correlation;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;

namespace Contoso.Notes;

public sealed class NotesFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.contoso.notes",
        Package = "Contoso.Notes",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
            new CapabilityRequirement(SecurityCapabilities.Authorization, SecurityCapabilities.Version),
        ],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(RequireCorrelationMiddleware).FullName!, PipelineStage.Validate)],
            Permissions = ["notes.write"],
        },
        Entry = typeof(NotesFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        // 权限码先登记，操作契约里的 Permission 才能引用
        context.AddPermission(new PermissionDefinition("notes.write", "Write notes"));
        context.AddMiddleware<RequireCorrelationMiddleware>(PipelineStage.Validate);
        // Kernel.Generators 从 [Operation] 生成的登记代码与 JSON Schema
        context.AddGeneratedOperations();
    }
}
```

操作类与序列化上下文的写法见 [Kernel.Generators](../packages/kernel-generators#使用示例)，完整的业务能力包示例是 `samples/SimpleApp/Orders/OrdersFeature.cs`（依赖四个能力，登记仓储、领域事件到消息的映射、消息处理器、两个权限码与两个操作）。应用能力包通常不写 `Provides`；要让别的能力包按能力 ID 依赖它时再声明，能力 ID 的文法见下一节。

## 命名规则

向框架仓库提交的包由 `spec/conventions/package-ids.md` 的文法与术语表推出，架构测试 `PackageIds_FollowGrammar` 执行：

```text
PackageId = "XiHan.Framework." Segment
Segment   = "Primitives" | "Kernel" [ "." KernelTool ] | "Domain" | "Testing" | "Cli"
          | Subsystem "." Role
          | "Hosting." Host | "Protocols." Protocol | "Plugins." ( Isolation | "Protocol" | "Conformance" ) | "Profiles." Profile
Role      = "Abstractions" | "Conformance" | "Generators" | Mechanism | Technology
```

| 对象 | 规则 | 示例 |
| --- | --- | --- |
| 包 ID | 领域在前、角色在后；实现包按机制（`InMemory`、`FileSystem`、`Database`）或技术（官方拼写，如 `EntityFrameworkCore`、`RabbitMQ`）命名；角色段禁用 `Default`、`Core`、`Common`、`Shared`、`Base`、`Impl`、`Util`、`Utils`、`Helper`、`Manager`、`Bus`、`Engine`；版本不进包名 | `XiHan.Framework.Caching.Redis`、`XiHan.Framework.Persistence.Conformance` |
| 默认实现 | 「默认」是清单的 `role: default`，不进包名，也不进类型名 | `Caching.InMemory` 的 `InMemoryCachingFeature` |
| 命名空间 | 等于包 ID，子命名空间最多一层、按功能区命名、与目录同名 | `XiHan.Framework.Caching.Abstractions.Stores` |
| 类型名 | 不带 `XiHan` 前缀（XH0002），不用 `Default` 前缀与 `Manager`、`Helper`、`Util(s)`、`Common`、`Shared`、`Impl`、`Base` 后缀（XH0004），公开类型标 `[ApiLevel]`（XH0001） | `RedisCacheStore` |
| 清单 ID | `^xihan\.[a-z][a-z0-9]*\.[a-z][a-z0-9-]*$` | `xihan.caching.redis` |
| 能力 ID | `<领域>.<能力>`，引用时带接口版本 `@1`，可选依赖加 `?`；接口变化只加新版本并存 | `persistence.relational@1`、`multitenancy.context@^1?` |
| 错误码 | `<领域大写>_<KIND>_<三位序号>`，KIND 为 `VALIDATION`、`NOTFOUND`、`CONFLICT`、`DENIED`、`UNAVAILABLE`、`TIMEOUT`、`CANCELLED`、`UNEXPECTED` 或领域自定义；序号一经发布不复用 | `CACHING_UNAVAILABLE_001` |
| 健康项 | `^[a-z][a-z0-9.-]*$`，约定 `<领域>.<机制>.<对象>` | `caching.redis.connection` |
| 配置节 | `XiHan:<领域>`，Provider 在其下用自己的子键 | `XiHan:Caching:Redis` |

框架外的扩展包不受 `XiHan.Framework.*` 包名文法约束，但清单 ID、能力 ID、错误码与健康项的文法由内核校验，照样适用；想让编译器执行同样的类型命名规则，按 [Kernel.Analyzers](../packages/kernel-analyzers#安装与启用) 启用分析器。

## 相关

- [能力包与清单](./modularity)、[执行管道](./pipeline)、[操作契约](./operations)、[插件](./plugins)
- [测试与一致性套件](./testing)、[Conformance 一致性套件](../packages/conformance)
- [Kernel.Analyzers](../packages/kernel-analyzers)、[Kernel.Generators](../packages/kernel-generators)
- 规范：RFC-0002《清单》、RFC-0004《管道阶段与排序》、RFC-0010《本地化》、RFC-0019《远程 Provider》
