# ADR-0076 打包支持 Kafka 与 Nacos 绑定；`xihan mcp` 与 `xihan setup`

## 状态

已采纳（`spec/rfcs/0016-packaging.md`、`spec/packaging/app-manifest.md`、`spec/packaging/app.schema.json`、`spec/manifest/manifest.schema.json` 随之修订）。

## 日期

2026-11-01

## 一、`xihan pack` 支持 `stream` 与 `configuration:remote` 绑定

### 背景

`Messaging.Kafka` 的清单声明可选绑定 `stream`（类型 `stream`，主值是引导地址），`Configuration.Nacos` 声明可选绑定 `nacos`（类型 `configuration:remote`，主值是服务地址）。`xihan pack` 的绑定类型只认 `connection:relational`、`cache:distributed`、`queue`、`blob`，`AppManifestReader` 遇到别的类型直接判清单不合法，用到 Kafka 或 Nacos 的应用因此打包不了；上一轮只在 RFC-0016 里注明「不支持」。

### 备选

1. 两个 Provider 不再声明绑定，只读各自的配置节。
2. `xihan pack` 认这两种类型，三种产物各自生成对应的依赖服务与注入。

### 决策

取 2。

- `BindingKind` 增加 `stream`（Compose 镜像 `apache/kafka:4.1.0`，端口 9092）与 `configuration:remote`（`nacos/nacos-server:v2.5.1`，端口 8848）；应用清单 Schema 与能力清单 Schema 的绑定类型说明同步。
- 注入的主值与 Provider 读取绑定主值的形式一致：Kafka 是 `<绑定名>:9092`，Nacos 是 `http://<绑定名>:8848`（没有路径时 Provider 用缺省上下文路径 `/nacos`）。
- Compose：Kafka 是单节点 KRaft（`KAFKA_PROCESS_ROLES=broker,controller`，对外监听 `PLAINTEXT://<绑定名>:9092`，内部主题副本数 1），健康检查 `kafka-broker-api-versions.sh --bootstrap-server localhost:9092`，`start_period` 30 秒；Nacos 是 `MODE=standalone`，数据目录挂命名卷 `<绑定名>-data`，健康检查 `curl -fs http://localhost:8848/nacos/v1/console/health/readiness`，`start_period` 60 秒。应用照旧 `depends_on: condition: service_healthy`。
- Aspire：Kafka 用 `AddKafka`（`Aspire.Hosting.Kafka`，连接串即引导地址）并 `WithReference`；Nacos 没有官方集成，以固定标签的容器资源运行，注入它的 `http` 端点。
- Kustomize：与其余绑定一样只生成 Secret 示例（`type`、`uri`）与挂载，不在集群里起依赖服务。

### 理由

- 两种绑定都已在 Provider 清单里声明，且主值格式明确；让 Provider 退回只读配置节，等于放弃 Service Binding 与 Compose / Aspire 的一致注入，部署方要为这两个包另写一套。
- Kafka 镜像一旦给了任何 `KAFKA_*` 变量就不再使用自带的配置文件，所以 KRaft 必需项要写全；对外监听写服务名，否则 Compose 网络里的应用拿到的元数据指向 `localhost`，连不上。
- 镜像都固定版本：Nacos 取一致性套件验证过的 2.5.1（3.x 默认开启鉴权且 v1 接口不再可用）；Kafka 取 4.1.0（`Confluent.Kafka` 2.16 支持）。两者都在本机 Docker 上以生成的 `compose.yaml` 起过：健康检查转为 healthy，另一个容器经 `stream:9092` 生产与消费、经 `http://nacos:8848/nacos` 发布与读取配置都成功。
- Kustomize 产物一贯不托管有状态依赖（PostgreSQL、Redis 也没有），Kafka 与 Nacos 不例外。

### 影响

- `xihan.app.json` 的 `bindings.<名>.type` 多两个合法值；以前写这两种类型的清单是 `CLI_VALIDATION_001`，现在可以打包。
- 清单里的绑定名要写成 Provider 读取的名字（`stream`、`nacos`），Provider 才取得到注入的值；与其余四种绑定相同。
- Compose 依赖服务都不映射宿主机端口；要打开 Nacos 控制台时另加 override 文件。
- 测试：`PackagerTests` 对 Compose 产物做整份快照、对 Aspire 与 Kustomize 做断言；`BindingKindTests` 核对各 Provider 清单声明的绑定类型与 `BindingKind` 一一对应、新增种类固定镜像标签。

## 二、`xihan mcp`：编码助手的框架知识库

### 背景

