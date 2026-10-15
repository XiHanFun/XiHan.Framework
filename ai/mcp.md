来源：https://framework.docs.xihanfun.com/ai/mcp

# MCP Server

本页介绍如何通过 Model Context Protocol（MCP）在 AI 编程工具中使用 XiHan.Framework 的知识库。

## 什么是 MCP

[Model Context Protocol](https://modelcontextprotocol.io) 是一个开放协议，让 AI 模型调用外部工具与数据源。接入 XiHan.Framework 的 MCP 服务端后，AI 写代码前可以直接查到包、模块类、`[DependsOn]` 依赖与配置键，而不是凭记忆猜。

## 官方 MCP Server

服务端由 [`XiHan.Framework.Cli`](./cli) 提供，命令是 `xihan-framework mcp`，走 stdio 传输，基于官方的 ModelContextProtocol C# SDK。数据随 NuGet 包离线发布、不联网，内容与工具的版本一致；工具与框架各包同号发布，钉住与项目相同的版本号即可查到项目实际用的包与配置。

### 工具

| 工具 | 说明 |
| --- | --- |
| `xihan_framework_list` | 按所在层列出全部包与模块类，可按层或关键字过滤 |
| `xihan_framework_info` | 一个包的安装命令、模块类与 `[DependsOn]`、引用的框架包与第三方包、绑定的配置节 |
| `xihan_framework_doc` | 文档页全文：开发指南、包参考页（写包名即可）或开始页；不传参数时列出全部页面 |
| `xihan_framework_config` | 配置节的属性、类型、缺省值与说明，附按缺省值写出的 appsettings 片段；不传参数时列出全部配置节 |
| `xihan_framework_search` | 在全部文档里检索关键字，按相关度返回页面与摘录 |
| `xihan_framework_changelog` | 查某一版或两版之间的更新日志，可按包名过滤 |

工具都是只读的。

### 提示词

| 提示词 | 说明 |
| --- | --- |
| `xihan-framework-expert` | 以 XiHan.Framework 专家身份工作，内容就是 [使用技能](https://framework.docs.xihanfun.com/ai/SKILL.md) |
| `xihan-framework-module` | 按描述写一个框架模块，参数 `description` |

### 配置

不安装工具、按版本直接运行（需要 .NET 10 SDK 自带的 `dnx`）：

```json
{
  "mcpServers": {
    "xihan-framework": {
      "command": "dnx",
      "args": ["XiHan.Framework.Cli@<版本>", "--yes", "--", "mcp"]
    }
  }
}
```

或者先装成全局工具，再直接调用命令：

```bash
dotnet tool install --global XiHan.Framework.Cli --version <版本>
```

```json
{
  "mcpServers": {
    "xihan-framework": {
      "command": "xihan-framework",
      "args": ["mcp"]
    }
  }
}
```

`<版本>` 写项目里 `XiHan.Framework.*` 包引用的版本。省略版本号时 `dnx` 取最新版，查到的内容可能比项目里引用的新。

### 自动写入

在项目里运行 `setup`，它会扫描项目文件与 props 里的 `XiHan.Framework.*` 包引用读出版本，把钉住版本的配置写进对应文件，只改 `xihan-framework` 这一项，不动已有的其他服务。配置写在所在 git 仓库的根目录（不在仓库里时写在向上第一个含解决方案或项目文件的目录），扫描也从这里开始：

```bash
dnx XiHan.Framework.Cli --yes -- setup --client claude              # .mcp.json，并写入技能
dnx XiHan.Framework.Cli --yes -- setup --client cursor --mode mcp   # 只写 .cursor/mcp.json
dnx XiHan.Framework.Cli --yes -- setup --client vscode              # .vscode/mcp.json
dnx XiHan.Framework.Cli --yes -- setup --client codex               # 打印 ~/.codex/config.toml 片段
dnx XiHan.Framework.Cli --yes -- setup --client claude --dry-run    # 只打印，不写文件
dnx XiHan.Framework.Cli --yes -- setup --client claude --check      # 核对配置是否就绪、版本是否一致
```

`--mode` 取 `mcp`、`skill` 或 `both`（缺省）；`--pin <版本>` 显式指定版本。配置文件已存在但无法解析为 JSON（例如带注释）时，`setup` 不改动文件并给出提示，按 `--dry-run` 打印的片段手动合并即可。

## 在各工具中配置

### Claude Code

项目根目录的 `.mcp.json` 使用上面的写法，也可以用命令添加：

```bash
claude mcp add --scope project xihan-framework -- dnx XiHan.Framework.Cli@<版本> --yes -- mcp
```

Windows（非 WSL）上 Claude Code 需要经 `cmd /c` 调用 `dnx`，`setup --client claude` 会自动写成这种形式：

```json
{
  "mcpServers": {
    "xihan-framework": {
      "command": "cmd",
      "args": ["/c", "dnx", "XiHan.Framework.Cli@<版本>", "--yes", "--", "mcp"]
    }
  }
}
```

### Cursor

写在项目的 `.cursor/mcp.json` 或用户目录的 `~/.cursor/mcp.json`，使用通用写法。

### VS Code

写在项目的 `.vscode/mcp.json`，顶层键是 `servers`，并标明传输类型：

```json
{
  "servers": {
    "xihan-framework": {
      "type": "stdio",
      "command": "dnx",
      "args": ["XiHan.Framework.Cli@<版本>", "--yes", "--", "mcp"]
    }
  }
}
```

### Visual Studio

Visual Studio 读取解决方案目录下的 `.mcp.json` 或 `.vs/mcp.json`，写法同 VS Code（顶层键 `servers`）。

### Codex

写在 `~/.codex/config.toml`：

```toml
[mcp_servers.xihan-framework]
command = "dnx"
args = [
  "XiHan.Framework.Cli@<版本>",
  "--yes",
  "--",
  "mcp"
]
```

### 其他工具

其他支持 stdio MCP 的工具，按各自的配置位置填入同一组 `command` 与 `args` 即可。

## 备选：LLMs.txt

工具不支持 MCP、也不能运行命令时，让它读文档站的 [机读资产](./llms)：`https://framework.docs.xihanfun.com/llms.txt` 是索引，任何一页在站点地址后加 `.md` 即可取到 Markdown。

## 相关

- [For Agents](./agents) · [CLI](./cli) · [LLMs.txt](./llms)
- 在自己的应用里对外提供 MCP 服务端：[XiHan.Framework.Web.Mcp](../packages/web-mcp)
