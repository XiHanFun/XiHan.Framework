来源：https://framework.docs.xihanfun.com/ai/tools

# 工具与知识库

这一页讲怎样让模型「做事」和「有依据」：`Expose.Ai` 把已有的操作契约变成模型可调用的工具，工具经完整管道执行、失败以 JSON 交给模型；知识库流水线把文档切块、嵌入、写入向量存储，检索后拼进提示词；`AI.VectorData` 把 `IKnowledgeStore` 适配到 `Microsoft.Extensions.VectorData` 的任何连接器，并自带健康项；最后说明它与 MCP 的关系。读完后，我们能让上一页的助手调用订单查询、并按 FAQ 文档回答问题。前置知识是[模型与对话](/ai/chat)、[操作契约](/web/operations)与[执行管道](/startup/pipeline)。

## 为什么需要

模型只会说话，查订单、改状态要靶向到业务代码；给它随手写一堆函数，权限、校验、事务、审计全都绕过了，模型能做的事超过了提问的用户本人。框架把工具与操作契约合二为一：操作上加 `Expose.Ai`，工具目录自动把它桥接成模型工具，调用时在新的调用上下文里经完整管道执行，沿用调用方的主体与租户。回答要有依据时，知识库流水线把检索到的片段编号拼进提示词，存储由应用选的向量库承担，框架不伪造默认实现。

## 概念

| 术语 | 说明 |
| --- | --- |
| 工具目录 `IAIToolCatalog` | 合并显式登记的 `AITool` 与 `Expose.Ai` 的操作；`All()` / `Find(name)` 返回声明，`Bind(caller)` 返回绑定到调用方、可执行的工具 |
| `OperationAIFunction` | 一个操作桥接成的工具：名字、描述、输入与输出 Schema 来自操作契约 |
| `AIToolsFeature` | 提供 `ai.tools@1` 的默认实现，`Profiles.Full` 已包含；启动时构建目录并校验工具名 |
| `IKnowledgeIngestor` / `IKnowledgeRetriever` / `IRagPromptAugmenter` | 知识库流水线的三个服务：摄入与删除、检索、提示增强，在 `AI.Abstractions` 里，与存储无关 |
| `IKnowledgeStore` | 能力 `ai.knowledge@1` 的契约：写入、按文档删除、向量搜索；由 [AI.VectorData](/packages/ai-vectordata) 实现 |
| `RetrievedChunk` | 检索结果的一块：`Id`、`DocumentId`、`ChunkIndex`、`Text`、`Score`、`Metadata` |

## 最小示例

把订单查询操作暴露成工具，并在特性里登记护栏与一个显式工具。先是操作：

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

