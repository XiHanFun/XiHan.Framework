# AI 与 MCP

框架把大模型接入收敛成三件事：按名解析 provider 拿到一个 `IChatClient`、把应用能力登记成「技能」、再决定这些技能是内部工具调用还是对外 MCP tool。本章讲这三条链路怎么串、配置放哪、以及哪些开关不打开就等于没有。

完整 API 与全部配置项见 [AI 包](../packages/ai)、[AI.Abstractions 包](../packages/ai-abstractions)、[Web.Mcp 包](../packages/web-mcp)。

## 三个包的分工

| 包 | 模块类 | 职责 |
| --- | --- | --- |
| `XiHan.Framework.AI.Abstractions` | 无（纯契约） | 接口与 Options：`IXiHanAiService`、`IAiProviderConfigStore`、`IAiSkill`、RAG 契约、护栏契约 |
| `XiHan.Framework.AI` | `XiHanAIModule` | 默认实现：OpenAI 兼容工厂、解析器、护栏、技能注册表、Agent 工厂、RAG |
| `XiHan.Framework.Web.Mcp` | `XiHanWebMcpModule` | 把技能经 HTTP 暴露成 MCP Server 端点，并做 key 鉴权 |

底座是 `Microsoft.Extensions.AI`（会话与嵌入）、`Microsoft.Agents.AI`（Agent）、`ModelContextProtocol`（MCP）、`Microsoft.Extensions.VectorData.Abstractions`（向量数据契约）。框架只在其上加「多 provider 选择 + 可插拔配置源 + 技能」这层语义，会话本身不做二次包装。

## 安装与启用

```bash
dotnet add package XiHan.Framework.AI
```

```csharp
[DependsOn(typeof(XiHanAIModule))]
public class MyModule : XiHanModule { }
```

`XiHanAIModule.ConfigureServices` 只做两件事：`AddXiHanAI()` 与 `AddXiHanRAG()`。两者注册的实现**全部走 `TryAdd*`**，所以应用层在任何位置显式注册同一接口都能顶掉默认实现。唯一例外是护栏 `IAiGuardrail`——它走 `TryAddEnumerable`，应用层注册的是追加而非顶替。

`XiHanAIModule` 依赖 `XiHanHttpModule`。要对外暴露 MCP 端点则改依赖 `XiHanWebMcpModule`（它已 `DependsOn` AI 模块与 Web.Core 模块）。

## 第一步：配 provider，发起对话

`XiHan:AI` 节里，`Providers` 是一张「provider 名 → 配置」的字典（键大小写不敏感），`DefaultProvider` 指定不显式传名时用哪一个。

```json
{
  "XiHan": {
    "AI": {
      "DefaultProvider": "DeepSeek",
      "Providers": {
        "DeepSeek": {
          "BaseUrl": "https://api.deepseek.com/v1",
          "ApiKey": "sk-xxx",
          "Model": "deepseek-chat",
          "EmbeddingModel": "text-embedding-3-small"
        },
        "Local": {
          "BaseUrl": "http://localhost:11434/v1",
          "Model": "qwen2.5"
        }
      }
    }
  }
}
```

```csharp
public sealed class AskService(IXiHanAiService ai)
{
    public async Task<string?> AskAsync(string question, CancellationToken ct)
    {
        ChatMessage[] messages =
        [
            new(ChatRole.System, "你是简洁的助理。"),
            new(ChatRole.User, question)
        ];

        // Provider 为 null 时用 DefaultProvider
        var response = await ai.ChatAsync(messages, new XiHanChatOptions { Provider = "Local" }, ct);
        return response.Text;
    }
}
```

流式用 `ChatStreamAsync`，逐条拿 `ChatResponseUpdate`：

```csharp
await foreach (var update in ai.ChatStreamAsync(messages, options, ct))
{
    Console.Write(update.Text);
}
```

::: tip 一个适配器覆盖多数厂商
`OpenAiCompatibleChatClientFactory` 只用 OpenAI 兼容协议建客户端。云端服务、本地推理服务、自训模型只要提供 OpenAI 兼容端点，都是「`BaseUrl` + `Model` + `ApiKey`」三个字段的事，不需要为每家写适配器。`ApiKey` 留空时会用占位串 `no-key`，方便本地端点不校验密钥的场景。
:::

