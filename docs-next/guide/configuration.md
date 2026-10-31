# 配置与选项

本章讲 5.0 应用与框架怎样读配置：.NET 原生配置源与选项、`XiHan:<领域>` 配置节、挂载的配置目录与热更新、资源绑定、远程配置与密钥。适用于编写特性、准备部署配置的开发者。

完整 API 见 [Configuration.Abstractions](../packages/configuration-abstractions) 与 [Configuration.Nacos](../packages/configuration-nacos) 包页。

## 配置模型

5.0 不另造配置模型。应用与框架都读 `Microsoft.Extensions.Configuration` 的 `IConfiguration`，配置源仍是 `appsettings.json`、`appsettings.<环境>.json`、环境变量与命令行这些 .NET 原生来源，没有「默认配置包」。`Configuration.Abstractions` 只定义两个 Provider 契约（远程配置客户端与密钥库）和把它们接进 `IConfigurationBuilder` 的配置源。

框架包的选项是带 `ConfigurationSection` 常量的 record（`KernelOptions`、`LocalizationOptions`、`FileSystemObjectStorageOptions` 等），由各自的静态方法 `FromConfiguration(IConfiguration)` 逐键读取，不经反射绑定，因而在 Native AOT 下照常工作。特性在装配时读取一次并登记为单例：`AddXiHan` 返回之后再改 `XiHan:*` 下的键，要重启才对框架生效。命名存储实例是例外，它的定义在首次取用时才从 `XiHan:ObjectStorage:Stores:<name>` 读取（见[对象存储](./storage)）。

`AddXiHan` 在调用时就求解能力图谱、解析绑定、装配全部特性，所以远程配置、密钥这类配置源必须在 `AddXiHan` 之前加入 `builder.Configuration`，否则框架的选项与绑定读不到它们。

## 配置节命名

框架的配置节一律以 `XiHan:<领域>` 开头，领域词取自规范术语表（`Kernel`、`Persistence`、`Caching`、`Localization`、`ObjectStorage`、`Configuration` 等）。实现包的配置在领域节下再加一段实现名，例如 `XiHan:Caching:Redis`、`XiHan:ObjectStorage:FileSystem`；领域节本身放抽象层的选项，例如 `XiHan:Localization:DefaultLocale`。

| 写法 | 例子 |
| --- | --- |
| JSON | `{ "XiHan": { "Kernel": { "StopTimeoutSeconds": 25 } } }` |
| 配置键 | `XiHan:Kernel:StopTimeoutSeconds` |
| 环境变量 | `XiHan__Kernel__StopTimeoutSeconds=25` |

时长类的键带单位后缀（`StopTimeoutSeconds`、`RefreshIntervalSeconds`、`MultipartExpiryHours`），在代码里对应 `TimeSpan` 属性。值不合法（负数、无法解析的枚举或布尔值）时 `FromConfiguration` 抛出 `ArgumentException`，应用在启动期失败。

应用自己的配置节用自己的名字，不放进 `XiHan` 下，例如 KubernetesApp 示例的 `Sample`。

## 应用自己的选项

应用的选项照常用 `IOptions<T>` / `IOptionsMonitor<T>` 绑定。需要随配置文件变化而更新的值，注入 `IOptionsMonitor<T>` 并读 `CurrentValue`。下面的代码取自 KubernetesApp 示例，略去了与配置无关的部分：

```csharp
// BannerOptions.cs
namespace KubernetesApp;

public sealed class BannerOptions
{
    public const string Section = "Sample";

    public string Banner { get; set; } = "Hello from XiHan";
}
```

```csharp
// Program.cs
using KubernetesApp;
using Microsoft.Extensions.Options;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Full;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<BannerOptions>(builder.Configuration.GetSection(BannerOptions.Section));

// AddXiHan 先装载 XIHAN_CONFIG_PATH 目录里的 JSON 文件并监听变化
builder.AddXiHan(xihan => xihan.UseFullProfile());

var app = builder.Build();
app.UseXiHan();

// 挂载文件里的 Sample:Banner 改了，CurrentValue 随之变化，不必重启
app.MapGet("/", (IOptionsMonitor<BannerOptions> banner) => banner.CurrentValue.Banner);

app.Run();
```

```json
{
  "Sample": {
    "Banner": "Hello from appsettings.json"
  }
}
```

