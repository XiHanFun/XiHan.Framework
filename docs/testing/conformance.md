# 一致性套件与门禁

这一页讲每个子系统的 `<领域>.Conformance` 包怎么用（继承抽象套件类、提供实现），插件协议的两份黑盒套件 `Plugins.Conformance` 与 `xihan-conformance`，以及框架仓库的架构测试与 CI 门禁。读完你能让自己写的 Provider 或插件跑完框架同一套用例，并知道向框架仓库提交代码要过哪些关。前置知识是[单元与集成测试](/testing/basics)。

## 为什么需要

「换 Provider 只改包引用与配置」这句承诺要有人兜底：同一个 `ICacheStore` 契约，内存实现、Redis 实现和 Go 插件实现对过期、批量、并发写入的语义必须一致，否则应用在开发机上跑得好、上线换了 Provider 就出问题。框架为每个子系统抽象配一个一致性套件：一组 xUnit 抽象测试类，Provider 的测试项目继承基类、实现工厂方法，套件里的全部用例随之执行。框架自己的每个默认实现与 Provider 都在仓库里跑同一套用例，自己写的 Provider 也应当继承它。插件协议同理，任何语言写的插件都用同一份黑盒套件验证。

## 套件一览

每个套件包是一组 xUnit v3 抽象测试类，只引用 xUnit 的核心与断言包，不带运行器；测试项目要自己引用 `xunit.v3`。各子系统的套件加插件协议的 `Plugins.Conformance`，各自的基类、要实现的工厂方法、可覆写的成员与用例覆盖面按子系统列在 [Conformance 包参考](/packages/conformance)。入口有三种形状：

- 直接给出被测对象：`CacheStoreConformance.CreateStore()`、`PermissionGrantStoreConformance.CreateStoreAsync(grants)`、`MessageLocalizerConformance.CreateLocalizer(catalogs, options)` 这类工厂方法，或 `CreateFixtureAsync()` 返回带被测对象与写入入口的夹具（审计、配置）。
- 给出特性：持久化、消息的 `CreateFeature()`，调度、工作流、AI 的 `StartAsync(...)` 返回套件的宿主句柄。这类套件自己用 `TestHost` 装配并走完整管道，验证的是 Provider 在真实管道里的行为，不只是接口方法。
- 给出插件的构建产物目录：`Plugins.Conformance` 的 `PluginDirectory`，见下文。