::: warning 采样参数不在 provider 配置里生效
`AiProviderOptions` 上的 `MaxOutputTokens`、`Temperature`、`TimeoutSeconds`、`ExtraJson` **当前没有任何实现读取**——工厂建客户端时只用 `Model` / `ApiKey` / `BaseUrl`。要控制温度、最大输出、工具集，请走 `XiHanChatOptions.ChatOptions`（原生 `ChatOptions`），它会被门面透传下去。
:::

## 配置源怎么选

`IAiProviderConfigStore` 是 provider 配置的唯一入口，框架给了两种用法：

| 方案 | 实现 | 适用 |
| --- | --- | --- |
| Options 兜底（默认） | `OptionsAiProviderConfigStore` 读 `IOptionsMonitor<XiHanAiOptions>` | 单部署、密钥由运维管、不需要租户各配各的 |
| 应用 store 化 | 应用自实现，`AddSingleton<IAiProviderConfigStore, X>()` 覆盖 | 密钥落库并加密、后台页面维护、按租户给不同 provider |

覆盖是透明的：解析器、Agent 工厂、RAG 都只认这个接口，换源不改任何调用方。默认实现里键即 provider 名——`AiProviderOptions.Provider` 没显式填时会被回填成字典键。

## 热切换：改了配置必须 Invalidate

`AiChatClientResolver` 和 `AiEmbeddingGeneratorResolver` 都按 provider 名把构建好的客户端缓存在 `ConcurrentDictionary` 里，首次 `Resolve` 才读配置源。**缓存不会感知外部变更**，所以 store 化之后，写完配置必须主动使缓存失效：

```csharp
public sealed class ProviderAdmin(
    IAiChatClientResolver chatResolver,
    IAiEmbeddingGeneratorResolver embeddingResolver)
{
    public void OnProviderUpdated(string providerName)
    {
        chatResolver.Invalidate(providerName);
        embeddingResolver.Invalidate(providerName);
    }
}
```

`Invalidate` 的语义：

- 传 provider 名 → 移除该名下的客户端，**同时移除默认槽**（默认 provider 可能正指向它）；
- 传 `null` 或空 → 清空全部；
- 被移除的客户端会被 `Dispose`，下次 `Resolve` 用最新配置重建。

::: warning 两个解析器要分别失效
会话客户端与嵌入生成器是两份独立缓存。只调会话侧的 `Invalidate`，RAG 仍会用旧密钥/旧端点。
:::

## 会话管道：三个开关默认全关

`OpenAiCompatibleChatClientFactory` 建客户端时按 `XiHan:AI:Pipeline` 逐项叠加中间件。注册顺序即由外到内：

| 层 | 开关 | 行为 |
| --- | --- | --- |
| 内容护栏 | `EnableGuardrail` | 最外层，先见原始输入；拦截即不下发内层 |
| 遥测 | `EnableTelemetry` | `UseOpenTelemetry`，源名取 `TelemetrySourceName`（默认 `XiHan.AI`） |
| 响应缓存 | `EnableResponseCache` | `UseDistributedCache`，需 DI 里有 `IDistributedCache` |
| 工具调用 | 无开关，**恒定挂载** | `UseFunctionInvocation`，最内层 |

`Pipeline` 是全局配置，不是按 provider 配的；三个开关默认 `false`。

::: danger 工具会自动执行，没有人工批准环节
`UseFunctionInvocation` 恒挂在最内层，Agent 侧的 `ChatClientAgent` 内部也自带函数调用中间件。模型决定调工具就直接调，没有确认步骤。只读型技能（检索、查询）无所谓；一旦技能有副作用（写库、发消息、调外部接口），批准与审计必须由技能自己实现。
:::

::: warning 缓存开在工具调用之外
`EnableResponseCache` 位于 `UseFunctionInvocation` 外层，命中缓存会连工具调用一起跳过，相同输入恒返回同一答案。创造性/高温场景不要开。
:::

遥测开关只负责挂中间件，未接 `TracerProvider` / `MeterProvider` 与导出器时它是静默空操作，看不到任何数据。`EnableSensitiveTelemetry` 控制是否把 prompt 与响应原文记进遥测，默认关。

## 内容护栏