## 挂载的配置目录与热更新

环境变量 `XIHAN_CONFIG_PATH` 指向一个目录时（Kubernetes 里是 ConfigMap 的挂载点），`Hosting.AspNetCore` 与 `Hosting.CommandLine` 的 `AddXiHan` 把其中的 JSON 文件追加为配置源，并以 `reloadOnChange: true` 监听变化。找文件的规则在内核的 `ConfigurationDirectory.Resolve`：

1. 只取目录顶层的 `*.json`，跳过以 `.` 开头的条目（ConfigMap 挂载工具放置的 `..data` 等）。
2. `appsettings.json` 最先装载，其余文件按文件名的序数顺序随后装载，后装载的覆盖先装载的同名键。
3. 目录不存在或环境变量为空时什么都不加。

这些文件追加在 `WebApplication.CreateBuilder` 已有的配置源之后，所以优先级高于 `appsettings.*.json`、环境变量与命令行参数：同一个键同时出现在挂载文件与环境变量里时，挂载文件的值生效。文件列表在启动时确定，运行中新增的文件要重启才装载；已装载文件的内容变化会触发 `IConfiguration` 的重载令牌，`IOptionsMonitor<T>` 随之更新。

::: warning ConfigMap 更新要用轮询监视
kubelet 更新 ConfigMap 时切换的是符号链接，默认的文件系统通知收不到变化。容器里要设 `DOTNET_USE_POLLING_FILE_WATCHER=1`；`xihan pack` 按应用清单的 `config` 生成 ConfigMap 时会一并写入它与 `XIHAN_CONFIG_PATH`（挂载点缺省 `/etc/xihan`）。
:::

热更新只作用于读 `IConfiguration` 或 `IOptionsMonitor<T>` 的代码。框架包的选项在装配时已经读完，改挂载文件里的 `XiHan:*` 键不会改变正在运行的特性。部署侧的生成规则见[部署与打包](./deployment)。

## 资源绑定

特性需要的外部资源（数据库连接、Redis、消息代理、对象存储端点）在清单的 `Bindings` 里按名字声明，宿主在装配时解析出 `BindingValue`（名字、类型、主值与其余属性），特性用 `context.GetBinding(name)` 或 `context.TryGetBinding(name)` 取用。这样同一个能力包在本地、Compose 与 Kubernetes 里不改代码，只换绑定的来源。

内核的 `BindingResolver.Resolve` 按下面的顺序收集绑定：

1. 环境变量 `SERVICE_BINDING_ROOT` 指向的目录（Service Binding 规范）。每个子目录是一个绑定，目录名是绑定名；目录里每个文件是一个属性，文件名是属性名、内容修剪首尾空白后是值；`type` 文件是类型，`uri` 文件既是主值也保留为属性；以 `.` 开头的文件跳过。`AppBuilder.UseServiceBindingRoot(directory)` 可以在代码里改用别的目录。
2. 配置节 `XiHan:Bindings:<name>`。节本身的标量值或子键 `value` 是主值，子键 `type` 是类型，其余子键是属性。

同名时配置整体覆盖目录来源，不逐属性合并。

```json
{
  "XiHan": {
    "Bindings": {
      "cache": "localhost:6379",
      "db": {
        "type": "connection:relational",
        "value": "Host=localhost;Database=orders;Username=dev;Password=dev-password"
      }
    }
  }
}
```

环境变量写法是 `XiHan__Bindings__db__value`、`XiHan__Bindings__db__type`，Compose 与 Aspire 的生成产物就是这样注入的。

清单里 `required: true` 的绑定在 `AddXiHan` 构建时校验：缺失，或来源声明了类型而与清单不一致，都抛出 `KERNEL_HOSTING_001`，应用不会启动。来源没有声明类型（空字符串）时不比对类型。可选绑定由特性自己用 `TryGetBinding` 处理。

框架内置包声明的绑定都是可选的，与各自配置节的先后关系不同，以下表为准：

