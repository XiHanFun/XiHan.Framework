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
