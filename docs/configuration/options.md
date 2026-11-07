# 配置模型与选项

这一页讲应用与框架怎样读配置：框架沿用哪套配置模型、`XiHan:<领域>` 配置节怎样命名、应用自己的选项怎样绑定与校验、哪些值能在运行中更新。读完你能给应用加一组自己的选项，并知道框架各包从哪个配置节读取什么。前置知识是[装配入口](/startup/bootstrap)：你需要知道 `AddXiHan` 在什么时候执行、做了什么。

## 为什么需要

一个应用总有一些值不能写死在代码里：端口、连接地址、超时、容量上限、功能开关。它们在开发机、测试环境和生产集群里各不相同，还要能在不改代码的前提下调整。.NET 自带 `Microsoft.Extensions.Configuration`，已经把文件、环境变量、命令行这些来源统一成一棵键值树；如果框架再造一套配置模型，应用就要学两套写法、维护两份来源。

XiHan.Framework 因此不另造配置模型：应用与框架都读同一个 `IConfiguration`。框架只约定两件事：它自己的配置节一律挂在 `XiHan:<领域>` 下；各包的选项按键显式读取而不经反射绑定，所以在 Native AOT 下照常工作。应用自己的选项照旧用 `IOptions<T>` / `IOptionsMonitor<T>`。

## 配置模型

| 概念 | 说明 |
| --- | --- |
| `IConfiguration` | .NET 的配置树；`appsettings.json`、`appsettings.<环境>.json`、环境变量、命令行都是它的来源 |
| 配置节 | 树上的一个子树，层级以 `:` 分隔，例如 `XiHan:Kernel` |
| 框架选项 | 各包里带 `ConfigurationSection` 常量的 record（`KernelOptions`、`ResilienceOptions`、`NacosOptions` 等），由静态方法 `FromConfiguration(IConfiguration)` 逐键读取 |
| 应用选项 | 应用自己的类或 record，用 `IOptions<T>` / `IOptionsMonitor<T>` 绑定 |
| `Configuration.Abstractions` | 只定义两个 Provider 契约（远程配置客户端与密钥库）以及把它们接进 `IConfigurationBuilder` 的配置源，见 [远程配置与密钥](/configuration/remote) |

框架没有「默认配置包」：不装任何配置相关的包，`appsettings.json` 与环境变量就是全部来源。

### 框架选项的读取时机

特性在装配时读取一次选项并登记为单例。也就是说 `AddXiHan` 返回之后再改 `XiHan:*` 下的键，要重启才对框架生效。命名存储实例是例外，它的定义在首次取用时才从 `XiHan:ObjectStorage:Stores:<name>` 读取，见[分片、预签名与命名实例](/object-storage/advanced)。

`AddXiHan` 在调用时就求解能力图谱、解析资源绑定、装配全部特性，所以远程配置、密钥这类配置源必须在 `AddXiHan` 之前加入 `builder.Configuration`，否则框架的选项与绑定读不到它们。

## 配置节命名

框架的配置节一律以 `XiHan:<领域>` 开头，领域词取自规范术语表（`Kernel`、`Persistence`、`Caching`、`Localization`、`ObjectStorage`、`Configuration` 等）。实现包的配置在领域节下再加一段实现名，例如 `XiHan:Caching:Redis`、`XiHan:ObjectStorage:FileSystem`；领域节本身放抽象层的选项，例如 `XiHan:Localization:DefaultLocale`。

同一个键有三种等价写法：

| 写法 | 例子 |
| --- | --- |
| JSON | `{ "XiHan": { "Kernel": { "StopTimeoutSeconds": 25 } } }` |
| 配置键 | `XiHan:Kernel:StopTimeoutSeconds` |
| 环境变量 | `XiHan__Kernel__StopTimeoutSeconds=25` |

几条约定：

