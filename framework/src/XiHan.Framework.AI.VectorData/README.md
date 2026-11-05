# XiHan.Framework.AI.VectorData

## 关于

`XiHan.Framework.AI.VectorData` 把 AI 知识库的存储接到 `Microsoft.Extensions.VectorData` 的 `VectorStore` 上，内存、Qdrant、pgvector、Redis、Azure AI Search 等任何连接器都能用。需要 RAG、语义检索时使用。

## 主要特性

* 按知识库集合建向量集合，余弦相似度检索
* 文档过滤下推到向量库，元数据过滤在进程内完成
* 健康项探测向量库是否应答，不可达时应用照常启动

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.AI.VectorData --prerelease
```

### 使用

向量库连接器由应用选择并自行引用其包；还需要一个提供嵌入的模型（如 `AI.OpenAI`）。下例用内存连接器，只适合开发：

```csharp
builder.Services.AddSingleton<VectorStore>(new InMemoryVectorStore());
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OpenAIFeature>()
    .UseFeature<VectorDataKnowledgeFeature>());
```

```json
{ "XiHan": { "AI": { "Knowledge": { "Collection": "faq", "Dimensions": 768 } } } }
```

摄入与检索走 `AI.Abstractions` 的 `IKnowledgeIngestor` 与 `IKnowledgeRetriever`，不直接调用本包。

## 主要类型

此库提供的主要类型：

* `VectorDataKnowledgeFeature`：启用入口
* `VectorDataKnowledgeStore`：`IKnowledgeStore` 的实现
* `VectorDataKnowledgeOptions`：健康探测间隔等选项
* `KnowledgeVectorRecord`：向量记录

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/ai-vectordata)
* [AI 指南：知识库](https://framework.docs.xihanfun.com/guide/ai#知识库)

## 相关包

* AI 抽象：[XiHan.Framework.AI.Abstractions](https://www.nuget.org/packages/XiHan.Framework.AI.Abstractions)
* OpenAI 兼容模型：[XiHan.Framework.AI.OpenAI](https://www.nuget.org/packages/XiHan.Framework.AI.OpenAI)

## 反馈与贡献

`XiHan.Framework.AI.VectorData` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
