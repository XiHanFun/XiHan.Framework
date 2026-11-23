# xihan 工具

本页说明 AI 工具与开发者如何用命令行核对一个基于本框架的应用：应用自带的命令行宿主命令，以及转发这些命令、生成部署产物的 `xihan` 工具。完整的包说明见 [Cli 包](/packages/cli)与[命令行宿主](/protocols/command-line)。

## 特点

- 命令读的是应用自己装配出的能力图谱、管道与操作登记表，结果与运行时一致，不依赖文档版本。
- 输出是纯文本或 JSON，失败时 Problem JSON 写到 stderr、退出码按错误种类区分，适合脚本与 AI 工具判断结果。
- 不需要启动 HTTP 服务，也不需要外部设施。

## 应用自带的命令

应用接入命令行宿主（`Hosting.CommandLine`）后，`help`、`graph`（能力图谱）、`pipeline`（管道的最终顺序）、`operations`（操作登记表）、`invoke <操作 ID>` 与它的简写 `<resource> <verb> --字段 值` 直接可用，加入 `McpProtocolFeature` 后还有 `mcp`；各命令的输出与选项见[命令行宿主](/protocols/command-line#内置命令)。

```bash
dotnet run --project framework/samples/SimpleCli -- operations
dotnet run --project framework/samples/SimpleCli -- greetings create --name Ada --times 2
dotnet run --project framework/samples/SimpleCli -- invoke greetings.create --json '{"name":"Ada","times":99}'; echo $?
```

第三条的 `times` 超出范围，stderr 输出 Problem JSON，退出码为 2。`--tenant`、`--locale`、`--authorization`、`--timeout`、`--correlation` 把租户、区域、凭据、超时与关联 ID 放进调用上下文。

## xihan 工具

`XiHan.Framework.Cli` 是一个 .NET 工具，命令名 `xihan`。当前是预览版，安装要带 `--prerelease`：

```bash
dotnet tool install --global XiHan.Framework.Cli --prerelease
```

| 命令 | 说明 |
| --- | --- |
| `xihan graph` / `pipeline` / `operations` / `invoke` | 转发给应用：`dotnet run --project <项目> --configuration Release -- <命令>`；项目取 `--project`，否则取当前目录 `xihan.app.json` 的 `project` |
| `xihan <resource> <verb> ...` | 转发 `invoke` 的简写 |
| `xihan pack` | 读 `xihan.app.json`，生成 Dockerfile、Kustomize、Compose 与 Aspire 产物，见[部署与打包](/deployment/pack) |
| `xihan migrate <来源版本>` | 通用的代码迁移机制；当前没有登记任何规则集，任何来源版本都返回 `CLI_VALIDATION_002` |
| `xihan mcp` | 以 stdio 提供框架离线知识库的 MCP 服务（包与特性清单、配置键、文档），见 [MCP Server](./mcp) |
| `xihan setup --client <claude\|cursor\|vscode\|codex>` | 把 `mcp` 服务与使用技能写进编码助手的项目配置，`--check` 核对是否就绪 |
| `xihan version` | 框架版本 |
| `xihan help` | 命令列表 |

```bash
xihan graph --project src/Shop.Cli
xihan pack --manifest src/Shop/xihan.app.json --output deploy/shop --only dockerfile,kustomize
```

## 退出码

成功是 `0`，用法错误是 `64`，其余按错误码的 KIND 段映射（如校验失败 `2`、不存在 `3`），对照表见[结果与错误处理](/web/errors#错误码)，`xihan` 自己各命令的错误码见 [Cli 包](/packages/cli#行为与限制)。转发的命令原样返回应用进程的退出码。

## 相关

- [For Agents](./agents) · [MCP Server](./mcp) · [LLMs.txt](./llms)
- [Cli 包](/packages/cli) · [Hosting.CommandLine 包](/packages/hosting-commandline)
