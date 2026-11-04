<div align="center">
<img src="./assets/banner_cn.png" alt="XiHan.Framework" />

<h1>XiHan.Framework</h1>

<p><b>快速、轻量、高效、用心的 .NET 现代模块化开发框架</b></p>

<p>基于 .NET 构建 · 按能力装配 · 一份操作契约服务多种协议 · 默认实现零外部依赖</p>

<p><a href="./README.md">English</a> | <b>简体中文</b></p>

<p>
  <a href="https://github.com/XiHanFun/XiHan.Framework/stargazers"><img alt="GitHub Stars" src="https://img.shields.io/github/stars/XiHanFun/XiHan.Framework?style=flat-square&logo=github&label=Stars&color=1f6feb" /></a>
  <a href="https://gitee.com/XiHanFun/XiHan.Framework"><img alt="Gitee Stars" src="https://gitee.com/XiHanFun/XiHan.Framework/badge/star.svg" /></a>
  <a href="https://gitcode.com/XiHanFun/XiHan.Framework"><img alt="GitCode Stars" src="https://gitcode.com/XiHanFun/XiHan.Framework/star/badge.svg" /></a>
</p>
<p>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-512BD4?style=flat-square&logo=dotnet&logoColor=white" />
  <img alt="C#" src="https://img.shields.io/badge/C%23-Latest-239120?style=flat-square" />
  <a href="https://www.nuget.org/packages?q=XiHan.Framework"><img alt="NuGet" src="https://img.shields.io/nuget/vpre/XiHan.Framework.Kernel?style=flat-square&logo=nuget&logoColor=white&label=NuGet&color=004880" /></a>
  <a href="https://www.nuget.org/packages/XiHan.Framework.Kernel"><img alt="Downloads" src="https://img.shields.io/nuget/dt/XiHan.Framework.Kernel?style=flat-square&logo=nuget&logoColor=white&label=Downloads&color=004880" /></a>
</p>



<p>
  <a href="./LICENSE"><img alt="License" src="https://img.shields.io/github/license/XiHanFun/XiHan.Framework?style=flat-square&color=green" /></a>
  <a href="https://github.com/XiHanFun/XiHan.Framework/commits"><img alt="Last Commit" src="https://img.shields.io/github/last-commit/XiHanFun/XiHan.Framework?style=flat-square&color=blueviolet" /></a>
  <img alt="Commit Activity" src="https://img.shields.io/github/commit-activity/m/XiHanFun/XiHan.Framework?style=flat-square" />
  <a href="https://github.com/XiHanFun/XiHan.Framework/issues"><img alt="Issues" src="https://img.shields.io/github/issues/XiHanFun/XiHan.Framework?style=flat-square" /></a>
  <a href="https://github.com/XiHanFun/XiHan.Framework/graphs/contributors"><img alt="Contributors" src="https://img.shields.io/github/contributors/XiHanFun/XiHan.Framework?style=flat-square" /></a>
  <img alt="Repo Size" src="https://img.shields.io/github/repo-size/XiHanFun/XiHan.Framework?style=flat-square" />
</p>

