# 命名速查

事实源：`next/spec/conventions/vocabulary.md`（术语表）与 `next/spec/conventions/package-ids.md`（文法）。架构测试直接解析术语表，新增领域词走 RFC。

## 包 ID

```text
XiHan.Framework.Primitives | Kernel[.Analyzers|.Generators] | Domain | Testing | Cli | Compat.V<n>
XiHan.Framework.<子系统>.<Abstractions|Conformance|Generators|机制|技术>
XiHan.Framework.Hosting.<AspNetCore|CommandLine>
XiHan.Framework.Protocols.<Http|Grpc|Mcp|CloudEvents>
XiHan.Framework.Plugins.<InProcess|OutOfProcess|Wasm>
XiHan.Framework.Profiles.<Minimal|Standard|Full>
```

子系统词：`Persistence`、`Caching`、`Messaging`、`Security`、`MultiTenancy`、`ObjectStorage`、`Scheduling`、`Configuration`、`Observability`、`Localization`、`Auditing`、`Workflow`、`Search`、`AI`、`Actors`。

角色段禁用词：`Default`、`Core`、`Common`、`Shared`、`Base`、`Impl`、`Util`、`Utils`、`Helper`、`Manager`、`Bus`、`Engine`。技术名用官方拼写（`EntityFrameworkCore`、`RabbitMQ`、`AliyunOss`）。

## 同词链

| 形态 | 规则 | 示例 |
| --- | --- | --- |
| 命名空间 | 等于包 ID，子命名空间最多一层 | `XiHan.Framework.Kernel.Pipeline` |
| 能力 ID | `<领域小写>.<能力>[@版本][?]` | `persistence.relational@^1`、`multitenancy.context@^1?` |
| 操作 ID | `<resource>.<verb>` | `orders.create` |
| 错误码 | `<领域大写>_<KIND>_<NNN>` | `PERSISTENCE_CONFLICT_001` |
| 配置节 | `XiHan:<领域>` | `XiHan:Caching` |
| Baggage 键 | `xihan.<name>` | `xihan.tenant` |
| 清单 ID | `xihan.<领域>.<名称>` | `xihan.persistence.efcore` |
| 资源名 | `xrn:<partition>:<service>:<tenant>:<type>/<id>` | `xrn:cn:orders:t-42:order/1001` |

## 类型名

不带 `XiHan` 前缀。已定名：

- 原语层：`Result<T>`、`Result`、`Error`、`Maybe<T>`、`TypedId<TTag, TValue>`、`InvocationContext`、`FrameworkException`、`FrameworkInfo`、`Stability`、`ApiLevelAttribute`、`LifecycleState`。
- 内核：`IFeature`、`IHostedFeature`、`FeatureContext`、`AppBuilder`、`App`、`IPipelineMiddleware`、`PipelineBuilder`、`PipelineDelegate`、`Stage`、`Placement`、`IOperation<TRequest, TResponse>`、`IOperationInvoker`。
- 约定：能力包入口 `<名称>Feature`；中间件 `<名称>Middleware`；操作 `<动词><资源>`；端口 `I<名词>`；选项 `<领域>Options`。

## 管道阶段

`ingress → observe → resilience → authenticate → tenant → authorize → validate → idempotency → transaction → handle → egress`。排序：阶段 → `Before/After` → 包 ID 字典序。

## 生命周期状态

`Declared → Resolved → Configured → Started → Ready → (Degraded) → Draining → Stopped`；任一步失败 → `Failed`。
