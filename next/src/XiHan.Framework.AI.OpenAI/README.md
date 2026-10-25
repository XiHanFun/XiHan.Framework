# XiHan.Framework.AI.OpenAI

AI 的 OpenAI 兼容 Provider（Ring 3，RFC-0022）：提供 `ai.chat@1` 与 `ai.embedding@1`，`Kind = openai` 的 Provider 都由本包创建——OpenAI 官方、Azure OpenAI 兼容网关、Ollama / vLLM / LM Studio 等本地兼容端点。

| 类型 | 用途 |
| --- | --- |
| `OpenAIFeature` | 登记两个工厂并装配 AI 核心服务；装配与启动不触网 |
| `OpenAIChatClientFactory` | `GetChatClient(ChatModel).AsIChatClient()` + 函数调用循环（`UseFunctionInvocation`），工具调用自动执行 |
| `OpenAIEmbeddingGeneratorFactory` | `GetEmbeddingClient(EmbeddingModel).AsIEmbeddingGenerator(EmbeddingDimensions)` |

```json
{
  "XiHan": { "AI": {
    "DefaultProvider": "openai",
    "Providers": {
      "openai": { "Kind": "openai", "ApiKey": "sk-…", "ChatModel": "gpt-4o-mini", "EmbeddingModel": "text-embedding-3-small", "EmbeddingDimensions": 1536 },
      "local": { "Kind": "openai", "Endpoint": "http://localhost:11434/v1", "ChatModel": "qwen2.5", "TimeoutSeconds": 300 }
    }
  } }
}
```

`Endpoint` 为空用官方端点；`ApiKey` 为空用占位凭据（本地端点不校验）。注册了名为 `xihan.ai.openai` 的 `HttpClient`（`services.AddHttpClient("xihan.ai.openai")`）时经它发请求，测试与代理场景用。缺 `ChatModel` / `EmbeddingModel` → `AI_VALIDATION_001`。

## 依赖方向

依赖 `XiHan.Framework.AI.Abstractions`、`Microsoft.Extensions.AI`、`Microsoft.Extensions.AI.OpenAI`、`Microsoft.Extensions.Http`。不进 `Profiles.Full`（需要外部端点）。

## 状态

5.x P0：可用（Preview）。
