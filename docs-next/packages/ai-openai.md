# XiHan.Framework.AI.OpenAI

AI 的 OpenAI 兼容 Provider：`Kind = openai` 的 Provider 都由本包按配置创建聊天客户端与嵌入生成器，覆盖 OpenAI 官方端点与 Ollama、vLLM、LM Studio 等 OpenAI 兼容端点。提供 `ai.chat@1` 与 `ai.embedding@1`。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.AI.OpenAI` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `ai.chat@1`、`ai.embedding@1`（`role: provider`）；依赖 `kernel.hosting@1` |
| 特性入口 | `OpenAIFeature`（清单 `xihan.ai.openai`） |
| 配置节 | `XiHan:AI` |
| 依赖 | `XiHan.Framework.AI.Abstractions`；`Microsoft.Extensions.AI` 10.11.0、`Microsoft.Extensions.AI.OpenAI` 10.11.0、`Microsoft.Extensions.Http` 10.0.13 |
| AOT | 不兼容（OpenAI SDK 未声明 AOT 兼容，csproj 设 `IsAotCompatible=false`） |
| 稳定性 | Preview |

## 概述

`OpenAIFeature` 调用 `AddAI(configuration)` 登记 AI 核心服务，再登记两个工厂：

- `OpenAIChatClientFactory`：`OpenAIClient` → `GetChatClient(ChatModel).AsIChatClient()`，再套上 Microsoft.Extensions.AI 的函数调用循环（`UseFunctionInvocation`），模型发起的工具调用在客户端里自动执行。
- `OpenAIEmbeddingGeneratorFactory`：`GetEmbeddingClient(EmbeddingModel).AsIEmbeddingGenerator(EmbeddingDimensions)`。

`OpenAIClient` 的网络超时取 Provider 的 `TimeoutSeconds`（缺省 100 秒）；`Endpoint` 为空时连官方端点；`ApiKey` 为空时用占位凭据，供不校验密钥的本地端点使用。装配与启动不访问网络，客户端在第一次解析时创建并按 Provider 名缓存。

## 何时使用

接 OpenAI 官方 API，或任何提供 OpenAI 兼容接口的模型服务（本地推理服务、兼容网关）。本包不进 `Profiles.Full`，因为它需要外部端点。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.AI.OpenAI" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.OpenAI;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<AIToolsFeature>()
    .UseFeature<OpenAIFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

## 配置

Provider 写在 `XiHan:AI:Providers:<name>` 下，`Kind` 为 `openai`（不写时缺省也是 `openai`）：

```json
{
  "XiHan": {
    "AI": {
      "DefaultProvider": "cloud",
      "Providers": {
        "cloud": {
          "Kind": "openai",
          "ApiKey": "dev-openai-key",
          "ChatModel": "gpt-4o-mini",
          "EmbeddingModel": "text-embedding-3-small",
          "EmbeddingDimensions": 1536
        },
        "local": {
          "Kind": "openai",
          "Endpoint": "http://localhost:11434/v1",
          "ChatModel": "qwen2.5",
          "TimeoutSeconds": 90
        }
      }
    }
  }
}
```

| 键 | 说明 |
| --- | --- |
| `Endpoint` | 绝对 URI；空表示官方端点 |
| `ApiKey` | 空时用占位凭据 `no-key` |
| `ChatModel` | 解析聊天客户端时必填，缺失返回 `AI_VALIDATION_001` |
| `EmbeddingModel` | 解析嵌入生成器时必填，缺失返回 `AI_VALIDATION_001` |
| `EmbeddingDimensions` | 请求的嵌入维度，空用模型缺省 |
| `TimeoutSeconds` | `OpenAIClientOptions.NetworkTimeout`，缺省 100 |

其余键（`DefaultProvider`、提示词、护栏、知识库）见 [AI.Abstractions](./ai-abstractions#配置)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `OpenAIFeature` | 能力包入口 |
| `OpenAIChatClientFactory` | `Kind` 为 `openai`（`KindName`）的聊天客户端工厂 |
| `OpenAIEmbeddingGeneratorFactory` | 同种类的嵌入生成器工厂 |

两个工厂都可以接收可选的 `IHttpClientFactory`；聊天工厂还接收可选的 `ILoggerFactory`，交给函数调用循环写日志。

## 使用示例

经代理或在测试里替换传输时，在 `Program.cs` 里登记名为 `xihan.ai.openai` 的 `HttpClient`，本包从 `IHttpClientFactory` 取它发请求：

```csharp
var builder = WebApplication.CreateBuilder(args);

// 工厂建出的 HttpClient 自带 100 秒的 Timeout，比 TimeoutSeconds 更早生效时调大它
builder.Services.AddHttpClient("xihan.ai.openai", static client => client.Timeout = TimeSpan.FromMinutes(5));
```

`AddHttpClient` 来自 `Microsoft.Extensions.Http`。聊天、工具与护栏的用法见 [AI](../guide/ai#聊天入口)。

## 行为与限制

- 容器里有 `IHttpClientFactory` 时（应用在任何地方调用过 `AddHttpClient`），本包就从它取名为 `xihan.ai.openai` 的客户端发请求，没有专门登记这个名字时取到的是缺省配置的客户端；`HttpClient.Timeout` 缺省 100 秒，会与 `TimeoutSeconds` 叠加生效，取较短者。
- 端点返回错误或不可达记 `AI_UNAVAILABLE_001`，超时记 `AI_TIMEOUT_001`；这两项在 `IAIChat` 里映射，直接从目录取客户端的调用拿到的是 SDK 的原始异常。
- 缓存、遥测等横切逻辑可以叠加在目录解析出的客户端上，用 Microsoft.Extensions.AI 自己的中间件。
- 通过 `AI.Conformance` 的 `AIProviderConformance`。

## 相关

- [AI](../guide/ai)
- [AI.Abstractions](./ai-abstractions)
- [AI.VectorData](./ai-vectordata)
- 规范：RFC-0022《AI：聊天、嵌入、工具与知识》
