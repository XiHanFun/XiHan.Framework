来源：https://framework.docs.xihanfun.com/getting-started/concepts

# 核心概念

这一页把框架的核心概念一次讲清：特性与清单、能力图谱、执行管道、操作契约、默认实现与 Provider、结果与错误、环层与命名。它是后面所有章节的词汇表。读完[第一个应用](/getting-started/first-app)再读这一页效果最好，因为每个概念你都已经在代码里碰过。

## 一张图

```text
HTTP 请求 / gRPC 调用 / MCP tools/call / 命令行 / 消息投递 / 作业触发 / 插件回调
   │  协议适配器或派发器构造 InvocationContext，把线上消息翻译成操作请求
   ▼
Ingress → Observe → Resilience → Authenticate → Tenant → Authorize
        → Validate → Idempotency → Transaction → Handle → Egress
   │  Handle 阶段执行操作，操作经子系统抽象访问 Provider
   ▼
Egress：Result 与 Error 翻译回协议（HTTP 为 RFC 9457 Problem Details，gRPC 为状态码与尾部元数据）
```

应用由若干特性组成，特性在启动前被内核装配成一张能力图谱；运行时每次调用都走同一条管道；业务在 Handle 阶段以操作的形式执行；横切能力由子系统提供，子系统的实现可以替换。

## 特性与清单

特性是实现 `IFeature` 的类，是内核装配的最小单元。它只有两个成员：一份清单 `Manifest`，一个 `Configure(FeatureContext)` 方法。清单写明这个特性是谁、提供哪些能力、依赖哪些能力、贡献了什么；`Configure` 在构建期被调用一次，用来显式注册服务、中间件、权限与操作。

特性没有基类、没有生命周期钩子，内核也不扫描程序集。应用用 `UseFeature<T>()` 显式加入每一个特性。需要在启动与停止时做事的特性实现 `IHostedFeature`。

能力包是包含一个或多个特性的 NuGet 包。框架里每个子系统的默认实现与 Provider 都是能力包；应用自己的业务模块也写成能力包。详见[特性与清单](/startup/features)。

## 能力 ID 与能力图谱

依赖按能力声明，不按包名或类型。能力 ID 的形式是 `<领域>.<能力>`，引用时带接口版本，如 `persistence.relational@1`、`caching.distributed@1`。同一能力可以由多个 Provider 提供：`persistence.relational` 可以来自 `Persistence.InMemory`、`Persistence.EntityFrameworkCore` 或 `Persistence.SqlSugar`。

内核在构建期收集全部清单，按版本范围求解，拓扑排序，生成已解析图。缺失依赖、循环、版本冲突、同一能力有多个候选都会带着双方信息报错；启动日志与 `graph` 命令打印同一份图。同一能力有多个 Provider 时，默认实现（清单 `role: default`）自动让位；两个以上显式 Provider 时，在配置里指定。详见[能力图谱与 Provider](/startup/capabilities)。

## 执行管道

每次调用都经过一条固定十一个阶段的管道：

| 阶段 | 职责 | 谁提供中间件 |
| --- | --- | --- |
| Ingress | 协议消息归一化、大小限制 | 内核 |
| Observe | Trace、日志、指标、审计 | 内核、审计子系统 |
| Resilience | 截止时间、限流、并发上限、熔断 | 内核 |
| Authenticate | 主体解析 | 安全子系统 |
| Tenant | 租户解析与分片 | 多租户子系统 |
| Authorize | 权限与策略判定 | 安全子系统 |
| Validate | 契约校验 | 内核，由操作 Schema 驱动 |
| Idempotency | 幂等键去重 | 缓存子系统 |
| Transaction | 工作单元 | 持久化子系统 |
| Handle | 调用操作 | 内核 |
| Egress | 结果与错误翻译 | 协议适配器 |

特性按阶段插入中间件，不需要知道其他中间件。排序规则是「阶段、阶段内的 Before 与 After 约束、归属清单 ID」，结果确定且可打印。管道不绑定 HTTP：Web、命令行、消息消费、作业与插件回调走同一条管道。详见[执行管道](/startup/pipeline)。

## 调用上下文

管道上下文是 `InvocationContext`，携带取消令牌、截止时间、W3C Trace、已登记的 Baggage 键（租户、主体、关联 ID、幂等键、区域）、作用域对象袋与作用域服务。安全上下文（主体、租户）由 Authenticate 与 Tenant 阶段写入，之后不可变。详见[调用上下文](/startup/invocation-context)。

## 操作契约

业务以操作为单位声明：一个类实现 `IOperation<TRequest, TResponse>` 并标注 `[Operation]`。操作 ID 的形式是 `<资源复数>.<动词>`，如 `orders.create`。源生成器从契约产出登记代码与请求、响应的 JSON Schema；协议适配器把 HTTP 请求、gRPC 调用、MCP 的 `tools/call` 与命令行参数翻译成同一种调用。

