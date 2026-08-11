# XiHan.Framework.AI.Abstractions

> 纯 AI 契约包：会话门面、多 provider 解析、可插拔配置源、智能体、技能、RAG、提示词库、护栏与管道横切开关的一组接口，零框架内部依赖。

- **NuGet**：`XiHan.Framework.AI.Abstractions`
- **模块类**：—（无模块类，直接引用）
- **所在层**：基础设施层
- **关键依赖**：仅第三方 **Microsoft.Extensions.AI.Abstractions**（`IChatClient` / `IEmbeddingGenerator`）+ **Microsoft.Agents.AI.Abstractions**（`AIAgent`）；不引用任何 `XiHan.Framework.*`

## 概述

这个包只放 AI 相关的接口与契约，不含任何具体实现，也不依赖框架内部任何包。它把「一次/流式会话、按 provider 名选模型、provider 配置来源、智能体、命名技能、RAG 检索增强、提示词库、输入护栏、会话管道横切开关」抽成一组接口，让上层代码只依赖抽象。真正的实现由 `XiHan.Framework.AI` 提供；应用层（如把 provider 配置存进数据库、把提示词做成后台可维护的版本库）也能通过实现这些接口来替换默认行为。

设计上贯穿两条主线：其一是**薄封装**——`IXiHanAiService` / `IXiHanAgentFactory` 只在 `Microsoft.Extensions.AI` 与 Microsoft Agent Framework 之上加「按 provider 名选模型」这一层 XiHan 语义，会话、工具调用、结构化输出、`AIAgent` 全走原生类型不重造；其二是**可插拔 store**——`IAiProviderConfigStore` / `IAiPromptStore` 沿用「框架给抽象、应用给实现」的范式，默认走 Options 兜底，应用层可无缝换成 DB store。

## 何时使用

- 你的库/模块需要引用 AI 会话、Agent 或 RAG 契约，但不想拉入具体实现与第三方 SDK
- 你要提供自定义的 provider 配置源、提示词库或知识检索实现，替换框架默认项
- 想以 provider 名解耦模型调用，而不把某个厂商写死在业务里
- 不该用它的场景：只是想直接发起对话/构建 Agent——那应引用实现包 `XiHan.Framework.AI` 并 `[DependsOn(typeof(XiHanAIModule))]`

## 安装与启用

```bash
dotnet add package XiHan.Framework.AI.Abstractions
```

无模块类。直接引用即可，无需 `[DependsOn]`。服务注册与默认实现由 `XiHan.Framework.AI` 的 `XiHanAIModule` 负责——通常业务只引用实现包即可间接得到本包的全部契约。

## 核心能力

- **会话门面契约**：`IXiHanAiService` 定义一次对话 / 流式对话，薄封装 `Microsoft.Extensions.AI` 的 `IChatClient`，只加「多 provider 选择」这层语义；会话选项载体 `XiHanChatOptions`
- **多 provider 解析**：`IAiChatClientResolver` / `IAiEmbeddingGeneratorResolver` 按 provider 名取已构建好的会话客户端 / 嵌入生成器，二者都带 `Invalidate` 以支持配置热切换
- **可插拔配置源**：`IAiProviderConfigStore` 抽象 provider 配置来源（默认读 Options，应用层可换 DB store），配置载体 `AiProviderOptions` / 根配置 `XiHanAiOptions`（配置节 `XiHan:AI`）
- **智能体**：`IXiHanAgentFactory` 基于 Microsoft Agent Framework 从 provider 解析器构建原生 `AIAgent`
- **技能**：`IAiSkill` / `IAiSkillRegistry` 把应用注册的命名能力暴露为对话工具（`AIFunction`）与 MCP tool
- **RAG 契约**：`IChunkingStrategy`（切片）、`IKnowledgeIngestor`（摄取）、`IKnowledgeRetriever`（检索）、`IRagPromptAugmenter`（提示增强）及配套模型 `TextChunk` / `RetrievedChunk` / `KnowledgeIngestRequest` / `RetrievalFilter` / `ChunkingOptions`
- **提示词库**：`IAiPromptStore` / `AiPromptTemplate` 抽象可版本化的提示模板来源
- **输入护栏**：`IAiGuardrail` 对入站消息做安全检查（敏感词/注入检测等），多护栏「全部放行」才通过，拦截即 fail-closed 拒绝；结果载体 `GuardrailResult`；配置 `AiGuardrailOptions`（配置节 `XiHan:AI:Guardrail`）
- **管道横切开关**：`AiPipelineOptions`（配置节 `XiHan:AI:Pipeline`）统一开关护栏 / OpenTelemetry 遥测 / 响应分布式缓存，全部默认关，由部署方按需开启

