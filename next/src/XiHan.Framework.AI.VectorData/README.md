# XiHan.Framework.AI.VectorData

AI 知识库的 `Microsoft.Extensions.VectorData` Provider（Ring 3，RFC-0022）：提供 `ai.knowledge@1`，把 `IKnowledgeStore` 映射到应用注册的 `VectorStore`——任何连接器（内存、Qdrant、PostgreSQL pgvector、Redis、Azure AI Search…）都行。

| 类型 | 用途 |
| --- | --- |
| `VectorDataKnowledgeFeature` | 要求 `ai.embedding@1`；启动时容器里没有 `VectorStore` → `AI_UNAVAILABLE_002` 启动失败 |
| `VectorDataKnowledgeStore` | 每个知识库集合一个向量集合（定义按 `XiHan:AI:Knowledge:Dimensions` 构造，余弦相似度）；`DocumentIds` 过滤下推，元数据过滤取回 `top × 4` 条后在进程内完成；按文档删除先分页取键再按键删 |
| `KnowledgeVectorRecord` | 向量记录：`Id`、`DocumentId`、`ChunkIndex`、`Text`、`Metadata`（JSON）、`Vector` |

```csharp
builder.Services.AddSingleton<VectorStore>(new InMemoryVectorStore());   // 或任何连接器
builder.AddXiHan(x => x.UseStandardProfile().UseFeature<OpenAIFeature>().UseFeature<VectorDataKnowledgeFeature>());

await ingestor.IngestAsync(new KnowledgeIngestRequest { DocumentId = "faq", Text = faqText }, ct);
var chunks = (await retriever.RetrieveAsync(new KnowledgeQuery { Text = "How do I reset my password?" }, ct)).ValueOrThrow();
var prompt = augmenter.Augment("How do I reset my password?", chunks);
```

向量集合的维度在创建后不能改：换嵌入模型要换 `Collection` 或删掉旧集合。通过 `AI.Conformance` 的 `KnowledgeStoreConformance`。

## 依赖方向

依赖 `XiHan.Framework.AI.Abstractions`、`Microsoft.Extensions.VectorData.Abstractions`。不进 `Profiles.Full`（需要应用注册向量存储）。

## 状态

5.x P0：可用（Preview）。