套件之外，规范仓库还有数据驱动的一致性向量（`spec/conformance/vectors/`，见[规范与多语言](/appendix/spec#一致性测试)），收文法、换算与判定表这类输入输出确定的规则。框架的 .NET 测试经仓库里不发包的 `framework/test/XiHan.Framework.Testing.Workspace` 的 `SpecVectors` 严格回放：文件里有、模型里没有的成员，缺了的必需成员，同一对象里重名的成员，落到不可空成员上的 `null`，都让读取失败，向量改了写法时回放随之失败，不会漏掉字段还照样通过。写法见下文[回放规范向量](#回放规范向量)。

## 最小示例

新建测试项目，引用被测 Provider、对应的 `<领域>.Conformance` 包与 `xunit.v3`，写一个 `public sealed class` 继承基类。`Caching.InMemory` 的测试项目就这样跑完整套缓存用例：

```csharp
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.Conformance;
using XiHan.Framework.Caching.InMemory;

public sealed class InMemoryCacheStoreConformanceTests : CacheStoreConformance
{
    protected override ICacheStore CreateStore() => new InMemoryCacheStore(new InMemoryCachingOptions(), TimeProvider.System);
}
```

以特性为入口的套件实现 `CreateFeature()`；内存代理每个宿主一个、两个宿主不共用，所以把多实例用例关掉：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Testing;

public sealed class InMemoryMessageBrokerConformanceTests : MessageBrokerConformance
{
    protected override bool SupportsMultipleInstances => false;

    protected override IFeature CreateFeature() => new InMemoryMessagingFeature();

    // 读出一个主题此刻的死信，不取走：套件靠它核对哪些消息进了死信
    protected override ValueTask<IReadOnlyList<MessageEnvelope>> ReadDeadLettersAsync(TestHost host, string topic, CancellationToken cancellationToken)
    {
        var broker = host.Services.GetRequiredService<InMemoryMessageBroker>();
        return ValueTask.FromResult<IReadOnlyList<MessageEnvelope>>([.. broker.DeadLetters(topic).Select(static delivery => delivery.Envelope)]);
    }
}
```

代理的套件除了特性还要实现 `ReadDeadLettersAsync`：按自己的介质（死信列表、死信队列、死信主题或死信流）读出死信，可以反复调用，还没有死信时返回空列表。套件里有两条用例在处理器正在处理时停止消费：一条在第一次尝试上，一条在最后一次尝试上，被打断的投递都不能进死信，重新开始消费后要再处理一次。尝试号随消息走（消息头、内存里的投递对象）的代理自然通过；靠代理一侧的计数（投递次数、重投次数）得出尝试号的，要在放弃这次投递时把计数退回一次，否则最后一次尝试上被打断的消息回来时已经超过上限、直接进死信。

运行 `dotnet test --project <测试项目>`，套件的每个 `[Fact]` 都作为这个类的用例出现：缓存套件会列出读写往返、键区分大小写、批量保持顺序、不存在才写只有一个赢家、滑动过期、分布式锁互斥等十几条，全部通过才算与默认实现语义一致。某条失败时，失败名就是语义差异所在，按它修 Provider，不要在派生类里跳过。

## 依赖真实服务的套件

依赖真实服务的 Provider（Redis、RabbitMQ、Kafka、PostgreSQL、SQL Server、MySQL、Oracle、S3、Nacos、Qdrant 等）在框架仓库里只在设置了对应环境变量时运行，没有就用 `Assert.SkipWhen` 跳过：`XIHAN_TEST_REDIS`、`XIHAN_TEST_RABBITMQ`、`XIHAN_TEST_KAFKA`、`XIHAN_TEST_POSTGRES`、`XIHAN_TEST_SQLSERVER`、`XIHAN_TEST_MYSQL`、`XIHAN_TEST_ORACLE`、`XIHAN_TEST_S3`、`XIHAN_TEST_NACOS`、`XIHAN_TEST_QDRANT`。CI 以服务容器提供这些服务。自己的 Provider 照这个做法：工厂方法里读环境变量，没有就跳过，这样本机没有服务也能跑其余用例，CI 再接真实服务。给不可达的存储做用例时只让被测存储的连接不可达，不要让整个宿主的库不可达：宿主里别的子系统的后台循环会如实报不可用，那是真实的故障信号而不是测试噪音。

## Plugins.Conformance 与 xihan-conformance

插件协议有两份黑盒套件，任何语言写的插件都用它们验证：.NET 的 `XiHan.Framework.Plugins.Conformance`，和规范仓库 [framework-spec](https://github.com/XiHanFun/framework-spec) 里语言中立的 Go 运行器 `xihan-conformance`。两者都以宿主身份启动插件、按规范核对，用例相同。

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

public sealed class EchoWasmPluginConformanceTests : WasmPluginConformance
{
    protected override string PluginDirectory => Path.Combine(AppContext.BaseDirectory, "plugins", "echo-wasm");

    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal) { "echoes.trap", "echoes.spin" };

    // 陷阱后重建用例用它触发陷阱
    protected override string? TrapOperation => "echoes.trap";
}
```

可覆写的成员：`Timeout`（每次协议调用，缺省 15 秒，进程外）、`VerifiesSessionToken`（进程外，缺省为真；只在插件 SDK 还没有发布校验会话令牌的版本时临时关掉）、`ConfigurationJson`（`Configure` 下发的配置节，缺省 `{}`）、`SampleRequests`（没有样例的操作以 `{}` 调用）、`ExcludedOperations`（故意崩溃或依赖宿主回调的操作）、`TrapOperation`（Wasm，给出时才跑陷阱后重建用例）。跑 Wasm 套件的测试项目在 Windows 上要设 `<CETCompat>false</CETCompat>`：套件会故意触发陷阱，影子栈强制时整个测试进程 fail-fast（[Wasm 插件](/plugins/wasm)）。

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

用例：`handshake`（握手行、版本协商、原语层版本、清单一致、声明在清单范围内、目录至少一种区域；`primitives` 取自清单、清单不以 BOM 开头、只声明一元操作、订阅方式总是写出）、`refusal`（没有共同版本时以 `FailedPrecondition` 拒绝）、`session`（不带、带错、带两个会话令牌值都以 `Unauthenticated` 拒绝）、`lifecycle`（`Configure` / `Start` / `Health` / `WatchHealth` / `Stop`，`Start` 之后报 `Ready` 或 `Degraded`，进程在宽限内退出）、`order`（调用次序不对以 `FailedPrecondition` 拒绝，`Start` 之前的 `Health` 报本地状态）、`unknown-operation`（`Unimplemented`）、`declared-operations`（每个声明的操作都返回 `status`）、`unavailable`（`Start` 之前的调用与投递报 `plugins.process.unavailable`）、`event`（订阅投递得到确认，没有订阅时跳过）、`unsubscribed`（找不到订阅报 `plugins.event.unsubscribed`，未知方式与没有信封为 `InvalidArgument`）、`stream`（`InvokeStream` 按第一帧以 `InvalidArgument` 或 `Unimplemented` 结束）、`configuration`（配置节不是对象时进入 `Failed`，`Stop` 后以退出码 0 退出）、`stop-early`（握手后直接 `Stop`，以退出码 0 退出）、`stdin`（关闭 stdin 后进程自行退出）。.NET 套件的用例说明里写着它对应的运行器用例名。每个用例单独启动一次插件进程，`XIHAN_PLUGIN_HOST_ADDRESS` 与 .NET 宿主一样是两项的列表（首选端点加回环 TCP，都没人监听）。退出码：`0` 全部通过（含跳过），`1` 有用例失败，`2` 参数或目录错误。`xihan-conformance vectors` 自检规范仓库 `conformance/vectors/` 下的数据向量（错误码、Baggage 键、握手行、版本协商等的合法与非法样例），各语言 SDK 的单元测试也直接读这些文件。

两份套件都不提供宿主回调服务，`Host.Invoke` / `Host.Publish` 的令牌与权限、崩溃重启由宿主实现的测试覆盖。

## 回放规范向量

这一节面向向框架仓库提交代码的人。`framework/test/XiHan.Framework.Testing.Workspace` 是给测试项目引用的类库：不打包、不发布，没有公开面文件，也不当测试运行；它只在框架仓库的检出里有用，框架仓库之外的测试不引用它。测试项目加上 `<ProjectReference Include="../XiHan.Framework.Testing.Workspace/XiHan.Framework.Testing.Workspace.csproj" />` 与 `using XiHan.Framework.Testing.Workspace;`。

| 类型 | 说明 |
| --- | --- |
| `FrameworkWorkspace` | `Root`：从测试的输出目录逐级向上第一个含 `XiHan.Framework.slnx` 的目录（仓库里的 `framework/`），第一次读取时找、之后缓存，找不到抛 `InvalidOperationException`。读规范、示例与别的项目的构建产物都以它为起点 |
| `SpecVectors` | `Directory` 是 `<工作区>/spec/conformance/vectors`；`PathOf(area, fileName)` 只拼路径，给要按 `JsonDocument` 自己读的测试；`Read<T>(area, fileName)` 按反射严格读，`Read<T>(area, fileName, resolver)` 按源生成的上下文严格读 |

下面回放缓存键的向量（`caching/cache-keys.json`）：

```csharp
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Testing.Workspace;
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

读法与报错：

- 读法一律严格：成员名按 Web 缺省（camelCase、不区分大小写），文件里有、模型里没有的成员失败，同一对象里重名的成员失败，没有缺省值的构造参数与 `required` 成员缺了失败，只去掉顶层的 `$comment`（别处的 `$comment` 是向量的内容）。
- `null` 只能落到声明为可空的成员上（属性、字段与构造参数，反射与源生成两条路都按可空性注解核对），落到不可空的成员上同样失败；集合的元素与字典的值不核对（System.Text.Json 不看泛型实参的可空性）。向量里会出现 `null` 的成员在模型里写成可空。
- 同一个文件由几个测试类分别回放时，各自把别的类回放的成员写成 `JsonElement`，可以不出现的成员给缺省值。
- 报错都带文件路径：文件不存在抛 `FileNotFoundException`（多半是没有检出规范子模块，`git submodule update --init framework/spec`），内容为空、只有 `null` 或顶层不是对象抛 `InvalidOperationException`，与模型对不上抛 `JsonException`（带成员与位置）；目录名与文件名必须是单独一段，空白、`.`、`..`、含 `/` 或 `\` 抛 `ArgumentException`。按反射读的重载标了 `RequiresUnreferencedCode` / `RequiresDynamicCode`，AOT 的测试项目改用带 `resolver` 的重载。

架构测试 `SpecVectorCoverageTests` 核对认领的档里每个向量都有回放或登记为缺口。它认的回放是 `SpecVectors.Read<…>("<目录>", "<文件>")` 与 `SpecVectors.PathOf("<目录>", "<文件>")` 的调用，前两个参数都要是字符串字面量（可以跨行），按（目录, 文件）成对识别：参数是常量或变量的调用、文件名出现在注释或数组里都不算回放；调用处的 `<目录>/<文件>` 不存在即失败（`VectorCalls_NameAFileOfThatDirectory`），报错列出别的目录里的同名文件，多半是目录写错。

## 架构测试与门禁

这一节面向向框架仓库提交扩展的人（[贡献指南](/appendix/contributing)）：`framework/` 下的每个正式包都要通过下列门禁，CI 全部是必过项，不放宽、不绕过、不加 `NoWarn`。

| 门禁 | 位置 | 检查什么 |
| --- | --- | --- |
| 编译 | `framework/Directory.Build.props` | `TreatWarningsAsErrors`、Nullable、`AnalysisLevel=latest-recommended`、`EnforceCodeStyleInBuild`（命名 IDE1006 与未使用 using IDE0005 在构建期即错误）、NuGet 高危 / 严重漏洞中断构建 |
| 公开面与包 | `framework/src/Directory.Build.props` | `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` 必须与公开面一致、包验证、AOT 分析（`IsAotCompatible` 缺省为真） |
| 分析器 | `Kernel.Analyzers` | XH0001–XH0008：`[ApiLevel]`、`XiHan` 前缀、禁用词、Internal API、Ring 0/1 零反射、级别依赖、Stable 不用可选参数（[Kernel.Analyzers](/packages/kernel-analyzers)） |
| 架构测试 | `framework/test/XiHan.Framework.Architecture.Tests` | 见下表 |
| 冒烟 | CI | `dotnet pack` 触发包验证；`samples/SimpleApp` 以 Native AOT 发布 |

架构测试扫描源码目录与项目文件，按包 ID 推出环层再判定：

| 测试 | 规则 |
| --- | --- |
| `ProjectReferences_PointToLowerRingsOrSameRingAbstractions` | 依赖方向：只能引用更低环层的包；同环只能引用对方的 `.Abstractions`；工具链与组合包例外 |
| `Primitives_HasNoPackageOrProjectReferences`、`KernelAndAbstractions_ReferenceOnlyMicrosoftExtensionsAbstractions` | 原语层零依赖；Ring 0/1 与抽象包只引用 `Microsoft.Extensions.*` 抽象包或白名单里的包 |
| `PrimitivesAndKernel_DoNotUseReflection` | 零反射：Ring 0/1 源码不使用反射 |
| `PublicSignatures_DoNotLeakThirdPartyTypes` | 公开签名只出现 BCL、`Microsoft.Extensions` 与框架自身的类型 |
| `PackageIds_FollowGrammar`、`Vocabulary_IsWellFormed` | 包 ID 文法：由术语表与规范的文法推出，见[能力包与中间件](/extending/feature) |
| `Namespaces_MatchFolders`、`Namespaces_EqualPackageIdWithAtMostOneSegment` | 命名空间等于包 ID，子命名空间最多一层且与目录同名 |
| `PublicTypes_DoNotCarryXiHanPrefix` | 公开类型名不带 `XiHan` 前缀 |
| `SourceFiles_DeclareOneTopLevelType_NamedLikeTheFile`、`SourceFiles_StartWithCopyrightHeader` | 一个文件一个顶层类型、文件名与类型名一致（泛型加 `OfT`）；版权头 |
| `XmlSummaries_UseTheThreeLineForm`、`XmlSummaries_AreOneConciseSentence` | `<summary>` 三行写法、一句话，细节放 `<remarks>` |
| `EverySourceProject_HasReadmeAndPublicApiFiles`、`EverySourceProject_IsReferencedByArchitectureTests` | 每个正式包都有 `README.md` 与两个 PublicAPI 文件，并加进架构测试项目的引用 |
| `EveryLocalizedPackage_HasOneFilePerLocale_WithConsistentKeys`、`LocaleFiles_AreEmbeddedWithTheConventionalLogicalName`、`MessageConstants_MatchTheEnglishLocaleFile`、`ErrorCallSites_UseMessageKeyConstants` | 本地化：10 种区域的语言文件齐全、占位符一致、按约定嵌入、消息键常量与 `en-US.json` 一一对应、错误调用点只用常量（[全球化和本地化](/common/localization)） |
| `ErrorCodes_AreRegisteredOnce` | 错误码全仓库唯一 |
| `DeclaredHealthItems_AreImplementedInTheSamePackage`、`PackagesRegisteringContributors_DeclareHealthItems` | 清单 `contributes.health` 的条目由同一个包实现，登记了 `IHealthContributor` 的包必须声明条目（插件宿主例外）；`samples/` 下的示例项目同样受检 |
| `SourceProjects_DoNotCallSingleItemMethodsInsideLoops` | 批量优先：不在循环里调用仓储、缓存存储、消息发布的单条方法，改用批量方法 |
| `DatabaseRecords_DeclareEveryStringColumnLength` | 数据库记录的字符串列都声明 `[StringLength(n)]` 或 `[LongText]` |
| `RecordsCarryingCredentials_MaskThemInToString` | 携带凭据的记录在 `ToString` 里遮蔽凭据 |
| `EveryVectorOfTheClaimedProfiles_IsReplayedOrAGap`、`VectorCalls_NameAFileOfThatDirectory`（`SpecVectorCoverageTests`） | 认领的档里每个规范向量都有回放或登记为缺口，回放的调用按（目录, 文件）成对识别、目录下要有那个文件，见上文[回放规范向量](#回放规范向量) |
| `TestSources_UseTheSharedWorkspaceAndTelemetryEntries`（`TestToolingTests`） | `framework/test` 下的源码不手写工作区查找与遥测监听：解决方案文件名 `XiHan.Framework.slnx` 只许出现在 `FrameworkWorkspace` 里，不许构造 `ActivityListener` 与 `MeterListener`（含目标类型的 `new()`），定位工作区、收 Span 与指标只用 `FrameworkWorkspace`、`SpanCapture` 与 `MetricCapture` |

## 常见问题

### 套件里有一条用例我的后端做不到，能跳过吗

先看基类有没有对应的虚成员：多实例、不可达存储、命名连接、直接写入入口这类差异都有开关，不覆写时相关用例自动跳过。没有开关的用例代表契约语义，做不到说明实现与契约不一致，要改实现或在应用里不声明这个能力。

### 我的应用不向框架仓库提交，架构门禁与我有关吗

包 ID 文法只约束 `XiHan.Framework.*`，但清单 ID、能力 ID、错误码与健康项的文法由内核在运行期校验，照样适用；本地化与批量优先的规则是框架默认实现的约定，自己的 Provider 遵守它们才能通过一致性套件。想让编译器执行同样的类型命名规则，启用 `Kernel.Analyzers`。

## 相关包与下一步

- 包参考：[Conformance](/packages/conformance)、[Testing](/packages/testing)、[Kernel.Analyzers](/packages/kernel-analyzers)、[Kernel.Generators](/packages/kernel-generators)
- 相关章节：[单元与集成测试](/testing/basics)、[进程外插件](/plugins/out-of-process)、[Wasm 插件](/plugins/wasm)、[贡献指南](/appendix/contributing)
- 规范：RFC-0017《进程外插件协议》、RFC-0018《Wasm 插件 ABI》；规范仓库 [framework-spec](https://github.com/XiHanFun/framework-spec)
- 下一步：[写一个 Provider](/extending/provider)把一致性套件用在自己的实现上