- 时长类的键带单位后缀（`StopTimeoutSeconds`、`RefreshIntervalSeconds`、`MultipartExpiryHours`），在代码里对应 `TimeSpan` 属性。
- 值不合法（负数、无法解析的枚举或布尔值）时 `FromConfiguration` 抛出 `ArgumentException`，应用在启动期失败，不会带着错误配置运行。
- 应用自己的配置节用自己的名字，不放进 `XiHan` 下。下面的示例用 `Sample`。
- 特性清单的 `Contributes.Config` 声明它读取的配置的最近公共节：只读自己子节的写子节（`Caching.Hybrid` 写 `XiHan:Caching:Hybrid`），同时读领域节下公共选项的写领域节（`Caching.Redis` 还读 `XiHan:Caching:Idempotency`，写 `XiHan:Caching`），见[特性与清单](/startup/features)与规范 RFC-0002。

## 最小示例

我们给应用加一个 `Sample:Banner` 选项，并用一个端点返回它的当前值。先写选项类：

```csharp
// BannerOptions.cs
namespace Shop;

public sealed class BannerOptions
{
    public const string Section = "Sample";

    public string Banner { get; set; } = "Hello from XiHan";
}
```

再在 `Program.cs` 里绑定并使用：

```csharp
// Program.cs
using Microsoft.Extensions.Options;
using Shop;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<BannerOptions>(builder.Configuration.GetSection(BannerOptions.Section));

builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();

// 读 CurrentValue 而不是在启动时缓存，配置源重载后返回新值
app.MapGet("/banner", (IOptionsMonitor<BannerOptions> banner) => banner.CurrentValue.Banner);

app.Run();
```

`appsettings.json`：

```json
{
  "Sample": {
    "Banner": "Hello from appsettings.json"
  },
  "XiHan": {
    "Kernel": {
      "StopTimeoutSeconds": 25
    }
  }
}
```

运行后 `curl localhost:5000/banner` 返回 `Hello from appsettings.json`。设置环境变量 `Sample__Banner="Hello from env"` 再启动，返回的就是环境变量的值：环境变量覆盖文件。`XiHan:Kernel:StopTimeoutSeconds` 由内核的 `KernelOptions.FromConfiguration` 读取，决定每个特性停止的超时，应用代码不用碰它。

## 应用自己的选项

### IOptions 与 IOptionsMonitor

| 注入类型 | 何时取值 | 适用 |
| --- | --- | --- |
| `IOptions<T>` | 首次取用时绑定一次，之后不变 | 启动后不再变化的值 |
| `IOptionsMonitor<T>` | `CurrentValue` 每次返回当前值，配置源重载后更新；`OnChange` 可注册回调 | 需要热更新的值，见 [配置目录与热更新](/configuration/config-directory) |
| `IOptionsSnapshot<T>` | 每个服务作用域绑定一次 | 一次调用内保持一致、跨调用可变 |

### 校验

选项值不合法时最好在启动期暴露。.NET 的选项构建器支持声明校验，并能要求在启动时执行：

```csharp
builder.Services.AddOptions<BannerOptions>()
    .Bind(builder.Configuration.GetSection(BannerOptions.Section))
    .Validate(static options => !string.IsNullOrWhiteSpace(options.Banner), "Sample:Banner must not be blank.")
    .ValidateOnStart();
```

这与框架选项的做法一致：配置错误属于编程与部署错误，应用启动失败，而不是带着坏值运行。这类消息面向开发与运维，用英文，不走本地化，区分方式见[结果与错误处理](/web/errors)。

### Native AOT 下的绑定

`Configure<T>(IConfiguration)` 默认按反射绑定。要以 Native AOT 发布，在项目文件里打开配置绑定源生成器，绑定代码在编译期生成，`IOptionsMonitor<T>` 的重载也照常工作：

```xml
<PropertyGroup>
  <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
</PropertyGroup>
```

也可以像框架一样按键读取、不绑定。AOT 的完整要求见 [JSON 序列化与 Native AOT](/common/serialization)。

## 配置节一览

下表列出各包在代码里读取的配置节（`ConfigurationSection` 常量与 `FromConfiguration` 读取的键）。`XiHan:Providers:<能力 ID>` 指定某个能力由哪份清单提供，原理见[能力图谱与 Provider](/startup/capabilities)；`XiHan:Bindings` 见 [资源绑定](/configuration/bindings)。

