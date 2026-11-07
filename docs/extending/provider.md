# 写一个 Provider

Provider 是为某个子系统能力提供另一种实现的特性：Redis 缓存、PostgreSQL 持久化、Kubernetes 领导选举都是 Provider。这一页用一个从 JSON 文件读授予的授权 Provider 走完五步：实现抽象接口、写特性与清单、补消息目录 / 健康 / AOT、通过一致性套件、让应用使用。读完你能给任何子系统补一个与默认实现语义一致、能被应用一行装配的实现。前置知识是[能力图谱与 Provider](/startup/capabilities)与[一致性套件与门禁](/testing/conformance)。

## 为什么需要

框架给每个抽象配一个零外部基础设施的默认实现，让应用不装任何服务就能启动；生产环境需要的却是数据库、Redis、消息队列。如果换实现要改业务代码，默认实现就只是演示品。框架的做法是：契约放在 `<领域>.Abstractions`，实现是声明 `role: provider` 的特性，内核按能力 ID 求解时让 Provider 顶替默认实现，应用只改包引用与配置；一致性套件保证两种实现对同一契约的语义一致。

## Provider 是什么

| 要点 | 说明 |
| --- | --- |
| 依赖 | 只依赖 `<领域>.Abstractions`（必要时加 `Kernel`、`Localization.Abstractions`），不依赖其他 Provider，也不依赖宿主包 |
| 清单 | `Kind = Provider`、`Role = Provider`，`Provides` 写真正实现了的能力 ID 与接口版本 |
| 选择规则 | `XiHan:Providers:<能力 ID>` 指定的优先，其次是唯一的 `role: provider`，再次是唯一的 `role: default`；所以 Provider 一装配就顶替内存默认实现 |
| 只配置被选中的 | `Configure` 里用 `context.Node.IsSelectedFor(能力 ID)` 判断，只有被选中时才登记服务与中间件 |
| 一致性 | 通过所属子系统的 `<领域>.Conformance` 套件 |
| 健康 | 清单 `contributes.health` 声明的条目由本包以 `IHealthContributor` 实现 |

下面的例子实现 `security.authorization@1`，结构照搬框架里的 `Security.Database` 与 `Caching.Redis`。

## 最小示例：实现抽象接口

