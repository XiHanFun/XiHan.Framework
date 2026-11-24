# XiHan.Framework.AI.Abstractions

AI 的抽象包：以 `Microsoft.Extensions.AI` 为线格式的 Provider 目录、带提示词 / 护栏 / 工具的聊天入口、把操作契约桥接成模型工具的目录，以及知识库流水线与存储契约。默认提供进程内的工具目录 `ai.tools@1`。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.AI.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 提供 `ai.tools@1`（`AIToolsFeature`，`role: default`）；定义 `ai.chat@1`、`ai.embedding@1`、`ai.knowledge@1`；`AIToolsFeature` 依赖 `kernel.hosting@1` |
| 特性入口 | `AIToolsFeature`（清单 `xihan.ai.tools`，`IHostedFeature`） |
| 配置节 | `XiHan:AI` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`；`Microsoft.Extensions.AI.Abstractions`，`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions`、`Microsoft.Extensions.Logging.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

公开签名直接使用 `IChatClient`、`IEmbeddingGenerator<string, Embedding<float>>`、`ChatMessage`、`AITool` 等 `Microsoft.Extensions.AI` 类型，不复制一套模型接口（ADR-0017）。本包提供：

- Provider 目录 `IAIProviderCatalog`：按名字解析客户端并缓存，给出默认聊天与嵌入 Provider 名（`DefaultProvider`、`DefaultEmbeddingProvider`）；Provider 包实现 `IChatClientFactory` / `IEmbeddingGeneratorFactory`。
- 聊天入口 `IAIChat`：提示词渲染 → 工具绑定 → 护栏 → 解析 Provider（没写名字时问目录的默认名）→ 调用。
- 提示词目录 `IAIPromptCatalog` 与模板 `AIPromptTemplate`。
- 护栏 `IAIGuardrail` 与内置的 `KeywordBlocklistGuardrail`。
- 工具目录 `IAIToolCatalog`：`AddAITool` 的工具加上 `Expose.Ai` 的操作，后者在新作用域经完整管道执行。
- 知识库流水线：`IKnowledgeIngestor`、`IKnowledgeRetriever`、`IRagPromptAugmenter`、`IChunkingStrategy`，以及 Provider 契约 `IKnowledgeStore`。

`AddAI(configuration)` 幂等地登记以上全部服务、选项与消息目录 `ai`；`AIToolsFeature`、`OpenAIFeature`、`VectorDataKnowledgeFeature` 都调用它。`AIToolsFeature` 与 `OpenAIFeature` 在启动时构建工具目录，工具名不合法或重名即启动失败（只装 `OpenAIFeature` 也一样）；它与 `OpenAIFeature`、`VectorDataKnowledgeFeature` 启动时都解析提示增强器，知识库配置引用的提示词找不到或渲染不了即启动失败。用法见 [AI](/ai/chat)。

## 何时使用

业务代码只依赖本包的接口；模型由 [AI.OpenAI](./ai-openai) 提供，知识库存储由 [AI.VectorData](./ai-vectordata) 提供。接入其他模型服务时，在自己的 Provider 包里实现两个工厂接口，或者直接用 Microsoft.Extensions.AI 生态里已有的 `IChatClient` 实现包一层工厂。

## 安装与启用

