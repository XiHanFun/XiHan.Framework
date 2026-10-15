来源：https://framework.docs.xihanfun.com/ai/agents

# For Agents

给 Claude Code、Cursor、Codex、GitHub Copilot 这类 AI 编程工具的入口：一段可以直接粘贴的提示词，加上 XiHan.Framework 为 AI 准备的全部资料从哪里取。

::: tip 和「AI 与 MCP」不是一回事
本册讲的是**用 AI 工具写基于 XiHan.Framework 的代码**。要在自己的应用里接入大模型、把应用能力暴露成 MCP 工具，见开发指南的 [AI 与 MCP](../guide/ai)。
:::

## 复制这段提示词

把下面这段放进对话，或写进项目的 `AGENTS.md` / `CLAUDE.md` / `.cursor/rules`：

```text
这个项目用 XiHan.Framework 写后端。写代码前先读这两份文档并按其中的规范执行：
- https://framework.docs.xihanfun.com/ai/agents.md
- https://framework.docs.xihanfun.com/ai/SKILL.md

XiHan.Framework 的包名、模块类名、配置键与 API 以项目引用的版本为准，可能与你记得的不同，也不要套用别的框架的写法。
不确定时用 CLI 查证（数据随工具离线，版本与项目对齐，需要 .NET 10 SDK）：
  dnx XiHan.Framework.Cli@<版本> --yes -- info <包或模块>      安装命令、模块类与依赖
  dnx XiHan.Framework.Cli@<版本> --yes -- config <配置节或包>  配置键、类型与缺省值
  dnx XiHan.Framework.Cli@<版本> --yes -- search <关键字>      在文档里检索
支持 MCP 时，运行 dnx XiHan.Framework.Cli@<版本> --yes -- setup --client <claude|cursor|vscode|codex> 接入 XiHan.Framework 的 MCP 服务端。
```

`<版本>` 写项目里 `XiHan.Framework.*` 包引用的版本。

## Agent 能拿到什么

### CLI：离线、与版本一致的知识库

[`XiHan.Framework.Cli`](./cli) 是一个 .NET 工具，把全部包的模块类与 `[DependsOn]` 依赖、包依赖、全部配置节（属性、类型、缺省值与说明）、整站文档与更新日志打包在 NuGet 包里，不联网，查到的内容与工具的版本一致；它与框架各包同号发布。

```bash
xihan-framework list                          # 按层列出包与模块类
xihan-framework info Caching                  # 安装命令、模块依赖、包依赖、配置节
xihan-framework config XiHan:Caching:Redis    # 配置节的属性、类型、缺省值与配置片段
xihan-framework search 分布式锁               # 在全部文档里检索
xihan-framework doc guide/dynamic-api         # 指南全文
xihan-framework changelog 4.6.0 4.6.2         # 两版之间的变更
```

不想安装时把 `xihan-framework` 换成 `dnx XiHan.Framework.Cli@<版本> --yes --`。

### MCP Server：把 CLI 接进 AI 工具

`xihan-framework mcp` 以 stdio 启动 [MCP 服务端](./mcp)，AI 工具可以直接调用 `xihan_framework_info`、`xihan_framework_config`、`xihan_framework_search` 等工具。

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

在项目里运行 `setup` 会读出项目引用的框架版本，并把配置写到所在 git 仓库的根目录。

### 技能：XiHan.Framework 的使用规范

[`SKILL.md`](https://framework.docs.xihanfun.com/ai/SKILL.md) 是写给 AI 的使用规范：先查证再写代码、应用骨架、模块与 `[DependsOn]`、动态 API 应用服务、配置节与基础设施、常见错误。它随工具发布，`setup --mode skill` 把它写进项目：Claude Code 写到 `.claude/skills/xihan-framework/`，其余工具写到 `.agents/skills/xihan-framework/`。MCP 提示词 `xihan-framework-expert` 用的也是这一份。

### LLMs.txt：直接放进上下文的文档

文档站构建时产出 [机读资产](./llms)：`llms.txt` 索引、`llms-full.txt` 全站正文、`llms-guide.txt` 开发指南、`llms-packages.txt` 模块总览，以及每一页的 Markdown（站点地址后加 `.md`，如 [guide/caching.md](https://framework.docs.xihanfun.com/guide/caching.md)）。

## 各部分的关系

| 部分 | 回答什么 | 什么时候用 |
| --- | --- | --- |
| 提示词与技能 | 规范：该怎么写、不该怎么写 | 每次会话开头，或长期写进项目的 Agent 规则 |
| CLI | 事实：某个包有哪些模块、依赖与配置键 | 加包引用、写 `[DependsOn]`、写 appsettings 之前 |
| MCP Server | 同上，以工具的形式交给 AI | AI 工具支持 MCP 时，替代手动跑 CLI |
| LLMs.txt | 整份文档 | 工具不能运行命令、只能读网页时 |

CLI、MCP 与技能都离线且跟随版本；LLMs.txt 在文档站上，内容对应最新发布的版本。

## 前端与整套应用

前端用 XiHan.UI 时，它有自己的 CLI 与 MCP 服务端，见 [视图组件的 AI 辅助开发](https://ui.docs.xihanfun.com/ai/agents)。基于 XiHan.BasicApp 二次开发时，两边一起接入，见 [基础应用的 AI 辅助开发](https://basicapp.docs.xihanfun.com/ai/agents)。

## 相关

- [快速上手](../quickstart) · [模块系统](../guide/modularity) · [配置与选项](../guide/configuration)
