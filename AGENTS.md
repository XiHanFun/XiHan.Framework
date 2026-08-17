# AGENTS.md

面向编码 Agent 的操作手册。本仓库是**纯类库**，没有可运行的宿主应用；改动必须可被下游应用（如 XiHan.BasicApp）以 NuGet 包消费。

当前版本 **3.13.1**，目标框架 **net10.0**，SDK 以根目录 `global.json` 为准（`10.0.302`，`rollForward: latestPatch` —— 锁在 10.0.3xx 功能带，10.0.4xx 下解决方案还原会全线失败）。解决方案是 **`framework/XiHan.Framework.slnx`**（slnx，不是 `.sln`）。`framework/src/` 下 66 个项目各自发布为同名 NuGet 包，版本号统一，不单独发某个包。

查代码优先用 CodeGraph（根目录 `.codegraph/`）：MCP `codegraph_explore` 或 `codegraph explore "<符号或问题>"`。跨模块调用链与影响面比 grep + 逐个 Read 准。

更完整的人类文档：根 `README.md`、模块内 `README.md`、文档站源码 `docs/`（部署到 https://framework.docs.xihanfun.com）。

---

## 目录地图

```text
.
├── framework/
│   ├── XiHan.Framework.slnx          # 唯一解决方案（按 7 层文件夹组织）
│   ├── src/                          # 66 个可打包库项目
│   ├── test/                         # xunit.v3 测试（CI 强门禁）
│   ├── tool/                         # 仓库内工具（不发布）
│   ├── props/                        # 共享 MSBuild：netcore / common / version / nuget / test
│   ├── nuget/                        # 打进每个包的 LICENSE / logo / readme
│   ├── nupkgs/                       # 本机构建产物（GeneratePackageOnBuild=true）
│   ├── scripts/                      # 交互式本机脚本，禁止在自动化里调用
│   └── .editorconfig                 # C# 风格（以这里为准，不要靠感觉）
├── docs/                             # VitePress 文档站
│   ├── guide/                        # 概念指南
│   ├── packages/                     # 每包一页
│   └── .vitepress/config.ts          # 侧边栏
├── .github/workflows/                # ci.yml 只构建+测试；deploy-docs.yml 发文档站
└── global.json                       # SDK 锁定
```

`slnx` 分层文件夹（新增项目必须放对层，禁止反向引用）：

| 文件夹 | 层 | 代表项目 |
| --- | --- | --- |
| `/1.src/1.Common/` | Utils / Analyzers | `Utils`（零依赖工具）、`Analyzers`（Roslyn） |
| `/1.src/2.Metadata/` | 元数据 | `Metadata` |
| `/1.src/3.Core/` | 核心 | `Core`（模块系统 / DI / 生命周期） |
| `/1.src/4.Domain/` | 领域 | `Domain`、`Domain.Shared` |
| `/1.src/5.Application/` | 应用 | `Application`、`Application.Contracts`、`MultiTenancy*`、`Validation*`、`Settings` |
| `/1.src/6.Infrastructure/` | 基础设施 | `Data`、`Caching`、`EventBus*`、`AI*`、`Bot*`、`Uow`、`Workflow*` 等 |
| `/1.src/7.Web/` | Web | `Web.Core`、`Web.Api`、`Web.Mcp`、`Web.Docs`、`Web.Gateway`、`Web.Grpc`、`Web.RealTime` |

`Abstractions` 后缀包只放接口契约，实现包依赖它，禁止反过来。可插拔实现拆成兄弟子包（`EventBus.Kafka`、`Bot.Telegram`），主包不引它们的依赖。

---

## 常用命令

一律在**仓库根目录**执行。

```bash
dotnet restore framework/XiHan.Framework.slnx

# 本机构建默认会产包（nuget.props 的 GeneratePackageOnBuild=true，输出到 framework/nupkgs）
# 只编译验证时关掉，明显更快 —— CI 就是这么做的
dotnet build framework/XiHan.Framework.slnx -c Release -p:GeneratePackageOnBuild=false

dotnet test framework/XiHan.Framework.slnx -c Release

# 单个测试项目 / 类 / 方法
dotnet test framework/test/XiHan.Framework.Utils.Tests/XiHan.Framework.Utils.Tests.csproj
dotnet test framework/test/XiHan.Framework.Utils.Tests/XiHan.Framework.Utils.Tests.csproj --filter "FullyQualifiedName~CacheHelperAdvancedTests"
```

文档站：

```bash
cd docs && pnpm install && pnpm dev
```

`framework/scripts/` 下的 PowerShell（版本升级、推包、清理 bin/obj）全部是**交互式**（`Read-Host`）且用 **CWD 相对路径**。必须 `cd` 到脚本自身目录再执行，**不要在自动化流程里调用**。

---

## 架构（读懂这个才读得懂其它一切）

### 分层

严格 7 层，禁止反向依赖与循环依赖：

```text
Utils（零依赖） → Metadata → Core → Domain(.Shared) → Application(.Contracts) → 基础设施 → Web
```

### 模块系统

