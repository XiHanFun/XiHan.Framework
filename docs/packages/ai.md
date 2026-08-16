# XiHan.Framework.AI

> AI 抽象包的默认实现：基于 Microsoft.Extensions.AI + Microsoft Agent Framework 的多 provider 会话、嵌入、智能体、RAG 与 MCP 工具桥接。

- **NuGet**：`XiHan.Framework.AI`
- **模块类**：`XiHanAIModule`（`[DependsOn(typeof(XiHanHttpModule))]`）
- **所在层**：基础设施层
- **关键依赖**：**Microsoft.Extensions.AI** / **Microsoft.Extensions.AI.OpenAI**（会话与嵌入，10.9.0）、**Microsoft.Agents.AI**（Agent，1.17.0）、**ModelContextProtocol**（MCP，2.2.0）、**Microsoft.Extensions.VectorData.Abstractions**（向量数据契约，10.9.0）；框架内部依赖 AI.Abstractions / Core / Http

## 概述

这个包是 AI 抽象（`XiHan.Framework.AI.Abstractions`）的默认实现，基于 `Microsoft.Extensions.AI` 与 Microsoft Agent Framework（MAF）提供开箱即用的会话、嵌入、智能体与 RAG 能力。核心设计有四点：

1. **一个 OpenAI 兼容适配器打天下**：多数 provider（OpenAI / DeepSeek / Azure / Ollama / vLLM / 自训模型）都走 OpenAI 兼容协议，设 `BaseUrl` 指向端点 + `Model` + `ApiKey` 即可，无须每家写一个适配器。
2. **多 provider 解析 + 配置热切换**：解析器按 provider 名构建并缓存 `IChatClient` / 嵌入生成器，配置源改动后调 `Invalidate` 使缓存失效即完成热切换。
3. **可插拔配置源**：默认读 `XiHan:AI` 配置节兜底，应用层可 `AddSingleton` 覆盖为 DB store（store 化 + 多租户 + 前端可维护），对上层透明。
4. **横切管道可选叠加**：内容护栏 / OpenTelemetry 遥测 / 响应分布式缓存三件套挂在 `ChatClientBuilder` 管道上，由 `XiHan:AI:Pipeline` 开关逐项启用，默认全关，不改变既有调用方式。

此外，它能把应用侧注册的「技能」通过 MCP（Model Context Protocol）暴露给外部 AI 客户端（如 Claude Code / Cursor），并提供 RAG 的切片/摄取/检索/提示增强默认实现，以及一套可插拔的提示词库（`IAiPromptStore`）。

## 何时使用

- 需要以统一门面调用大模型（一次对话 / 流式对话），并按 provider 名切换厂商
- 需要基于 provider 的嵌入生成 + RAG 检索增强（切片 / 摄取 / 检索 / 提示增强）
- 想构建带工具调用与多轮记忆的智能代理（`AIAgent`）
- 想把应用侧「技能」暴露为 MCP tools，供外部 AI 客户端调用

## 安装与启用

```bash
dotnet add package XiHan.Framework.AI
```

```csharp
[DependsOn(typeof(XiHanAIModule))]
public class MyModule : XiHanModule { }
```

`XiHanAIModule.ConfigureServices` 里调用 `AddXiHanAI()` 与 `AddXiHanRAG()`，注册结果（全部 `TryAdd*`，应用层可覆盖任一默认项）：

