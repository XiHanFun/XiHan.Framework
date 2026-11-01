# XiHan.Framework.Cli

命令行工具 `xihan`（.NET 工具包，RFC-0016）：`pack` 从应用清单 `xihan.app.json` 生成 Dockerfile、Kustomize、Compose 与 Aspire 产物，`graph` / `pipeline` / `operations` / `invoke` 转发给应用自己的命令行宿主，`migrate` 提供按来源版本选规则集的源码迁移机制，`mcp` 以 stdio 给编码助手提供框架离线知识库（包与特性清单、配置键与缺省值、文档）。适用于本地开发与 CI 里的打包与诊断，以及让 AI 编码工具按工具版本查证框架。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Cli` |
| 环层 | 工具链 |
| 能力 | — |
| 特性入口 | —（命令名 `xihan`，`PackAsTool`） |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Hosting.CommandLine`（解析、帮助、失败输出、退出码）、`XiHan.Framework.Localization.InMemory`（文案）、`XiHan.Framework.Protocols.Mcp`（`mcp` 的协议与 stdio 传输，带入 ASP.NET Core 共享框架）；`Microsoft.CodeAnalysis.CSharp` 4.14.0（`migrate` 的语法树） |
| AOT | 不适用（以框架依赖的 .NET 工具运行） |
| 稳定性 | Preview |

## 概述

`xihan` 用 `Hosting.CommandLine` 的解析器、帮助与失败输出：失败时把 Problem JSON 写到 stderr，退出码按错误码的 KIND 段映射（[命令行宿主](../guide/command-line)）。文案按 `--locale` 或当前 UI 区域协商，内置与框架相同的 10 种区域。工具包的公开面只有消息键常量 `CliMessages`，命令本身是 `internal` 的，不作为库使用。

| 命令 | 说明 |
| --- | --- |
| `pack` | 从 `xihan.app.json` 生成部署产物，逐行输出写出的文件 |
| `graph` / `pipeline` / `operations` / `invoke` | 转发给应用的命令行宿主 |
| `migrate <来源版本>` | 按来源版本的规则集改写源码；当前没有登记任何规则集 |
| `mcp` | 以 stdio 提供框架离线知识库的 MCP 服务，三个只读 tool：`xihan_framework_info`、`xihan_framework_config`、`xihan_framework_search` |
| `setup --client <claude\|cursor\|vscode\|codex>` | 把 `mcp` 服务与使用技能写进编码助手的项目配置 |
| `version` | 打印框架名与版本 |
| `help` | 命令列表；没有命令时打印到 stderr 并以 64 退出 |

## 何时使用

- 生成容器与 Kubernetes 产物：`xihan pack`，写法见[部署与打包](../guide/deployment)。
- 在仓库根或清单目录里查看应用的能力图谱、管道与操作：`xihan graph` 等转发命令，省去手写 `dotnet run --project … --`。
- 让 Claude Code、Cursor、VS Code、Codex 等编码助手查证包名、能力 ID、配置键与缺省值：把 `xihan mcp` 配成它们的 MCP 服务，见 [MCP Server](../ai/mcp)。
- 不需要工具也能做同样的事：转发命令等价于 `dotnet run --project <项目> -- <命令>`，应用要装配 `Hosting.CommandLine`。

## 安装与启用