授权的存储契约是 `IPermissionGrantStore`，只有一个方法。先写一个从 JSON 文件读授予的实现：

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;
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

    public ValueTask<Result<IReadOnlyCollection<string>>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
    {
        // 全局授予（TenantId 为空）对任何租户生效，租户授予只对该租户生效；
        // 文件在启动时已读进内存，这里不会失败。读外部存储的实现在故障时返回 SECURITY_UNAVAILABLE_001，不能返回空集合
        IReadOnlyCollection<string> permissions = grants
            .Where(g => g.Subject == subject && (g.TenantId is null || (tenantId is not null && g.TenantId == tenantId)))
            .Select(static g => g.Permission)
            .ToHashSet(StringComparer.Ordinal);
        return ValueTask.FromResult(Result.Success(permissions));
    }
}
```

语义以对应的一致性套件为准：主体与权限区分大小写、全局授予对任何租户生效、通配授予 `*` 原样返回、没有授予是成功的空集合、存储故障是失败而不是空集合。批量优先：契约里有批量方法的（如 `ICacheStore.SetManyAsync`），Provider 要给出明确的逐项结果、空集合与重复项语义，能用后端原生批量接口时覆写成一次请求。

## 写特性与清单

特性把实现接进能力图谱。它实现 `IHostedFeature`（有启停）而不是只实现 `IFeature`，因为要启动健康探测：

```csharp
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
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
        // 配置错误是编程错误：抛 FrameworkException，英文消息，不本地化
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
        // 打不开就抛出，PeriodicHealthContributor 把条目记为 Degraded，reason 只写异常类型，完整异常进日志
        using var stream = File.OpenRead(services.GetRequiredService<FileSecurityOptions>().Path);
        return ValueTask.FromResult(Result.Success());
    }
}
```

清单上的几个要点：

| 字段 | 写法 | 原因 |
| --- | --- | --- |
| `Kind` / `Role` | `Provider` / `Provider` | 让内核在能力求解时把它排在默认实现之前 |
| `Provides` | 能力 ID 加接口版本 | 依赖按能力 ID 声明，不按包名；只提供真正实现了的能力，`Security.Database` 就只提供授权、不提供认证 |
| `Requires` | 用到的能力 | 缺失报 `KERNEL_CAPABILITY_001`，循环报 `KERNEL_CAPABILITY_002`；可选依赖写 `optional: true` |
| `Contributes.Pipeline` | 中间件类型全名与阶段 | 与 `AddMiddleware` 的阶段一致，`xihan graph` / `pipeline` 据此展示 |
| `Contributes.Config` | `XiHan:<领域>` 或其子节 | 写特性在本领域里读取的配置的最近公共节：只读自己子节写子节（如 `XiHan:Caching:Hybrid`），同时读领域公共选项写领域节（本例还读 `SecurityOptions`，所以写 `XiHan:Security`） |
| `Contributes.Health` | 条目名 `<领域>.<机制>.<对象>` | 声明的条目必须由同一个包以 `IHealthContributor` 实现，反之亦然 |

`context.Node.IsSelectedFor(...)` 是关键：同一能力的多个实现都可能被引用，只有被选中的那个注册服务与中间件。有与能力选择无关的服务要登记（如命名实例的 Provider）时实现 `IShadowAwareFeature`，被顶替时内核改调 `ConfigureShadowed`。

## 消息目录、健康与 AOT

- 文案不写在代码里（规范 RFC-0010）：错误只携带消息键常量与参数。Provider 用的是子系统已有的错误码时，注册子系统的目录（如 `SecurityMessages.Catalog`）；有自己的错误码时，定义 `<领域>Messages` 常量类，在包里放 10 种区域的 `Localization/<区域>.json`（`en-US` 必须有），以 `LogicalName="Localization/%(Filename)%(Extension)"` 嵌入，用 `MessageCatalog.FromEmbeddedResources(name, typeof(...))` 建目录并 `AddMessageCatalog`。配置错误与编程错误（如上面缺少 `Path`）抛 `FrameworkException`，消息用英文、不本地化（[全球化和本地化](/common/localization)）。
- 连接类 Provider（数据库、缓存、消息代理、对象存储）用 `PeriodicHealthContributor(name, probe, interval, clock, loggerFactory, description)`：启动时同步探一次，之后按间隔重探，探针失败或抛出记 `KERNEL_UNAVAILABLE_005` 并报 Degraded，恢复后报 Ready，应用只从 Ready 压成 Degraded 而不退出。容量类实现（内存存储、队列）同步上报，达到容量即 Degraded。间隔的配置键按惯例叫 `HealthProbeIntervalSeconds`，缺省 15（[生命周期与健康](/startup/lifecycle)）。
- 外部资源的连接串经绑定取得：清单 `Bindings` 声明绑定名与类型，`Configure` 里用 `context.GetBinding(name)` / `TryGetBinding(name)` 读值，同一个 Provider 在 Compose 里读环境变量、在 Kubernetes 里读 Secret（[资源绑定](/configuration/bindings)）。
- AOT：包缺省按 AOT 兼容写（`<IsAotCompatible>true</IsAotCompatible>`，序列化走源生成）。依赖的第三方库不兼容时设为 `false` 并在 README 写明；必须用反射的入口标 `RequiresDynamicCode` / `RequiresUnreferencedCode`，把警告交给调用方。
- 资源有界：缓存、队列、注册表不能无界增长，容量、过期、清理与并发语义都要有测试；不加静默兜底与无限重试。

## 通过一致性套件

为 Provider 建一个测试项目，继承对应的套件基类，全部用例通过才算语义与默认实现一致：

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

文件存储在进程内、读取不会失败，所以不覆写 `CreateUnavailableStoreAsync`，不可达用例跳过；访问外部存储的 Provider 要覆写它，证明故障返回 `SECURITY_UNAVAILABLE_001` 而不是空集合。再用测试宿主验证能力求解确实选中了它：

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

## 让应用使用

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

启动后 `xihan graph` 能看到 `security.authorization@1` 由 `xihan.security.files` 提供；`/health/ready` 里多出条目 `security.files.grants`，把 `grants.json` 改成不可读，下一次探测后条目变 Degraded，应用仍在服务。

## 远程 Provider

用其他语言实现 Provider 的做法是远程 Provider（规范 RFC-0019）：宿主侧写一个 `<领域>.Remote` 适配器声明能力，插件只实现规范规定的操作集。[Caching.Remote](/packages/caching-remote) 加 Go 写的 `samples/CacheGoPlugin` 是现成的例子：适配器把 `cache-entries.*` 操作集接成 `ICacheStore`，整体通过 `CacheStoreConformance`，见[进程外插件](/plugins/out-of-process)。

## 常见问题

### Provider 装配了，解析出来的还是内存实现

三个检查点：清单 `Provides` 的能力 ID 与版本是否与抽象包的常量一致（`SecurityCapabilities.Authorization`、`SecurityCapabilities.Version`）；`Configure` 里是否在 `IsSelectedFor` 为真时用 `TryAddSingleton` 登记，而默认实现又先登记了（Provider 的特性配置顺序由能力图谱决定，被顶替的默认实现不会登记）；有没有在 `XiHan:Providers:<能力 ID>` 里把别的实现钉死。

### 同一领域的多个能力要不要放一个包

只提供真正实现了的能力：`Security.Database` 只提供授权不提供认证，认证仍由默认实现负责。一个包提供多个能力时，每个能力各自判断 `IsSelectedFor`。

### 配置应该抛异常还是返回 Result

配置缺失、格式错误是编程错误，启动时抛 `FrameworkException` 让应用立刻失败；运行期的外部资源故障是可恢复失败，返回带领域错误码的 `Result`，由健康条目报 Degraded。

### 包名和清单 ID 怎么取

包按机制或技术命名（`Security.Files`、`Caching.Redis`），`Default` 不进包名；清单 ID `xihan.<领域>.<名字>`；错误码 `<领域大写>_<KIND>_<序号>`；健康项 `<领域>.<机制>.<对象>`。完整规则见[能力包与中间件](/extending/feature)。

## 相关包与下一步

- 包参考：[Kernel](/packages/kernel)、[Conformance](/packages/conformance)、[Testing](/packages/testing)、[Caching.Remote](/packages/caching-remote)
- 相关章节：[能力图谱与 Provider](/startup/capabilities)、[特性与清单](/startup/features)、[生命周期与健康](/startup/lifecycle)、[全球化和本地化](/common/localization)、[一致性套件与门禁](/testing/conformance)、[资源绑定](/configuration/bindings)
- 规范：RFC-0002《清单》、RFC-0010《本地化》、RFC-0019《远程 Provider》
- 下一步：[能力包与中间件](/extending/feature)