```bash
dotnet add package XiHan.Framework.AI.Abstractions --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[环境与安装](/getting-started/installation)。`Profiles.Full` 已装配 `AIToolsFeature`；其他组合里 `UseFeature<AIToolsFeature>()`。

## 配置

`XiHan:AI` 下的键（装配时读一次，不热更新，改了要重启应用）：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `DefaultProvider` | string | 只有一个 Provider 时取它 | 框架自带目录的默认聊天 Provider；应用替换了目录时不起作用 |
| `DefaultEmbeddingProvider` | string | 同 `DefaultProvider` | 框架自带目录的默认嵌入 Provider |
| `Providers:<name>:Kind` | string | 必填 | 种类，转小写后与工厂的 `Kind` 匹配 |
| `Providers:<name>:Endpoint` | 绝对的 http(s) 地址 | 空 | 模型端点；不写或写空串表示 Provider 的官方端点 |
| `Providers:<name>:ApiKey` | string | 空 | API 密钥；空时由 Provider 决定 |
| `Providers:<name>:ChatModel` | string | — | 聊天模型，解析聊天客户端时必填 |
| `Providers:<name>:EmbeddingModel` | string | — | 嵌入模型，解析嵌入生成器时必填 |
| `Providers:<name>:EmbeddingDimensions` | int，至少 1 | 模型缺省维度 | 请求的嵌入维度 |
| `Providers:<name>:TimeoutSeconds` | 数值，大于 0 | 100 | Provider 调用的时限；`IAIChat` 与知识库流水线按它落成超时令牌，范围与归因见[超时与取消](/ai/chat#超时与取消) |
| `Providers:<name>:Properties:<key>` | string | — | 原样透传给工厂 |
| `Tools:<操作 ID>` | string | 按约定推导 | 覆盖操作工具名 |
| `Prompts:<name>:Template` | string | — | 提示词模板；也可以把模板直接写成 `Prompts:<name>` 的值 |
| `Prompts:<name>:Description` | string | — | 提示词说明 |
| `Guardrails:Enabled` | bool | `false` | 是否登记内置护栏 `KeywordBlocklistGuardrail` |
| `Guardrails:BlockedKeywords` | 数组 | 空 | 禁用关键词，不区分大小写的子串匹配 |
| `Guardrails:InjectionPatterns` | 数组 | 空 | 追加的提示注入正则，不区分大小写，单次匹配超时 250 毫秒 |
| `Guardrails:UseBuiltInInjectionHeuristics` | bool | `true` | 是否启用内置的中英文提示注入模式（如「ignore previous instructions」「忽略以上指令」） |
| `Knowledge:Collection` | string | `knowledge` | 默认集合；带租户的调用用 `<Collection>.<租户 ID>`，各租户互不可见，请求显式指明的集合原样使用（ADR-0210） |
| `Knowledge:Dimensions` | int，至少 1 | 1536 | 嵌入维度 |
| `Knowledge:ChunkSize` | int，至少 1 | 1000 | 切块大小，按 Unicode 码位计（代理对算一个） |
| `Knowledge:ChunkOverlap` | int，不小于 0 | 100 | 相邻块的重叠，按 Unicode 码位计 |
| `Knowledge:Top` | int，1 到 1000 | 5 | 默认检索条数 |
| `Knowledge:EmbeddingProvider` | string | 目录的默认嵌入 Provider | 知识库用的嵌入 Provider；请求写了 Provider 时以请求为准 |
| `Knowledge:PromptName` | string | 内置模板 | 提示增强用的模板名；启动时校验存在且只用 `{context}`、`{question}`，找不到或渲染不了即启动失败 |

`VectorData:HealthProbeIntervalSeconds` 是 [AI.VectorData](./ai-vectordata#配置) 自己的键。

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

值不合法时 `AIOptions.FromConfiguration` 抛出 `ArgumentException`，消息带出完整的配置键与原值，应用启动失败：Provider 缺 `Kind`、`Endpoint` 不是绝对的 http(s) 地址（例如漏了 `http://`；写错的端点不会退回官方端点、把 `ApiKey` 发出去）、数值项不是要求的整数或正数、布尔项不是 `true` / `false`、提示词缺 `Template`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `AICapabilities` | `Chat`、`Embedding`、`Tools`、`Knowledge` 与版本 1 |
| `AIOptions`、`Providers.AIProviderOptions` | 选项；`ResolveDefaultProvider()`、`ResolveDefaultEmbeddingProvider()` 按配置推出框架自带目录的默认名；`AIProviderOptions.DefaultTimeout` 为 100 秒 |
| `AIServiceCollectionExtensions` | `AddAI`、`AddAIChatClientFactory<T>`、`AddAIEmbeddingGeneratorFactory<T>`、`AddAIGuardrail<T>`、`AddAIPrompt`、`AddAITool` |
| `AIToolsFeature` | `ai.tools@1` 的特性入口 |
| `Providers.IAIProviderCatalog` | `Providers`、`DefaultProvider`、`DefaultEmbeddingProvider`、`Find`、`ResolveChatClient`、`ResolveEmbeddingGenerator`、`Invalidate`；框架的实现不公开，经接口从容器取用 |
| `Providers.IChatClientFactory`、`IEmbeddingGeneratorFactory` | Provider 包实现：`Kind` 与 `Create(AIProviderOptions)` |
| `Chat.IAIChat`、`Chat.AIChatRequest` | 聊天入口；`CompleteAsync`、`StreamAsync` |
| `Prompts.IAIPromptCatalog`、`Prompts.AIPromptTemplate` | 提示词目录与模板；`Render`、`Placeholders` |
| `Guardrails.IAIGuardrail`、`GuardrailInspection`、`GuardrailDirection`、`GuardrailVerdict`、`KeywordBlocklistGuardrail`、`AIGuardrailOptions` | 护栏：输入方向在调用模型前，输出方向在非流式调用拿到响应后 |
| `Tools.IAIToolCatalog`、`Tools.OperationAIFunction`、`Tools.AIToolNameConvention` | 工具目录、操作工具（`OperationId`）、命名约定（`Derive`、`DescriptionKeyOf`，上限 64） |
| `Knowledge.IKnowledgeIngestor`、`IKnowledgeRetriever`、`IRagPromptAugmenter`、`IChunkingStrategy` | 流水线；`KnowledgeIngestorExtensions` 提供单篇的 `IngestAsync` 与 `RemoveAsync` |
| `Knowledge.KnowledgeMetadataKeys` | 框架认识的块元数据键：`Title`（`title`）、`Source`（`source`），提示增强依次用它们写片段的来源，都没有时用文档 ID |
| `Knowledge.IKnowledgeStore` | Provider 契约：`UpsertAsync`、`DeleteAsync`、`TrimAsync`（按 `KnowledgeTrim` 列表删每篇文档里块序号不小于给定值的记录）、`SearchAsync`（收 `KnowledgeSearch`：向量、条数、过滤与最低得分，按 `Score` 降序） |
| `Knowledge.KnowledgeIngestRequest`、`KnowledgeQuery`、`KnowledgeRecord`、`RetrievedChunk`、`RetrievalFilter`、`ChunkingOptions`、`KnowledgeOptions` | 知识库的数据类型；`KnowledgeRecord.IdOf(documentId, chunkIndex)` 生成 `<documentId>#<chunkIndex>` |
| `AIIdentifiers` | 名字文法 `^[a-z0-9][a-z0-9._-]{0,63}$`，工具名 `^[A-Za-z0-9_-]{1,64}$` |
| `AIErrors`、`AIMessages` | 错误码与消息键 |

