# XiHan.Framework.AI.Abstractions

AI 的抽象包：以 `Microsoft.Extensions.AI` 为线格式的 Provider 目录、带提示词 / 护栏 / 工具的聊天入口、把操作契约桥接成模型工具的目录，以及知识库流水线与存储契约。默认提供进程内的工具目录 `ai.tools@1`。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.AI.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 提供 `ai.tools@1`（`AIToolsFeature`，`role: default`）；定义 `ai.chat@1`、`ai.embedding@1`、`ai.knowledge@1`；`AIToolsFeature` 依赖 `kernel.hosting@1` |
| 特性入口 | `AIToolsFeature`（清单 `xihan.ai.tools`，`IHostedFeature`） |
| 配置节 | `XiHan:AI` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`；`Microsoft.Extensions.AI.Abstractions` 10.11.0，`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions`、`Microsoft.Extensions.Logging.Abstractions` 10.0.13 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

公开签名直接使用 `IChatClient`、`IEmbeddingGenerator<string, Embedding<float>>`、`ChatMessage`、`AITool` 等 `Microsoft.Extensions.AI` 类型，不复制一套模型接口（ADR-0017）。本包提供：

- Provider 目录 `IAIProviderCatalog`：按名字解析客户端并缓存，Provider 包实现 `IChatClientFactory` / `IEmbeddingGeneratorFactory`。
- 聊天入口 `IAIChat`：提示词渲染 → 工具绑定 → 护栏 → 解析 Provider → 调用。
- 提示词目录 `IAIPromptCatalog` 与模板 `AIPromptTemplate`。
- 护栏 `IAIGuardrail` 与内置的 `KeywordBlocklistGuardrail`。
- 工具目录 `IAIToolCatalog`：`AddAITool` 的工具加上 `Expose.Ai` 的操作，后者在新作用域经完整管道执行。
- 知识库流水线：`IKnowledgeIngestor`、`IKnowledgeRetriever`、`IRagPromptAugmenter`、`IChunkingStrategy`，以及 Provider 契约 `IKnowledgeStore`。

`AddAI(configuration)` 幂等地登记以上全部服务、选项与消息目录 `ai`；`AIToolsFeature`、`OpenAIFeature`、`VectorDataKnowledgeFeature` 都调用它。`AIToolsFeature` 在启动时构建工具目录，工具名不合法或重名即启动失败。用法见 [AI](../guide/ai)。

## 何时使用

业务代码只依赖本包的接口；模型由 [AI.OpenAI](./ai-openai) 提供，知识库存储由 [AI.VectorData](./ai-vectordata) 提供。接入其他模型服务时，在自己的 Provider 包里实现两个工厂接口，或者直接用 Microsoft.Extensions.AI 生态里已有的 `IChatClient` 实现包一层工厂。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.AI.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Full` 已装配 `AIToolsFeature`；其他组合里 `UseFeature<AIToolsFeature>()`。

## 配置

`XiHan:AI` 下的键：

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `DefaultProvider` | 只有一个 Provider 时取它 | 默认聊天 Provider |
| `DefaultEmbeddingProvider` | 同 `DefaultProvider` | 默认嵌入 Provider |
| `Providers:<name>:Kind` | `openai` | 种类，转小写后与工厂的 `Kind` 匹配 |
| `Providers:<name>:Endpoint` | 空 | 绝对 URI；空表示 Provider 的官方端点 |
| `Providers:<name>:ApiKey` | 空 | 空时由 Provider 决定 |
| `Providers:<name>:ChatModel`、`EmbeddingModel` | — | 解析对应客户端时必填 |
| `Providers:<name>:EmbeddingDimensions` | 模型缺省 | 至少 1 |
| `Providers:<name>:TimeoutSeconds` | 100 | 单次调用超时 |
| `Providers:<name>:Properties:<key>` | — | 透传给工厂的字符串 |
| `Tools:<操作 ID>` | 按约定推导 | 覆盖操作工具名 |
| `Prompts:<name>:Template`、`Description` | — | 配置里的提示词 |
| `Guardrails:Enabled` | `false` | 登记内置护栏 |
| `Guardrails:BlockedKeywords`、`InjectionPatterns` | 空 | 数组 |
| `Guardrails:UseBuiltInInjectionHeuristics` | `true` | 内置中英文提示注入模式 |
| `Knowledge:Collection` | `knowledge` | 默认集合 |
| `Knowledge:Dimensions` | 1536 | 嵌入维度 |
| `Knowledge:ChunkSize`、`ChunkOverlap` | 1000、100 | 切块（字符） |
| `Knowledge:Top` | 5 | 默认检索条数 |
| `Knowledge:EmbeddingProvider`、`PromptName` | 空 | 知识库的嵌入 Provider 与增强模板 |

