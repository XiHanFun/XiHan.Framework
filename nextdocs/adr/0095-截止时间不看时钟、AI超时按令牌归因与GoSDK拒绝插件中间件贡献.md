# ADR-0095 截止时间不看时钟、AI 超时按令牌归因与 Go SDK 拒绝插件中间件贡献

## 状态

已采纳（`spec/rfcs/0004-pipeline-stages.md`、`spec/rfcs/0022-ai.md`、`spec/conventions/error-codes.md` 随之修订）。修订 ADR-0092：第二节备选 4「`DeadlineMiddleware` 另看时钟」定为不做；第一节「影响」里「Go SDK（framework-sdk-go）不改」改为 Go SDK 读清单时同样拒绝。

## 日期

2026-11-02

本 ADR 记录第七批回灌里宿主分区的三项决定（`k-batch-backlog.md` 第 4、5、6 条，来由是 ADR-0092 留下的待定），每项一节。

## 一、截止时间中间件只按截止令牌归因，不另看时钟（第 4 条）

### 背景

ADR-0092 让 `DeadlineMiddleware` 的两条路径（下游抛出的取消、下游写成结果的取消）同一规则：截止令牌已触发即超时，不论调用方令牌是否也已触发。框架的插件运行时随之不再按时钟归因。剩下一个令牌覆盖不到的情形：截止令牌由按 `TimeProvider` 落下的定时器触发，定时器可能晚于时钟读数到达截止时间（线程池繁忙、定时器合并）；调用方恰在这段间隙里取消时，截止令牌还没触发，调用报 `KERNEL_CANCELLED_001`，而按时钟截止时间已经到了。ADR-0092 把「中间件另看时钟」列为备选 4、没有采纳，留作待定。

### 备选

1. 维持只按截止令牌归因，写成已定（采纳）。
2. 中间件另看时钟：截止令牌已触发或时钟已过截止时间即超时。能覆盖定时器晚到的间隙，但令牌之外多一个来源，第四批从内核去掉的时钟判断又回来。

### 决策

- `DeadlineMiddleware` 不变：只认截止令牌，截止令牌没有触发、调用方令牌已触发即 `KERNEL_CANCELLED_001`，不论时钟读数。
- RFC-0004 `DeadlineMiddleware` 一条补「归因不看时钟」，「行为变化」小节新增「不看时钟」一条写明间隙里的情形、代价与收益；docs-next 韧性指南与 Kernel README 同步。不改代码。

### 理由

- 间隙通常不超过一个系统节拍，落在其中且调用方恰好取消的调用，调用方已经放弃，影响只在审计、追踪与日志里看到的错误码。
- 归因只有令牌一个来源：在注入时钟下可以确定地复现（`FakeTimeProvider` 先推时钟再触发定时器，「时钟到了而定时器没触发」本来就构造不出来），两条路径不会再分成两种回答，下游与插件运行时也不必各自再按时钟兜底。
- 与第 5 条的 AI 超时同一形状：谁持有令牌，谁按令牌归因。

### 影响

- 没有代码、公开 API、错误码、配置键与行为变化。
- ADR-0092 第二节的备选 4 由本节定为不做。

## 二、AI Provider 的超时由框架落成令牌，与调用方取消同时发生时报超时（第 5 条）

### 背景

AI Provider 的 `TimeoutSeconds` 以前只由 Provider 自己执行（OpenAI 是 SDK 的 `NetworkTimeout`，作用于每次网络请求），框架在 `AIChat.CompleteAsync` 与知识库的嵌入调用里按「调用方令牌没有触发的取消即超时」推断。这与第六批第 6 条修掉的问题同一形状：

- Provider 的超时与调用方取消同时发生时，OpenAI SDK 先看调用方的令牌、报调用方的取消，框架原样抛出，管道在截止时间没到时记 `KERNEL_CANCELLED_001`；
- Provider 接住取消、交回截断的应答或条数不全的嵌入时，框架当作成功（检索报 `AI_UNAVAILABLE_001`「没有嵌入」，摄入越界抛出）；
- 流里完全不做归因，Provider 的超时以它自己的取消异常抛出，调用方分不出是超时还是取消。