护栏是 `IAiGuardrail` 列表，`GuardrailChatClient`（一个 `DelegatingChatClient`）逐个跑 `InspectInputAsync`：

- 任一护栏返回拦截 → 直接回 `RefusalMessage`，不下发内层；
- 护栏自身抛异常 → 同样按拦截处理（fail-closed，避免护栏故障时误放行）；
- 流式与非流式行为一致，拦截时流里只吐一条拒绝话术。

默认实现 `KeywordBlocklistGuardrail` 是薄自包含实现：`BlockedKeywords` 做大小写不敏感子串匹配，`InjectionPatterns` 加上内置的中英注入启发式正则做匹配，**只检查 `ChatRole.User` 消息**。它定位「第一道防线」，混淆、多语言改写、编码绕过都能穿过去。

追加护栏用 `AddSingleton<IAiGuardrail, XxxGuardrail>()`——默认护栏是 `TryAddEnumerable` 注册的，不会被顶掉，多个护栏全部放行才算通过。要接真正的内容安全检测服务，实现这个接口挂进去即可，管道不用动。

::: warning 只做输入侧
当前护栏不检查模型输出，没有输出脱敏。输出侧的合规处理需要自己在调用方做。
:::

## 技能注册表

「技能」是应用供给的一个命名能力单元，接口只有三个成员：

```csharp
public sealed class SearchDocSkill : IAiSkill
{
    public string Name => "search_doc";

    public string Description => "在项目文档中检索关键词，返回最相关的片段。";

    public AIFunction AsFunction() => AIFunctionFactory.Create(
        (string keyword) => Search(keyword),
        name: Name,
        description: Description);
}
```

```csharp
services.AddSingleton<IAiSkill, SearchDocSkill>();
```

`DefaultAiSkillRegistry` 构造时把 DI 里所有 `IAiSkill` 一次性收纳进来（线程安全、按名索引、同名覆盖），也支持运行期 `Register` 追加。注册表的价值在于**一次登记、两条交付通道**：

| 通道 | 怎么用 |
| --- | --- |
| 对话工具 | `skill.AsFunction()` 放进 `ChatOptions.Tools` 或 Agent 的 `tools` |
| MCP tool | 由 `SkillMcpToolsConfigurator` 自动投影，外部 AI 客户端可见 |

::: tip 框架不内置任何技能
框架只提供接口与注册表，一个内置技能都没有。不注册技能时注册表是空的——MCP 端点即使开了也是零工具。
:::

## Agent 门面

`IXiHanAgentFactory` 是对 Agent 框架的薄封装，只加「按 provider 名选模型」这一层，返回的是原生 `AIAgent`：

```csharp
public sealed class AgentSample(IXiHanAgentFactory factory, IAiSkillRegistry skills)
{
    public async Task<string> RunAsync(string task, CancellationToken ct)
    {
        var tools = skills.All.Select(s => (AITool)s.AsFunction()).ToList();
        var agent = factory.Create(
            instructions: "你是项目助理，回答前先用工具查证。",
            name: "assistant",
            tools: tools,
            providerName: null);

        var response = await agent.RunAsync(task, cancellationToken: ct);
        return response.Text;
    }
}
```

多轮对话与记忆走原生会话对象，框架不再包一层：

```csharp
var session = await agent.CreateSessionAsync(ct);
var first = await agent.RunAsync("帮我看下这个模块", session, cancellationToken: ct);
var second = await agent.RunAsync("那第二点怎么改？", session, cancellationToken: ct);
```

流式用 `RunStreamingAsync`。工厂本身不缓存 Agent——每次 `Create` 都新建一个，底下的 `IChatClient` 才是被解析器缓存的那个。

## MCP Server：暴露与守门

`XiHanWebMcpModule` 让外部 AI 客户端通过 MCP 协议调用你的技能。它由 `XiHan:AI:Mcp` 节门控，**默认不暴露**：

```json
{
  "XiHan": {
    "AI": {
      "Mcp": {
        "Enabled": true,
        "ApiKey": "替换为足够长的随机密钥",
        "Path": "/mcp",
        "HeaderName": "X-Api-Key",
        "Stateless": true
      }
    }
  }
}
```

判定集中在 `XiHanMcpOptions.IsExposable`（`Enabled` 且 `ApiKey` 非空），并且在两处都生效：

