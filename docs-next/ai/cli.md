# CLI

本页说明 AI 工具与开发者如何用命令行核对一个 5.0 应用：应用自带的命令行宿主命令，以及转发这些命令、生成部署产物的 `xihan` 工具。完整的包说明见 [Cli 包](../packages/cli)与[命令行宿主](../guide/command-line)。

## 特点

- 命令读的是应用自己装配出的能力图谱、管道与操作登记表，结果与运行时一致，不依赖文档版本。
- 输出是纯文本或 JSON，失败时 Problem JSON 写到 stderr、退出码按错误种类区分，适合脚本与 AI 工具判断结果。
- 不需要启动 HTTP 服务，也不需要外部设施。

## 应用自带的命令

应用接入命令行宿主（`Hosting.CommandLine`）后，以下命令直接可用：

| 命令 | 输出 |
| --- | --- |
| `help` | 用法与命令列表 |
| `graph` | 已解析的能力图谱：每个特性提供与依赖的能力、选中的提供者 |
| `pipeline` | 管道的最终顺序：一行一个中间件，阶段名与类型全名 |
| `operations` | 登记的操作：操作 ID、稳定性、暴露的协议、授权要求 |
| `invoke <操作 ID> --json '<载荷>'` | 经完整管道调用一个操作 |
| `<resource> <verb> --字段 值` | `invoke` 的简写 |
| `mcp` | 加入 `McpProtocolFeature` 后可用：以 stdio 提供 MCP 服务端 |

```bash
dotnet run --project next/samples/SimpleCli -- operations
dotnet run --project next/samples/SimpleCli -- greetings create --name Ada --times 2
dotnet run --project next/samples/SimpleCli -- invoke greetings.create --json '{"name":"Ada","times":99}'; echo $?
```

第三条的 `times` 超出范围，stderr 输出 Problem JSON，退出码为 2。`--tenant`、`--locale`、`--authorization`、`--timeout`、`--correlation` 把租户、区域、凭据、超时与关联 ID 放进调用上下文。

## xihan 工具

`XiHan.Framework.Cli` 是一个 .NET 工具，命令名 `xihan`。5.0 尚未发布到 nuget.org，从本地包源安装（打包步骤见[快速上手](../quickstart#获取预览包)）：

```bash
dotnet tool install --global XiHan.Framework.Cli --version 5.0.0-preview.1 --add-source ./next/artifacts/packages
```

| 命令 | 说明 |
| --- | --- |
| `xihan graph` / `pipeline` / `operations` / `invoke` | 转发给应用：`dotnet run --project <项目> --configuration Release -- <命令>`；项目取 `--project`，否则取当前目录 `xihan.app.json` 的 `project` |
| `xihan <resource> <verb> ...` | 转发 `invoke` 的简写 |
| `xihan pack` | 读 `xihan.app.json`，生成 Dockerfile、Kustomize、Compose 与 Aspire 产物，见[部署与打包](../guide/deployment) |
| `xihan migrate <来源版本>` | 通用的代码迁移机制；当前没有登记任何规则集，任何来源版本都返回 `CLI_VALIDATION_002` |
| `xihan version` | 框架版本 |
| `xihan help` | 命令列表 |

```bash
xihan graph --project src/Shop.Cli
xihan pack --manifest src/Shop/xihan.app.json --output deploy/shop --only dockerfile,kustomize
```

## 退出码

| 退出码 | 含义 |
| --- | --- |
| `0` | 成功 |
| `2` | 校验失败（VALIDATION），如载荷不满足契约、应用清单不合法 |
| `3` | 不存在（NOTFOUND），如操作未登记、`xihan.app.json` 不存在 |
| `64` | 用法错误 |
| 其他 | 按错误 KIND 映射，完整对照见[命令行宿主](../guide/command-line) |

转发的命令原样返回应用进程的退出码。

## 相关

- [For Agents](./agents) · [MCP Server](./mcp) · [LLMs.txt](./llms)
- [Cli 包](../packages/cli) · [Hosting.CommandLine 包](../packages/hosting-commandline)
