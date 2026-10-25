# XiHan.Framework.AI.Abstractions

AI 契约（Ring 2，RFC-0022，ADR-0017）：以 `Microsoft.Extensions.AI` 的 `IChatClient` / `IEmbeddingGenerator` / `AITool` 为线格式，框架只做目录、护栏、提示词、工具与知识库。能力 `ai.chat@1`、`ai.embedding@1`（Provider：`AI.OpenAI`）、`ai.tools@1`（本包的 `AIToolsFeature` 默认提供）、`ai.knowledge@1`（Provider：`AI.VectorData`）。

| 类型 | 用途 |
| --- | --- |
| `AIOptions`、`AIProviderOptions` | `XiHan:AI`：`DefaultProvider`、`DefaultEmbeddingProvider`、`Providers:<name>`（`Kind`、`Endpoint`、`ApiKey`、`ChatModel`、`EmbeddingModel`、`EmbeddingDimensions`、`TimeoutSeconds`、`Properties`）、`Tools:<操作 ID>`、`Guardrails`、`Knowledge`、`Prompts:<name>` |
| `IAIProviderCatalog` | 按名字解析 `IChatClient` / `IEmbeddingGenerator<string, Embedding<float>>`，缓存到 `Invalidate`；Provider 包实现 `IChatClientFactory` / `IEmbeddingGeneratorFactory` |
| `IAIChat`、`AIChatRequest` | 提示词渲染 → 工具绑定 → 护栏 → 解析 Provider → 调用；`CompleteAsync` / `StreamAsync` |
| `IAIGuardrail`、`GuardrailVerdict`、`KeywordBlocklistGuardrail` | 输入护栏；内置关键词与提示注入护栏由 `XiHan:AI:Guardrails:Enabled` 开启 |
| `IAIPromptCatalog`、`AIPromptTemplate` | 提示词模板（`{name}` 占位符，`{{` / `}}` 转义），显式登记优先于配置 |
| `IAIToolCatalog`、`OperationAIFunction`、`AIToolNameConvention` | `AddAITool` 的工具 + `Expose.Ai` 的操作桥接成 `AIFunction`（名 `<resource>_<verb>`，描述取消息目录 `<操作 ID>.description`），`Bind(caller)` 后在新作用域经完整管道执行 |
| `IKnowledgeStore` | `ai.knowledge@1` 的 Provider 契约：按集合写入 / 按文档删除 / 向量搜索 |
| `IKnowledgeIngestor`、`IKnowledgeRetriever`、`IRagPromptAugmenter`、`IChunkingStrategy` | 切块 → 嵌入 → 写入；问题嵌入 → 搜索 → 最低得分；检索结果拼进提示 |
| `AIErrors`、`AIMessages` | `AI_*` 错误码与 `ai.*` 消息键（10 种区域） |

```csharp
[Operation("orders.list", Expose = Expose.Http | Expose.Ai)]
public sealed class ListOrders : IOperation<ListOrdersRequest, ListOrdersResponse> { … }

var reply = await chat.CompleteAsync(new AIChatRequest
{
    Messages = [new ChatMessage(ChatRole.User, "How many orders are pending?")],
    Prompt = "assistant",
    Tools = ["orders_list"],
}, ct);
```

错误：Provider 未配置或种类无工厂 → `AI_NOTFOUND_001`；提示词不存在 → `AI_NOTFOUND_002`；工具不存在 → `AI_NOTFOUND_003`；护栏拦截 → `AI_DENIED_001`；模型不可达 → `AI_UNAVAILABLE_001`，超时 → `AI_TIMEOUT_001`；没有知识库 Provider → `AI_UNAVAILABLE_002`；嵌入维度不一致 → `AI_VALIDATION_002`。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`Microsoft.Extensions.AI.Abstractions`。被 `AI.OpenAI`、`AI.VectorData`、`AI.Conformance`、`Profiles.Full` 引用。

## 状态

5.x P0：可用（Preview）。
