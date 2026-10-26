# XiHan.Framework.Cli

命令行工具 `xihan`（.NET 工具包，RFC-0016）。

```bash
dotnet tool install --global XiHan.Framework.Cli
xihan pack                                   # 读 ./xihan.app.json，写到 ./deploy/
xihan pack --manifest src/Orders/xihan.app.json --output deploy/orders --only dockerfile,kustomize
xihan graph --project src/Orders             # 转发给应用：dotnet run --project src/Orders -- graph
xihan orders create --name widget --amount 3 # 转发 invoke 简写
xihan migrate v5 --path src --dry-run --report migrate.json   # 按来源版本的规则集，先看报告（当前没有规则集）
xihan version
```

| 命令 | 说明 |
| --- | --- |
| `pack` | 从 `xihan.app.json`（规范：`spec/packaging/app-manifest.md`）生成 `Dockerfile` + `Dockerfile.dockerignore`、`kustomize/`（Deployment、Service、ConfigMap、HPA、PDB、Lease 的 RBAC、Secret 示例、kustomization）、`compose.yaml`、`aspire/XiHanAppHost.g.cs`；逐行输出写出的文件 |
| `migrate <来源版本>` | 按来源版本的规则集改写应用代码（通用约定：`spec/migration/README.md`；语法级 Roslyn 重写，不加载 MSBuild 工作区）：`--path` 目录、`.csproj` 或 `.slnx`（默认当前目录），`--fixers` 选规则子集（仍按规则集顺序），`--dry-run` 只报告不写文件，`--report` 把 JSON 报告写到文件。执行分两遍：先扫描收集跨文件事实，再按规则顺序改写回写，之后写生成文件、删除与改名；改不了的位置留 `// xihan-migrate(<规则>): …` 标记，再跑一遍零改动。跳过 `bin/`、`obj/`、`.git/`、`.vs/`、`node_modules/`、`*.g.cs`、`*.Designer.cs`。当前没有登记任何规则集：4.x 单独维护，不提供 4.x → 5.0 迁移（ADR-0021），任何来源版本都报 `CLI_VALIDATION_002` |
| `graph` / `pipeline` / `operations` / `invoke` | 转发给应用自己的命令行宿主（`Hosting.CommandLine`）：`dotnet run --project <project> --configuration <c> -- <命令> …`；项目取 `--project`，否则取当前目录清单的 `project` |
| `version` | 框架版本 |
| `help` | 命令列表 |

退出码沿用命令行宿主：成功 0，清单不存在 3（`CLI_NOTFOUND_001`），清单不合法 2（`CLI_VALIDATION_001`，`detail` 列出全部问题），用法错误 64，转发命令原样返回子进程退出码；`migrate` 的路径不存在 3（`CLI_NOTFOUND_002`），来源版本没有规则集或 `--fixers` 有不认识的编号 64（`CLI_VALIDATION_002` / `CLI_VALIDATION_003`）。文案按 `--locale` 或当前 UI 文化；`migrate` 留下的标记也按这个区域。

## 依赖方向

工具链；依赖 `Hosting.CommandLine`（解析、帮助、失败输出、退出码）、`Localization.InMemory`（文案）与 `Microsoft.CodeAnalysis.CSharp`（`migrate` 的语法树）。

## 状态

P3：`pack` 与转发命令可用。`migrate` 的通用机制可用、规则集为空：4.x 规则集（V4-01 ~ V4-10）已按 ADR-0021 删除。离线知识库与 MCP 服务端后续。