每个包有且只有一个 `XiHanModule` 子类，命名 `XiHan{ModuleName}Module`，放在项目根目录。

- `[DependsOn(typeof(XxxModule))]` 声明依赖 → `XiHanModuleHelper.FindAllModuleTypes` 从启动模块递归收集全图（重复跳过），再拓扑排序决定装配顺序
- 7 个生命周期钩子分两阶段：`PreConfigureServices` / `ConfigureServices` / `PostConfigureServices`，然后 `OnPreApplicationInitialization` / `OnApplicationInitialization` / `OnPostApplicationInitialization`，退出时 `OnApplicationShutdown`
- 宿主入口：`builder.AddApplicationAsync<TStartupModule>()` + `app.InitializeApplicationAsync()`（见 `framework/src/XiHan.Framework.Web.Core/Extensions/DependencyInjection/`）

**模块类只做装配，不写逻辑。** `ConfigureServices` 里调一个 `services.AddXiHan{Feature}(configuration)` 扩展方法，实现放在 `Extensions/DependencyInjection/XiHan{Feature}ServiceCollectionExtensions.cs`。改功能先找扩展方法，别在模块类里堆代码。

### 配置约定

Options 类型命名 `XiHan{Feature}Options`，自带 `const string SectionName`，配置节一律 `XiHan:` 前缀（如 `XiHan:AI:Mcp`、`XiHan:Web:Api:Auth`）。

涉及对外暴露 / 凭据的能力用 **fail-closed 门控**：没启用或没配密钥时**既不注册服务也不映射端点**，而不是注册了再拦。范式见 `XiHanWebMcpServiceCollectionExtensions` —— 服务注册与端点映射共用同一个 `IsExposable` 判定。

### 动态 API

应用服务被自动投影成 controller，不手写 controller。约定在 `DefaultDynamicApiConvention`，全局值在 `AddXiHanWebApiMvc()` 里设定。

- **路由剥离动词**（`CreateXxxAsync` → `POST /Xxx`），由 `Conventions.PreserveRoutePredicate = false` 决定。全部前端按此对接，翻转它会导致所有路由变化。
- 只有显式 `[FromRoute]` 的参数才进路由段，不按参数名后缀推断（给既有方法加参数不能变成静默的线上破坏）。
- 启动期会物化一次 `ActionDescriptor`，让路由 / 控制器名冲突在启动时暴露，而不是首个请求 500。

MVC 过滤器顺序有语义，调整前先读 `XiHanWebApiServiceCollectionExtensions` 里对应注释：缓存过滤器排在工作单元**之外**（命中缓存不开事务），工作单元排**最后**（最贴近动作，动作抛的异常先落到它手里）。

Web 中间件顺序同样有语义（见 `XiHanWebApiModule.OnApplicationInitialization`）：`UseForwardedHeaders` 必须最先；限流 / 熔断在路由后、鉴权前；租户解析在认证后、授权前；会话闸门夹在租户解析与授权之间。

### AI 技能与 MCP 双通道

`IAiSkill` 是应用层供给的能力单元，框架同时投影成：① `AIFunction`（M.E.AI 自动函数调用）；② MCP tool。

传输与投影解耦：`SkillMcpToolsConfigurator` 通过 `IConfigureOptions<McpServerOptions>` 把技能并入工具集，**与传输无关**，只在宿主调了官方 `AddMcpServer()` 时才触发。`XiHan.Framework.Web.Mcp` 只负责 HTTP 传输 + `/mcp` 端点 + key 鉴权。加新传输（如 stdio）应**新建包**复用 `AddXiHanMcpServerTools()`，不要改投影层。

---

## 编码约定

- **文件头是编译期规则**：每个 `.cs` 必须以这两行开头，由 `XHFH001`（`XiHanFileHeaderAnalyzer`，severity=warning）检查并提供 Code Fix：

  ```csharp
  // Copyright (c) 2021-Present XiHanFun and contributors.
  // Licensed under the MIT License. See LICENSE in the project root for license information.
  ```

- 注释与 XML 文档注释一律**简体中文**；`GenerateDocumentationFile` 全局开启，public 成员缺 `<summary>` 会告警
- `Nullable` 与 `ImplicitUsings` 全局 enable（`props/common.props`）；C# `LangVersion=latest`（`props/netcore.props`）
- file-scoped namespace、primary constructor、表达式体**属性 / 访问器**；表达式体**方法 / 构造函数**明确关闭（`framework/.editorconfig`）
- 缩进 4 空格，换行 **LF**，UTF-8
- 改功能时保持周围文件的风格：不要顺手把大段代码改成另一种写法

### 项目文件约定

- 非 Web：`Sdk="Microsoft.NET.Sdk"`
- Web 类模块：`Sdk="Microsoft.NET.Sdk.Web"` 且 `<OutputType>Library</OutputType>`，通常再加 `<NoDefaultLaunchSettingsFile>true</NoDefaultLaunchSettingsFile>`
- csproj 按序 Import：`netcore` → `common` → `version` → `nuget`
- 测试项目 Import `props/test.props`（已 `<Using Include="Xunit" />`，`IsPackable=false`）