框架看不到 Provider 内部的超时令牌，按 Provider 报出的异常归因就会随调度把同一个超时报成取消。

### 备选

1. 框架在调用 Provider 的位置（`IAIChat`、知识库的嵌入）把 `TimeoutSeconds` 按容器的 `TimeProvider` 落成超时令牌、与调用方令牌链接后交给 Provider，按自己的令牌归因（采纳）。
2. 在 Provider 包里、函数调用循环之内包一层计时的客户端，以 `TimeoutException` 报告超时：时限仍是每次模型请求，但只有遵守约定的 Provider 才统一，第三方 Provider 照旧随调度报取消；还要新增公开的委托客户端类型。
3. 维持推断、只写进规范：同一情形两种回答，按错误码决定重试或统计的一方无法据此行事。

### 决策

- 新增内部类型 `ProviderTimeout`：一次 Provider 调用一个超时令牌（`AIProviderOptions.Timeout`，按注入的 `TimeProvider` 计时），与调用方令牌链接后交给 Provider。
- 时限的范围：完整应答（`CompleteAsync`）从调用 Provider 起到它返回，含 Provider 自己的函数调用循环与其中的工具调用（工具拿到的也是链接令牌）；嵌入是摄入与检索的每一次批调用；流式（`StreamAsync`）是等每个更新（含第一个）的时长，只在等 Provider 时计时，调用方处理更新的时间不算，流的总时长不限。直接从目录解析的客户端不经这一层。
- 归因只看令牌，Provider 抛出与写成结果同一规则：
  1. 超时令牌已触发即超时，不论调用方令牌是否也已触发、Provider 报的是哪个令牌；Provider 接住取消、正常返回的结果同样按超时报告。
  2. 超时令牌没有触发、调用方令牌已触发即取消：抛出的取消原样抛出，正常返回的结果也不交出、改为抛出调用方的 `OperationCanceledException`，经管道时由 `DeadlineMiddleware` 按截止令牌归因。
  3. 两个都没触发：Provider 抛出的取消视为它自己的传输超时，`TimeoutException` 一律超时，其他异常 `AI_UNAVAILABLE_001`，结果原样返回。
- 报告方式：完整应答与嵌入返回 `AI_TIMEOUT_001`；流已交给调用方、不能再返回结果，超时在枚举时抛出 `TimeoutException`（`InnerException` 是 Provider 抛出的异常），调用方取消抛出 `OperationCanceledException`。错误码沿用 `AI_TIMEOUT_001`，没有 AI 自己的取消码（取消交给管道）。
- 公开签名：`AIChat`、`KnowledgeIngestor`、`KnowledgeRetriever` 的构造函数新增 `TimeProvider timeProvider` 参数（PublicAPI.Unshipped 直接改，不留旧构造）。嵌入调用的 Provider 名在没有显式指定时取实际的默认名，错误细节里的 `provider` 不再是空串；目录解析得出客户端却查不到配置时按 `AI_NOTFOUND_001` 报告，不换缺省时限。
- 规范：RFC-0022 新增「超时与取消」一节，聊天、工具、知识库各处引用；`conventions/error-codes.md` 的 `AI_TIMEOUT_001` 说明改写。

### 理由

- 与 `DeadlineMiddleware` 同一个原则：持有令牌的一层按令牌归因，回答不取决于 Provider 的写法与线程调度。框架不持有令牌就做不到确定的归因，所以由框架把 `TimeoutSeconds` 落成令牌，所有 Provider 一样，测试可以用注入时钟确定地覆盖。
- 写成结果按令牌归因：截断的应答、条数不全的嵌入、提前结束的流只看结果分不出来，超时令牌已触发时当作成功交出去，比报超时更糟。
- 流式按每次等待计时：限的是 Provider 卡住，不是生成的总长度；与 OpenAI SDK 的 `NetworkTimeout` 在流式响应上按读取计时的做法相近，长回答不会被总时长截断。
- 备选 2 保住了「每次模型请求」的含义，但统一与否取决于每个 Provider；框架层的令牌对任何 Provider 都成立。

### 影响

