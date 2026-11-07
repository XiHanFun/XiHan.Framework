# 组合包与选包

框架有六十多个包。这一页告诉你不用记住它们：先选一个组合包，再按对外暴露的方式加协议适配器，最后按生产环境的外部设施加 Provider。读完它，你能为任何一个项目列出要引用的包。

## 为什么需要组合包

抽象与实现分包、每个领域一个包，带来的代价是「包太多不知道装哪个」。组合包解决这个问题：它是元包，只引用、不含代码，等于预先写好的一串 `UseFeature`。粒度没有牺牲，任何时候都可以退回到单包引用。

## 三个组合包

| 组合包 | 包含 | 装配方法 | 适合 |
| --- | --- | --- | --- |
| `Profiles.Minimal` | 原语层、内核、ASP.NET Core 宿主、内存本地化 | `UseMinimalProfile()` | 边缘服务、工具、插件宿主、只要几个操作的服务 |
| `Profiles.Standard` | Minimal 加领域模型，以及持久化、缓存、多租户、安全、消息五个子系统的抽象与内存默认实现 | `UseStandardProfile()` | 大多数业务服务 |
| `Profiles.Full` | Standard 加对象存储、调度、审计、工作流的默认实现，AI 工具目录，OpenTelemetry 导出与运行信息，HTTP、gRPC、MCP、CloudEvents 四种协议适配器 | `UseFullProfile()` | 中后台、SaaS |

它们在代码里就是嵌套的 `UseFeature`：

```csharp
// UseStandardProfile() 展开后
builder
    .UseMinimalProfile()
    .UseFeature<InMemoryPersistenceFeature>()
    .UseFeature<InMemoryCachingFeature>()
    .UseFeature<InMemoryMultiTenancyFeature>()
    .UseFeature<InMemorySecurityFeature>()
    .UseFeature<InMemoryMessagingFeature>();
```

```csharp
// UseFullProfile() 展开后
builder
    .UseStandardProfile()
    .UseFeature<FileSystemObjectStorageFeature>()
    .UseFeature<InMemorySchedulingFeature>()
    .UseFeature<InMemoryAuditingFeature>()
    .UseFeature<InMemoryWorkflowFeature>()
    .UseFeature<AIToolsFeature>()
    .UseFeature<OpenTelemetryObservabilityFeature>()
    .UseFeature<HostObservabilityFeature>()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<GrpcProtocolFeature>()
    .UseFeature<McpProtocolFeature>()
    .UseFeature<CloudEventsProtocolFeature>();
```

Full 组合装配了协议特性，但端点仍要在宿主上映射：`MapXiHanOperations()`、`MapXiHanGrpc()`、`MapXiHanMcp()`、`MapXiHanCloudEvents()`。

## 选包的四步

### 第一步：选组合包

按上表选。拿不准时选 Standard：它在零外部设施下就能运行，又覆盖了多数业务需要的横切能力。

### 第二步：加协议适配器

Minimal 与 Standard 不含协议适配器，按对外暴露的方式加：

| 要暴露为 | 加 |
| --- | --- |
| REST 与 OpenAPI | `Protocols.Http` |
| gRPC | `Protocols.Grpc` |
| MCP tool | `Protocols.Mcp` |
| 接收或发送 CloudEvents | `Protocols.CloudEvents` |
| 命令行命令或 MCP stdio | `Hosting.CommandLine`（替代 ASP.NET Core 宿主） |

### 第三步：加 Provider

默认实现是内存或文件系统，容量有界，进程重启数据就没了。上线前按外部设施加 Provider，每个都要 `UseFeature` 加入，默认实现自动让位：

| 子系统 | 默认实现 | Provider |
| --- | --- | --- |
| 持久化 | `Persistence.InMemory` | `Persistence.EntityFrameworkCore`、`Persistence.SqlSugar` |
| 缓存与分布式锁 | `Caching.InMemory` | `Caching.Redis`、`Caching.Hybrid`、`Caching.Remote` |
| 消息 | `Messaging.InMemory` | `Messaging.RabbitMQ`、`Messaging.Kafka` |
| 认证 | `Security.InMemory`（ApiKey） | `Security.Jwt`、`Security.OpenIdConnect` |
| 授权 | `Security.InMemory`（RBAC 授予） | `Security.Database` |
| 多租户 | `MultiTenancy.InMemory` | `MultiTenancy.Database` |
| 本地化 | `Localization.InMemory` | `Localization.Database` |
| 对象存储 | `ObjectStorage.FileSystem` | `ObjectStorage.S3` |
| 调度 | `Scheduling.InMemory` | `Scheduling.Database`、`Scheduling.Kubernetes` |
| 工作流 | `Workflow.InMemory` | `Workflow.Database` |
| 审计 | `Auditing.InMemory` | `Auditing.Database` |
| 远程配置 | .NET 原生配置（无包） | `Configuration.Nacos` |
| AI | `AI.Abstractions` 的工具目录 | `AI.OpenAI`、`AI.VectorData` |