```bash
dotnet tool install --global XiHan.Framework.Cli --version 5.0.0-preview.1
xihan version
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。不安装也可以从源码运行（在仓库根执行）：

```bash
dotnet run --project next/src/XiHan.Framework.Cli -- pack --manifest next/samples/SimpleApp/xihan.app.json --output ./artifacts/simple-app-deploy
```

## 命令

### pack

```bash
xihan pack [--manifest <path>] [--output <dir>] [--only dockerfile,kustomize,compose,aspire]
```

| 选项 | 缺省 | 说明 |
| --- | --- | --- |
| `--manifest` | `./xihan.app.json` | 应用清单路径 |
| `--output` | 清单所在目录下的 `deploy/` | 输出目录，产物整份覆盖写入（UTF-8 无 BOM、LF 换行） |
| `--only` | 全部 | 逗号分隔的产物种类，不区分大小写；不认识的种类为 `CLI_VALIDATION_001`，退出码 64 |

产物与清单字段的映射见[部署与打包](../guide/deployment#生成部署产物)。

### graph、pipeline、operations、invoke

```bash
xihan graph --project src/Orders
xihan operations
xihan invoke orders.create --json '{"name":"widget","amount":3}'
xihan orders create --name widget --amount 3
```

转发规则：执行 `dotnet run --project <项目> --configuration <配置> -- <命令> <参数> <其余选项>`。项目取 `--project`，否则取当前目录 `xihan.app.json` 的 `project`，都没有时交给 `dotnet run` 自己找；`--configuration` 缺省 `Release`；`--project` 与 `--configuration` 之外的选项（含 `--locale`）原样转发。不是已知命令、但「命令.第一个参数」是合法操作 ID 时按 `invoke` 简写处理：`xihan orders create …` 等价于 `xihan invoke orders.create …`。退出码是子进程的退出码；`dotnet` 无法启动为 `CLI_UNAVAILABLE_001`，退出码 6。

### migrate

```bash
xihan migrate <来源版本> [--path <目录|.csproj|.slnx>] [--fixers <规则编号,…>] [--dry-run] [--report <文件>]
```

这一版工具没有登记任何规则集：4.x 单独维护，5.0 不提供 4.x → 5.0 的迁移规则（ADR-0021），所以任何来源版本都以 `CLI_VALIDATION_002` 失败，退出码 64。机制本身保留给 5.x 之后的大版本升级（RFC-0020，`spec/migration/README.md`）：语法级 Roslyn 重写，不加载 MSBuild 工作区；先扫描收集跨文件事实，再按规则顺序改写回写，之后写生成文件、删除与改名；改不了的位置留 `// xihan-migrate(<规则编号>): <说明>` 标记；对已迁移的代码再跑一遍零改动；跳过 `bin/`、`obj/`、`.git/`、`.vs/`、`node_modules/`、`*.g.cs`、`*.Designer.cs`。`--dry-run` 只出报告不写文件，`--report` 把 JSON 报告写到文件。从 4.x 迁移的做法见[从 4.x 迁移](../guide/upgrade)。

### mcp

```bash
xihan mcp [--path <项目目录>] [--locale <区域>]
dnx XiHan.Framework.Cli@5.0.0-preview.1 --yes -- mcp    # 不安装、按版本运行（编码助手配置里的写法）
```

以 stdio 提供 MCP 服务（协议版本 `2025-06-18`），stdout 每行一条 JSON-RPC 消息，stdin 关闭即退出。工具内嵌一个最小的 XiHan 应用（内存本地化 + `Protocols.Mcp` + 三个只读、只暴露给 MCP 的操作），协议、`tools/list` 与 `tools/call` 都由 [Protocols.Mcp](./protocols-mcp) 处理；tool 的标题与描述按 `--locale`（或当前 UI 区域）取自工具的消息目录。服务名 `xihan-framework`。

| tool | 输入 | 输出 |
| --- | --- | --- |
| `xihan_framework_info` | `name`：包 ID、短名（`Caching.Redis`）、文档页名（`caching-redis`）、清单 ID、特性类型名或能力 ID（可带 `@版本`）；不给时列出全部包 | 包：安装命令、环层、框架包 / 第三方包 / 共享框架依赖、每个特性的清单（种类与角色、提供与依赖的能力、绑定、配置节、管道中间件、操作、权限码、健康项）、读取的配置节、相关文档页；能力：提供者（含 `default` 角色）与依赖方 |
| `xihan_framework_config` | `query`：配置节（含其下子节）、完整键路径（按名字展开的段任意匹配）、包、Options 类名，或键名与说明里的文字；不给时列出全部配置节 | 每个键的完整路径、类型、缺省值、是否必填、上下限或枚举取值、说明，以及按缺省值与必填示例写出的 appsettings 片段 |
| `xihan_framework_search` | `query`（空格分隔的词须同时出现）与 `limit`（1-50，缺省 10），或 `page`（仓库内路径、`guide/caching`、RFC 编号或包 ID） | 按相关度排序的文档页与摘录，或一页全文 |

结果是结构化 JSON（`structuredContent` 与文本内容相同）。查不到时 `isError`，文本 `CLI_NOTFOUND_003: …` 带最接近的候选；检索既没给 `query` 也没给 `page`、或 `limit` 越界为 `CLI_VALIDATION_004`。

数据随工具一起构建，与工具版本一致：包与特性清单、各 Options 读取的配置键（有测试逐条核对与代码一致），以及文档站、RFC 与约定、各包 README 的全文。`--path`（缺省当前目录）所在项目引用的 `XiHan.Framework.*` 版本与工具不同时，stderr 与服务端说明里提示改用 `dnx XiHan.Framework.Cli@<项目版本> --yes -- mcp`。

### setup

```bash
xihan setup --client <claude|cursor|vscode|codex> [--mode mcp|skill|both] [--pin <版本>] [--path <目录>] [--dry-run] [--check]
dnx XiHan.Framework.Cli@5.0.0-preview.1 --yes -- setup --client claude    # 不安装、按版本运行
```

把 `mcp` 服务（服务名 `xihan-framework`，命令 `dnx XiHan.Framework.Cli@<版本> --yes -- mcp`）写进编码助手的项目配置，并按 `--mode` 写入使用技能：