| 配置节 | 所属包 | 用途 |
| --- | --- | --- |
| `XiHan:Providers` | [Kernel](/packages/kernel) | `<能力 ID>` → 清单 ID，指定能力的提供者 |
| `XiHan:Bindings` | [Kernel](/packages/kernel) | 资源绑定 |
| `XiHan:Kernel` | [Kernel](/packages/kernel) | `StartTimeoutSeconds`、`StopTimeoutSeconds`、`FailurePolicy`、`UseBuiltInMiddleware` |
| `XiHan:Kernel:Resilience` | [Kernel](/packages/kernel) | Resilience 阶段的默认策略，`Operations:<操作 ID>` 按操作覆盖，见[弹性与限流](/common/resilience) |
| `XiHan:Persistence:InMemory` | [Persistence.InMemory](/packages/persistence-inmemory) | `MaxEntitiesPerType` |
| `XiHan:Persistence:EntityFrameworkCore` | [Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore) | `EnsureCreated`、`HealthProbeIntervalSeconds` |
| `XiHan:Persistence:SqlSugar` | [Persistence.SqlSugar](/packages/persistence-sqlsugar) | `InitTables`、`CommandTimeoutSeconds`、`Replicas:*`（从库的权重与健康探测）、`HealthProbeIntervalSeconds` |
| `XiHan:Persistence:Snowflake` | [Persistence.Abstractions](/packages/persistence-abstractions) | 雪花标识：`WorkerId`、`Epoch`、`LeaseTimeToLiveSeconds`、`ClockDriftToleranceMilliseconds` |
| `XiHan:MultiTenancy` | [MultiTenancy.Abstractions](/packages/multitenancy-abstractions) | `DefaultTenant`、`RequireTenant`、`TenantClaim`、`ValidateAgainstStore` |
| `XiHan:MultiTenancy:InMemory` | [MultiTenancy.InMemory](/packages/multitenancy-inmemory) | `MaxTenants` |
| `XiHan:MultiTenancy:Tenants` | [MultiTenancy.InMemory](/packages/multitenancy-inmemory) | 从配置装载的租户（`DisplayName`、`IsEnabled`、`Properties`） |
| `XiHan:MultiTenancy:Database` | [MultiTenancy.Database](/packages/multitenancy-database) | `CacheSize`、`CacheTimeToLiveSeconds`、`MaxListedTenants` |
| `XiHan:Security` | [Security.Abstractions](/packages/security-abstractions) | `RequireAuthentication`、`RoleClaim` |
| `XiHan:Security:Outbound` | [Kernel](/packages/kernel) | [出站地址策略](/security/outbound)：`AllowLoopback`、`AllowPrivateNetwork`、`AllowedNetworks`、`AllowedHosts` |
| `XiHan:Security:InMemory` | [Security.InMemory](/packages/security-inmemory) | 开发用主体与授予（`Principals`、`Grants`） |
| `XiHan:Security:Database` | [Security.Database](/packages/security-database) | `CacheSize`、`CacheTimeToLiveSeconds` |
| `XiHan:Security:Jwt` | [Security.Jwt](/packages/security-jwt) | `Issuer`、`Audience`、`Secret`、`ClockSkewSeconds`、`NameClaim` |
| `XiHan:Security:OpenIdConnect` | [Security.OpenIdConnect](/packages/security-openidconnect) | `Authority`、`Audience`、元数据地址与刷新间隔 |
| `XiHan:Security:Ip2Region` | [Security.Ip2Region](/packages/security-ip2region) | xdb 数据文件路径 `V4Database`、`V6Database` |
| `XiHan:Caching:InMemory` | [Caching.InMemory](/packages/caching-inmemory) | `MaxEntries`、`DefaultTimeToLiveSeconds` |
| `XiHan:Caching:Redis` | [Caching.Redis](/packages/caching-redis) | `Configuration`、`KeyPrefix`、`DefaultTimeToLiveSeconds`、`PublishChanges`、`HealthProbeIntervalSeconds` |
| `XiHan:Caching:Hybrid` | [Caching.Hybrid](/packages/caching-hybrid) | `LocalCapacity`、`LocalTimeToLiveSeconds` |
| `XiHan:Caching:Remote` | [Caching.Remote](/packages/caching-remote) | `TimeoutMilliseconds` |
| `XiHan:Caching:Idempotency` | [Caching.Abstractions](/packages/caching-abstractions) | 幂等记录的 `TimeToLiveSeconds`、`InProgressTimeToLiveSeconds` |
| `XiHan:Messaging` | [Messaging.Abstractions](/packages/messaging-abstractions) | `MaxAttempts`、`DeadLetterSuffix`、`InstanceId` |
| `XiHan:Messaging:Outbox` | [Messaging.Abstractions](/packages/messaging-abstractions) | 发件箱的 `PollIntervalSeconds`、`BatchSize`、`MaxAttempts`、`RetryDelaySeconds`、`MaxRetryDelaySeconds`、`DispatchedRetentionSeconds` |
| `XiHan:Messaging:InMemory` | [Messaging.InMemory](/packages/messaging-inmemory) | `MaxQueuedMessages` |
| `XiHan:Messaging:RabbitMQ` | [Messaging.RabbitMQ](/packages/messaging-rabbitmq) | `Configuration`、`QueuePrefix`、`PrefetchCount`、`HealthProbeIntervalSeconds` |
| `XiHan:Messaging:Kafka` | [Messaging.Kafka](/packages/messaging-kafka) | `BootstrapServers`、`GroupId`、`TopicPrefix`、`HealthProbeIntervalSeconds`、`RedeliveryDelaySeconds`、`Properties:<key>` |
| `XiHan:Scheduling` | [Scheduling.Abstractions](/packages/scheduling-abstractions) | 调度引擎与领导选举：轮询、租约、并发、错过阈值、历史保留 |
| `XiHan:Scheduling:InMemory` | [Scheduling.InMemory](/packages/scheduling-inmemory) | `MaxRuns` |
| `XiHan:Scheduling:Database` | [Scheduling.Database](/packages/scheduling-database) | `HealthProbeIntervalSeconds` |
| `XiHan:Scheduling:Kubernetes` | [Scheduling.Kubernetes](/packages/scheduling-kubernetes) | Lease 选举：`Lease`、`Namespace`、`Holder`、`ApiServer` |
| `XiHan:Workflow` | [Workflow.Abstractions](/packages/workflow-abstractions) | 引擎：锁超时、子流程深度、表达式缓存、保留天数 |
| `XiHan:Workflow:InMemory` | [Workflow.InMemory](/packages/workflow-inmemory) | 各存储容量、轮询周期与批大小 |
| `XiHan:Workflow:Database` | [Workflow.Database](/packages/workflow-database) | 到期书签的轮询与批大小、租约时长、`HolderId` |
| `XiHan:Localization` | [Localization.Abstractions](/packages/localization-abstractions) | `DefaultLocale`、`Locales` |
| `XiHan:Localization:Database` | [Localization.Database](/packages/localization-database) | `RefreshIntervalSeconds`、`MaxTranslations` |
| `XiHan:ObjectStorage:FileSystem` | [ObjectStorage.FileSystem](/packages/objectstorage-filesystem) | `RootPath`、`MaxTotalBytes`、`MaxBlobBytes`、`MultipartExpiryHours` |
| `XiHan:ObjectStorage:S3` | [ObjectStorage.S3](/packages/objectstorage-s3) | `ServiceUrl`、`Region`、`AccessKey`、`SecretKey`、`ForcePathStyle` 等 |
| `XiHan:ObjectStorage:Stores` | [ObjectStorage.Abstractions](/packages/objectstorage-abstractions) | 命名存储实例：`<name>:Provider` 加该 Provider 的设置 |
| `XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds` | [ObjectStorage.Abstractions](/packages/objectstorage-abstractions) | 命名实例健康探测的间隔（单个键） |
| `XiHan:ObjectStorage:SignedUrls` | [Hosting.AspNetCore](/packages/hosting-aspnetcore) | 宿主代收发预签名地址的 `BaseUrl`、`Secret` |
| `XiHan:Configuration:Nacos` | [Configuration.Nacos](/packages/configuration-nacos) | Nacos 服务地址、分组、命名空间、登录与超时 |
| `XiHan:Auditing` | [Auditing.Abstractions](/packages/auditing-abstractions) | `Selection`、`IncludeSuccesses` |
| `XiHan:Auditing:InMemory` | [Auditing.InMemory](/packages/auditing-inmemory) | `Capacity`、`LogEvents` |
| `XiHan:Auditing:Database` | [Auditing.Database](/packages/auditing-database) | `QueueCapacity`、`BatchSize`、`FlushIntervalSeconds`、`LogEvents` |
| `XiHan:Observability:Host` | [Observability.Host](/packages/observability-host) | `SampleIntervalSeconds` |
| `XiHan:Observability:OpenTelemetry` | [Observability.OpenTelemetry](/packages/observability-opentelemetry) | 服务名、追踪、指标、日志、采样与 OTLP 导出 |
| `XiHan:AI` | [AI.Abstractions](/packages/ai-abstractions) | Provider 表、工具名、护栏、知识库与提示词 |
| `XiHan:Protocols:Http` | [Protocols.Http](/packages/protocols-http) | `RoutePrefix`，`Routes:<操作 ID>` 覆盖推导的路由 |
| `XiHan:Protocols:Grpc` | [Protocols.Grpc](/packages/protocols-grpc) | `Package`，`Methods:<操作 ID>` 覆盖推导的方法名 |
| `XiHan:Protocols:Mcp` | [Protocols.Mcp](/packages/protocols-mcp) | `ServerName`、`ServerVersion`、`Instructions`，`Tools:<操作 ID>` |
| `XiHan:Protocols:CloudEvents` | [Protocols.CloudEvents](/packages/protocols-cloudevents) | `Inbound`、`Outbound`、`Topics:<事件类型>`、`Webhooks:<主题>` |
| `XiHan:Plugins:OutOfProcess` | [Plugins.OutOfProcess](/packages/plugins-outofprocess) | 插件目录、授予、握手与启停超时、健康与重启限制 |
| `XiHan:Plugins:Wasm` | [Plugins.Wasm](/packages/plugins-wasm) | 插件目录、授予、挂载、内存与栈上限、调用超时 |

