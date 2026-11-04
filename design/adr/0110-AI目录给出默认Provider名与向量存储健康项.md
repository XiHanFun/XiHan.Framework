# ADR-0110 AI 目录给出默认 Provider 名、聊天与知识库没写名字时问目录，知识库向量存储 Provider 自带健康项

## 状态

已采纳（`spec/rfcs/0022-ai.md`、`spec/conventions/error-codes.md` 随之修订）。第二节按 ADR-0030 的连接类写法落实，不修订 ADR-0030。

## 日期

2026-11-04

本 ADR 记录第二十三批回灌里 AI 分区的两项决定（`ab-batch-backlog.md` 第 2、3 条，来由是 BasicApp N4（AI）调研报回的 F-N4-1、F-N4-4），每项一节。

## 一、`IAIProviderCatalog` 给出默认 Provider 名，聊天与知识库没写名字时只问目录（F-N4-1）

### 背景

`IAIChat.CompleteAsync` / `StreamAsync`、`KnowledgeIngestor`、`KnowledgeRetriever` 在请求没写 Provider 时，用 `AIOptions.ResolveDefaultProvider()` / `ResolveDefaultEmbeddingProvider()`（读 `XiHan:AI` 配置）定名字，再拿这个名字向目录 `Find` 取超时；`IAIProviderCatalog` 没有「默认名」成员。框架自带的 `AIProviderCatalog` 也从这份配置推默认名，两边一致，所以没有暴露问题。

BasicApp 6.0 的提供商落库（`DatabaseAIProviderCatalog`，替换框架以 `TryAdd` 登记的目录，Provider 表与默认标记都在库里、不在配置里）之后：

- 聊天：`ResolveChatClient(null)` 由落库目录按它自己的默认解析成功，随后 `Find(配置推出的名字 ?? "")` 查不到，返回 `AI_NOTFOUND_001`（`AI Provider '' 未配置`）。
- 知识库：配置推不出名字，`EmbedAsync` 拿到 `null` 直接报 `AI_NOTFOUND_001`。

BasicApp 只能每次调用都显式传 Provider 名。用 `ProviderDefaultTests` 复现：应用配置里没有 Provider、目录换成 Provider 表与默认名都不来自应用配置的目录，不写名字的聊天、摄入、检索在修复前全部是 `AI_NOTFOUND_001`。

### 备选

1. 目录加两个同步只读属性 `DefaultProvider` / `DefaultEmbeddingProvider`（`string?`，`null` 表示没有默认）（采纳）。
2. 目录加方法 `ResolveName(string? name)`：聊天与嵌入的默认可能不同（配置本来就有两个键），方法得再带一个「用途」参数（新枚举）；传了名字时是原样返回还是核对存在，语义也不清楚。
3. 异步成员 `ValueTask<string?> GetDefaultProviderAsync(CancellationToken)`：落库目录可以按需查库。但目录其余读取（`Providers`、`Find`、`Resolve*`）都是同步的，`ResolveChatClient(null)` 本身就要同步取默认；只把默认名做成异步，`Resolve*(null)` 与它就不再是同一个回答。BasicApp 调研已定落库目录持内存快照、写入后重载（F-N4-2，不改框架），默认名是快照的一部分。
4. 保留读配置，在目录查不到配置推出的名字时再退回目录的默认：两个来源、两种回答，是静默兜底。
5. 给接口成员加默认实现（返回 `null`）兼容已有实现：推测性兼容，违反仓库约定；接口在 `PublicAPI.Unshipped.txt` 里，直接改。

### 决策

