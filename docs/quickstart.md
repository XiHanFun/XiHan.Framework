# 快速上手

本篇带你从零建一个基于 XiHan.Framework 的 Web API。**目标：5 分钟内跑起一个能被浏览器访问的接口。** 全程只用到真实存在的 API，照抄即可。

## 环境准备

| 依赖 | 版本 | 说明 |
| --- | --- | --- |
| .NET SDK | **10.0+** | 必需 |
| IDE | Visual Studio 2022 / Rider / VS Code | 任选 |
| 数据库 | PostgreSQL / MySQL / SQLite 等 | 仅在用到 `Data` 模块时需要 |
| Redis | 6.0+ | 仅在用到分布式缓存时需要 |

检查 SDK 版本：

```bash
dotnet --version   # 应输出 10.x
```

## 第一步：创建项目

```bash
dotnet new web -n MyApp
cd MyApp
```

## 第二步：安装框架模块

框架是**按模块安装**的，用什么装什么。建一个能提供 Web API 的应用，最少需要这两个：

```bash
# 动态 API + 完整中间件管道（依赖会自动带上 Core、Web.Core 等）
dotnet add package XiHan.Framework.Web.Api

# API 文档（Scalar + Swagger UI），开发期强烈推荐
dotnet add package XiHan.Framework.Web.Docs
```

> `XiHan.Framework.Web.Api` 的 NuGet 包已经把 `Core`、`Web.Core`、`Application`、`MultiTenancy`、`Serialization`、`Auditing` 等基础库/模块作为传递依赖带上了，你不用逐个单独安装；`XiHanWebApiModule` 自身声明的 `[DependsOn]` 是 `Web.Core`、`MultiTenancy`、`Serialization`、`Auditing`。

## 第三步：定义启动模块

框架里，**一个"模块"就是一个继承 `XiHanModule` 的类**，用 `[DependsOn]` 声明它要用哪些能力。新建 `MyAppModule.cs`：

```csharp
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api;
using XiHan.Framework.Web.Docs;

namespace MyApp;

[DependsOn(
    typeof(XiHanWebApiModule),   // 动态 API + 中间件管道
    typeof(XiHanWebDocsModule)   // Scalar / Swagger 文档
)]
public class MyAppModule : XiHanModule
{
    // 这里可以重写 ConfigureServices / OnApplicationInitialization 等钩子，
    // 现在先留空，框架会用默认管道帮你把一切装配好。
}
```

## 第四步：改写 Program.cs

把默认的 `Program.cs` 换成三行式启动：

```csharp
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;
using MyApp;

var builder = WebApplication.CreateBuilder(args);

// 加载以 MyAppModule 为根的整棵模块依赖树（自动拓扑排序）
await builder.AddApplicationAsync<MyAppModule>();

var app = builder.Build();

// 触发所有模块的初始化钩子，装配中间件管道
await app.InitializeApplicationAsync();

await app.RunAsync();
```

> `AddApplicationAsync<T>` 和 `InitializeApplicationAsync` 是框架的两个入口方法：前者在**服务注册阶段**装配模块，后者在**应用初始化阶段**接入中间件。详见 [核心概念 · 生命周期](./guide/lifecycle)。

## 第五步：写第一个接口

框架用**动态 API** 暴露接口：你只需要写一个**应用服务**类，框架会自动把它变成 REST 接口，**不需要写 Controller**。新建 `HelloAppService.cs`：

```csharp
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Services;

namespace MyApp;

/// <summary>
/// 打招呼服务 —— 会被自动暴露为 REST 接口
/// </summary>
[DynamicApi]
public class HelloAppService : ApplicationServiceBase
{
    public string GetGreeting(string name)
    {
        return $"你好，{name}！欢迎使用 XiHan.Framework。";
    }
}
```

这里发生了什么：

- 继承 `ApplicationServiceBase` —— 它同时标记了 `IApplicationService` 和 `ITransientDependency`，所以**会被自动注册进 DI 容器**，无需手写 `services.AddTransient`。
- 打上 `[DynamicApi]` —— 框架的动态 API 约定会扫描到它，为 `GetGreeting` 生成对应的 HTTP 路由。

> 想了解路由是怎么从方法名推导出来的、如何自定义分组/版本/路由，见 [核心概念 · 动态 API](./guide/dynamic-api)。

## 第六步：运行

```bash
dotnet run
```

启动后打开浏览器：

- **API 文档**：`https://localhost:<端口>/scalar`（由 `Web.Docs` 提供）
- 在文档里找到 `Hello / GetGreeting`，填入 `name` 参数即可在线调用

恭喜，你的第一个 XiHan.Framework 接口跑起来了 🎉

## 接下来加数据访问

真实项目免不了要读写数据库。安装 `Data` 模块（SqlSugar），在启动模块上加一行依赖：

```bash
dotnet add package XiHan.Framework.Data
```

```csharp
[DependsOn(
    typeof(XiHanWebApiModule),
    typeof(XiHanWebDocsModule),
    typeof(XiHanDataModule)      // ← 新增：SqlSugar 数据访问
)]
public class MyAppModule : XiHanModule { }
```

在 `appsettings.json` 配置连接串（以 PostgreSQL 为例）：

```json
{
  "XiHan": {
    "Data": {
      "SqlSugarCore": {
        "ConnectionConfigs": [
          {
            "DbType": "PostgreSQL",
            "ConnectionString": "Host=localhost;Port=5432;Database=myapp;Username=postgres;Password=your_password;"
          }
        ]
      }
    }
  }
}
```

之后就能在应用服务里注入仓储 `IRepositoryBase<TEntity, TKey>` 读写数据。数据访问的完整用法见 [Data 模块文档](./packages/data)。

## 你可能想加的其它能力

| 想要 | 安装 | 文档 |
| --- | --- | --- |
| 缓存（内存 + Redis） | `XiHan.Framework.Caching` | [Caching](./packages/caching) |
| JWT / OAuth2 认证 | `XiHan.Framework.Authentication` | [Authentication](./packages/authentication) |
| RBAC 授权 | `XiHan.Framework.Authorization` | [Authorization](./packages/authorization) |
| 事件总线（内置内存实现，另有 RabbitMQ / Kafka / Redis Broker 可选） | `XiHan.Framework.EventBus` | [EventBus](./packages/eventbus) |
| 定时任务 + 后台作业（Cron 调度 / 一次性任务队列） | `XiHan.Framework.Tasks` | [Tasks](./packages/tasks) |
| 健康检查 / 指标 / 性能诊断 | `XiHan.Framework.Observability` | [Observability](./packages/observability) |
| SignalR 实时通信 | `XiHan.Framework.Web.RealTime` | [Web.RealTime](./packages/web-realtime) |
| AI（Microsoft.Extensions.AI + Agent Framework + MCP） | `XiHan.Framework.AI` | [AI](./packages/ai) |

完整模块清单见 [模块总览](./packages/)。

## 想要一个完整的实战项目？

[**XiHan.BasicApp**](https://basicapp.docs.xihanfun.com/) 是官方基于本框架构建的企业级中后台系统，包含多租户、RBAC + ABAC 权限、代码生成、实时通信等完整能力，是学习框架用法最好的参考。

## 下一步

- [核心概念 · 模块系统](./guide/modularity)：理解 `[DependsOn]` 与自动装配
- [核心概念 · 生命周期](./guide/lifecycle)：理解 7 个生命周期钩子
- [核心概念 · 依赖注入](./guide/dependency-injection)：理解约定式服务注册
- [核心概念 · 动态 API](./guide/dynamic-api)：理解应用服务如何变成接口