## 使用示例

登记一个提示词模板，同名时覆盖配置里的。写在应用特性的 `Configure` 里，特性与清单的写法见[第一个应用](/getting-started/first-app)：

```csharp
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Prompts;

public void Configure(FeatureContext context)
{
    // 与配置里同名的模板以这里为准；{{ 与 }} 输出字面的花括号
    context.Services.AddAIPrompt(new AIPromptTemplate(
        "order-summary",
        "Summarize order {orderId} for {audience}. Reply as JSON like {{\"summary\": \"<text>\"}}.",
        "Order summary for a given audience"));
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

聊天、护栏、工具与知识库的完整示例见 [AI](/ai/chat)。

## 行为与限制

- 目录按名字缓存实例；`Invalidate(names)` / `InvalidateAll()` 释放缓存（实现了 `IDisposable` 的实例被释放），下次解析重建。选项在装配时读一次，`Invalidate` 不重读配置。
- 默认名由目录给出（ADR-0110）：`IAIChat` 不写 Provider 时取 `DefaultProvider`，摄入与检索依次取请求的 `Provider`、`Knowledge:EmbeddingProvider`、`DefaultEmbeddingProvider`，都不读 `XiHan:AI` 的默认键。框架的 `AIProviderCatalog` 从配置推出默认名；应用替换的目录（如 Provider 存在库里）给出自己的。两个属性是同步的、不做 I/O，要查库的目录持内存快照。没有默认又没写名字时 `AI_NOTFOUND_001`（`ai.provider.default_missing` / `ai.provider.embedding_default_missing`）。
- `IAIChat` 是作用域服务；没有当前调用时工具绑定到本作用域新建的调用上下文。
- 护栏按登记顺序执行，只作用于 `IAIChat`；直接从目录取客户端的调用不经护栏。
- 工具目录在 `AIToolsFeature` 或 `OpenAIFeature` 启动时构建；稳定性为 `Internal` 的操作不进目录。
- 操作工具在新作用域、新调用上下文里经完整管道执行，trace-id、截止时间、Baggage 与安全上下文（主体与租户）复制自调用方，以调用方身份按契约授权；凭据不复制、不重新认证；失败以 `{"error","message"}` JSON 交给模型。
- 同一文档重新摄入先写新块、写入成功后再删多余的旧块：嵌入、维度校验或写入失败时旧文档照常可检索，存储没有事务，写入中途失败可能留下新旧混合的块，重试摄入即恢复一致；嵌入维度必须等于 `Knowledge:Dimensions`。
- 没有 `IKnowledgeStore` 时流水线返回 `AI_UNAVAILABLE_002`，聊天与工具不受影响。
- `IAIChat` 与知识库流水线把 Provider 的 `TimeoutSeconds` 按容器的 `TimeProvider` 落成超时令牌、与调用方令牌链接后交给 Provider：完整应答覆盖 Provider 的整次调用（含函数调用循环），嵌入是每次批调用，流式是等每个更新的时长（已定，ADR-0097：不设整条流的总时限）。超时令牌已触发即 `AI_TIMEOUT_001`（流里抛出 `TimeoutException`），只有调用方取消时抛出 `OperationCanceledException`；归因的细则见[超时与取消](/ai/chat#超时与取消)。

## 错误码

常量在 `AIErrors`。消息目录是 `ai`，键为 `ai.*`，内置 10 种区域的译文。

| 错误码 | 常量 | 何时出现 |
| --- | --- | --- |
| `AI_VALIDATION_001` | `RequestInvalid` | 请求不合法：Provider 缺 `ChatModel` / `EmbeddingModel`、提示词缺参数、摄入的文档 ID 为空、检索的问题为空、按租户派生的默认集合名超过 64 个字符、工具参数序列化不了（以 JSON 错误交给模型）；工具名不合法或重名、知识库提示词用了 `{context}`、`{question}` 以外的占位符（启动失败） |
| `AI_VALIDATION_002` | `DimensionsMismatch` | 嵌入维度与 `Knowledge:Dimensions` 不一致 |
| `AI_NOTFOUND_001` | `ProviderNotFound` | Provider 未配置，或它的种类没有登记工厂；没写 Provider 名而目录没有默认 |
| `AI_NOTFOUND_002` | `PromptNotFound` | 提示词模板不存在 |
| `AI_NOTFOUND_003` | `ToolNotFound` | 请求里的工具名不在目录里 |
| `AI_DENIED_001` | `GuardrailBlocked` | 被护栏拦截，细节带 `guardrail` 与 `reason` |
| `AI_TIMEOUT_001` | `ModelTimeout` | 模型调用超时：Provider 的超时令牌已触发（`TimeoutSeconds` 到了，调用方同时取消也一样），或 Provider 报告了自己的超时 |
| `AI_UNAVAILABLE_001` | `ModelUnavailable` | 模型端点不可达或返回错误 |
| `AI_UNAVAILABLE_002` | `KnowledgeUnavailable` | 没有知识库 Provider，或向量存储不可达 |
| `AI_UNEXPECTED_001` | `Unexpected` | 护栏抛出异常或返回失败 |

## 相关

- [AI](/ai/chat)
- [AI.OpenAI](./ai-openai)
- [AI.VectorData](./ai-vectordata)
- [Protocols.Mcp](./protocols-mcp)
- 规范：RFC-0022《AI：聊天、嵌入、工具与知识》
