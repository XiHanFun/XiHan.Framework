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

`OpenAIClient` 的网络超时只取 Provider 的 `TimeoutSeconds`（缺省 100 秒）；`Endpoint` 为空时连官方端点；`ApiKey` 为空时用占位凭据，供不校验密钥的本地端点使用。装配与启动不访问网络，客户端在第一次解析时创建并按 Provider 名缓存。启动时构建工具目录：操作派生或 `XiHan:AI:Tools:<操作 ID>` 覆盖的工具名不合法、与别的工具重名（`AI_VALIDATION_001`）即启动失败。再解析提示增强器：`XiHan:AI:Knowledge:PromptName` 指向的模板不存在（`AI_NOTFOUND_002`）或用了 `{context}`、`{question}` 以外的占位符（`AI_VALIDATION_001`）即启动失败，只装本特性（不装 `AIToolsFeature` 与知识库 Provider）时也一样。

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

Provider 写在 `XiHan:AI:Providers:<name>` 下，`Kind` 为 `openai`（必填，不写时启动失败）。配置在装配时读一次、不热更新，改了要重启应用：

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
| `Endpoint` | 绝对的 http(s) 地址；空表示官方端点，写错时启动失败 |
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

经代理或在测试里替换传输时，在 `Program.cs` 里给名为 `xihan.ai.openai` 的 `HttpClient` 配置处理器，本包从 `IHttpClientFactory` 取它发请求：

```csharp
var builder = WebApplication.CreateBuilder(args);

// 经公司代理访问模型端点；超时不在这里设，由 Provider 的 TimeoutSeconds 决定
builder.Services.AddHttpClient("xihan.ai.openai")
    .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler { Proxy = new WebProxy("http://proxy.internal:8080") });
```

`WebProxy` 在 `System.Net` 命名空间。

`AddHttpClient` 来自 `Microsoft.Extensions.Http`。聊天、工具与护栏的用法见 [AI](../guide/ai#聊天入口)。

## 行为与限制

- 容器里有 `IHttpClientFactory` 时（应用在任何地方调用过 `AddHttpClient`），本包就从它取名为 `xihan.ai.openai` 的客户端发请求，没有专门配置这个名字时取到的是工厂的缺省客户端，应用用 `ConfigureHttpClientDefaults` 给全部客户端加的处理器（如弹性处理器）同样作用于它。
- 超时只以 `TimeoutSeconds` 为准：取到的客户端的 `HttpClient.Timeout` 由本包置为不限，与 SDK 自己的传输一样，不会被缺省的 100 秒截断；在 `AddHttpClient` 里设的 `Timeout` 不生效。
- 端点返回错误或不可达记 `AI_UNAVAILABLE_001`（`reason` 只写异常类型，完整异常进日志），超时记 `AI_TIMEOUT_001`；这两项在 `IAIChat` 与知识库流水线里映射，直接从目录取客户端的调用拿到的是 SDK 的原始异常。
- 经 `IAIChat` 与知识库流水线的调用另有框架的超时令牌（同取 `TimeoutSeconds`，按容器的 `TimeProvider` 计时），覆盖整次调用，含函数调用循环；SDK 的 `NetworkTimeout` 照常作用于每次网络请求。两者同时触发、或超时与调用方取消同时发生时，SDK 报的是调用方的取消，框架按自己的令牌报 `AI_TIMEOUT_001`（[超时与取消](../guide/ai#超时与取消)）。
- 缓存、遥测等横切逻辑可以叠加在目录解析出的客户端上，用 Microsoft.Extensions.AI 自己的中间件。
- 通过 `AI.Conformance` 的 `AIProviderConformance`。

## 相关

- [AI](../guide/ai)
- [AI.Abstractions](./ai-abstractions)
- [AI.VectorData](./ai-vectordata)
- 规范：RFC-0022《AI：聊天、嵌入、工具与知识》
