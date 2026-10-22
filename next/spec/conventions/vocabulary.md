# 术语表

一个领域一个词，全链路同词。本表是唯一事实源：包名与命名空间用 PascalCase 领域词，能力 ID 用小写，配置节用 `XiHan:<领域>`，错误码域用大写。架构测试直接解析本表，新增条目走 RFC。

| 领域词 | 能力 ID 前缀 | 配置节 | 错误码域 | 边界 |
| --- | --- | --- | --- | --- |
| `Primitives` | — | — | `PRIMITIVES` | 零行为原语与契约 |
| `Kernel` | `kernel` | `XiHan:Kernel` | `KERNEL` | 宿主、能力图谱、管道、诊断、安全原语、插件宿主；子命名空间 `Hosting`、`Capabilities`、`Pipeline`、`Diagnostics`、`Security`、`Plugins` |
| `Domain` | `domain` | — | `DOMAIN` | 实体协议、值对象、领域事件、规约；纯契约，无实现包 |
| `Persistence` | `persistence` | `XiHan:Persistence` | `PERSISTENCE` | 仓储、工作单元、迁移、ID 生成 |
| `Caching` | `caching` | `XiHan:Caching` | `CACHING` | 本地与分布式缓存、分布式锁 |
| `Messaging` | `messaging` | `XiHan:Messaging` | `MESSAGING` | 队列、流、发布订阅、Outbox；集成事件用 CloudEvents 信封 |
| `Security` | `security` | `XiHan:Security` | `SECURITY` | 认证、授权、策略；与 `Kernel.Security` 原语同域 |
| `MultiTenancy` | `multitenancy` | `XiHan:MultiTenancy` | `MULTITENANCY` | 租户解析、隔离、分片 |
| `ObjectStorage` | `objectstorage` | `XiHan:ObjectStorage` | `OBJECTSTORAGE` | 对象存储 |
| `Scheduling` | `scheduling` | `XiHan:Scheduling` | `SCHEDULING` | 定时与后台任务、领导选举 |
| `Configuration` | `configuration` | `XiHan:Configuration` | `CONFIGURATION` | 远程配置与密钥的 Provider |
| `Observability` | `observability` | `XiHan:Observability` | `OBSERVABILITY` | 导出器与采样配置 |
| `Localization` | `localization` | `XiHan:Localization` | `LOCALIZATION` | 资源与文化 |
| `Auditing` | `auditing` | `XiHan:Auditing` | `AUDITING` | 审计写入 |
| `Workflow` | `workflow` | `XiHan:Workflow` | `WORKFLOW` | 持久化执行 |
| `Search` | `search` | `XiHan:Search` | `SEARCH` | 索引与查询 |
| `AI` | `ai` | `XiHan:AI` | `AI` | 基于 `Microsoft.Extensions.AI` 的端口 |
| `Actors` | `actors` | `XiHan:Actors` | `ACTORS` | 单点状态协调 |
| `Hosting` | `hosting` | `XiHan:Hosting` | `HOSTING` | 宿主适配 |
| `Protocols` | `protocols` | `XiHan:Protocols` | `PROTOCOLS` | 协议适配器 |
| `Plugins` | `plugins` | `XiHan:Plugins` | `PLUGINS` | 插件隔离与宿主 |
| `Profiles` | — | — | — | 元包 |
| `Testing` | — | — | — | 测试基座 |
| `Cli` | — | — | — | 命令行工具 |
| `Compat` | — | — | `COMPAT` | 旧版本兼容桥，角色段为大版本号（`Compat.V4`） |

`Hosting`、`Security`、`Plugins` 既是 `Kernel` 的子命名空间，也是独立包的领域词：同域同词，环层由 `Kernel.` 前缀区分。
