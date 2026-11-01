# XiHan.Framework.Cli

命令行工具 `xihan`（.NET 工具包，RFC-0016）。

```bash
dotnet tool install --global XiHan.Framework.Cli
xihan pack                                   # 读 ./xihan.app.json，写到 ./deploy/
xihan pack --manifest src/Orders/xihan.app.json --output deploy/orders --only dockerfile,kustomize
xihan graph --project src/Orders             # 转发给应用：dotnet run --project src/Orders -- graph
xihan orders create --name widget --amount 3 # 转发 invoke 简写
xihan migrate v5 --path src --dry-run --report migrate.json   # 按来源版本的规则集，先看报告（当前没有规则集）
xihan mcp                                    # 以 stdio 提供框架离线知识库的 MCP 服务（编码助手以子进程启动）
xihan setup --client claude                  # 把 mcp 服务与使用技能写进 .mcp.json 与 .claude/skills/
xihan version
```

| 命令 | 说明 |
| --- | --- |
| `pack` | 从 `xihan.app.json`（规范：`spec/packaging/app-manifest.md`）生成 `Dockerfile` + `Dockerfile.dockerignore`、`kustomize/`（Deployment、Service、ConfigMap、HPA、PDB、Lease 的 RBAC、Secret 示例、kustomization；不在集群里起依赖服务，Kafka、Nacos 也只出 Secret 示例）、`compose.yaml`、`aspire/XiHanAppHost.g.cs`；逐行输出写出的文件。chiseled 运行镜像里没有 shell 与 `curl`，Dockerfile 在构建阶段另外从内联源码编译就绪探针 `/xihan/healthprobe`，Compose 的 healthcheck 用它请求 `/health/ready`。绑定在 Compose 与 Aspire 里以 `XiHan__Bindings__<名>__type` / `__value` 注入，`blob` 的依赖服务是 S3 兼容的 RustFS（`rustfs/rustfs:1.0.4`，ADR-0086），另注入它的 `accessKey` / `secretKey` 属性（RFC-0016「资源绑定与配置节」）；依赖服务镜像一律固定版本，Kustomize 的 Secret 示例以注释列出各类型可单独给出的凭据键 |
| `migrate <来源版本>` | 按来源版本的规则集改写应用代码（通用约定：`spec/migration/README.md`；语法级 Roslyn 重写，不加载 MSBuild 工作区）：`--path` 目录、`.csproj` 或 `.slnx`（默认当前目录），`--fixers` 选规则子集（仍按规则集顺序），`--dry-run` 只报告不写文件，`--report` 把 JSON 报告写到文件。执行分两遍：先扫描收集跨文件事实，再按规则顺序改写回写，之后写生成文件、删除与改名；改不了的位置留 `// xihan-migrate(<规则>): …` 标记，再跑一遍零改动。跳过 `bin/`、`obj/`、`.git/`、`.vs/`、`node_modules/`、`*.g.cs`、`*.Designer.cs`。当前没有登记任何规则集：4.x 单独维护，不提供 4.x → 5.0 迁移（ADR-0021），任何来源版本都报 `CLI_VALIDATION_002` |
| `graph` / `pipeline` / `operations` / `invoke` | 转发给应用自己的命令行宿主（`Hosting.CommandLine`）：`dotnet run --project <project> --configuration <c> -- <命令> …`；项目取 `--project`，否则取当前目录清单的 `project` |
| `mcp` | 以 stdio 提供框架离线知识库的 MCP 服务（ADR-0076）：内嵌一个最小的 XiHan 应用（内存本地化 + `Protocols.Mcp` + 三个只读操作），协议与传输由 `Protocols.Mcp` 处理。三个 tool 沿用 4.x 的名字：`xihan_framework_info`（包、特性清单、能力 ID：安装命令、环层、框架与第三方依赖、提供与依赖的能力、绑定、配置节、管道中间件、操作、权限码、健康项；能力 ID 给出提供者与依赖方；不给名字列出全部包）、`xihan_framework_config`（Options 读取的配置键：完整路径、类型、缺省值、必填、取值范围与按缺省值写出的 appsettings 片段；按节、键路径、包、Options 类名或文字查）、`xihan_framework_search`（检索文档站、RFC 与约定、各包 README，或以 `page` 取一页全文）。都只读、只给 MCP，结果是结构化 JSON；查不到为 `isError`（`CLI_NOTFOUND_003`），检索参数不合法为 `CLI_VALIDATION_004`。tool 的标题与描述按 `--locale`；`--path`（默认当前目录）所在项目引用的 `XiHan.Framework.*` 版本与工具不同时在 stderr 与服务端说明里提示改用 `dnx XiHan.Framework.Cli@<版本> --yes -- mcp` |
| `setup --client <claude\|cursor\|vscode\|codex>` | 把 `mcp` 服务写进编码助手的项目配置：Claude Code `.mcp.json`（Windows 上经 `cmd /c dnx`）、Cursor `.cursor/mcp.json`、VS Code `.vscode/mcp.json`（键 `servers`、带 `"type": "stdio"`）、Codex 只打印 `~/.codex/config.toml` 片段；服务名 `xihan-framework`，命令 `dnx XiHan.Framework.Cli@<版本> --yes -- mcp`。`--mode mcp\|skill\|both`（缺省 both）另写使用技能 `SKILL.md`（Claude 写 `.claude/skills/xihan-framework/`，其余写 `.agents/skills/xihan-framework/`）；版本取 `--pin`，否则取项目引用的 `XiHan.Framework.*` 版本，再否则取工具版本；项目根是 `--path`（缺省当前目录）向上的 git 根或含解决方案 / 项目文件的目录。JSON 配置只替换本服务一项，其余条目与顶层键原样保留，重复执行结果相同；要合并的配置读不了（含注释、顶层不是对象）时一个文件都不写（`CLI_VALIDATION_006`）。`--dry-run` 只打印，`--check` 核对是否就绪（未就绪 `CLI_CONFLICT_001`，退出码 5） |
| `version` | 框架版本 |
| `help` | 命令列表 |