编码助手需要一个 stdio 的 MCP 服务端来查框架的包、特性与配置键：BasicApp 的 AI 文档让编码助手用三个 tool 查询。

包的身份与依赖在特性清单（`Manifest`：ID、种类与角色、提供与依赖的能力、绑定、贡献的配置节 / 管道 / 操作 / 权限 / 健康项）里；配置键由各 Options 类的 `FromConfiguration` 手写读取（键名与属性名不一一对应，如 `HealthProbeIntervalSeconds` → `TimeSpan HealthProbeInterval`），属性初始值不等于配置的缺省写法。用正则读源码既读不到清单（清单是对象初始化器、引用常量），也给不出可靠的键与缺省值。

### 数据从哪里来：备选

1. **读用户项目实际引用的包**：运行时加载项目输出目录里的框架程序集，实例化特性读 `Manifest`，反射 Options。版本天然一致，但要求项目先构建成功、在工具进程里执行用户依赖链上的代码；Options 的缺省值仍要实例化才拿得到，键名仍然拿不到；离线、未构建、跨平台的场景都失效。
2. **CLI 构建时由 MSBuild 步骤或源生成器生成**：生成器看得到的是语法与元数据，读不到其他程序集里对象初始化器的值，也解析不了 `FromConfiguration` 的语义；MSBuild 步骤要在 CLI 构建时先构建并加载全部包，构建图与并行构建都变复杂。
3. **随工具嵌入的索引 + 防漂移测试**：索引是仓库里提交的文件，构建时嵌入工具；测试项目引用全部正式包，用真实代码核对索引。

### 决策

取 3，分三部分：

- **包与特性清单** `Knowledge/catalog.json`：由 `XiHan.Framework.Cli.Tests` 从 `framework/src/*/*.csproj`（说明、项目引用、包引用 + 中央包管理版本、共享框架）、`framework/README.md` 的包表（环层与定位）与各包程序集里全部公开 `IFeature` 实例的 `Manifest` 生成；与代码不一致时 `KnowledgeCatalogTests` 失败，`framework/scripts/Update-CliKnowledge.ps1` 重新生成。泛型特性以约束类型封闭，只有委托参数的构造函数传空操作；`Testing.StubFeature`（清单由调用方传入）列在排除名单里，名单本身也受测试约束。
- **配置键** `Knowledge/configuration.json`：人工逐条整理（50 个 Options 类、240 余个键：键、类型、缺省值的配置写法、必填、上下限 / 开区间下限 / 枚举取值、示例、说明；按名字展开的段写占位符，读取自身节以外的键写绝对路径）。`KnowledgeConfigurationTests` 用记录读取路径的 `IConfiguration` 调用每个 `FromConfiguration`：全量配置下每个键都被读到、节内读到的路径都在索引里；只给必填键能成功、去掉任何一个必填键会失败；每个缺省值与不写该键得到的 Options 按内容相同；上下限取边界成功、越界失败，枚举每个取值成功、`__invalid__` 失败；另要求每个带 `ConfigurationSection` 常量与 `FromConfiguration(IConfiguration, …)` 的公开类型都在索引里。用变异核对过测试本身：改错缺省值、删键、加不存在的键、收紧上下限都会失败。
- **文档**：构建时把 `docs/**/*.md`、`framework/spec/rfcs`、`framework/spec/conventions` 与各包 `README.md` 作为嵌入资源打进程序集（约 2 MB）。

### MCP 协议：复用 `Protocols.Mcp`

`xihan mcp` 不引入 MCP SDK，而是内嵌一个最小的 XiHan 应用：`Host.CreateEmptyApplicationBuilder`（不读当前目录的 appsettings 与环境变量，不注册控制台日志）+ `InMemoryLocalizationFeature` + `McpProtocolFeature` + 本工具的 `KnowledgeFeature`。三个查询是只读、只暴露给 MCP 的操作 `cli.info` / `cli.config` / `cli.search`，经 `XiHan:Protocols:Mcp:Tools` 改名为 `xihan_framework_info` / `xihan_framework_config` / `xihan_framework_search`；标题与描述是本工具消息目录里的 `cli.info.title` 等（10 种语言，按 `--locale`）；`Protocols.Mcp` 的 stdio 传输处理 JSON-RPC、`tools/list`（只读幂等提示、输入输出 Schema）与 `tools/call`（经内核管道，查不到以 `isError` 返回 `CLI_NOTFOUND_003`）。标准输入按 UTF-8 读（Windows 上重定向的 `Console.In` 用 OEM 代码页，中文检索词会乱码）。代价是工具带入 ASP.NET Core 共享框架（装了 .NET SDK 的机器都有）。

