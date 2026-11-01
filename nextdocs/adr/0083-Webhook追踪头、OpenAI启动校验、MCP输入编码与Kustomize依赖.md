# ADR-0083 Webhook 的 HTTP 追踪头缺省不传播、只装 OpenAIFeature 时提示词启动校验、MCP stdio 按 UTF-8 读、Kustomize 不起 Kafka 与 Nacos

## 状态

已采纳（`spec/protocol/cloudevents.md`、`spec/rfcs/0022-ai.md`、`spec/protocol/mcp.md`、`spec/rfcs/0016-packaging.md` 随之修订；修订 ADR-0074）。

## 日期

2026-11-01

## 背景

上一轮的决定（ADR-0074、ADR-0076）留下几处待定，`xihan mcp` 的实现另外发现 `Protocols.Mcp` 的一个编码缺陷，用户逐项拍板。本 ADR 记录这几项决定，每项一节。

## Webhook 的 HTTP 追踪头缺省不传播

### 背景

ADR-0074 定下 Webhook 发布者不往信封补内部扩展（含 `traceparent` 扩展），但 HTTP 层另有一条与信封无关的传播：有当前 Activity 时（启用追踪即如此），.NET HttpClient 的主处理器按全局传播器在请求头里写 `traceparent`、`tracestate` 与 `baggage`。ADR-0074 只在文档里写了怎么在命名客户端上关掉，缺省仍写。在带 `tracestate` 与 Baggage 的 Activity 里推一条事件，第三方接收方实际收到 `traceparent`、`tracestate: vendor=internal` 与 `baggage: xihan.tenant = acme`：内部的追踪 ID 与 Baggage 照样出了组织边界。

### 决策

- `CloudEventsProtocolOptions` 新增 `PropagateTraceContext`（配置键 `XiHan:Protocols:CloudEvents:PropagateTraceContext`，缺省 `false`，只接受 `true` / `false`，否则装配时 `ArgumentException`）。
- 为 `false` 时 `CloudEventsProtocolFeature` 以 `UseSocketsHttpHandler` 把命名客户端 `xihan.protocols.cloudevents` 的主处理器设为 `SocketsHttpHandler`，`ActivityHeadersPropagator = null`：W3C 的 `traceparent`、`tracestate`、`baggage` 与旧格式的 `Request-Id`、`Correlation-Context` 都不写。为 `true` 时不碰主处理器，照 .NET 缺省按 `DistributedContextPropagator.Current` 写。
- 只改这一个命名客户端：进程全局的传播器、别的 HttpClient、出站请求自身的客户端跨度都不变。
- 应用之后再用 `UseSocketsHttpHandler` 配置的是同一个处理器实例，缺省保留；用 `ConfigurePrimaryHttpMessageHandler` 整个替换主处理器是显式接管，是否传播由替换后的处理器决定。

### 理由

- 与 ADR-0074 同一条规则：追踪链要不要跨出组织边界由调用方决定，不由框架缺省打开。Baggage 尤其可能带内部标识，上游经 `baggage` 头带进来的条目也会原样外推。
- 用选项而不是让每个应用自己配处理器：缺省就是安全的一侧，打开只要一个配置键，CLI 知识库与文档都查得到。
- 关掉主处理器的传播器，而不是发送时清空 `Activity.Current`：清空之后，有监听器（如 OpenTelemetry 订阅的 `System.Net.Http` 源）时处理器会新起一个根跨度并照样写它的 `traceparent`，客户端跨度也与调用链断开；关掉传播器则跨度照常记录、只是不写头。也不用进程级开关 `System.Net.Http.EnableActivityPropagation`，那会关掉所有客户端的传播。
- 替换了主处理器时不报错：整个换掉主处理器是应用显式接管，测试替身（捕获请求的处理器）也走这条路；文档写明替换后由谁决定。

### 影响