- **`AddXiHanAI()`**：绑定 `XiHanAiOptions`（配置节 `XiHan:AI`）与 `AiGuardrailOptions`（配置节 `XiHan:AI:Guardrail`）；`IAiProviderConfigStore → OptionsAiProviderConfigStore`；默认护栏 `IAiGuardrail → KeywordBlocklistGuardrail`（`TryAddEnumerable`，应用层可再 `AddSingleton<IAiGuardrail, X>()` 追加）；`OpenAiCompatibleChatClientFactory` + `IAiChatClientResolver → AiChatClientResolver`；`OpenAiEmbeddingGeneratorFactory` + `IAiEmbeddingGeneratorResolver → AiEmbeddingGeneratorResolver`；`IXiHanAiService → XiHanAiService`；`IAiPromptStore → OptionsAiPromptStore`；`IAiSkillRegistry → DefaultAiSkillRegistry`；`IXiHanAgentFactory → XiHanAgentFactory`。
- **`AddXiHanRAG()`**：`IChunkingStrategy → FixedWindowChunkingStrategy`；`IKnowledgeIngestor → DefaultKnowledgeIngestor`；`IKnowledgeRetriever → DefaultKnowledgeRetriever`；`IRagPromptAugmenter → DefaultRagPromptAugmenter`。

MCP 工具桥接 `AddXiHanMcpServerTools()` 不在模块内自动调用，须配合官方 `AddMcpServer()` 使用；HTTP 传输与端点暴露由 [XiHan.Framework.Web.Mcp](./web-mcp) 负责（见「MCP 工具桥接」）。

## 工作原理

**会话链路**：`XiHanAiService.ChatAsync` → `IAiChatClientResolver.Resolve(provider)` 取 `IChatClient` → 透传 `GetResponseAsync` / `GetStreamingResponseAsync`。门面只做「选 provider + 透传」，工具调用、结构化输出全走 M.E.AI 原生；解析到的 `IChatClient` 本身是否带护栏/遥测/缓存，取决于 `OpenAiCompatibleChatClientFactory` 按 `Pipeline` 开关构建时是否叠加了对应中间件（见下文「会话管道」）。

**解析与缓存**：`AiChatClientResolver` 用 `ConcurrentDictionary` 按 provider 名（缺省用内部默认槽键）缓存 `IChatClient`。首次解析时从 `IAiProviderConfigStore.GetAsync` 读配置（无匹配则抛 `InvalidOperationException`），交 `OpenAiCompatibleChatClientFactory` 构建。`OpenAiEmbeddingGeneratorFactory` / `AiEmbeddingGeneratorResolver` 与之同构（模型取 `EmbeddingModel`）。

**热切换**：`Invalidate(providerName)` 从缓存移除并释放对应客户端——空则清全部，否则清指定 provider 及默认槽（默认可能指向它）；下次 `Resolve` 按最新配置重建。这是应用层改了 DB 里 provider 的 key/baseUrl/model 后无重启生效的关键。

**工厂细节**：`OpenAiCompatibleChatClientFactory.Create` 用 `OpenAI.Chat.ChatClient`（`BaseUrl` 空则用官方端点，否则指向兼容端点），套 `.AsIChatClient().AsBuilder()` 按 `XiHan:AI:Pipeline` 开关逐项叠加中间件后 `.Build()`。`ApiKey` 为空时用占位符 `"no-key"`（本地/兼容端点常不校验 key）。

**会话管道（护栏 / 遥测 / 缓存）**：`AiPipelineOptions`（`XiHanAiOptions.Pipeline`，全局非按 provider）控制三个可选中间件，注册顺序=外→内（先见原始输入者最外层），**全部默认关**：

1. `EnableGuardrail`（且已注册至少一个 `IAiGuardrail`）→ `builder.UseContentGuardrail(...)` 挂 `GuardrailChatClient`，管道最外层，逐护栏跑 `InspectInputAsync`，任一拦截或护栏自身抛异常都直接回拒绝话术、不下发内层（fail-closed）；仅检查 `ChatRole.User` 消息，流式/非流式行为一致，v1 只做输入侧防护，不做输出侧脱敏。
2. `EnableTelemetry` → `builder.UseOpenTelemetry(sourceName: Pipeline.TelemetrySourceName, ...)`，`EnableSensitiveTelemetry` 控制是否记录 prompt/响应原文；未接 `TracerProvider`/`MeterProvider` 及导出器时为静默空操作。
3. `EnableResponseCache`（且 DI 中有 `IDistributedCache`）→ `builder.UseDistributedCache(cache)`，置于工具调用之外（命中即跳过工具），相同请求会重放同答，慎用于高温创造性调用。