### 输入输出

| tool | 内容 |
| --- | --- |
| `xihan_framework_info` | 包（安装命令、环层、框架包 / 第三方包 / 共享框架依赖、读取的配置节、相关文档）与其中每个特性的清单（种类与角色、提供与依赖的能力、绑定、配置节、管道中间件、操作、权限码、健康项）；输入能力 ID 时给出提供者（含 `default` 角色）与依赖方；不给 `name` 时列出全部包（环层、定位、特性清单 ID） |
| `xihan_framework_config` | Options 读取的配置键（完整路径、类型、缺省值、必填、取值范围）与按缺省值写出的 appsettings 片段；可按节（含子节）、完整键路径（占位符任意匹配）、包、Options 类名或文字查 |
| `xihan_framework_search` | 检索文档站、RFC 与约定、各包 README；`page` 参数取一页全文 |

输出是结构化 JSON（`structuredContent` 与文本内容相同）：结构化字段由编码助手直接取用，文本里的说明来自文档与索引，不需要另写本地化的版式文案。

### 不做的

- 更新日志 tool 与命令行的 `list` / `info` / `doc` / `config` / `search` / `changelog` 查询命令：本轮只要求 `mcp` 与 `setup`；更新日志是文档站的 `changelog.md`，可经 `xihan_framework_search` 检索与读取。
- MCP 提示词：`Protocols.Mcp` 只实现 tools，使用规范放在服务端 `instructions` 与 `setup` 写入的技能里。
- NuGet 的 MCP 服务描述（`.mcp/server.json`、`PackageType=McpServer`）：工具包同时承担 `pack` 与转发命令，不改包类型；需要登记到 MCP 注册表时再补。

### 影响

- `XiHan.Framework.Cli` 新增对 `Protocols.Mcp` 的引用；为让顶级语句的 `Program` 不被 ASP.NET Core 的源生成器公开，显式声明为 `internal`。
- 新错误码 `CLI_NOTFOUND_003`、`CLI_VALIDATION_004`；新消息键 `cli.mcp.*`、`cli.info.*`、`cli.config.*`、`cli.search.*`、`cli.knowledge.*`。
- 任何改动特性清单、项目文件说明与引用、`framework/README.md` 包表的提交，都要运行 `Update-CliKnowledge.ps1` 更新 `catalog.json`；改动 Options 读取的键、缺省值或范围的提交，要按 `KnowledgeConfigurationTests` 的失败信息更新 `configuration.json`。这是有意的门禁：索引与代码不一致就不能合入。

## 三、`xihan setup`：把服务写进编码助手的配置

### 背景

BasicApp 的 AI 文档要让用户一次写好框架与组件库两个服务的配置。

### 决策

`setup` 由 `SetupPlanner` 规划动作：`--client claude|cursor|vscode|codex`、`--mode mcp|skill|both`、`--pin`、`--dry-run`、`--check`；JSON 配置只改本服务一项，Codex 只打印片段，技能写进 `.claude/skills` 或 `.agents/skills`。

- `dnx` 命令（Windows 上的 Claude Code 经 `cmd /c`）、版本选择（`--pin` → 项目引用的 `XiHan.Framework.*` 版本 → 工具版本）；服务名是 `xihan-framework`；技能用框架的写法（特性与能力、操作契约、`Result` / `Error`、消息键、管道中间件、资源绑定），随工具嵌入。
- `--path`：从给定目录向上找项目根（缺省当前目录），测试与脚本不必改工作目录。
- 先把要合并的 JSON 配置都读通再写：任何一份读不了（含注释的 JSONC、顶层不是对象）就一个文件都不写。
- 输出与提示文案全部走 CLI 的消息键（10 种语言）；失败按命令行宿主的约定输出 Problem JSON：`--client` / `--mode` 不认识 `CLI_VALIDATION_005`（64），找不到项目根 `CLI_NOTFOUND_004`（3），配置读不了 `CLI_VALIDATION_006`（2），`--check` 未就绪 `CLI_CONFLICT_001`（5，`detail` 列出各项）。

### 影响

- 新错误码 `CLI_NOTFOUND_004`、`CLI_VALIDATION_005`、`CLI_VALIDATION_006`、`CLI_CONFLICT_001`；新消息键 `cli.setup.*`。
- 写入 `.mcp.json` 等文件时用 LF 与两空格缩进重排整份 JSON（内容不变）；原文件里的注释无法保留，所以带注释的文件直接拒绝而不是静默丢掉注释。
