# XiHan Spec（草案）

语言中立的框架规范。.NET（`next/src`）是参考实现，其他语言按本规范实现并通过一致性测试。P4 公开时拆为独立仓库 `XiHanFun/xihan-spec`。

## 目录

```text
spec/
├─ rfcs/           # 提案：先 RFC，再 ADR，再实现
├─ conventions/    # 规范性：术语表、包 ID、能力 ID、操作 ID、错误码、Baggage 键
├─ manifest/       # 规范性：xihan.manifest.json 的 JSON Schema
├─ primitives/     # P1 起：RFC-0001 采纳后的规范性文本
├─ pipeline/       # P1 起：RFC-0004 采纳后的规范性文本
├─ plugin/         # P4 起：插件协议 plugin.proto
├─ protocol/       # P3 起：HTTP / gRPC / MCP / CloudEvents 映射
└─ conformance/    # P1 起：数据驱动向量与运行器
```

## 规范性与信息性

| 规范性（各语言必须一致） | 信息性（各语言自选） |
| --- | --- |
| 原语语义与线上编码 | 内部 API 形态 |
| 清单 Schema | 包管理与目录布局 |
| 上下文传播键与载体 | 日志实现 |
| 管道阶段名与排序规则 | 具体中间件 |
| 插件协议 | 隔离机制 |
| 协议映射 | 服务器实现 |
| 术语表、错误码、能力 ID、操作 ID、包 ID 命名 | 代码风格 |

## RFC 流程

1. 在 `rfcs/` 新建 `NNNN-<slug>.md`，状态 `草案`。
2. 评审通过后写 ADR（`nextdocs/adr/`），RFC 状态改为 `已采纳`，正文并入对应规范性目录。
3. 实现与一致性向量随 RFC 一起合并；没有 RFC 的公开契约变更不合并。

| RFC | 标题 | 状态 |
| --- | --- | --- |
| [0001](rfcs/0001-primitives.md) | 原语层 | 已采纳（参考实现 `src/XiHan.Framework.Primitives`） |
| [0002](rfcs/0002-manifest.md) | 清单 | 已采纳（参考实现 `Kernel.Capabilities`） |
| [0003](rfcs/0003-invocation-context.md) | 调用上下文与传播 | 已采纳（载体映射随协议适配器实现） |
| [0004](rfcs/0004-pipeline-stages.md) | 管道阶段与排序 | 已采纳（参考实现 `Kernel.Pipeline`） |
| [0005](rfcs/0005-persistence.md) | 领域协议与持久化 | 已采纳（参考实现 `Domain`、`Persistence.Abstractions`，套件 `Persistence.Conformance`） |
| [0006](rfcs/0006-caching.md) | 缓存、分布式锁与幂等 | 已采纳（参考实现 `Caching.Abstractions`、`Caching.InMemory`、`Caching.Redis`，套件 `Caching.Conformance`） |
| [0007](rfcs/0007-multitenancy.md) | 多租户 | 已采纳（参考实现 `MultiTenancy.Abstractions`、`MultiTenancy.InMemory`，套件 `MultiTenancy.Conformance`） |
| [0008](rfcs/0008-security.md) | 认证与授权 | 已采纳（参考实现 `Security.Abstractions`、`Security.InMemory`、`Security.Jwt`，套件 `Security.Conformance`） |
| [0009](rfcs/0009-messaging.md) | 消息、CloudEvents 信封与发件箱 | 已采纳（参考实现 `Messaging.Abstractions`、`Messaging.InMemory`、`Messaging.RabbitMQ`，套件 `Messaging.Conformance`） |
| [0010](rfcs/0010-localization.md) | 本地化：默认英文与 10 种内置区域 | 已采纳（参考实现 `Localization.Abstractions`、`Localization.InMemory`） |
| [0011](rfcs/0011-operations.md) | 操作契约与 XGI 消息 | 已采纳（参考实现 `Kernel.Operations`） |