- 行为变化：启用追踪时 Webhook 请求以前带 `traceparent`、`tracestate`、`baggage`，现在不带。接收方依赖这些头关联链路的，设 `PropagateTraceContext = true`。
- 公开 API（Unshipped）：新增 `CloudEventsProtocolOptions.PropagateTraceContext`；新配置键写进 CLI 知识库 `configuration.json`。
- 命名客户端的缺省主处理器从 `HttpClientHandler` 变为 `SocketsHttpHandler`（未打开传播时）。应用若用 `ConfigurePrimaryHttpMessageHandler` 设置代理、证书等，要保持不传播就改用 `UseSocketsHttpHandler`，或在自己的 `SocketsHttpHandler` 上设 `ActivityHeadersPropagator = null`。
- 启动期新增失败：`XiHan:Protocols:CloudEvents:PropagateTraceContext` 不是 `true` / `false`。
- 测试：用本机回环上的真实 Kestrel 接收方断言实际收到的请求头——缺省没有五种追踪头、应用用 `UseSocketsHttpHandler` 调连接参数之后仍然没有、打开后 `traceparent` 带当前追踪 ID 且 `tracestate` 原样；改动之前缺省的两个用例失败（收到三个头）。

## 只装 OpenAIFeature 时提示词也在启动时校验

### 背景

ADR-0074 让提示增强器在创建时取定并试渲染知识库配置引用的模板（`XiHan:AI:Knowledge:PromptName`），由 AI 的宿主特性（`AIToolsFeature`、`VectorDataKnowledgeFeature`）在 `StartAsync` 里解析它，配置错误即启动失败。`OpenAIFeature` 不是宿主特性：只装它（例如只用聊天、知识库增强由应用自己调用）时没有谁在启动时解析增强器，配置错误推迟到第一次解析增强器才抛出。

### 决策

`OpenAIFeature` 改为宿主特性（实现 `IHostedFeature`）：`StartAsync` 解析 `IRagPromptAugmenter`，模板不存在抛 `FrameworkException(AI_NOTFOUND_002)`、引用了 `{context}` / `{question}` 以外的占位符抛 `FrameworkException(AI_VALIDATION_001)`，启动失败；`StopAsync` 无事可做。装配与启动仍不触网，Provider 配置（缺模型名等）仍在第一次解析客户端时才校验，不在本项范围。

### 理由

- 正式包的 AI 特性都调用 `AddAI`、都可能是应用唯一装的 AI 特性；只要其中一个不在启动时解析增强器，「配置错误让应用起不来」就取决于装了哪几个特性。三个特性各自解析一次，单例只建一次，没有额外代价。
- 不另造「AI 启动校验」的通用机制：内核的启动钩子就是 `IHostedFeature`，另两个 AI 特性也是这样做的。
- 一致性包里只供测试的 `HashingEmbeddingFeature` 不改：它不是正式 Provider，测试用例自己决定是否解析增强器。

### 影响

- 公开 API（Unshipped）：`OpenAIFeature` 从 `IFeature` 改为 `IHostedFeature`，新增 `StartAsync`、`StopAsync`。
- 启动期新增失败条件：只装 `OpenAIFeature`（不装 `AIToolsFeature` 与 `VectorDataKnowledgeFeature`）的应用，`XiHan:AI:Knowledge:PromptName` 指向不存在的模板或模板用了别的占位符时启动失败；以前在第一次解析增强器时抛出。
- `OpenAIFeature` 按拓扑顺序参与启动与停止；它的清单（能力、依赖、配置节）不变。
- 测试：只装 `OpenAIFeature` 时缺模板、多占位符都在启动时失败，配置正确时照常启动、增强只用配置的模板；改动之前该用例失败（启动没有抛出）。

## MCP stdio 的标准输入按 UTF-8 解码

### 背景