- 行为：Provider 的超时与调用方取消同时发生时，由抛出调用方的取消（经管道、截止时间没到时 `KERNEL_CANCELLED_001`，HTTP 408）变为 `AI_TIMEOUT_001`（HTTP 504）；Provider 接住取消交回的截断结果，由成功（或 `AI_UNAVAILABLE_001`、越界异常）变为按令牌的超时或取消；流里的 Provider 超时由 Provider 自己的取消异常变为 `TimeoutException`。
- 时限的含义：完整应答的时限覆盖整个函数调用循环（以前 OpenAI 只限每次网络请求），工具调用多、总时长可能超过 `TimeoutSeconds` 的应用要调大它；OpenAI SDK 的 `NetworkTimeout` 同取 `TimeoutSeconds`，照常作用于每次网络请求。
- 公开签名：三个构造函数多一个 `TimeProvider` 参数，经容器解析的不受影响（内核登记了 `TimeProvider`），直接 `new` 的要补参数。没有新错误码、消息键、配置键与表结构变化。
- 测试：`ProviderTimeoutAttributionTests`（注入时钟；替身 Provider 带与 SDK 同形的自身超时，在闸门处等用例触发完令牌再观察）覆盖完整应答、流式、摄入、检索 × 只有时限、只有调用方取消、时限先到再取消、取消后时限再到 × Provider 抛出、接住取消写成结果，共 32 例，修前 25 例失败、其中「两者都触发」的 16 例全部失败；另 1 例锁住流式只在等 Provider 时计时。

## 三、Go SDK 读清单时拒绝进程外与进程内插件的 `contributes.pipeline`（第 6 条）

### 背景

ADR-0092 第一节让 .NET 的进程外与进程内插件宿主在发现或加载时拒绝写了 `contributes.pipeline` 的清单（`PLUGINS_VALIDATION_001`），Go SDK（framework-sdk-go）没改：它的清单模型对能力包与插件通用，照常读这个字段，Go 写的进程外插件带着永远不会生效的中间件启动，要到宿主发现时才失败，或在一致性套件的用例 1 报出。

### 备选

1. 在 Go 的 `Manifest.Validate` 里按跨字段约束拒绝（采纳）。
2. 只在 `plugin.Serve` 里拒绝：只覆盖进程外运行时，`manifest.Read` / `Write` 仍会读出、写出宿主会拒绝的清单。
3. 维持只在宿主拒绝：作者要部署到宿主才知道。

### 决策

- `Manifest.Validate`：`kind` 为 `plugin`、`isolation` 不是 `wasm` 且 `contributes.pipeline` 非空时返回 `PRIMITIVES_VALIDATION_001`（Go 侧清单校验统一的错误码），消息带清单 ID、隔离级别与声明的中间件名。`manifest.Read`、`ReadFile`、`Write` 与 `plugin.Serve`（启动时读清单）都因此拒绝。能力包、Provider 与 Wasm 插件照常可以写，空列表不算贡献。
- 与 Go SDK 先前校验配置节文法、权限码的做法一致（后者见 ADR-0085）：宿主在发现时拒绝的，Go 侧读清单时同样拒绝。
- 在 framework-sdk-go 本地提交，不动它的 `spec/` 子模块指针；规范不改（RFC-0017「范围」已写明只有 Wasm 插件能贡献中间件），docs-next 插件指南写明 Go SDK 的本地拒绝。

### 理由

- 「不静默」：作者在本地构建或启动插件时就看到错误，而不是部署后才在宿主的健康条目里看到 `Failed`。
- 规则与宿主一致（进程内插件也拒绝），Go 的清单模型不只服务进程外插件。

### 影响

- Go 插件的清单写了非空 `contributes.pipeline`：`plugin.Serve` 返回错误、不监听、不写握手行；`manifest.Read` / `Write` 返回 `PRIMITIVES_VALIDATION_001`。仓库里的示例插件都没写，不受影响。
- 测试：`TestOnlyWasmPluginsMayContributeMiddleware`（进程外、进程内被拒并核对错误码与消息；Wasm 插件、能力包、Provider、空列表接受；`Write` 同样拒绝），`TestServeRejectsBadConfiguration` 补一例，改动前都失败。
