# AI

本章讲 5.0 的 AI 端口：按名字解析模型客户端、带提示词与护栏的聊天入口、把操作契约变成模型可调用的工具，以及切块、嵌入、检索与提示增强的知识库流水线，适用于要在业务里接入大模型的应用。完整 API 见 [AI.Abstractions 包](../packages/ai-abstractions)。

## 以 Microsoft.Extensions.AI 为线格式

5.0 不另造模型接口。公开签名直接使用 `Microsoft.Extensions.AI.Abstractions`（10.11.0）的 `IChatClient`、`IEmbeddingGenerator<string, Embedding<float>>`、`ChatMessage`、`AITool`，以及 `Microsoft.Extensions.VectorData.Abstractions` 的 `VectorStore`；框架只做目录、提示词、护栏、工具与知识库（ADR-0017）。从目录解析出的客户端就是标准的 `IChatClient`，应用可以在上面叠加任何 Microsoft.Extensions.AI 中间件。

| 能力 | 含义 | 实现 |
| --- | --- | --- |
| `ai.chat@1` | 按名字解析 `IChatClient`；`IAIChat` 在其上叠加提示词、护栏与工具 | [AI.OpenAI](../packages/ai-openai) |
| `ai.embedding@1` | 按名字解析 `IEmbeddingGenerator<string, Embedding<float>>` | [AI.OpenAI](../packages/ai-openai) |
| `ai.tools@1` | 工具目录：显式登记的 `AITool` 与 `Expose.Ai` 的操作 | `AI.Abstractions` 的 `AIToolsFeature`（`role: default`，进 `Profiles.Full`） |
| `ai.knowledge@1` | 知识库存储 `IKnowledgeStore` | [AI.VectorData](../packages/ai-vectordata) |

模型端点与向量存储都是外部资源，`ai.chat`、`ai.embedding`、`ai.knowledge` 没有默认实现，框架不伪造；只有进程内的工具目录有默认实现。

## 启用

1. 引用 `XiHan.Framework.AI.OpenAI`；要知识库时再引用 `XiHan.Framework.AI.VectorData` 与所选向量库的连接器包。
2. 在 `XiHan:AI` 下配置 Provider、提示词与护栏。
3. 装配 `AIToolsFeature`（`Profiles.Full` 已包含）、`OpenAIFeature` 与业务特性。

```json
{
  "XiHan": {
    "AI": {
      "DefaultProvider": "local",
      "Providers": {
        "local": {
          "Kind": "openai",
          "Endpoint": "http://localhost:11434/v1",
          "ChatModel": "qwen2.5",
          "EmbeddingModel": "nomic-embed-text",
          "TimeoutSeconds": 300
        },
        "cloud": {
          "Kind": "openai",
          "ApiKey": "dev-openai-key",
          "ChatModel": "gpt-4o-mini"
        }
      },
      "Prompts": {
        "assistant": { "Template": "You are the order assistant of {company}. Answer briefly.", "Description": "Order assistant" }
      },
      "Guardrails": { "Enabled": true, "BlockedKeywords": [ "password" ] },
      "Knowledge": { "Dimensions": 768 }
    }
  }
}
```

```csharp
using MyApp.Assistants;
using MyApp.Orders;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.OpenAI;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<AIToolsFeature>()
    .UseFeature<OpenAIFeature>()
    .UseFeature<OrdersFeature>()
    .UseFeature<AssistantsFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanOperations();
app.Run();
```