## 主要 API / 类型

### 会话（Chat）

| 类型 | 说明 |
| --- | --- |
| `IXiHanAiService` | AI 会话服务门面。`Task<ChatResponse> ChatAsync(IEnumerable<ChatMessage> messages, XiHanChatOptions? options = null, CancellationToken ct = default)`；`IAsyncEnumerable<ChatResponseUpdate> ChatStreamAsync(...)` |
| `XiHanChatOptions` | 会话选项。`string? Provider`（null 用默认 provider）；`ChatOptions? ChatOptions`（透传 M.E.AI 的工具/温度/模型覆盖） |

### Provider 解析与配置

| 类型 | 说明 |
| --- | --- |
| `IAiChatClientResolver` | `IChatClient Resolve(string? providerName = null)`；`void Invalidate(string? providerName = null)`（清缓存，实现配置热切换；空则清全部，否则清指定 provider 及默认槽） |
| `IAiEmbeddingGeneratorResolver` | `IEmbeddingGenerator<string, Embedding<float>> Resolve(string? providerName = null)`；`void Invalidate(string? providerName = null)`。模型取 `AiProviderOptions.EmbeddingModel` |
| `IAiProviderConfigStore` | provider 配置来源抽象。`Task<AiProviderOptions?> GetAsync(string? providerName = null, CancellationToken ct = default)`（null 取默认 provider，无匹配返回 null 由调用方 fail-closed）；`Task<IReadOnlyList<AiProviderOptions>> GetAllAsync(...)` |
| `AiProviderOptions` | 单个 provider 配置：`Provider` / `ApiKey` / `BaseUrl` / `Model` / `EmbeddingModel` / `MaxOutputTokens` / `Temperature` / `TimeoutSeconds` / `ExtraJson` |
| `XiHanAiOptions` | 根配置（绑定配置节 `SectionName = "XiHan:AI"`）：`string? DefaultProvider`；`IDictionary<string, AiProviderOptions> Providers`（键为 provider 名，大小写不敏感）；`AiPipelineOptions Pipeline`（会话管道横切开关，非按 provider）；`IList<AiPromptTemplate> Prompts`（提示词模板 Options 兜底源） |

### 智能体（Agents）

| 类型 | 说明 |
| --- | --- |
| `IXiHanAgentFactory` | `AIAgent Create(string? instructions = null, string? name = null, IList<AITool>? tools = null, string? providerName = null)`。返回 MAF 原生 `AIAgent`，调用方直接用其 `RunAsync` / `RunStreamingAsync` / 多轮会话 API |

### 技能（Skills）

| 类型 | 说明 |
| --- | --- |
| `IAiSkill` | 命名可复用 AI 能力。`string Name`（唯一，作工具名 / MCP tool 名）；`string Description`；`AIFunction AsFunction()`（转为可供对话工具调用 / MCP 暴露的函数） |
| `IAiSkillRegistry` | 技能注册表。`void Register(IAiSkill skill)`（同名覆盖）；`IReadOnlyList<IAiSkill> All`；`IAiSkill? Find(string name)` |

### RAG（检索增强生成）