| 绑定名 | 类型 | 声明它的包 | 用法 |
| --- | --- | --- | --- |
| `db` | `connection:relational` | `Persistence.EntityFrameworkCore`、`Persistence.SqlSugar` | 交给应用传入特性的配置委托，主值是连接字符串 |
| `cache` | `cache:distributed` | `Caching.Redis` | `XiHan:Caching:Redis:Configuration` 没配时用主值 |
| `queue` | `queue` | `Messaging.RabbitMQ` | `XiHan:Messaging:RabbitMQ:Configuration` 没配时用主值 |
| `stream` | `stream` | `Messaging.Kafka` | `XiHan:Messaging:Kafka:BootstrapServers` 没配时用主值 |
| `nacos` | `configuration:remote` | `Configuration.Nacos` | `XiHan:Configuration:Nacos:ServerAddresses` 没配时用主值 |
| `objectstorage` | `storage:object` | `ObjectStorage.S3` | 主值（服务端点）与属性 `region`、`accessKey`、`secretKey`、`forcePathStyle` 覆盖 `XiHan:ObjectStorage:S3` 里的同名设置 |

应用自己的特性声明绑定的写法：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace Billing;

public sealed class BillingFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.billing.ledger",
        Package = "Billing",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        // 必需绑定：缺失时 AddXiHan 抛出 KERNEL_HOSTING_001，启动前就暴露
        Bindings = [new Binding("ledger", "connection:relational")],
        Entry = typeof(BillingFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        var ledger = context.GetBinding("ledger");
        context.Services.AddSingleton(new LedgerConnection(ledger.Value ?? throw new InvalidOperationException("Binding 'ledger' has no value.")));
    }
}

public sealed record LedgerConnection(string ConnectionString);
```

清单字段与能力声明的完整说明见[能力包与清单](./modularity)。

## 远程配置

`configuration.remote@1` 的契约是 `IRemoteConfigurationClient.FetchAsync(namespace, knownVersion)`：按命名空间取整份带版本的文档 `ConfigurationDocument`，键已经展平成 `A:B:C` 形式；`knownVersion` 非空且远端没有变化时返回无值，实现可以长轮询。`AddXiHanRemoteConfiguration(client, options)` 把客户端接进配置：

1. 构建配置时同步加载一次。`IConfigurationProvider.Load` 是同步契约，取值在线程池上执行并等待，受 `LoadTimeout`（缺省 10 秒）约束。
2. 首次加载失败时先回调 `OnError`；`Optional` 为 `false`（缺省）时随后抛出带错误码的 `FrameworkException`，启动失败，为 `true` 时该源为空。
3. 之后按 `PollInterval`（缺省 30 秒，`null` 关闭轮询）带已知版本轮询。拿到新版本就整体替换这个源的数据并触发重载令牌，`IOptionsMonitor<T>` 随之更新。
4. 轮询失败（返回失败结果或抛出异常）只回调 `OnError`，保留最后一份成功的数据。

`Prefix` 把文档挂到某个配置节下，为空时挂在根。`OnError` 是唯一的报告渠道，因为配置在日志之前构建。

| 选项 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Namespace` | `string` | 构造参数 | 远程命名空间，Nacos 里是 `dataId` |
| `Prefix` | `string?` | `null` | 挂载的配置节 |
| `PollInterval` | `TimeSpan?` | 30 秒 | 轮询周期，`null` 关闭，必须为正 |
| `Clock` | `TimeProvider` | 系统时钟 | 轮询计时用的时钟；测试换成可推进的时钟（如 `FakeTimeProvider`），轮询由用例触发 |
| `LoadTimeout` | `TimeSpan` | 10 秒 | 首次加载的超时，必须为正 |
| `Optional` | `bool` | `false` | 首次加载失败时是否继续启动 |
| `OnError` | `Action<Error>?` | `null` | 加载或轮询失败时的回调 |

### Nacos

5.0 提供的远程配置 Provider 是 [Configuration.Nacos](../packages/configuration-nacos)：直接调用 Nacos 的 HTTP 配置接口（v1，与 Nacos 2.x 兼容），命名空间参数就是 `dataId`，版本是服务端给出的内容 MD5，`knownVersion` 非空时先长轮询监听，服务端报告变化才取整份文档。下面把 `dataId` 为 `orders` 的 JSON 文档挂到 `Orders` 节下，服务地址等连接设置从 `XiHan:Configuration:Nacos` 读：