<p>
  <a href="https://zread.ai/XiHanFun/XiHan.Framework" target="_blank"><img src="https://img.shields.io/badge/Ask_Zread-_.svg?style=flat&color=00b0aa&labelColor=000000&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB3aWR0aD0iMTYiIGhlaWdodD0iMTYiIHZpZXdCb3g9IjAgMCAxNiAxNiIgZmlsbD0ibm9uZSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj4KPHBhdGggZD0iTTQuOTYxNTYgMS42MDAxSDIuMjQxNTZDMS44ODgxIDEuNjAwMSAxLjYwMTU2IDEuODg2NjQgMS42MDE1NiAyLjI0MDFWNC45NjAxQzEuNjAxNTYgNS4zMTM1NiAxLjg4ODEgNS42MDAxIDIuMjQxNTYgNS42MDAxSDQuOTYxNTZDNS4zMTUwMiA1LjYwMDEgNS42MDE1NiA1LjMxMzU2IDUuNjAxNTYgNC45NjAxVjIuMjQwMUM1LjYwMTU2IDEuODg2NjQgNS4zMTUwMiAxLjYwMDEgNC45NjE1NiAxLjYwMDFaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik00Ljk2MTU2IDEwLjM5OTlIMi4yNDE1NkMxLjg4ODEgMTAuMzk5OSAxLjYwMTU2IDEwLjY4NjQgMS42MDE1NiAxMS4wMzk5VjEzLjc1OTlDMS42MDE1NiAxNC4xMTM0IDEuODg4MSAxNC4zOTk5IDIuMjQxNTYgMTQuMzk5OUg0Ljk2MTU2QzUuMzE1MDIgMTQuMzk5OSA1LjYwMTU2IDE0LjExMzQgNS42MDE1NiAxMy43NTk5VjExLjAzOTlDNS42MDE1NiAxMC42ODY0IDUuMzE1MDIgMTAuMzk5OSA0Ljk2MTU2IDEwLjM5OTlaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik0xMy43NTg0IDEuNjAwMUgxMS4wMzg0QzEwLjY4NSAxLjYwMDEgMTAuMzk4NCAxLjg4NjY0IDEwLjM5ODQgMi4yNDAxVjQuOTYwMUMxMC4zOTg0IDUuMzEzNTYgMTAuNjg1IDUuNjAwMSAxMS4wMzg0IDUuNjAwMUgxMy43NTg0QzE0LjExMTkgNS42MDAxIDE0LjM5ODQgNS4zMTM1NiAxNC4zOTg0IDQuOTYwMVYyLjI0MDFDMTQuMzk4NCAxLjg4NjY0IDE0LjExMTkgMS42MDAxIDEzLjc1ODQgMS42MDAxWiIgZmlsbD0iI2ZmZiIvPgo8cGF0aCBkPSJNNCAxMkwxMiA0TDQgMTJaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik00IDEyTDEyIDQiIHN0cm9rZT0iI2ZmZiIgc3Ryb2tlLXdpZHRoPSIxLjUiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCIvPgo8L3N2Zz4K&logoColor=ffffff" alt="zread"/></a>
  <a href="https://deepwiki.com/XiHanFun/XiHan.Framework"><img alt="Ask DeepWiki" src="https://deepwiki.com/badge.svg" /></a>
  <a href="https://framework.docs.xihanfun.com"><img alt="Docs" src="https://img.shields.io/badge/Docs-framework.docs.xihanfun.com-2496ED?style=flat-square&logo=readthedocs&logoColor=white" /></a>
  <a href="https://qm.qq.com/q/qYp1Urv3z2"><img alt="QQ Group" src="https://img.shields.io/badge/QQ_Group-462371834-EB1923?style=flat-square&logo=tencentqq&logoColor=white" /></a>
</p>
<p>
  <a href="https://trendshift.io/repositories/83128?utm_source=trendshift-badge&amp;utm_medium=badge&amp;utm_campaign=badge-trendshift-83128" target="_blank" rel="noopener noreferrer"><img src="https://trendshift.io/api/badge/trendshift/repositories/83128/daily?language=C%23" alt="XiHanFun%2FXiHan.Framework | Trendshift" width="250" height="55"/></a>
</p>

</div>

## 概述

XiHan.Framework 是面向企业级应用的 .NET 模块化框架。能力按环层拆成独立的包，依赖按能力 ID 写在特性清单里；业务接口是一份与协议无关的操作契约，同时在 HTTP、gRPC、MCP 和命令行上服务；每个抽象都有零外部基础设施的默认实现，换 Provider 只改包引用与配置。

## 文档

