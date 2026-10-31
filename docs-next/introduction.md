# XiHan Framework 5

XiHan Framework 5 是一套语言中立的应用框架规范（XiHan Spec）及其 .NET 10 参考实现，以微内核、能力图谱与管道执行为核心。本页说明框架的整体形态、最小写法与文档组织，适合第一次接触 5.0 的读者。

::: warning 5.0 处于预览阶段
当前版本 `5.0.0-preview.1`，尚未发布到 nuget.org，取得方式见[快速上手](./quickstart#获取预览包)。5.0 在 `next` 分支从零重写，与 4.x 互不引用；4.x 单独维护，不提供 4.x 到 5.0 的迁移工具，差异见[从 4.x 迁移](./guide/upgrade)。
:::

## 框架的形态

应用由若干特性组成。特性是实现 `IFeature` 的类，带一份清单（`Manifest`），清单写明它提供哪些能力、依赖哪些能力，依赖以能力 ID（如 `persistence.relational@1`）声明，不按包名或类型。应用在启动时把特性交给内核，内核求解能力图谱、按拓扑顺序调用各特性的 `Configure` 注册服务与中间件，构建期就能发现缺失的依赖、版本冲突与循环。

每次调用都经过一条固定十一个阶段的管道：`Ingress → Observe → Resilience → Authenticate → Tenant → Authorize → Validate → Idempotency → Transaction → Handle → Egress`。认证、租户解析、授权、契约校验、幂等与工作单元都是某个阶段里的中间件，由对应子系统的特性贡献。

业务以操作契约声明：一个类实现 `IOperation<TRequest, TResponse>` 并标注 `[Operation]`，源生成器产出登记代码与请求、响应的 JSON Schema。协议适配器把 HTTP 请求、gRPC 调用、MCP 的 `tools/call` 与命令行参数翻译成同一种调用，交给同一条管道执行。

持久化、缓存、消息、安全、多租户等子系统各有一个抽象包（`<领域>.Abstractions`），配一个零外部依赖、容量有界的默认实现（`InMemory`、`FileSystem`），再由 Provider 包接入 EF Core、SqlSugar、Redis、RabbitMQ、Kafka、S3 等外部设施。换 Provider 只加包、加一行 `UseFeature`，业务代码不变。

## 最小写法

下面三段代码组成一个可运行的 Web 应用：一个操作、一个特性、一个入口。完整步骤与项目文件见[快速上手](./quickstart)。

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace HelloXiHan;

// 操作 ID greetings.create 经 HTTP 适配器推导为 POST /greetings
[Operation(Id, Stability = Stability.Experimental, Expose = Expose.Http)]
public sealed class Greet : IOperation<GreetRequest, GreetResponse>
{
    public const string Id = "greetings.create";

    public ValueTask<Result<GreetResponse>> HandleAsync(GreetRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(new GreetResponse($"Hello, {request.Name}!", context.TraceId)));
}

public sealed record GreetRequest(string Name);

public sealed record GreetResponse(string Message, string TraceId);
```

```csharp
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace HelloXiHan;

// 特性：清单声明依赖内核宿主能力，Configure 登记源生成器产出的操作
public sealed class GreetingsFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.hello.greetings",
        Package = "HelloXiHan",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(GreetingsFeature).FullName,
    };

    public void Configure(FeatureContext context) => context.AddGeneratedOperations();
}
```

```csharp
using HelloXiHan;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseMinimalProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<GreetingsFeature>());

var app = builder.Build();

app.UseXiHan();            // HTTP 请求进入内核管道
app.MapXiHanHealth();      // /health/startup、/health/ready、/health/live
app.MapXiHanOperations();  // 按登记表映射操作路由

