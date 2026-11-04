---
name: xihan-framework-development
description: 设计、实现、重构或审查 XiHan.Framework 时使用：原语层、内核、子系统、Provider、宿主、协议、插件、规范、ADR、公共 API 与门禁。缓存、队列等资源实现以及测试发布分别使用对应专项技能。
---

# XiHan.Framework 开发

命名由术语表与文法推出时读 `references/naming.md`；构建、测试、门禁与阶段退出条件读 `references/gates.md`。

## 先查事实

```bash
git status --short --branch
git log -5 --oneline
ls framework/src framework/spec/rfcs design/adr
```

在 worktree 中先按 `AGENTS.md`「分支与 worktree」核对分支分组和基线。

按顺序读：`design/框架总体方案.md` → `design/architecture/01–05` → 相关 RFC 与 ADR → 目标包的 README 与 PublicAPI 文件。只读与任务相关的部分。

## 架构边界

- 环层只能向下依赖：Primitives(0) → Kernel(1) → Domain 与 `.Abstractions`(2) → 实现(3) → Hosting(4) → Protocols 与 Plugins(5) → Profiles(6)；协议适配器在宿主之上翻译消息，可引用宿主包；工具链（Analyzers、Generators、Conformance、Testing、Cli）可引用任何环。同环之间只经契约包（`.Abstractions`，以及纯契约的 `Domain`）。
- Ring 0/1 与抽象包零第三方依赖，只允许 `Microsoft.Extensions.*` 抽象包（内核另加 BCL 扩展包 `System.Threading.RateLimiting`，见 `DependencyTests` 的允许名单）；原语层无任何引用。
- Ring 0/1 零反射；反射只允许在 `Plugins.InProcess` 并标注 `RequiresDynamicCode`。
- 原语层只增不改；破坏即新包 `Primitives.V2` 并存。
- 每个抽象配一个 `role: default` 的默认实现，包名按机制命名（`InMemory`、`FileSystem`），零外部基础设施、容量有界、通过一致性套件。
- 依赖按能力 ID 声明在清单里，不按包名；管道按固定阶段插入中间件。
- 规范先于实现：公开契约先进 `framework/spec/rfcs`，再写 ADR（`design/adr`），再写代码与一致性向量。
- 借鉴只能落成规则与约束，不落成类名；标识符里不出现哲学词汇。

## 代码规范

细则在 `design/architecture/03-代码规范.md`；`.editorconfig` 的命名与样式规则在构建期执行（`EnforceCodeStyleInBuild`，IDE1006 / IDE0005 警告即错误），不降 severity、不加 `NoWarn`。

- 一个类型一个文件：类、记录、接口、枚举、委托都算；泛型类型文件名加 `OfT`（`MaybeOfT.cs`、`EntityQueryOfT.cs`），同名非泛型工厂类留在无后缀文件；只有嵌套类型留在外层类型的文件里。
- XML 文档：所有类型与成员，含 private / internal / protected、字段、常量、`[GeneratedRegex]` 方法、嵌套类型、测试类与测试辅助类型，都写中文 `/// <summary>`（三行、只写一句，细节进 `<remarks>`）；`[Fact]` / `[Theory]` 方法除外；实现接口与重写用 `<inheritdoc />`；局部函数用 `//`。
- 关键逻辑注释：锁与临界区范围、状态机转移、回退链、容量与并发判定、安全判定顺序（认证 → 租户 → 授权）、延迟开启的事务、原子比较删等处，用中文 `//` 说明为什么，不复述代码，句末不加「。」。
- 文案不写在代码里（RFC-0010）：每个包一个 `<领域>Messages` 常量类只定义消息键（`kernel.manifest.empty`、`security.token.subject_missing`；文法 `^[a-z][a-z0-9]*(\.[a-z][a-z0-9_]*)+$`，首段是术语表领域词小写），每种语言一个文件 `Localization/<区域>.json`（键 → 模板，含 `en-US.json`），`<EmbeddedResource Include="Localization/*.json" LogicalName="Localization/%(Filename)%(Extension)" />` 嵌入，10 种区域键集一致、占位符与 `en-US` 一致。模板 `{name}` 按名字、`{0}` 按位置，可带 `:format`，按命中区域的文化格式化。
- 取文案：任何地方 `key.L()` / `key.L(("id", id))` / `key.L(count, amount)`（原语层 `MessageKeyExtensions`，按 `MessageScope.Current`：内核在每次调用入口按 Baggage `xihan.locale` 安装调用方区域，`Localization.InMemory` 启动时设进程默认区域；没有作用域或找不到键返回 `MessageTemplate.Describe` 的"键 (参数)"）。显式区域用 `IMessageLocalizer.Resolve/Localize`，上下文用 `context.Localize(key, args)`。
- 错误：`Error.NotFound(code, OrdersMessages.NotFound, MessageArguments.Of(("id", id)))` 只带键与参数（`MessageKey` + `Details`），`Error.Message` 按进程默认区域渲染供日志用，宿主在 Egress 用 `IMessageLocalizer.Resolve(error, locale)` 翻译成 Problem Details 的 `detail` 并以命中区域设 `Content-Language`；`Error.Literal(code, text)` 只用于线上还原与异常转换（`FrameworkException.ToError`、`IdempotencyRecord`），`WithCode` 换码保留译文。正式包里 `Error.*` 第二个参数必须是常量，架构测试 `ErrorCallSites_UseMessageKeyConstants` 强制。
- 目录注册：子系统 Provider 在 `Configure` 第一行 `context.Services.AddMessageCatalog(<领域>Messages.Catalog)`；原语层与内核的目录由 `InMemoryLocalizationFeature` 注册（Minimal 与 Standard 都装配它）；`Plugins.InProcess` 与应用自己的目录由应用 `AddMessageCatalog(MessageCatalog.FromEmbeddedResources("orders", typeof(OrdersMessages)))`。新增键 = 常量 + 10 个语言文件，缺一架构测试不过。
- 其余运行时英文：`FrameworkException` / `ArgumentException` 消息（配置与编程错误，不本地化）、`LoggerMessage` 模板、分析器诊断、一致性套件与测试的断言与夹具文本、`RequiresUnreferencedCode` / `RequiresDynamicCode` 消息。句子以句号结尾，标识符与 ID 用单引号；分析器标题与单句消息按 RS1031 / RS1032 不加句号。XML 文档、`//` 注释、README、RFC、提交说明保持中文；`.csproj` 的 `Description` 是中文包元数据。
- 命名：私有实例与私有静态字段 `_camelCase`，私有常量 PascalCase，其余见 `references/naming.md`。

## 完成条件

1. 包名、命名空间、能力 ID、配置节、错误码域由术语表推出，`framework/test/XiHan.Framework.Architecture.Tests` 通过。
2. 公开类型有 `[ApiLevel]` 与 XML 文档，`PublicAPI.Unshipped.txt` 已更新。
3. `dotnet build -c Release` 零警告；`dotnet test --solution` 通过；`dotnet pack` 通过包验证。
4. 新公开契约有 RFC、ADR 与一致性向量；新错误码与术语已登记；新消息键有常量与 10 个语言文件。
5. 一个类型一个文件；全部成员含私有有中文 `<summary>`；文案只在语言文件里、代码只引用键；关键逻辑有 `//` 注释。
6. 一项能力一个提交：`<type>(<scope>): <中文说明>`，scope 用术语表领域词小写或 `spec`、`docs`、`build`。
