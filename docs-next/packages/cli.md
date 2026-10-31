# XiHan.Framework.Cli

命令行工具 `xihan`（.NET 工具包，RFC-0016）：`pack` 从应用清单 `xihan.app.json` 生成 Dockerfile、Kustomize、Compose 与 Aspire 产物，`graph` / `pipeline` / `operations` / `invoke` 转发给应用自己的命令行宿主，`migrate` 提供按来源版本选规则集的源码迁移机制。适用于本地开发与 CI 里的打包与诊断。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Cli` |
| 环层 | 工具链 |
| 能力 | — |
| 特性入口 | —（命令名 `xihan`，`PackAsTool`） |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Hosting.CommandLine`（解析、帮助、失败输出、退出码）、`XiHan.Framework.Localization.InMemory`（文案）；`Microsoft.CodeAnalysis.CSharp` 4.14.0（`migrate` 的语法树） |
| AOT | 不适用（以框架依赖的 .NET 工具运行） |
| 稳定性 | Preview |

## 概述

`xihan` 用 `Hosting.CommandLine` 的解析器、帮助与失败输出：失败时把 Problem JSON 写到 stderr，退出码按错误码的 KIND 段映射（[命令行宿主](../guide/command-line)）。文案按 `--locale` 或当前 UI 区域协商，内置与框架相同的 10 种区域。工具包的公开面只有消息键常量 `CliMessages`，命令本身是 `internal` 的，不作为库使用。

| 命令 | 说明 |
| --- | --- |
| `pack` | 从 `xihan.app.json` 生成部署产物，逐行输出写出的文件 |
| `graph` / `pipeline` / `operations` / `invoke` | 转发给应用的命令行宿主 |
| `migrate <来源版本>` | 按来源版本的规则集改写源码；当前没有登记任何规则集 |
| `version` | 打印框架名与版本 |
| `help` | 命令列表；没有命令时打印到 stderr 并以 64 退出 |

## 何时使用

- 生成容器与 Kubernetes 产物：`xihan pack`，写法见[部署与打包](../guide/deployment)。
- 在仓库根或清单目录里查看应用的能力图谱、管道与操作：`xihan graph` 等转发命令，省去手写 `dotnet run --project … --`。
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
| 被 Ctrl+C 取消 | — | 8 |

- Ctrl+C 先取消当前命令，让它按取消语义收尾，而不是直接终止进程。
- `pack` 只读清单与它引用的文件（项目、构建上下文、`config.files`），不构建项目、不调用 Docker 或 kubectl。
- 生成的 Compose 应用服务 healthcheck 是 `dotnet --version`，不探测 `/health/ready`，Native AOT 镜像里没有 `dotnet`。
- `CliMessages.Catalog` 是工具的消息目录（目录名 `cli`），10 种区域各一个文件。

## 相关

- [部署与打包](../guide/deployment)、[命令行宿主](../guide/command-line)、[从 4.x 迁移](../guide/upgrade)
- [Hosting.CommandLine](./hosting-commandline)
- 规范：RFC-0016《应用清单与打包》、RFC-0020《版本迁移》；ADR-0021