`OrdersFeature` 登记订单操作，写法见[审计](./auditing#记录调用来源)一章；`AssistantsFeature` 与两个特性用到的操作在本章后面定义。`OpenAIFeature` 的装配与启动不访问网络，Provider 配置错误在第一次解析客户端或调用模型时才暴露；知识库配置引用的提示词则在启动时校验（见下文「知识库」）。

## Provider 目录

`IAIProviderCatalog` 按名字解析客户端：`ResolveChatClient(name)`、`ResolveEmbeddingGenerator(name)` 返回 `Result`，名字为空时取目录的默认 Provider。

默认名也由目录给出：`DefaultProvider` 是默认聊天 Provider，`DefaultEmbeddingProvider` 是默认嵌入 Provider，`null` 表示没有默认。两者是同步属性，读的是目录当前的状态，不做 I/O。框架自带的目录从配置推出它们：

- `DefaultProvider` 取配置的 `XiHan:AI:DefaultProvider`；没写且只配了一个 Provider 时就是它，否则没有默认。
- `DefaultEmbeddingProvider` 取配置的 `XiHan:AI:DefaultEmbeddingProvider`，没写时同 `DefaultProvider`。

`IAIChat` 与知识库在请求没写 Provider 时只问目录的这两个属性，不读配置。应用用自己的目录替换框架的实现（例如 Provider 存在库里、在界面上设默认）时，默认名随它走，不必在配置里再写一份。没有默认又没写名字时返回 `AI_NOTFOUND_001`，消息键聊天是 `ai.provider.default_missing`、嵌入是 `ai.provider.embedding_default_missing`。默认名不在 Provider 表里（例如配置写错）时，与其他未配置的名字一样报 `ai.provider.not_found`。

每个 Provider 有一个种类 `Kind`（必填，转成小写），由登记的 `IChatClientFactory` / `IEmbeddingGeneratorFactory` 的 `Kind` 匹配；Provider 包以 `AddAIChatClientFactory<T>()`、`AddAIEmbeddingGeneratorFactory<T>()` 登记工厂，同一种类登记两次以后者为准。Provider 名字、提示词名字与 `XiHan:AI:Knowledge:Collection` 的文法是 `^[a-z0-9][a-z0-9._-]{0,63}$`，不合法时构造选项抛出 `ArgumentException`。

解析结果按名字缓存，同名并发解析只建一个实例。`Invalidate(name)` 释放缓存的实例，下次解析重建；`name` 为空时清全部。选项在装配时从配置读一次，`Invalidate` 用的仍是这份选项，改了 `XiHan:AI:Providers` 要重启应用才生效。

直接从目录取 `IChatClient` 的调用不经提示词、护栏与工具绑定，需要这些时走 `IAIChat`。

## 聊天入口

`IAIChat` 是作用域服务，`CompleteAsync` 返回 `Result<ChatResponse>`，`StreamAsync` 返回 `Result<IAsyncEnumerable<ChatResponseUpdate>>`。一次请求按以下顺序准备：

1. 提示词：`Prompt` 不为空时按 `PromptArguments` 渲染模板，结果作为首条 `System` 消息。
2. 工具：`Tools` 列出的名字从工具目录绑定到当前调用；`Options.Tools` 会被绑定结果覆盖。
3. 护栏：`SkipGuardrails` 为假时按登记顺序检查全部消息，任一拦截即返回。
4. Provider：`Provider` 为空取目录的 `DefaultProvider`（没有即 `AI_NOTFOUND_001`），解析客户端。
5. 调用模型。

| `AIChatRequest` 成员 | 说明 |
| --- | --- |
| `Messages` | 必填，对话消息 |
| `Provider` | Provider 名，空取目录的默认聊天 Provider |
| `Prompt`、`PromptArguments` | 提示词模板名与参数 |
| `Tools` | 要带上的工具名（目录里的名字），空表示不带工具 |
| `Options` | `ChatOptions`，温度、最大令牌数等原样传给模型 |
| `SkipGuardrails` | 跳过护栏，缺省 `false` |

下面的操作把用户问题交给模型，并允许模型调用 `orders_list` 工具：

```csharp
using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace MyApp.Assistants;

public sealed record AskRequest(string Question);

public sealed record AskResponse(string Answer);

[Operation("assistants.ask", Expose = Expose.Http)]
public sealed class AskAssistant(IAIChat chat) : IOperation<AskRequest, AskResponse>
{
    public async ValueTask<Result<AskResponse>> HandleAsync(AskRequest request, InvocationContext context)
    {
        var reply = await chat.CompleteAsync(new AIChatRequest
        {
            Messages = [new ChatMessage(ChatRole.User, request.Question)],
            Prompt = "assistant",
            PromptArguments = new Dictionary<string, object?> { ["company"] = "Acme" },
            Tools = ["orders_list"],
        }, context.CancellationToken);

        // 护栏拦截、模型不可达、超时都以 Result 返回，由宿主翻译成调用方语言
        return reply.Map(static response => new AskResponse(response.Text));
    }
}
```

`StreamAsync` 在返回流之前完成提示词、工具、护栏与 Provider 解析，这些失败以 `Result` 返回；流开始后模型端点的异常在枚举时抛出。模型端点不可达或返回错误记 `AI_UNAVAILABLE_001`，超时记 `AI_TIMEOUT_001`。

### 超时与取消

`IAIChat` 与知识库流水线调用 Provider 时，把这个 Provider 的 `TimeoutSeconds` 按容器的 `TimeProvider` 落成超时令牌，与调用方的取消令牌链接后交给 Provider：

| 调用 | 时限的范围 |
| --- | --- |
| `CompleteAsync` | 从调用 Provider 起到它返回，含 Provider 自己的函数调用循环与其中的工具调用 |
| 摄入、检索 | 每一次嵌入批调用 |
| `StreamAsync` | 等每个更新（含第一个）的时长：只在等 Provider 时计时，调用方处理更新的时间不算，流的总时长不限 |

流式按每次更新计时是已定的做法（ADR-0097）：不改成整条流的总时限——那会把正常输出的长回答、调用方处理得慢的流截断；也不退回不计时——卡住的连接就没有上限，流里的超时也没有归因。要限制一次对话的总时长，给 `StreamAsync` 传自己的取消令牌，或给调用设截止时间（只有调用方取消时枚举抛出 `OperationCanceledException`）。

归因只看令牌，Provider 抛出与接住取消后正常返回是同一个回答：

| 触发的令牌 | `CompleteAsync`、摄入、检索 | `StreamAsync` 的枚举 |
| --- | --- | --- |
| Provider 的超时令牌（调用方是否也取消不论） | `AI_TIMEOUT_001` | 抛出 `TimeoutException` |
| 只有调用方取消 | 抛出 `OperationCanceledException`，经管道时按调用的截止时间归因（截止时间没到即 `KERNEL_CANCELLED_001`） | 抛出 `OperationCanceledException` |
| 都没有触发，Provider 却抛出取消或 `TimeoutException`（它自己的传输超时） | `AI_TIMEOUT_001` | 抛出 `TimeoutException` |

Provider 接住取消、交回截断的应答、条数不全的嵌入或提前结束的流时，框架按触发的令牌改报超时或取消，不把截断的结果当作成功。直接从目录解析的客户端不经这一层。完整应答的时限覆盖整个函数调用循环，工具调用多、总时长可能超过 `TimeoutSeconds` 的应用要调大它。

## 提示词模板

`AIPromptTemplate(name, template, description)` 的占位符写成 `{name}`，连写两个左花括号（或两个右花括号）输出一个字面的花括号，示例见 [AI.Abstractions 包](../packages/ai-abstractions#使用示例)。`Render(arguments)` 按不变文化把值转成字符串，`null` 为空串；缺参数返回 `AI_VALIDATION_001`。参数值原样拼入，不会再被当作模板解析。

提示词目录合并两处来源：配置 `XiHan:AI:Prompts:<name>:Template` 与 `:Description`（也可以把模板直接写成 `XiHan:AI:Prompts:<name>` 的值），以及 `AddAIPrompt(template)` 的显式登记；同名时显式登记优先。

## 护栏

护栏实现 `IAIGuardrail`，返回 `GuardrailVerdict.Allow()` 或 `GuardrailVerdict.Block(reasonKey, reasonArguments)`。原因是消息键加参数，`IAIChat` 拦截时返回 `AI_DENIED_001`，错误细节带 `guardrail`（护栏名）与 `reason`（按当前区域渲染的原因）。护栏抛出异常或返回失败时记 `AI_UNEXPECTED_001`。

```csharp
using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Guardrails;
using XiHan.Framework.Primitives;

namespace MyApp.Assistants;

public sealed class QuestionLengthGuardrail : IAIGuardrail
{
    // 应用自己消息目录里的键，模板如 "The question is longer than {limit} characters."
    public const string TooLong = "assistants.question.too_long";

    private const int Limit = 4000;

    public string Name => "question-length";

    public ValueTask<Result<GuardrailVerdict>> InspectAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var tooLong = messages.Any(static message => message.Role == ChatRole.User && message.Text.Length > Limit);
        var verdict = tooLong ? GuardrailVerdict.Block(TooLong, MessageArguments.Of(("limit", Limit))) : GuardrailVerdict.Allow();
        return ValueTask.FromResult(Result.Success(verdict));
    }
}
```

护栏以 `AddAIGuardrail<T>()` 登记，按登记顺序执行，收到的是包括渲染后系统提示在内的全部消息。内置的 `KeywordBlocklistGuardrail`（名字 `keyword-blocklist`）在 `XiHan:AI:Guardrails:Enabled` 为 `true` 时登记，只检查 `User` 消息：

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `XiHan:AI:Guardrails:Enabled` | `false` | 是否登记内置护栏 |
| `XiHan:AI:Guardrails:BlockedKeywords` | 空 | 禁用关键词，不区分大小写的子串匹配 |
| `XiHan:AI:Guardrails:InjectionPatterns` | 空 | 追加的提示注入正则，不区分大小写，单次匹配超时 250 毫秒 |
| `XiHan:AI:Guardrails:UseBuiltInInjectionHeuristics` | `true` | 是否启用内置的中英文提示注入模式（如「ignore previous instructions」「忽略以上指令」） |

## 工具

工具目录 `IAIToolCatalog` 合并两处来源：`AddAITool(AITool)` 显式登记的工具原样返回；登记表里 `Expose` 含 `Ai`、稳定性不是 `Stability.Internal` 的操作，各桥接成一个 `OperationAIFunction`。`All()` 与 `Find(name)` 返回声明（名字、描述、输入与输出 Schema），`Bind(caller)` 返回绑定到调用方上下文、可以执行的工具；`IAIChat` 会自动绑定到当前调用。

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace MyApp.Orders;

public sealed record ListOrdersRequest(string? Status);

public sealed record OrderSummary(string Id, string Status, decimal Amount);

public sealed record OrderList(IReadOnlyList<OrderSummary> Orders);

// Expose.Ai：工具目录把它桥接成名为 orders_list 的模型工具
[Operation("orders.list", Expose = Expose.Http | Expose.Ai)]
public sealed class ListOrders : IOperation<ListOrdersRequest, OrderList>
{
    private static readonly OrderSummary[] _orders = [new("o-1", "pending", 12.5m), new("o-2", "shipped", 99m)];

    public ValueTask<Result<OrderList>> HandleAsync(ListOrdersRequest request, InvocationContext context)
    {
        var matched = _orders.Where(order => request.Status is null || order.Status == request.Status).ToList();
        return ValueTask.FromResult(Result.Success(new OrderList(matched)));
    }
}
```

`OrdersFeature` 以 `context.AddGeneratedOperations()` 登记它，写法同[审计](./auditing#记录调用来源)一章；三个请求与响应类型登记在项目的 `JsonSerializerContext` 里。操作工具的规则：

| 项 | 规则 |
| --- | --- |
| 名字 | `<resource>_<verb>`，`-` 与 `.` 换成 `_`；超过 64 个字符时取前 55 个字符加 `_` 与 SHA-256 的前 8 位十六进制；`XiHan:AI:Tools:<操作 ID>` 覆盖 |
| 名字校验 | 必须匹配 `^[A-Za-z0-9_-]{1,64}$`，不合法或重名时启动失败（`AI_VALIDATION_001`）：`AIToolsFeature` 与 `OpenAIFeature` 启动时都构建工具目录，只装其中任何一个也一样 |
| 描述 | 消息目录里的 `<操作 ID>.description`（ID 里的 `-` 换成 `_`），按调用方区域翻译；没有时为空 |
| 输入 Schema | 操作的请求 Schema，没有时 `{"type":"object"}` |
| 输出 Schema | 操作的响应 Schema |

工具被调用时，参数序列化成请求 JSON，在新作用域、新 `InvocationContext` 里经完整管道执行：trace-id、截止时间、Baggage 与安全上下文（主体与租户）复制自调用方，Validate、Authorize、Transaction 等阶段照常生效；新上下文不带调用来源，`GetOrigin()` 为无值。成功返回响应 JSON；失败返回 `{"error":"<错误码>","message":"<按调用方区域翻译的文案>"}` 交给模型处理，不抛异常。`AI.OpenAI` 的聊天客户端带函数调用循环，模型发起的工具调用在客户端里自动执行。

::: tip 工具以调用方身份执行
工具调用沿用调用方此刻的主体与租户：调用方已在自己的管道里认证过，凭据不复制，Authenticate 阶段没有凭据可认证即保留这个主体。Authorize 阶段以调用方主体按契约判定，契约声明了 `Permission` 的操作，调用方持有该权限才放行；没有权限得到 `SECURITY_DENIED_004`、匿名调用得到 `SECURITY_DENIED_001`，错误以 JSON 交给模型。模型能做的事不超过发起聊天的用户本人能做的事。
:::

显式工具用 `AIFunctionFactory` 创建，在特性里登记，护栏也在这里登记：

```csharp
using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace MyApp.Assistants;

public sealed class AssistantsFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.myapp.assistants",
        Package = "MyApp",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(AICapabilities.Chat, AICapabilities.Version),
            new CapabilityRequirement(AICapabilities.Tools, AICapabilities.Version),
        ],
        Entry = typeof(AssistantsFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        context.Services.AddAIGuardrail<QuestionLengthGuardrail>();
        context.Services.AddAITool(AIFunctionFactory.Create(static () => DateTimeOffset.UtcNow.ToString("O"), "clock_now", "Current UTC time in ISO 8601."));
    }
}
```

同一份操作契约暴露成 MCP tool 由 `Protocols.Mcp` 负责，见 [gRPC、MCP 与 CloudEvents](./protocols)。

## 知识库

知识库流水线是「切块 → 嵌入 → 写入存储 → 检索 → 提示增强」。切块、嵌入、检索与增强在 `AI.Abstractions` 里，与存储无关；Provider 只实现 `IKnowledgeStore` 的写入、按文档删除与向量搜索。

| 服务 | 作用 |
| --- | --- |
| `IKnowledgeIngestor` | `IngestAsync(requests)` 批量摄入，每篇文档一个 `Result<int>`（写入的块数）；`RemoveAsync(collection, documentIds)` 按文档删除 |
| `IChunkingStrategy` | 内置 `FixedWindowChunkingStrategy`：按字符的固定窗口，重叠裁到 `[0, MaxChunkSize − 1]`，换行统一为 `\n`，空白块丢弃 |
| `IKnowledgeRetriever` | `RetrieveAsync(query)`：问题嵌入 → 向量搜索 → 按 `MinScore` 过滤 |
| `IRagPromptAugmenter` | `Augment(question, chunks)`：把检索结果编号拼进提示词，返回 `string`（模板在启动时校验过，增强本身不会失败） |

```csharp
using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.Primitives;

