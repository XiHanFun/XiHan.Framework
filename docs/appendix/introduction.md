# 介绍

让 .NET 应用的边界清楚十年。

## 序言

写框架的人很多，能把一个框架维护十年的人很少。我希望 XiHan Framework 成为后者，所以它的每一条设计都在回答同一个问题：十年后这段代码还能不能用、还能不能换。

## 名字的由来

曦，是清晨的第一缕光；寒，是冬日的清冷。我把这两个字放在一起给框架命名，是想让它像冬天的早晨：清冷、克制、不喧哗，但光一出来，一整天的工作就能看得清楚。框架、视图组件、基础应用和四款设计器共用「曦寒」这个前缀，组织叫曦寒懿（XiHanFun），寓意这一整套东西是一个有完整生态的家族，而不是零散的仓库。

## 文档地址

| 文档 | 地址 |
| --- | --- |
| 开发框架（本站） | [framework.docs.xihanfun.com](https://framework.docs.xihanfun.com) |
| 视图组件 | [ui.docs.xihanfun.com](https://ui.docs.xihanfun.com) |
| 基础应用 | [basicapp.docs.xihanfun.com](https://basicapp.docs.xihanfun.com) |
| 组织门户 | [docs.xihanfun.com](https://docs.xihanfun.com) |

给 AI 编码助手读的纯文本版本见 [LLMs.txt](/agents/llms)。

## 开源地址

| 平台 | 地址 |
| --- | --- |
| GitHub（主库） | [XiHanFun/XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework) |
| Gitee（同步备库） | [XiHanFun/XiHan.Framework](https://gitee.com/XiHanFun/XiHan.Framework) |
| GitCode（同步备库） | [XiHanFun/XiHan.Framework](https://gitcode.com/XiHanFun/XiHan.Framework) |
| NuGet | [XiHanFun](https://www.nuget.org/profiles/XiHanFun) |
| 规范 | [XiHanFun/framework-spec](https://github.com/XiHanFun/framework-spec) |
| Go SDK | [XiHanFun/framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) |

## 框架包

框架由一组可以独立引用的 NuGet 包组成，全部共享一条版本线。包名由术语表与文法推出：`XiHan.Framework.<领域>[.<角色>]`。多数应用只需要记住三个组合包，再按需加协议适配器与 Provider：

| 包 | 说明 |
| --- | --- |
| `XiHan.Framework.Profiles.Minimal` | 原语层、内核、ASP.NET Core 宿主、内存本地化 |
| `XiHan.Framework.Profiles.Standard` | Minimal 加领域模型，以及持久化、缓存、多租户、安全、消息五个子系统的抽象与内存默认实现 |
| `XiHan.Framework.Profiles.Full` | Standard 加对象存储、调度、审计、工作流的默认实现，AI 工具目录，OpenTelemetry 导出与运行信息，HTTP、gRPC、MCP、CloudEvents 四种协议适配器 |

常用的单包：

| 类别 | 包 |
| --- | --- |
| 原语与内核 | `Primitives`、`Kernel`、`Kernel.Generators`（源生成器）、`Kernel.Analyzers`（分析器） |
| 协议 | `Protocols.Http`、`Protocols.Grpc`、`Protocols.Mcp`、`Protocols.CloudEvents` |
| 宿主 | `Hosting.AspNetCore`、`Hosting.CommandLine` |
| 持久化 | `Persistence.Abstractions`、`Persistence.InMemory`、`Persistence.EntityFrameworkCore`、`Persistence.SqlSugar`、`Persistence.Generators` |
| 缓存 | `Caching.Abstractions`、`Caching.InMemory`、`Caching.Redis`、`Caching.Hybrid`、`Caching.Remote` |
| 消息 | `Messaging.Abstractions`、`Messaging.InMemory`、`Messaging.RabbitMQ`、`Messaging.Kafka`、`Messaging.Redis` |
| 安全 | `Security.Abstractions`、`Security.InMemory`、`Security.Database`、`Security.Jwt`、`Security.OpenIdConnect`、`Security.Ip2Region` |
| 多租户 | `MultiTenancy.Abstractions`、`MultiTenancy.InMemory`、`MultiTenancy.Database` |
| 其他子系统 | `ObjectStorage.*`、`Scheduling.*`、`Workflow.*`、`Auditing.*`、`Localization.*`、`Configuration.*`、`Observability.*`、`AI.*` |
| 插件 | `Plugins.Protocol`、`Plugins.InProcess`、`Plugins.OutOfProcess`、`Plugins.Wasm` |
| 工具链 | `Cli`（`xihan` 工具）、`Testing`、各 `<领域>.Conformance` |

完整清单与每个包的能力、配置节、主要类型见[包参考](/packages/)。

## 框架特点

- 微内核：内核只定义宿主、能力图谱、管道、诊断、安全原语、操作契约与插件抽象，公开类型有预算，第三方依赖为零。
- 按能力装配：特性在清单里按能力 ID 声明提供与依赖，内核在启动前求解整张图并打印；缺失、循环、冲突在启动期报错，不在第一次请求时报错。
- 固定阶段的管道：每次调用依次经过 Ingress 到 Egress 十一个阶段，认证、租户、授权、校验、幂等、事务都是阶段里的中间件，顺序确定、可打印、可单独测试。
- 一份契约多协议：操作用 `[Operation]` 声明一次，源生成器产出 JSON Schema；HTTP、gRPC、MCP 与命令行共用同一份 Schema、授权要求与错误码。
- 默认实现零外部依赖：每个子系统的抽象都配一个内存或文件系统实现，容量有界，通过一致性套件；换 Redis、EF Core、SqlSugar、RabbitMQ 只加包与配置，业务代码不变。
- 零反射与 Native AOT：原语层与内核不使用运行时反射，注册与 Schema 在编译期生成；示例应用以 Native AOT 发布作为 CI 冒烟项。
- 规范先于实现：原语、清单、上下文、管道阶段、协议映射与插件协议写在 XiHan Spec，Go SDK 与 .NET 参考实现通过同一套一致性向量。
- 边界由机器执行：依赖方向、包 ID 文法、命名空间、零反射、第三方类型泄漏、本地化文件齐全都由架构测试与分析器守护。

## 功能模块

| 模块 | 内容 | 章节 |
| --- | --- | --- |
| 应用启动 | 装配、特性与清单、能力图谱、生命周期与健康、依赖注入、管道、调用上下文 | [应用启动](/startup/bootstrap) |
| 配置 | 配置节、选项、配置目录热更新、资源绑定、远程配置与密钥 | [配置与选项](/configuration/options) |
| Web | ASP.NET Core 宿主、操作契约、HTTP 路由、端点、实时通信、OpenAPI | [Web 应用开发](/web/hosting) |
| 结果与校验 | `Result` 与 `Error`、错误码、Problem Details、契约校验 | [结果与错误处理](/web/errors)与[数据校验](/web/validation) |
| 数据 | 领域模型、仓储、工作单元、EF Core、SqlSugar、建表约定、连接、标识生成 | [数据库操作指南](/persistence/domain) |
| 多租户 | 租户解析与隔离、分片与分库、数据库租户目录 | [SaaS 多租户](/multi-tenancy/resolution) |
| 安全 | 认证、授权、RBAC 授予、条件引擎、IP 归属地、出站地址策略 | [安全鉴权](/security/authentication) |
| 缓存 | 缓存读写、分布式锁、幂等、两级缓存、键管理 | [分布式缓存](/caching/basics) |
| 消息 | 发布订阅、领域事件、发件箱、队列与流 | [消息与事件](/messaging/pubsub) |
| 协议 | gRPC、MCP、CloudEvents、命令行宿主 | [多协议暴露](/protocols/grpc) |
| 本地化 | 消息键、十种内置区域、回退链、数据库翻译 | [全球化和本地化](/common/localization) |
| 调度 | 作业、cron、集群分发、领导选举 | [定时任务](/scheduling/jobs) |
| 工作流 | 定义、实例、用户任务、活动、补偿 | [工作流](/workflow/definitions) |
| 对象存储 | 读写列举、分片上传、预签名、命名实例 | [对象存储](/object-storage/basics) |
| 审计与可观测性 | 审计事件与查询、追踪、指标、日志、运行信息 | [审计](/common/auditing)与[日志与可观测性](/common/observability) |
| AI | 模型与对话、护栏、操作即工具、知识库 | [AI](/ai/chat) |
| 弹性 | 截止时间、熔断、限流、并发上限 | [弹性与限流](/common/resilience) |
| 插件 | 进程内、进程外、Wasm 三级隔离，Go SDK | [插件](/plugins/overview) |
| 部署 | 应用清单、`xihan pack`、Kubernetes | [托管、部署、发布](/deployment/pack) |
| 测试 | 测试宿主、一致性套件、架构门禁 | [测试指南](/testing/basics) |

## 框架依赖

原语层、内核与全部抽象包不引用任何第三方库，只允许 `Microsoft.Extensions.*` 抽象包，内核另加 `System.Threading.RateLimiting`。第三方库只出现在按技术命名的 Provider 包里：

| 类别 | 技术 |
| --- | --- |
| 序列化 | System.Text.Json 源生成 |
| ORM Provider | EF Core、SqlSugar |
| 缓存 Provider | StackExchange.Redis |
| 消息 Provider | RabbitMQ.Client、Confluent.Kafka |
| 对象存储 Provider | AWSSDK.S3 |
| 认证 Provider | Microsoft.IdentityModel.JsonWebTokens |
| AI | Microsoft.Extensions.AI、Microsoft.Extensions.VectorData |
| gRPC | Grpc.AspNetCore.Server |
| 可观测性 | OpenTelemetry |
| Wasm 插件 | Wasmtime |

具体版本以仓库的 `framework/Directory.Packages.props` 为准。

## 环境要求

| 项 | 要求 |
| --- | --- |
| SDK | .NET 10 SDK；构建框架源码时用仓库 `global.json` 锁定的功能带 |
| 编辑器 | Visual Studio、JetBrains Rider 或 VS Code（C# Dev Kit） |
| 测试运行器 | Microsoft.Testing.Platform，由 `global.json` 启用 |
| 外部设施 | 默认实现不需要；Provider 按各自包页的说明 |

## 支持平台

| 类别 | 支持 |
| --- | --- |
| 运行环境 | Windows、Linux、macOS；容器与 Kubernetes；Native AOT 发布 |
| 数据库 | 由持久化 Provider 决定：EF Core 支持的数据库，SqlSugar 支持的数据库；一致性套件在 SQLite 上运行，PostgreSQL、MySQL 与 Oracle 在 CI 里实测 |
| 缓存 | 内存、Redis、两级缓存、远程插件 |
| 消息 | 内存、RabbitMQ、Kafka、CloudEvents Webhook |
| 对象存储 | 文件系统、Amazon S3 与兼容服务（MinIO、OSS、COS、R2） |
| 远程配置 | Nacos |
| 协议 | HTTP、gRPC、MCP、CloudEvents、命令行 |
| 插件语言 | .NET、Go，以及任何能编译到 Wasm 或实现 gRPC 的语言 |

Wasm 插件宿主在 Windows 上要求宿主可执行项目设置 `CETCompat=false`。

## 关于性能

内核的预算是：冷启动在 ASP.NET Core 基线之上，JIT 下不超过 100 毫秒、Native AOT 下少于 50 毫秒，单个中间件的管道执行少于 0.1 毫秒，空应用里框架额外占的常驻内存不超过 15 MB，原语层加内核的包少于 500 KB，基准回归超过 5% 阻断。基准项目在仓库的 `framework/benchmarks` 下，可以本地运行；我不贴测试图，因为它们只在特定机器上成立。

## 贡献代码

框架遵循 MIT 协议，欢迎提交 Pull Request 与 Issue。流程、代码规范与门禁见[贡献指南](/appendix/contributing)。感谢每一位为框架贡献代码、文档与一致性向量的朋友。

## 下一步

- [环境与安装](/getting-started/installation)：开始使用
