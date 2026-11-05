来源：https://framework.docs.xihanfun.com/packages/ai-vectordata

# XiHan.Framework.AI.VectorData

AI 知识库的存储 Provider：把 `IKnowledgeStore` 映射到应用登记的 `Microsoft.Extensions.VectorData` 向量存储 `VectorStore`，任何连接器都可以用。提供 `ai.knowledge@1`。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.AI.VectorData` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `ai.knowledge@1`（`role: provider`）；依赖 `kernel.hosting@1`、`ai.embedding@1` |
| 特性入口 | `VectorDataKnowledgeFeature`（清单 `xihan.ai.vectordata`，`IHostedFeature`） |
| 配置节 | `XiHan:AI`（用到 `Knowledge:Dimensions`、`Knowledge:Collection`，本包自己的 `VectorData:HealthProbeIntervalSeconds`） |
| 健康项 | `ai.vectordata.store`（向量存储是否应答） |
| 依赖 | `XiHan.Framework.AI.Abstractions`；`Microsoft.Extensions.VectorData.Abstractions` |
| AOT | 不兼容（VectorData 的记录映射基于反射与表达式，csproj 设 `IsAotCompatible=false`） |
| 稳定性 | Preview |

## 概述

特性被选中时把 `VectorDataKnowledgeStore` 登记为 `IKnowledgeStore`，并登记健康项 `ai.vectordata.store`；向量存储本身由应用选连接器登记。启动时容器里没有 `VectorStore` 就以 `AI_UNAVAILABLE_002` 启动失败，装配错误不会拖到第一次检索才暴露。

每个知识库集合对应一个向量集合，集合定义按 `XiHan:AI:Knowledge:Dimensions` 构造：

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `Key` | `Guid` | 键，由 `RecordId` 确定性推出（`KnowledgeVectorRecord.KeyOf`） |
| `RecordId` | `string` | 记录 ID，`<documentId>#<chunkIndex>`，也就是 `KnowledgeRecord.Id` 与 `RetrievedChunk.Id` |
| `DocumentId` | `string` | 带索引，按文档过滤与删除 |
| `ChunkIndex` | `int` | 带索引，块序号；重新摄入按它删掉多余的旧块 |
| `Text` | `string` | 块文本 |
| `Metadata` | `string` | 元数据序列化成的 JSON（源生成，不经反射） |
| `Vector` | `ReadOnlyMemory<float>` | 维度为 `Dimensions`，余弦相似度 |

切块、嵌入、检索与提示增强都在 `AI.Abstractions` 里，本包只负责写入、按文档删除与向量搜索。

## 何时使用

需要 RAG、语义检索时。向量库的选择由连接器决定（内存、Qdrant、PostgreSQL pgvector、Redis、Azure AI Search 等），换库只换连接器，一致性套件保证语义相同。连接器要接受 `Guid` 键；Microsoft 的连接器已迁到 `CommunityToolkit.VectorData.*` 包，要选这些包：旧的 `Microsoft.SemanticKernel.Connectors.Qdrant` 预览包与当前的 VectorData 不兼容，搜索时报找不到 `VectorSearchFilter` 类型。本包不进 `Profiles.Full`，因为它需要应用登记向量存储。

## 安装与启用

```bash
dotnet add package XiHan.Framework.AI.VectorData --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](../quickstart#获取预览包)。还需要一个提供 `ai.embedding@1` 的 Provider（如 [AI.OpenAI](./ai-openai)）与一个向量库连接器：

```csharp
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.VectorData;
using XiHan.Framework.AI.OpenAI;
using XiHan.Framework.AI.VectorData;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 连接器由应用选择；这里用内存连接器（CommunityToolkit.VectorData.InMemory 包），只适合开发
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

知识库配置在装配时读一次、不热更新，改了要重启应用。

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `XiHan:AI:Knowledge:Dimensions` | 1536 | 向量集合的维度，必须等于嵌入模型输出的维度 |
| `XiHan:AI:Knowledge:Collection` | `knowledge` | 不指定集合时用的集合名；健康探针问的也是它 |
| `XiHan:AI:VectorData:HealthProbeIntervalSeconds` | 15 | 健康项的重探间隔（秒），也是单次探测的超时；不是有限的正数时启动失败 |

```json
{ "XiHan": { "AI": { "Knowledge": { "Collection": "faq", "Dimensions": 768 } } } }
```