namespace MyApp.Assistants;

public sealed class FaqKnowledge(IKnowledgeIngestor ingestor, IKnowledgeRetriever retriever, IRagPromptAugmenter augmenter, IAIChat chat)
{
    public async ValueTask<IReadOnlyList<Result<int>>> ImportAsync(IReadOnlyDictionary<string, string> documents, CancellationToken cancellationToken)
    {
        // 批量摄入：每篇文档一个结果（写入的块数），同一文档重新摄入先写新块、再删多余的旧块
        var requests = documents
            .Select(static document => new KnowledgeIngestRequest
            {
                DocumentId = document.Key,
                Text = document.Value,
                Metadata = new Dictionary<string, string> { ["source"] = "faq" },
            })
            .ToList();
        return await ingestor.IngestAsync(requests, cancellationToken);
    }

    public async ValueTask<Result<string>> AnswerAsync(string question, CancellationToken cancellationToken)
    {
        var found = await retriever.RetrieveAsync(new KnowledgeQuery { Text = question, Top = 3, MinScore = 0.3 }, cancellationToken);
        if (!found.TryGetValue(out var chunks))
        {
            return found.Error!;
        }

        var prompt = augmenter.Augment(question, chunks);
        var reply = await chat.CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, prompt)] }, cancellationToken);
        return reply.Map(static response => response.Text);
    }
}
```

`FaqKnowledge` 以 `builder.Services.AddScoped<FaqKnowledge>()` 登记（`IAIChat` 是作用域服务）。流水线的规则：

- 记录 ID 是 `<documentId>#<chunkIndex>`。同一文档重新摄入先写后删：新块全部嵌入、校验维度并写入成功后，才删掉旧版本多出来的块（块序号不小于新块数的），同 ID 的块由写入覆盖。嵌入、维度校验或写入失败时不删任何旧块，旧文档照常可检索；写入中途失败最多留下新旧混合的块，按返回的错误重试摄入即恢复一致。删多余旧块这一步失败时返回该错误，新块已经在库里。新文本切不出块时整篇删除，返回 0。
- 每个嵌入向量的长度必须等于 `XiHan:AI:Knowledge:Dimensions`，不一致返回 `AI_VALIDATION_002`。换嵌入模型时一并改维度，并换一个集合。
- 摄入与检索的嵌入 Provider 依次取请求的 `Provider`、`XiHan:AI:Knowledge:EmbeddingProvider`、目录的 `DefaultEmbeddingProvider`，都没有时返回 `AI_NOTFOUND_001`（`ai.provider.embedding_default_missing`）。
- 检索的 `Top` 缺省取 `XiHan:AI:Knowledge:Top`；`Filter` 的 `DocumentIds` 与 `Metadata`（键值相等）取交集；`MinScore` 在存储返回后过滤。
- `Augment` 在检索结果为空时原样返回问题。配置了 `XiHan:AI:Knowledge:PromptName` 就用它指向的模板（占位符 `{context}`、`{question}`，配置里的或 `AddAIPrompt` 登记的都行），没配置时用内置英文模板。模板在启动时校验：`AIToolsFeature`、`VectorDataKnowledgeFeature` 与 `OpenAIFeature` 启动时都解析增强器（只装其中任何一个也一样），模板不存在（`AI_NOTFOUND_002`）或引用了这两个以外的占位符、用代表性参数试渲染失败（`AI_VALIDATION_001`）即启动失败，不会悄悄换成内置模板。校验之后增强只用这份模板、只给这两个参数，不会再失败，所以返回 `string`。
- 没有装配 `ai.knowledge` Provider 时，摄入、删除与检索都返回 `AI_UNAVAILABLE_002`。

