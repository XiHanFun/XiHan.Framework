# 框架概述

XiHan.Framework 是面向企业级应用的**模块化后端框架**，专为前后端分离的 ASP.NET Core 应用设计。它把一套完整的后端能力拆成许多职责单一的模块，通过 `[DependsOn]` 属性声明依赖、自动拓扑排序加载，并以**应用服务 + 动态 API** 约定统一对外暴露接口。

## 设计原则

- **分层架构** —— 遵循清晰的分层，避免循环依赖
- **依赖倒置** —— 高层模块不依赖低层模块，二者都依赖抽象接口
- **单一职责** —— 每个包只负责一个特定的功能领域
- **开闭原则** —— 对扩展开放、对修改关闭，通过接口和抽象类支持自定义
- **优先 .NET 10** —— 使用内置能力（DI、日志、序列化），仅在必要时引入第三方库
- **性能优化** —— 利用 .NET 10 高性能特性；AOT 不在支持范围（核心依赖 SqlSugar / Castle DynamicProxy / Newtonsoft.Json 暂不兼容裁剪）

## 为什么是"模块化"

传统项目里，`Program.cs` 常常堆满几十行 `builder.Services.AddXxx()`，顺序、依赖、初始化时机全靠人肉维护。XiHan.Framework 把每一块能力封装成一个**模块类**，模块自己负责：

- 注册自己需要的服务（`ConfigureServices`）
- 声明自己依赖谁（`[DependsOn]`）
- 在合适的生命周期节点接入中间件、后台任务等（`OnApplicationInitialization` 等）

你只需要在自己的应用模块上声明"我要用哪些模块"，框架会**自动把整棵依赖树按正确顺序装配好**。新增或移除一块能力，就是加/减一行 `[DependsOn]`。

> 想深入这套机制，见 [核心概念 · 模块系统](./guide/modularity) 与 [生命周期](./guide/lifecycle)。

## 分层架构

框架采用严格的模块化分层，通过 `[DependsOn]` 强制依赖关系，自动拓扑排序加载：

```text
┌─────────────────────────────────────────────────────────────────┐
│                         7. Web 层                                │
│  Web.Docs → Web.Api → Web.Core    Web.Gateway    Web.RealTime    │
│                                    Web.Grpc                       │
├─────────────────────────────────────────────────────────────────┤
│                       6. 基础设施层                              │
│  Data  Authentication  Authorization  Caching  EventBus          │
│  AI  Bot  Tasks  Traffic  Upgrade  Messaging  ObjectStorage      │
│  Logging  Observability  SearchEngines  Script  Http  Castle …   │
├─────────────────────────────────────────────────────────────────┤
│                        5. 应用层                                 │
│  Application → Application.Contracts                              │
│  MultiTenancy → MultiTenancy.Abstractions                        │
│  Validation → Validation.Abstractions      Settings              │
├─────────────────────────────────────────────────────────────────┤
│                        4. 领域层                                 │
│  Domain → Domain.Shared                                          │
├─────────────────────────────────────────────────────────────────┤
│                        3. 核心层                                 │
│  Core（模块系统 / DI / 生命周期 / 选项模式 / 异常处理）          │
├─────────────────────────────────────────────────────────────────┤
│                      2. 元数据层                                 │
│  Metadata（框架信息 / 版本 / 平台）                              │
├─────────────────────────────────────────────────────────────────┤
│                      1. 公共层                                   │
│  Utils（零依赖通用工具库）                                       │
└─────────────────────────────────────────────────────────────────┘
```

**分层规则**：任何模块只能依赖比它更低的层，绝不反向。这保证了依赖图无环、可裁剪、可测试。

## 命名约定

- `XiHan.Framework.[模块名]` —— 通用类库，使用 `Microsoft.NET.Sdk`
- `XiHan.Framework.Web.[模块名]` —— Web 相关功能，使用 `Microsoft.NET.Sdk.Web`
- 每个模块的入口类名为 `XiHan[模块名]Module`，继承 `XiHanModule`
- 纯工具库（`Utils` / `Metadata`）与分析器（`Analyzers`）没有模块类，直接引用即可

## 核心机制速览

| 机制 | 说明 | 深入 |
| --- | --- | --- |
| 模块系统 | `XiHanModule` + `[DependsOn]`，自动拓扑排序加载 | [模块系统](./guide/modularity) |
| 生命周期 | 服务注册 3 钩子 + 应用初始化 3 钩子 + 关机 1 钩子 | [生命周期](./guide/lifecycle) |
| 依赖注入 | 约定式注册（`ITransientDependency` 等）+ 选项模式 | [依赖注入](./guide/dependency-injection) |
| 动态 API | 应用服务无需写 Controller，自动暴露为 REST 接口 | [动态 API](./guide/dynamic-api) |

## 技术栈

| 类别 | 技术 | 版本 |
| --- | --- | --- |
| 运行时 | .NET | 10.0 |
| 语言 | C# | Latest |
| ORM | SqlSugar | 5.1.4.217 |
| 日志 | Serilog | 10.0.0 |
| 缓存 | HybridCache + StackExchange.Redis | 10.9.0 / 10.0.11 |
| AOP | Castle DynamicProxy | 5.2.1 |
| 加密 | BouncyCastle | 2.7.0 |
| 序列化 | System.Text.Json + Newtonsoft.Json | 13.0.4 |
| 模板引擎 | Scriban | 7.2.6 |
| AI | Microsoft.Extensions.AI + Microsoft.Agents.AI + MCP | 10.9.0 / 1.17.0 / 2.2.0 |
| HTTP 韧性 | Polly | 10.0.11 |
| gRPC | Grpc.AspNetCore | 2.83.0 |
| 实时通信 | ASP.NET Core SignalR | - |
| API 文档 | Scalar + Swashbuckle | 2.16.20 / 10.2.3 |
| IP 定位 | ip2region | 3.0.2 |
| 消息通知 | MailKit + Telegram.Bot | 4.17.0 / 22.10.2.1 |

## 环境要求

| 依赖 | 版本 |
| --- | --- |
| .NET SDK | 10.0+ |
| C# | Latest |
| 支持平台 | Windows / Linux / macOS |

## 下一步

- [快速上手](./quickstart)：动手建第一个 Web API
- [核心概念](./guide/modularity)：理解框架的运转方式
- [模块总览](./packages/)：查阅你要用的每一个包
