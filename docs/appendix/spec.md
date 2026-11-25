# 规范与多语言

XiHan Framework 的本体不是 .NET 代码，而是一份语言中立的规范：XiHan Spec。.NET 实现是它的参考实现，Go SDK 是第二个实现，其他语言按同一份规范实现并通过同一套一致性测试。这一页讲规范怎样组织、哪些内容是规范性的、一致性测试怎样工作、规范怎样定版与变更，以及各语言 SDK 的现状。要写插件或另一种语言的 SDK 的读者需要读它；只用 .NET 的读者了解即可。

## 为什么要有规范

「同一个名字各语言都能用」这件事，只有两条路：要么每种语言各写一个风格相似的框架，然后在文档里宣称它们是一家；要么先写下语言中立的语义，再让每种实现去通过同一套测试。前者做出来的东西经不起细看，后者才是 OpenTelemetry、MCP、CloudEvents 走通的路。我选了后者。

规范还解决一个 .NET 内部的问题：公开契约先写进规范，再写架构决策记录，再写代码与一致性向量。没有 RFC 的公开 API 变更不合并。这条流程让「随手加一个公开方法」变得不可能，也让每个设计决定都有据可查。

## 规范仓库

规范在独立仓库 [XiHanFun/framework-spec](https://github.com/XiHanFun/framework-spec)，文档都用中文写。它涵盖原语语义与线上编码、清单 Schema、调用上下文与传播、管道阶段与排序、进程外插件协议 `xihan.plugin.v1` 与 Wasm 载体、协议映射、应用清单与打包，以及数据驱动的一致性向量和黑盒运行器。目录结构：

```text
spec/
├─ rfcs/           提案：先 RFC，再 ADR，再实现
├─ conventions/    规范性：规范用语、术语表、包 ID、能力 ID、操作 ID、错误码、Baggage 键、批量优先
├─ manifest/       规范性：xihan.manifest.json 的 JSON Schema
├─ plugin/         规范性：插件协议 plugin.proto 与握手、生命周期、调用文本；Wasm 载体 wasm.md
├─ protocol/       规范性：HTTP、gRPC、MCP、CloudEvents、命令行的映射
├─ packaging/      规范性：应用清单 xihan.app.json 与 xihan pack 的产物映射
├─ provider/       规范性：远程 Provider 的通用规则与各能力的操作集（caching.distributed）
├─ migration/      规范性：xihan migrate 的通用约定（命令、标记、报告格式）与各来源版本的规则集
└─ conformance/    一致性档表（profiles.json）、向量（vectors/）与黑盒运行器（Go 模块）
```

### 引用规范

规范文件按相对路径互相链接，整个仓库可以作为 git submodule 放进实现仓库：

```bash
git submodule add https://github.com/XiHanFun/framework-spec.git spec
git submodule update --init
```

XiHan.Framework 引用到 `framework/spec`，各语言 SDK 引用到 `spec`。`plugin/plugin.proto` 由各语言用自己的 protobuf 工具链生成代码。

## 规范性与信息性

不是所有东西都要各语言一致。规范把内容分成两类：

| 规范性（认领了所属档的实现必须一致） | 信息性（各语言自选） |
| --- | --- |
| 原语语义与线上编码（`Result`、`Error`、`Maybe` 的 JSON 形态） | 内部 API 形态 |
| 清单 Schema | 包管理与目录布局 |
| 上下文传播键与载体 | 日志实现 |
| 管道阶段名与排序规则 | 具体中间件 |
| 插件协议 | 隔离机制 |
| 协议映射（路由推导、错误信封） | 服务器实现 |
| 术语表、错误码、能力 ID、操作 ID、包 ID 命名 | 代码风格 |
| 批量优先：集合入参、原生批量、部分失败语义（`conventions/batch-first.md`） | 批次大小 |

举例说，`orders.create` 在任何语言的实现里都推导为 `POST /orders`，失败时都返回 RFC 9457 的 Problem Details，错误码都是 `<领域大写>_<KIND>_<三位序号>`；但 Go 用 `context.Context` 传递取消、.NET 用 `CancellationToken`，这是各语言自己的事。

一条规则有多强的约束力看它的用语（`conventions/normative-language.md`，ADR-0276），含义与 RFC 2119 的五个关键词相同：

| 用语 | 同组的写法 | 约束力 |
| --- | --- | --- |
| 必须 | 须 | 绝对的要求，不照做就不符合规范 |
| 不得 | 不能、不可、禁止 | 绝对的禁止 |
| 应当 | 应、应该 | 推荐的做法，有站得住的理由可以不照做 |
| 不应当 | 不应、不应该 | 不推荐的做法，有站得住的理由可以这样做 |
| 可以 | 可、可选 | 任选，做与不做都符合规范，两边仍然要能互通 |

规范性内容里描述行为、线上格式或取值的陈述句与「必须」同等约束，正文不逐句加这个词。要求落在行为上，不落在名字上：正文里的 .NET 类型名是参考实现落实这条规则的地方，别的语言不必同名。

规范性内容又按一致性档划分（RFC-0024）。一个实现认领若干档，只对认领的档负责；档表是规范仓库的 [`conformance/profiles.json`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/profiles.json)，每个 RFC 的状态行写着它属于哪些档：

| 档 | 内容 | 谁认领 |
| --- | --- | --- |
| `core` | 所有实现都认领：原语与线上编码、清单读写、调用上下文、管道阶段与排序、消息模板、操作契约与载荷、命名约定 | 所有实现 |
| `plugin` | 用本实现写进程外插件：握手、生命周期、调用与回调的插件一侧，并通过黑盒运行器 | 六个 SDK |
| `host` | 宿主：能力求解、进程外插件宿主、协议映射、应用清单与打包、版本迁移 | .NET |
| `wasm` | Wasm 插件 ABI（Experimental） | .NET |
| 子系统档 | 一个子系统 RFC 一档，按能力 ID 前缀命名：`persistence`、`caching`、`multitenancy`、`security`、`messaging`、`localization`、`objectstorage`、`scheduling`、`configuration`、`auditing`、`workflow`、`ai`、`observability` | .NET |
| `provider:<操作集>` | 远程 Provider 的操作集，目前只有 `provider:caching.distributed@1` | .NET（适配器）、Go（插件） |

## 一致性测试

一致性有两层：

| 层 | 位置 | 形式 | 谁用 |
| --- | --- | --- | --- |
| 数据驱动向量 | `conformance/vectors/` | JSON 文件，按主题分目录（`primitives/`、`manifest/`、`context/`、`pipeline/`、`operations/`、`caching/`、`plugin/`、`security/`、`provider/`，以及各子系统的目录）：`Result` 与 `Error` 编码、清单校验、版本解析、管道排序、上下文传播、包 ID 与能力 ID 文法、操作 ID 的文法与 128 个字符的上限（`primitives/operation-ids.json`）、HTTP 载体从请求头读出的截止时间（`context/http-deadline.json`）、缓存键与键前缀必须接受与必须拒绝的样例（`caching/cache-keys.json`），以及插件的握手行、回调端点、截止时间、回调状态的映射、错误的读入、下发给插件的绑定（`plugin/binding-properties.json`：属性的文法与上限，按清单的 `required` 与类型核对，绑定目录的文件大小与条目数上限，整个 `Configure` 请求 2097152 字节的两侧）、插件发布的信封的 `time`（`plugin/envelope-times.json`，档 `host` 与 `plugin`：宿主读得出与必须拒绝的写法、宿主写给插件的写法，前两类对 SDK 的读入同样适用）与 SDK 一侧读写的信封 `time`（`plugin/sdk-envelope-times.json`，档 `plugin`：按纳秒、100 纳秒、微秒、毫秒四种时间类型精度各给写出的文本与读出的时刻，另有必须读不出的文本；期望值由一致性模块的规则算出，100 纳秒一列另由 .NET 核对） | 各语言 SDK 的单元测试直接读取这些文件；Go 可以 `go get github.com/xihanfun/framework-spec/conformance` 后经内嵌的 `conformance.Vectors` 读取 |
| 黑盒运行器 | `conformance/cmd/xihan-conformance` | 语言中立，以宿主身份启动任意语言写的进程外插件；运行器本身用 Go 写成 | 插件作者、远程 Provider 作者 |

安装运行器：

```bash
go install github.com/xihanfun/framework-spec/conformance/cmd/xihan-conformance@latest
```

| 命令 | 作用 |
| --- | --- |
| `xihan-conformance plugin --dir <插件目录>` | 按 `plugin/handshake.md`、`lifecycle.md`、`invocation.md` 核对握手行与版本协商、无共同版本时拒绝、会话令牌（含两个令牌值）、清单一致性、Configure / Start / Health / WatchHealth / Stop 与宽限内退出、调用次序不对时 `FailedPrecondition`、未知操作 `Unimplemented`、声明的操作都返回 `status`、`Start` 之前的调用与投递报 `plugins.process.unavailable`、订阅投递确认、没人订阅或方式未知的投递报 `plugins.event.unsubscribed` 与 `InvalidArgument`、`InvokeStream` 的第一帧、配置节不是 JSON 对象时进入 `Failed`、`Start` 之前的 `Stop` 应答后退出、stdin 关闭即退出；每个插件都以两项的 `XIHAN_PLUGIN_HOST_ADDRESS` 启动 |
| `xihan-conformance vectors` | 校验向量文件本身 |

子系统的一致性档（消息、多租户、对象存储、调度、本地化、配置、持久化、审计、工作流、可观测、AI）各有向量，收的是正文已经写明、输入输出确定的规则：主题、租户 ID、容器名、对象键与保存的文件名（`objectstorage/download-file-names.json`）、对象的内容类型（`objectstorage/blob-content-types.json`：`;` 之后可以为空，不以空格结尾，制表符不收）、信封扩展属性名的文法与保留名（`messaging/envelope-extensions.json`）、作业名这些名字的文法与上限，计划的文本形式（`scheduling/schedule-texts.json`），cron 表达式的推算（含各段步长的上限两侧）、重试与退避的间隔、区域名的协商与回退、雪花标识的位布局、Linux 上读运行信息的取法这类换算，工作流表达式的求值（`workflow/` 下五个 `expression-*.json`，含 `format`、`upper` / `lower` 的简单大小写映射与一次求值的工作量上限，读数字按它的写法的长度计，`len` 读字符串按长度计，`isNullOrEmpty` 只读到能判定为止）、列表关键字的折叠（`workflow/keyword-matching.json`，Unicode 简单小写映射），以及 Tenant 阶段的裁定、审计事件的选择这类判定表。要有运行中的存储或代理才能核对的行为不在向量里，由下面的 .NET 套件覆盖。

向量的期望值由 .NET 参考实现的程序算出，不手算，也不取某个 SDK 的输出。例外是插件一侧（`plugin` 档）没有 .NET 实现的向量：期望值由 Go 一致性模块（`conformance/rules`，不是 Go SDK）的规则函数算出，再用 .NET 能用的部分与各语言的标准库交叉核对，向量顶部的 `$comment` 注明算出期望值的规则函数，以及哪几项由 .NET、哪几项由哪种语言的标准库核对，`plugin/sdk-envelope-times.json` 就是这样做的（ADR-0310）。

各向量文件的内容、规则包、运行器的用例与选项见规范仓库的 [conformance/README.md](https://github.com/XiHanFun/framework-spec/blob/main/conformance/README.md)。

.NET 侧另有每个子系统的 `<领域>.Conformance` 包，是 xUnit 套件：默认实现与 Provider 都要通过，远程 Provider 跑同一套。用法见[一致性套件与门禁](/testing/conformance)。

## 规范的版本

规范以独立的语义化版本 `vX.Y.Z` 打标签，与框架的版本线分开（RFC-0024）；Go 的一致性模块在同一提交另打 `conformance/vX.Y.Z`：

- PATCH 不改规范性内容。
- MINOR 只增，或把以前不作规定的行为写死，但不能让按较早 MINOR 实现的对端互通失败。
- MAJOR 改已经写明的行为、删除内容或线上不兼容；`v2` 与 `v1` 并存，对应原语层的冻结规则（RFC-0001）。

大版本与线上的数字一致：`FrameworkInfo.SpecVersion`、清单的 `primitives` 都是 `1`。`v1.0.0` 之前打 `v1.0.0-rc.N`，rc 之间允许任何改动；`v1.0.0` 与 XiHan.Framework 的 5.0.0 正式版一起打。改动记在规范仓库的 [CHANGELOG.md](https://github.com/XiHanFun/framework-spec/blob/main/CHANGELOG.md)。

规范的日常提交在 `dev`，`main` 由 `dev` 快进，标签打在 `main` 上。实现的日常分支可以指向规范已推送的任意提交；发版时子模块必须正好在规范标签上，正式版只钉正式版标签。

## 各语言 SDK

| 语言 | 包名 | 仓库 | 认领的档 | 成熟度 |
| --- | --- | --- | --- | --- |
| .NET | `XiHan.Framework.*` | [XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework) | `plugin` 之外的全部档：`core`、`host`、`wasm`、全部子系统档、`provider:caching.distributed@1`（适配器） | Reference |
| [Go](/sdks/go) | `github.com/xihanfun/framework-sdk-go` | [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) | `core`、`plugin`、`provider:caching.distributed@1`（插件） | Experimental |
| [TypeScript](/sdks/typescript) | npm `xihan-framework` | [framework-sdk-typescript](https://github.com/XiHanFun/framework-sdk-typescript) | `core`、`plugin` | Experimental |
| [Rust](/sdks/rust) | crate `xihan-framework` | [framework-sdk-rust](https://github.com/XiHanFun/framework-sdk-rust) | `core`、`plugin` | Experimental |
| [Java](/sdks/java) | `com.xihanfun:xihan-framework` | [framework-sdk-java](https://github.com/XiHanFun/framework-sdk-java) | `core`、`plugin` | Experimental |
| [Python](/sdks/python) | `xihan-framework` | [framework-sdk-python](https://github.com/XiHanFun/framework-sdk-python) | `core`、`plugin` | Experimental |
| [PHP](/sdks/php) | `xihanfun/xihan-framework` | [framework-sdk-php](https://github.com/XiHanFun/framework-sdk-php) | `core`、`plugin` | Experimental |

七个实现在认领的档里都没有缺口。六个 SDK 都提供原语、清单、调用上下文、管道、操作契约与进程外插件运行时，Go 还用于远程 Provider；它们都已有钉在规范 rc 标签上的预发布版，还是 Experimental：Stable 要求钉在规范正式版标签上的发布版。

成熟度按认领的档判定（RFC-0024）：

- Reference：.NET。
- Stable：认领的档里每个向量都回放并通过，运行器全部通过，且有钉在规范正式版标签上的发布版。
- Experimental：有没回放的向量（缺口），或还没有这样的发布版。
- Community：社区维护。

SDK 仓库按 `framework-sdk-<语言>` 命名（ADR-0010）。除 .NET 参考实现外，每种语言的 SDK 只有一个包，包名都是 `xihan-framework`，需要写所有者的生态用 `xihanfun`；Go 的模块路径必须是仓库地址，所以跟着仓库名。

各语言的领域词与术语表一致，只换大小写风格。

为什么 Go 先行：它离 C# 最远，能证明规范不是 .NET 形状的；同时它适合承载进程外插件、黑盒运行器和将来的 Kubernetes Operator。Go SDK 已经做到的事：用 Go 写的插件在 .NET 宿主里服务一个操作，崩溃时宿主降级而不退出；用 Go 实现的分布式缓存经远程 Provider 适配器通过 .NET 的缓存一致性套件。示例见 `framework/samples/EchoGoPlugin` 与 `CacheGoPlugin`，写法见[进程外插件](/plugins/out-of-process)。

## 变更流程

公开契约的变更按固定顺序走 RFC 流程：

1. 在 `rfcs/` 新建 `NNNN-<slug>.md`，状态为 `草案`。
2. 评审通过后写架构决策记录（XiHan.Framework 的 `plans/adr/`），RFC 状态改为 `已采纳`，正文并入对应的规范性目录。
3. 实现与一致性向量随 RFC 一起合并；没有 RFC 的公开契约变更不合并。
4. 改动规范性内容的提交在 `CHANGELOG.md` 的「未发布」一节记一条，标明 PATCH、MINOR 或 MAJOR 与 ADR（RFC-0024）；新的规范文件与向量要写进档表。

新增术语表条目同样走 RFC。错误码、能力 ID、Baggage 键、操作 ID 一经发布不改；能力登记表里已经登记、还没有任何实现提供的 ID 标成「保留，尚无实现」，不删、不挪作他用，落地时走 RFC。提交前的门禁与写法约定见规范仓库的 [AGENTS.md](https://github.com/XiHanFun/framework-spec/blob/main/AGENTS.md)。

## 已采纳的 RFC

参考实现的路径相对 XiHan.Framework 的 `framework/src`。

| RFC | 主题 | 参考实现与修订 |
| --- | --- | --- |
| [0001](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0001-primitives.md) | 原语层 | 2026-11-26 写明：写出与转交的 JSON 文本同样在字符串与 UTF-8 之间严格转换，两个方向都不换成 U+FFFD，以字符串给出的孤立代理项、以字节收到的非法序列都按那一处不合法的 JSON 处理（Wasm 宿主解码客体消息、命令行 `--json`、MCP stdio 的一行、`xihan setup` 读配置）；同日再写明：写出与转交扩到 SDK 以字符串交回的应答载荷与发布的 `data`、宿主转交的插件应答载荷（转交之前过共同检查），以及写进 JSON 的命令行字段选项、插件配置节与信封属性（有孤立的代理项即按那一处不合法的输入处理），末句只排除上面列出之外的写出器；2026-11-25 修订：.NET 映射里 JSON 文本检查的入口改名 `JsonText.FindProblem`，通过时返回无值的 `Maybe`，问题是 `JsonTextProblem { Kind, Path }`、按出处分五个种类，规则与报告的位置不变；同日写明：以字符串交来的 JSON 文本先严格编码成 UTF-8，孤立的代理项不合法；2026-11-24 修订：收调用上下文的契约不另收取消令牌，取消与截止时间只在上下文的令牌上；.NET 映射补 `DeadlineText` 与 `ActivationEvent`；「JSON 文本」的适用范围加上消息信封，.NET 映射补 `TimestampText` 与内核的 `JsonText`；参考实现 `XiHan.Framework.Primitives` |
| [0002](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0002-manifest.md) | 清单 | 2026-11-25 写明：`bindings[].required`（缺省 `true`）对插件宿主同样有效；同日修订：装配特性的绑定核对与插件宿主同一套规则，只给了类型的必需绑定算没给，可选的绑定也核对类型；2026-11-24 修订：`activation` 里 `onEvent:` 的模式可以带下划线，与主题文法同一字符集，`onOperation:` 不变；参考实现 `Kernel.Capabilities`，激活事件的文法在 `Primitives` 的 `ActivationEvent` |
| [0003](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0003-invocation-context.md) | 调用上下文与传播 | 2026-11-24 修订：截止时间的文本只有一条文法（规则 8），HTTP 的 `xihan-deadline` 与插件元数据的 `deadline` 共用，读不出当作没有；规则 8 同时是消息信封 `time` 的文法；截止时间不为空而读不出时宿主应当记一条不含原值的限频告警（`protocol/http.md`、`protocol/grpc.md`、`plugin/invocation.md`）；参考实现 `Primitives` 的 `DeadlineText`，载体映射随协议适配器实现 |
| [0004](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0004-pipeline-stages.md) | 管道阶段与排序 | 参考实现 `Kernel.Pipeline` |
| [0005](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0005-persistence.md) | 领域协议与持久化 | 2026-11-24 修订：新增「管道外的工作单元」，单例存储、后台循环与健康探针开工作单元的开始与收尾只有一种写法；参考实现 `Domain`、`Persistence.Abstractions`，套件 `Persistence.Conformance` |
| [0006](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0006-caching.md) | 缓存、分布式锁与幂等 | 2026-11-24 修订：`GetOrCreateAsync` 在同一个缓存实例上合并同键并发，只回源一次；键与键前缀的长度按 UTF-8 编码后的字节数计，不超过 512 字节，代数缓存区的区名与直写的范围、条目各不超过 128 字节；参考实现 `Caching.Abstractions`、`Caching.InMemory`、`Caching.Redis`、`Caching.Hybrid`，套件 `Caching.Conformance` |
| [0007](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0007-multitenancy.md) | 多租户 | 2026-11-24 修订：租户解析器 `ResolveAsync(context)` 不另传取消令牌；参考实现 `MultiTenancy.Abstractions`、`MultiTenancy.InMemory`，套件 `MultiTenancy.Conformance`；Provider `MultiTenancy.Database` |
| [0008](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0008-security.md) | 认证与授权 | 2026-11-24 修订：凭据处理器改名 `ICredentialHandler` 且不另传取消令牌，策略判定的结果改名 `AuthorizationDecision`；授权要求的记法改成 `AuthorizationRequirement { Policy, Resource? }`；参考实现 `Security.Abstractions`、`Security.InMemory`、`Security.Jwt`、`Security.OpenIdConnect`，套件 `Security.Conformance`；Provider `Security.Database`、`Security.Ip2Region`（IP 归属地） |
| [0009](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0009-messaging.md) | 消息、CloudEvents 信封与发件箱 | 2026-11-26 写明：规范属性与扩展属性的字符串值里有孤立的代理项时，写出一侧在发布时就拒绝（.NET 的 `CloudEventsFormat.Encode` 抛出携带 `MESSAGING_VALIDATION_002` 的异常），不写成 U+FFFD；2026-11-25 修订：扩展属性名排除保留名（CloudEvents 的规范属性名与结构化 JSON 格式的成员名），构造信封、设扩展属性时就拒绝，`protocol/cloudevents.md` 的 `ce-data` 头不合法；信封承载 CloudEvents 的可选属性 `dataschema`（非空字符串，与 `source` 一样不核对 URI 语法），结构化 JSON 的同名成员、binary 的 `ce-dataschema` 头与插件协议的 `Envelope.data_schema` 原样收发；Redis 的消费循环收到停止信号之后不再发起新的认领与读取，被放弃的条目退回投递次数时空闲时间设到回收阈值，别的实例下一次维护就接手；2026-11-24 修订：一次投递尝试的了结只有一种写法，被停止打断或被管道拒绝的投递不计尝试，无法解码的消息竞争订阅进死信、广播订阅丢弃；发件箱新增死信重投与健康项；处理器不另传取消令牌；信封的读取收紧：先过 JSON 文本的共同检查，`time` 与截止时间同一条文法、读成 UTC，`data` 与 `data_base64` 互斥，扩展属性的值是字符串、数字或布尔，写出的文档必须读得回来；发件箱的退避对任何次数都饱和在上限；被放弃的投递再到达时尝试号不因这次放弃而超过上限，Redis 放弃时退回投递次数；参考实现 `Messaging.Abstractions`、`Messaging.InMemory`、`Messaging.RabbitMQ`、`Messaging.Kafka`、`Messaging.Redis`，套件 `Messaging.Conformance` |
| [0010](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0010-localization.md) | 本地化：默认英文与 10 种内置区域 | 2026-11-24 修订：取文案先按区域、再按目录；参考实现 `Localization.Abstractions`、`Localization.InMemory`；Provider `Localization.Database`，套件 `Localization.Conformance` |
| [0011](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0011-operations.md) | 操作契约与 XGI 消息 | 2026-11-24 修订：操作 ID 不超过 128 个字符，清单 Schema 同步设上限；弃用的操作在 `.proto` 里带 `option deprecated = true;`、命令行调用时在 stderr 写一行提示（`protocol/grpc.md`、`protocol/commandline.md`）；参考实现 `Kernel.Operations`，协议适配器在 `Protocols.*` |
| [0012](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0012-objectstorage.md) | 对象存储 | 2026-11-26 修订：内容类型不能以空格结尾（RFC 9110 的字段值不含首尾空白，对所有存储成立）；「S3 Provider」照 RustFS 的实测写明签名按合并空白之后的值计算，只差在连续空格的写法验签通过而被改写，照样以 `OBJECTSTORAGE_VALIDATION_007` 拒绝；S3 上传分片的应答没有可用的 `ETag`、列出分片的应答里分片缺可用的编号或 `ETag`、长度为负时返回 `OBJECTSTORAGE_UNAVAILABLE_001`；同日再修订：S3 列出分片的分页不前进或超过 10000 片、列举截断却没有可用的续页标记、批量删除的错误项认不出键、读取与写入的应答缺 `ETag` 或修改时间、条件写入探测里发起分片上传的应答缺上传标识，都按不合规的应答处理（`OBJECTSTORAGE_UNAVAILABLE_001`，探测照常启动、后台重试），写入结果的修改时间是 Provider 记下的写入时刻、不要求与之后读出的相同；宿主代收的上传端点照 RFC 9110 先去掉 `Content-Type` 首尾的空格与制表符再比较、核对与取用，去掉之后为空即当作没给；2026-11-25 修订：S3 命名实例在设置里配代理要运营方打开 `NamedInstancesAllowProxy`，缺省实例只给一项密钥是配置错误，服务端点不合法时报错不带原值，服务端点与绑定 `objectstorage` 的主值不能带用户信息、凭据只走密钥设置；发起分片上传的应答没有可用的上传标识时返回 `OBJECTSTORAGE_UNAVAILABLE_001`；内容类型照 RFC 9110 收空参数（取代 2026-11-24 的「`;` 之后必须有参数」），内容类型原样存取，存储客户端写不出原值的以 `OBJECTSTORAGE_VALIDATION_007` 失败，「S3 Provider」写明 AWS SDK 的改写与读取一侧的已知缺口；2026-11-24 修订：分片上传的放弃与命名实例的移除改为批量、逐项结果，命名实例设登记数上限，`S3BlobStore` 不再有公开构造；对象键、查询前缀与保存的文件名的长度按 UTF-8 字节计，内容类型的每个 `;` 之后必须有参数；S3 Provider 新增客户端的设置（会话令牌、请求超时、尝试次数与 HTTP 代理），命名实例配了代理时过出站地址策略的是代理的地址；参考实现 `ObjectStorage.Abstractions`、`ObjectStorage.FileSystem`、`ObjectStorage.S3`，套件 `ObjectStorage.Conformance` |
| [0013](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0013-scheduling.md) | 调度与领导选举 | 2026-11-25 修订：cron 的步长不超过这一段能写的最大值（秒、分 59，时 23，日 31，月 12，周 7，年 2099）；新增「计划的文本形式」，各部分之间只认单个空格，间隔、次数、cron 与选项的读法与写法相同、选项次序固定且不能重复，时刻按 RFC-0003 规则 8 读，时区名是本机查得到的 IANA 名，新增向量 `scheduling/schedule-texts.json`；2026-11-24 修订：调度器与作业存储的查询方法补上动词（`ListRunsAsync`、`FindStatusesAsync` 等）；cron 的文法与推算逐条写明，只按空格与制表符分段，搜索上限按下一次的墙上时间判定（不超过 1830 天）；2026-10-26 修订：作业存储、动态作业、多触发器、misfire、暂停、历史、集群分发；参考实现 `Scheduling.Abstractions`、`Scheduling.InMemory`、`Scheduling.Database`，套件 `Scheduling.Conformance` |
| [0014](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0014-configuration.md) | 远程配置与密钥 | 2026-11-24 修订：Nacos 的两种文档格式逐条写明，展平后重复的键与空的键不合法；「选项里的凭据」把 `ProxyUserName` 列入登录用户名；参考实现 `Configuration.Abstractions`、`Configuration.Nacos`，套件 `Configuration.Conformance`；默认实现是 .NET 原生文件与环境变量，无包 |
| [0015](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0015-auditing.md) | 审计 | 参考实现 `Auditing.Abstractions`、`Auditing.InMemory`，套件 `Auditing.Conformance`；Provider `Auditing.Database` |
| [0016](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0016-packaging.md) | 应用清单与打包 | 2026-11-26 修订：根目录下以 `.` 开头的子目录不是绑定，不读、不计入 128 个绑定，写明绑定目录里的子目录不是属性、目录项可以是符号链接（名字取目录项自己的，内容按链接最终指向的读，Kubernetes 的 projected volume 与 Secret 卷都读得出）；同日写明：`xihan setup` 要合并的配置不是合法的 UTF-8 也是不合法的 JSON；2026-11-25 修订：Kustomize 的绑定 Secret 样板为 `blob` 列出 `sessionToken`；绑定目录的文件有大小上限（一个文件 524288 字节、一个绑定合计 1048576 字节，超限 `KERNEL_HOSTING_005`），根目录下最多 128 个绑定、一个绑定目录最多 128 个文件（先数后读，超限同样是 `KERNEL_HOSTING_005`），下发给隔离运行的插件之前核对来源的类型与清单的必需，装配特性时的核对与它同一套规则；进程内插件的宿主与插件用应用指定的绑定根目录；2026-11-24 修订：下发给隔离运行的插件的绑定按同一套规则解析；应用清单 Schema 收紧到与字段表一致，`name`、绑定的 `secret` 与 `leader.lease` 都是 DNS 标签，Schema 表达不了的约束另列一节、由 `xihan pack` 读取时核对；下发给隔离运行的插件的绑定带上属性；`blob` 绑定的属性补 `sessionToken`；参考实现 `Cli` 的 `xihan pack`；规范 `packaging/` |
| [0017](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0017-plugin-protocol.md) | 进程外插件协议 `xihan.plugin.v1` | 2026-11-26 修订：SDK 读写 `Envelope.time` 的向量 `plugin/sdk-envelope-times.json`（档 `plugin`），`plugin/envelope-times.json` 也进 `plugin` 档；.NET 映射里绑定核对的入口改名 `Kernel.Hosting.BindingRules`；同日写明：SDK 以字符串给出的 `Host.Invoke` 载荷发出之前严格编码成 UTF-8，编码不成即不发请求、在本地以 `KERNEL_VALIDATION_001` 失败；同日再写明：宿主核对插件交回的应答载荷，不为空而过不了 JSON 文本的共同检查即这次调用 `PLUGINS_UNEXPECTED_001`，不按崩溃处理、计入熔断、幂等回放做同一道检查；SDK 以字符串交回的应答编码不成为 `KERNEL_UNEXPECTED_001`，`Host.Publish` 以字符串给出的 `data` 编码不成的那一项在本地以 `MESSAGING_VALIDATION_002` 失败、其余照发；宿主写出的配置节有孤立的代理项即配置失败；`plugin-messages.json` 加两条宿主一侧的 `reason`；2026-11-25 修订：`plugin/lifecycle.md` 写明下发之前按清单核对必需与来源的类型（下发的 `type` 总是清单的）、整个 `Configure` 请求按 protobuf 编码不超过 2097152 字节、插件 gRPC 服务端的接收上限不小于 4194304 字节、绑定目录的文件超限或多于 128 个文件时配置失败，进程内宿主在加载前做同样的必需与类型核对、用应用指定的绑定根目录；`plugin/invocation.md` 写明 `Envelope.time` 与 RFC-0009 的信封同一条文法，插件发布的不合文法即那一项不发布，新增向量 `plugin/envelope-times.json`；`plugin.proto` 的 `Envelope` 新增 `data_schema`（字段 11，同一协议版本），承载信封的 `dataschema`，空串表示没有；SDK 把 `Envelope.time` 读成各语言的时间类型（更细的小数截断），读不出以 gRPC `InvalidArgument` 拒绝投递，发布时换算成 UTC、以 `Z` 结尾写出；2026-11-24 修订：`plugin/lifecycle.md` 写明下发给插件的绑定按 RFC-0016 解析、在每次配置插件时解析；`plugin/invocation.md` 写明 Span `xihan.plugin.invoke` 的父级、标签与状态；`plugin.proto` 的 `Binding` 新增 `properties`，只有属性的绑定照常下发、主值为空，属性名的文法与上限写在 `plugin/lifecycle.md`；`plugin/invocation.md` 写明回调的 `deadline` 读不出时的限频告警；规范 `plugin/`；参考实现 `Plugins.OutOfProcess`（协议代码在 `Plugins.Protocol`），套件 `Plugins.Conformance` |
| [0018](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0018-wasm-plugins.md) | Wasm 插件 ABI（Experimental）：同一套消息经导出函数 + 线性内存 | 2026-11-26 写明：宿主严格按 UTF-8 解码客体写来的消息，有非法字节序列的按不是 JSON 处理；2026-11-24 修订：只写在 RFC 里的规则并入 `plugin/wasm.md`，RFC 改为引用，规则不变；规范 `plugin/wasm.md`；参考实现 `Plugins.Wasm`，套件 `Plugins.Conformance` |
| [0019](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0019-remote-providers.md) | 远程 Provider：子系统能力经插件操作提供，首个操作集 `caching.distributed@1` | 规范 `provider/`；适配器 `Caching.Remote` |
| [0020](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0020-migration.md) | 版本迁移：`xihan migrate <来源版本>` 按规则集做语法级重写，不做运行期兼容桥 | 规范 `migration/README.md`；实现 `XiHan.Framework.Cli` |
| [0021](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0021-workflow.md) | 工作流：持久化执行 | 2026-11-26 修订：工作量里 `len` 读字符串按长度计（以前只读数字），`isNullOrEmpty` 只读到能判定为止，非空的字符串记 1，空串、数字、布尔与容器不读；2026-11-25 修订：表达式新增内置函数 `format(x, n)`；一次求值的工作量上限 16777216 与计数规则，新增消息键 `workflow.expression.budget_exceeded`；关键字的折叠写明是逐个码位的 Unicode 简单小写映射（`İ` 折成 `i`，平面以外的字母同样折叠）；`Delay.Until`、`UserTask.DueAt` 与按时间读的值改用表达式的时间文法、读成 UTC；`upper` / `lower` 改用与关键字折叠同一份的 Unicode 简单大小写映射，结果以实现所带的 Unicode 数据为准；读数字按它的写法的长度计入工作量，表达式得出的数字写成 JSON 是数字的文本；`Workflow.Database` 写明查询列的回填；2026-11-24 修订：「表达式」逐条写明词法、文法、运算符、内置函数、数字与时间的文本、模板、上限与错误，数字的文本只看数值、时间是定宽的字符串、容器的文本是成员按名字排序的紧凑 JSON；2026-10-26 修订：触发式启动、事件网关、外部任务、HTTP 与补偿活动、跳转与迁移、认领 / 退回 / 催办、提醒与审批阈值、保留期清理；参考实现 `Workflow.Abstractions`、`Workflow.InMemory`、`Workflow.Database`，套件 `Workflow.Conformance` |
| [0022](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0022-ai.md) | AI：聊天、嵌入、工具与知识 | 2026-11-24 修订：提示词模板的渲染、工具名的文法与超长时的前缀写明，固定窗口分块按码位计并去掉每块首尾的空白；参考实现 `AI.Abstractions`、`AI.OpenAI`、`AI.VectorData`，套件 `AI.Conformance` |
| [0023](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0023-observability.md) | 可观测性：导出与运行信息 | 2026-11-24 修订：新增「内核的遥测」：Span `xihan.invoke` 与 `xihan.plugin.invoke`、计数器与直方图，标签 `xihan.operation` 只取登记表里的操作 ID；「平台读取路径」逐条写明 Linux 与 Windows 各项的取法；参考实现 `Observability.Abstractions`、`Observability.Host`、`Observability.OpenTelemetry`；运行信息不启动外部命令，取不到的项见正文 |
| [0024](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0024-versions-and-profiles.md) | 规范的版本与一致性档 | 2026-11-24 修订：新增「规范用语」，定义在 `conventions/normative-language.md`；档表 `conformance/profiles.json`，由 `tools/speccheck` 与 conformance 模块的测试核对；.NET 由架构测试 `SpecVectorCoverageTests` 按认领的档核对向量 |

## 下一步

- [核心概念](/getting-started/concepts)：这些规范在 .NET 实现里对应什么
- [进程外插件](/plugins/out-of-process)：用任意语言按规范写插件
- [贡献指南](/appendix/contributing)：怎样提一个 RFC
