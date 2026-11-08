来源：https://framework.docs.xihanfun.com/ai/chat

# 模型与对话

这一页讲 AI 子系统的基础：框架为什么直接以 Microsoft.Extensions.AI 为线格式、怎样配置 OpenAI 兼容的 Provider 并按名字解析模型客户端、聊天入口 `IAIChat` 怎样把提示词、护栏与工具叠在模型调用之上、超时与取消怎样归因、配置与错误码。读完后，我们能在一个操作里把用户问题交给模型并把回答以 `Result` 返回。前置知识是[操作契约](/web/operations)、[结果与错误处理](/web/errors)与[配置模型与选项](/configuration/options)；工具与知识库在[下一页](/ai/tools)。

## 为什么需要

直接在业务代码里 `new` 一个模型 SDK 的客户端，很快会遇到几件重复的事：多个模型端点要按环境切换、系统提示词散落各处、用户输入要先过一遍拦截、调用要有超时又不能把截断的回答当成功、模型报错要翻译成调用方能理解的错误。框架不另造模型接口，公开签名直接用 `Microsoft.Extensions.AI` 的 `IChatClient`、`ChatMessage`、`AITool` 等类型，只在上面加目录、提示词、护栏、工具与知识库这几层，解析出的客户端仍是标准的 `IChatClient`，可以叠加任何 Microsoft.Extensions.AI 中间件。

## 以 Microsoft.Extensions.AI 为线格式

框架公开签名使用 `Microsoft.Extensions.AI.Abstractions` 的 `IChatClient`、`IEmbeddingGenerator<string, Embedding<float>>`、`ChatMessage`、`AITool`，以及 `Microsoft.Extensions.VectorData.Abstractions` 的 `VectorStore`；框架只做目录、提示词、护栏、工具与知识库。

| 能力 | 含义 | 实现 |
| --- | --- | --- |
| `ai.chat@1` | 按名字解析 `IChatClient`；`IAIChat` 在其上叠加提示词、护栏与工具 | [AI.OpenAI](/packages/ai-openai) |
| `ai.embedding@1` | 按名字解析 `IEmbeddingGenerator<string, Embedding<float>>` | [AI.OpenAI](/packages/ai-openai) |
| `ai.tools@1` | 工具目录：显式登记的 `AITool` 与 `Expose.Ai` 的操作 | `AI.Abstractions` 的 `AIToolsFeature`（`role: default`，进 `Profiles.Full`） |
| `ai.knowledge@1` | 知识库存储 `IKnowledgeStore` | [AI.VectorData](/packages/ai-vectordata) |

模型端点与向量存储都是外部资源，`ai.chat`、`ai.embedding`、`ai.knowledge` 没有默认实现，框架不伪造；只有进程内的工具目录有默认实现。

## 启用

1. 引用 `XiHan.Framework.AI.OpenAI`；要知识库时再引用 `XiHan.Framework.AI.VectorData` 与所选向量库的连接器包。
2. 在 `XiHan:AI` 下配置 Provider、提示词与护栏。
3. 装配 `AIToolsFeature`（`Profiles.Full` 已包含）、`OpenAIFeature` 与业务特性。

```bash
dotnet add package XiHan.Framework.AI.OpenAI --prerelease
```

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
      "Guardrails": { "Enabled": true, "BlockedKeywords": [ "password" ] }
    }
  }
}
```

`OpenAIFeature` 的装配与启动不访问网络，Provider 配置错误在第一次解析客户端或调用模型时才暴露；工具名与知识库引用的提示词则在启动时校验。`local` 指向本机 Ollama 的 OpenAI 兼容端点，`cloud` 不写 `Endpoint` 即官方端点。

## 最小示例

一个操作把用户问题交给模型，带上 `assistant` 提示词：

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
        }, context.CancellationToken);

        // 护栏拦截、模型不可达、超时都以 Result 返回，由宿主翻译成调用方语言
        return reply.Map(static response => new AskResponse(response.Text));
    }
}
```

```csharp
using MyApp.Assistants;
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
    .UseFeature<AssistantsFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanOperations();
app.Run();
```

