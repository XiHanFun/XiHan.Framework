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
├─ conventions/    规范性：术语表、包 ID、能力 ID、操作 ID、错误码、Baggage 键、批量优先
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
| 数据驱动向量 | `conformance/vectors/` | JSON 文件，按主题分目录（`primitives/`、`manifest/`、`pipeline/`、`operations/`、`plugin/`、`security/`、`provider/`）：`Result` 与 `Error` 编码、清单校验、版本解析、管道排序、上下文传播、包 ID 与能力 ID 文法，以及插件的握手行、回调端点、截止时间、回调状态的映射与错误的读入 | 各语言 SDK 的单元测试直接读取这些文件；Go 可以 `go get github.com/xihanfun/framework-spec/conformance` 后经内嵌的 `conformance.Vectors` 读取 |
| 黑盒运行器 | `conformance/cmd/xihan-conformance` | 语言中立，以宿主身份启动任意语言写的进程外插件；运行器本身用 Go 写成 | 插件作者、远程 Provider 作者 |

安装运行器：

```bash
go install github.com/xihanfun/framework-spec/conformance/cmd/xihan-conformance@latest
```

| 命令 | 作用 |
| --- | --- |
| `xihan-conformance plugin --dir <插件目录>` | 按 `plugin/handshake.md`、`lifecycle.md`、`invocation.md` 核对握手行与版本协商、无共同版本时拒绝、会话令牌（含两个令牌值）、清单一致性、Configure / Start / Health / WatchHealth / Stop 与宽限内退出、调用次序不对时 `FailedPrecondition`、未知操作 `Unimplemented`、声明的操作都返回 `status`、`Start` 之前的调用与投递报 `plugins.process.unavailable`、订阅投递确认、没人订阅或方式未知的投递报 `plugins.event.unsubscribed` 与 `InvalidArgument`、`InvokeStream` 的第一帧、配置节不是 JSON 对象时进入 `Failed`、`Start` 之前的 `Stop` 应答后退出、stdin 关闭即退出；每个插件都以两项的 `XIHAN_PLUGIN_HOST_ADDRESS` 启动 |
| `xihan-conformance vectors` | 校验向量文件本身 |

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

七个实现在认领的档里都没有缺口。六个 SDK 都提供原语、清单、调用上下文、管道、操作契约与进程外插件运行时，Go 还用于远程 Provider；它们还是 Experimental，因为还没有钉在规范标签上的发布版。

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