| `--client` | MCP 配置 | 技能 |
| --- | --- | --- |
| `claude` | `.mcp.json` 的 `mcpServers`；Windows 上命令写成 `cmd /c dnx …` | `.claude/skills/xihan-framework/SKILL.md` |
| `cursor` | `.cursor/mcp.json` 的 `mcpServers` | `.agents/skills/xihan-framework/SKILL.md` |
| `vscode` | `.vscode/mcp.json` 的 `servers`，每项带 `"type": "stdio"` | 同上 |
| `codex` | 不写文件，打印 `~/.codex/config.toml` 的 `[mcp_servers.xihan-framework]` 片段 | 同上 |

| 选项 | 缺省 | 说明 |
| --- | --- | --- |
| `--client` | — | 必填 |
| `--mode` | `both` | `mcp` 只写服务配置，`skill` 只写技能 |
| `--pin` | 项目引用的 `XiHan.Framework.*` 版本，没有时为工具版本 | 服务钉住的工具版本 |
| `--path` | 当前目录 | 从这里向上找项目根：git 根，否则第一个含解决方案或项目文件的目录 |
| `--dry-run` | — | 只打印要写的内容 |
| `--check` | — | 核对配置与技能是否已就绪 |

- 幂等：JSON 配置只替换本服务那一项，别的服务与顶层键原样保留，重复执行得到相同的文件；技能文件整份覆盖。
- 要合并的配置文件读不了（带注释的 JSONC、顶层不是对象）时一个文件都不写，按 `--dry-run` 打印的片段手工合并。
- 版本：数据随工具版本走，`--pin` 与项目引用的版本一致时，编码助手查到的就是项目在用的那一版。

## 行为与限制

退出码：

| 情形 | 错误码 | 退出码 |
| --- | --- | --- |
| 成功 | — | 0 |
| 应用清单不存在 | `CLI_NOTFOUND_001` | 3 |
| 应用清单不合法（`detail` 列出全部问题） | `CLI_VALIDATION_001` | 2 |
| `--only` 有不认识的产物种类 | `CLI_VALIDATION_001` | 64 |
| 没有命令、命令不认识 | — | 64 |
| `dotnet` 无法启动 | `CLI_UNAVAILABLE_001` | 6 |
| 转发命令 | — | 子进程的退出码 |
| `migrate` 的 `--path` 不存在 | `CLI_NOTFOUND_002` | 3 |
| `migrate` 的来源版本没有规则集 | `CLI_VALIDATION_002` | 64 |
| `migrate --fixers` 有不认识的规则编号 | `CLI_VALIDATION_003` | 64 |
| `setup` 的 `--client` 或 `--mode` 不认识 | `CLI_VALIDATION_005` | 64 |
| `setup` 找不到项目根 | `CLI_NOTFOUND_004` | 3 |
| `setup` 要合并的配置文件读不了（不改动任何文件） | `CLI_VALIDATION_006` | 2 |
| `setup --check` 未就绪（`detail` 列出各项） | `CLI_CONFLICT_001` | 5 |
| `mcp` 在 stdin 关闭后 | — | 0 |
| 被 Ctrl+C 取消 | — | 8 |

- Ctrl+C 先取消当前命令，让它按取消语义收尾，而不是直接终止进程。
- `pack` 只读清单与它引用的文件（项目、构建上下文、`config.files`），不构建项目、不调用 Docker 或 kubectl。
- 生成的 Compose 应用服务 healthcheck 用 Dockerfile 随镜像构建的就绪探针 `/xihan/healthprobe` 请求 `/health/ready`（chiseled 镜像里没有 `curl` 与 `wget`；AOT 镜像里是原生可执行文件，否则以 `dotnet` 运行框架依赖的探针），`interval` / `timeout` / `start_period` 取 `probes` 的三个秒数。
- 绑定在 Compose 与 Aspire 里以 `XiHan__Bindings__<名>__type` / `__value` 注入；`blob` 的依赖服务是 S3 兼容的 RustFS（`rustfs/rustfs:1.0.4`，MinIO 社区版已停发镜像，ADR-0086），以固定的本地联调凭据启动，同一对值另作绑定属性 `accessKey` / `secretKey` 注入，覆盖 S3 配置节里的同名键。Kustomize 的 `bindings.example.yaml` 以注释列出各类型可单独给出的凭据键（RFC-0016「资源绑定与配置节」）。
- `CliMessages.Catalog` 是工具的消息目录（目录名 `cli`），10 种区域各一个文件。

## 相关

- [部署与打包](../guide/deployment)、[命令行宿主](../guide/command-line)、[从 4.x 迁移](../guide/upgrade)
- [Hosting.CommandLine](./hosting-commandline)
- 规范：RFC-0016《应用清单与打包》、RFC-0020《版本迁移》；ADR-0021