| 类型 | 说明 |
| --- | --- |
| `IChunkingStrategy` | `IReadOnlyList<string> Chunk(string text, ChunkingOptions options)` |
| `ChunkingOptions` | `int MaxChunkSize`（默认 1000）；`int Overlap`（相邻切片重叠字符数，默认 100） |
| `IKnowledgeIngestor` | `Task<int> IngestAsync(KnowledgeIngestRequest request, CancellationToken ct = default)`（返回切片数）；`Task RemoveDocumentAsync(string documentId, int chunkCount, CancellationToken ct = default)`（按文档清理已入库向量） |
| `KnowledgeIngestRequest` | 摄取请求：`DocumentId`（required）/ `Text`（required）/ `TenantId`（0=平台全局）/ `Title` / `Source` / `Provider`（嵌入 provider，null 用默认）/ `Chunking` |
| `IKnowledgeRetriever` | `Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK = 5, RetrievalFilter? filter = null, string? provider = null, CancellationToken ct = default)` |
| `RetrievalFilter` | 向量库 pre-filter：`long? TenantId`（0=平台全局，null 不限）；`string? DocumentId`（null 不限） |
| `IRagPromptAugmenter` | `string Augment(string userPrompt, IReadOnlyList<RetrievedChunk> context)`（context 为空原样返回） |
| `TextChunk` | 入库前切片：`DocumentId` / `Index` / `Text`（均 required）/ `TenantId` / `Title` / `Source` |
| `RetrievedChunk` | 检索命中：`DocumentId` / `Index` / `Text`（均 required）/ `Title` / `Source` / `double? Score`（相似度，越大越相近；连接器量纲可能不同） |

### 提示词库（Prompts）

| 类型 | 说明 |
| --- | --- |
| `IAiPromptStore` | `Task<AiPromptTemplate?> GetAsync(string name, string? version = null, CancellationToken ct = default)`（version 空取当前版本）；`Task<IReadOnlyList<AiPromptTemplate>> ListAsync(...)` |
| `AiPromptTemplate` | `Name`（唯一）/ `Content`（正文，可含占位/Scriban 变量）/ `Version`（空为当前）/ `Description` |

### 护栏（Guardrails）

| 类型 | 说明 |
| --- | --- |
| `IAiGuardrail` | 输入护栏检查 seam。`string Name`（诊断用）；`ValueTask<GuardrailResult> InspectInputAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default)`。多护栏「全部放行」才通过，拦截即 fail-closed，不下发模型 |
| `GuardrailResult` | 检查结果（私有构造，经静态工厂构建）。`bool IsBlocked`；`string? Reason`（放行时为 null）；`static GuardrailResult Allow()`；`static GuardrailResult Block(string reason)` |
| `AiGuardrailOptions` | 护栏配置（绑定配置节 `SectionName = "XiHan:AI:Guardrail"`）：`IList<string> BlockedKeywords`（敏感词黑名单，大小写不敏感子串匹配）；`IList<string> InjectionPatterns`（自定义注入检测正则）；`bool UseBuiltInInjectionHeuristics`（是否启用内置提示注入启发式正则，默认 `true`）；`string RefusalMessage`（拦截时的拒绝话术） |

### 管道横切开关（Pipeline）

| 类型 | 说明 |
| --- | --- |
| `AiPipelineOptions` | 会话管道横切开关（绑定配置节 `XiHan:AI:Pipeline`，全部默认关）：`bool EnableGuardrail`（管道最外层，fail-closed 拦截）；`bool EnableTelemetry`（须另接 OTel `TracerProvider`/`MeterProvider` + 导出器方可收集）；`bool EnableSensitiveTelemetry`（遥测是否记录 prompt/响应原文，默认关）；`string TelemetrySourceName`（默认 `"XiHan.AI"`）；`bool EnableResponseCache`（响应分布式缓存，慎用于创造性/高温调用） |

## 配置

配置载体在本包，但绑定与实现由 `XiHan.Framework.AI` 完成。三个配置节均定义在本包：根配置节 `XiHan:AI`（`XiHanAiOptions.SectionName`）、护栏配置节 `XiHan:AI:Guardrail`（`AiGuardrailOptions.SectionName`）；管道开关 `AiPipelineOptions` 作为 `XiHanAiOptions.Pipeline` 属性随根配置节一并绑定（对应 `XiHan:AI:Pipeline`）。字段见上表。示例见 [XiHan.Framework.AI](./ai) 的「配置」小节。

## 使用示例

以下示例只依赖本包的契约类型（实现由 `XiHan.Framework.AI` 注入）。

**注入门面发起一次对话：**

