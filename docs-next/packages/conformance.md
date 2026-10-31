# 一致性套件

全部 `<领域>.Conformance` 包与插件协议的 `Plugins.Conformance` 汇总：每个包是一组 xUnit v3 抽象测试类，Provider（或插件）的测试项目继承基类、实现工厂方法，全部用例随之执行。它们保证同一能力的不同实现语义一致，换 Provider 只改包引用与配置。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.<领域>.Conformance`（13 个，见下文各节）与 `XiHan.Framework.Plugins.Conformance` |
| 环层 | 工具链 |
| 能力 | — |
| 特性入口 | —（抽象测试基类） |
| 配置节 | — |
| 依赖 | 各领域的 `.Abstractions`（部分套件另引用 `XiHan.Framework.Testing`）；`xunit.v3.assert` 4.0.2、`xunit.v3.extensibility.core` 4.0.2（不引用元包 `xunit.v3`，套件包不是可执行测试项目） |
| AOT | 不兼容（测试项目不以 AOT 发布） |
| 稳定性 | 按基类的 `[ApiLevel]`，各节写明 |

## 用法

1. 新建测试项目，引用被测实现、对应的 Conformance 包与 `xunit.v3`，测试项目的其余配置见[测试与一致性套件](../guide/testing#测试项目)。
2. 写一个 `public sealed class` 继承套件基类，实现抽象的工厂方法；需要时覆写虚成员（时限、配置、容器）。
3. `dotnet test --project <测试项目>` 运行，套件的每个 `[Fact]` 都作为这个类的用例出现。

```xml
<PackageReference Include="XiHan.Framework.Caching.Conformance" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。依赖真实服务的实现（Redis、RabbitMQ、Kafka、PostgreSQL、S3、Nacos）在框架仓库里以 `XIHAN_TEST_*` 环境变量提供连接，没设置时用 `Assert.SkipWhen` 跳过；自己的 Provider 可以照这个做法。

