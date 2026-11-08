来源：https://framework.docs.xihanfun.com/packages/conformance

# 一致性套件

全部 `<领域>.Conformance` 包与插件协议的 `Plugins.Conformance` 汇总：每个包是一组 xUnit v3 抽象测试类，Provider（或插件）的测试项目继承基类、实现工厂方法，全部用例随之执行。它们保证同一能力的不同实现语义一致，换 Provider 只改包引用与配置。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.<领域>.Conformance`（13 个，见下文各节）与 `XiHan.Framework.Plugins.Conformance` |
| 环层 | 工具链 |
| 能力 | — |
| 特性入口 | —（抽象测试基类） |
| 配置节 | — |
| 依赖 | 各领域的 `.Abstractions`（部分套件另引用 `XiHan.Framework.Testing`）；`xunit.v3.assert`、`xunit.v3.extensibility.core`（不引用元包 `xunit.v3`，套件包不是可执行测试项目） |
| AOT | 不兼容（测试项目不以 AOT 发布） |
| 稳定性 | 按基类的 `[ApiLevel]`，各节写明 |

## 用法

1. 新建测试项目，引用被测实现、对应的 Conformance 包与 `xunit.v3`，测试项目的其余配置见[测试与一致性套件](/testing/basics)。
2. 写一个 `public sealed class` 继承套件基类，实现抽象的工厂方法；需要时覆写虚成员（时限、配置、容器）。
3. `dotnet test --project <测试项目>` 运行，套件的每个 `[Fact]` 都作为这个类的用例出现。

```bash
dotnet add package XiHan.Framework.Caching.Conformance --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。依赖真实服务的实现（Redis、RabbitMQ、Kafka、PostgreSQL、SQL Server、MySQL、Oracle、S3、Nacos）在框架仓库里以 `XIHAN_TEST_*` 环境变量提供连接，没设置时用 `Assert.SkipWhen` 跳过；自己的 Provider 可以照这个做法。

