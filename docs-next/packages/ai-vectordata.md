# XiHan.Framework.AI.VectorData

AI 知识库的存储 Provider：把 `IKnowledgeStore` 映射到应用登记的 `Microsoft.Extensions.VectorData` 向量存储 `VectorStore`，任何连接器都可以用。提供 `ai.knowledge@1`。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.AI.VectorData` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `ai.knowledge@1`（`role: provider`）；依赖 `kernel.hosting@1`、`ai.embedding@1` |
| 特性入口 | `VectorDataKnowledgeFeature`（清单 `xihan.ai.vectordata`，`IHostedFeature`） |
| 配置节 | `XiHan:AI`（用到 `Knowledge:Dimensions`） |
| 依赖 | `XiHan.Framework.AI.Abstractions`；`Microsoft.Extensions.VectorData.Abstractions` 10.11.0 |
| AOT | 不兼容（VectorData 的记录映射基于反射与表达式，csproj 设 `IsAotCompatible=false`） |
| 稳定性 | Preview |

## 概述

特性被选中时把 `VectorDataKnowledgeStore` 登记为 `IKnowledgeStore`；向量存储本身由应用选连接器登记。启动时容器里没有 `VectorStore` 就以 `AI_UNAVAILABLE_002` 启动失败，装配错误不会拖到第一次检索才暴露。

每个知识库集合对应一个向量集合，集合定义按 `XiHan:AI:Knowledge:Dimensions` 构造：

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | `string` | 键，`<documentId>#<chunkIndex>` |
| `DocumentId` | `string` | 带索引，按文档过滤与删除 |
| `ChunkIndex` | `int` | 块序号 |
| `Text` | `string` | 块文本 |
| `Metadata` | `string` | 元数据序列化成的 JSON（源生成，不经反射） |
| `Vector` | `ReadOnlyMemory<float>` | 维度为 `Dimensions`，余弦相似度 |

切块、嵌入、检索与提示增强都在 `AI.Abstractions` 里，本包只负责写入、按文档删除与向量搜索。

## 何时使用

需要 RAG、语义检索时。向量库的选择由连接器决定（内存、Qdrant、PostgreSQL pgvector、Redis、Azure AI Search 等），换库只换连接器，一致性套件保证语义相同。本包不进 `Profiles.Full`，因为它需要应用登记向量存储。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.AI.VectorData" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。还需要一个提供 `ai.embedding@1` 的 Provider（如 [AI.OpenAI](./ai-openai)）与一个向量库连接器：

```csharp
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using XiHan.Framework.AI.OpenAI;
using XiHan.Framework.AI.VectorData;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 连接器由应用选择；这里用 Semantic Kernel 的内存连接器（Microsoft.SemanticKernel.Connectors.InMemory 包），只适合开发
builder.Services.AddSingleton<VectorStore>(new InMemoryVectorStore());
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OpenAIFeature>()
    .UseFeature<VectorDataKnowledgeFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

## 配置

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `XiHan:AI:Knowledge:Dimensions` | 1536 | 向量集合的维度，必须等于嵌入模型输出的维度 |
| `XiHan:AI:Knowledge:Collection` | `knowledge` | 不指定集合时用的集合名 |

```json
{ "XiHan": { "AI": { "Knowledge": { "Collection": "faq", "Dimensions": 768 } } } }
```

切块、检索条数等其余键见 [AI.Abstractions](./ai-abstractions#配置)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `VectorDataKnowledgeFeature` | 能力包入口；启动时检查容器里有 `VectorStore` |
| `VectorDataKnowledgeStore` | `IKnowledgeStore` 的实现：`UpsertAsync`、`DeleteAsync`、`SearchAsync` |
| `KnowledgeVectorRecord` | 向量记录；`Definition(dimensions)` 构造集合定义 |

## 使用示例

摄入与检索走 `AI.Abstractions` 的流水线，不直接调用本包：

```csharp
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.Primitives;

namespace MyApp.Assistants;

public sealed class HandbookSearch(IKnowledgeIngestor ingestor, IKnowledgeRetriever retriever)
{
    public ValueTask<Result<int>> ImportAsync(string chapterId, string text, CancellationToken cancellationToken)
        => ingestor.IngestAsync(new KnowledgeIngestRequest { DocumentId = chapterId, Text = text, Collection = "handbook", Metadata = new Dictionary<string, string> { ["lang"] = "en" } }, cancellationToken);

    public ValueTask<Result<IReadOnlyList<RetrievedChunk>>> SearchAsync(string question, CancellationToken cancellationToken)
        => retriever.RetrieveAsync(new KnowledgeQuery
        {
            Text = question,
            Collection = "handbook",
            Top = 5,
            // 文档过滤下推到向量库；元数据过滤在取回 top × 4 条后于进程内完成
            Filter = new RetrievalFilter { Metadata = new Dictionary<string, string> { ["lang"] = "en" } },
        }, cancellationToken);
}
```

完整的问答示例见 [AI](../guide/ai#知识库)。

## 行为与限制

- 写入：先确保集合存在再批量 upsert；空列表直接成功。同一 ID 再次写入即替换。
- 删除：集合不存在时直接成功；连接器不一定支持按条件删除，所以按文档分页取键（每页 500 个）再按键删除，直到取不满一页。
- 搜索：集合不存在返回空列表；`DocumentIds` 过滤以常量比较的表达式下推；有元数据过滤时向量库取 `top × 4` 条，在进程内按键值相等过滤后按 `Score` 降序取前 `top` 条，命中稀疏时可能少于 `top` 条。连接器不给分数时按 0 处理。
- 向量集合的维度在创建后不能改：换嵌入模型时换 `Collection`，或删掉旧集合重新摄入。
- 不区分租户：要隔离时按集合或元数据区分。
- 连接器抛出的异常转成 `AI_UNAVAILABLE_002`，`reason` 是异常消息；取消照常抛出。
- 通过 `AI.Conformance` 的 `KnowledgeStoreConformance`（写入与排序、`top`、文档与元数据过滤、重写入替换、按文档删除、集合隔离、空集合搜索）。

## 相关

- [AI](../guide/ai#知识库)
- [AI.Abstractions](./ai-abstractions)
- [AI.OpenAI](./ai-openai)
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0022《AI：聊天、嵌入、工具与知识》
