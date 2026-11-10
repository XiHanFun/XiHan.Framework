# ADR-0129 启动 Banner

## 状态

已采纳（不改规范：Banner 是宿主侧的控制台输出，不进跨语言契约；配置键写进内核 README、文档站与 `xihan` 的配置键索引）。

## 日期

2026-11-10

## 背景

4.x 在应用创建时（`XiHanApplicationBase` 的构造函数）向控制台打印一段固定输出：彩虹渐变的字符画 Logo，然后是框架名与版本、描述、寄语和入口程序集的名字与版本。5.x 重写后没有带过来，启动时只剩宿主与特性的日志，看不出跑的是哪个框架版本、哪个应用、什么环境。

4.x 的实现不能照搬：

- 它不能关闭，写死在应用基类里。
- 它总写 stdout。5.x 有命令行宿主，stdout 承载命令的 JSON 输出与 MCP stdio 的消息流，混进 Banner 就坏了。
- 版本经反射读程序集，5.x 的 Ring 0/1 零反射（`SourceHygieneTests.PrimitivesAndKernel_DoNotUseReflection`）。
- 描述是写在代码里的中文，5.x 面向人的文案要走消息键（RFC-0010）。

## 决策

1. **放在内核的 `Kernel.Hosting`**。`Hosting.AspNetCore` 与 `Hosting.CommandLine` 同在 Ring 4，同环之间不共享实现，两个宿主共用的只有内核；Worker 一类只用内核 `AddXiHan` 的通用宿主也要有 Banner。
2. **打印时机**：内部的 `StartupBannerService : IHostedLifecycleService` 在 `StartingAsync` 里打印，早于所有宿主服务的 `StartAsync`。Banner 排在特性启动日志与 Kestrel 监听日志之前，对应 4.x 在应用创建时打印的位置。`AppBuilder.Build()` 登记它，排在 `AppHostedService` 之前。`TestHost` 不经宿主服务启动，不打印。
3. **选项** `StartupBannerOptions`（公开，`Preview`），配置节 `XiHan:Kernel:Banner`：
   - `Enabled`：配置键 `XiHan:Kernel:Banner:Enabled`，缺省 `true`，非法值启动失败。
   - `Output`：写入目标，只能在代码里设，缺省在打印时取 `Console.Out`。
   - `ApplicationVersion`：只能在代码里设。
   - `FromConfiguration(configuration, defaults = null)`：配置里没有的键取 `defaults`，宿主用它给出自己的缺省。
   - 内核以 `TryAddSingleton` 登记，应用先登记的实例不被覆盖；选项在打印时才从容器里取。
4. **版本**：
   - 框架版本取 `FrameworkInfo.Version`，即构建期生成的常量，与包版本、程序集的 `AssemblyInformationalVersion` 同源，不经反射。
   - 应用版本由两个宿主包的 `AddXiHan` 从入口程序集的 `AssemblyInformationalVersion` 读取（去掉 `+` 之后的提交哈希，没有时取程序集版本），在内核登记之前填进选项。Native AOT 下可用。
   - 直接用内核泛型 `AddXiHan` 时只有应用名。
5. **命令行宿主**：`AddXiHanCommandLine()` 去掉已有的选项登记，换成缺省 `Enabled = false`、`Output = Console.Error`，配置显式打开时仍然生效。先于或晚于 `AddXiHan` 调用结果相同。之后再登记的选项优先。
6. **文案**：
   - 标语与应用、环境、运行时、操作系统四行的标签是消息键 `kernel.banner.*`，10 个语言文件。
   - Banner 在特性启动之前打印，本地化特性还没有设置进程默认作用域，所以直接用容器里的 `IMessageResolver` 以空区域（默认区域）解析，没有解析器时退回 `key.L()`。
   - Logo 与寄语是固定的作品，不是界面文案，不进语言文件，也不翻译。
7. **版式与颜色**：
   - 与 4.x 相同：空行、Logo、`XiHan Framework v<版本>`、标语、空行、寄语、空行，之后是应用、环境、运行时与操作系统各一行。
   - Logo 按列取色，色相 0°–300°，24 位色 SGR，每行末尾复位。
   - 只有写到没有重定向的控制台才上色，`NO_COLOR` 非空时不上色。标语与信息行不上色，不再像 4.x 那样写成白色，浅色主题的终端里也看得见。
   - 整段一次写出。不改 `Console.OutputEncoding`。

## 备选

1. **做成可选的 `<名称>Feature`，由组合包装配**：Worker 与只装内核的应用默认就没有 Banner，与「效果和旧版一致」不符；它也不提供能力，没有能力 ID 可写，硬做成特性只为了能被装上。否决。
2. **放在两个宿主包里各写一份**：渲染、颜色与本地化要写两遍；只用内核的通用宿主没有。否决。版本读取是两份相同的十几行，因为内核不能反射，只能放在宿主包。
3. **经 `ILogger` 输出**：日志格式化器会加时间戳、级别与类别前缀，JSON 格式化器还会把整段当成一条消息，彩色字符画无从谈起，与 4.x 的效果不同。否决。
4. **命令行宿主照常打印到 stderr**：一条命令启动一次宿主，每条命令都在终端里刷一屏 Banner；MCP 客户端也会把它当成日志收进来。改为缺省关闭、显式打开时写 stderr。
5. **在 `AddXiHan`（装配期）打印**：只构建不启动的场景（OpenAPI 生成工具、测试里只验装配）也会打印；装配失败时 Banner 先于异常出现，误导排查。否决。
6. **像 4.x 一样设 `Console.OutputEncoding = UTF8`**：会改掉整个进程乃至所在控制台窗口的代码页，是框架不该有的副作用。否决，文档里写出在应用入口自己设置的办法。

## 影响

- 公开 API：
  - 内核新增 `StartupBannerOptions`，以及 `KernelMessages` 的五个键：`BannerTagline`、`BannerApplication`、`BannerEnvironment`、`BannerRuntime`、`BannerOperatingSystem`。
  - 两个宿主包没有新增公开成员。
- 行为：
  - Web 宿主与通用宿主启动时多一段 stdout 输出。以 JSON 行收集 stdout 的部署要设 `XiHan:Kernel:Banner:Enabled=false`，或登记自己的选项改写入目标。
  - 命令行宿主的输出不变。
- `xihan` 的配置键索引（`Knowledge/configuration.json`）新增 `XiHan:Kernel:Banner`。
- 测试：
  - 内核：选项读取、登记顺序、版式逐行、颜色的开关与去掉序列后的等价、默认区域、关闭不输出、写入目标。
  - Web 宿主：入口程序集版本、zh-CN 文案、先于应用的宿主服务打印、配置关闭。
  - 命令行宿主：两种调用顺序下都缺省关闭并指向 stderr；执行 `invoke` 时 stdout 整段是 JSON，打开后 Banner 只出现在 stderr。