`Protocols.Mcp` 的 `McpStdioCommand` 用 `Console.In` 读 stdin。Windows 上 stdin 被重定向时，`Console.In` 按控制台输入代码页（`GetConsoleCP`，简体中文系统是 936，英文系统是 437）解码，而 MCP 客户端按 UTF-8 写消息：应用自己的 stdio MCP 服务收到的中文参数是乱码，消息开头带 UTF-8 BOM 时第一条整条解析失败。`xihan mcp` 自己按 UTF-8 读 stdin 避开了，`McpStdioCommand` 没有。

### 决策

- `mcp` 命令打开标准输入的字节流（`Console.OpenStandardInput()`），以 `Encoding.UTF8` 解码，不按 BOM 改换编码（开头的 UTF-8 BOM 跳过、不算内容）。
- 输出不改：服务端用 `Utf8JsonWriter` 的缺省编码器，非 ASCII 字符都转义成 `\uXXXX`，答复只含 ASCII，经宿主给的输出（`Console.Out`）按任何兼容 ASCII 的代码页写出都是同样的字节，即合法的 UTF-8。
- 不改控制台的代码页：`Console.InputEncoding` / `OutputEncoding` 的赋值会调用 `SetConsoleCP` / `SetConsoleOutputCP` 改整个控制台，波及启动它的终端。
- `McpStdioTransport.RunAsync` 仍收 `TextReader`，参数文档写明按 UTF-8 构造、不要直接用 `Console.In`。

### 理由

- MCP 规范规定 stdio 消息是 UTF-8，解码不能取决于运行环境的控制台设置。
- 输出只含 ASCII 是服务端写 JSON 的现有性质，用测试锁住即可；不必绕过宿主给的输出另开标准输出流，命令仍经命令行宿主的输出抽象。
- 测试接缝改为字节流（内部构造函数收 `Func<Stream>`）：用例给的是 UTF-8 字节，与客户端实际写入的一样，解码由命令自己完成，接缝本身覆盖了这处逻辑。

### 影响

- 公开 API 不变（内部构造函数从 `TextReader` 改为 `Func<Stream>`）。
- 行为：在代码页不是 UTF-8 的 Windows 控制台里，stdio MCP 服务收到的非 ASCII 参数现在正确，以前是乱码；Linux、macOS 与代码页 65001 的控制台不受影响。
- 测试：`McpStdioTests.Command_DecodesStdinAsUtf8_AndAnswersInAscii`——带 BOM 的 UTF-8 中文参数原样到达操作，答复只含 ASCII。改动前在代码页 936 下失败（第一条消息按 GBK 解码后不是合法 JSON，-32700）、65001 下通过；改动后两种代码页都通过。`dotnet test` 会把控制台切成 UTF-8，复现要直接运行测试程序（`chcp 936` 后执行测试可执行文件）。

## Kustomize 不在集群里起 Kafka 与 Nacos

### 背景

ADR-0076 让 `xihan pack` 支持 Kafka（`stream`）与 Nacos（`configuration:remote`）绑定：Compose 起单节点 KRaft 与 standalone Nacos，Aspire 用 `AddKafka` 与固定标签的 Nacos 容器，Kustomize 照旧只出 Secret 示例与挂载。Kustomize 要不要也为这两种绑定生成集群内的单节点服务，留作待定。

### 决策

维持现状，定为规则：Kustomize 产物只包含应用自己，六种绑定类型一律只生成 Secret 示例（`type`、`uri`）与挂载，Kafka 与 Nacos 与 PostgreSQL、Redis、RabbitMQ、MinIO 一样不在集群里起服务。代码不变，RFC-0016「绑定的依赖服务」第 4 条、文档站部署与配置指南、CLI README 写明。

### 理由

- 集群里的有状态依赖要按环境定副本数、存储类、备份、升级与安全策略，生成器给不出通用的正确答案；生成一个单节点清单容易被原样用到生产。
- 部署方本来就用各自的 Helm Chart、Operator 或托管服务提供这些依赖，应用只需要按 Service Binding 规范拿到连接。
- 单节点的本地联调形态已经由 Compose 与 Aspire 提供，Kustomize 不必重复。

### 影响

无代码与行为变化。
