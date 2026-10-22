# XiHan.Framework.Cli

命令行工具 `xihan`（.NET 工具包，RFC-0016）。

```bash
dotnet tool install --global XiHan.Framework.Cli
xihan pack                                   # 读 ./xihan.app.json，写到 ./deploy/
xihan pack --manifest src/Orders/xihan.app.json --output deploy/orders --only dockerfile,kustomize
xihan graph --project src/Orders             # 转发给应用：dotnet run --project src/Orders -- graph
xihan orders create --name widget --amount 3 # 转发 invoke 简写
xihan version
```

| 命令 | 说明 |
| --- | --- |
| `pack` | 从 `xihan.app.json`（规范：`spec/packaging/app-manifest.md`）生成 `Dockerfile` + `.dockerignore`、`kustomize/`（Deployment、Service、ConfigMap、HPA、PDB、Lease 的 RBAC、Secret 示例、kustomization）、`compose.yaml`、`aspire/XiHanAppHost.g.cs`；逐行输出写出的文件 |
| `graph` / `pipeline` / `operations` / `invoke` | 转发给应用自己的命令行宿主（`Hosting.CommandLine`）：`dotnet run --project <project> --configuration <c> -- <命令> …`；项目取 `--project`，否则取当前目录清单的 `project` |
| `version` | 框架版本 |
| `help` | 命令列表 |

退出码沿用命令行宿主：成功 0，清单不存在 3（`CLI_NOTFOUND_001`），清单不合法 2（`CLI_VALIDATION_001`，`detail` 列出全部问题），用法错误 64，转发命令原样返回子进程退出码。文案按 `--locale` 或当前 UI 文化。

## 依赖方向

工具链；依赖 `Hosting.CommandLine`（解析、帮助、失败输出、退出码）与 `Localization.InMemory`（文案）。

## 状态

P3：`pack` 与转发命令可用；`migrate v4`、离线知识库与 MCP 服务端后续。
