# XiHan.Framework 架构

XiHan.Framework 的包架构，专为前后端分离的 ASP.NET Core 应用设计。框架基于 .NET 9，优先使用原生功能，减少第三方依赖，确保模块化、可扩展和易用性。命名空间根据项目类型区分：

- **XiHan.Framework.[ModuleName]**: 通用类库，使用 `Microsoft.NET.Sdk`。
- **XiHan.Framework.Web.[ModuleName]**: Web 相关功能，使用 `Microsoft.NET.Sdk.Web`。

## 设计原则

- **命名规范**：遵循 .NET 官方和开源项目常用命名，确保一致性。
- **模块化**：单一职责，清晰依赖，用户按需选择。
- **优先 .NET 9**：使用内置功能（如 DI、日志、序列化），仅在必要时兼容第三方库。
- **国内外环境**：提供中文文档，兼容国际标准（OpenAPI、gRPC）。
- **可扩展性**：通过接口和抽象类支持自定义。
- **性能优化**：利用 .NET 9 AOT 编译和高性能特性，提供性能基准数据。
- **快速启动**：确保用户下载后即可运行完整的 Web API 项目，快速体验框架功能。

## 元数据包

元数据包提供框架的基础信息和配置，是其他所有包的基础。

1. **XiHan.Framework**
   - **SDK**: Microsoft.NET.Sdk
   - **简要功能**: 框架元数据和基础配置。
   - **详细描述**: 包含框架版本信息等，为所有其他包提供基础支持。
   - **依赖**: 无（元数据包）。

## 核心基础包

提供框架最基础的功能，其他包都依赖这些基础功能。

2. **XiHan.Framework.Utils**

   - **SDK**: Microsoft.NET.Sdk
   - **简要功能**: 提供通用工具和扩展方法。
   - **详细描述**: 包含字符串处理、日期时间操作、加密解密、反射工具等实用方法，简化日常开发任务。
   - **依赖**: XiHan.Framework（元数据包）。

3. **XiHan.Framework.Core**

   - **SDK**: Microsoft.NET.Sdk
   - **简要功能**: 提供框架核心工具、依赖注入、模块化支持。
   - **详细描述**: 包含通用扩展方法、基类和配置管理工具，集成 .NET 9 内置 DI，支持服务注册与解析，模块化设计，提升开发效率。
   - **依赖**: XiHan.Framework（元数据包）。

4. **XiHan.Framework.Serialization**
   - **SDK**: Microsoft.NET.Sdk
   - **简要功能**: 支持对象序列化和反序列化。
   - **详细描述**: 基于 System.Text.Json 提供高效 JSON 序列化，支持自定义转换器，兼容 Newtonsoft.Json。
   - **依赖**: XiHan.Framework.Core。

## Web 核心包

确保用户能快速启动 Web API 项目，立即体验框架功能。

5. **XiHan.Framework.Web.Core**

   - **SDK**: Microsoft.NET.Sdk.Web
   - **简要功能**: 支持 Web 应用的基础配置和运行时环境。
   - **详细描述**: 提供 ASP.NET Core 的 WebHost 和 Kestrel 配置，简化中间件管道设置，适用于快速搭建 Web 项目。
   - **依赖**: XiHan.Framework.Core。

6. **XiHan.Framework.Web.Api**

   - **SDK**: Microsoft.NET.Sdk.Web
   - **简要功能**: 助力 RESTful API 开发与管理。
   - **详细描述**: 提供控制器基类、路由管理和模型绑定，支持 OpenAPI、System.Text.Json、API 版本控制，优化 API 开发体验。
   - **依赖**: XiHan.Framework.Web.Core, XiHan.Framework.Serialization。

7. **XiHan.Framework.Web.Docs**
   - **SDK**: Microsoft.NET.Sdk.Web
   - **简要功能**: 生成 API 文档和客户端代码。
   - **详细描述**: 集成 Swashbuckle 和 Scalar，支持交互式 OpenAPI 文档生成和客户端代码（如 TypeScript SDK）输出，方便开发与集成。
   - **依赖**: XiHan.Framework.Web.Api。

## 日志包

为开发和调试提供必要的日志支持，确保开发体验。

8. **XiHan.Framework.Logging**
   - **SDK**: Microsoft.NET.Sdk
   - **简要功能**: 统一日志记录与管理。
   - **详细描述**: 基于 .NET 9 日志框架，支持结构化日志、多目标输出（如文件、控制台），兼容 Serilog 等扩展库。
   - **依赖**: XiHan.Framework.Core。

## 数据验证包

支持 API 数据验证，提供完整的 Web API 体验。

9. **XiHan.Framework.Validation**

   - **SDK**: Microsoft.NET.Sdk
   - **简要功能**: 支持数据输入验证。
   - **详细描述**: 基于数据注解和 FluentValidation，提供模型验证和自定义规则，保障输入数据的可靠性。
   - **依赖**: XiHan.Framework.Core。

