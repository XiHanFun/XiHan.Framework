# LLMs.txt

文档站在构建时产出一组给大语言模型读取的纯文本资产，内容与站点正文同源。本页列出这些资产与在 AI 工具中的用法。

## 可用资源

下表的路径相对文档站根地址：

| 文件 | 说明 |
| --- | --- |
| `/llms.txt` | 索引：全部页面的标题、一句话摘要与地址，以及下列资产的入口 |
| `/llms-full.txt` | 全站正文 |
| `/llms-guide.txt` | 开发指南全部章节 |
| `/llms-packages.txt` | 包总览与全部包页 |

单页 Markdown：任一页面地址后加 `.md` 得到这一页的 Markdown，例如 `/guide/operations.md`、`/packages/kernel.md`。每页正文上方的「取本页 Markdown」指向它。

`llms-full.txt` 体积较大。多数场景先读 `llms.txt` 索引，再按需取单页或只取用到的分册。

## 在 AI 工具中使用

| 工具 | 做法 |
| --- | --- |
| Claude Code | 写进项目的 `CLAUDE.md` |
| Cursor | 写进 `.cursor/rules`，或在对话里用 `@Docs` 添加 `llms.txt` 的地址 |
| Codex | 写进项目的 `AGENTS.md` |
| GitHub Copilot | 写进 `.github/copilot-instructions.md` |
| Windsurf | 写进 `.windsurf/rules` |

可以直接使用的提示词见 [For Agents](./agents)。在 XiHan.Framework 仓库内工作的工具可以不经网络，直接读 `docs-next/` 下的 Markdown 源文件。

## 相关

- [For Agents](./agents) · [MCP Server](./mcp) · [CLI](./cli)