| 阶段 | 未就绪时的行为 |
| --- | --- |
| `AddXiHanWebMcp(configuration)` | 只绑定选项，**不注册** MCP Server 与 HTTP 传输 |
| `MapXiHanMcp(options)` | 直接返回，**不映射任何端点** |

端点映射后做了两步：`AllowAnonymous()` 绕开框架的全局鉴权兜底策略，再挂 `McpApiKeyEndpointFilter` 用应用管理的 key 守门。过滤器先读 `HeaderName` 指定的请求头，为空则回退读 `Authorization: Bearer`，比较用定长比较防时序侧信道，不匹配直接 401。

::: danger key 是平台级凭据，不是用户身份
它只表示「这个 MCP 客户端可以访问本应用」。经 MCP 进来的调用**没有用户上下文，也没有租户上下文**——技能内部不要假设 `ICurrentUser` / `ICurrentTenant` 有值；涉及租户数据的技能必须自己决定取哪个租户的数据，或干脆拒绝在无上下文时执行。
:::

::: warning 部署侧两件事
`/mcp` 是 SSE 长连接：走反向代理要关闭响应缓冲并放宽超时。若启用了开放接口签名校验之类的前置中间件，把 `/mcp` 加进它的忽略路径。
:::

## RAG

`AddXiHanRAG()` 注册四件默认实现，链路是固定的：

| 环节 | 接口 | 默认实现 |
| --- | --- | --- |
| 切片 | `IChunkingStrategy` | `FixedWindowChunkingStrategy`（固定字符窗口 + 重叠，唯一策略） |
| 摄取 | `IKnowledgeIngestor` | 切片 → 批量嵌入 → upsert 向量库 |
| 检索 | `IKnowledgeRetriever` | query 嵌入 → 向量检索 → 映射 `RetrievedChunk` |
| 提示增强 | `IRagPromptAugmenter` | 约束语 + 编号片段 + 问题的纯字符串拼接 |

```csharp
public sealed class KnowledgeSample(
    IKnowledgeIngestor ingestor,
    IKnowledgeRetriever retriever,
    IRagPromptAugmenter augmenter,
    IXiHanAiService ai)
{
    public Task<int> ImportAsync(string docId, string text, CancellationToken ct) =>
        ingestor.IngestAsync(new KnowledgeIngestRequest
        {
            DocumentId = docId,
            Text = text,
            Title = "产品手册",
            Source = "manual.md",
            TenantId = 0,
            Chunking = new ChunkingOptions { MaxChunkSize = 800, Overlap = 80 }
        }, ct);

    public async Task<string?> AskAsync(string question, CancellationToken ct)
    {
        var chunks = await retriever.RetrieveAsync(
            question,
            topK: 5,
            filter: new RetrievalFilter { TenantId = 0 },
            cancellationToken: ct);

        var prompt = augmenter.Augment(question, chunks);
        var response = await ai.ChatAsync([new ChatMessage(ChatRole.User, prompt)], null, ct);
        return response.Text;
    }
}
```

::: danger 向量库必须由应用自己注册
`AddXiHanRAG()` **不注册任何 `VectorStore`**——选哪个向量库属于部署决策，框架只依赖 `Microsoft.Extensions.VectorData` 的抽象。应用侧不登记具体连接器，解析 `IKnowledgeIngestor` / `IKnowledgeRetriever` 时就会因缺少 `VectorStore` 依赖而失败。
:::

几个必须知道的约定：

- **维度必须与嵌入模型一致**。`KnowledgeVectorOptions` 默认集合名 `default_knowledge`、维度 `1536`。维度对不上时摄取与检索都会在 upsert / 向量检索**之前**抛出明确异常（`EnsureDimensions`），而不是丢一个驱动层报错。换模型要同时改维度并更换集合名或删除原集合重建。
- **这份 Options 不绑配置节**。`AddXiHanRAG(Action<KnowledgeVectorOptions>)` 只接受代码配置；要从 appsettings 读，应用侧自行 `services.Configure<KnowledgeVectorOptions>(configuration.GetSection("..."))`。
- **切片主键是确定性的**。由 `documentId:index` 派生，重复摄取同一文档即覆盖，天然幂等。
- **删除要传原切片数**。`RemoveDocumentAsync(documentId, chunkCount)` 靠枚举 `0..chunkCount-1` 生成键来删，`chunkCount` 传小了会留孤儿向量——摄取返回的切片数要存下来。
- **过滤只作用于两个索引字段**。`RetrievalFilter` 只有 `TenantId` 与 `DocumentId`，它们在集合定义里标了索引，作为 pre-filter 下推到向量库。
- **嵌入模型单独配**。取 `AiProviderOptions.EmbeddingModel`，与会话共用同一端点和密钥；该字段为空时解析嵌入生成器会抛异常。

