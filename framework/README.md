# XiHan.Framework 工程说明

本文件面向框架的开发者与贡献者，记录**框架自身的组织方式**：分层架构、模块清单、目录结构与依赖关系。

想快速上手用框架搭应用，请看[仓库根 README](../README.md)；完整 API 与逐包文档见[文档站](https://framework.docs.xihanfun.com)。

## 架构概览

框架采用严格的模块化分层组织，通过 `[DependsOn]` 属性强制模块依赖关系，自动拓扑排序加载：

```text
┌─────────────────────────────────────────────────────────────────┐
│                         7. Web 层                               │
│  Web.Docs → Web.Api → Web.Core    Web.Gateway    Web.RealTime   │
│                                   Web.Grpc       Web.Mcp        │
├─────────────────────────────────────────────────────────────────┤
│                       6. 基础设施层                              │
│  Data  Uow  Caching  EventBus  Auditing  Security               │
│  Authentication  Authorization  AI  Bot  Workflow  Tasks        │
│  Traffic  Upgrade  Messaging  ObjectStorage  SearchEngines      │
│  Logging  Observability  Serialization  Script  Http  Castle    │
├─────────────────────────────────────────────────────────────────┤
│                        5. 应用层                                 │
│  Application → Application.Contracts                            │
│  MultiTenancy → MultiTenancy.Abstractions                       │
│  Validation → Validation.Abstractions    Settings               │
├─────────────────────────────────────────────────────────────────┤
│                        4. 领域层                                 │
│  Domain → Domain.Shared                                         │
├─────────────────────────────────────────────────────────────────┤
│                        3. 核心层                                 │
│  Core (模块系统 / DI / 生命周期 / 选项模式 / 异常处理)             │
├─────────────────────────────────────────────────────────────────┤
│                      2. 元数据层                                 │
│  Metadata (框架信息 / 版本 / 平台)                                │
├─────────────────────────────────────────────────────────────────┤
│                      1. 公共层                                   │
│  Utils (零依赖通用工具库)                                         │
└─────────────────────────────────────────────────────────────────┘
```

### 命名约定

- `XiHan.Framework.[ModuleName]` — 通用类库，使用 `Microsoft.NET.Sdk`
- `XiHan.Framework.Web.[ModuleName]` — Web 相关功能，使用 `Microsoft.NET.Sdk.Web`
- `XiHan.Framework.[ModuleName].Abstractions` — 只含契约、不含实现的抽象包，供第三方实现替换
- `XiHan.Framework.[ModuleName].[Provider]` — 某个抽象包的具体提供程序实现（如 `EventBus.Kafka`）

## 模块清单

共 66 个模块，与 `framework/src` 下的工程一一对应，包名与工程名一致。

### 公共与核心

| 模块 | 说明 |
| --- | --- |
| `Metadata` | 框架元数据：名称、版本、作者、组织、支持平台等静态信息 |
| `Utils` | 零依赖通用工具库：字符串处理、加密算法、异步编程、序列化、集合操作、反射、网络通信、文件IO、数学计算、时间处理等 |
| `Analyzers` | Roslyn 分析器：文件头规范检查与代码修复（`XiHanFileHeaderAnalyzer` + CodeFixProvider），编译期静态检查 |
| `Core` | 模块化引擎核心：`IXiHanModule` 基类、`[DependsOn]` 依赖声明、拓扑排序加载、7 个生命周期钩子、DI 扩展、选项模式、异常处理链 |

### 领域与应用

| 模块 | 说明 |
| --- | --- |
| `Domain.Shared` | 领域共享模型：基础实体类型、枚举、常量、值对象、异常 |
| `Domain` | DDD 领域层：聚合根、实体、领域服务、领域事件、规约、仓储抽象、业务规则引擎 |
| `Application.Contracts` | 应用服务契约：DTO 定义、应用服务接口 |
| `Application` | 应用层实现：应用服务基类、CQRS 调度、请求管道、DTO 映射 |

### 基础设施

| 模块 | 说明 |
| --- | --- |
| `Data` | SqlSugar 数据访问：仓储模式、工作单元集成、多租户数据隔离、模块分库、启动自动建表 |
| `Uow` | 工作单元：AOP 拦截器自动管理事务边界 |
| `Caching` | 混合缓存：HybridCache（内存 + Redis）、缓存拦截器、租户感知 |
| `Authentication` | 认证：JWT / OAuth2 / OIDC、令牌工厂、MFA、SSO |
| `Authorization` | 授权：RBAC、策略授权、声明授权 |
| `Security` | 安全与加密：BouncyCastle 企业级密码学、密钥管理、密码哈希、数据保护 |
| `Auditing` | 审计日志：操作/访问/登录/异常/接口/实体变更日志的采集管道、异步队列、脱敏与写入契约 |
| `EventBus.Abstractions` | 事件总线抽象：发布/订阅接口、事件处理管道 |
| `EventBus` | 事件总线：本地/分布式事件、Outbox 模式、事件存储（内置实现，分布式 Broker 由以下子包提供） |
| `EventBus.RabbitMQ` | 分布式事件总线 RabbitMQ 提供程序 |
| `EventBus.Kafka` | 分布式事件总线 Kafka 提供程序 |
| `EventBus.Redis` | 分布式事件总线 Redis（Streams）提供程序 |
| `Workflow.Abstractions` | 工作流抽象：流程定义模型、活动契约、运行时实例与书签模型、存储端口、人工任务契约，不含执行实现 |
| `Workflow` | 工作流引擎：图执行引擎、内置活动集、人工任务（审批）、表达式求值、定时器调度、内存存储默认实现 |
| `Castle` | AOP 动态代理：Castle DynamicProxy 集成，服务拦截器注册 |
| `Logging` | 结构化日志：Serilog 集成、文件/控制台输出、异步写入 |
| `Serialization` | 序列化：System.Text.Json + Newtonsoft.Json 双引擎、策略管理 |
| `Http` | HTTP 客户端：Polly 韧性策略（重试/熔断）、请求管道 |
| `Localization.Abstractions` | 国际化抽象：`IStringLocalizer` 抽象层 |
| `Localization` | 国际化：多语言资源文件、动态文化切换 |
| `MultiTenancy.Abstractions` | 多租户抽象：租户上下文接口、解析链 |
| `MultiTenancy` | 多租户：租户解析中间件、数据隔离、租户配置管理、生命周期 |
| `Settings` | 设置管理：设置定义提供者模式、动态配置、多来源（租户级别） |
| `Validation.Abstractions` | 校验抽象：校验工厂、规则构建器接口 |
| `Validation` | 数据校验：校验实现 |
| `ObjectMapping` | 对象映射：Mapster 集成 |
| `ObjectStorage` | 对象存储：OSS / MinIO / S3 适配抽象 |
| `VirtualFileSystem` | 虚拟文件系统：本地/云存储适配、文件元数据、版本控制 |
| `Messaging` | 消息处理：消息代理抽象（发布/消费/路由） |
| `DistributedIds` | 分布式 ID：Snowflake / ULID / SQID / NanoID 多算法支持 |
| `Threading` | 并发控制：异步信号量、读写锁、优先级任务调度、背压控制 |
| `Timing` | 时间策略：时区管理、时间抽象 |
| `Templating` | 模板渲染：Scriban 引擎、模板注册表 |
| `Tasks` | 定时任务与后台作业：调度引擎、后台服务、多租户感知 |
| `Traffic` | 流量治理：灰度路由、限流、熔断 |
| `Upgrade` | 升级引擎：版本存储、迁移执行、分布式锁、启动自动检查 |
| `AI.Abstractions` | AI 抽象层：智能体、对话、配置、护栏、提示词、RAG、技能等接口契约 |
| `AI` | AI 集成：Microsoft.Extensions.AI 统一模型抽象、Microsoft.Agents.AI 智能体框架、MCP 协议支持 |
| `Bot` | 机器人核心：多渠道消息分发管道、策略与模板，渠道能力由以下子包提供 |
| `Bot.Email` | 机器人邮件渠道：基于 MailKit |
| `Bot.Sms` | 机器人短信渠道 |
| `Bot.Telegram` | 机器人 Telegram 渠道：基于 Telegram.Bot |
| `Bot.DingTalk` | 机器人钉钉渠道 |
| `Bot.Lark` | 机器人飞书渠道 |
| `Bot.WeCom` | 机器人企业微信渠道 |
| `Script` | 脚本引擎：沙箱执行、JS / Python / C# 动态脚本 |
| `SearchEngines.Abstractions` | 搜索引擎抽象：索引、文档、检索请求与结果的统一契约，零第三方依赖 |
| `SearchEngines` | 搜索引擎：契约的内存实现与索引构建、全文检索的公共能力 |
| `SearchEngines.Elasticsearch` | 搜索引擎契约的 Elasticsearch 实现 |
| `Observability` | 可观测性：健康检查、性能计数器、指标采集、OpenTelemetry 链路 |
| `DevTools` | 开发工具：开发期辅助与调试能力 |

### Web 层

| 模块 | 说明 |
| --- | --- |
| `Web.Core` | Web 基础设施：托管环境、中间件管道、CORS、IP 地理定位（ip2region）、UA 解析 |
| `Web.Api` | 动态 API：自动 API 发现与注册、OpenAPI 安全、完整中间件管道（TraceId → 请求上下文 → 异常日志 → 路由 → CORS → 认证 → 租户解析 → 授权 → 控制器） |
| `Web.Docs` | API 文档：Scalar UI + Swagger UI、动态 API 分组发现 |
| `Web.Gateway` | API 网关：灰度路由、负载均衡、限流 |
| `Web.Grpc` | gRPC 服务集成 |
| `Web.Mcp` | MCP Server：AI 技能经 HTTP 传输暴露为 MCP tools、应用管理 key 鉴权 |
| `Web.RealTime` | 实时通信：SignalR 集成、JSON 序列化 |

## 模块依赖关系

核心依赖链（从底层到上层）：

```text
Utils (零依赖)
  └── Metadata (零依赖)
        └── Core
              ├── Serialization
              ├── Security ──→ Authentication ──→ Authorization
              ├── Threading
              ├── Timing
              ├── DistributedIds
              ├── VirtualFileSystem ──→ Localization
              ├── Uow
              │     ├── Caching (+ Redis)
              │     └── EventBus ──→ EventBus.RabbitMQ / Kafka / Redis
              ├── Domain.Shared ──→ Domain ──→ Data (SqlSugar)
              │     └── Application.Contracts ──→ Application
              ├── MultiTenancy.Abstractions ──→ MultiTenancy
              │     ├── Tasks
              │     ├── Traffic
              │     └── Upgrade
              ├── Workflow.Abstractions ──→ Workflow
              ├── SearchEngines.Abstractions ──→ SearchEngines ──→ SearchEngines.Elasticsearch
              ├── Http (+ Polly) ──→ AI (Microsoft.Extensions.AI + Agents.AI + MCP)
              │     └── Bot (MailKit + Telegram)
              └── Web.Core
                    ├── Web.Api ──→ Web.Docs (Scalar + Swagger)
                    ├── Web.Gateway
                    ├── Web.Grpc
                    ├── Web.Mcp (MCP Server)
                    └── Web.RealTime (SignalR)
```

## 项目结构

```text
XiHan.Framework/
├── framework/
│   ├── XiHan.Framework.slnx              # 解决方案文件
│   ├── src/                               # 源码（66 个模块）
│   │   ├── XiHan.Framework.Utils/         #   公共工具
│   │   ├── XiHan.Framework.Metadata/      #   框架元数据
│   │   ├── XiHan.Framework.Core/          #   模块化核心
│   │   ├── XiHan.Framework.Domain.Shared/ #   领域共享
│   │   ├── XiHan.Framework.Domain/        #   领域层
│   │   ├── XiHan.Framework.Application.Contracts/ # 应用契约
│   │   ├── XiHan.Framework.Application/   #   应用层
│   │   ├── XiHan.Framework.Data/          #   数据访问
│   │   ├── XiHan.Framework.Web.Core/      #   Web 核心
│   │   ├── XiHan.Framework.Web.Api/       #   动态 API
│   │   └── ...                            #   其他模块
│   ├── test/                              # 测试（src 下每个项目一一对应，共 66 个单测工程）
│   │   ├── XiHan.Framework.Utils.Tests/   #   工具测试
│   │   ├── XiHan.Framework.Core.Tests/    #   内核测试
│   │   └── ...                            #   其余按 <项目名>.Tests 一一对应
│   ├── sample/                            # 可运行示例宿主（非测试工程）
│   │   ├── XiHan.Framework.Web.Host/      #   Web 示例宿主（动态 API + 文档站）
│   │   └── XiHan.Framework.Integration.Host/ # 模块装配示例宿主
│   ├── tool/                              # 工具
│   │   └── Region/                        #   代码规范化工具
│   ├── props/                             # 共享 MSBuild 属性
│   ├── scripts/                           # NuGet 发布与运维脚本
│   └── nupkgs/                            # NuGet 包输出
├── docs/                                  # 文档站源码（VitePress，部署到 framework.docs.xihanfun.com）
└── assets/                                # README 资源文件
```

## 本地构建与测试

```bash
# 还原与构建
dotnet restore framework/XiHan.Framework.slnx
dotnet build framework/XiHan.Framework.slnx --configuration Release

# 全量测试（MTP 模式）
dotnet test --solution framework/XiHan.Framework.slnx --configuration Release
```

CI 会在此基础上采集覆盖率并跑覆盖率门禁，详见 [.github/workflows/ci.yml](../.github/workflows/ci.yml)。