| 套件包 | 基类 | 用例数 |
| --- | --- | --- |
| [AI](#ai) | `KnowledgeStoreConformance`、`AIProviderConformance` | 4 + 5 |
| [Auditing](#auditing) | `AuditTrailConformance` | 4 |
| [Caching](#caching) | `CacheStoreConformance` | 16 |
| [Configuration](#configuration) | `RemoteConfigurationClientConformance`、`SecretStoreConformance` | 3 + 2 |
| [Localization](#localization) | `MessageLocalizerConformance` | 6 |
| [Messaging](#messaging) | `MessageBrokerConformance`、`MessageStreamConformance`、`OutboxConformance` | 14 + 2 + 4 |
| [MultiTenancy](#multitenancy) | `TenantStoreConformance`、`TenantShardResolverConformance` | 4 + 3 |
| [ObjectStorage](#objectstorage) | `BlobStoreConformance`、`BlobStoreProviderConformance` | 13 + 4 |
| [Persistence](#persistence) | `RepositoryConformance`、`IdGeneratorConformance<TKey>` | 36 + 3 |
| [Plugins](#plugins) | `PluginProtocolConformance`、`WasmPluginConformance` | 7 + 8 |
| [Scheduling](#scheduling) | `JobSchedulerConformance`、`JobStoreConformance`、`LeaderElectionConformance` | 16 + 6 + 3 |
| [Security](#security) | `PermissionGrantStoreConformance` | 5 |
| [Workflow](#workflow) | `WorkflowStoreConformance`、`WorkflowEngineConformance`、`WorkflowTaskConformance` | 15 + 40 + 8 |

## AI

包 ID `XiHan.Framework.AI.Conformance`（RFC-0022，Preview），命名空间按功能区分为 `.Knowledge`、`.Providers`、`.Embeddings`。

| 基类 | 被测接口 | 实现 | 覆盖 |
| --- | --- | --- | --- |
| `KnowledgeStoreConformance` | `IKnowledgeStore` | `StartAsync()` 返回 `KnowledgeStoreHost(store, dimensions, host)` | 写入与按相似度排序、`top`、按文档与元数据过滤、同 ID 重写入替换、按文档删除、按块序号裁剪、集合隔离、空集合搜索 |
| `AIProviderConformance` | `IAIProviderCatalog` 上的聊天与嵌入 Provider | `StartAsync()`（被测 Provider 是聊天与嵌入的默认）与 `StartWithoutDefaultAsync()`（目录没有默认）返回 `AIProviderHost(services, providerName, embeddingDimensions, host)`；能把聊天与嵌入的默认分开的目录再覆写 `StartWithSeparateDefaultsAsync()`，夹具以 `EmbeddingProviderName` 给出默认嵌入 Provider，不覆写时该例跳过 | 应答、嵌入维度与批量条数、缓存与 `Invalidate`、未知名字 `AI_NOTFOUND_001`；默认名：有默认时不写名字的解析与 `IAIChat` 用它，没有默认时 `AI_NOTFOUND_001`（`ai.provider.default_missing` / `ai.provider.embedding_default_missing`），分开的默认各按各的解析 |

`HashingEmbeddingGenerator`、`HashingEmbeddingGeneratorFactory` 与提供 `ai.embedding@1` 的 `HashingEmbeddingFeature` 是确定性的词袋哈希嵌入（`Kind = hashing`），让知识库 Provider 的测试不依赖外部模型。仓库里 `AI.VectorData` 与 `AI.OpenAI` 通过本套件。

## Auditing

包 ID `XiHan.Framework.Auditing.Conformance`（RFC-0015，Preview）。基类 `AuditTrailConformance`，实现 `CreateFixtureAsync()` 返回 `AuditTrailFixture`（抽象属性 `Sink` 是被测 `IAuditSink`，`Trail` 是被测 `IAuditTrail`）。用例覆盖写入后按写入顺序读回且字段完整往返、各过滤项取交集、分页与续页令牌、不合法查询是 VALIDATION。用例以 trace-id 隔离数据，夹具可以共用一个存储。`Auditing.InMemory` 与 `Auditing.Database` 通过本套件。

## Caching

包 ID `XiHan.Framework.Caching.Conformance`（RFC-0006，Stable）。任何 `ICacheStore` 实现（无论提供 `caching.local` 还是 `caching.distributed`）都继承 `CacheStoreConformance`，实现 `CreateStore()`，每个用例创建一个新存储。

覆盖：读写与未命中、覆盖、键区分大小写、删除并报告是否存在、批量读写保持顺序与后写覆盖、批量的逐项过期与滑动延长、批量删除逐项结果、空批量、不存在才写只有一个赢家（含并发）、比较删、绝对过期、滑动过期在读取时延长、大值、基于存储的分布式锁的互斥 / 释放 / 到期，以及存储实现 `ICacheStoreInspector` 时的按前缀分页列键、读元数据与按前缀删除、每页上限越界（0、1001）返回 `CACHING_VALIDATION_003` 而边界值（1、1000）照常（不实现即跳过）。过期用例用真实时间等待，`ExpirationGrace`（缺省 300 毫秒）可按 Provider 调整；`Key()`、`Bytes(text)`、`Text(value)` 是供派生类补充用例的辅助方法。

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

包 ID `XiHan.Framework.Localization.Conformance`（RFC-0010，Preview）。基类 `MessageLocalizerConformance`，实现 `CreateLocalizer(catalogs, options)` 返回被测 `IMessageLocalizer`，实现同时必须是 `IMessageResolver`。用例覆盖回退链（精确区域 → 同语言的启用区域 → 默认区域 → 源语言）、按命中区域的文化格式化、缺模板或缺参数时退回描述、错误按键与细节翻译、按调用上下文的 Baggage 区域、先注册的目录优先、解析器按启用列表协商调用方区域（与按上下文翻译同一区域）。`Localization.InMemory` 与 `Localization.Database` 通过本套件。

## Messaging

包 ID `XiHan.Framework.Messaging.Conformance`（RFC-0009）。

| 基类 | 稳定性 | 实现 | 覆盖 |
| --- | --- | --- | --- |
| `MessageBrokerConformance` | Stable | `CreateFeature()` 返回提供 `messaging.queue@1` 的特性；可覆写 `Configuration`、`DeliveryTimeout`（缺省 10 秒）、`SupportsMultipleInstances`（缺省真；两个宿主不能共用代理的 Provider 覆写为假，多实例用例跳过） | 信封属性、扩展与数据（含二进制）完整到达处理器；同一主题按发布顺序投递；主题互不串扰；批量发布逐项结果、空批量、非法主题整批拒绝；处理器失败或抛异常时重试到 `MaxAttempts`（2）后停止且不阻塞后续消息；租户、关联与追踪进入调用上下文；非法主题被发布者拒绝；发布到没人订阅的主题（单条与批量）成功；同一主题的竞争与广播订阅各收到一次、各自重试；两个宿主时广播每个实例都收到、竞争每条只有一个实例收到 |
| `MessageStreamConformance` | Preview | 继承 `MessageBrokerConformance`，同样实现 `CreateFeature()` | 在队列用例之上再验证 `messaging.stream@1`：同一分区键按发布顺序投递、按时刻重放 |
| `OutboxConformance` | Preview | `CreateFeature()` 返回持久化特性（发件箱的存储经仓储）；可覆写 `Configuration` | 代理对某条消息一直抛出时按尝试上限与退避进死信，同批与排在后面的照常发出；返回失败与抛出逐轮状态相同；调用方取消回滚本轮、不计尝试；不是调用方取消的 `OperationCanceledException` 计为失败（ADR-0108）。套件登记发件箱仓储与可推进的时钟，逐轮驱动 `OutboxDispatcher`，不起后台循环 |

用例以 `TestHost` 走完整管道，主题常量 `OrdersTopic`、`ShipmentsTopic`、`UnsubscribedTopic`、`NoticesTopic`，`StartWithSubscriptionsAsync` 按给定订阅启动宿主，`WarmUpBroadcastAsync` 等每个实例的广播订阅都接上，`DeliveryLog` / `RecordingHandler` / `ReceivedMessage` 是套件登记的记录器。提供流能力的 Provider 只继承 `MessageStreamConformance`，不必再继承队列套件。`Messaging.InMemory`、`Messaging.RabbitMQ` 通过队列套件，`Messaging.Kafka` 通过流套件。内存持久化、SqlSugar 的 SQLite 与 PostgreSQL、EF Core 的 SQLite 通过发件箱套件（EF Core 的上下文要把 `OutboxMessage` 放进模型）。

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
| `BlobStoreConformance` | `CreateStore()` 返回 `IBlobStore`；`PlantOutsideContractAsync(container, key, content)` 绕过契约直接写一个对象；可覆写 `Container`（缺省 `conformance`） | 读写往返与元数据、覆盖与不覆盖、并发的不覆盖写入只有一个成功、保留前缀 `.xihan` 下的键构造时拒绝且列举跳过存储里已有的这类对象、键区分大小写与嵌套段、批量写入逐项结果、批量删除逐项结果、按前缀分页列举、范围读取、分片上传（任意顺序上传、原子完成、非法分片列表、覆盖与中止）、非法标识提前拒绝、列举的页大小越界是校验错误（`OBJECTSTORAGE_VALIDATION_005`，边界照常）、大对象流式读写 |
| `BlobStoreProviderConformance` | `CreateProvider()` 返回 `IBlobStoreProvider`，`ValidSettings()` 与 `OtherValidSettings()` 给两组可用设置 | 按定义建出的实例可读写且探测通过、未知设置键被拒绝、经注册表同定义复用 / 定义变了替换 / 移除后不存在、定义错误翻译为 `OBJECTSTORAGE_VALIDATION_003` 且不影响已有实例 |

`ObjectStorage.FileSystem` 与 `ObjectStorage.S3` 通过两套用例。

## Persistence

包 ID `XiHan.Framework.Persistence.Conformance`（RFC-0005）。

| 基类 | 稳定性 | 实现 | 覆盖 |
| --- | --- | --- | --- |
| `RepositoryConformance` | Stable | `CreateFeature()` 返回提供 `persistence.relational@1` 的特性；可覆写 `Configuration`、`ConfigureServices`、`ConfigureApp`，以及 `EnsureTransaction`（Provider 的直接写入入口）与 `NamedConnection`（套件可用的命名连接，配置里要声明它），不覆写这两个的用例跳过 | 读己之写与提交可见性、回滚不留痕迹、标识重复（含两个工作单元并发新增同一标识，只有一个成功）、审计字段与并发戳、更新沿用存储的创建时间、租户与删除标记、软删除与物理删除、租户必填与隔离、过滤排序分页与计数（含可空值类型与 null 的相等 / 不等按 `IS NULL` 语义，判空与取反嵌套在与 / 或条件里照常过滤；规约里字符串的 `Contains` / `StartsWith` / `EndsWith` 按字面匹配，实参里的 `%`、`_`、`[`、反斜杠、`!` 与单引号不是通配符或转义符，不断言大小写；三个方法与闭包变量、常量、列三种实参在 ASCII 字母的大小写上一致，全都区分或全都不区分；一次最多取 1000 条，更大的取出条数以 `PERSISTENCE_VALIDATION_005` 拒绝，调用里是校验结果、已做的写入回滚）、失败结果与异常回滚、Transaction 阶段之外使用仓储抛错、只读操作拒绝全部写入（单条与批量，忽略拒绝的调用仍以 `PERSISTENCE_UNEXPECTED_001` 结束）、只读操作在新作用域里调用写操作照常提交、同一作用域的嵌套调用以 `PERSISTENCE_VALIDATION_002` 结束、`IUnitOfWork.HasTransaction`（开始与读取后为假，第一次写入后为真，提交与回滚后为假，只读工作单元恒为假；直接写入入口开启事务；绑定命名连接的工作单元只报告那个连接，与业务调用各报各的；ADR-0109）、标识生成器 |
| `IdGeneratorConformance<TKey>` | Preview | `CreateGenerator(TimeProvider clock)` 返回 `IIdGenerator<TKey>`（`TKey : IComparable<TKey>`） | 顺序生成与 8 线程并发生成不重复（真实时钟）、时钟前进后标识更大（`ConformanceClock`） |

`Fixtures` 命名空间里的 `ConformanceOrder`、`ConformanceNote`、`ConformanceItem` 等是套件自带的实体，`ConformanceFeature` 登记它们；EF Core、SqlSugar 这类需要映射的 Provider 在 `ConfigureServices` / `ConfigureApp` 里为这些实体建模。`ConformanceLease` 只在给出 `NamedConnection` 时登记到那个连接，EF Core 的 `DbContext` 模型要包含它。`RunAsync`、`FindAsync`、`ListAsync`、`CountAsync` 等静态辅助方法供派生类补充用例。
只读与嵌套的用例经 `ConformanceFeature` 登记的两个操作（只读的 `conformance-scripts.get`、可写的 `conformance-scripts.run`）走真实的操作契约。

`DataSourceConformance` 是运行时数据源的套件（ADR-0078），每个 Provider 也都继承它：实现 `CreateDefinition(name)`（连得上，连接串末尾加分号仍连得上）与 `CreateUnreachableDefinition(name)`（连不上、带口令）。用例验证测试连接成功且不登记、同定义复用 / 定义变了替换 / 批量移除逐项报告、连不上返回 `PERSISTENCE_UNAVAILABLE_006` 且错误里没有连接串与口令、每个数据源一条健康项；不支持的 Provider（EF Core、内存实现）两个方法都返回 `null`，用例断言取用与测试连接返回 `PERSISTENCE_UNAVAILABLE_008`。SqlSugar 在 SQLite 与 PostgreSQL（`XIHAN_TEST_POSTGRES`）上都跑。

`SchemaConformance` 是库结构读取与实体目录的套件（ADR-0078），每个 Provider 都继承它：设 `ReadsSchema`、实现 `CreateDefinition(name)`（指向应用自己库的运行时数据源，不支持时 `null`）。宿主登记套件的三种实体（订单登记两次），用例验证实体目录每个实体一条、标识类型 / 协议 / 主键列对、领域事件不列；读结构的 Provider 列出三张表、关键字与分页，表列表的分页越界以结果返回（`Skip` = -1、`Take` = 0 → `PERSISTENCE_VALIDATION_007`，`Take` = 1001 → `PERSISTENCE_VALIDATION_005`，经运行时数据源时不登记）而边界值照常，读到不可空的主键列，不存在的表该项 `PERSISTENCE_NOTFOUND_003`，超过 100 张整批 `PERSISTENCE_VALIDATION_005`，空集合空列表，经运行时数据源读到的结果与缺省连接一致；不读结构的 Provider 断言 `PERSISTENCE_UNAVAILABLE_008`。

`IdGeneratorConformance<TKey>` 是标识生成器自己的套件：应用自定义的 `IIdGenerator<TKey>` 也应继承它。


```csharp
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Conformance;
using XiHan.Framework.Persistence.InMemory;

public sealed class InMemoryRepositoryConformanceTests : RepositoryConformance
{
    protected override IFeature CreateFeature() => new InMemoryPersistenceFeature();
}
```

`Persistence.InMemory`、`Persistence.EntityFrameworkCore`（SQLite）、`Persistence.SqlSugar`（SQLite，设置了 `XIHAN_TEST_POSTGRES` 时另跑 PostgreSQL）通过仓储套件（内存实现没有直接写入入口、不支持命名连接，那两例跳过；SqlSugar 与 EF Core 在 SQLite 上把命名连接放在另一个库文件，SqlSugar 在 PostgreSQL 上指向同一个临时库）；`Persistence.SqlSugar` 的库结构套件另在 PostgreSQL、MySQL、SQL Server、Oracle 上跑（`XIHAN_TEST_POSTGRES`、`XIHAN_TEST_MYSQL`、`XIHAN_TEST_SQLSERVER`、`XIHAN_TEST_ORACLE`）；`GuidV7IdGenerator`（`Guid`）与 `SnowflakeIdGenerator`（`long`）通过标识生成器套件。

## Plugins

包 ID `XiHan.Framework.Plugins.Conformance`（RFC-0017、RFC-0018）：`xihan.plugin.v1` 插件的黑盒套件，以插件的构建产物目录为输入，任何语言写的插件都用它验证。依赖 `Plugins.Protocol`、`Plugins.OutOfProcess`（进程启动）、`Plugins.Wasm`（模块校验与实例）与 `Wasmtime`。

| 基类 | 稳定性 | 实现 | 可覆写 |
| --- | --- | --- | --- |
| `PluginProtocolConformance` | Preview | `PluginDirectory`：含 `xihan.manifest.json` 与 `entry` 指向的可执行文件 | `Timeout`（每次协议调用，缺省 15 秒）、`ConfigurationJson`（缺省 `{}`）、`SampleRequests`（没有样例的操作以 `{}` 调用）、`ExcludedOperations`（故意崩溃或依赖宿主回调的操作） |
| `WasmPluginConformance` | Experimental | `PluginDirectory`：含清单与模块文件 | `ConfigurationJson`、`SampleRequests`、`ExcludedOperations`、`TrapOperation`（故意陷阱的操作，给出时才跑陷阱后重建用例） |

进程外用例（以宿主身份启动进程）：握手行合法、协商出宿主支持的版本、原语层版本相同、清单与目录里的文件一致、操作与订阅在清单声明范围内；没有共同协议版本时以 `FailedPrecondition` 拒绝；`Configure` 与 `Start` 成功，`Health` 与 `WatchHealth` 报可服务状态且健康项都已声明，`Stop` 后进程在宽限内退出；未声明的操作以 gRPC `Unimplemented` 结束；每个声明的一元操作都返回带 `status` 的应答（成功时载荷是 JSON，失败时错误码合法）；向声明的订阅投递消息得到确认；stdin 关闭后进程自行退出。
进程外用例还要求清单不写 `contributes.pipeline`（进程外插件不能贡献中间件，宿主在发现时拒绝）。Wasm 套件会故意触发陷阱，Windows 上影子栈强制时整个进程会 fail-fast，所以套件先以 `PLUGINS_UNAVAILABLE_003` 拒绝；陷阱之后从同一个模块重建实例，重走握手的声明与首次相同（`wasm.md` §5）。


Wasm 用例（在进程内实例化模块）：模块是 reactor、导出与导入表符合 ABI v1、`xihan_abi_version` 返回 1；握手协商出版本 1、清单一致、声明在范围内、没有流式操作，声明了订阅 / 中间件就导出了对应函数；`xihan_configure` / `xihan_start` 成功、`xihan_health` 报可服务状态、`xihan_stop` 成功；未声明的操作以 `KERNEL_NOTFOUND_001` 的失败状态应答；每个声明的操作都返回带 `status` 的应答；订阅投递得到确认；每个贡献的中间件在 before 阶段返回合法动作（短路时带失败结果），在 after 阶段也应答；`TrapOperation` 陷阱之后，从同一个模块重建的实例重走握手、声明与首次相同，配置、启动后报可服务状态并照常应答。

```csharp
using XiHan.Framework.Plugins.Conformance;

public sealed class EchoWasmPluginConformanceTests : WasmPluginConformance
{
    protected override string PluginDirectory => Path.Combine(AppContext.BaseDirectory, "plugins", "echo-wasm");

    // 故意陷阱与忙等的操作不调用
    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal) { "echoes.trap", "echoes.spin" };

    // 陷阱后重建用例用它触发陷阱
    protected override string? TrapOperation => "echoes.trap";
}
```

两套用例都不提供宿主回调服务：Wasm 套件提供的宿主导入只有 `xihan.log`，`xihan.invoke` / `xihan.publish` 返回不可用状态；回调的令牌与权限、崩溃重启由宿主实现的测试覆盖。跑 Wasm 套件的测试项目在 Windows 上要设 `<CETCompat>false</CETCompat>`，否则套件先以 `PLUGINS_UNAVAILABLE_003` 拒绝。.NET 之外的插件也可以用规范仓库的 Go 运行器 `xihan-conformance`，用例相同（[测试与一致性套件](/testing/conformance)）。`samples/EchoPlugin`、`samples/EchoGoPlugin` 通过进程外用例，`samples/EchoWasmPlugin` 通过 Wasm 用例。

## Scheduling

包 ID `XiHan.Framework.Scheduling.Conformance`（RFC-0013，Preview）。

| 基类 | 实现 | 覆盖 |
| --- | --- | --- |
| `JobSchedulerConformance` | `StartAsync(configure, configuration)` 启动装配了被测调度器的宿主并返回 `JobSchedulerHost(scheduler, poll, host)`，`poll` 轮询一次并等本周期起的运行结束（`JobSchedulerEngine.PollOnceAsync`）；轮询周期设得很长（如 3600 秒），后台循环不动作；回调 `configure` 里套件登记 `RecordingJob`、`RecordingJobLog` 与本用例的时钟，要在 Provider 装配之前应用；可覆写 `WaitTimeout`（缺省 10 秒，只防挂死） | 手动触发经管道运行并记录结果、未登记的作业、间隔计划反复运行、不允许并发时的冲突、作业异常翻译为失败、状态报告、失败重试（成功即止、次数用尽即止、每实例作业在本实例重试）、执行时限超时与取消不重试、重试策略与时限的运行时修改和恢复、运行历史的分页越界是校验错误（`SCHEDULING_VALIDATION_002`，计数不看分页） |
| `JobStoreConformance` | `CreateAsync()` 返回 `(IJobStore Store, IAsyncDisposable Host)` | 作业写入 / 查找 / 列表 / 替换 / 级联删除与快照语义、触发器全字段往返、到期抢占跨节点原子且按到期时刻与优先级排序、独占运行与阻塞 / 完成解除 / 查询 / 取消 / 清理、节点心跳与失联恢复 |
| `LeaderElectionConformance` | `CreateElection(clock)` 返回 `ILeaderElection`，选举与它产生的租约按传入的时钟判定到期（用例推进时钟，不等真实时间）；可覆写 `LeaseTimeToLive`（缺省 400 毫秒，整数秒粒度的 Provider 如 Kubernetes Lease 改为 1 秒）；`StartNodeAsync(clock, probe)` 返回 `(ILeaderElection Competitor, IAsyncDisposable Host)`：起一个调度引擎用被测选举的节点（登记套件的时钟、轮询周期设得很长、`UseFeature(probe)` 装配套件给的探针特性），`Competitor` 是与节点争同一租约、节点启动到停止期间都在服务的另一个持有者（数据库实现是同一个库上的另一个节点，进程内实现是节点自己的选举），`Host` 释放这一切（节点可能已被用例停止） | 获取、被占用时拒绝、释放后可再获取、过期后可被他人获取且原持有者续约失败、续约延长到期；应用生命周期（ADR-0094）：启动中的周期不拿租约，进入服务后竞争者放手即由节点拿到，排空中的周期释放、竞争者在租约到期之前就拿到 |

调度器用例由用例驱动时间（ADR-0074）：套件登记一个只由用例推进的时钟（`FakeTimeProvider`），引擎的到期判定、领导租约、作业的停留与执行时限都按它计时；用例推进时钟、调用 `JobSchedulerHost.PollAsync` 轮询一次再断言，不靠后台循环、也不按墙钟等待，结果与机器快慢和负载无关。等作业开始用 `RecordingJobLog.WaitAsync`，由作业的记录唤醒。`RecordingJob` 认运行数据里的 `failBeforeAttempt` 与 `delayMilliseconds`（按注入的时钟停留），用来构造重试与超时场景。`Scheduling.InMemory`、`Scheduling.Database` 通过三套用例，`Scheduling.Kubernetes` 通过领导选举用例（以内存 API Server 桩验证，应用生命周期用例的节点装配内存调度与 Kubernetes 选举）。领导选举的应用生命周期用例要求 `Release` 在应用不在服务状态时也真正生效：经管道释放的实现（排空与停止时管道拒绝调用）过不了。作业要等时钟推进才结束时（停留、执行时限），先发起轮询或触发，等作业开始，再推进时钟，最后等调用完成。

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
        var settings = new List<KeyValuePair<string, string?>> { new("XiHan:Scheduling:PollIntervalSeconds", "3600") };
        settings.AddRange(configuration ?? []);
        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySchedulingFeature>(), settings, configure);
        var engine = host.Services.GetRequiredService<JobSchedulerEngine>();
        return new JobSchedulerHost(engine, engine.PollOnceAsync, host);
    }
}
```

## Security

包 ID `XiHan.Framework.Security.Conformance`（RFC-0008，Stable）。基类 `PermissionGrantStoreConformance`，实现 `CreateStoreAsync(grants)` 返回装好给定授予的 `IPermissionGrantStore`。用例覆盖用户与角色授予、全局授予对任何租户生效、租户专属授予只对该租户生效、未知主体为空、主体与权限区分大小写、通配授予 `*` 原样返回、空存储，以及存储不可达时返回 `SECURITY_UNAVAILABLE_001` 而不是空集合（访问外部存储的 Provider 覆写 `CreateUnavailableStoreAsync`）。`Security.InMemory` 与 `Security.Database` 通过本套件。

`GetGrantsAsync` 返回 `Result<IReadOnlyCollection<string>>`：没有授予是成功的空集合，存储故障是失败。不覆写 `CreateUnavailableStoreAsync` 时存储不可达的用例跳过，只有进程内、读取不会失败的存储可以不覆写。

在完整的应用宿主上给不可达的存储时，不要让整个宿主的库不可达（删库、断开默认连接）：宿主里别的子系统的后台循环——工作流轮询与它的领导权、发件箱、调度、翻译加载——在服务状态下会如实报存储不可达（如 `WORKFLOW_UNAVAILABLE_001`），那是真实的故障信号。用只装被测特性的最小宿主（框架 `Security.Database` 的测试就是这样），或只让被测存储用的连接不可达。

## Workflow

包 ID `XiHan.Framework.Workflow.Conformance`（RFC-0021，Preview），命名空间 `.Storage` 与 `.Engine`。

| 基类 | 实现 | 覆盖 |
| --- | --- | --- |
| `WorkflowStoreConformance` | `StartAsync()` 返回 `WorkflowStoreHost(services, host)`；存储是作用域服务，用例在 `InPartitionAsync(tenantId, …)` 开的调用里使用它们 | 定义按 ID / 名字 / 版本 / 最新已发布查找、列表排序与过滤、批量写与删、级联、快照语义、书签按到期 / 种类与键 / 信号查询、可领取书签按到期时刻限量查询、待办查询的过滤 / 升降序 / 分页 / 计数且随实例状态进出、定义查询的过滤 / 分页 / 计数与关键字按字面且不跨字段匹配、实例列表的关联 ID 集合 / 只取顶层 / 关键字（标题或关联 ID）、定义查询的创建时间区间与每个排序字段的升降序、实例列表每个排序字段的升降序（次序键补成全序、标题按前 256 个字符、可空时间只比较有值的行，ADR-0102；状态按生命周期、状态变了位置随之变化，ADR-0103）、多字段排序（主次字段的组合、升降混合、含状态、分页稳定、空列表等于缺省，ADR-0104）、待办的关键字（任务标题、实例标题、定义名或关联 ID）、按分区的名字空间、分区互不可见、写入别的分区的记录即抛出、实例锁的互斥与超时（锁等待上限配置在 1 秒以内） |
| `WorkflowEngineConformance` | `StartAsync(configure, configuration)` 返回 `WorkflowEngineHost(services, poll, host)`，`poll` 轮询一次到期书签；可覆写 `WaitTimeout`（缺省 10 秒） | 顺序流与事件、决策与默认出边、并行与汇合、用户任务 Any / All / Sequential、转办与加签、延时与超时、信号、子流程与 ForEach、重试与继续、挂起 / 恢复 / 取消 / 终止与补偿、定义生命周期与结果处理器、死循环保护、分区隔离（平台与租户对等：任务、信号、定义的名字空间与实例的控制和查询）、并发的拒绝重复启动只建一个实例（套件给实例存储与锁套装饰器，Provider 要以具体类型登记它们、再以 `TryAdd` 把接口转给具体类型）、业务回滚丢弃引擎写入、办理任务随业务提交或回滚、实例锁持有到调用结束、只读调用在执行活动之前拒绝写入、轮询覆盖平台与每个启用的租户、实例查询的分页与清理的条数越界是校验错误（`WORKFLOW_VALIDATION_002`）、目录的定义查询（摘要的一页与总数、分页越界）、实例查询只取顶层与一组关联 ID（子流程继承发起人与关联 ID、集合越界与空集合）、按实例列出全部等待点（全部书签种类、分区隔离）、目录与实例查询的排序经引擎下推且排序项越界——字段不在白名单、重复、超过上限——是校验错误（ADR-0102、ADR-0104）、按状态排序是生命周期的先后且经目录与引擎改了状态之后位置随之变化（ADR-0103）、故障存消息键与参数并按调用方的区域本地化——发起即故障、完成任务之后活动失败、子流程故障（子实例的故障是内层原因），经存储与事件往返之后仍在——以及返回的校验错误只带消息键与参数（ADR-0105）、设计期诊断与活动描述符、`lastError`、表达式的运行期错误、子流程被取消与状态不允许操作都按调用方的区域渲染（ADR-0106）、宿主里别的发布端的消息照常确认且不当工作流事件解析（ADR-0096 修订）；不加入调用方事务的 Provider 覆写 `JoinsCallerTransaction => false`（只读调用的用例两种 Provider 同一断言，ADR-0099） |
| `WorkflowTaskConformance` | 同 `WorkflowEngineConformance` 的 `StartAsync` | 会签阈值（按人数或百分比）、认领与释放、退回与历史、催办事件、条件查询的分页结果与总数（越界是校验错误、挂起的实例的任务不出现）、任务元数据（优先级、到期、表单键）与周期提醒、外部任务的领取 / 完成 / 失败 / 锁、按创建先后限量领取与条数上限 |

`ConformanceLog`（兼消息代理）、`RecordActivity`、`RecordingOutcomeHandler` 是套件登记进容器的记录器、活动与结果处理器。自写宿主跑套件时，`WorkflowEngineHost` / `WorkflowStoreHost` 的释放句柄要先停宿主（应用排空：数据库工作流的轮询器释放领导权、后台循环退出，ADR-0087）再删库或删库文件；先删库再停，还在服务的宿主会如实报 `WORKFLOW_UNAVAILABLE_001`。`Workflow.InMemory` 与 `Workflow.Database` 通过三套用例，引擎与任务两套在装了发件箱的宿主上也通过，引擎套件在同时装了调度的宿主上也通过（调度不记告警）。

租户相关的用例经 `ITenantStore` 取租户：套件以 `TryAdd` 登记一份固定的租户目录（`t1`、`t2`、`t-other` 启用，`t-off` 停用），夹具不必装配多租户。`WorkflowEngineHost` 的 `AsTenantAsync` 以租户开一次调用（可选回滚），`PublishManyAsync` 在一个分区里批量发布。延时启动、外部任务的锁到期与重试时刻由用例推进时钟判定：`configure` 里登记手动推进的 `TimeProvider`，Provider 的引擎、存储与轮询器须从容器取时钟，不读墙钟。只读调用的用例两种 Provider 同一断言：写入在执行活动之前以 `PERSISTENCE_UNEXPECTED_001` 拒绝，调用本身成功（ADR-0099）。

事件在代理一侧记录（ADR-0096 第六节）：`Register` 把 `ConformanceLog` 登记成 `IMessageBroker`——消息 Provider 以 `TryAdd` 登记代理与直接发布者（框架的内存、RabbitMQ、Kafka 都是），先登记的记录器不被顶替；没装消息 Provider 的宿主由套件补上 `DirectPublisher`。宿主装了发件箱（`OutboxFeature`）也照常：事件随事务写进发件箱，夹具的 `InScopeAsync`、`AsTenantAsync` 在操作结束之后、`PollAsync` 在轮询之后调 `WorkflowEngineHost.DrainOutboxAsync`，把已提交的事件立即投递给记录器，回滚的不送达；同一信封按 ID 只记一次。所以应用可以在自己装着发件箱的真实宿主上跑这两个套件，不用另起不装发件箱的宿主。记录器只把工作流事件（信封来源是 `WorkflowTopics.Source`，即 `/workflow`）解析进 `Events`；宿主里别的发布端（调度的作业事件、应用自己的消息）送来的照常确认成功、不解析，主题按送达顺序记进 `OtherTopics`（ADR-0096 修订）——调度等发布端不会因套件记告警，发件箱的投递轮次也不会因别的主题失败。记录器只记录、不消费：套件运行时宿主别的消息订阅仍收不到消息。在夹具之外操作引擎后要读事件，先调 `DrainOutboxAsync`。

## 相关

- [测试与一致性套件](/testing/basics)、[扩展与二次开发](/extending/provider)
- [Testing 测试基座](./testing)
