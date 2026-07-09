![logo](./assets/logo.png)

[![GitHub Star](https://img.shields.io/github/stars/XiHanFun/XiHan.Framework?style=flat&logo=github)](https://github.com/XiHanFun/XiHan.Framework) | [![Gitee Star](https://gitee.com/XiHanFun/XiHan.Framework/badge/star.svg)](https://gitee.com/XiHanFun/XiHan.Framework) | [![AtomGit Star](https://atomgit.com/XiHanFun/XiHan.Framework/star/badge.svg)](https://atomgit.com/XiHanFun/XiHan.Framework)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/XiHanFun/XiHan.Framework)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/) | [![NuGet](https://img.shields.io/nuget/v/XiHan.Framework.Core?label=NuGet&color=blue)](https://www.nuget.org/packages?q=XiHan.Framework)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](./LICENSE)


[曦寒懿官方交流群](https://qm.qq.com/q/qYp1Urv3z2) 462371834 | [在线文档](https://docs.xihanfun.com)

# XiHan.Framework

快速、轻量、高效、用心的 .NET 模块化开发框架，基于 .NET 10 构建。

## 概述

XiHan.Framework 是面向企业级应用的模块化后端框架，专为前后端分离的 ASP.NET Core 应用设计。框架优先使用 .NET 原生功能，减少第三方依赖，强调模块清晰、依赖可控、扩展可维护。通过 `[DependsOn]` 属性声明模块依赖，自动拓扑排序加载，以应用服务与动态 API 约定统一接口暴露方式。

## 设计原则

- **分层架构** - 遵循清晰的分层原则，避免循环依赖
- **依赖倒置** - 高层模块不依赖低层模块，都依赖抽象接口
- **单一职责** - 每个包只负责一个特定的功能领域
- **开闭原则** - 对扩展开放，对修改关闭，通过接口和抽象类支持自定义
- **优先 .NET 10** - 使用内置功能（DI、日志、序列化），仅在必要时引入第三方库
- **性能优化** - 利用 .NET 10 高性能特性，目标支持 AOT 编译

## 技术栈

| 类别 | 技术 | 版本 |
| --- | --- | --- |
| 运行时 | .NET | 10.0 |
| 语言 | C# | Latest |
| ORM | SqlSugar | 5.1.4 |
| 日志 | Serilog | 10.0.0 |
| 缓存 | HybridCache + StackExchange.Redis | 10.5.0 |
| AOP | Castle DynamicProxy | 5.2.1 |
| 加密 | BouncyCastle | 2.6.2 |
| 序列化 | System.Text.Json + Newtonsoft.Json | 13.0.4 |
| 模板引擎 | Scriban | 7.1.0 |
| AI | Semantic Kernel + MCP | 1.74.0 / 1.2.0 |
| HTTP 韧性 | Polly | 10.0.6 |
| gRPC | Grpc.AspNetCore | 2.76.0 |
| 实时通信 | ASP.NET Core SignalR | - |
| API 文档 | Scalar + Swashbuckle | 2.14.1 / 10.1.7 |
| IP 定位 | ip2region | 3.0.2 |
| 消息通知 | MailKit + Telegram.Bot | 4.16.0 / 22.9.6 |
| 测试 | xunit v3 + coverlet | 3.2.2 / 8.0.1 |

## 架构概览

框架采用严格的模块化分层组织，通过 `[DependsOn]` 属性强制模块依赖关系，自动拓扑排序加载：

```text
┌─────────────────────────────────────────────────────────────────┐
│                         7. Web 层                              │
│  Web.Docs → Web.Api → Web.Core    Web.Gateway    Web.RealTime │
│                                    Web.Grpc                    │
├─────────────────────────────────────────────────────────────────┤
│                       6. 基础设施层                             │
│  Data  Authentication  Authorization  Caching  EventBus       │
│  AI  Bot  Tasks  Traffic  Upgrade  Messaging  ObjectStorage   │
│  Logging  Observability  SearchEngines  Script  Http  Castle  │
├─────────────────────────────────────────────────────────────────┤
│                        5. 应用层                               │
│  Application → Application.Contracts                           │
│  MultiTenancy → MultiTenancy.Abstractions                     │
│  Validation → Validation.Abstractions    Settings              │
├─────────────────────────────────────────────────────────────────┤
│                        4. 领域层                               │
│  Domain → Domain.Shared                                        │
├─────────────────────────────────────────────────────────────────┤
│                        3. 核心层                               │
│  Core (模块系统 / DI / 生命周期 / 选项模式 / 异常处理)          │
├─────────────────────────────────────────────────────────────────┤
│                      2. 元数据层                               │
│  Metadata (框架信息 / 版本 / 平台)                              │
├─────────────────────────────────────────────────────────────────┤
│                      1. 公共层                                 │
│  Utils (零依赖通用工具库)                                       │
└─────────────────────────────────────────────────────────────────┘
```

### 命名约定

- `XiHan.Framework.[ModuleName]` — 通用类库，使用 `Microsoft.NET.Sdk`
- `XiHan.Framework.Web.[ModuleName]` — Web 相关功能，使用 `Microsoft.NET.Sdk.Web`

## 模块清单

### 公共与核心

| 模块 | 说明 |
| --- | --- |
| `Utils` | 零依赖通用工具库：字符串处理、加密算法、异步编程、序列化、集合操作、反射、网络通信、文件IO、数学计算、时间处理等 |
| `Metadata` | 框架元数据：名称、版本、作者、组织、支持平台等静态信息 |
| `Core` | 模块化引擎核心：`IXiHanModule` 基类、`[DependsOn]` 依赖声明、拓扑排序加载、8 个生命周期钩子、DI 扩展、选项模式、异常处理链 |

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
| `Data` | SqlSugar 数据访问：仓储模式、工作单元集成、多租户数据隔离、启动自动建表 |
| `Caching` | 混合缓存：HybridCache（内存 + Redis）、缓存拦截器、租户感知 |
| `Authentication` | 认证：JWT / OAuth2 / OIDC、令牌工厂、MFA、SSO |
| `Authorization` | 授权：RBAC、策略授权、声明授权 |
| `Security` | 安全与加密：BouncyCastle 企业级密码学、密钥管理、密码哈希、数据保护 |
| `EventBus` | 事件总线：本地/分布式事件、Outbox 模式、事件存储 |
| `EventBus.Abstractions` | 事件总线抽象：发布/订阅接口、事件处理管道 |
| `Uow` | 工作单元：AOP 拦截器自动管理事务边界 |
| `Castle` | AOP 动态代理：Castle DynamicProxy 集成，服务拦截器注册 |
| `Logging` | 结构化日志：Serilog 集成、文件/控制台输出、异步写入 |
| `Serialization` | 序列化：System.Text.Json + Newtonsoft.Json 双引擎、策略管理 |
| `Http` | HTTP 客户端：Polly 韧性策略（重试/熔断）、请求管道 |
| `Localization` | 国际化：多语言资源文件、动态文化切换 |
| `Localization.Abstractions` | 国际化抽象：`IStringLocalizer` 抽象层 |
| `MultiTenancy` | 多租户：租户解析中间件、数据隔离、租户配置管理、生命周期 |
| `MultiTenancy.Abstractions` | 多租户抽象：租户上下文接口、解析链 |
| `Settings` | 设置管理：设置定义提供者模式、动态配置、多来源（租户级别） |
| `Validation` | 数据校验：校验实现 |
| `Validation.Abstractions` | 校验抽象：校验工厂、规则构建器接口 |
| `ObjectMapping` | 对象映射：Mapster 集成 |
| `ObjectStorage` | 对象存储：OSS / MinIO / S3 适配抽象 |
| `VirtualFileSystem` | 虚拟文件系统：本地/云存储适配、文件元数据、版本控制 |
| `Messaging` | 消息处理：消息代理抽象（发布/消费/路由） |
| `DistributedIds` | 分布式 ID：Snowflake / ULID / SQID / NanoID 多算法支持 |
| `Threading` | 并发控制：异步信号量、读写锁、优先级任务调度、背压控制 |
| `Timing` | 时间策略：时区管理、时间抽象 |
| `Templating` | 模板渲染：Scriban 引擎、模板注册表 |
| `Tasks` | 定时任务：调度引擎、后台服务、多租户感知 |
| `Traffic` | 流量治理：灰度路由、限流、熔断 |
| `Upgrade` | 升级引擎：版本存储、迁移执行、分布式锁、启动自动检查 |
| `AI` | AI 集成：Semantic Kernel、智能代理、MCP 协议支持 |
| `Bot` | 机器人：Telegram / 邮件（MailKit）/ 微信 / 钉钉 / 飞书 / 短信 多平台接入 |
| `Script` | 脚本引擎：沙箱执行、JS / Python / C# 动态脚本 |
| `SearchEngines` | 搜索引擎：Elasticsearch 集成抽象、索引构建、全文检索 |
| `Observability` | 可观测性：健康检查、性能计数器、指标采集 |
| `DevTools` | 开发工具：开发期辅助与调试能力 |

### Web 层

| 模块 | 说明 |
| --- | --- |
| `Web.Core` | Web 基础设施：托管环境、中间件管道、CORS、IP 地理定位（ip2region）、UA 解析 |
| `Web.Api` | 动态 API：自动 API 发现与注册、OpenAPI 安全、完整中间件管道（TraceId → 请求上下文 → 异常日志 → 路由 → CORS → 认证 → 租户解析 → 授权 → 控制器） |
| `Web.Docs` | API 文档：Scalar UI + Swagger UI、动态 API 分组发现 |
| `Web.Gateway` | API 网关：灰度路由、负载均衡、限流 |
| `Web.Grpc` | gRPC 服务集成 |
| `Web.RealTime` | 实时通信：SignalR 集成、JSON 序列化 |

## 快速开始

### 安装

通过 NuGet 安装所需模块：

```bash
# 安装核心模块
dotnet add package XiHan.Framework.Core

# 安装 Web API 模块（包含完整中间件管道）
dotnet add package XiHan.Framework.Web.Api

# 安装 API 文档模块
dotnet add package XiHan.Framework.Web.Docs

# 安装数据访问模块
dotnet add package XiHan.Framework.Data
```

### 定义模块

每个模块继承 `XiHanModule`，通过 `[DependsOn]` 声明依赖：

```csharp
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api;
using XiHan.Framework.Data;

[DependsOn(
    typeof(XiHanWebApiModule),
    typeof(XiHanDataModule)
)]
public class MyAppModule : XiHanModule
{
    public override Task ConfigureServicesAsync(ServiceConfigurationContext context)
    {
        // 注册服务
        return Task.CompletedTask;
    }

    public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        // 应用初始化
        return Task.CompletedTask;
    }
}
```

### 启动应用

```csharp
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
await builder.AddApplicationAsync<MyAppModule>();

var app = builder.Build();
await app.InitializeApplicationAsync();
await app.RunAsync();
```

### 模块生命周期

每个模块提供 8 个生命周期钩子，按拓扑排序顺序执行：

```text
服务注册阶段                          应用初始化阶段
┌──────────────────────┐            ┌───────────────────────────────┐
│ PreConfigureServices │            │ OnPreApplicationInitialization │
│ ConfigureServices    │     →      │ OnApplicationInitialization    │
│ PostConfigureServices│            │ OnPostApplicationInitialization│
└──────────────────────┘            └───────────────────────────────┘
                                                  ↓
                                    ┌───────────────────────────────┐
                                    │ OnApplicationShutdown          │
                                    └───────────────────────────────┘
```

## 项目结构

```text
XiHan.Framework/
├── framework/
│   ├── XiHan.Framework.slnx              # 解决方案文件
│   ├── src/                               # 源码（47 个模块）
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
│   │   ├── XiHan.Framework.Web.Docs/      #   API 文档
│   │   ├── XiHan.Framework.Web.Gateway/   #   网关
│   │   ├── XiHan.Framework.Web.Grpc/      #   gRPC
│   │   ├── XiHan.Framework.Web.RealTime/  #   实时通信
│   │   └── ...                            #   其他模块
│   ├── test/                              # 测试（9 个项目）
│   │   ├── XiHan.Framework.TestsBase/     #   测试基础设施
│   │   ├── XiHan.Framework.Utils.Tests/   #   工具测试
│   │   ├── XiHan.Framework.Http.Tests/    #   HTTP 测试
│   │   ├── XiHan.Framework.Web.Api.Tests/ #   Web API 测试
│   │   ├── XiHan.Framework.Integration.Tests/ # 集成测试
│   │   └── ...                            #   其他测试
│   ├── tool/                              # 工具
│   │   └── Region/                        #   代码规范化工具
│   ├── docs/                              # 架构文档
│   ├── props/                             # 共享 MSBuild 属性
│   ├── scripts/                           # NuGet 发布与运维脚本
│   └── nupkgs/                            # NuGet 包输出
└── assets/                                # README 资源文件
```

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
              │     └── EventBus
              ├── Domain.Shared ──→ Domain ──→ Data (SqlSugar)
              │     └── Application.Contracts ──→ Application
              ├── MultiTenancy.Abstractions ──→ MultiTenancy
              │     ├── Tasks
              │     ├── Traffic
              │     └── Upgrade
              ├── Http (+ Polly) ──→ AI (SemanticKernel + MCP)
              │     └── Bot (MailKit + Telegram)
              └── Web.Core
                    ├── Web.Api ──→ Web.Docs (Scalar + Swagger)
                    ├── Web.Gateway
                    ├── Web.Grpc
                    └── Web.RealTime (SignalR)
```

## NuGet 包

所有模块均发布至 [NuGet.org](https://www.nuget.org/packages?q=XiHan.Framework)，包名与项目名一致：

```bash
# 搜索所有 XiHan.Framework 包
dotnet package search XiHan.Framework
```

| 常用包 | 用途 |
| --- | --- |
| `XiHan.Framework.Core` | 模块化核心（必装） |
| `XiHan.Framework.Web.Api` | Web API 全套中间件 |
| `XiHan.Framework.Web.Docs` | Scalar + Swagger 文档 |
| `XiHan.Framework.Data` | SqlSugar 数据访问 |
| `XiHan.Framework.Caching` | HybridCache + Redis |
| `XiHan.Framework.Authentication` | JWT / OAuth2 认证 |
| `XiHan.Framework.Authorization` | RBAC 授权 |
| `XiHan.Framework.EventBus` | 事件总线 + Outbox |
| `XiHan.Framework.AI` | Semantic Kernel + MCP |

## 环境要求

| 依赖 | 版本 |
| --- | --- |
| .NET SDK | 10.0+ |
| C# | Latest |
| 支持平台 | Windows / Linux / macOS |

## 相关项目

- [XiHan.BasicApp](https://github.com/XiHanFun/XiHan.BasicApp) - 基于 XiHan.Framework 构建的企业级管理系统

## 贡献

欢迎提交 Issue 和 Pull Request。

## 诚挚致谢

排名不分先后。

| 项目                                                       | 致谢                                           |
| ---------------------------------------------------------- | ---------------------------------------------- |
| [Abp](https://github.com/abpframework/abp)                 | 作为部分架构和逻辑灵感来源（启蒙项目）         |
| [Furion](https://gitee.com/dotnetchina/Furion)             | 作为部分架构和逻辑灵感来源                     |
| 其他第三方依赖                                             | 作为项目功能丰富与拓展的基石                   |

## 支持&赞助

如果此项目对你的开发有助益，也欢迎请作者一杯咖啡。

<table>
  <tr>
    <td align="center"><img src="./assets/alipay.png" width="200" /><br/>支付宝</td>
    <td align="center"><img src="./assets/weixinpay.png" width="200" /><br/>微信</td>
  </tr>
</table>

## 版权&授权

Copyright (c) 2026 XiHanFun and ZhaiFanhua

本项目采用 MIT 授权，详见 [License](./LICENSE)

XiHan.Framework Logo、XiHan.Framework名称归作者所有，第三方依赖和第三方服务分别遵循其各自授权与服务条款。

项目仅供学习参考，作者不承担任何软件的使用风险。