`AI.VectorData` 把 `IKnowledgeStore` 映射到应用登记的 `Microsoft.Extensions.VectorData` 的 `VectorStore`，任何连接器都行；容器里没有 `VectorStore` 时启动失败。集合定义、距离函数与过滤的下推方式见 [AI.VectorData 包](../packages/ai-vectordata)。

`AI.VectorData` 自带健康项 `ai.vectordata.store`，应用不必另写向量库的健康检查：

- 启动时同步探一次，之后每隔 `XiHan:AI:VectorData:HealthProbeIntervalSeconds`（缺省 15 秒，也是单次探测的超时）在后台重探。
- 探针只问默认集合（`XiHan:AI:Knowledge:Collection`）在不在，存在与否都算连得上；不列集合、不建集合、不读写记录。
- 向量库应答即 Ready。连接器抛出时 Degraded，错误码 `AI_UNAVAILABLE_002`，与检索在这时返回的码相同；探测超时也是 Degraded，错误码 `KERNEL_UNAVAILABLE_005`。
- 条目 Degraded 时应用照常启动、照常服务，`/health/ready` 仍通过，恢复后条目回到 Ready。条目不带数据与说明，不写端点或连接信息。汇总见[生命周期与健康](./lifecycle#健康条目)。

## 配置

AI 配置不热更新：`XiHan:AI` 下的全部键在装配时读一次，改了要重启应用。框架不监听配置变化，`Invalidate` 只丢弃缓存的客户端、按同一份选项重建；提示词目录、工具目录、护栏、知识库选项与提示增强器都在创建时取定。这样做是因为这些配置在启动时校验（工具名、知识库提示词）并被多个单例取用，热更新只会部分生效——例如换了嵌入模型而向量集合的维度没变——AI 配置的改动通常本就伴随部署（ADR-0074）。

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:AI:DefaultProvider` | string | 只有一个 Provider 时取它 | 框架自带目录的默认聊天 Provider；替换了目录时不起作用 |
| `XiHan:AI:DefaultEmbeddingProvider` | string | 同 `DefaultProvider` | 框架自带目录的默认嵌入 Provider |
| `XiHan:AI:Providers:<name>:Kind` | string | 必填 | Provider 种类，转成小写后匹配工厂 |
| `XiHan:AI:Providers:<name>:Endpoint` | 绝对的 http(s) 地址 | 空，即 Provider 的官方端点 | 模型端点 |
| `XiHan:AI:Providers:<name>:ApiKey` | string | 空 | API 密钥；空时由 Provider 决定 |
| `XiHan:AI:Providers:<name>:ChatModel` | string | — | 聊天模型，解析聊天客户端时必填 |
| `XiHan:AI:Providers:<name>:EmbeddingModel` | string | — | 嵌入模型，解析嵌入生成器时必填 |
| `XiHan:AI:Providers:<name>:EmbeddingDimensions` | int，至少 1 | 模型缺省维度 | 请求的嵌入维度 |
| `XiHan:AI:Providers:<name>:TimeoutSeconds` | 数值，大于 0 | 100 | Provider 调用的时限，范围与归因见[超时与取消](#超时与取消) |
| `XiHan:AI:Providers:<name>:Properties:<key>` | string | — | 原样透传给工厂 |
| `XiHan:AI:Tools:<操作 ID>` | string | 按约定推导 | 覆盖操作工具名 |
| `XiHan:AI:Prompts:<name>:Template` | string | — | 提示词模板 |
| `XiHan:AI:Prompts:<name>:Description` | string | — | 提示词说明 |
| `XiHan:AI:Guardrails:*` | — | — | 见[护栏](#护栏) |
| `XiHan:AI:Knowledge:Collection` | string | `knowledge` | 默认集合 |
| `XiHan:AI:Knowledge:Dimensions` | int，至少 1 | 1536 | 嵌入维度 |
| `XiHan:AI:Knowledge:ChunkSize` | int，至少 1 | 1000 | 切块大小（字符） |
| `XiHan:AI:Knowledge:ChunkOverlap` | int，不小于 0 | 100 | 相邻块的重叠（字符） |
| `XiHan:AI:Knowledge:Top` | int，至少 1 | 5 | 默认检索条数 |
| `XiHan:AI:Knowledge:EmbeddingProvider` | string | 目录的默认嵌入 Provider | 知识库用的嵌入 Provider；请求写了 Provider 时以请求为准 |
| `XiHan:AI:Knowledge:PromptName` | string | 内置模板 | 提示增强用的模板名；启动时校验存在且只用 `{context}`、`{question}` |
| `XiHan:AI:VectorData:HealthProbeIntervalSeconds` | 有限的数值，大于 0 | 15 | `AI.VectorData` 健康项的重探间隔，也是单次探测的超时 |

::: warning 配置值写错时启动失败
读取 `XiHan:AI` 时值不合法即抛出 `ArgumentException`，应用启动失败，异常消息带出完整的配置键与原值：Provider 缺 `Kind`；`Endpoint` 不是绝对的 http(s) 地址（例如漏了 `http://`，写错的端点不会退回官方端点、把 `ApiKey` 发出去）；`EmbeddingDimensions` 与知识库的数值不是要求的整数；`TimeoutSeconds` 不是正数；护栏的两个开关不是 `true` / `false`；提示词缺 `Template`。`Endpoint` 不写或写空串表示官方端点。
:::

## 错误码

| 错误码 | 何时出现 |
| --- | --- |
| `AI_VALIDATION_001` | 请求不合法：Provider 缺 `ChatModel` / `EmbeddingModel`、提示词缺参数、摄入的文档 ID 为空、检索的问题为空、工具参数序列化不了（交给模型的 JSON 错误）、工具名不合法或重名（启动失败） |
| `AI_VALIDATION_002` | 嵌入维度与 `XiHan:AI:Knowledge:Dimensions` 不一致 |
| `AI_NOTFOUND_001` | Provider 未配置，或它的种类没有登记工厂；没写 Provider 名而目录没有默认 |
| `AI_NOTFOUND_002` | 提示词模板不存在 |
| `AI_NOTFOUND_003` | 请求里的工具名不在目录里 |
| `AI_DENIED_001` | 被护栏拦截，细节带 `guardrail` 与 `reason` |
| `AI_TIMEOUT_001` | 模型调用超时：Provider 的 `TimeoutSeconds` 到了（调用方同时取消也一样），或 Provider 报告了自己的超时 |
| `AI_UNAVAILABLE_001` | 模型端点不可达或返回错误 |
| `AI_UNAVAILABLE_002` | 没有知识库 Provider，或向量存储不可达；`AI.VectorData` 启动时找不到 `VectorStore` 也以此码失败；健康项 `ai.vectordata.store` 探不到向量库时也是它 |
| `AI_UNEXPECTED_001` | 护栏抛出异常或返回失败 |

消息目录是 `ai`，键为 `ai.*`，内置 10 种区域的译文。

## 与其他能力的关系

- 执行管道：工具调用是一次完整的管道调用，审计、事务、校验都照常生效，审计事件与发起聊天的调用共享 trace-id，见[执行管道](./pipeline)。
- 操作契约：`Expose.Ai` 是 `Expose` 的一个标志位，`Expose.All` 包含它，见[操作契约](./operations)。
- 测试：`AI.Conformance` 提供 `KnowledgeStoreConformance`、`AIProviderConformance` 与确定性的 `HashingEmbeddingGenerator`，测试不依赖外部模型，见[测试与一致性套件](./testing)。自写目录的应用继承 `AIProviderConformance`，实现 `StartAsync`（被测 Provider 是默认）与 `StartWithoutDefaultAsync`（没有默认），能把聊天与嵌入的默认分开时再覆写 `StartWithSeparateDefaultsAsync`。
- 4.x：4.x 的 Agent 工厂不迁移，等 `Microsoft.Agents.AI` 稳定后另立规范；其余对照见[从 4.x 迁移](./upgrade)。

## 相关

- [AI.Abstractions 包](../packages/ai-abstractions)
- [AI.OpenAI 包](../packages/ai-openai)
- [AI.VectorData 包](../packages/ai-vectordata)
- [gRPC、MCP 与 CloudEvents](./protocols)
- 规范：RFC-0022《AI：聊天、嵌入、工具与知识》