`AssistantsFeature` 是登记这个操作的特性（`context.AddGeneratedOperations()`），完整写法在[下一页](/ai/tools#最小示例)；请求与响应类型登记到项目的 `JsonSerializerContext`。运行后：`POST /assistants/ask` 带 `{"question": "..."}` 返回 `{"answer": "..."}`，首条消息是渲染后的系统提示 `You are the order assistant of Acme. Answer briefly.`；问题里含 `password` 时得到 403 的 Problem Details，`code` 是 `AI_DENIED_001`、细节带 `guardrail: keyword-blocklist`；Ollama 没起来时得到 `AI_UNAVAILABLE_001`。

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
| `Tools` | 要带上的工具名（目录里的名字），空表示不带工具，见[工具与知识库](/ai/tools) |
| `Options` | `ChatOptions`，温度、最大令牌数等原样传给模型 |
| `SkipGuardrails` | 跳过护栏，缺省 `false` |

`StreamAsync` 在返回流之前完成提示词、工具、护栏与 Provider 解析，这些失败以 `Result` 返回；流开始后模型端点的异常在枚举时抛出。模型端点不可达或返回错误记 `AI_UNAVAILABLE_001`，`reason` 只写异常类型的全名，完整异常进日志（每次失败一条 Warning，带 Provider 名）；超时记 `AI_TIMEOUT_001`。

### 超时与取消

`IAIChat` 与知识库流水线调用 Provider 时，把这个 Provider 的 `TimeoutSeconds` 按容器的 `TimeProvider` 落成超时令牌，与调用方的取消令牌链接后交给 Provider：

| 调用 | 时限的范围 |
| --- | --- |
| `CompleteAsync` | 从调用 Provider 起到它返回，含 Provider 自己的函数调用循环与其中的工具调用 |
| 摄入、检索 | 每一次嵌入批调用 |
| `StreamAsync` | 等每个更新（含第一个）的时长：只在等 Provider 时计时，调用方处理更新的时间不算，流的总时长不限 |

流式按每次更新计时：不改成整条流的总时限，那会把正常输出的长回答、调用方处理得慢的流截断；也不退回不计时，卡住的连接就没有上限。要限制一次对话的总时长，给 `StreamAsync` 传自己的取消令牌，或给调用设截止时间（只有调用方取消时枚举抛出 `OperationCanceledException`）。

归因只看令牌，Provider 抛出与接住取消后正常返回是同一个回答：

| 触发的令牌 | `CompleteAsync`、摄入、检索 | `StreamAsync` 的枚举 |
| --- | --- | --- |
| Provider 的超时令牌（调用方是否也取消不论） | `AI_TIMEOUT_001` | 抛出 `TimeoutException` |
| 只有调用方取消 | 抛出 `OperationCanceledException`，经管道时按调用的截止时间归因（截止时间没到即 `KERNEL_CANCELLED_001`） | 抛出 `OperationCanceledException` |
| 都没有触发，Provider 却抛出取消或 `TimeoutException`（它自己的传输超时） | `AI_TIMEOUT_001` | 抛出 `TimeoutException` |

Provider 接住取消、交回截断的应答、条数不全的嵌入或提前结束的流时，框架按触发的令牌改报超时或取消，不把截断的结果当作成功。直接从目录解析的客户端不经这一层。完整应答的时限覆盖整个函数调用循环，工具调用多、总时长可能超过 `TimeoutSeconds` 的应用要调大它。

## 提示词模板

`AIPromptTemplate(name, template, description)` 的占位符写成 `{name}`，连写两个左花括号（或两个右花括号）输出一个字面的花括号。`Render(arguments)` 按不变文化把值转成字符串，`null` 为空串；缺参数返回 `AI_VALIDATION_001`。参数值原样拼入，不会再被当作模板解析。

提示词目录合并两处来源：配置 `XiHan:AI:Prompts:<name>:Template` 与 `:Description`（也可以把模板直接写成 `XiHan:AI:Prompts:<name>` 的值），以及 `AddAIPrompt(template)` 的显式登记；同名时显式登记优先。

```csharp
using XiHan.Framework.AI.Abstractions.Prompts;

// 代码里登记的模板，覆盖配置里同名的 assistant
builder.Services.AddAIPrompt(new AIPromptTemplate("assistant", "You are the order assistant of {company}. Reply in {language}.", "Order assistant"));
```

## 护栏

护栏实现 `IAIGuardrail`，返回 `GuardrailVerdict.Allow()` 或 `GuardrailVerdict.Block(reasonKey, reasonArguments)`。原因是消息键加参数，`IAIChat` 拦截时返回 `AI_DENIED_001`，错误细节带 `guardrail`（护栏名）与 `reason`（按当前区域渲染的原因）。护栏抛出异常或返回失败时记 `AI_UNEXPECTED_001`：抛出时 `reason` 只写异常类型的全名，完整异常进日志（每次一条 Error，带护栏名）；返回失败时 `reason` 是那个错误的文案。

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

护栏以 `AddAIGuardrail<T>()` 登记（下一页的 `AssistantsFeature` 里有示例），按登记顺序执行，收到的是包括渲染后系统提示在内的全部消息。消息键的写法见[全球化和本地化](/common/localization)。内置的 `KeywordBlocklistGuardrail`（名字 `keyword-blocklist`）在 `XiHan:AI:Guardrails:Enabled` 为 `true` 时登记，只检查 `User` 消息：

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `XiHan:AI:Guardrails:Enabled` | `false` | 是否登记内置护栏 |
| `XiHan:AI:Guardrails:BlockedKeywords` | 空 | 禁用关键词，不区分大小写的子串匹配 |
| `XiHan:AI:Guardrails:InjectionPatterns` | 空 | 追加的提示注入正则，不区分大小写，单次匹配超时 250 毫秒 |
| `XiHan:AI:Guardrails:UseBuiltInInjectionHeuristics` | `true` | 是否启用内置的中英文提示注入模式（如「ignore previous instructions」「忽略以上指令」） |

## 配置

AI 配置不热更新：`XiHan:AI` 下的全部键在装配时读一次，改了要重启应用。框架不监听配置变化，`Invalidate` 只丢弃缓存的客户端、按同一份选项重建；提示词目录、工具目录、护栏、知识库选项与提示增强器都在创建时取定。这样做是因为这些配置在启动时校验（工具名、知识库提示词）并被多个单例取用，热更新只会部分生效——例如换了嵌入模型而向量集合的维度没变——AI 配置的改动通常本就伴随部署。

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
| `XiHan:AI:Providers:<name>:TimeoutSeconds` | 数值，大于 0 | 100 | Provider 调用的时限，范围与归因见上文「超时与取消」 |
| `XiHan:AI:Providers:<name>:Properties:<key>` | string | — | 原样透传给工厂 |
| `XiHan:AI:Prompts:<name>:Template` | string | — | 提示词模板 |
| `XiHan:AI:Prompts:<name>:Description` | string | — | 提示词说明 |
| `XiHan:AI:Guardrails:*` | — | — | 见上文「护栏」 |
| `XiHan:AI:Tools:<操作 ID>`、`XiHan:AI:Knowledge:*`、`XiHan:AI:VectorData:*` | — | — | 见[工具与知识库](/ai/tools#配置) |

::: warning 配置值写错时启动失败
读取 `XiHan:AI` 时值不合法即抛出 `ArgumentException`，应用启动失败，异常消息带出完整的配置键与原值：Provider 缺 `Kind`；`Endpoint` 不是绝对的 http(s) 地址（例如漏了 `http://`，写错的端点不会退回官方端点、把 `ApiKey` 发出去）；`EmbeddingDimensions` 与知识库的数值不是要求的整数；`TimeoutSeconds` 不是正数；护栏的两个开关不是 `true` / `false`；提示词缺 `Template`。`Endpoint` 不写或写空串表示官方端点。
:::

## 错误码

| 错误码 | 何时出现 |
| --- | --- |
| `AI_VALIDATION_001` | 请求不合法：Provider 缺 `ChatModel` / `EmbeddingModel`、提示词缺参数、摄入的文档 ID 为空、检索的问题为空、工具参数序列化不了、工具名不合法或重名（启动失败） |
| `AI_VALIDATION_002` | 嵌入维度与 `XiHan:AI:Knowledge:Dimensions` 不一致 |
| `AI_NOTFOUND_001` | Provider 未配置，或它的种类没有登记工厂；没写 Provider 名而目录没有默认 |
| `AI_NOTFOUND_002` | 提示词模板不存在 |
| `AI_NOTFOUND_003` | 请求里的工具名不在目录里 |
| `AI_DENIED_001` | 被护栏拦截，细节带 `guardrail` 与 `reason` |
| `AI_TIMEOUT_001` | 模型调用超时：Provider 的 `TimeoutSeconds` 到了（调用方同时取消也一样），或 Provider 报告了自己的超时 |
| `AI_UNAVAILABLE_001` | 模型端点不可达或返回错误 |
| `AI_UNAVAILABLE_002` | 没有知识库 Provider，或向量存储不可达 |
| `AI_UNEXPECTED_001` | 护栏抛出异常或返回失败 |

消息目录是 `ai`，键为 `ai.*`，内置 10 种区域的译文。

## 常见问题

### 能接 Azure OpenAI、DeepSeek 或本机 Ollama 吗

只要提供 OpenAI 兼容的聊天与嵌入接口，就配成 `Kind: openai` 并给 `Endpoint`；Ollama 的是 `http://localhost:11434/v1`。不兼容的服务由应用实现 `IChatClientFactory` 并以 `AddAIChatClientFactory<T>()` 登记一个新的 `Kind`。

### 为什么不直接注入 `IChatClient`

可以从 `IAIProviderCatalog` 解析到标准的 `IChatClient`，但那条路不经提示词、护栏、工具绑定与超时归因。业务对话走 `IAIChat`；只有在上面叠自己的中间件、或做与业务无关的实验时才直接拿客户端。

### 回答被截断了却返回成功，怎么回事

不会：Provider 接住取消、交回截断的应答时，框架按触发的令牌改报 `AI_TIMEOUT_001` 或抛出取消。看到的截断更可能是 `ChatOptions.MaxOutputTokens` 太小，调大它。

### 流式回答怎样限制总时长

`StreamAsync` 只按每个更新计时，总时长不限。要限总时长，给它传自己带超时的取消令牌，或在操作上设截止时间；只有调用方取消时枚举抛出 `OperationCanceledException`。

### 改了配置里的提示词或密钥为什么没生效

AI 配置在装配时读一次、不热更新，重启应用。`Invalidate` 只是丢掉缓存的客户端、按同一份选项重建，不是重新读配置。

## 相关包与下一步

- [AI.Abstractions](/packages/ai-abstractions)：目录、聊天入口、提示词、护栏与错误码
- [AI.OpenAI](/packages/ai-openai)：OpenAI 兼容 Provider
- [AI.VectorData](/packages/ai-vectordata)：知识库存储适配
- 下一步：[工具与知识库](/ai/tools)