切块、检索条数等其余键见 [AI.Abstractions](./ai-abstractions#配置)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `VectorDataKnowledgeFeature` | 能力包入口；启动时检查容器里有 `VectorStore`；常量 `HealthName`（`ai.vectordata.store`） |
| `VectorDataKnowledgeOptions` | 本包自己的选项（`XiHan:AI:VectorData`）：`HealthProbeInterval` |
| `VectorDataKnowledgeStore` | `IKnowledgeStore` 的实现：`UpsertAsync`、`DeleteAsync`、`TrimAsync`、`SearchAsync` |
| `KnowledgeVectorRecord` | 向量记录；`Definition(dimensions)` 构造集合定义，`KeyOf(recordId)` 由记录 ID 推出向量键 |

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

- 写入：先确保集合存在再批量 upsert；空列表直接成功。同一 ID 再次写入即替换：键由记录 ID 确定性推出（UTF-8 字节的 SHA-256 取前 16 字节，写上 RFC 9562 的版本 8 与变体位）。
- 键是 `Guid`（ADR-0111）：Qdrant 这类连接器不接受字符串键。键形状变化之前建的集合要重建，换集合或删掉旧集合重新摄入。连接器不接受 `Guid` 键时，写入与搜索返回 `AI_UNAVAILABLE_002`。
- 删除与裁剪：集合不存在时直接成功；连接器不一定支持按条件删除，所以按条件分页取键（每页 500 个）再按键删除，直到取不满一页。删除的条件是 `DocumentId`，裁剪的条件是 `DocumentId` 加 `ChunkIndex >= 新块数`。
- `DocumentId` 与 `ChunkIndex` 在集合定义里都是可过滤字段。要求过滤字段在建集合时声明的向量库（如 Azure AI Search）上，早于此定义建出的集合缺 `ChunkIndex` 的过滤能力，裁剪会以 `AI_UNAVAILABLE_002` 失败，换集合或删掉旧集合重新摄入。
- 搜索：集合不存在返回空列表；`DocumentIds` 过滤以常量比较的表达式下推；有元数据过滤时向量库取 `top × 4` 条，在进程内按键值相等过滤后按 `Score` 降序取前 `top` 条，命中稀疏时可能少于 `top` 条。连接器不给分数时按 0 处理。
- 向量集合的维度在创建后不能改：换嵌入模型时换 `Collection`，或删掉旧集合重新摄入。
- 不区分租户：要隔离时按集合或元数据区分。
- 存储操作里连接器抛出的异常转成 `AI_UNAVAILABLE_002`，`reason` 只写异常类型的全名（ADR-0112）：这个错误会返回给调用方，连接器的异常消息可能带地址或连接串。完整异常进日志（类别 `VectorDataKnowledgeStore`），每次失败一条 Warning，带操作名与集合名。取消照常抛出。
- 健康项 `ai.vectordata.store`（ADR-0030 的连接类写法，ADR-0110）：启动时同步探一次，之后按 `HealthProbeIntervalSeconds` 在后台重探；探针只问默认集合在不在（`CollectionExistsAsync`，存在与否都算可达），不列集合、不建集合、不读写记录，连接器不理会取消令牌时也按时限结束等待。应答 Ready；连接器抛出 Degraded（`AI_UNAVAILABLE_002`，与存储操作同一个错误）；超时 Degraded（`KERNEL_UNAVAILABLE_005`）；没有 Failed，向量库不可达时应用照常启动与服务。条目不带数据与说明，不写端点或连接信息。
- 健康项的 `reason` 只写异常类型的全名（ADR-0111），连接器的异常消息可能带地址或连接串，不进健康快照。完整异常进日志（类别 `VectorDataKnowledgeStore`）：同一种失败第一次记 Warning，持续时记 Debug。
- 通过 `AI.Conformance` 的 `KnowledgeStoreConformance`（写入与排序、`top`、文档与元数据过滤、重写入替换、按文档删除、按块序号裁剪、集合隔离、空集合搜索）。设置 `XIHAN_TEST_QDRANT`（gRPC 地址，如 `http://localhost:6334`）时，套件与摄入、检索用例另在真实 Qdrant 上跑，连接器是 `CommunityToolkit.VectorData.Qdrant`。

## 相关

- [AI](../guide/ai#知识库)
- [AI.Abstractions](./ai-abstractions)
- [AI.OpenAI](./ai-openai)
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0022《AI：聊天、嵌入、工具与知识》