```csharp
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Configuration.Nacos;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 必须在 AddXiHan 之前：框架在 AddXiHan 里读取选项与绑定
builder.Configuration.AddXiHanNacos(
    NacosOptions.FromConfiguration(builder.Configuration),
    new RemoteConfigurationOptions("orders")
    {
        Prefix = "Orders",
        PollInterval = TimeSpan.FromSeconds(15),
        OnError = static error => Console.Error.WriteLine($"Nacos: {error.Code}"),
    });
builder.Services.Configure<OrderLimits>(builder.Configuration.GetSection("Orders"));

builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();
app.Run();

public sealed class OrderLimits
{
    public int MaxItems { get; set; } = 100;
}
```

```json
{
  "XiHan": {
    "Configuration": {
      "Nacos": {
        "ServerAddresses": "http://localhost:8848",
        "Group": "DEFAULT_GROUP",
        "Format": "Json"
      }
    }
  }
}
```

`AddXiHanNacos` 自建 HTTP 客户端，用在配置构建期。另装 `NacosConfigurationFeature` 时，运行期容器里会登记 `IRemoteConfigurationClient`，供其他组件按需取文档。连接选项、长轮询与登录的细节见包页。

## 密钥

`configuration.secrets@1` 的契约是 `ISecretStore.GetAsync(names[])`：按名字批量读取，只读，逐项返回 `SecretLookup(Name, Result<SecretValue>)`，顺序与入参一致，重复项各自作答，空集合返回空。写入与轮换属于运维工具，不在应用契约里。

- 密钥名 `SecretName` 的文法是 `^[A-Za-z0-9][A-Za-z0-9._/-]{0,255}$`，不含 `..` 与 `//`，不以 `/` 结尾，大小写敏感；不合法在构造时抛出 `ArgumentException`。
- `SecretValue.ToString()` 固定返回 `***`（`SecretValue.Redacted`），值不会经字符串插值进入日志或异常消息。读值用 `Value`。
- 单条读取用扩展方法 `store.GetAsync(name, cancellationToken)`，它转调批量方法。

`AddXiHanSecrets(store, options)` 把密钥映射到配置键：`Mappings` 是配置键到密钥名的字典，构建配置时一次批量读取。首次读取中有缺失或失败的项时，每个失败的项回调一次 `OnError`；`Optional` 为 `false`（缺省）时随后抛出 `FrameworkException` 并列出失败的配置键，为 `true` 时跳过这些项。`RefreshInterval` 非空时周期性重读，重读失败的项保留旧值。

| 选项 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Mappings` | `IReadOnlyDictionary<string, SecretName>` | 构造参数 | 配置键 → 密钥名，至少一项 |
| `LoadTimeout` | `TimeSpan` | 10 秒 | 首次读取的超时 |
| `RefreshInterval` | `TimeSpan?` | `null` | 周期重读的间隔，`null` 不重读 |
| `Clock` | `TimeProvider` | 系统时钟 | 重读计时用的时钟；测试换成可推进的时钟，重读由用例触发 |
| `Optional` | `bool` | `false` | 首次读取缺失或失败的项是否跳过 |
| `OnError` | `Action<Error>?` | `null` | 每个失败的项回调一次 |

5.0 没有提供密钥库 Provider（Vault、Key Vault 等），应用按自己的密钥来源实现 `ISecretStore`，并让测试项目继承 `Configuration.Conformance` 的 `SecretStoreConformance` 验证语义。下面的实现从一个目录读文件，每个密钥一个文件：

```csharp
using XiHan.Framework.Configuration.Abstractions;
using XiHan.Framework.Configuration.Abstractions.Secrets;
using XiHan.Framework.Primitives;

namespace Orders.Secrets;

public sealed class DirectorySecretStore(string root) : ISecretStore
{
    public async ValueTask<IReadOnlyList<SecretLookup>> GetAsync(IReadOnlyList<SecretName> names, CancellationToken cancellationToken)
    {
        var results = new List<SecretLookup>(names.Count);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // SecretName 的文法已排除 ".." 与开头的 "/"，拼出的路径不会跳出根目录
            var path = Path.Combine(root, name.Value);
            Result<SecretValue> result;
            try
            {
                result = File.Exists(path)
                    ? new SecretValue((await File.ReadAllTextAsync(path, cancellationToken)).Trim())
                    : Error.NotFound(ConfigurationErrors.SecretNotFound, ConfigurationMessages.SecretNotFound, MessageArguments.Of(("name", name.Value)));
            }
            catch (IOException exception)
            {
                result = Error.Unavailable(ConfigurationErrors.SecretsUnavailable, ConfigurationMessages.SecretsUnavailable, MessageArguments.Of(("reason", exception.Message)));
            }

            results.Add(new SecretLookup(name, result));
        }

