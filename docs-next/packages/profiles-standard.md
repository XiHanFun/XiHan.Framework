# XiHan.Framework.Profiles.Standard

Standard 组合包（元包）：Minimal 加 `Domain` 与持久化、缓存、多租户、安全、消息五个子系统的抽象与内存默认实现，`UseStandardProfile()` 装配它们。大多数业务服务从这里开始，零外部基础设施即可运行。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Profiles.Standard` |
| 环层 | Ring 6（组合包） |
| 能力 | 自身不声明；装配的默认实现提供的能力见下文 |
| 特性入口 | `UseStandardProfile()` |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Profiles.Minimal`、`XiHan.Framework.Domain`，以及 `Persistence`、`Caching`、`MultiTenancy`、`Security`、`Messaging` 各自的 `.Abstractions` 与 `.InMemory` |
| AOT | 兼容 |
| 稳定性 | Stable |

## 概述

`UseStandardProfile()` 先调用 `UseMinimalProfile()`，再装配五个内存默认实现：

| 特性 | 包 | 提供的能力 |
| --- | --- | --- |
| `InMemoryLocalizationFeature`（来自 Minimal） | `Localization.InMemory` | `localization.resources@1` |
| `InMemoryPersistenceFeature` | `Persistence.InMemory` | `persistence.relational@1` |
| `InMemoryCachingFeature` | `Caching.InMemory` | `caching.local@1`、`caching.distributed@1` |
| `InMemoryMultiTenancyFeature` | `MultiTenancy.InMemory` | `multitenancy.context@1`、`multitenancy.sharding@1` |
| `InMemorySecurityFeature` | `Security.InMemory` | `security.authentication@1`、`security.authorization@1` |
| `InMemoryMessagingFeature` | `Messaging.InMemory` | `messaging.queue@1` |

它们都是清单里的 `role: default`。再 `UseFeature` 一个声明同一能力的显式 Provider（`EntityFrameworkCorePersistenceFeature<TContext>`、`RedisCachingFeature`、`JwtSecurityFeature`、`RabbitMQMessagingFeature` 等），能力求解就选中 Provider。默认实现提供的能力全部被顶替时不装配、不启动；还有能力被选中时照常装配，只为被选中的能力注册服务，例如加入 `RedisCachingFeature` 后 `Caching.InMemory` 仍提供 `caching.local`，加入 `JwtSecurityFeature` 后 `Security.InMemory` 仍提供授权（RFC-0002，[能力包与清单](../guide/modularity)）。换 Provider 只加包引用、`UseFeature` 与配置，业务代码不变。

这些特性填满管道的中段：Authenticate（ApiKey）→ Tenant（声明或 Baggage）→ Authorize（策略与权限）→ Idempotency（幂等键）→ Transaction（工作单元）。本地化不占阶段：宿主在 Ingress 把 `Accept-Language` 协商进 Baggage `xihan.locale`，在 Egress 把失败结果翻译成 Problem Details 的 `detail`。

## 何时使用

- 业务服务，需要仓储、缓存、租户、鉴权与消息，开发与测试阶段不想起外部服务：用 Standard。
- 还需要对象存储、调度、审计、工作流、OpenTelemetry 与四种协议适配器：用 [Profiles.Full](./profiles-full)。
- 只要内核与宿主：用 [Profiles.Minimal](./profiles-minimal)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Profiles.Standard" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    // 可选：事务性发件箱
    .UseFeature<OutboxFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.MapXiHanOperations();
app.Run();
```

`samples/SimpleApp` 是完整示例：Standard 组合加 HTTP、MCP、gRPC、CloudEvents 协议适配器与两个业务能力包，也是 Native AOT 冒烟示例。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `StandardProfile` | 静态类，扩展方法 `UseStandardProfile(this AppBuilder)` |

## 行为与限制

- 内存默认实现都有容量上限，进程重启即丢数据，适合开发、测试与单进程场景；上线前按需换 Provider，各默认实现的容量与语义见其包页。
- `Security.InMemory` 从配置装载 ApiKey 主体与授予，配置里的密钥只用开发值。
- 组合包不读配置，也不改任何默认值；各特性的配置节照常生效。

## 相关

- [能力包与清单](../guide/modularity)、[执行管道](../guide/pipeline)
- [Profiles.Minimal](./profiles-minimal)、[Profiles.Full](./profiles-full)
- [Persistence.InMemory](./persistence-inmemory)、[Caching.InMemory](./caching-inmemory)、[MultiTenancy.InMemory](./multitenancy-inmemory)、[Security.InMemory](./security-inmemory)、[Messaging.InMemory](./messaging-inmemory)