| 套件包 | 基类 | 用例数 |
| --- | --- | --- |
| [AI](#ai) | `KnowledgeStoreConformance`、`AIProviderConformance` | 3 + 2 |
| [Auditing](#auditing) | `AuditTrailConformance` | 4 |
| [Caching](#caching) | `CacheStoreConformance` | 15 |
| [Configuration](#configuration) | `RemoteConfigurationClientConformance`、`SecretStoreConformance` | 3 + 2 |
| [Localization](#localization) | `MessageLocalizerConformance` | 6 |
| [Messaging](#messaging) | `MessageBrokerConformance`、`MessageStreamConformance` | 9 + 2 |
| [MultiTenancy](#multitenancy) | `TenantStoreConformance`、`TenantShardResolverConformance` | 4 + 3 |
| [ObjectStorage](#objectstorage) | `BlobStoreConformance`、`BlobStoreProviderConformance` | 10 + 4 |
| [Persistence](#persistence) | `RepositoryConformance`、`IdGeneratorConformance<TKey>` | 21 + 3 |
| [Plugins](#plugins) | `PluginProtocolConformance`、`WasmPluginConformance` | 7 + 7 |
| [Scheduling](#scheduling) | `JobSchedulerConformance`、`JobStoreConformance`、`LeaderElectionConformance` | 15 + 5 + 2 |
| [Security](#security) | `PermissionGrantStoreConformance` | 4 |
| [Workflow](#workflow) | `WorkflowStoreConformance`、`WorkflowEngineConformance`、`WorkflowTaskConformance` | 5 + 23 + 7 |

## AI

包 ID `XiHan.Framework.AI.Conformance`（RFC-0022，Preview），命名空间按功能区分为 `.Knowledge`、`.Providers`、`.Embeddings`。

| 基类 | 被测接口 | 实现 | 覆盖 |
| --- | --- | --- | --- |
| `KnowledgeStoreConformance` | `IKnowledgeStore` | `StartAsync()` 返回 `KnowledgeStoreHost(store, dimensions, host)` | 写入与按相似度排序、`top`、按文档与元数据过滤、同 ID 重写入替换、按文档删除、集合隔离、空集合搜索 |
| `AIProviderConformance` | `IAIProviderCatalog` 上的聊天与嵌入 Provider | `StartAsync()` 返回 `AIProviderHost(services, providerName, embeddingDimensions, host)` | 应答、嵌入维度与批量条数、缓存与 `Invalidate`、未知名字 `AI_NOTFOUND_001` |

`HashingEmbeddingGenerator`、`HashingEmbeddingGeneratorFactory` 与提供 `ai.embedding@1` 的 `HashingEmbeddingFeature` 是确定性的词袋哈希嵌入（`Kind = hashing`），让知识库 Provider 的测试不依赖外部模型。仓库里 `AI.VectorData` 与 `AI.OpenAI` 通过本套件。

## Auditing

包 ID `XiHan.Framework.Auditing.Conformance`（RFC-0015，Preview）。基类 `AuditTrailConformance`，实现 `CreateFixtureAsync()` 返回 `AuditTrailFixture`（抽象属性 `Sink` 是被测 `IAuditSink`，`Trail` 是被测 `IAuditTrail`）。用例覆盖写入后按写入顺序读回且字段完整往返、各过滤项取交集、分页与续页令牌、不合法查询是 VALIDATION。用例以 trace-id 隔离数据，夹具可以共用一个存储。`Auditing.InMemory` 与 `Auditing.Database` 通过本套件。

## Caching

包 ID `XiHan.Framework.Caching.Conformance`（RFC-0006，Stable）。任何 `ICacheStore` 实现（无论提供 `caching.local` 还是 `caching.distributed`）都继承 `CacheStoreConformance`，实现 `CreateStore()`，每个用例创建一个新存储。

覆盖：读写与未命中、覆盖、键区分大小写、删除并报告是否存在、批量读写保持顺序与后写覆盖、批量的逐项过期与滑动延长、批量删除逐项结果、空批量、不存在才写只有一个赢家（含并发）、比较删、绝对过期、滑动过期在读取时延长、大值、基于存储的分布式锁的互斥 / 释放 / 到期，以及存储实现 `ICacheStoreInspector` 时的按前缀分页列键、读元数据与按前缀删除（不实现即跳过）。过期用例用真实时间等待，`ExpirationGrace`（缺省 300 毫秒）可按 Provider 调整；`Key()`、`Bytes(text)`、`Text(value)` 是供派生类补充用例的辅助方法。

```csharp
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.Conformance;
using XiHan.Framework.Caching.InMemory;

public sealed class InMemoryCacheStoreConformanceTests : CacheStoreConformance
{
    protected override ICacheStore CreateStore() => new InMemoryCacheStore(new InMemoryCachingOptions(), TimeProvider.System);
}
```

`Caching.InMemory`、`Caching.Redis` 与 `Caching.Remote`（适配器 + Go 插件 `samples/CacheGoPlugin`）通过本套件。

## Configuration

包 ID `XiHan.Framework.Configuration.Conformance`（RFC-0014，Preview），命名空间 `.Remote` 与 `.Secrets`。

| 基类 | 被测接口 | 实现 | 覆盖 |
| --- | --- | --- | --- |
| `RemoteConfigurationClientConformance` | `IRemoteConfigurationClient` | `CreateFixtureAsync()` 返回 `RemoteConfigurationFixture`：抽象属性 `Client` 与向远端发布文档的 `PublishAsync(namespaceName, values, cancellationToken)` | 取整份文档与版本、已知版本未变化返回无值（长轮询实现须在 `UnchangedWait` 内返回，缺省 3 秒）、再次发布后版本变化、未知命名空间 NOTFOUND |
| `SecretStoreConformance` | `ISecretStore` | `CreateFixtureAsync()` 返回 `SecretStoreFixture`：抽象属性 `Store` 与写入密钥的 `PutAsync(name, value, cancellationToken)` | 批量逐项按序返回、重复项、缺失 NOTFOUND、空集合、遮蔽的文本表示、覆盖后读新值 |

`Configuration.Nacos` 通过远程配置套件。

## Localization

包 ID `XiHan.Framework.Localization.Conformance`（RFC-0010，Preview）。基类 `MessageLocalizerConformance`，实现 `CreateLocalizer(catalogs, options)` 返回被测 `IMessageLocalizer`，实现同时必须是 `IMessageResolver`。用例覆盖回退链（精确区域 → 同语言的启用区域 → 默认区域 → 源语言）、按命中区域的文化格式化、缺模板或缺参数时退回描述、错误按键与细节翻译、按调用上下文的 Baggage 区域、先注册的目录优先。`Localization.InMemory` 与 `Localization.Database` 通过本套件。

## Messaging

包 ID `XiHan.Framework.Messaging.Conformance`（RFC-0009）。

| 基类 | 稳定性 | 实现 | 覆盖 |
| --- | --- | --- | --- |
| `MessageBrokerConformance` | Stable | `CreateFeature()` 返回提供 `messaging.queue@1` 的特性；可覆写 `Configuration`、`DeliveryTimeout`（缺省 10 秒） | 信封属性、扩展与数据（含二进制）完整到达处理器；同一主题按发布顺序投递；主题互不串扰；批量发布逐项结果、空批量、非法主题整批拒绝；处理器失败或抛异常时重试到 `MaxAttempts`（2）后停止且不阻塞后续消息；租户、关联与追踪进入调用上下文；非法主题被发布者拒绝 |
| `MessageStreamConformance` | Preview | 继承 `MessageBrokerConformance`，同样实现 `CreateFeature()` | 在队列用例之上再验证 `messaging.stream@1`：同一分区键按发布顺序投递、按时刻重放 |

用例以 `TestHost` 走完整管道，主题常量 `OrdersTopic`、`ShipmentsTopic`，`DeliveryLog` / `RecordingHandler` / `ReceivedMessage` 是套件登记的记录器。提供流能力的 Provider 只继承 `MessageStreamConformance`，不必再继承队列套件。`Messaging.InMemory`、`Messaging.RabbitMQ` 通过队列套件，`Messaging.Kafka` 通过流套件。

```csharp
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Messaging.InMemory;

public sealed class InMemoryMessageBrokerConformanceTests : MessageBrokerConformance
{
    protected override IFeature CreateFeature() => new InMemoryMessagingFeature();
}
```

## MultiTenancy

包 ID `XiHan.Framework.MultiTenancy.Conformance`（RFC-0007）。

| 基类 | 稳定性 | 实现 | 覆盖 |
| --- | --- | --- | --- |
| `TenantStoreConformance` | Stable | `CreateStoreAsync(tenants)` 返回装好给定租户的 `ITenantStore`；种子租户 `Acme`、`Dormant` | 按 ID 查找、不存在为无值、ID 区分大小写、显示名、启用标记与属性完整往返、枚举全部租户 |
| `TenantShardResolverConformance` | Preview | `CreateResolverAsync(tenants)` 返回 `ITenantShardResolver`；种子租户 `Sharded`、`Shared` | 分库租户解析出连接串、小写的提供者名与去前缀的透传属性；共库与未知租户为成功无值；失效单个或全部后重解析结果一致 |

`MultiTenancy.InMemory` 与 `MultiTenancy.Database` 通过两套用例。

## ObjectStorage

包 ID `XiHan.Framework.ObjectStorage.Conformance`（RFC-0012，Preview）。

| 基类 | 实现 | 覆盖 |
| --- | --- | --- |
| `BlobStoreConformance` | `CreateStore()` 返回 `IBlobStore`；可覆写 `Container`（缺省 `conformance`） | 读写往返与元数据、覆盖与不覆盖、键区分大小写与嵌套段、批量写入逐项结果、批量删除逐项结果、按前缀分页列举、范围读取、分片上传（任意顺序上传、原子完成、非法分片列表、覆盖与中止）、非法标识提前拒绝、大对象流式读写 |
| `BlobStoreProviderConformance` | `CreateProvider()` 返回 `IBlobStoreProvider`，`ValidSettings()` 与 `OtherValidSettings()` 给两组可用设置 | 按定义建出的实例可读写且探测通过、未知设置键被拒绝、经注册表同定义复用 / 定义变了替换 / 移除后不存在、定义错误翻译为 `OBJECTSTORAGE_VALIDATION_003` 且不影响已有实例 |

`ObjectStorage.FileSystem` 与 `ObjectStorage.S3` 通过两套用例。

## Persistence

包 ID `XiHan.Framework.Persistence.Conformance`（RFC-0005）。

| 基类 | 稳定性 | 实现 | 覆盖 |
| --- | --- | --- | --- |
| `RepositoryConformance` | Stable | `CreateFeature()` 返回提供 `persistence.relational@1` 的特性；可覆写 `Configuration`、`ConfigureServices`、`ConfigureApp` | 读己之写与提交可见性、标识重复、审计字段与并发戳、软删除与物理删除、租户必填与隔离、过滤排序分页与计数（含可空值类型与 null 的相等 / 不等按 `IS NULL` 语义）、失败结果与异常回滚、Transaction 阶段之外使用仓储抛错、标识生成器 |
| `IdGeneratorConformance<TKey>` | Preview | `CreateGenerator(TimeProvider clock)` 返回 `IIdGenerator<TKey>`（`TKey : IComparable<TKey>`） | 顺序生成与 8 线程并发生成不重复（真实时钟）、时钟前进后标识更大（`ConformanceClock`） |

`Fixtures` 命名空间里的 `ConformanceOrder`、`ConformanceNote`、`ConformanceItem` 等是套件自带的实体，`ConformanceFeature` 登记它们；EF Core、SqlSugar 这类需要映射的 Provider 在 `ConfigureServices` / `ConfigureApp` 里为这些实体建模。`RunAsync`、`FindAsync`、`ListAsync`、`CountAsync` 等静态辅助方法供派生类补充用例。

```csharp
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Conformance;
using XiHan.Framework.Persistence.InMemory;

public sealed class InMemoryRepositoryConformanceTests : RepositoryConformance
{
    protected override IFeature CreateFeature() => new InMemoryPersistenceFeature();
}
```

`Persistence.InMemory`、`Persistence.EntityFrameworkCore`（SQLite）、`Persistence.SqlSugar`（SQLite，设置了 `XIHAN_TEST_POSTGRES` 时另跑 PostgreSQL）通过仓储套件；`GuidV7IdGenerator`（`Guid`）与 `SnowflakeIdGenerator`（`long`）通过标识生成器套件。

## Plugins

包 ID `XiHan.Framework.Plugins.Conformance`（RFC-0017、RFC-0018）：`xihan.plugin.v1` 插件的黑盒套件，以插件的构建产物目录为输入，任何语言写的插件都用它验证。依赖 `Plugins.Protocol`、`Plugins.OutOfProcess`（进程启动）、`Plugins.Wasm`（模块校验与实例）与 `Wasmtime` 48.0.2。

| 基类 | 稳定性 | 实现 | 可覆写 |
| --- | --- | --- | --- |
| `PluginProtocolConformance` | Preview | `PluginDirectory`：含 `xihan.manifest.json` 与 `entry` 指向的可执行文件 | `Timeout`（每次协议调用，缺省 15 秒）、`ConfigurationJson`（缺省 `{}`）、`SampleRequests`（没有样例的操作以 `{}` 调用）、`ExcludedOperations`（故意崩溃或依赖宿主回调的操作） |
| `WasmPluginConformance` | Experimental | `PluginDirectory`：含清单与模块文件 | `ConfigurationJson`、`SampleRequests`、`ExcludedOperations` |

进程外用例（以宿主身份启动进程）：握手行合法、协商出宿主支持的版本、原语层版本相同、清单与目录里的文件一致、操作与订阅在清单声明范围内；没有共同协议版本时以 `FailedPrecondition` 拒绝；`Configure` 与 `Start` 成功，`Health` 与 `WatchHealth` 报可服务状态且健康项都已声明，`Stop` 后进程在宽限内退出；未声明的操作以 gRPC `Unimplemented` 结束；每个声明的一元操作都返回带 `status` 的应答（成功时载荷是 JSON，失败时错误码合法）；向声明的订阅投递消息得到确认；stdin 关闭后进程自行退出。

Wasm 用例（在进程内实例化模块）：模块是 reactor、导出与导入表符合 ABI v1、`xihan_abi_version` 返回 1；握手协商出版本 1、清单一致、声明在范围内、没有流式操作，声明了订阅 / 中间件就导出了对应函数；`xihan_configure` / `xihan_start` 成功、`xihan_health` 报可服务状态、`xihan_stop` 成功；未声明的操作以 `KERNEL_NOTFOUND_001` 的失败状态应答；每个声明的操作都返回带 `status` 的应答；订阅投递得到确认；每个贡献的中间件在 before 阶段返回合法动作（短路时带失败结果），在 after 阶段也应答。

```csharp
using XiHan.Framework.Plugins.Conformance;

public sealed class EchoWasmPluginConformanceTests : WasmPluginConformance
{
    protected override string PluginDirectory => Path.Combine(AppContext.BaseDirectory, "plugins", "echo-wasm");

    // 故意陷阱与忙等的操作不调用
    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal) { "echoes.trap", "echoes.spin" };
}
```

两套用例都不提供宿主回调服务：Wasm 套件提供的宿主导入只有 `xihan.log`，`xihan.invoke` / `xihan.publish` 返回不可用状态；回调的令牌与权限、崩溃重启由宿主实现的测试覆盖。跑 Wasm 套件的测试项目在 Windows 上要设 `<CETCompat>false</CETCompat>`，否则套件先以 `PLUGINS_UNAVAILABLE_003` 拒绝。.NET 之外的插件也可以用规范仓库的 Go 运行器 `xihan-conformance`，用例相同（[测试与一致性套件](../guide/testing#plugins-conformance-与-xihan-conformance)）。`samples/EchoPlugin`、`samples/EchoGoPlugin` 通过进程外用例，`samples/EchoWasmPlugin` 通过 Wasm 用例。

## Scheduling

包 ID `XiHan.Framework.Scheduling.Conformance`（RFC-0013，Preview）。

| 基类 | 实现 | 覆盖 |
| --- | --- | --- |
| `JobSchedulerConformance` | `StartAsync(configure, configuration)` 启动装配了被测调度器的宿主并返回 `JobSchedulerHost(scheduler, host)`；回调 `configure` 里套件登记 `RecordingJob` 与 `RecordingJobLog`；可覆写 `WaitTimeout`（缺省 10 秒） | 手动触发经管道运行并记录结果、未登记的作业、间隔计划反复运行、不允许并发时的冲突、作业异常翻译为失败、状态报告、失败重试（成功即止、次数用尽即止、每实例作业在本实例重试）、执行时限超时与取消不重试、重试策略与时限的运行时修改和恢复 |
| `JobStoreConformance` | `CreateAsync()` 返回 `(IJobStore Store, IAsyncDisposable Host)` | 作业写入 / 查找 / 列表 / 替换 / 级联删除与快照语义、触发器全字段往返、到期抢占跨节点原子且按到期时刻与优先级排序、独占运行与阻塞 / 完成解除 / 查询 / 取消 / 清理、节点心跳与失联恢复 |
| `LeaderElectionConformance` | `CreateElection()` 返回 `ILeaderElection`；可覆写 `LeaseTimeToLive`（缺省 400 毫秒）、`ExpirationGrace`（缺省 300 毫秒） | 获取、被占用时拒绝、释放后可再获取、过期后可被他人获取且原持有者续约失败、续约延长到期 |

`RecordingJob` 认运行数据里的 `failBeforeAttempt` 与 `delayMilliseconds`，用来构造重试与超时场景。`Scheduling.InMemory`、`Scheduling.Database` 通过三套用例，`Scheduling.Kubernetes` 通过领导选举用例（以内存 API Server 桩验证）。

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Scheduling.InMemory;
using XiHan.Framework.Testing;

public sealed class InMemoryJobSchedulerConformanceTests : JobSchedulerConformance
{
    protected override async Task<JobSchedulerHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySchedulingFeature>(), configuration, configure);
        return new JobSchedulerHost(host.Services.GetRequiredService<IJobScheduler>(), host);
    }
}
```

## Security

包 ID `XiHan.Framework.Security.Conformance`（RFC-0008，Stable）。基类 `PermissionGrantStoreConformance`，实现 `CreateStoreAsync(grants)` 返回装好给定授予的 `IPermissionGrantStore`。用例覆盖用户与角色授予、全局授予对任何租户生效、租户专属授予只对该租户生效、未知主体为空、主体与权限区分大小写、通配授予 `*` 原样返回、空存储。`Security.InMemory` 与 `Security.Database` 通过本套件。

## Workflow

包 ID `XiHan.Framework.Workflow.Conformance`（RFC-0021，Preview），命名空间 `.Storage` 与 `.Engine`。

| 基类 | 实现 | 覆盖 |
| --- | --- | --- |
| `WorkflowStoreConformance` | `StartAsync()` 返回 `WorkflowStoreHost(definitions, instances, bookmarks, instanceLock, host)` | 定义按 ID / 名字 / 版本 / 最新已发布查找、列表排序与过滤、批量写与删、级联、快照语义、书签按到期 / 种类与键 / 信号查询、实例锁的互斥与超时（锁等待上限配置在 1 秒以内） |
| `WorkflowEngineConformance` | `StartAsync(configure, configuration)` 返回 `WorkflowEngineHost(services, poll, host)`，`poll` 轮询一次到期书签；可覆写 `WaitTimeout`（缺省 10 秒） | 顺序流与事件、决策与默认出边、并行与汇合、用户任务 Any / All / Sequential、转办与加签、延时与超时、信号、子流程与 ForEach、重试与继续、挂起 / 恢复 / 取消 / 终止与补偿、定义生命周期与结果处理器、死循环保护、租户隔离 |
| `WorkflowTaskConformance` | 同 `WorkflowEngineConformance` 的 `StartAsync` | 会签阈值（按人数或百分比）、认领与释放、退回与历史、催办事件、条件查询与分页、任务元数据（优先级、到期、表单键）与周期提醒、外部任务的领取 / 完成 / 失败 / 锁 |

`ConformanceLog`（兼消息发布者）、`RecordActivity`、`RecordingOutcomeHandler` 是套件登记进容器的记录器、活动与结果处理器。`Workflow.InMemory` 与 `Workflow.Database` 通过三套用例。

## 相关

- [测试与一致性套件](../guide/testing)、[扩展与二次开发](../guide/extending)
- [Testing 测试基座](./testing)
