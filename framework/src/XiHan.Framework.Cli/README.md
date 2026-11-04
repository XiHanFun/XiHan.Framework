# XiHan.Framework.Cli

## 关于

`XiHan.Framework.Cli` 是命令行工具 `xihan`：从应用清单生成 Docker、Kubernetes、Compose 与 Aspire 部署产物，把 `graph`、`invoke` 等命令转发给应用，并以 MCP 服务把框架的离线知识库提供给编码助手。

## 主要特性

* `pack`：读取 `xihan.app.json` 生成部署产物
* `graph` / `pipeline` / `operations` / `invoke`：转发给应用自己的命令行宿主
* `mcp` / `setup`：给编码助手提供框架包、配置键与文档的离线检索
* `migrate`：按来源版本的规则集改写应用代码（当前没有登记规则集）

## 如何使用

### 安装

```bash
dotnet tool install --global XiHan.Framework.Cli --prerelease
```

### 使用

在应用项目目录下运行：

```bash
xihan pack                                   # 读 ./xihan.app.json，写到 ./deploy/
xihan pack --only dockerfile,kustomize       # 只生成部分产物
xihan graph --project src/Orders             # 转发给应用的命令行宿主
xihan orders create --name widget --amount 3 # invoke 的简写
xihan setup --client claude                  # 写入 mcp 服务配置与使用技能
xihan version
```

| 命令 | 说明 |
| --- | --- |
| `pack` | 生成 `Dockerfile`、`kustomize/`、`compose.yaml`、`aspire/` |
| `graph` / `pipeline` / `operations` / `invoke` | 转发给应用 |
| `mcp` | 以 stdio 提供离线知识库 MCP 服务 |
| `setup --client <claude\|cursor\|vscode\|codex>` | 把 `mcp` 服务写进编码助手配置 |
| `migrate <来源版本>` | 源码迁移 |

## 主要类型

此库提供的主要类型：

* `pack`、`mcp`、`setup`、`migrate`：命令，工具包没有需要直接引用的类型

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/cli)

## 相关包

* 命令行宿主：[XiHan.Framework.Hosting.CommandLine](https://www.nuget.org/packages/XiHan.Framework.Hosting.CommandLine)

## 反馈与贡献

`XiHan.Framework.Cli` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