最内层始终是 `.UseFunctionInvocation()`，因此工具调用会自动执行、无人工批准。默认护栏实现 `KeywordBlocklistGuardrail` 是敏感词子串匹配 + 中英提示注入启发式正则（`AiGuardrailOptions.BlockedKeywords` / `InjectionPatterns` / `UseBuiltInInjectionHeuristics`），定位「第一道防线」，将来可挂真检测服务（如内容安全 API）而不动管道。

**RAG 链路**：摄取 = 切片 → 批量 embedding → upsert 向量库；检索 = query embedding → 向量检索 → 映射片段。二者都依赖 `Microsoft.Extensions.VectorData` 的 `VectorStore`，**该 `VectorStore` 由应用层注册**（框架不注册）。

**提示词库**：`OptionsAiPromptStore` 实现 `IAiPromptStore`，读 `XiHanAiOptions.Prompts`（`IList<AiPromptTemplate>`，appsettings 兜底），按 `Name` + 可选 `Version` 查询；应用层可用 SysAiPrompt 等 DB 实现 `TryAddSingleton` 覆盖（store 化 + 后台维护 + 版本管理），对上层透明。

## 核心能力

- **会话门面实现**：`XiHanAiService` 实现 `IXiHanAiService`，选 provider 后透传 M.E.AI，工具/结构化输出/中间件走原生
- **多 provider 解析 + 热切换**：`AiChatClientResolver` / `AiEmbeddingGeneratorResolver` 按 provider 名构建并缓存，`Invalidate` 失效重建
- **OpenAI 兼容适配**：`OpenAiCompatibleChatClientFactory` / `OpenAiEmbeddingGeneratorFactory` 一套工厂覆盖 OpenAI / DeepSeek / Azure / Ollama / vLLM 等端点
- **可插拔配置源**：默认 `OptionsAiProviderConfigStore` 读 `XiHan:AI` 兜底；应用层可换 DB store
- **内容护栏**：默认 `KeywordBlocklistGuardrail`（敏感词 + 提示注入启发式），经 `GuardrailChatClient`（`DelegatingChatClient`）挂管道最外层，fail-closed；支持 `AddSingleton<IAiGuardrail, X>()` 叠加多护栏（全部放行才通过）；由 `XiHan:AI:Pipeline.EnableGuardrail` 开关，默认关
- **遥测 / 响应缓存管道**：`XiHan:AI:Pipeline` 另两个可选开关——`EnableTelemetry`（OpenTelemetry）、`EnableResponseCache`（`IDistributedCache`），均默认关
- **提示词库**：`OptionsAiPromptStore` 读 `XiHanAiOptions.Prompts` 兜底，按名 + 版本查询；应用层可换 DB store（SysAiPrompt 等）
- **智能体**：`XiHanAgentFactory` 从解析器取 `IChatClient`，经 `chatClient.AsAIAgent(...)` 产出 MAF 原生 `AIAgent`，支持工具与多轮会话
- **技能注册表**：`DefaultAiSkillRegistry` 构造时自动收纳 DI 里全部 `IAiSkill`，也支持运行时 `Register`
- **RAG 默认实现**：`FixedWindowChunkingStrategy`（固定窗口 + 重叠切片）、`DefaultKnowledgeIngestor`、`DefaultKnowledgeRetriever`、`DefaultRagPromptAugmenter`
- **MCP 工具桥接**：`AddXiHanMcpServerTools` 把技能注册表投影为 MCP server tools

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanAiService` | `IXiHanAiService` 默认实现（选 provider → 透传 M.E.AI 的 `GetResponseAsync` / `GetStreamingResponseAsync`） |
| `AiChatClientResolver` | `IAiChatClientResolver` 实现。`ConcurrentDictionary` 缓存 `IChatClient`，`IDisposable` 释放；`Invalidate` 热切换 |
| `AiEmbeddingGeneratorResolver` | `IAiEmbeddingGeneratorResolver` 实现（与会话解析器同构，模型取 `EmbeddingModel`） |
| `OpenAiCompatibleChatClientFactory` | 由 `AiProviderOptions` 构建 `IChatClient`，按 `Pipeline` 开关叠加护栏/遥测/缓存，最内层套 `UseFunctionInvocation`；`Model` 空则抛异常 |
| `OpenAiEmbeddingGeneratorFactory` | 由 `AiProviderOptions` 构建 `IEmbeddingGenerator<string, Embedding<float>>`；`EmbeddingModel` 空则抛异常 |
| `OptionsAiProviderConfigStore` | 默认 provider 配置源，读 `IOptionsMonitor<XiHanAiOptions>`（appsettings 兜底），键即 provider 名回填 |
| `GuardrailChatClient` | `DelegatingChatClient`，管道最外层；逐护栏跑 `InspectInputAsync`，任一拦截或护栏抛异常即 fail-closed 回拒绝话术（流式/非流式一致） |
| `KeywordBlocklistGuardrail` | 默认 `IAiGuardrail`：敏感词子串黑名单 + 中英提示注入启发式正则（内置 + 可配置扩展），只检 `ChatRole.User` 消息 |
| `GuardrailChatClientBuilderExtensions.UseContentGuardrail` | `ChatClientBuilder` 扩展方法，把 `GuardrailChatClient` 挂入管道（应置最外层，先于遥测/缓存/工具调用） |
| `OptionsAiPromptStore` | 默认 `IAiPromptStore` 实现，读 `IOptionsMonitor<XiHanAiOptions>.Prompts`（appsettings 兜底），按 `Name` + 可选 `Version` 查询 |
| `XiHanAgentFactory` | `IXiHanAgentFactory` 实现（解析器 → `IChatClient.AsAIAgent`） |
| `DefaultAiSkillRegistry` | `IAiSkillRegistry` 实现，构造时收纳 DI 全部 `IAiSkill`，线程安全、同名覆盖 |
| `SkillMcpToolsConfigurator` | `IConfigureOptions<McpServerOptions>`，把技能 `AsFunction()` 经 `McpServerTool.Create` 并入 MCP 工具集 |
| `FixedWindowChunkingStrategy` | 固定窗口 + 重叠切片；`MaxChunkSize` / `Overlap`，换行归一后按步长切分 |
| `DefaultKnowledgeIngestor` | 切片 → 批量 embedding → upsert；`RemoveDocumentAsync` 按文档删向量。依赖注入的 `VectorStore` |
| `DefaultKnowledgeRetriever` | query embedding → `VectorStore` 检索 → 映射 `RetrievedChunk`；`RetrievalFilter` 转 pre-filter 表达式 |
| `DefaultRagPromptAugmenter` | 简单模板增强（约束 + 编号片段 + 问题）；不走 Scriban，直接插值 |
| `VectorStoreKnowledgeRecord` | 向量库记录模型（`Microsoft.Extensions.VectorData` 特性），见下文 |

### DI 入口扩展方法

| 扩展方法 | 作用 |
| --- | --- |
| `IServiceCollection.AddXiHanAI()` | 注册会话/嵌入/护栏/提示词库/Agent/技能全套（模块自动调用） |
| `IServiceCollection.AddXiHanRAG()` | 注册 RAG 切片/摄取/检索/增强默认实现（模块自动调用） |
| `IServiceCollection.AddXiHanMcpServerTools()` | 把技能注册表投影为 MCP server tools（须配合官方 `AddMcpServer()`；Web.Mcp 模块已代为调用） |

### VectorStoreKnowledgeRecord 约定

`DefaultKnowledgeIngestor` / `DefaultKnowledgeRetriever` 用固定的记录模型：

- 集合名常量 `CollectionName = "xihan_knowledge"`
- 主键 `Guid Id`，由 `MakeId(documentId, index)` 用 `MD5(documentId:index)` 确定性派生（同文档同序号恒等，重建即覆盖）。用 `Guid` 是因为 Qdrant 只支持 `Guid`/`ulong` 键，`Guid` 兼容各连接器
- 过滤字段 `DocumentId` / `TenantId` 标 `IsIndexed = true` 以支持 pre-filter
- 向量维度编译期常量 `EmbeddingDimensions = 1536`（对齐 `text-embedding-3-small`）；换维度的嵌入模型须改此常量并重建集合。距离函数 `CosineSimilarity`，索引 `Hnsw`

## 配置

配置节 `XiHan:AI`（`XiHanAiOptions.SectionName`）。字段：

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `DefaultProvider` | `string?` | null | 未显式指定 provider 时用的 provider 名 |
| `Providers` | `IDictionary<string, AiProviderOptions>` | 空（键大小写不敏感） | 各 provider 配置，键为 provider 名 |
| `Pipeline` | `AiPipelineOptions` | 见下表（全部默认关） | 会话管道横切开关（护栏/遥测/缓存；全局，非按 provider） |
| `Prompts` | `IList<AiPromptTemplate>` | 空 | 提示词模板（Options 兜底源；store 化由应用层 `IAiPromptStore` 覆盖） |

单个 `AiProviderOptions`：

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `Provider` | `string` | 空 | provider 名（键未显式填时由配置源回填） |
| `ApiKey` | `string?` | null | API 密钥（空时工厂用占位符 `no-key`） |
| `BaseUrl` | `string?` | null | 端点基址（空用 OpenAI 官方端点，否则指向兼容端点） |
| `Model` | `string` | 空 | 会话模型名（空则会话工厂抛异常） |
| `EmbeddingModel` | `string?` | null | 嵌入模型名（RAG 用；空则该 provider 不支持嵌入） |
| `MaxOutputTokens` | `int?` | null | 最大输出 token |
| `Temperature` | `float?` | null | 采样温度 |
| `TimeoutSeconds` | `int?` | null | 请求超时秒 |
| `ExtraJson` | `string?` | null | 扩展参数 JSON（新 provider 专属参数） |

`AiPipelineOptions`（`XiHanAiOptions.Pipeline`；三者全部默认关闭，需部署方按需在 appsettings 打开）：

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `EnableGuardrail` | `bool` | false | 是否启用内容护栏（管道最外层，fail-closed 拦截） |
| `EnableTelemetry` | `bool` | false | 是否启用 OpenTelemetry 遥测（须另接 `TracerProvider`/`MeterProvider` + 导出器方可收集） |
| `EnableSensitiveTelemetry` | `bool` | false | 遥测是否记录敏感数据（prompt/响应原文） |
| `TelemetrySourceName` | `string` | `"XiHan.AI"` | 遥测 ActivitySource/Meter 名 |
| `EnableResponseCache` | `bool` | false | 是否启用响应分布式缓存（相同请求命中同答，慎用于创造性/高温调用） |

`AiPromptTemplate`（`XiHanAiOptions.Prompts` 元素）：

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `Name` | `string` | 空 | 模板名（唯一标识） |
| `Content` | `string` | 空 | 模板正文（可含占位/Scriban 变量，由渲染器填充） |
| `Version` | `string?` | null | 版本（空为当前/最新） |
| `Description` | `string?` | null | 说明 |

配置节 `XiHan:AI:Guardrail`（`AiGuardrailOptions.SectionName`）：

| 字段 | 类型 | 默认 | 含义 |
| --- | --- | --- | --- |
| `BlockedKeywords` | `IList<string>` | 空 | 敏感词黑名单（大小写不敏感子串匹配，命中即拦截） |
| `InjectionPatterns` | `IList<string>` | 空 | 自定义注入检测正则（大小写不敏感，命中即拦截） |
| `UseBuiltInInjectionHeuristics` | `bool` | true | 是否启用内置提示注入启发式正则（中英） |
| `RefusalMessage` | `string` | `"抱歉，你的请求包含不被允许的内容，已被安全策略拦截。"` | 拦截时返回的拒绝话术 |

示例 appsettings.json：

```json
{
  "XiHan": {
    "AI": {
      "DefaultProvider": "DeepSeek",
      "Providers": {
        "DeepSeek": {
          "ApiKey": "sk-xxx",
          "BaseUrl": "https://api.deepseek.com",
          "Model": "deepseek-chat",
          "EmbeddingModel": "text-embedding-3-small"
        },
        "Ollama": {
          "BaseUrl": "http://localhost:11434/v1",
          "Model": "qwen2.5"
        }
      },
      "Pipeline": {
        "EnableGuardrail": true,
        "EnableTelemetry": false,
        "EnableResponseCache": false
      },
      "Guardrail": {
        "BlockedKeywords": ["敏感词示例"],
        "UseBuiltInInjectionHeuristics": true
      }
    }
  }
}
```

## 使用示例

**解析聊天客户端并发起对话（走门面）：**

```csharp
public sealed class ChatSample(IXiHanAiService ai)
{
    public async Task<string?> AskAsync(string question, CancellationToken ct)
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, "你是简洁的助理。"),
            new ChatMessage(ChatRole.User, question),
        };
        var options = new XiHanChatOptions { Provider = "DeepSeek" }; // null 用默认 provider
        var response = await ai.ChatAsync(messages, options, ct);
        return response.Text;
    }
}
```

**流式对话：**

```csharp
await foreach (var update in ai.ChatStreamAsync(messages, options, ct))
{
    Console.Write(update.Text);
}
```

**注册 / 切换 provider（应用层用 DB store 覆盖 + 热切换）：**

```csharp
// 1) 覆盖默认配置源（在 AddXiHanAI 之后，TryAdd 不会顶掉先注册的显式实现）
services.AddSingleton<IAiProviderConfigStore, DbAiProviderConfigStore>();