`.Database` 后缀的 Provider 经持久化子系统的仓储读写，数据库由你选中的持久化 Provider 决定。

### 第四步：加开发期依赖

| 包 | 作用 |
| --- | --- |
| `Kernel.Generators` | 从 `[Operation]` 生成登记代码与 JSON Schema，写操作就需要 |
| `Persistence.Generators` | 从实体类生成仓储登记、EF Core 实体登记与过滤规约，用持久化就建议加 |
| `Kernel.Analyzers` | 分析器：稳定性标记、禁用词、`Internal` 引用、反射；写 Provider 或能力包时加 |
| `<领域>.Conformance` | 一致性套件；写 Provider 时在测试项目里引用 |
| `Testing` | 测试宿主与上下文构造；写测试时引用 |

## 三个典型组合

一个内部工具服务：

```bash
dotnet add package XiHan.Framework.Profiles.Minimal --prerelease
dotnet add package XiHan.Framework.Protocols.Http --prerelease
dotnet add package XiHan.Framework.Kernel.Generators --prerelease
```

一个用 PostgreSQL、Redis 与 RabbitMQ 的业务服务：

```bash
dotnet add package XiHan.Framework.Profiles.Standard --prerelease
dotnet add package XiHan.Framework.Protocols.Http --prerelease
dotnet add package XiHan.Framework.Persistence.EntityFrameworkCore --prerelease
dotnet add package XiHan.Framework.Caching.Redis --prerelease
dotnet add package XiHan.Framework.Messaging.RabbitMQ --prerelease
dotnet add package XiHan.Framework.Security.Jwt --prerelease
dotnet add package XiHan.Framework.Kernel.Generators --prerelease
dotnet add package XiHan.Framework.Persistence.Generators --prerelease
```

一个多租户 SaaS 中后台：

```bash
dotnet add package XiHan.Framework.Profiles.Full --prerelease
dotnet add package XiHan.Framework.Persistence.SqlSugar --prerelease
dotnet add package XiHan.Framework.Caching.Redis --prerelease
dotnet add package XiHan.Framework.MultiTenancy.Database --prerelease
dotnet add package XiHan.Framework.Security.Jwt --prerelease
dotnet add package XiHan.Framework.Security.Database --prerelease
dotnet add package XiHan.Framework.ObjectStorage.S3 --prerelease
dotnet add package XiHan.Framework.Scheduling.Database --prerelease
dotnet add package XiHan.Framework.Workflow.Database --prerelease
dotnet add package XiHan.Framework.Auditing.Database --prerelease
dotnet add package XiHan.Framework.Kernel.Generators --prerelease
dotnet add package XiHan.Framework.Persistence.Generators --prerelease
```

## 常见问题

### 引用了 Provider 包，但运行时仍在用内存实现

只引用包不会生效，还要在 `AddXiHan` 里 `UseFeature<...>()` 加入它的特性。加入后看启动日志里的能力图谱：被顶替的默认实现不再出现在该能力下。

### 两个 Provider 提供同一能力，启动报 KERNEL_CAPABILITY_004

同一能力有两个以上显式 Provider，内核不替应用选择。在配置 `XiHan:Providers:<能力 ID>` 里写清单 ID，或在代码里 `UseProvider`，见[能力图谱与 Provider](/startup/capabilities)。

### 不想用组合包，可以只引用单包吗

可以。组合包只是 `UseFeature` 的简写，`Profiles.Minimal` 的内容等于 `Kernel`、`Hosting.AspNetCore` 与 `Localization.InMemory` 三个包。

### 哪些包不能 Native AOT

接外部设施的 Provider 多数依赖的第三方库不支持 AOT（EF Core、SqlSugar、Redis、RabbitMQ、Kafka、S3、JWT、OIDC、插件宿主）。各包页标注了是否 AOT 兼容，见[JSON 序列化与 Native AOT](/common/serialization)。

## 下一步

- [项目结构建议](/getting-started/project-structure)：解决方案怎么分层
- [包参考](/packages/)：每个包的能力 ID、配置键与主要类型