```json
{
  "XiHan": {
    "AI": {
      "DefaultProvider": "cloud",
      "Providers": {
        "cloud": { "Kind": "openai", "ApiKey": "dev-openai-key", "ChatModel": "gpt-4o-mini", "EmbeddingModel": "text-embedding-3-small", "EmbeddingDimensions": 1536 }
      },
      "Tools": { "orders.list": "list_orders" }
    }
  }
}
```

`Endpoint` 不是绝对 URI，或数值项写错时按缺省处理，不报错。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `AICapabilities` | `Chat`、`Embedding`、`Tools`、`Knowledge` 与版本 1 |
| `AIOptions`、`Providers.AIProviderOptions` | 选项；`ResolveDefaultProvider()`、`ResolveDefaultEmbeddingProvider()` 给出实际的默认名；`AIProviderOptions.DefaultTimeout` 为 100 秒 |
| `AIServiceCollectionExtensions` | `AddAI`、`AddAIChatClientFactory<T>`、`AddAIEmbeddingGeneratorFactory<T>`、`AddAIGuardrail<T>`、`AddAIPrompt`、`AddAITool` |
| `AIToolsFeature` | `ai.tools@1` 的特性入口 |
| `Providers.IAIProviderCatalog` | `Providers`、`Find`、`ResolveChatClient`、`ResolveEmbeddingGenerator`、`Invalidate` |
| `Providers.IChatClientFactory`、`IEmbeddingGeneratorFactory` | Provider 包实现：`Kind` 与 `Create(AIProviderOptions)` |
| `Chat.IAIChat`、`Chat.AIChatRequest` | 聊天入口；`CompleteAsync`、`StreamAsync` |
| `Prompts.IAIPromptCatalog`、`Prompts.AIPromptTemplate` | 提示词目录与模板；`Render`、`Placeholders` |
| `Guardrails.IAIGuardrail`、`GuardrailVerdict`、`KeywordBlocklistGuardrail`、`AIGuardrailOptions` | 护栏 |
| `Tools.IAIToolCatalog`、`Tools.OperationAIFunction`、`Tools.AIToolNameConvention` | 工具目录、操作工具（`OperationId`）、命名约定（`Derive`、`DescriptionKeyOf`，上限 64） |
| `Knowledge.IKnowledgeIngestor`、`IKnowledgeRetriever`、`IRagPromptAugmenter`、`IChunkingStrategy` | 流水线；`KnowledgeIngestorExtensions` 提供单篇的 `IngestAsync` 与 `RemoveAsync` |
| `Knowledge.IKnowledgeStore` | Provider 契约：`UpsertAsync`、`DeleteAsync`、`SearchAsync`（按 `Score` 降序） |
| `Knowledge.KnowledgeIngestRequest`、`KnowledgeQuery`、`KnowledgeRecord`、`RetrievedChunk`、`RetrievalFilter`、`ChunkingOptions`、`KnowledgeOptions` | 知识库的数据类型；`KnowledgeRecord.IdOf(documentId, chunkIndex)` 生成 `<documentId>#<chunkIndex>` |
| `AIIdentifiers` | 名字文法 `^[a-z0-9][a-z0-9._-]{0,63}$`，工具名 `^[A-Za-z0-9_-]{1,64}$` |
| `AIErrors`、`AIMessages` | 错误码与消息键 |