// 2) 运维改了某 provider 的 key/baseUrl/model 后，使缓存失效即热切换
public sealed class ProviderAdmin(
    IAiChatClientResolver chatResolver,
    IAiEmbeddingGeneratorResolver embedResolver)
{
    public void OnProviderUpdated(string providerName)
    {
        chatResolver.Invalidate(providerName);
        embedResolver.Invalidate(providerName);
    }
}
```

**构建智能体并运行（MAF `AIAgent`）：**

```csharp
public sealed class AgentSample(IXiHanAgentFactory factory, IAiSkillRegistry skills)
{
    public async Task<string> RunAsync(string task, CancellationToken ct)
    {
        var tools = skills.All.Select(s => (AITool)s.AsFunction()).ToList();
        var agent = factory.Create(
            instructions: "你是 XiHan 项目助理。",
            name: "xihan-agent",
            tools: tools,
            providerName: null);
        var result = await agent.RunAsync(task, cancellationToken: ct);
        return result.Text;
    }
}
```

## 扩展点 / 自定义

所有默认实现均以 `TryAdd*` 注册，`AddSingleton` 覆盖任一即可（在 `AddXiHanAI` 之后或让 DI 顺序保证显式实现先注册）：

- **provider 配置源**：`IAiProviderConfigStore` → DB store（把 provider 从 appsettings 迁到数据库 + 前端可维护 + 多租户）；应用层负责在写入后调解析器 `Invalidate`
- **提示词库**：`IAiPromptStore` → DB store（如 SysAiPrompt，store 化 + 后台维护 + 版本），`TryAddSingleton` 覆盖 `OptionsAiPromptStore`
- **内容护栏**：`AddSingleton<IAiGuardrail, XxxGuardrail>()` 追加（`TryAddEnumerable` 注册的默认 `KeywordBlocklistGuardrail` 不会被顶掉，多护栏全部放行才通过；也可接 Azure Content Safety 等真检测服务）
- **切片策略**：`IChunkingStrategy` → 语义/句子切分等
- **摄取 / 检索**：`IKnowledgeIngestor` / `IKnowledgeRetriever` → 自定义向量库交互
- **提示增强**：`IRagPromptAugmenter` → 自定义模板
- **技能供给**：`AddSingleton<IAiSkill, XxxSkill>()`，注册表构造时自动收纳
- **MCP 工具**：依赖 [XiHan.Framework.Web.Mcp](./web-mcp) 的 `XiHanWebMcpModule`，技能即成为 MCP tool（HTTP 传输、端点映射与 key 鉴权都在该包内）

## 注意事项与最佳实践

- **向量库（Qdrant）不由本框架包注册**：`DefaultKnowledgeIngestor` / `DefaultKnowledgeRetriever` 构造依赖 `Microsoft.Extensions.VectorData` 的 `VectorStore`，而 `AddXiHanRAG` **不注册**任何具体 `VectorStore`。向量连接器与嵌入模型选择属**应用层部署事项**——应用（如 BasicApp）负责 `AddQdrantVectorStore(...)` 等登记，本框架包本身不提供 Qdrant 能力，也不引用其连接器包。若未注册 `VectorStore` 而调用 RAG 摄取/检索，会在解析这两个服务时因缺依赖失败。
- **嵌入维度硬编码**：`VectorStoreKnowledgeRecord.EmbeddingDimensions = 1536`。换用不同维度的嵌入模型必须改此常量并重建向量集合，否则 upsert/检索维度不匹配。
- **确定性主键**：切片主键由 `MD5(documentId:index)` 派生，重复摄取同一文档会覆盖旧切片（幂等）；删除须传原 `chunkCount`（`RemoveDocumentAsync`）以枚举全部键。
- **解析器缓存与热切换**：改了配置源里的 provider 参数后，必须调 `Invalidate` 才生效——缓存不会自动感知外部 DB 变更。
- **工具自动执行**：`OpenAiCompatibleChatClientFactory` 套了 `UseFunctionInvocation`，MAF 的 `ChatClientAgent` 内部也套 `FunctionInvokingChatClient`——工具/技能会被自动调用、无人工批准。当前 v1 技能均只读（知识检索）安全；将来接入有副作用的技能须自行加批准/审计。
- **管道三开关默认全关**：护栏是有意为之的安全策略（默认关避免无感知拦截业务）；缓存对高温创造性调用会重放同答，语义有风险；遥测在未接 OTel 导出器前是静默空操作。生产环境建议至少打开 `EnableGuardrail`，其余按需评估。
- **护栏仅做输入侧防护**：`GuardrailChatClient` v1 只检查 `ChatRole.User` 消息，不做模型输出脱敏；护栏自身抛异常也会被当作拦截处理（fail-closed），避免护栏故障时误放行。
- **RAG 提示增强不走 Scriban**：`DefaultRagPromptAugmenter` 直接字符串插值（与框架 `ITemplateService` 的 string 引擎一致，不解析 Scriban）。

## 依赖模块

- [XiHan.Framework.AI.Abstractions](./ai-abstractions)（接口与契约）
- [XiHan.Framework.Core](./core)
- [XiHan.Framework.Http](./http)（`XiHanAIModule` 依赖 `XiHanHttpModule`）
- 第三方核心：**Microsoft.Extensions.AI** / **Microsoft.Extensions.AI.OpenAI**（会话与嵌入）、**Microsoft.Agents.AI**（Agent）、**ModelContextProtocol**（MCP）、**Microsoft.Extensions.VectorData.Abstractions**（向量数据契约）

::: warning 部署边界
本包不注册具体 `VectorStore`。向量连接器（如 Qdrant）与嵌入模型选择属部署事项，由应用层登记（如 `AddQdrantVectorStore`）。框架 AI 包本身不是「Qdrant 能力」——它只依赖 `Microsoft.Extensions.VectorData` 的抽象 `VectorStore`。
:::

## 相关模块

- [XiHan.Framework.AI.Abstractions](./ai-abstractions)
- [XiHan.Framework.Http](./http)
