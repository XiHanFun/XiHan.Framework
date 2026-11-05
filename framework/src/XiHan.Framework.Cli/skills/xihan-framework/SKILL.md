---
name: xihan-framework
description: 在 .NET 项目里用 XiHan Framework 写后端：选包与特性、按能力 ID 装配、写操作契约、配置节与资源绑定、错误与本地化、管道中间件。凡是代码里出现 XiHan.Framework 命名空间、IFeature、AddXiHan、[Operation] 或 xihan.app.json 时使用。
---

# 在项目里使用 XiHan Framework

XiHan Framework 按能力装配：每个包带一份特性清单（Manifest），声明提供与依赖的能力 ID、资源绑定、配置节、管道中间件与健康项；应用在 `AddXiHan` 里显式加入特性，内核按能力 ID 求解依赖。

## 先查证，再写代码

包名、特性类型、能力 ID、配置键与缺省值以项目引用的版本为准，不要凭记忆写。

- 配了 `xihan-framework` MCP 服务时：`xihan_framework_info` 查包与特性（安装命令、提供与依赖的能力、绑定、配置节、管道中间件），也可传能力 ID 查它的提供者；`xihan_framework_config` 查配置键的类型、缺省值、取值范围与 appsettings 片段，只写查得到的键；`xihan_framework_search` 检索指南、包参考页、RFC 与 README，传 `page` 读整页。
- 数据随工具版本走：服务端提示版本与项目不一致时，按提示改用 `dnx XiHan.Framework.Cli@<项目版本> --yes -- mcp`。
- 类型与成员以包的 `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` 为准。

## 写法

- 装配：在 `Program.cs` 用 `builder.AddXiHan(x => x.UseStandardProfile().UseFeature<T>())` 显式加入特性；应用自己的特性实现 `IFeature`，带 `Manifest`（`Requires` 用 `<领域>Capabilities` 常量），服务在 `Configure` 里显式注册。
- 接口：一个操作一个类，`[Operation("<资源复数>.<动词>")]` + `IOperation<TRequest, TResponse>`，请求与响应登记到 `JsonSerializerContext`，特性里调用 `context.AddGeneratedOperations()`；`Expose` 写明暴露的协议。
- 失败：返回 `Result` / `Error`，不抛异常；错误码 `<域>_<KIND>_<NNN>`，`Error` 的第二个参数是消息键常量，文案写在 `Localization/<区域>.json`（10 种区域各一个文件）。
- 横切逻辑：写 `IPipelineMiddleware`，按 `PipelineStage` 登记，跨包次序用 `Before` / `After` 声明。
- 外部资源：连接信息走资源绑定（`XiHan:Bindings:<名字>` 或 Service Binding 目录），绑定名与 Provider 清单里声明的一致；部署产物用 `xihan pack` 从 `xihan.app.json` 生成。

## 核对

改完运行应用的 `graph`、`pipeline`、`operations` 命令（或 `xihan graph --project <项目>`），核对能力图谱、管道顺序与操作登记；新消息键在 10 个语言文件里各有一行，占位符与 `en-US.json` 一致。