`OrdersFeature` 以 `context.AddGeneratedOperations()` 登记它，写法见[审计](/common/auditing#记录调用来源)；三个请求与响应类型登记在项目的 `JsonSerializerContext` 里。然后是助手特性，声明对聊天与工具能力的依赖，登记上一页的护栏与一个用 `AIFunctionFactory` 创建的显式工具：

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
        context.AddGeneratedOperations();
    }
}
```

最后在上一页的 `AskAssistant` 请求里加一行 `Tools = ["orders_list", "clock_now"]`。运行后：问「有哪些待发货的订单」时，模型发起 `orders_list` 调用、参数 `{"status": "pending"}`，客户端的函数调用循环在管道里执行 `orders.list` 并把 `{"orders":[{"id":"o-1",…}]}` 交回模型，最终回答里出现 `o-1`；审计与追踪里能看到这次 `orders.list` 调用与发起聊天的调用共享同一个 trace-id。

## 操作工具

工具目录合并两处来源：`AddAITool(AITool)` 显式登记的工具原样返回；登记表里 `Expose` 含 `Ai`、稳定性不是 `Stability.Internal` 的操作，各桥接成一个 `OperationAIFunction`。`IAIChat` 会自动把 `Tools` 列出的名字绑定到当前调用。

| 项 | 规则 |
| --- | --- |
| 名字 | `<resource>_<verb>`，`-` 与 `.` 换成 `_`；超过 64 个字符时取前 55 个字符加 `_` 与 SHA-256 的前 8 位十六进制；`XiHan:AI:Tools:<操作 ID>` 覆盖 |
| 名字校验 | 必须匹配 `^[A-Za-z0-9_-]{1,64}$`，不合法或重名时启动失败（`AI_VALIDATION_001`）：`AIToolsFeature` 与 `OpenAIFeature` 启动时都构建工具目录，只装其中任何一个也一样 |
| 描述 | 消息目录里的 `<操作 ID>.description`（ID 里的 `-` 换成 `_`），按调用方区域翻译；没有时为空 |
| 输入 Schema | 操作的请求 Schema，没有时 `{"type":"object"}` |
| 输出 Schema | 操作的响应 Schema |

工具被调用时，参数序列化成请求 JSON，在新作用域、新 `InvocationContext` 里经完整管道执行：trace-id、截止时间、Baggage 与安全上下文（主体与租户）复制自调用方，Validate、Authorize、Transaction 等阶段照常生效；新上下文不带调用来源，`FindOrigin()` 为无值。成功返回响应 JSON；失败返回 `{"error":"<错误码>","message":"<按调用方区域翻译的文案>"}` 交给模型处理，不抛异常。`AI.OpenAI` 的聊天客户端带函数调用循环，模型发起的工具调用在客户端里自动执行。

::: tip 工具以调用方身份执行
工具调用沿用调用方此刻的主体与租户：调用方已在自己的管道里认证过，凭据不复制，Authenticate 阶段没有凭据可认证即保留这个主体。Authorize 阶段以调用方主体按契约判定，契约声明了 `Permission` 的操作，调用方持有该权限才放行；没有权限得到 `SECURITY_DENIED_004`、匿名调用得到 `SECURITY_DENIED_001`，错误以 JSON 交给模型。模型能做的事不超过发起聊天的用户本人能做的事。
:::

## 知识库

知识库流水线是「切块 → 嵌入 → 写入存储 → 检索 → 提示增强」。切块、嵌入、检索与增强在 `AI.Abstractions` 里，与存储无关；Provider 只实现 `IKnowledgeStore` 的写入、按文档删除与向量搜索。

| 服务 | 作用 |
| --- | --- |
| `IKnowledgeIngestor` | `IngestAsync(requests)` 批量摄入，每篇文档一个 `Result<int>`（写入的块数）；`RemoveAsync(collection, documentIds)` 按文档删除 |
| `IChunkingStrategy` | 内置 `FixedWindowChunkingStrategy`：按字符的固定窗口，重叠裁到 `[0, MaxChunkSize − 1]`，换行统一为 `\n`，空白块丢弃 |
| `IKnowledgeRetriever` | `RetrieveAsync(query)`：问题嵌入 → 向量搜索（过滤与 `MinScore` 一起交给存储） |
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
                Metadata = new Dictionary<string, string> { [KnowledgeMetadataKeys.Source] = "faq" },
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
- 检索的 `Top` 缺省取 `XiHan:AI:Knowledge:Top`，最多 1000；`Filter`（`RetrievalFilter`）的 `DocumentIds`（最多 1000 个）与 `Metadata`（键值相等）取交集，超过上限在构造时抛出 `ArgumentOutOfRangeException`（ADR-0190）；`MinScore` 与过滤一起交给存储施加，原生支持得分阈值的后端在服务端过滤。
- `Augment` 在检索结果为空时原样返回问题。配置了 `XiHan:AI:Knowledge:PromptName` 就用它指向的模板（占位符 `{context}`、`{question}`，配置里的或 `AddAIPrompt` 登记的都行），没配置时用内置英文模板。模板在启动时校验：`AIToolsFeature`、`VectorDataKnowledgeFeature` 与 `OpenAIFeature` 启动时都解析增强器（只装其中任何一个也一样），模板不存在（`AI_NOTFOUND_002`）或引用了这两个以外的占位符、用代表性参数试渲染失败（`AI_VALIDATION_001`）即启动失败，不会悄悄换成内置模板。
- `{context}` 里每个片段一段：先是 `[序号] Source: 来源` 一行，再是片段文本。来源依次取块元数据的 `KnowledgeMetadataKeys.Title`（`title`）、`KnowledgeMetadataKeys.Source`（`source`），都没有时取文档 ID。文档 ID 多半是 Guid，模型拿它说不清依据，所以摄入时最好带上标题。
- 没有装配 `ai.knowledge` Provider 时，摄入、删除与检索都返回 `AI_UNAVAILABLE_002`。

## AI.VectorData

`AI.VectorData` 把 `IKnowledgeStore` 映射到应用登记的 `Microsoft.Extensions.VectorData` 的 `VectorStore`，任何连接器都行；容器里没有 `VectorStore` 时启动失败。向量库连接器由应用选择并自行引用其包，下例用内存连接器，只适合开发：

```bash
dotnet add package XiHan.Framework.AI.VectorData --prerelease
```

```csharp
using XiHan.Framework.AI.OpenAI;
using XiHan.Framework.AI.VectorData;