---

## 新增一个模块要动的地方

1. `framework/src/XiHan.Framework.X/` + csproj（按上节 Import）
2. `XiHanXModule.cs` + `Extensions/DependencyInjection/` 下的装配扩展
3. 模块 `README.md`，固定七段：**概述 / 核心能力 / 依赖关系 / 配置与约定 / 使用方式 / 扩展点 / 目录结构**
4. 注册进 `framework/XiHan.Framework.slnx` 对应分层文件夹
5. `docs/packages/x.md`，必要时加 `docs/guide/x.md`，并更新 `docs/.vitepress/config.ts` 侧边栏
6. 根 `README.md` 的模块清单与架构图

公共 API / 配置节 / 路由一旦合入即被下游消费。破坏性变更必须在 PR 模板的「破坏性变更」栏写明，并同步文档。

---

## 测试

- 栈：xunit.v3 4.0 + Microsoft.Testing.Platform（MTP）。`global.json` 的 `test.runner` 指定 MTP —— 没有它，`dotnet test` 会走 VSTest 目标并被 .NET 10 SDK 硬性拒绝。因此不要给 `dotnet test` 加 `--logger` / `--results-directory` 这类 VSTest 专属参数，MTP 下会以退出码 5「零项测试被执行」失败
- CI（`.github/workflows/ci.yml`）在 **ubuntu** 上跑，触发 `main` / `dev` 的 push 与 PR，**不起任何外部服务**
- 依赖 Redis / ES 等的测试必须自跳过：`Assert.SkipWhen(...)`，地址从环境变量取。范式见 `RedisPendingBehaviorTests`（读 `XIHAN_TEST_REDIS`，缺省 `localhost:6379,user=redis,password=redis`，连不上整类跳过）
- 所有测试项目都是强门禁，失败即中断
- 改公共约定（动态 API 路由、过滤器顺序、模块生命周期、fail-closed 门控）时，优先补/改对应测试项目，不要只手测

测试项目位置：`framework/test/XiHan.Framework.{模块}.Tests/`。集成探测在 `Integration.Tests` 与 `Web.Tests`（后者是最小 Web 宿主，不是产品应用）。

---

## 版本与提交

- 唯一版本源：`framework/props/version.props`（与 `docs/package.json` 对齐）
- 发布全在本机：`scripts/nuget/VersionUpgrade.ps1`（改版本并构建）→ `PushNugetPackages.ps1`（推 NuGet）
- CI **不发布**任何包
- 提交信息：中文 Conventional Commits，作用域用模块小写名
  - `fix(web-api): 动作抛异常时回滚事务`
  - `feat(caching): ...`
  - 提版本：`build: v3.13.0`
  - 随后单独一条：`docs: 补写 vX.Y.Z 更新日志，文档站版本抬到 X.Y.Z`（同时改 `docs/package.json` 与 `docs/changelog.md`）

---

## 改代码时先看哪里

| 要改的东西 | 先打开 |
| --- | --- |
| 模块装配 / 生命周期 | `XiHan{Name}Module.cs`，立刻转到 `Extensions/DependencyInjection/` |
| 选项与配置节 | `Options/XiHan{Feature}Options.cs` 的 `SectionName` |
| 动态 API 路由 / 动词 | `Web.Api/DynamicApi/Conventions/DefaultDynamicApiConvention.cs` |
| MVC 过滤器顺序 | `Web.Api/Extensions/DependencyInjection/XiHanWebApiServiceCollectionExtensions.cs` |
| HTTP 中间件顺序 | `Web.Api/XiHanWebApiModule.cs` 的 `OnApplicationInitialization` |
| MCP 投影（与传输无关） | `AI/Mcp/SkillMcpToolsConfigurator.cs`、`AddXiHanMcpServerTools` |
| MCP HTTP 传输 / 鉴权 | `Web.Mcp/Extensions/DependencyInjection/` |
| 工作单元 / 事务 | `Uow` + Web.Api 过滤器注册顺序 |
| 文件头 / 分析器 | `Analyzers/FileHeaders/` |

---

## 不要做的事

- 不要在本仓库里「跑起来看一下」——没有宿主。验证靠 `dotnet build` / `dotnet test`，或下游应用
- 不要手写 `.sln`，不要改 slnx 的分层文件夹语义去迁就引用
- 不要在 `XiHanModule` 子类里堆业务或大段注册逻辑
- 不要让实现包被 Abstractions 引用，不要让主包引用可插拔子包
- 不要翻转 `PreserveRoutePredicate`，不要按参数名推断路由段
- 不要在没启用 / 没密钥时仍注册对外端点
- 不要把新 MCP 传输塞进 `Web.Mcp` 或改投影层
- 不要在自动化里跑 `framework/scripts/**/*.ps1`
- 不要为了「完整测试」在 CI 假设 Redis / ES 一定存在
- 不要用英文写注释或 XML 文档
- 不要漏文件头；不要提交 CRLF 的 `.cs`
