# 模块总览

XiHan.Framework 由 **66 个 NuGet 包**组成，按分层组织。本册是**参考手册**——每个包一页，写完整的 API 清单与配置项全表。

::: tip 先看指南还是先看这里
文档分两册，解决的问题不同：

| 你的问题 | 去哪 |
| --- | --- |
| 「**我要做 X**」——数据访问、事务、认证、缓存、多租户… | [开发指南](../guide/modularity)（38 章，按能力域组织） |
| 「**这个包有什么**」——完整 API、全部配置项、类型清单 | **本册** |

指南章节末尾都会链到对应的包页。不确定从哪开始就先读[快速上手](../quickstart)。
:::

> 命名约定：`XiHan.Framework.[模块名]` 为通用类库，`XiHan.Framework.Web.[模块名]` 为 Web 功能。安装即 `dotnet add package <包名>`。

## 如何选择要装哪些包

- 只想跑个 Web API？→ `Web.Api` + `Web.Docs`（依赖会自动带上 Core 等底层）
- 要读写数据库？→ 加 `Data`
- 要缓存/认证/授权？→ 按需加 `Caching` / `Authentication` / `Authorization`

大多数包无需单独安装——它们会作为上层包的 `[DependsOn]` 依赖被自动引入。

---

## 1. 公共与核心

框架的地基，几乎所有上层模块都依赖它们。

| 模块 | 说明 |
| --- | --- |
| [Utils](./utils) | 零依赖通用工具库：字符串、加密、异步、序列化、集合、反射、网络、文件 IO、数学、时间等 |
| [Metadata](./metadata) | 框架元数据：名称、版本、作者、组织、支持平台等静态信息 |
| [Core](./core) | 模块化引擎核心：`XiHanModule`、`[DependsOn]`、拓扑排序加载、生命周期钩子、DI 扩展、选项模式、异常处理 |
| [Analyzers](./analyzers) | Roslyn 代码分析器：编译期规范检查 |

## 2. 领域与应用

DDD 分层与应用服务契约。

| 模块 | 说明 |
| --- | --- |
| [Domain.Shared](./domain-shared) | 领域共享模型：基础实体类型、枚举、常量、值对象、异常 |
| [Domain](./domain) | DDD 领域层：聚合根、实体、领域服务、领域事件、规约、仓储抽象、业务规则引擎 |
| [Application.Contracts](./application-contracts) | 应用服务契约：DTO 定义、应用服务接口 |
| [Application](./application) | 应用层实现：应用服务基类、CRUD 基类、DTO 映射、动态 API 特性 |

## 3. 数据与持久化

| 模块 | 说明 |
| --- | --- |
| [Data](./data) | SqlSugar 数据访问：仓储模式、工作单元集成、多租户数据隔离、启动自动建表 |
| [Uow](./uow) | 工作单元：AOP 拦截器自动管理事务边界 |
| [Caching](./caching) | 混合缓存：HybridCache（内存 + Redis）、缓存拦截器、租户感知 |

## 4. 安全 · 认证 · 授权

| 模块 | 说明 |
| --- | --- |
| [Security](./security) | 安全与加密：BouncyCastle 企业级密码学、密钥管理、密码哈希、数据保护 |
| [Authentication](./authentication) | 认证：JWT / OAuth2 / OIDC、令牌工厂、MFA、SSO |
| [Authorization](./authorization) | 授权：RBAC、策略授权、声明授权 |

## 5. 多租户 · 配置 · 校验

| 模块 | 说明 |
| --- | --- |
| [MultiTenancy.Abstractions](./multitenancy-abstractions) | 多租户抽象：租户上下文接口、解析链 |
| [MultiTenancy](./multitenancy) | 多租户：租户解析中间件、数据隔离、租户配置管理、生命周期 |
| [Settings](./settings) | 设置管理：设置定义提供者模式、动态配置、多来源（租户级别） |
| [Validation.Abstractions](./validation-abstractions) | 校验抽象：校验异常与错误承载接口 |
| [Validation](./validation) | 数据校验：当前为薄占位，核心类型在抽象包 |

## 6. 事件 · 消息 · 通信

| 模块 | 说明 |
| --- | --- |
| [EventBus.Abstractions](./eventbus-abstractions) | 事件总线抽象：发布/订阅接口、事件处理管道 |
| [EventBus](./eventbus) | 事件总线：本地/分布式事件、Outbox 模式、事件存储 |
| [EventBus.RabbitMQ](./eventbus-rabbitmq) | 分布式事件总线的 RabbitMQ 提供程序：direct 交换机 + 事件名路由键、队列竞争消费 |
| [EventBus.Kafka](./eventbus-kafka) | 分布式事件总线的 Kafka 提供程序：单主题 + 事件名作 Key、消费者组竞争消费 |
| [EventBus.Redis](./eventbus-redis) | 分布式事件总线的 Redis Streams 提供程序：`XADD` / `XREADGROUP` 消费者组竞争消费 |
| [Messaging](./messaging) | 消息处理：消息代理抽象（发布/消费/路由） |
| [Http](./http) | HTTP 客户端：Polly 韧性策略（重试/熔断）、请求管道 |

## 7. 通用基础设施