        return results;
    }
}
```

```csharp
// Program.cs：密钥源同样要在 AddXiHan 之前加入，绑定 db 的主值才能取到它
builder.Configuration.AddXiHanSecrets(
    new DirectorySecretStore("/run/secrets"),
    new SecretsConfigurationOptions(new Dictionary<string, SecretName>
    {
        ["XiHan:Bindings:db:value"] = "orders/db-connection",
    })
    {
        RefreshInterval = TimeSpan.FromMinutes(5),
    });
```

::: warning 进入 IConfiguration 的密钥是明文
遮蔽只作用于 `SecretValue` 对象。映射进配置之后，`configuration["XiHan:Bindings:db:value"]` 返回的是原始字符串；不要把整个配置节序列化进日志或诊断端点。
:::

## 失败与错误码

| 错误码 | 含义 | 出现在 |
| --- | --- | --- |
| `KERNEL_HOSTING_001` | 必需绑定缺失或类型不匹配 | `AddXiHan` 构建时、`FeatureContext.GetBinding` |
| `CONFIGURATION_NOTFOUND_001` | 远程配置的命名空间不存在 | `IRemoteConfigurationClient.FetchAsync` |
| `CONFIGURATION_NOTFOUND_002` | 密钥不存在 | `ISecretStore.GetAsync` 的逐项结果 |
| `CONFIGURATION_UNAVAILABLE_001` | 远程配置不可达或超时（首次加载超时也归此类） | `IRemoteConfigurationClient.FetchAsync`、首次加载 |
| `CONFIGURATION_UNAVAILABLE_002` | 密钥库不可达或超时 | `ISecretStore.GetAsync`、首次读取 |
| `CONFIGURATION_DENIED_001` | 没有读取该密钥的权限 | `ISecretStore.GetAsync` 的逐项结果 |
| `CONFIGURATION_VALIDATION_001` | 远程文档不是声明的格式 | `IRemoteConfigurationClient.FetchAsync` |

远程配置与密钥的首次加载失败且不可选时，`FrameworkException` 的错误码就是上表里对应的码；轮询与重读的失败只经 `OnError` 报告。选项值不合法、密钥名或命名空间为空属于编程错误，构造时抛出 `ArgumentException`。

## 配置节一览

下表列出 5.0 各包在代码里读取的配置节（`ConfigurationSection` 常量与 `FromConfiguration` 读取的键）。`XiHan:Providers:<能力 ID>` 指定某个能力由哪份清单提供，原理见[能力包与清单](./modularity)。

| 配置节 | 所属包 | 用途 |
| --- | --- | --- |
| `XiHan:Providers` | [Kernel](../packages/kernel) | `<能力 ID>` → 清单 ID，指定能力的提供者 |
| `XiHan:Bindings` | [Kernel](../packages/kernel) | 资源绑定，见上文「资源绑定」 |
| `XiHan:Kernel` | [Kernel](../packages/kernel) | `StartTimeoutSeconds`、`StopTimeoutSeconds`、`FailurePolicy`、`UseBuiltInMiddleware` |
| `XiHan:Kernel:Resilience` | [Kernel](../packages/kernel) | Resilience 阶段的默认策略，`Operations:<操作 ID>` 按操作覆盖 |
| `XiHan:Persistence:InMemory` | [Persistence.InMemory](../packages/persistence-inmemory) | `MaxEntitiesPerType` |
| `XiHan:Persistence:EntityFrameworkCore` | [Persistence.EntityFrameworkCore](../packages/persistence-entityframeworkcore) | `EnsureCreated`、`HealthProbeIntervalSeconds` |
| `XiHan:Persistence:SqlSugar` | [Persistence.SqlSugar](../packages/persistence-sqlsugar) | `InitTables`、`HealthProbeIntervalSeconds` |
| `XiHan:Persistence:Snowflake` | [Persistence.Abstractions](../packages/persistence-abstractions) | 雪花标识：`WorkerId`、`Epoch`、`LeaseTimeToLiveSeconds`、`ClockDriftToleranceMilliseconds` |
| `XiHan:MultiTenancy` | [MultiTenancy.Abstractions](../packages/multitenancy-abstractions) | `DefaultTenant`、`RequireTenant`、`TenantClaim`、`ValidateAgainstStore` |
| `XiHan:MultiTenancy:InMemory` | [MultiTenancy.InMemory](../packages/multitenancy-inmemory) | `MaxTenants` |
| `XiHan:MultiTenancy:Tenants` | [MultiTenancy.InMemory](../packages/multitenancy-inmemory) | 从配置装载的租户（`DisplayName`、`IsEnabled`、`Properties`） |
| `XiHan:MultiTenancy:Database` | [MultiTenancy.Database](../packages/multitenancy-database) | `CacheSize`、`CacheTimeToLiveSeconds`、`MaxListedTenants` |
| `XiHan:Security` | [Security.Abstractions](../packages/security-abstractions) | `RequireAuthentication`、`RoleClaim` |
| `XiHan:Security:InMemory` | [Security.InMemory](../packages/security-inmemory) | 开发用主体与授予（`Principals`、`Grants`） |
| `XiHan:Security:Database` | [Security.Database](../packages/security-database) | `CacheSize`、`CacheTimeToLiveSeconds` |
| `XiHan:Security:Jwt` | [Security.Jwt](../packages/security-jwt) | `Issuer`、`Audience`、`Secret`、`ClockSkewSeconds`、`NameClaim` |
| `XiHan:Security:OpenIdConnect` | [Security.OpenIdConnect](../packages/security-openidconnect) | `Authority`、`Audience`、元数据地址与刷新间隔 |
| `XiHan:Security:Ip2Region` | [Security.Ip2Region](../packages/security-ip2region) | xdb 数据文件路径 `V4Database`、`V6Database` |
| `XiHan:Caching:InMemory` | [Caching.InMemory](../packages/caching-inmemory) | `MaxEntries`、`DefaultTimeToLiveSeconds` |
| `XiHan:Caching:Redis` | [Caching.Redis](../packages/caching-redis) | `Configuration`、`KeyPrefix`、`DefaultTimeToLiveSeconds`、`PublishChanges`、`HealthProbeIntervalSeconds` |
| `XiHan:Caching:Hybrid` | [Caching.Hybrid](../packages/caching-hybrid) | `LocalCapacity`、`LocalTimeToLiveSeconds` |
| `XiHan:Caching:Remote` | [Caching.Remote](../packages/caching-remote) | `TimeoutMilliseconds` |
| `XiHan:Caching:Idempotency` | [Caching.Abstractions](../packages/caching-abstractions) | 幂等记录的 `TimeToLiveSeconds`、`InProgressTimeToLiveSeconds` |
| `XiHan:Messaging` | [Messaging.Abstractions](../packages/messaging-abstractions) | `MaxAttempts`、`DeadLetterSuffix` |
| `XiHan:Messaging:Outbox` | [Messaging.Abstractions](../packages/messaging-abstractions) | 发件箱的 `PollIntervalSeconds`、`BatchSize` |
| `XiHan:Messaging:InMemory` | [Messaging.InMemory](../packages/messaging-inmemory) | `MaxQueuedMessages` |
| `XiHan:Messaging:RabbitMQ` | [Messaging.RabbitMQ](../packages/messaging-rabbitmq) | `Configuration`、`QueuePrefix`、`PrefetchCount`、`HealthProbeIntervalSeconds` |
| `XiHan:Messaging:Kafka` | [Messaging.Kafka](../packages/messaging-kafka) | `BootstrapServers`、`GroupId`、`TopicPrefix`、`Properties:<key>` |
| `XiHan:Scheduling` | [Scheduling.Abstractions](../packages/scheduling-abstractions) | 调度引擎与领导选举：轮询、租约、并发、错过阈值、历史保留 |
| `XiHan:Scheduling:InMemory` | [Scheduling.InMemory](../packages/scheduling-inmemory) | `MaxRuns` |
| `XiHan:Scheduling:Database` | [Scheduling.Database](../packages/scheduling-database) | `HealthProbeIntervalSeconds` |
| `XiHan:Scheduling:Kubernetes` | [Scheduling.Kubernetes](../packages/scheduling-kubernetes) | Lease 选举：`Lease`、`Namespace`、`Holder`、`ApiServer` |
| `XiHan:Workflow` | [Workflow.Abstractions](../packages/workflow-abstractions) | 引擎：锁超时、子流程深度、表达式缓存、保留天数 |
| `XiHan:Workflow:InMemory` | [Workflow.InMemory](../packages/workflow-inmemory) | 各存储容量、轮询周期与批大小 |
| `XiHan:Workflow:Database` | [Workflow.Database](../packages/workflow-database) | 到期书签的轮询与批大小、租约时长、`HolderId` |
| `XiHan:Localization` | [Localization.Abstractions](../packages/localization-abstractions) | `DefaultLocale`、`Locales` |
| `XiHan:Localization:Database` | [Localization.Database](../packages/localization-database) | `RefreshIntervalSeconds`、`MaxTranslations` |
| `XiHan:ObjectStorage:FileSystem` | [ObjectStorage.FileSystem](../packages/objectstorage-filesystem) | `RootPath`、`MaxTotalBytes`、`MaxBlobBytes`、`MultipartExpiryHours` |
| `XiHan:ObjectStorage:S3` | [ObjectStorage.S3](../packages/objectstorage-s3) | `ServiceUrl`、`Region`、`AccessKey`、`SecretKey`、`ForcePathStyle` 等 |
| `XiHan:ObjectStorage:Stores` | [ObjectStorage.Abstractions](../packages/objectstorage-abstractions) | 命名存储实例：`<name>:Provider` 加该 Provider 的设置 |
| `XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds` | [ObjectStorage.Abstractions](../packages/objectstorage-abstractions) | 命名实例健康探测的间隔（单个键） |
| `XiHan:ObjectStorage:SignedUrls` | [Hosting.AspNetCore](../packages/hosting-aspnetcore) | 宿主代收发预签名地址的 `BaseUrl`、`Secret` |
| `XiHan:Configuration:Nacos` | [Configuration.Nacos](../packages/configuration-nacos) | Nacos 服务地址、分组、命名空间、登录与超时 |
| `XiHan:Auditing` | [Auditing.Abstractions](../packages/auditing-abstractions) | `Selection`、`IncludeSuccesses` |
| `XiHan:Auditing:InMemory` | [Auditing.InMemory](../packages/auditing-inmemory) | `Capacity`、`LogEvents` |
| `XiHan:Auditing:Database` | [Auditing.Database](../packages/auditing-database) | `QueueCapacity`、`BatchSize`、`FlushIntervalSeconds`、`LogEvents` |
| `XiHan:Observability:Host` | [Observability.Host](../packages/observability-host) | `SampleIntervalSeconds` |
| `XiHan:Observability:OpenTelemetry` | [Observability.OpenTelemetry](../packages/observability-opentelemetry) | 服务名、追踪、指标、日志、采样与 OTLP 导出 |
| `XiHan:AI` | [AI.Abstractions](../packages/ai-abstractions) | Provider 表、工具名、护栏、知识库与提示词 |
| `XiHan:Protocols:Http` | [Protocols.Http](../packages/protocols-http) | `RoutePrefix`，`Routes:<操作 ID>` 覆盖推导的路由 |
| `XiHan:Protocols:Grpc` | [Protocols.Grpc](../packages/protocols-grpc) | `Package`，`Methods:<操作 ID>` 覆盖推导的方法名 |
| `XiHan:Protocols:Mcp` | [Protocols.Mcp](../packages/protocols-mcp) | `ServerName`、`ServerVersion`、`Instructions`，`Tools:<操作 ID>` |
| `XiHan:Protocols:CloudEvents` | [Protocols.CloudEvents](../packages/protocols-cloudevents) | `Inbound`、`Outbound`、`Topics:<事件类型>`、`Webhooks:<主题>` |
| `XiHan:Plugins:OutOfProcess` | [Plugins.OutOfProcess](../packages/plugins-outofprocess) | 插件目录、授予、握手与启停超时、健康与重启限制 |
| `XiHan:Plugins:Wasm` | [Plugins.Wasm](../packages/plugins-wasm) | 插件目录、授予、挂载、内存与栈上限、调用超时 |

各键的类型与缺省值以对应包页为准。

## 相关

- [能力包与清单](./modularity)
- [部署与打包](./deployment)
- [序列化与 Native AOT](./aot)
- [Configuration.Abstractions](../packages/configuration-abstractions)
- [Configuration.Nacos](../packages/configuration-nacos)
- 规范：RFC-0014《远程配置与密钥》、RFC-0016《打包》
