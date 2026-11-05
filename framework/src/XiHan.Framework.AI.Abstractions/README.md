# XiHan.Framework.AI.Abstractions

## 关于

`XiHan.Framework.AI.Abstractions` 定义 AI 的统一入口：以 `Microsoft.Extensions.AI` 的 `IChatClient`、`IEmbeddingGenerator` 为基础，提供模型目录、带提示词与护栏的聊天、把操作暴露成模型工具，以及知识库（RAG）流水线。它还自带进程内的工具目录。

## 主要特性

* 按名字解析聊天与嵌入模型，默认模型随配置
* 聊天入口统一处理提示词、护栏、工具与超时
* `Expose.Ai` 的操作自动成为模型可调用的工具
* 知识库摄入、检索与提示增强，存储由提供程序实现

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.AI.Abstractions --prerelease
```

### 使用

通常由实现包带入；写业务代码或自定义提供程序时才直接引用。模型由 `XiHan.Framework.AI.OpenAI` 提供，知识库存储由 `XiHan.Framework.AI.VectorData` 提供。其他组合里启用工具目录并调用聊天入口：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<AIToolsFeature>());
```

```csharp
var reply = await chat.CompleteAsync(new AIChatRequest
{
    Messages = [new ChatMessage(ChatRole.User, "How many orders are pending?")],
    Prompt = "assistant",
    Tools = ["orders_list"],
}, ct);
```

## 主要类型

此库提供的主要类型：

* `IAIChat`、`AIChatRequest`：聊天入口，`CompleteAsync` / `StreamAsync`
* `IAIProviderCatalog`：按名字解析聊天与嵌入客户端
* `IAIPromptCatalog`、`AIPromptTemplate`：提示词模板
* `IAIGuardrail`、`KeywordBlocklistGuardrail`：输入护栏
* `IAIToolCatalog`：工具目录
* `IKnowledgeIngestor`、`IKnowledgeRetriever`、`IKnowledgeStore`：知识库流水线与存储契约

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/ai-abstractions)
* [AI 指南](https://framework.docs.xihanfun.com/guide/ai)

## 相关包

* OpenAI 兼容模型：[XiHan.Framework.AI.OpenAI](https://www.nuget.org/packages/XiHan.Framework.AI.OpenAI)
* 向量知识库：[XiHan.Framework.AI.VectorData](https://www.nuget.org/packages/XiHan.Framework.AI.VectorData)
* 一致性套件：[XiHan.Framework.AI.Conformance](https://www.nuget.org/packages/XiHan.Framework.AI.Conformance)

## 反馈与贡献

`XiHan.Framework.AI.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