builder.Services.AddSingleton<VectorStore>(new InMemoryVectorStore());
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OpenAIFeature>()
    .UseFeature<VectorDataKnowledgeFeature>());
```

```json
{ "XiHan": { "AI": { "Knowledge": { "Collection": "faq", "Dimensions": 768 } } } }
```

集合定义、距离函数与过滤的下推方式见 [AI.VectorData](/packages/ai-vectordata)。它自带健康项 `ai.vectordata.store`，应用不必另写向量库的健康检查：

- 启动时同步探一次，之后每隔 `XiHan:AI:VectorData:HealthProbeIntervalSeconds`（缺省 15 秒，也是单次探测的超时）在后台重探。
- 探针只问默认集合（`XiHan:AI:Knowledge:Collection`）在不在，存在与否都算连得上；不列集合、不建集合、不读写记录。
- 向量库应答即 Ready。连接器抛出时 Degraded，错误码 `AI_UNAVAILABLE_002`，与检索在这时返回的码相同；探测超时也是 Degraded，错误码 `KERNEL_UNAVAILABLE_005`。
- 条目 Degraded 时应用照常启动、照常服务，`/health/ready` 仍通过，恢复后条目回到 Ready。条目不带数据与说明，不写端点或连接信息。汇总见[生命周期与健康](/startup/lifecycle)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:AI:Tools:<操作 ID>` | string | 按约定推导 | 覆盖操作工具名 |
| `XiHan:AI:Knowledge:Collection` | string | `knowledge` | 默认集合；带租户的调用用 `<Collection>.<租户 ID>`，各租户互不可见（ADR-0210） |
| `XiHan:AI:Knowledge:Dimensions` | int，至少 1 | 1536 | 嵌入维度 |
| `XiHan:AI:Knowledge:ChunkSize` | int，至少 1 | 1000 | 切块大小（字符） |
| `XiHan:AI:Knowledge:ChunkOverlap` | int，不小于 0 | 100 | 相邻块的重叠（字符） |
| `XiHan:AI:Knowledge:Top` | int，至少 1 | 5 | 默认检索条数 |
| `XiHan:AI:Knowledge:EmbeddingProvider` | string | 目录的默认嵌入 Provider | 知识库用的嵌入 Provider；请求写了 Provider 时以请求为准 |
| `XiHan:AI:Knowledge:PromptName` | string | 内置模板 | 提示增强用的模板名；启动时校验存在且只用 `{context}`、`{question}` |
| `XiHan:AI:VectorData:HealthProbeIntervalSeconds` | 有限的数值，大于 0 | 15 | `AI.VectorData` 健康项的重探间隔，也是单次探测的超时 |