| 去处 | 内容 |
| --- | --- |
| [文档站](https://framework.docs.xihanfun.com) | 完整指南与逐包 API 文档 |
| [框架工程说明](./framework/README.md) | 包清单、示例、门禁、目录结构 |
| [设计文档](./design/框架总体方案.md) | 总体方案、架构说明、ADR |
| [规范](https://github.com/XiHanFun/framework-spec) | RFC、术语表、清单 Schema、协议映射、一致性向量 |
| [更新日志](https://framework.docs.xihanfun.com/changelog) | 各版本变更与升级须知 |
| [贡献指南](https://docs.xihanfun.com/cosmos/contributing) | 分支约定、提交规范、本地构建与测试 |

## 设计原则

- **环层依赖** - 原语层 → 内核 → 抽象 → 实现 → 宿主 → 协议与插件 → 组合包，只能向下依赖
- **按能力装配** - 特性清单声明提供与依赖的能力，启动时求解能力图谱，不按包名耦合
- **协议无关** - 一份操作契约服务多种协议，共用同一条管道与同一份 Schema
- **默认实现可独立工作** - 每个抽象配一个内存或文件系统实现，容量有界，通过一致性套件
- **规范先于实现** - 公开契约先进 RFC，再写代码与一致性向量
- **失败是值** - 可恢复的失败返回 `Result<T>`，不抛异常
- **AOT 友好** - 内核零反射、源生成序列化，示例 SimpleApp 可发布为 Native AOT；依赖反射的 Provider 在包说明里标注

## 技术栈

| 类别 | 技术 |
| --- | --- |
| 运行时 | .NET 10 |
| 语言 | C# |
| 持久化 | 内存、Entity Framework Core、SqlSugar |
| 缓存 | 内存、Redis、两级缓存 |
| 消息 | 内存、RabbitMQ、Kafka，CloudEvents 信封 |
| 对象存储 | 文件系统、S3 兼容服务 |
| 安全 | API Key、JWT、OpenID Connect、RBAC |
| 配置 | .NET 配置、Nacos |
| 可观测 | OpenTelemetry |
| AI | Microsoft.Extensions.AI、Microsoft.Extensions.VectorData |
| 协议 | HTTP（OpenAPI 3.1）、gRPC、MCP、CloudEvents、命令行 |
| 插件 | 进程内（AssemblyLoadContext）、进程外（gRPC）、Wasm（wasmtime） |
| 测试 | xunit.v3 + Microsoft.Testing.Platform |

各依赖的具体版本以 `framework/Directory.Packages.props` 为准。

## 快速开始

### 安装

```bash
dotnet add package XiHan.Framework.Profiles.Minimal --prerelease
dotnet add package XiHan.Framework.Protocols.Http --prerelease
dotnet add package XiHan.Framework.Kernel.Generators --prerelease
```

要调试框架源码或使用本地包源，见[快速上手](https://framework.docs.xihanfun.com/quickstart)。

### 写操作契约

操作 ID `greetings.create` 的资源是 `greetings`、动词是 `create`，HTTP 适配器据此推出 `POST /greetings`。内核不经反射序列化，请求与响应类型登记到源生成的序列化上下文：

```csharp
[Operation(Id, Stability = Stability.Experimental, Expose = Expose.Http)]
public sealed class Greet : IOperation<GreetRequest, GreetResponse>
{
    public const string Id = "greetings.create";

    public ValueTask<Result<GreetResponse>> HandleAsync(GreetRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(new GreetResponse($"Hello, {request.Name}!", context.TraceId)));
}

public sealed record GreetRequest(string Name);

public sealed record GreetResponse(string Message, string TraceId);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GreetRequest))]
[JsonSerializable(typeof(GreetResponse))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

### 写特性

清单声明这个特性依赖内核的宿主能力，`Configure` 调用源生成器产出的登记方法：

```csharp
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

### 启动应用

```csharp
var builder = WebApplication.CreateBuilder(args);

// 把特性交给内核：求解能力图谱，按拓扑顺序调用各特性的 Configure
builder.AddXiHan(xihan => xihan
    .UseMinimalProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<GreetingsFeature>());

var app = builder.Build();

app.UseXiHan();            // HTTP 请求进入内核管道
app.MapXiHanHealth();      // /health/startup, /health/ready, /health/live
app.MapXiHanOperations();  // 按操作登记表映射路由

app.Run();
```

### 生命周期与管道

应用按状态机启动和停止，任一步失败进入 `Failed`：

```text
Declared → Resolved → Configured → Started → Ready → (Degraded) → Draining → Stopped
```

每次调用都走同一条固定阶段的管道：

```text
ingress → observe → resilience → authenticate → tenant → authorize → validate → idempotency → transaction → handle → egress
```

## NuGet 包

所有包均发布至 [NuGet.org](https://www.nuget.org/packages?q=XiHan.Framework)，包名与项目名一致。

| 常用包 | 用途 |
| --- | --- |
| `XiHan.Framework.Profiles.Minimal` | 组合包：原语层 + 内核 + ASP.NET Core 宿主 + 内存本地化 |
| `XiHan.Framework.Profiles.Standard` | 组合包：Minimal + 领域 + 五个子系统的抽象与内存默认实现 |
| `XiHan.Framework.Profiles.Full` | 组合包：Standard + 对象存储、调度、审计、工作流、AI 工具桥、OpenTelemetry 与四种协议适配器 |
| `XiHan.Framework.Protocols.Http` | HTTP 协议适配器：操作 ID 推导路由、OpenAPI |
| `XiHan.Framework.Kernel.Generators` | 源生成器：`[Operation]` → 登记代码与 JSON Schema |
| `XiHan.Framework.Persistence.EntityFrameworkCore` | EF Core Provider |
| `XiHan.Framework.Persistence.SqlSugar` | SqlSugar Provider |
| `XiHan.Framework.Caching.Redis` | Redis Provider：共享缓存、分布式锁、幂等 |
| `XiHan.Framework.Messaging.RabbitMQ` | RabbitMQ Provider |
| `XiHan.Framework.Security.Jwt` | JWT Bearer 认证 Provider |

完整包清单见[框架工程说明](./framework/README.md#正式包)。

## 环境要求

| 依赖 | 版本 |
| --- | --- |
| .NET SDK | 10.0+ |
| C# | Latest |
| 支持平台 | Windows / Linux / macOS |

## 项目生态

- [XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework) - 快速、轻量、高效、用心的 .NET 现代模块化开发框架
- [XiHan.UI](https://github.com/XiHanFun/XiHan.UI) - 快速、轻量、高效、用心的框架无关 Headless UI 组件库
- [XiHan.BasicApp](https://github.com/XiHanFun/XiHan.BasicApp) - 基于 XiHan.Framework 和 XiHan.UI 的超高颜值通用中后台内核

## 贡献

欢迎提交 Issue 和 Pull Request，详见[贡献指南](https://docs.xihanfun.com/cosmos/contributing)。

## 诚挚致谢

排名不分先后。

| 项目                                       | 致谢                                   |
| ------------------------------------------ | -------------------------------------- |
| [Abp](https://github.com/abpframework/abp) | 作为部分架构和逻辑灵感来源（启蒙项目） |
| 其他第三方依赖                             | 作为项目功能丰富与拓展的基石           |


## 支持&赞助

如果此项目对你的开发有助益，也欢迎请作者一杯咖啡。

官方赞助页 https://docs.xihanfun.com/cosmos/sponsor


## 版权&授权

Copyright (c) 2021-Present XiHanFun and contributors.

本项目采用 MIT 授权，详见 [License](./LICENSE)

XiHan.Framework Logo、XiHan.Framework名称归作者所有，第三方依赖和第三方服务分别遵循其各自授权与服务条款。

项目仅供学习参考，作者不承担任何软件的使用风险。
