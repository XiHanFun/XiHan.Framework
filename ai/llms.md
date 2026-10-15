来源：https://framework.docs.xihanfun.com/ai/llms

# LLMs.txt

文档站在构建时产出一组给大语言模型读的机读资产，内容与站点正文同源。AI 工具读取它们即可了解 XiHan.Framework 的模块、API 与用法。

## 可用资源

| 文件 | 说明 |
| --- | --- |
| [llms.txt](https://framework.docs.xihanfun.com/llms.txt) | 索引：全部页面的标题、一句话摘要与地址，以及下列资产的入口 |
| [llms-full.txt](https://framework.docs.xihanfun.com/llms-full.txt) | 全站正文 |
| [llms-guide.txt](https://framework.docs.xihanfun.com/llms-guide.txt) | 开发指南：按能力域组织的用法与原理 |
| [llms-packages.txt](https://framework.docs.xihanfun.com/llms-packages.txt) | 模块总览：每个包的 API 清单与配置项全表 |
| [ai/SKILL.md](https://framework.docs.xihanfun.com/ai/SKILL.md) | 给 AI 编程工具的使用规范，见 [For Agents](./agents) |

**单页 Markdown**：任何一页的站点地址后加 `.md` 即可取到这一页的 Markdown，如 [guide/caching.md](https://framework.docs.xihanfun.com/guide/caching.md)、[packages/web-api.md](https://framework.docs.xihanfun.com/packages/web-api.md)。每页正文上方的「取本页 Markdown」也指向它。

`llms-full.txt` 体积较大，多数场景读 `llms.txt` 索引后按需取单页，或只取用到的分册。

## 在 AI 工具中使用

所有工具都可以用同一段提示词：

```text
阅读 https://framework.docs.xihanfun.com/llms.txt 了解 XiHan.Framework，写代码时按需读取其中列出的页面（站点地址后加 .md 取 Markdown），以文档为准，不要凭记忆写包名、模块类名与配置键。
```

| 工具 | 做法 |
| --- | --- |
| Claude Code | 写进项目的 `CLAUDE.md`，或用 `/memory` 记下 |
| Cursor | 在对话中用 `@Docs` 添加 `https://framework.docs.xihanfun.com/llms.txt`，或写进 `.cursor/rules` |
| Codex | 写进项目的 `AGENTS.md` |
| GitHub Copilot | 写进 `.github/copilot-instructions.md` |
| Windsurf | 写进 `.windsurf/rules` |

能运行命令或接入 MCP 的工具，优先用 [CLI](./cli) 或 [MCP Server](./mcp)：数据离线且与项目版本一致，配置节还能精确到属性的类型与缺省值；文档站上的资产对应最新发布的版本。

## 相关

- [For Agents](./agents) · [MCP Server](./mcp) · [CLI](./cli)