| 操作 ID | HTTP | gRPC | MCP tool | 命令行 |
| --- | --- | --- | --- | --- |
| `orders.create` | `POST /orders` | `Orders/Create` | `orders_create` | `orders create` |
| `orders.get` | `GET /orders/{id}` | `Orders/Get` | `orders_get` | `orders get` |
| `orders.list` | `GET /orders` | `Orders/List` | `orders_list` | `orders list` |

授权要求与错误码在各协议上一致。详见[操作契约](/web/operations)。

## 默认实现与 Provider

每个子系统有一个抽象包 `<领域>.Abstractions`，配一个零外部依赖、容量有界的默认实现（`InMemory`、`FileSystem`），再由 Provider 包接入 EF Core、SqlSugar、Redis、RabbitMQ、Kafka、S3 等外部设施。

| 角色 | 包名形式 | 示例 |
| --- | --- | --- |
| 抽象 | `<领域>.Abstractions` | `Caching.Abstractions` |
| 默认实现 | 按机制命名 | `Caching.InMemory`、`ObjectStorage.FileSystem` |
| 经持久化子系统的 Provider | `<领域>.Database` | `Security.Database` |
| 接外部设施的 Provider | 按技术命名 | `Caching.Redis`、`Persistence.EntityFrameworkCore` |
| 一致性套件 | `<领域>.Conformance` | `Caching.Conformance` |

「默认」是清单里的角色，不是包名。换 Provider 只加包、加一行 `UseFeature`，业务代码不变；默认实现与 Provider 通过同一套一致性套件。

## 结果与错误

可恢复的失败用 `Result` 与 `Error` 表达，不抛异常。错误码的形式是 `<领域大写>_<KIND>_<三位序号>`，如 `PERSISTENCE_CONFLICT_001`、`SECURITY_DENIED_004`；KIND 决定 HTTP 状态码、gRPC 状态与命令行退出码。`Error` 只携带消息键与参数，宿主在 Egress 阶段按调用方的区域翻译成文案。配置与编程错误才抛异常。详见[结果与错误处理](/web/errors)。

## 本地化

面向人的文案不写在代码里。代码只引用消息键常量，每种语言一个文件 `Localization/<区域>.json`，内置十种区域，默认 `en-US`。取文案用 `key.L()`，按当前调用的区域渲染。详见[全球化和本地化](/common/localization)。

## 环层与依赖规则

包按环层组织，依赖只能指向更低的环，同环之间只经抽象包往来，规则由架构测试执行：

```text
Ring 6  组合包          Profiles.Minimal / Standard / Full
Ring 5  协议与插件      Protocols.* / Plugins.InProcess / OutOfProcess / Wasm
Ring 4  宿主            Hosting.AspNetCore / Hosting.CommandLine
Ring 3  实现            <领域>.InMemory、<领域>.Database、Redis、EntityFrameworkCore 等
Ring 2  抽象            Domain、<领域>.Abstractions
Ring 1  内核            Kernel
Ring 0  原语层          Primitives
工具链                  Kernel.Analyzers、Kernel.Generators、Persistence.Generators、
                        <领域>.Conformance、Testing、Cli
```

原语层不引用任何包；内核与全部抽象包只允许 `Microsoft.Extensions.*` 抽象包；原语层与内核零反射，反射只允许出现在 `Plugins.InProcess`。

## 命名约定

命名由术语表推出，架构测试校验：

| 对象 | 规则 | 示例 |
| --- | --- | --- |
| 包 ID | `XiHan.Framework.<领域>[.<角色>]` | `XiHan.Framework.Caching.Redis` |
| 命名空间 | 等于包 ID，子命名空间最多一层 | `XiHan.Framework.Caching.Abstractions.Locking` |
| 能力 ID | `<领域小写>.<能力>`，引用时带接口版本 | `caching.distributed@1` |
| 清单 ID | `xihan.<领域>.<名称>` | `xihan.caching.redis` |
| 配置节 | `XiHan:<领域>` | `XiHan:Caching` |
| 错误码 | `<领域大写>_<KIND>_<三位序号>` | `PERSISTENCE_CONFLICT_001` |
| 操作 ID | `<资源复数>.<动词>` | `orders.create` |
| 消息键 | `<领域>.<资源>.<键>` | `security.token.subject_missing` |

类型名不带 `XiHan` 前缀；`Default`、`Core`、`Common`、`Manager`、`Helper` 等词不进包名与领域名。

## 稳定性分级

公开类型都带 `[ApiLevel(Stability, <引入版本>)]`，公开面以各包的 `PublicAPI.Shipped.txt` 与 `PublicAPI.Unshipped.txt` 为准：

| 级别 | 含义 |
| --- | --- |
| `Stable` | 大版本内不破坏 |
| `Preview` | 可能变更 |
| `Experimental` | 可能移除，用于收集反馈 |
| `Internal` | 不对外承诺，分析器阻止外部引用 |
| `Deprecated` | 将在下一个大版本移除 |

## 下一步

- [组合包与选包](/getting-started/profiles)：Minimal、Standard、Full 各含什么
- [装配入口](/startup/bootstrap)：从 `AddXiHan` 开始系统学习
