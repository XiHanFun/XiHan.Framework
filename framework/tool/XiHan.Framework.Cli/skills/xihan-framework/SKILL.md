---
name: xihan-framework
description: 在 .NET 项目里用 XiHan.Framework 写后端：选包、写模块与 [DependsOn]、动态 API 应用服务、配置节、数据访问与缓存等基础设施。凡是代码里出现 XiHan.Framework 命名空间、XiHanModule 或 [DependsOn] 时使用。
---

# 在项目里使用 XiHan.Framework

XiHan.Framework 是面向 .NET 的模块化开发框架：能力按职责拆成可独立引用的 NuGet 包，每个包有一个模块类，模块用 `[DependsOn]` 显式声明依赖，共享统一的生命周期。文档：https://framework.docs.xihanfun.com

## 先查证，再写代码

包名、模块类名、配置键与 API 以项目引用的版本为准，不要凭记忆写，也不要套用别的框架的写法。

- 配了 MCP 时：`xihan_framework_search` 找能力在哪个包，`xihan_framework_info` 查安装命令、模块类与依赖，`xihan_framework_config` 查配置节的键与缺省值，`xihan_framework_doc` 读指南全文。
- 没有 MCP 时用命令行（数据随工具离线）：`dnx XiHan.Framework.Cli@<版本> --yes -- info Caching`、`-- config XiHan:Caching:Redis`、`-- search 分布式锁`、`-- doc guide/dynamic-api`。版本与项目里 `XiHan.Framework.*` 的包引用保持一致。
- 也可以读机读文档：https://framework.docs.xihanfun.com/llms.txt（索引）与每页的 `.md`（如 https://framework.docs.xihanfun.com/guide/caching.md）。

## 应用骨架

- 按需安装包：`dotnet add package XiHan.Framework.Web.Api`（动态 API 与中间件管道，会带上 Core、Web.Core 等基础包）、`XiHan.Framework.Web.Docs`（Scalar 接口文档）；数据访问加 `XiHan.Framework.Data`，缓存加 `XiHan.Framework.Caching`，依此类推。所有 `XiHan.Framework.*` 包用同一个版本。
- 启动模块继承 `XiHanModule`，在 `[DependsOn(typeof(...))]` 里列出要用的模块类，如 `XiHanWebApiModule`、`XiHanWebDocsModule`；模块类名以 `xihan_framework_info` 查到的为准。
- `Program.cs`：`await builder.AddApplicationAsync<MyAppModule>();` → `var app = builder.Build();` → `await app.InitializeApplicationAsync();` → `await app.RunAsync();`。
- 服务注册写在模块的 `ConfigureServices(ServiceConfigurationContext context)` 等生命周期钩子里，不堆在 `Program.cs`。

## 写接口

- 普通业务接口写成应用服务：继承 `ApplicationServiceBase`、标 `[DynamicApi]`，框架按约定生成 REST 路由，不另写 Controller。
- 写操作需要事务时用工作单元（`[UnitOfWork]`），读 `guide/uow` 了解边界与传播；异步方法接受并传递 `CancellationToken`。
- 统一响应、异常与校验走框架的约定（`guide/response`、`guide/validation`），不自行包一层返回结构。

## 配置与基础设施

- 选项类带 `SectionName` 常量并绑定 `XiHan:<模块>:<…>` 配置节。写 appsettings 前用 `xihan_framework_config` 查准键名、类型与缺省值，不要猜。
- 缓存、分布式锁、延迟队列、事件总线等默认实现不依赖外部基础设施；需要 Redis、消息队列时按对应包的配置启用（如 `XiHan:Caching:Redis:IsEnabled`），不要自己再造一层。
- 多租户、认证授权、审计、本地化等横切能力各有包与指南，先 `xihan_framework_search` 再动手。

## 常见错误

- 只装了包却没在启动模块的 `[DependsOn]` 里加模块类，或写了不存在的模块类名。
- 为普通业务接口另写 Controller 或 Minimal API，与动态 API 形成两套入口。
- 配置键写错层级或拼写（如漏掉 `XiHan:` 前缀），选项静默保持缺省值。
- 混用不同版本的 `XiHan.Framework.*` 包。
- 在 `Program.cs` 里手动注册本该由模块完成的服务，或绕过模块生命周期。
