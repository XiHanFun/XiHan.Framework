# XiHan.Framework.Kernel

## 关于

`XiHan.Framework.Kernel` 是框架的微内核：应用构建、能力图谱求解、执行管道与内置中间件、操作契约与调用器、安全原语、健康与插件抽象。所有应用都经它装配；它不处理 HTTP，不含具体的子系统实现。

## 主要特性

* `AddXiHan` 收集特性、求解能力图谱，并按拓扑顺序装配
* 带阶段与次序约束的执行管道，内置观测、截止时间、弹性（熔断、限流、并发）与契约校验中间件
* 操作契约 `[Operation]` 与 `IOperationInvoker`，协议适配器按登记表发布
* `Principal`、`TenantContext`、权限登记与出站地址策略等安全原语
* 健康快照与 `IHealthContributor`；失败结果只带消息键，按调用方区域翻译

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Kernel --prerelease
```

### 使用

通常经 `Hosting.AspNetCore` 或 `Hosting.CommandLine` 间接使用。在通用宿主上直接使用内核：

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<WorkerFeature>());   // WorkerFeature 是应用自己的特性
await builder.Build().RunAsync();
```

常用配置节：`XiHan:Kernel`（启动与停止超时、失败策略）、`XiHan:Kernel:Resilience`（按操作的限制）、`XiHan:Kernel:Banner`（启动 Banner）、`XiHan:Providers`（指定能力提供者）。

宿主启动时先打印启动 Banner：字符画 Logo、框架版本、应用、环境、运行时与操作系统，写到标准输出，没有重定向的控制台上 Logo 带颜色（设置 `NO_COLOR` 可去掉）。`XiHan:Kernel:Banner:Enabled=false` 关闭；要改写入目标，在 `AddXiHan` 之前登记自己的 `StartupBannerOptions`。命令行宿主缺省不打印。

## 主要类型

此库提供的主要类型：

* `AppBuilder`、`App`：装配入口与运行中的应用
* `KernelOptions`、`StartupBannerOptions`：内核选项与启动 Banner 的选项
* `IFeature`、`IHostedFeature`、`FeatureContext`：特性与它的装配入口
* `PipelineBuilder`、`IPipelineMiddleware`、`PipelineStage`：执行管道
* `OperationAttribute`、`IOperation<TRequest, TResponse>`、`IOperationInvoker`：操作契约与调用
* `SecurityContext`、`Principal`、`PermissionRegistry`：安全上下文与权限登记

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/kernel)

## 相关包

* 原语层：[XiHan.Framework.Primitives](https://www.nuget.org/packages/XiHan.Framework.Primitives)
* 操作登记与 Schema 的源生成器：[XiHan.Framework.Kernel.Generators](https://www.nuget.org/packages/XiHan.Framework.Kernel.Generators)
* 命名与稳定性规则分析器：[XiHan.Framework.Kernel.Analyzers](https://www.nuget.org/packages/XiHan.Framework.Kernel.Analyzers)

## 反馈与贡献

`XiHan.Framework.Kernel` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