```csharp
public sealed class MyChatUseCase(IXiHanAiService ai)
{
    public async Task<string?> AskAsync(string question, CancellationToken ct)
    {
        var messages = new[] { new ChatMessage(ChatRole.User, question) };
        // 指定 provider（null 用默认 provider）
        var options = new XiHanChatOptions { Provider = "DeepSeek" };
        var response = await ai.ChatAsync(messages, options, ct);
        return response.Text;
    }
}
```

**从解析器直接拿 `IChatClient`（需要用 M.E.AI 原生管道时）：**

```csharp
public sealed class MyLowLevel(IAiChatClientResolver resolver)
{
    public IChatClient Client => resolver.Resolve(providerName: null); // 默认 provider
}
```

**注册一个自定义技能（应用层）：**

```csharp
public sealed class SumSkill : IAiSkill
{
    public string Name => "sum_numbers";
    public string Description => "计算两个整数之和";
    public AIFunction AsFunction() =>
        AIFunctionFactory.Create((int a, int b) => a + b, Name, Description);
}

// DI 注册后 DefaultAiSkillRegistry 构造时自动收纳
services.AddSingleton<IAiSkill, SumSkill>();
```

## 扩展点 / 自定义

本包全是契约，扩展方式是「实现接口并在 DI 覆盖实现包的默认项」（实现包一律以 `TryAdd*` 注册）：

- **换 provider 配置源**：实现 `IAiProviderConfigStore`（如读数据库的 store），`TryAddSingleton` 之前 `AddSingleton` 覆盖，即可把 provider 配置从 appsettings 迁到 DB / 前端可维护；改配置后调 `IAiChatClientResolver.Invalidate` / `IAiEmbeddingGeneratorResolver.Invalidate` 热切换。
- **换提示词库**：实现 `IAiPromptStore`（store 化 + 版本）。
- **换 RAG 组件**：实现 `IChunkingStrategy` / `IKnowledgeIngestor` / `IKnowledgeRetriever` / `IRagPromptAugmenter` 任一，覆盖框架默认实现。
- **供给技能**：`AddSingleton<IAiSkill, XxxSkill>()`，注册表构造时自动收纳，随后暴露为对话工具与 MCP tool。
- **叠加自定义护栏**：`AddSingleton<IAiGuardrail, XxxGuardrail>()` 追加检查逻辑（如接入第三方内容安全服务），与内置护栏一起「全部放行」才通过；需先在 `AiPipelineOptions.EnableGuardrail` 打开管道开关方可生效。

## 注意事项与最佳实践

- **fail-closed 语义**：`IAiProviderConfigStore.GetAsync` 无匹配返回 `null`，调用方（如解析器）应据此抛出明确异常而非静默降级——这是框架约定。
- **薄封装、勿重造**：会话/Agent 契约刻意贴近 `Microsoft.Extensions.AI` 与 MAF 原生类型（`ChatMessage` / `ChatResponse` / `AIAgent` / `AITool`），业务不必再包一层 DTO。
- **嵌入模型是可选项**：`AiProviderOptions.EmbeddingModel` 为空表示该 provider 不支持嵌入；RAG 摄取/检索前须确保所选 provider 配了嵌入模型。
- **RAG 向量库不属本抽象**：这些契约只描述「切片/摄取/检索/增强」的行为，**不涉及**具体向量数据库（Qdrant 等）——向量存储是实现包 + 应用层部署事项，见 [XiHan.Framework.AI](./ai)。
- **管道开关全部默认关**：`AiPipelineOptions` 的护栏/遥测/缓存三项均默认 `false`——护栏是有意开启的安全策略，缓存对高温创造性调用有重放同答的语义风险，遥测在未接 OTel 导出器前是静默空操作；是否开启由部署方按需在配置中打开。

## 依赖模块

- 无框架内部依赖（不引用任何 `XiHan.Framework.*`）
- 第三方核心：**Microsoft.Extensions.AI.Abstractions**（`IChatClient` / `IEmbeddingGenerator` / `ChatMessage` / `AIFunction` / `AITool`）、**Microsoft.Agents.AI.Abstractions**（`AIAgent`）

## 相关模块

- [XiHan.Framework.AI](./ai)（这些抽象的默认实现）
- [XiHan.Framework.Http](./http)（实现包的传输依赖）
