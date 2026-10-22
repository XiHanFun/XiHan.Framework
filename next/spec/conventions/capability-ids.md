# 能力 ID

```text
CapabilityId   = Domain "." Capability
CapabilityRef  = CapabilityId [ "@" VersionRange ] [ "?" ]
Domain         = 术语表领域词的小写形态
Capability     = 小写字母与数字，连字符分隔
VersionRange   = 整数接口版本或 ^ 范围（"1"、"^1"）
"?"            = 可选依赖（仅在 requires 中）
```

示例：`persistence.relational@1`、`caching.distributed@^1`、`multitenancy.context@^1?`。

## 规则

1. 依赖按能力 ID 声明，不按包名；同一能力可由多个 Provider 提供。
2. 接口版本是整数，变更只加新版本并存（`persistence.relational@2`），不改旧版本语义。
3. 能力 ID 一经发布不改；新能力走 RFC。

## 已登记的能力

| 能力 ID | 含义 |
| --- | --- |
| `kernel.hosting` | 应用构建、特性装配、生命周期 |
| `kernel.capabilities` | 清单解析与能力图谱 |
| `kernel.pipeline` | 执行管道 |
| `kernel.diagnostics` | 诊断薄桥与审计接收器 |
| `kernel.security` | 安全原语 |
| `kernel.plugins` | 插件宿主抽象 |
| `plugins.out-of-process` | 进程外插件宿主（RFC-0017） |
| `domain.model` | 实体协议、值对象、领域事件、规约 |
| `persistence.relational` / `persistence.document` | 仓储、工作单元、迁移 |
| `caching.local` / `caching.distributed` | 缓存与分布式锁 |
| `messaging.queue` / `messaging.stream` | 队列与流 |
| `security.authentication` / `security.authorization` | 认证与授权 |
| `multitenancy.context` / `multitenancy.sharding` | 租户上下文与分片 |
| `objectstorage.blob` | 对象存储 |
| `scheduling.jobs` / `scheduling.leader` | 任务与领导选举 |
| `configuration.remote` / `configuration.secrets` | 远程配置与密钥 |
| `observability.export` | 导出器 |
| `localization.resources` | 本地化资源 |
| `auditing.trail` | 审计 |
| `workflow.execution` | 持久化执行 |
| `search.fulltext` | 全文检索 |
| `ai.chat` / `ai.embedding` / `ai.tools` | AI 端口 |
| `actors.virtual` | 虚拟 Actor |
| `hosting.aspnetcore` / `hosting.commandline` | 宿主 |
| `protocols.http` / `protocols.grpc` / `protocols.mcp` / `protocols.cloudevents` | 协议适配器 |
| `testing` | 测试基座 |
