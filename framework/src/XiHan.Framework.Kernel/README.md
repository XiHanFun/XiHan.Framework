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
* `BindingResolver`、`BindingValue`：资源绑定的解析与结果（主值与其余属性；打印时主值遮蔽、属性只列名字）；`FeatureContext.Bindings` 是装配时解析好的一份，`FeatureContext.ServiceBindingRoot` 是应用指定的绑定根目录，装配之后还要按同样的来源解析时用它
* `PipelineBuilder`、`IPipelineMiddleware`、`PipelineStage`：执行管道
* `OperationAttribute`、`IOperation<TRequest, TResponse>`、`IOperationInvoker`：操作契约与调用
* `FrameworkDiagnostics`：内核的 ActivitySource 与 Meter（名字都是 `XiHan.Framework`）。Span `xihan.invoke` 与指标 `xihan.invocations`、`xihan.invocation.duration` 带 `xihan.outcome`；操作调用另带 `xihan.operation`（操作 ID），不是操作调用时没有这个标签
* `UnreadableDeadlineLog`：调用方给的截止时间读不出时的限频告警，内核登记为单例。请求头（`xihan-deadline`、`grpc-timeout`）与插件回调各算一类，每类 1 分钟最多一条 Warning，不带原值，下一条带上被压掉的条数
* `SecurityContext`、`Principal`、`PermissionRegistry`：安全上下文与权限登记
* `ResultJson`：Result 与 Error 的线上 JSON 编码，与各语言 SDK 逐字一致
* `JsonText`、`JsonTextProblem`、`JsonTextProblemKind`：读取 JSON 文本的共同检查，操作载荷、清单与消息信封都先过它。`JsonText.FindProblem(utf8)` 核对语法、嵌套不超过 `MaxDepth`（64）层、合法的 Unicode 与任何一层都没有重复的成员，通过时返回无值的 `Maybe`，否则是第一个问题：`Kind` 是 `Syntax`、`NestingTooDeep`、`InvalidUtf8`、`LoneSurrogate` 或 `DuplicateMember`，`Path` 在重复成员时是第二次出现的路径（如 `$.data.lines[0].sku`），其余都是语法错误、一律是 `$`。语法错误不论出现在哪里都优先于重复成员，几种语法错误之间报文本里第一处

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/kernel)

## 相关包

* 原语层：[XiHan.Framework.Primitives](https://www.nuget.org/packages/XiHan.Framework.Primitives)
* 操作登记与 Schema 的源生成器：[XiHan.Framework.Kernel.Generators](https://www.nuget.org/packages/XiHan.Framework.Kernel.Generators)
* 命名与稳定性规则分析器：[XiHan.Framework.Kernel.Analyzers](https://www.nuget.org/packages/XiHan.Framework.Kernel.Analyzers)

## 反馈与贡献

`XiHan.Framework.Kernel` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