| 模块 | 说明 |
| --- | --- |
| [Serialization](./serialization) | 序列化：基于 System.Text.Json 的动态 JSON 操作、序列化选项组合 |
| [ObjectMapping](./objectmapping) | 对象映射：Mapster 集成 |
| [Localization.Abstractions](./localization-abstractions) | 国际化抽象：`IStringLocalizer` 抽象层 |
| [Localization](./localization) | 国际化：多语言资源文件、动态文化切换 |
| [Logging](./logging) | 结构化日志：Serilog 集成、文件/控制台输出、异步写入 |
| [Auditing](./auditing) | 审计日志：6 类日志记录、Channel 异步队列 + 批量消费者、脱敏器、写入器契约（默认空实现） |
| [Castle](./castle) | AOP 动态代理：Castle DynamicProxy 集成、服务拦截器注册 |
| [Threading](./threading) | 并发辅助：取消令牌提供者、基于 AsyncLocal 的环境作用域 |
| [Timing](./timing) | 时间策略：时区管理、时间抽象 |
| [DistributedIds](./distributed-ids) | 分布式 ID：Snowflake / NanoId / SequentialGuid / Sqids 多方案 |

## 8. 存储 · 模板 · 任务 · 治理

| 模块 | 说明 |
| --- | --- |
| [ObjectStorage](./object-storage) | 对象存储：统一抽象 + 本地 / 阿里云 OSS / MinIO / 腾讯 COS 适配 |
| [VirtualFileSystem](./virtual-file-system) | 虚拟文件系统：本地目录 + 程序集嵌入资源统一挂载、内存版本快照 |
| [Templating](./templating) | 模板渲染：默认简单占位替换引擎 + 可选 Scriban、模板注册表 |
| [Tasks](./tasks) | 定时任务：调度引擎（Cron/间隔/延迟）、后台服务基类、多租户感知 |
| [Traffic](./traffic) | 流量治理：灰度路由（百分比/用户/租户/请求头）、限流与熔断策略接口 |
| [Upgrade](./upgrade) | 升级引擎：版本存储、迁移执行、分布式锁、启动自动检查 |
| [Script](./script) | 脚本引擎：基于 Roslyn 的 C# 动态脚本、编译校验与超时 |
| [Workflow.Abstractions](./workflow-abstractions) | 工作流抽象：流程定义模型、活动契约、书签与存储端口 |
| [Workflow](./workflow) | 工作流引擎：图执行、17 个内置活动、人工任务（审批）、表达式、定时器 |
| [SearchEngines.Abstractions](./search-engines-abstractions) | 搜索抽象：索引/文档/检索契约，零第三方依赖 |
| [SearchEngines](./search-engines) | 搜索默认实现：进程内引擎（开发与测试兜底） |
| [SearchEngines.Elasticsearch](./search-engines-elasticsearch) | 搜索实现：Elasticsearch（生产推荐） |
| [Observability](./observability) | 可观测性：OpenTelemetry、指标采集、性能监控、运行时诊断与内存健康检查 |
| [DevTools](./devtools) | 开发工具：自研命令行（CLI）框架，开发期辅助 |

## 9. AI 与机器人

| 模块 | 说明 |
| --- | --- |
| [AI.Abstractions](./ai-abstractions) | AI 抽象：AI 服务接口与契约 |
| [AI](./ai) | AI 集成：LLM 接入（OpenAI 兼容）、智能代理、MCP 协议、RAG |
| [Bot](./bot) | 机器人核心：多平台消息接入的统一抽象 |
| [Bot.Email](./bot-email) | 邮件通道：MailKit 集成 |
| [Bot.Sms](./bot-sms) | 短信通道 |
| [Bot.Telegram](./bot-telegram) | Telegram 机器人 |
| [Bot.DingTalk](./bot-dingtalk) | 钉钉机器人 |
| [Bot.Lark](./bot-lark) | 飞书机器人 |
| [Bot.WeCom](./bot-wecom) | 企业微信机器人 |

## 10. Web 层

| 模块 | 说明 |
| --- | --- |
| [Web.Core](./web-core) | Web 基础设施：托管环境、中间件管道、CORS、IP 地理定位（ip2region）、UA 解析 |
| [Web.Api](./web-api) | 动态 API：自动 API 发现与注册、OpenAPI 安全、完整中间件管道 |
| [Web.Docs](./web-docs) | API 文档：Scalar UI + Swagger UI、动态 API 分组发现 |
| [Web.Gateway](./web-gateway) | API 网关：灰度路由（依赖 Traffic）、请求追踪、限流熔断 |
| [Web.Grpc](./web-grpc) | gRPC 服务集成 |
| [Web.Mcp](./web-mcp) | MCP Server：把 AI 技能暴露为 HTTP MCP tools，并以 API Key fail-closed 鉴权 |
| [Web.RealTime](./web-realtime) | 实时通信：SignalR 集成、JSON 序列化 |

---

## 依赖关系速览

核心依赖链（从底层到上层）：

```text
Utils（零依赖）
  └ Metadata
      └ Core
          ├ Serialization
          ├ Security → Authentication → Authorization
          │     └ Auditing（+ MultiTenancy.Abstractions）
          ├ Threading / Timing / DistributedIds
          ├ VirtualFileSystem → Localization
          ├ Uow → Caching(+Redis) / EventBus
          │                          └ EventBus.RabbitMQ / EventBus.Kafka / EventBus.Redis
          ├ Domain.Shared → Domain → Data(SqlSugar)
          │     └ Application.Contracts → Application
          ├ MultiTenancy.Abstractions → MultiTenancy → Tasks / Traffic / Upgrade
          │     └ Workflow.Abstractions → Workflow(+Caching/EventBus/Script/Timing)
          ├ SearchEngines.Abstractions → SearchEngines → SearchEngines.Elasticsearch
          ├ Http(+Polly) → AI(Agents.AI + MCP) → Bot(MailKit + Telegram)
          └ Web.Core → Web.Api → Web.Docs / Web.Gateway / Web.Grpc / Web.Mcp / Web.RealTime
```

> 你只需在应用模块上 `[DependsOn]` 顶层包，整条依赖链会被框架自动装配。详见 [核心概念 · 模块系统](../guide/modularity)。