Provider、提示词与护栏的键见[模型与对话](/ai/chat#配置)。与那里相同，这些键在装配时读一次、不热更新，取值不合法时抛出 `ArgumentException`、启动失败。

## 与 MCP 的关系

`Expose.Ai` 面向进程内的模型调用：工具在本进程的聊天里被模型发起、在本进程的管道里执行。要让外部的 Agent 或 IDE 通过 Model Context Protocol 调用同一份操作契约，由 `Protocols.Mcp` 把 `Expose.Mcp` 的操作暴露成 MCP tool，见 [MCP](/protocols/mcp)。两者共用操作契约与管道，一个操作可以同时标 `Expose.Ai | Expose.Mcp`（`Expose.All` 包含两者），分别服务于「我的应用里的模型」与「别人的 Agent」。

## 与其他能力的关系

- 执行管道：工具调用是一次完整的管道调用，审计、事务、校验都照常生效，审计事件与发起聊天的调用共享 trace-id，见[执行管道](/startup/pipeline)与[审计](/common/auditing)。
- 操作契约：`Expose.Ai` 是 `Expose` 的一个标志位，`Expose.All` 包含它，见[操作契约](/web/operations)。
- 测试：`AI.Conformance` 提供 `KnowledgeStoreConformance`、`AIProviderConformance` 与确定性的 `HashingEmbeddingGenerator`，测试不依赖外部模型，见[一致性套件与门禁](/testing/conformance)。自写目录的应用继承 `AIProviderConformance`，实现 `StartAsync`（被测 Provider 是默认）与 `StartWithoutDefaultAsync`（没有默认），能把聊天与嵌入的默认分开时再覆写 `StartWithSeparateDefaultsAsync`。
- 框架不提供 Agent 工厂，等 `Microsoft.Agents.AI` 稳定后另立规范。

## 常见问题

### 怎样控制模型能调哪些工具

两层：操作上没有 `Expose.Ai` 就不进目录；请求的 `Tools` 只列这次要带的名字，没列的模型看不到。再往下是授权：工具以调用方身份执行，没有权限的操作返回 `SECURITY_DENIED_004` 给模型。

### 工具名为什么是 `orders_list` 而不是 `orders.list`

模型工具名只允许字母、数字、`_` 与 `-`，所以 `.` 换成 `_`；太长时截断加哈希。要自己定名字用 `XiHan:AI:Tools:orders.list`，重名或不合法在启动时就失败。

### 换了嵌入模型，旧的知识库还能用吗

不能直接用：向量维度变了，摄入会报 `AI_VALIDATION_002`，检索结果也没有意义。改 `Dimensions`、换一个 `Collection`，重新摄入全部文档。

### 摄入中途失败，知识库会不会一半新一半旧

最多在同一文档内留下新旧混合的块；旧块不会被提前删掉，所以旧文档照常可检索。按返回的错误重试摄入即恢复一致。

### 没有向量库能不能先跑起来

能。开发时用 `Microsoft.Extensions.VectorData` 的内存连接器登记 `VectorStore`；完全不装知识库 Provider 时，摄入与检索返回 `AI_UNAVAILABLE_002`，聊天与工具不受影响。

## 相关包与下一步

- [AI.Abstractions](/packages/ai-abstractions)：工具目录、知识库流水线与契约
- [AI.OpenAI](/packages/ai-openai)：带函数调用循环的聊天客户端与嵌入生成器
- [AI.VectorData](/packages/ai-vectordata)：`IKnowledgeStore` 的向量库适配与健康项
- [Protocols.Mcp](/packages/protocols-mcp)：把操作暴露成 MCP tool
- 下一章：[插件：隔离级别与清单](/plugins/overview)