新增术语表条目同样走 RFC。错误码、能力 ID、Baggage 键、操作 ID 一经发布不改。提交前的门禁与写法约定见规范仓库的 [AGENTS.md](https://github.com/XiHanFun/framework-spec/blob/main/AGENTS.md)。

## 已采纳的 RFC

参考实现的路径相对 XiHan.Framework 的 `framework/src`。

| RFC | 主题 | 参考实现与修订 |
| --- | --- | --- |
| [0001](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0001-primitives.md) | 原语层 | 参考实现 `XiHan.Framework.Primitives` |
| [0002](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0002-manifest.md) | 清单 | 参考实现 `Kernel.Capabilities` |
| [0003](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0003-invocation-context.md) | 调用上下文与传播 | 载体映射随协议适配器实现 |
| [0004](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0004-pipeline-stages.md) | 管道阶段与排序 | 参考实现 `Kernel.Pipeline` |
| [0005](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0005-persistence.md) | 领域协议与持久化 | 参考实现 `Domain`、`Persistence.Abstractions`，套件 `Persistence.Conformance` |
| [0006](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0006-caching.md) | 缓存、分布式锁与幂等 | 参考实现 `Caching.Abstractions`、`Caching.InMemory`、`Caching.Redis`、`Caching.Hybrid`，套件 `Caching.Conformance` |
| [0007](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0007-multitenancy.md) | 多租户 | 参考实现 `MultiTenancy.Abstractions`、`MultiTenancy.InMemory`，套件 `MultiTenancy.Conformance`；Provider `MultiTenancy.Database` |
| [0008](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0008-security.md) | 认证与授权 | 参考实现 `Security.Abstractions`、`Security.InMemory`、`Security.Jwt`、`Security.OpenIdConnect`，套件 `Security.Conformance`；Provider `Security.Database`、`Security.Ip2Region`（IP 归属地） |
| [0009](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0009-messaging.md) | 消息、CloudEvents 信封与发件箱 | 参考实现 `Messaging.Abstractions`、`Messaging.InMemory`、`Messaging.RabbitMQ`、`Messaging.Kafka`、`Messaging.Redis`，套件 `Messaging.Conformance` |
| [0010](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0010-localization.md) | 本地化：默认英文与 10 种内置区域 | 参考实现 `Localization.Abstractions`、`Localization.InMemory`；Provider `Localization.Database`，套件 `Localization.Conformance` |
| [0011](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0011-operations.md) | 操作契约与 XGI 消息 | 参考实现 `Kernel.Operations`，协议适配器在 `Protocols.*` |
| [0012](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0012-objectstorage.md) | 对象存储 | 参考实现 `ObjectStorage.Abstractions`、`ObjectStorage.FileSystem`、`ObjectStorage.S3`，套件 `ObjectStorage.Conformance` |
| [0013](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0013-scheduling.md) | 调度与领导选举 | 2026-10-26 修订：作业存储、动态作业、多触发器、misfire、暂停、历史、集群分发；参考实现 `Scheduling.Abstractions`、`Scheduling.InMemory`、`Scheduling.Database`，套件 `Scheduling.Conformance` |
| [0014](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0014-configuration.md) | 远程配置与密钥 | 参考实现 `Configuration.Abstractions`、`Configuration.Nacos`，套件 `Configuration.Conformance`；默认实现是 .NET 原生文件与环境变量，无包 |
| [0015](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0015-auditing.md) | 审计 | 参考实现 `Auditing.Abstractions`、`Auditing.InMemory`，套件 `Auditing.Conformance`；Provider `Auditing.Database` |
| [0016](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0016-packaging.md) | 应用清单与打包 | 参考实现 `Cli` 的 `xihan pack`；规范 `packaging/` |
| [0017](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0017-plugin-protocol.md) | 进程外插件协议 `xihan.plugin.v1` | 规范 `plugin/`；参考实现 `Plugins.OutOfProcess`（协议代码在 `Plugins.Protocol`），套件 `Plugins.Conformance` |
| [0018](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0018-wasm-plugins.md) | Wasm 插件 ABI（Experimental）：同一套消息经导出函数 + 线性内存 | 规范 `plugin/wasm.md`；参考实现 `Plugins.Wasm`，套件 `Plugins.Conformance` |
| [0019](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0019-remote-providers.md) | 远程 Provider：子系统能力经插件操作提供，首个操作集 `caching.distributed@1` | 规范 `provider/`；适配器 `Caching.Remote` |
| [0020](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0020-migration.md) | 版本迁移：`xihan migrate <来源版本>` 按规则集做语法级重写，不做运行期兼容桥 | 规范 `migration/README.md`；实现 `XiHan.Framework.Cli` |
| [0021](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0021-workflow.md) | 工作流：持久化执行 | 2026-10-26 修订：触发式启动、事件网关、外部任务、HTTP 与补偿活动、跳转与迁移、认领 / 退回 / 催办、提醒与审批阈值、保留期清理；参考实现 `Workflow.Abstractions`、`Workflow.InMemory`、`Workflow.Database`，套件 `Workflow.Conformance` |
| [0022](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0022-ai.md) | AI：聊天、嵌入、工具与知识 | 参考实现 `AI.Abstractions`、`AI.OpenAI`、`AI.VectorData`，套件 `AI.Conformance` |
| [0023](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0023-observability.md) | 可观测性：导出与运行信息 | 参考实现 `Observability.Abstractions`、`Observability.Host`、`Observability.OpenTelemetry`；运行信息不启动外部命令，取不到的项见正文 |
| [0024](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0024-versions-and-profiles.md) | 规范的版本与一致性档 | 档表 `conformance/profiles.json`，由 `tools/speccheck` 与 conformance 模块的测试核对；.NET 由架构测试 `SpecVectorCoverageTests` 按认领的档核对向量 |

## 下一步

- [核心概念](/getting-started/concepts)：这些规范在 .NET 实现里对应什么
- [进程外插件](/plugins/out-of-process)：用任意语言按规范写插件
- [贡献指南](/appendix/contributing)：怎样提一个 RFC
