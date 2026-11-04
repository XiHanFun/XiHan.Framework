# XiHan.Framework.AI.VectorData

AI 知识库的 `Microsoft.Extensions.VectorData` Provider（Ring 3，RFC-0022）：提供 `ai.knowledge@1`，把 `IKnowledgeStore` 映射到应用注册的 `VectorStore`——任何连接器（内存、Qdrant、PostgreSQL pgvector、Redis、Azure AI Search…）都行。

| 类型 | 用途 |
| --- | --- |
| `VectorDataKnowledgeFeature` | 要求 `ai.embedding@1`；启动时容器里没有 `VectorStore` → `AI_UNAVAILABLE_002` 启动失败；健康项 `HealthName`（`ai.vectordata.store`） |
| `VectorDataKnowledgeOptions` | 本包自己的选项 `XiHan:AI:VectorData`：`HealthProbeIntervalSeconds`（默认 15，有限的正数，也是单次探测的超时） |
| `VectorDataKnowledgeStore` | 每个知识库集合一个向量集合（定义按 `XiHan:AI:Knowledge:Dimensions` 构造，余弦相似度）；`DocumentIds` 过滤下推，元数据过滤取回 `top × 4` 条后在进程内完成；按文档删除与按块序号裁剪（`DocumentId`、`ChunkIndex` 是可过滤字段）都先分页取键再按键删 |
| `KnowledgeVectorRecord` | 向量记录：`Key`（由记录 ID 确定性推出的 `Guid`，`KeyOf`）、`RecordId`、`DocumentId`、`ChunkIndex`、`Text`、`Metadata`（JSON）、`Vector` |

```csharp
// 连接器由应用选择并自行引用其包，本包不引用任何连接器；例如内存连接器（CommunityToolkit.VectorData.InMemory），只适合开发
builder.Services.AddSingleton<VectorStore>(new InMemoryVectorStore());
builder.AddXiHan(x => x.UseStandardProfile().UseFeature<OpenAIFeature>().UseFeature<VectorDataKnowledgeFeature>());

await ingestor.IngestAsync(new KnowledgeIngestRequest { DocumentId = "faq", Text = faqText }, ct);
var chunks = (await retriever.RetrieveAsync(new KnowledgeQuery { Text = "How do I reset my password?" }, ct)).ValueOrThrow();
var prompt = augmenter.Augment("How do I reset my password?", chunks);
```

健康项 `ai.vectordata.store`（ADR-0030、ADR-0110）：被选为 `ai.knowledge` 时登记，启动时同步探一次、之后按间隔在后台重探；探针只问默认集合（`Knowledge:Collection`）在不在，不列集合、不建集合、不读写记录，连接器不理会取消令牌也按时限结束等待。应答 Ready；连接器抛出 Degraded（`AI_UNAVAILABLE_002`，与存储操作同一个错误）；超时 Degraded（`KERNEL_UNAVAILABLE_005`）；没有 Failed，向量库不可达时应用照常启动与服务。条目不带数据与说明，不写端点或连接信息；应用不必另写向量库的健康检查。

连接器抛出时，存储操作与健康项的 `reason` 都只写异常类型的全名，完整异常进日志（ADR-0111、ADR-0112）。

连接器要接受 `Guid` 键；键形状变化之前建的集合要重建（ADR-0111）。Qdrant 用 `CommunityToolkit.VectorData.Qdrant`：`Microsoft.SemanticKernel.Connectors.Qdrant` 预览包与当前的 VectorData 不兼容。

向量集合的维度在创建后不能改：换嵌入模型要换 `Collection` 或删掉旧集合。知识库配置在装配时读一次、不热更新，改了要重启应用（ADR-0074）。通过 `AI.Conformance` 的 `KnowledgeStoreConformance`。

## 依赖方向

依赖 `XiHan.Framework.AI.Abstractions`、`Microsoft.Extensions.VectorData.Abstractions`。不进 `Profiles.Full`（需要应用注册向量存储）。

## 状态

P0：可用（Preview）。