10. **XiHan.Framework.Data**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 提供数据库访问和 ORM 工具。
    - **详细描述**: 集成 Entity Framework Core、SqlSugar 和 Dapper，支持迁移、仓储模式和数据操作，适用于多种数据库场景。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Validation。

## 安全认证包

提供完整的认证授权解决方案。

11. **XiHan.Framework.Security**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 提供安全相关的基础功能。
    - **详细描述**: 包含加密解密、哈希算法、数字签名等安全工具，为认证授权提供基础支持。
    - **依赖**: XiHan.Framework.Core。

12. **XiHan.Framework.Authentication**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 实现用户身份认证功能。
    - **详细描述**: 支持 JWT、OAuth 2.0 和 OpenID Connect，集成 ASP.NET Core Identity，提供登录、令牌生成与验证功能。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Security。

13. **XiHan.Framework.Authorization**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 提供权限管理和访问控制。
    - **详细描述**: 支持角色、策略和声明的授权方式，与认证模块无缝协作，实现资源保护和权限校验。
    - **依赖**: XiHan.Framework.Authentication。

## 系统功能包

提供缓存、配置等系统级功能。

14. **XiHan.Framework.Caching**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 优化应用缓存管理。
    - **详细描述**: 支持内存缓存和分布式缓存（如 Redis），支持分级缓存、缓存优先级、缓存统计、缓存策略、缓存淘汰策略、缓存预热，提升性能和响应速度。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Serialization。

15. **XiHan.Framework.Settings**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 管理应用配置和设置。
    - **详细描述**: 支持动态配置、数据库存储配置、租户级设置等，灵活控制应用行为。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Data。

16. **XiHan.Framework.Threading**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 提供线程安全和异步编程工具。
    - **详细描述**: 包含锁机制、线程池管理、异步任务调度等功能，优化多线程应用开发。
    - **依赖**: XiHan.Framework.Core。

17. **XiHan.Framework.DistributedIds**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 生成分布式唯一 ID。
    - **详细描述**: 实现雪花算法（Snowflake）或其他 ID 生成策略，确保分布式系统中 ID 唯一性。
    - **依赖**: XiHan.Framework.Core。

## 高级数据包

18. **XiHan.Framework.Uow**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 实现工作单元模式。
    - **详细描述**: 管理数据库事务，确保数据操作一致性，支持 EF Core 和 Dapper。
    - **依赖**: XiHan.Framework.Data。

19. **XiHan.Framework.Ddd**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 支持领域驱动设计实现。
    - **详细描述**: 提供领域模型、聚合根和仓储设计，适用于复杂业务系统。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Data。

## 开发工具包

20. **XiHan.Framework.DevTools**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 提供开发与调试工具集。
    - **详细描述**: 包含 xUnit 测试框架、Moq 模拟工具和性能监控功能，助力开发、测试和问题排查。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Logging。

## 扩展功能包

### 通信和消息

21. **XiHan.Framework.HttpClient**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 封装 HTTP 客户端。
    - **详细描述**: 基于 HttpClientFactory，提供重试、超时、认证等功能，简化外部 API 调用。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Serialization。

22. **XiHan.Framework.Messaging**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 支持消息队列与事件驱动开发。
    - **详细描述**: 兼容 RabbitMQ 和 Kafka，提供消息发布与订阅机制，助力构建高扩展性事件驱动系统。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Serialization。

23. **XiHan.Framework.EventBus**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 支持事件总线和发布/订阅模式。
    - **详细描述**: 提供本地和分布式事件处理，支持 MediatR、RabbitMQ，解耦业务逻辑。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Messaging。

### 任务和模板

24. **XiHan.Framework.BackgroundJobs**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 管理后台任务和定时作业。
    - **详细描述**: 集成 Hangfire 或 Quartz.NET，支持异步任务队列和定时调度，提供任务监控和管理功能。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Logging。

25. **XiHan.Framework.Templating**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 支持文本模板生成。
    - **详细描述**: 集成 RazorLight 或 Scriban，提供动态模板渲染，用于邮件、报告和代码生成。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Serialization。

26. **XiHan.Framework.CodeGeneration**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 自动生成代码。
    - **详细描述**: 基于 T4 模板或 Roslyn，生成实体、DTO、控制器等代码，减少重复劳动。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Templating。

### 存储和搜索

27. **XiHan.Framework.FileSystem**

    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 管理文件存储和访问。
    - **详细描述**: 支持本地文件系统、云存储（如 AWS S3），提供文件上传、下载和权限控制。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Authentication。

28. **XiHan.Framework.SearchEngines**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 提供全文搜索和索引功能。
    - **详细描述**: 集成 Elasticsearch，支持高效搜索、数据索引和查询优化，适用于搜索密集型应用。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Serialization。

### 国际化和本地化

29. **XiHan.Framework.Localization**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 支持国际化与本地化。
    - **详细描述**: 基于 .NET 9 的 `IStringLocalizer` 和资源文件（`.resx`）实现多语言支持，支持动态语言切换和本地化中间件，适用于多语言应用。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Web.Core。

### 高级功能

