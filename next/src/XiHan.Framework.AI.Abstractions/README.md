# XiHan.Framework.AI.Abstractions

AI 契约（Ring 2，RFC-0022，ADR-0017）：以 `Microsoft.Extensions.AI` 的 `IChatClient` / `IEmbeddingGenerator` / `AITool` 为线格式，框架只做目录、护栏、提示词、工具与知识库。能力 `ai.chat@1`、`ai.embedding@1`（Provider：`AI.OpenAI`）、`ai.tools@1`（本包的 `AIToolsFeature` 默认提供）、`ai.knowledge@1`（Provider：`AI.VectorData`）。

| 类型 | 用途 |
| --- | --- |
| `AIOptions`、`AIProviderOptions` | `XiHan:AI`：`DefaultProvider`、`DefaultEmbeddingProvider`、`Providers:<name>`（`Kind` 必填、`Endpoint`、`ApiKey`、`ChatModel`、`EmbeddingModel`、`EmbeddingDimensions`、`TimeoutSeconds`、`Properties`）、`Tools:<操作 ID>`、`Guardrails`、`Knowledge`、`Prompts:<name>`；值不合法即抛出 `ArgumentException`（启动失败） |
| `IAIProviderCatalog` | 按名字解析 `IChatClient` / `IEmbeddingGenerator<string, Embedding<float>>`，缓存到 `Invalidate`；Provider 包实现 `IChatClientFactory` / `IEmbeddingGeneratorFactory` |
| `IAIChat`、`AIChatRequest` | 提示词渲染 → 工具绑定 → 护栏 → 解析 Provider → 调用；`CompleteAsync` / `StreamAsync` |
| `IAIGuardrail`、`GuardrailVerdict`、`KeywordBlocklistGuardrail` | 输入护栏；内置关键词与提示注入护栏由 `XiHan:AI:Guardrails:Enabled` 开启 |
| `IAIPromptCatalog`、`AIPromptTemplate` | 提示词模板（`{name}` 占位符，`{{` / `}}` 转义），显式登记优先于配置 |
| `IAIToolCatalog`、`OperationAIFunction`、`AIToolNameConvention` | `AddAITool` 的工具 + `Expose.Ai` 的操作桥接成 `AIFunction`（名 `<resource>_<verb>`，描述取消息目录 `<操作 ID>.description`），`Bind(caller)` 后在新作用域经完整管道执行，以调用方身份（复制主体与租户，不复制凭据）按契约授权 |
| `IKnowledgeStore` | `ai.knowledge@1` 的 Provider 契约：按集合写入 / 按文档删除 / 按块序号裁剪 / 向量搜索；重新摄入先写新块、写入成功后再裁掉多余的旧块 |
| `IKnowledgeIngestor`、`IKnowledgeRetriever`、`IRagPromptAugmenter`、`IChunkingStrategy` | 切块 → 嵌入 → 写入；问题嵌入 → 搜索 → 最低得分；检索结果拼进提示（配置的模板在启动时校验：找不到或渲染不了即启动失败，不退回内置模板；增强本身不会失败） |
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

超时与取消（RFC-0022「超时与取消」，ADR-0095）：`IAIChat` 与知识库流水线调用 Provider 时，把它的 `TimeoutSeconds` 按容器的 `TimeProvider` 落成超时令牌、与调用方令牌链接后交给 Provider。完整应答的时限覆盖 Provider 的整次调用（含它自己的函数调用循环与其中的工具调用），嵌入是每次嵌入批调用，流式是等每个更新（含第一个）的时长、调用方处理更新的时间不算——这是已定的做法（ADR-0097）：不设整条流的总时限（会截断正常的长回答），也不退回不计时（卡住的连接没有上限），总时长由调用方的取消令牌或截止时间限制。归因只看令牌：超时令牌已触发即 `AI_TIMEOUT_001`（流里抛出 `TimeoutException`），不论调用方是否也取消、Provider 是抛出还是接住取消正常返回；只有调用方取消时抛出 `OperationCanceledException`，交给管道按截止令牌归因；两个都没触发时 Provider 抛出的取消视为它自己的传输超时。直接从目录解析的客户端不经这一层。

AI 配置不热更新（ADR-0074）：`XiHan:AI` 的全部配置——Provider、默认 Provider、提示词、护栏、工具名覆盖、知识库——在装配时读一次，改了要重启应用。框架不监听配置变化，`IAIProviderCatalog.Invalidate` 只丢弃缓存的客户端、用同一份选项重建；提示词目录、工具目录、护栏、知识库选项与提示增强器都在创建时取定。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`Microsoft.Extensions.AI.Abstractions`。被 `AI.OpenAI`、`AI.VectorData`、`AI.Conformance`、`Profiles.Full` 引用。

## 状态

5.x P0：可用（Preview）。