app.Run();
```

操作需要的 `JsonSerializerContext`（`[JsonSerializable(typeof(GreetRequest))]` 等）在快速上手里给出：内核不经反射序列化，源生成器据此检查请求与响应类型已登记。

## 文档怎么组织

文档分两册：

| 册 | 定位 | 什么时候看 |
| --- | --- | --- |
| [开发指南](./guide/modularity) | 按能力域编号的任务式文档 | 要做某件事：装配特性、写操作、接数据库、做授权、发消息 |
| [包总览](./packages/) | 按 NuGet 包组织的参考手册，每包一页 | 要查某个包：提供什么能力、配置键、主要类型、限制 |

指南章节链接到对应的包页，两册不重复大段内容。给 AI 工具读取的纯文本版本见 [LLMs.txt](./ai/llms)。

## 开发指南速览

| 章 | 内容 |
| --- | --- |
| 1–5 | 能力包与清单 · 生命周期与健康 · 依赖注入 · 执行管道 · 配置与选项 |
| 6–10 | 宿主与 Web 应用 · 操作契约 · HTTP 与 OpenAPI · gRPC、MCP 与 CloudEvents · 命令行宿主 |
| 11–12 | 结果与错误 · 数据校验 |
| 13–16 | 领域模型 · 持久化 · 工作单元与事务 · 多租户 |
| 17–18 | 认证 · 授权 |
| 19–23 | 缓存与分布式锁 · 消息与事件 · 调度与后台作业 · 工作流 · 实时通信 |
| 24–27 | 对象存储 · 国际化 · 审计 · 可观测性 |
| 28–31 | 序列化与 Native AOT · 标识生成 · AI · 弹性与限流 |
| 32–34 | 插件 · 部署与打包 · 测试与一致性套件 |
| 35–37 | 从 4.x 迁移 · 扩展与二次开发 · 常见问题 |

## 环层一览

包按环层组织，依赖只能指向更低的环，同环之间只经抽象包往来，规则由架构测试执行：

```text
┌──────────────────────────────────────────────────────────────────────┐
│ Ring 6  组合包          Profiles.Minimal / Standard / Full             │
├──────────────────────────────────────────────────────────────────────┤
│ Ring 5  协议与插件      Protocols.Http / Grpc / Mcp / CloudEvents       │
│                        Plugins.InProcess / OutOfProcess / Wasm         │
├──────────────────────────────────────────────────────────────────────┤
│ Ring 4  宿主            Hosting.AspNetCore / Hosting.CommandLine        │
├──────────────────────────────────────────────────────────────────────┤
│ Ring 3  实现            <领域>.InMemory 等默认实现与 Redis、EF Core 等 Provider │
├──────────────────────────────────────────────────────────────────────┤
│ Ring 2  抽象            Domain 与 <领域>.Abstractions                    │
├──────────────────────────────────────────────────────────────────────┤
│ Ring 1  内核            Kernel：Hosting / Capabilities / Pipeline /      │
│                        Operations / Security / Plugins 等命名空间        │
├──────────────────────────────────────────────────────────────────────┤
│ Ring 0  原语层          Primitives：Result / Error / Maybe /             │
│                        InvocationContext / 清单模型 / 管道阶段           │
└──────────────────────────────────────────────────────────────────────┘
  工具链（Analyzers、Generators、Conformance、Testing、Cli）可引用任何环
```

每个包所在的环与提供的能力见[包总览](./packages/)。

## 技术栈

| 类别 | 技术 | 版本 |
| --- | --- | --- |
| 运行时 | .NET | 10.0 |
| 内核依赖 | `Microsoft.Extensions.*` 抽象包、`System.Threading.RateLimiting` | 10.0.13 |
| 序列化 | System.Text.Json 源生成 | 随 .NET 10 |
| ORM Provider | EF Core / SqlSugar | 10.0.13 / 5.1.4.221 |
| 缓存 Provider | StackExchange.Redis | 3.4.5 |
| 消息 Provider | RabbitMQ.Client / Confluent.Kafka | 7.2.2 / 2.16.0 |
| 对象存储 Provider | AWSSDK.S3 | 4.0.102.3 |
| 认证 Provider | Microsoft.IdentityModel.JsonWebTokens | 8.23.0 |
| AI | Microsoft.Extensions.AI / VectorData | 10.11.0 |
| gRPC | Grpc.AspNetCore.Server | 2.84.0 |
| 可观测性 | OpenTelemetry | 1.20.1 |
| Wasm 插件 | Wasmtime | 48.0.2 |

原语层、内核与全部抽象包不引用第三方库；第三方库只出现在按技术命名的 Provider 包里。

## 社区资源

- [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework)（5.0 在 `next` 分支） · [Gitee 镜像](https://gitee.com/XiHanFun/XiHan.Framework)
- [XiHan Spec](https://github.com/XiHanFun/framework-spec)：RFC、术语表、能力 ID、错误码与一致性向量
- [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go)：Go SDK，用于编写进程外插件
- [XiHan.BasicApp](https://github.com/XiHanFun/XiHan.BasicApp)：基于本框架的企业级中后台，6.0 正在接入 5.0
- [问题反馈](https://github.com/XiHanFun/XiHan.Framework/issues) · QQ 群 462371834