- `IAIProviderCatalog` 新增 `string? DefaultProvider { get; }`、`string? DefaultEmbeddingProvider { get; }`：目录此刻的默认聊天 / 嵌入 Provider 名，`null` 表示没有默认。两者可以相同也可以不同；只支持一个默认的目录两者相同。名字不保证在 `Providers` 里（如配置写错），按它解析时照常报 `AI_NOTFOUND_001`（`ai.provider.not_found`，带名字）。
- `ResolveChatClient(null)` / `ResolveEmbeddingGenerator(null)` 按这两个成员解析；没有默认 → `AI_NOTFOUND_001`，新消息键 `ai.provider.default_missing` / `ai.provider.embedding_default_missing`（不带参数，10 种区域）。目录实现与框架的流水线同用这两个键。
- `AIChat` 在请求没写 Provider 时取 `_providers.DefaultProvider`，没有即上面的错误；名字取定一次，解析、超时与错误细节（`details.provider`）都用它。`AIChat` 不再依赖 `AIOptions`，构造去掉 `options` 参数。
- 知识库摄入与检索的嵌入 Provider 取请求的 `Provider`、`XiHan:AI:Knowledge:EmbeddingProvider`、目录的 `DefaultEmbeddingProvider` 中第一个非空的；都为空即 `AI_NOTFOUND_001`（`ai.provider.embedding_default_missing`）。`Knowledge:EmbeddingProvider` 是知识库自己的显式选择，不是默认名，保留且先于目录的默认。
- 框架自带的 `AIProviderCatalog` 照旧从配置给出：`DefaultProvider` 是配置的 `DefaultProvider`，没写且只配了一个 Provider 时是它；`DefaultEmbeddingProvider` 是配置的 `DefaultEmbeddingProvider`，没写时同 `DefaultProvider`（即 `AIOptions.ResolveDefault*`）。
- `AIProviderConformance` 补三例（用例 3–5）：
  - 有默认（`StartAsync` 的夹具，被测 Provider 是聊天与嵌入的默认）：两个默认名都是它，不写名字的解析与写名字的是同一实例，`IAIChat` 不写 Provider 由它应答。
  - 无默认（新增抽象方法 `StartWithoutDefaultAsync`）：两个默认名为 `null`，不写名字的解析与 `IAIChat` 都是 `AI_NOTFOUND_001` 带上面两个键，写了名字照常。
  - 聊天与嵌入分开的默认（新增虚方法 `StartWithSeparateDefaultsAsync`，`AIProviderHost.EmbeddingProviderName` 给出默认嵌入名）：各按各的默认解析；不覆写（返回 `null`）即跳过，只支持一个默认的目录（BasicApp 4.x 的提供商表只有一个 `IsDefault`）不必伪造。

### 理由

- 默认名属于目录：谁维护 Provider 表，谁定默认。流水线只问目录，替换目录的应用不必再在配置里镜像一份默认名，也不会出现「目录解析成功、流水线查不到」的两种回答。
- 同步属性与目录现有的读取一致，`Resolve*(null)` 与默认名是同一份快照；读它不做 I/O，所以不带取消令牌，「异步 API 接受并传递 `CancellationToken`」的约定不适用。要查库的目录照 F-N4-2 持快照，BasicApp 方案 2.3 本来就把「外加缺省名」放进快照。
- 两个属性而不是带用途参数的方法：成员与配置键 `DefaultProvider` / `DefaultEmbeddingProvider`、`AIOptions` 一一对应，不新增类型。
- 没有默认是可预期的状态（应用要求每次显式写名字），用不带参数的专门消息键说清，比以前的「AI Provider '' 未配置」可读；错误码沿用 `AI_NOTFOUND_001`，调用方的处理不变。
- 分开默认的用例设为可跳过：接口允许两者不同，但「只有一个默认」的目录同样合规，强制会逼目录多存一个标记。

### 影响

- 公开 API（都在 Unshipped，直接改，不留兼容）：`IAIProviderCatalog` 与 `AIProviderCatalog` 新增 `DefaultProvider`、`DefaultEmbeddingProvider`；`AIMessages` 新增 `DefaultProviderMissing`、`DefaultEmbeddingProviderMissing`；`AIChat` 构造去掉 `AIOptions options`（经容器解析的不受影响）；`AIProviderConformance` 新增抽象 `StartWithoutDefaultAsync`、虚 `StartWithSeparateDefaultsAsync` 与用例 3–5，`AIProviderHost` 新增 `EmbeddingProviderName`。
- 自写目录的应用要实现两个新属性；继承 `AIProviderConformance` 的测试要实现 `StartWithoutDefaultAsync`，`StartAsync` 的夹具里被测 Provider 要是默认。
- 行为：替换了目录的应用，不写 Provider 的聊天、摄入、检索改用目录的默认名（以前 `AI_NOTFOUND_001`）；没有默认时的消息由 `ai.provider.not_found`（名字为空）改为两个新键，错误码不变。只用框架目录的应用没有变化。
- 没有新错误码、配置键；`error-codes.md` 的 `AI_NOTFOUND_001` 补写「没给名字而目录没有默认」。

## 二、知识库的向量存储 Provider 自带健康项 `ai.vectordata.store`（F-N4-4）

### 背景

`VectorDataKnowledgeFeature` 的清单没有 `contributes.health`，向量库不可达时 `/health/ready` 看不到任何条目，只有检索返回 `AI_UNAVAILABLE_002`。4.x 宿主有自写的 `QdrantHealthCheck`；按 ADR-0030，健康项由 Provider 自带，BasicApp 6.0 不另写，于是 6.0 没有向量库的健康项。

### 备选