退出码沿用命令行宿主：成功 0，`mcp` 在 stdin 关闭后 0，`setup` 的 `--client` / `--mode` 不认识 64（`CLI_VALIDATION_005`）、找不到项目根 3（`CLI_NOTFOUND_004`）、配置文件读不了 2（`CLI_VALIDATION_006`）、`--check` 未就绪 5（`CLI_CONFLICT_001`），清单不存在 3（`CLI_NOTFOUND_001`），清单不合法 2（`CLI_VALIDATION_001`，`detail` 列出全部问题），用法错误 64，转发命令原样返回子进程退出码；`migrate` 的路径不存在 3（`CLI_NOTFOUND_002`），来源版本没有规则集或 `--fixers` 有不认识的编号 64（`CLI_VALIDATION_002` / `CLI_VALIDATION_003`）。文案按 `--locale` 或当前 UI 文化；`migrate` 留下的标记也按这个区域。

## 离线知识库

`mcp` 的数据随工具一起构建，与工具版本一致（ADR-0076）：

- `Knowledge/catalog.json`：全部正式包（说明、环层、框架与第三方依赖）与全部公开特性的清单，由 `XiHan.Framework.Cli.Tests` 从项目文件、`next/README.md` 包表与各特性的 `Manifest` 生成；与代码不一致时 `KnowledgeCatalogTests` 失败，运行 `pwsh next/scripts/Update-CliKnowledge.ps1` 重新生成。
- `Knowledge/configuration.json`：各包 Options 类读取的键、类型、缺省值、必填与取值范围，人工整理；`KnowledgeConfigurationTests` 以记录读取的配置调用每个 `FromConfiguration`，核对键都被读到、读到的键都在索引里、必填键缺了就失败、缺省值与不写等价、上下限与枚举按索引接受或拒绝，并要求每个带 `ConfigurationSection` 与 `FromConfiguration` 的 Options 类都在索引里。改了 Options 就按失败信息改它。
- 文档：构建时把 `docs-next/**/*.md`、`next/spec/rfcs`、`next/spec/conventions` 与各包 `README.md` 嵌入程序集。

## 依赖方向

工具链；依赖 `Hosting.CommandLine`（解析、帮助、失败输出、退出码）、`Localization.InMemory`（文案）、`Protocols.Mcp`（`mcp` 的协议与 stdio 传输，带入 ASP.NET Core 共享框架）与 `Microsoft.CodeAnalysis.CSharp`（`migrate` 的语法树）。

## 状态

P5：`pack`（含 Kafka 与 Nacos 绑定）、转发命令、`mcp` 与 `setup` 可用。`migrate` 的通用机制可用、规则集为空：4.x 规则集（V4-01 ~ V4-10）已按 ADR-0021 删除。