30. **XiHan.Framework.AI**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 集成 AI 和机器学习功能。
    - **详细描述**: 提供 ML.NET 或 TensorFlow.NET 集成，支持模型训练、推理和智能推荐。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Serialization。

### Web 扩展

31. **XiHan.Framework.Web.RealTime**

    - **SDK**: Microsoft.NET.Sdk.Web
    - **简要功能**: 实现实时 Web 通信。
    - **详细描述**: 基于 ASP.NET Core SignalR，支持消息推送、实时通知和双向通信，适用于聊天、动态更新等场景。
    - **依赖**: XiHan.Framework.Web.Core, XiHan.Framework.Authentication。

32. **XiHan.Framework.ApiGateway**
    - **SDK**: Microsoft.NET.Sdk.Web
    - **简要功能**: 实现 API 网关。
    - **详细描述**: 基于 Ocelot 或 YARP，提供路由、负载均衡、认证等功能，管理微服务 API（可选模块）。
    - **依赖**: XiHan.Framework.Web.Core, XiHan.Framework.Authentication。

### 复杂架构模式

33. **XiHan.Framework.MultiTenancy**
    - **SDK**: Microsoft.NET.Sdk
    - **简要功能**: 支持多租户架构。
    - **详细描述**: 提供租户隔离策略、租户解析中间件和租户管理 API，支持数据和配置的租户级隔离，适用于 SaaS 应用。
    - **依赖**: XiHan.Framework.Core, XiHan.Framework.Data, XiHan.Framework.Settings, XiHan.Framework.Authentication。

## 前后端分离支持

- **XiHan.Framework.Web.Docs** 生成 OpenAPI 或 gRPC 客户端代码，支持 React、Vue、Angular 等前端框架。
- **XiHan.Framework.Web.Api** 和 **XiHan.Framework.Web.RealTime** 提供高效 API 和实时通信，简化前后端协作。

## 开发和发布流程

1. **项目结构**
   - 通用功能使用 `Microsoft.NET.Sdk`，命名为 `XiHan.Framework.[ModuleName]`。
   - Web 功能使用 `Microsoft.NET.Sdk.Web`，命名为 `XiHan.Framework.Web.[ModuleName]`。
2. **打包**  
   使用 `dotnet pack` 创建 NuGet 包，配置正确元数据（版本、描述、依赖）。
3. **发布**  
   在 NuGet.org 发布，支持预发布版本（如 `1.0.0-beta`）。
4. **版本管理**  
   遵循语义化版本控制（SemVer），同步所有包版本。
5. **CI/CD**  
   使用 GitHub Actions 自动化构建、测试和发布。

## 示例项目

提供 GitHub/Gitee 托管的示例项目，展示包的使用：

- **快速启动项目**：使用最小包集合 (`XiHan.Framework.Web.Api`, `XiHan.Framework.Web.Docs`) 创建可运行的 Web API。
- **完整 RESTful API**：`XiHan.Framework.Web.Api`, `XiHan.Framework.Data`, `XiHan.Framework.Authentication`。
- **认证和授权**：`XiHan.Framework.Authentication`, `XiHan.Framework.Authorization`。
- **实时通信**：`XiHan.Framework.Web.RealTime`。
- **监控和日志**：`XiHan.Framework.DevTools`, `XiHan.Framework.Logging`。
- **国际化与本地化**：`XiHan.Framework.Localization`。
- **多租户**：`XiHan.Framework.MultiTenancy`。

## 注意事项

- **精简依赖**：优先使用 .NET 9 原生功能（如内置 DI、日志、序列化），仅在必要时兼容第三方库（如 Redis、HotChocolate）。
- **国内环境**：提供中文文档，托管在 Gitee，支持国内 NuGet 镜像。
- **国际标准**：支持 OpenAPI、gRPC、Swagger，符合全球开发者习惯。
- **性能优化**：利用 .NET 9 AOT 编译，提供性能基准数据。
- **社区支持**：为每个包提供 GitHub/Gitee 仓库，支持问题反馈和贡献。
- **快速体验**：确保用户使用核心包组合能在 5 分钟内启动完整的 Web API 项目。

## 使用示例

### 快速启动（最小化配置）

```bash
// 添加元数据包（必需）
dotnet add package XiHan.Framework

// 添加 Web API 核心包（快速启动）
dotnet add package XiHan.Framework.Web.Api
dotnet add package XiHan.Framework.Web.Docs
dotnet add package XiHan.Framework.Logging
```

### 完整功能（推荐配置）

```bash
// 添加完整的 Web API 解决方案
dotnet add package XiHan.Framework.Web.Api
dotnet add package XiHan.Framework.Web.Docs
dotnet add package XiHan.Framework.Authentication
dotnet add package XiHan.Framework.Data
dotnet add package XiHan.Framework.Caching
dotnet add package XiHan.Framework.Validation
```

### 扩展功能（按需添加）

```bash
// 添加扩展包（按需）
dotnet add package XiHan.Framework.Web.RealTime
dotnet add package XiHan.Framework.BackgroundJobs
dotnet add package XiHan.Framework.Localization
dotnet add package XiHan.Framework.MultiTenancy
```