各键的类型与缺省值以对应包页为准。

## 常见问题

### 改了 appsettings.json 里的 XiHan:* 键，为什么运行中的应用没反应

框架选项在 `AddXiHan` 装配时读取一次并登记为单例，改 `XiHan:*` 下的键要重启。只有读 `IConfiguration` 或 `IOptionsMonitor<T>` 的应用代码会随文件变化更新。

### 应用的配置节能放在 XiHan 下吗

不要。`XiHan:<领域>` 保留给框架与 Provider 包，领域词来自规范术语表；应用放进去会与将来新增的包冲突。用应用自己的名字做顶层节。

### 配置值写错了会怎样

框架选项的 `FromConfiguration` 对负数、无法解析的枚举与布尔值抛出 `ArgumentException`，启动失败并在消息里给出键名。应用选项用 `ValidateOnStart()` 得到同样的效果。

### 怎样知道某个键的缺省值

以对应包的参考页为准，各页的「配置」小节列出键、类型与缺省；上面的一览表只列键名与用途。

### 远程配置里的键能覆盖 appsettings 吗

能，前提是它在 `AddXiHan` 之前加进 `builder.Configuration`：后加入的配置源覆盖先加入的。完整顺序见 [配置目录与热更新](/configuration/config-directory)的「配置源的优先级」。

## 相关包与下一步

- [Kernel](/packages/kernel)：`KernelOptions`、`ResilienceOptions` 与 `XiHan:Kernel` 配置节
- [Configuration.Abstractions](/packages/configuration-abstractions)：远程配置与密钥的契约
- [Hosting.AspNetCore](/packages/hosting-aspnetcore)：装载配置目录的 Web 宿主
- 下一页：[配置目录与热更新](/configuration/config-directory)