::: tip 故障可读
向量库连不上会被翻译成 `ServiceUnavailableException`（503），嵌入端点的 401/404/429/5xx 也被翻译成带 provider 名与模型名的可操作消息。请求本身的问题（如 400 内容超长、维度不符）原样抛出——那类失败重试没用，报成 503 反而掩盖真实缺陷。
:::

## 配置速查

| 配置节 | Options 类 | 管什么 |
| --- | --- | --- |
| `XiHan:AI` | `XiHanAiOptions` | `DefaultProvider`、`Providers`、`Pipeline`、`Prompts` |
| `XiHan:AI:Pipeline` | `AiPipelineOptions` | 护栏/遥测/缓存三开关，默认全 `false` |
| `XiHan:AI:Guardrail` | `AiGuardrailOptions` | 敏感词、注入正则、内置启发式开关、拒绝话术 |
| `XiHan:AI:Mcp` | `XiHanMcpOptions` | MCP 端点开关、密钥、请求头名、路径、无状态传输 |

提示词模板走 `IAiPromptStore`：默认实现 `OptionsAiPromptStore` 读 `XiHan:AI:Prompts`，按 `Name` + 可选 `Version` 查；应用层可覆盖成落库版本，对上层同样透明。

全部字段的类型与默认值见 [AI 包](../packages/ai) 与 [Web.Mcp 包](../packages/web-mcp)。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 抛「未找到 AI Provider 配置」 | `DefaultProvider` 没配，或传的 provider 名不在 `Providers` 字典里 |
| 改了数据库里的密钥仍用旧的 | 没调 `Invalidate`，解析器缓存不感知外部变更 |
| 换了密钥后会话正常但 RAG 报鉴权失败 | 只失效了会话解析器，嵌入解析器还是旧客户端 |
| 配了 `Temperature` / `MaxOutputTokens` 不起作用 | 这些字段当前无实现读取，改用 `XiHanChatOptions.ChatOptions` |
| 护栏没拦住任何东西 | `Pipeline.EnableGuardrail` 默认关；或内容不在 `ChatRole.User` 消息里 |
| 开了遥测但没有任何数据 | 未接 `TracerProvider` / `MeterProvider` 与导出器，中间件是空操作 |
| 相同问题总是同一个答案 | 开了 `EnableResponseCache`，命中缓存连工具调用一起跳过 |
| `/mcp` 返回 404 | `Enabled` 为 false 或 `ApiKey` 为空，端点根本没被映射 |
| `/mcp` 返回 401 | 请求头名与 `HeaderName` 不一致，或没走 `Authorization: Bearer` |
| MCP 连上了但看不到工具 | 没有任何 `IAiSkill` 注册，框架不内置技能 |
| 解析 `IKnowledgeIngestor` 失败 | 应用没注册 `VectorStore`，框架不提供具体连接器 |
| 摄取时报维度不匹配 | 嵌入模型输出维度与 `KnowledgeVectorOptions.Dimensions` 不一致 |
| 删了文档但还能检索到 | `RemoveDocumentAsync` 的 `chunkCount` 小于实际切片数 |
| 向量库故障返回 503 | 连接类故障被统一翻译，原始异常在内部异常里 |

## 下一步

- [配置与选项](./configuration)：Options 绑定与覆盖顺序
- [依赖注入](./dependency-injection)：`TryAdd` 覆盖默认实现的时机
- [Web 应用开发](./web)：端点鉴权与全局兜底策略
- [缓存与分布式锁](./caching)：响应缓存依赖的 `IDistributedCache`
- [AI 包](../packages/ai)：完整 API 与全部配置项
- [AI.Abstractions 包](../packages/ai-abstractions)：接口与契约
- [Web.Mcp 包](../packages/web-mcp)：MCP 端点与鉴权细节