## 使用示例

登记一个提示词模板，同名时覆盖配置里的：

```csharp
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Prompts;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace MyApp.Assistants;

public sealed class PromptsFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.myapp.prompts",
        Package = "MyApp",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(PromptsFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        // 与配置里同名的模板以这里为准；{{ 与 }} 输出字面的花括号
        context.Services.AddAIPrompt(new AIPromptTemplate(
            "order-summary",
            "Summarize order {orderId} for {audience}. Reply as JSON like {{\"summary\": \"<text>\"}}.",
            "Order summary for a given audience"));
    }
}
```

直接从目录取嵌入生成器，不经护栏：

```csharp
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace MyApp.Assistants;

public sealed class EmbeddingService(IAIProviderCatalog catalog)
{
    public async ValueTask<Result<float[]>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var resolved = catalog.ResolveEmbeddingGenerator();
        if (!resolved.TryGetValue(out var generator))
        {
            return resolved.Error!;
        }

        // 直接使用目录里的生成器：不经护栏，传输异常照常抛出
        var embeddings = await generator.GenerateAsync([text], cancellationToken: cancellationToken);
        return embeddings[0].Vector.ToArray();
    }
}
```

聊天、护栏、工具与知识库的完整示例见 [AI](../guide/ai)。

## 行为与限制

- 目录按名字缓存实例；`Invalidate(name)` 释放缓存（实现了 `IDisposable` 的实例被释放），下次解析重建。选项在装配时读一次，`Invalidate` 不重读配置。
- `IAIChat` 是作用域服务；没有当前调用时工具绑定到本作用域新建的调用上下文。
- 护栏按登记顺序执行，只作用于 `IAIChat`；直接从目录取客户端的调用不经护栏。
- 工具目录在 `AIToolsFeature` 启动时构建；稳定性为 `Internal` 的操作不进目录。
- 操作工具在新作用域、新调用上下文里经完整管道执行，trace-id、截止时间与 Baggage 复制自调用方，凭据与安全上下文不复制；失败以 `{"error","message"}` JSON 交给模型。
- 同一文档重新摄入先删旧块再写新块，不是原子替换；嵌入维度必须等于 `Knowledge:Dimensions`。
- 没有 `IKnowledgeStore` 时流水线返回 `AI_UNAVAILABLE_002`，聊天与工具不受影响。

| 错误码 | 何时出现 |
| --- | --- |
| `AI_VALIDATION_001` | Provider 缺必填项、提示词缺参数、文档 ID 或问题为空、工具名不合法或重名 |
| `AI_VALIDATION_002` | 嵌入维度与知识库配置不一致 |
| `AI_NOTFOUND_001` | Provider 未配置，或种类没有登记工厂 |
| `AI_NOTFOUND_002` | 提示词模板不存在 |
| `AI_NOTFOUND_003` | 工具不存在 |
| `AI_DENIED_001` | 被护栏拦截 |
| `AI_TIMEOUT_001` | 模型调用超时 |
| `AI_UNAVAILABLE_001` | 模型端点不可达或返回错误 |
| `AI_UNAVAILABLE_002` | 没有知识库 Provider，或存储不可达 |
| `AI_UNEXPECTED_001` | 护栏抛出异常或返回失败 |

## 相关

- [AI](../guide/ai)
- [AI.OpenAI](./ai-openai)
- [AI.VectorData](./ai-vectordata)
- [Protocols.Mcp](./protocols-mcp)
- 规范：RFC-0022《AI：聊天、嵌入、工具与知识》
