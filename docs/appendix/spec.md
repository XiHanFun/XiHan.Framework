# 1.11 规范与多语言

XiHan Framework 的本体不是 .NET 代码，而是一份语言中立的规范：XiHan Spec。.NET 实现是它的参考实现，Go SDK 是第二个实现，其他语言按同一份规范实现并通过同一套一致性测试。这一页讲规范怎样组织、哪些内容是规范性的、一致性测试怎样工作，以及各语言 SDK 的现状。要写插件或另一种语言的 SDK 的读者需要读它；只用 .NET 的读者了解即可。

## 为什么要有规范

「同一个名字各语言都能用」这件事，只有两条路：要么每种语言各写一个风格相似的框架，然后在文档里宣称它们是一家；要么先写下语言中立的语义，再让每种实现去通过同一套测试。前者做出来的东西经不起细看，后者才是 OpenTelemetry、MCP、CloudEvents 走通的路。我选了后者。

规范还解决一个 .NET 内部的问题：公开契约先写进规范，再写架构决策记录，再写代码与一致性向量。没有 RFC 的公开 API 变更不合并。这条流程让「随手加一个公开方法」变得不可能，也让每个设计决定都有据可查。

## 规范仓库

规范在独立仓库 [XiHanFun/framework-spec](https://github.com/XiHanFun/framework-spec)，.NET 实现以子模块引用它到 `framework/spec`，Go SDK 引用到 `spec`。目录结构：

```text
spec/
├─ rfcs/           提案：先 RFC，再 ADR，再实现
├─ conventions/    规范性：术语表、包 ID、能力 ID、操作 ID、错误码、Baggage 键、批量优先
├─ manifest/       规范性：xihan.manifest.json 的 JSON Schema
├─ plugin/         规范性：插件协议 plugin.proto 与握手、生命周期、调用；Wasm 载体
├─ protocol/       规范性：HTTP、gRPC、MCP、CloudEvents、命令行的映射
├─ packaging/      规范性：应用清单 xihan.app.json 与 xihan pack 的产物映射
├─ provider/       规范性：远程 Provider 的通用规则与各能力的操作集
├─ migration/      规范性：xihan migrate 的通用约定
└─ conformance/    一致性向量与黑盒运行器
```

## 规范性与信息性

不是所有东西都要各语言一致。规范把内容分成两类：

| 规范性（各语言必须一致） | 信息性（各语言自选） |
| --- | --- |
| 原语语义与线上编码（`Result`、`Error`、`Maybe` 的 JSON 形态） | 内部 API 形态 |
| 清单 Schema | 包管理与目录布局 |
| 上下文传播键与载体 | 日志实现 |
| 管道阶段名与排序规则 | 具体中间件 |
| 插件协议 | 隔离机制 |
| 协议映射（路由推导、错误信封） | 服务器实现 |
| 术语表、错误码、能力 ID、操作 ID、包 ID 命名 | 代码风格 |
| 批量优先：集合入参、原生批量、部分失败语义 | 批次大小 |

举例说，`orders.create` 在任何语言的实现里都推导为 `POST /orders`，失败时都返回 RFC 9457 的 Problem Details，错误码都是 `<领域大写>_<KIND>_<三位序号>`；但 Go 用 `context.Context` 传递取消、.NET 用 `CancellationToken`，这是各语言自己的事。

## 一致性测试

一致性有两层：

| 层 | 形式 | 谁用 |
| --- | --- | --- |
| 数据驱动向量 | `conformance/vectors/` 下的 JSON 文件：`Result` 与 `Error` 编码、清单校验、版本解析、管道排序、上下文传播、包 ID 与能力 ID 文法 | 各语言 SDK 的单元测试直接读取 |
| 黑盒运行器 | `xihan-conformance plugin --dir <插件目录>`：以宿主身份启动任意语言写的进程外插件，核对握手行与版本协商、清单一致性、生命周期、未知操作的拒绝、声明的操作都返回状态、订阅投递确认、stdin 关闭即退出 | 插件作者、远程 Provider 作者 |

.NET 侧另有每个子系统的 `<领域>.Conformance` 包，是 xUnit 套件：默认实现与 Provider 都要通过，远程 Provider 跑同一套。用法见[一致性套件与门禁](/testing/conformance)。

SDK 按成熟度分级：Reference（.NET）、Stable（通过全部向量）、Experimental（部分向量）、Community（社区维护）。

## 各语言 SDK

| 语言 | 包名 | 仓库 | 状态 |
| --- | --- | --- | --- |
| .NET | `XiHan.Framework.*` | [XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework) | 参考实现 |
| Go | `github.com/xihanfun/framework-sdk-go` | [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) | 通过全部向量；用于进程外插件与远程 Provider |
| TypeScript | npm 组织 `@xihan-framework/*` | [framework-sdk-typescript](https://github.com/XiHanFun/framework-sdk-typescript) | 占位，Go 之后的下一个 |
| Rust | crate `xihan-framework` | [framework-sdk-rust](https://github.com/XiHanFun/framework-sdk-rust) | 占位，面向进程外与 Wasm 插件 |
| Java | `com.xihanfun:xihan-framework` | [framework-sdk-java](https://github.com/XiHanFun/framework-sdk-java) | 占位 |
| Python | `xihan-framework` | [framework-sdk-python](https://github.com/XiHanFun/framework-sdk-python) | 占位 |
| PHP | `xihanfun/xihan-framework` | [framework-sdk-php](https://github.com/XiHanFun/framework-sdk-php) | 占位 |

各语言的领域词与术语表一致，只换大小写风格。

为什么 Go 先行：它离 C# 最远，能证明规范不是 .NET 形状的；同时它适合承载进程外插件、黑盒运行器和将来的 Kubernetes Operator。Go SDK 已经做到的事：用 Go 写的插件在 .NET 宿主里服务一个操作，崩溃时宿主降级而不退出；用 Go 实现的分布式缓存经远程 Provider 适配器通过 .NET 的缓存一致性套件。示例见 `framework/samples/EchoGoPlugin` 与 `CacheGoPlugin`，写法见[进程外插件](/plugins/out-of-process)。

## 变更流程

公开契约的变更按固定顺序走：

1. 在 `rfcs/` 新建提案，状态为草案。
2. 评审通过后写架构决策记录，RFC 状态改为已采纳，正文并入对应的规范性目录。
3. 实现与一致性向量随 RFC 一起合并。

新增术语表条目同样走 RFC。错误码、能力 ID、Baggage 键、操作 ID 一经发布不改。

## 已采纳的 RFC

| RFC | 主题 | 参考实现 |
| --- | --- | --- |
| 0001 | 原语层 | `Primitives` |
| 0002 | 清单 | `Kernel` |
| 0003 | 调用上下文与传播 | 各协议适配器 |
| 0004 | 管道阶段与排序 | `Kernel` |
| 0005 | 领域协议与持久化 | `Domain`、`Persistence.*` |
| 0006 | 缓存、分布式锁与幂等 | `Caching.*` |
| 0007 | 多租户 | `MultiTenancy.*` |
| 0008 | 认证与授权 | `Security.*` |
| 0009 | 消息、CloudEvents 信封与发件箱 | `Messaging.*` |
| 0010 | 本地化 | `Localization.*` |
| 0011 | 操作契约与 XGI | `Kernel`、`Protocols.*` |
| 0012 | 对象存储 | `ObjectStorage.*` |
| 0013 | 调度 | `Scheduling.*` |
| 0014 | 远程配置与密钥 | `Configuration.*` |
| 0015 | 审计 | `Auditing.*` |
| 0016 | 应用清单与打包 | `Cli` |
| 0017 | 进程外插件协议 | `Plugins.Protocol`、`Plugins.OutOfProcess` |
| 0018 | Wasm 插件 | `Plugins.Wasm` |
| 0019 | 远程 Provider | `Caching.Remote` |
| 0020 | 版本迁移 | `Cli` |
| 0021 | 工作流 | `Workflow.*` |
| 0022 | AI | `AI.*` |
| 0023 | 可观测性与运行信息 | `Observability.*` |

正文见规范仓库的 `rfcs/` 目录。

## 下一步

- [核心概念](/getting-started/concepts)：这些规范在 .NET 实现里对应什么
- [进程外插件](/plugins/out-of-process)：用任意语言按规范写插件
- [贡献指南](/contributing/)：怎样提一个 RFC