1. 按 ADR-0030 的连接类写法，由 `AI.VectorData` 用内核的 `PeriodicHealthContributor` 周期探测（采纳）。
2. 由登记 `VectorStore` 的连接器或应用登记：连接器不认识框架的健康契约，应用各写一份，与 ADR-0030「Provider 自带」相反。
3. 探针用 `ListCollectionNamesAsync`：最通用，但按租户分集合的应用（BasicApp 每个租户一个 `knowledge.<租户>` 集合）每次要列出全部集合。
4. 探针对默认集合做一次真实检索或写入：要建集合、要向量，探针有副作用。
5. 探针由 `VectorStore.GetService` 取连接器的原生客户端各做各的 PING：要认识每种连接器，`AI.VectorData` 就不再与连接器无关。

### 决策

- 条目名 `ai.vectordata.store`（`<领域>.<机制>.<对象>`），常量 `VectorDataKnowledgeFeature.HealthName`，清单 `Health = [HealthName]`。只在被选为 `ai.knowledge` 时登记存储与条目，没被选中的不探测。
- 间隔与超时：新增 `VectorDataKnowledgeOptions`（配置节 `XiHan:AI:VectorData`）的 `HealthProbeInterval`，配置键 `HealthProbeIntervalSeconds`，默认 15 秒，也是单次探测的超时；不是有限的正数即 `ArgumentException`，装配时启动失败。知识库自己的选项仍在 `XiHan:AI:Knowledge`（照 `Scheduling.Database` 另起 Provider 自己的配置节）。
- 探针（`VectorDataKnowledgeStore.ProbeAsync`，在根容器上取存储，不开作用域、不经管道）：问向量存储默认集合（`XiHan:AI:Knowledge:Collection`）是否存在（`VectorStore.CollectionExistsAsync`），存在与否都算可达；不列集合、不建集合、不读写记录。对连接器返回的任务按令牌 `WaitAsync`：连接器不理会取消令牌时也按时限结束等待。
- 判定：应答 Ready；连接器抛出 → Degraded，`AI_UNAVAILABLE_002`（`ai.knowledge.unavailable`，`reason` 是连接器的异常消息，与存储操作报告的错误同一个构造）；超时 → Degraded，`KERNEL_UNAVAILABLE_005`（探针宿主的报告）。没有 Failed。启动时同步首探，向量库连不上只让条目 Degraded，不让启动失败；首探放在「容器里没有 `VectorStore`」与提示词校验两项装配检查之后，前面失败时不留下在跑的循环。
- 连接信息：条目不带 `Data` 与 `Description`，不写端点、集合名、密钥；探针端点本来只输出状态与错误码，原因只进日志与健康快照。

### 理由

- 与其他连接类 Provider 同一个探针宿主：`Check()` 只读缓存，向量库卡住最多让条目 Degraded，不拖慢 readiness，也不改变「Degraded 仍服务」的聚合语义（ADR-0030 备选 4 否）。
- `CollectionExistsAsync` 是 `VectorStore` 抽象自带、各连接器都实现的轻量往返（Qdrant 是一次 `CollectionExists`），与集合多少无关，也不需要集合定义与向量；问默认集合而不是任意名字，是因为名字一定合连接器的命名规则（应用已为它配过）。
- 连接器抛出报 `AI_UNAVAILABLE_002` 而不是让探针宿主统一包成 `KERNEL_UNAVAILABLE_005`：同一个故障，摄入与检索返回的就是这个码，运维看健康项与看调用错误是同一个码；超时仍由探针宿主报 `KERNEL_UNAVAILABLE_005`，与其他 Provider 的超时一致。
- 没有 Failed：向量库不可达是外部条件、可恢复，不是配置错误；配置错误（没有 `VectorStore`、提示词不存在、间隔不合法）已在装配或启动时失败。
- `reason` 保留连接器的异常消息：与存储操作的错误、其他连接类 Provider 的探针一致，排查时要知道是拒绝连接、认证失败还是 TLS 错误；主机与端口不是机密（ADR-0078），探针端点也不输出它；连接器的消息若带口令类内容，存储操作的错误早已同样带出，应在连接器一侧处理。

### 影响

- 公开 API：`VectorDataKnowledgeFeature.HealthName`、`VectorDataKnowledgeOptions`（`ConfigurationSection`、`HealthProbeInterval`、`FromConfiguration`）。`VectorDataKnowledgeStore` 构造不变。
- 新配置键 `XiHan:AI:VectorData:HealthProbeIntervalSeconds`（CLI 知识库 `configuration.json` 新增一节，`catalog.json` 重新生成）。没有新错误码与消息键；`error-codes.md` 的 `AI_UNAVAILABLE_002` 补写健康项。
- 行为：装了 `VectorDataKnowledgeFeature` 的应用启动时多一次同步探测（受间隔上限约束），健康快照多一条 `ai.vectordata.store`；向量库不可达时应用以 Degraded 启动而不是失败（以前看不出）。BasicApp 不必自写向量库的健康项。
