# 包总览

XiHan Framework 5 由一组可以独立引用的 NuGet 包组成，按环层组织。本册是参考手册，每个包一页，列出它提供的能力、配置节、主要类型、限制与错误码；要完成某件事时先读[开发指南](../guide/modularity)。

::: tip 先看指南还是先看这里
| 问题 | 去处 |
| --- | --- |
| 要做某件事：装配特性、写操作、接数据库、做授权、发消息 | [开发指南](../guide/modularity) |
| 某个包有什么：能力 ID、配置键、主要类型、AOT 与限制 | 本册 |
:::

5.0 尚未发布到 nuget.org，全部包的版本是 `5.0.0-preview.1`，取得方式见[快速上手](../quickstart#获取预览包)。

## 命名规则

包 ID 由术语表与文法推出：`XiHan.Framework.<领域>[.<角色>]`。

| 后缀 | 角色 | 示例 |
| --- | --- | --- |
| `.Abstractions` | 抽象包：接口、契约、阶段中间件，零第三方依赖 | `Caching.Abstractions` |
| 机制名 | 默认实现：零外部设施、容量有界，清单角色为 `default` | `Caching.InMemory`、`ObjectStorage.FileSystem` |
| `.Database` | 经持久化子系统的仓储读写的 Provider，数据库由应用选中的持久化 Provider 决定 | `Security.Database` |
| 技术名 | 接外部设施的 Provider | `Caching.Redis`、`Persistence.EntityFrameworkCore` |
| `.Conformance` | 一致性套件：实现必须通过的 xUnit 测试 | `Caching.Conformance` |
| `.Generators` / `.Analyzers` | 源生成器与分析器，开发期依赖 | `Kernel.Generators` |

默认是清单里的 `role`，不是包名：没有显式 Provider 时默认实现被选中，加入 Provider 后自动让位。

## 怎样选包

1. 先选组合包：`Profiles.Minimal`（内核、ASP.NET Core 宿主、内存本地化）、`Profiles.Standard`（再加持久化、缓存、多租户、安全、消息的内存默认实现）、`Profiles.Full`（再加对象存储、调度、审计、工作流、AI 工具目录、OpenTelemetry、运行信息与四种协议适配器）。
2. 按对外暴露的方式加协议适配器：`Protocols.Http`、`Protocols.Grpc`、`Protocols.Mcp`、`Protocols.CloudEvents`，或换成命令行宿主 `Hosting.CommandLine`。
3. 按生产环境的外部设施加 Provider：`Persistence.EntityFrameworkCore` / `Persistence.SqlSugar`、`Caching.Redis`、`Messaging.RabbitMQ` / `Messaging.Kafka`、`Security.Jwt` / `Security.OpenIdConnect`、`ObjectStorage.S3` 等，每个都要 `UseFeature` 加入。
4. 开发期依赖：`Kernel.Generators`（操作契约）与 `Persistence.Generators`（仓储登记）。

## Ring 0–1 原语与内核

| 包 | 说明 |
| --- | --- |
| [Primitives](./primitives) | 原语与契约：`Result`、`Error`、`Maybe`、`InvocationContext`、清单模型、管道阶段、生命周期状态、消息键；只增不改 |
| [Kernel](./kernel) | 微内核：应用构建、能力图谱、管道与内置中间件、操作契约与调用器、安全原语、健康、插件抽象 |

## Ring 2 领域与抽象

| 包 | 说明 |
| --- | --- |
| [Domain](./domain) | 实体协议接口、值对象、领域事件缓冲、规约；纯契约 |
| [Persistence.Abstractions](./persistence-abstractions) | 仓储、查询、工作单元与 Transaction 阶段、标识生成、领域事件派发 |
| [Caching.Abstractions](./caching-abstractions) | 字节存储、类型化缓存、序列化器、分布式锁、Idempotency 阶段、`IDistributedCache` 适配、键管理 |
| [MultiTenancy.Abstractions](./multitenancy-abstractions) | 租户信息与存储、租户解析器与 Tenant 阶段、租户分片 |
| [Security.Abstractions](./security-abstractions) | 凭据与认证处理器、权限与策略、RBAC 授予、Authenticate / Authorize 阶段、条件引擎、IP 归属地契约 |
| [Messaging.Abstractions](./messaging-abstractions) | CloudEvents 信封、发布与订阅、经管道的派发、发件箱、领域事件映射 |
| [Localization.Abstractions](./localization-abstractions) | 区域、消息目录、显式区域的本地化接口 |
| [ObjectStorage.Abstractions](./objectstorage-abstractions) | 对象存储契约：批量写入与删除、按前缀列举、范围读取、分片上传、预签名 |
| [Scheduling.Abstractions](./scheduling-abstractions) | 作业、触发器、计划、调度引擎与作业存储契约、领导选举 |
| [Configuration.Abstractions](./configuration-abstractions) | 远程配置与密钥契约、接入 `IConfigurationBuilder` 的配置源 |
| [Auditing.Abstractions](./auditing-abstractions) | Observe 阶段审计中间件、审计扩展与查询契约 |
| [Observability.Abstractions](./observability-abstractions) | 进程与主机运行信息的契约 |
| [Workflow.Abstractions](./workflow-abstractions) | 工作流定义模型、目录、引擎、内置活动、用户任务、存储契约 |
| [AI.Abstractions](./ai-abstractions) | 基于 `Microsoft.Extensions.AI` 的 Provider 目录、`IAIChat`、护栏、工具目录、知识库 |

## Ring 3 默认实现与 Provider

| 领域 | 默认实现 | Provider |
| --- | --- | --- |
| 持久化 | [Persistence.InMemory](./persistence-inmemory) | [EntityFrameworkCore](./persistence-entityframeworkcore)、[SqlSugar](./persistence-sqlsugar) |
| 缓存 | [Caching.InMemory](./caching-inmemory) | [Redis](./caching-redis)、[Hybrid](./caching-hybrid)、[Remote](./caching-remote)（Experimental） |
| 多租户 | [MultiTenancy.InMemory](./multitenancy-inmemory) | [Database](./multitenancy-database) |
| 安全 | [Security.InMemory](./security-inmemory) | [Database](./security-database)、[Jwt](./security-jwt)、[OpenIdConnect](./security-openidconnect)、[Ip2Region](./security-ip2region) |
| 消息 | [Messaging.InMemory](./messaging-inmemory) | [RabbitMQ](./messaging-rabbitmq)、[Kafka](./messaging-kafka) |
| 本地化 | [Localization.InMemory](./localization-inmemory) | [Database](./localization-database) |
| 对象存储 | [ObjectStorage.FileSystem](./objectstorage-filesystem) | [S3](./objectstorage-s3) |
| 调度 | [Scheduling.InMemory](./scheduling-inmemory) | [Database](./scheduling-database)、[Kubernetes](./scheduling-kubernetes) |
| 配置 | .NET 原生的文件与环境变量，无包 | [Nacos](./configuration-nacos) |
| 审计 | [Auditing.InMemory](./auditing-inmemory) | [Database](./auditing-database) |
| 可观测性 | [Observability.Host](./observability-host)（运行信息） | [OpenTelemetry](./observability-opentelemetry)（导出） |
| 工作流 | [Workflow.InMemory](./workflow-inmemory) | [Database](./workflow-database) |
| AI | 工具目录由 `AI.Abstractions` 的 `AIToolsFeature` 提供 | [OpenAI](./ai-openai)、[VectorData](./ai-vectordata) |

插件协议的 .NET 类型 [Plugins.Protocol](./plugins-protocol) 也在 Ring 3，由各隔离级别的插件宿主共用。

## Ring 4 宿主

| 包 | 说明 |
| --- | --- |
| [Hosting.AspNetCore](./hosting-aspnetcore) | HTTP 请求桥接进管道、`Accept-Language` 协商、探针端点、本地化的 Problem Details、`XiHan` 认证方案 |
| [Hosting.CommandLine](./hosting-commandline) | 命令行宿主：`help` / `graph` / `pipeline` / `operations` / `invoke`，按错误 KIND 的退出码 |

## Ring 5 协议与插件

| 包 | 说明 |
| --- | --- |
| [Protocols.Http](./protocols-http) | 操作 ID 推导路由、载荷绑定、OpenAPI 3.1 |
| [Protocols.Grpc](./protocols-grpc) | 操作成为一元方法，protobuf 由 JSON Schema 推出，发布 `/operations.proto` |
| [Protocols.Mcp](./protocols-mcp) | 操作成为 MCP tool，Streamable HTTP 与 stdio 传输 |
| [Protocols.CloudEvents](./protocols-cloudevents) | CloudEvents 1.0 HTTP 绑定：入站 `/events` 与出站 Webhook |
| [Plugins.InProcess](./plugins-inprocess) | 可收集 `AssemblyLoadContext` 的进程内插件宿主 |
| [Plugins.OutOfProcess](./plugins-outofprocess) | 按 `xihan.plugin.v1` 启动与监视子进程的插件宿主，插件可用任意语言编写 |
| [Plugins.Wasm](./plugins-wasm) | wasmtime 沙箱里的 Wasm 插件宿主（Experimental） |

## Ring 6 组合包

| 包 | 内容 |
| --- | --- |
| [Profiles.Minimal](./profiles-minimal) | Primitives + Kernel + Hosting.AspNetCore + Localization.InMemory，`UseMinimalProfile()` |
| [Profiles.Standard](./profiles-standard) | Minimal + Domain + 五个子系统的抽象与内存默认实现，`UseStandardProfile()` |
| [Profiles.Full](./profiles-full) | Standard + 对象存储、调度、审计、工作流、AI 工具、OpenTelemetry、运行信息与四种协议适配器，`UseFullProfile()` |

## 工具链

| 包 | 说明 |
| --- | --- |
| [Kernel.Generators](./kernel-generators) | 源生成器：`[Operation]` → 登记代码与请求 / 响应 JSON Schema（XHG001–XHG005） |
| [Kernel.Analyzers](./kernel-analyzers) | Roslyn 分析器：`ApiLevel`、命名、Internal 引用、零反射 |
| [Persistence.Generators](./persistence-generators) | 源生成器：实体 → 仓储登记、EF Core 实体登记、软删除与租户过滤规约 |
| [Cli](./cli) | 命令行工具 `xihan`：`pack` 生成部署产物，转发 `graph` / `pipeline` / `operations` / `invoke` |
| [Testing](./testing) | 测试宿主、特性桩、清单与上下文构造 |
| [Conformance](./conformance) | 各领域的一致性套件与插件协议黑盒套件 |

工具链包可以引用任何环；源生成器与分析器只在编译期运行，不进入应用的运行时依赖。
