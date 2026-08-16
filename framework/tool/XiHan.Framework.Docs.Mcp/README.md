# XiHan.Framework.Docs.Mcp

## 概述
把曦寒框架仓库内的 163 篇 Markdown 文档变成 AI 助手可检索的知识源，以本机 stdio MCP Server 的形式对外提供。仓库内部工具，不发布为 NuGet 包（`IsPackable=false`）。

## 核心能力
- 索引四类文档来源：使用指南（`docs/guide`）、包文档（`docs/packages`）、文档站全局文档（`docs` 顶层）、各包 README（`framework/src/*/README.md`）
- 按 Markdown 标题切成章节（当前语料 1720 个），建立内存 bigram 倒排索引，支持中英混合检索
- 框架术语同义词扩展，补足纯字面匹配处理不了的「换句话说」提问
- 相关性截断：问的不是框架文档时，返回明确的「文档中没有，请不要基于猜测作答」，而不是几段蹭词的正文
- 文档保存后自动热更新（mtime 轮询），无需重启客户端
- 三个 MCP 工具：`search_docs` / `read_doc` / `list_docs`

## 依赖关系
- `ModelContextProtocol` 2.2.0：MCP 协议与 stdio 传输
- `Microsoft.Extensions.Hosting` 10.0.0：宿主与依赖注入
- 不依赖仓库内任何其他项目（仅把 `XiHan.Framework.Analyzers` 作为分析器引用，不进运行期）

## 配置与约定
| 项 | 说明 |
| --- | --- |
| `XIHAN_DOCS_ROOT` | 环境变量，显式指定仓库根。不设置时从程序集所在目录逐层向上查找同时含 `docs/` 与 `framework/` 的目录；找不到则退出码 1 并在 stderr 给出已尝试路径 |
| `Resources/synonyms.json` | 术语同义词表，缺失或格式损坏时降级为不扩展，服务照常 |
| 日志 | 全部写入 stderr。stdout 是 MCP 的 JSON-RPC 协议通道，禁止写入 |

排序与截断的可调参数都在 `Options/DocsMcpOptions.cs`：

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `TitleBoost` | `3.0` | 标题命中的加权倍数 |
| `SourceWeights` | 指南 1.2 / 包文档 1.0 / 全局 0.9 / 包自述 0.8 | 各来源权重 |
| `MaxSectionsPerFile` | `2` | 同一文件最多返回的章节数 |
| `MinKnownTermCoverage` | `0.90` | 相关性截断阈值：查询里语料认识的词条，其 IDF 之和至少要占全部词条的这个比例 |
| `MinLatinTermDocumentFrequency` | `2` | 拉丁词条要出现在至少这么多章节里才算「语料认识它」 |
| `MinSectionsForRelevanceCutoff` | `200` | 语料小于此规模时不做截断（IDF 在极小语料上不可用） |

**改动上表任何一项后必须重跑黄金查询集**：`framework/test/XiHan.Framework.Docs.Mcp.Tests/GoldenQueryTests.cs` 是唯一能发现「调好了一条、弄坏了四条」的机制。

## 使用方式
先构建：

```bash
dotnet build framework/tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj -c Release
```

产物在 `framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll`。

在 Claude Code 中注册：把下面这段写进仓库根的 `.mcp.json`（项目级，会随仓库共享），或用户级的 `~/.claude.json`。把 `<仓库绝对路径>` 换成实际路径：

```json
{
  "mcpServers": {
    "xihan-docs": {
      "command": "dotnet",
      "args": ["<仓库绝对路径>/framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll"]
    }
  }
}
```

也可以用 CLI 一行注册：

```bash
claude mcp add xihan-docs -- dotnet <仓库绝对路径>/framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll
```

注册后用 `/mcp` 确认 `xihan-docs` 处于 connected，工具列表里应有 `search_docs`、`read_doc`、`list_docs` 三项。

其他 MCP 客户端（Cursor、VS Code、Windsurf 等）配置形式相同：`command` 为 `dotnet`，`args` 指向构建产物的 dll。若客户端的工作目录不在仓库内，加一条环境变量指向仓库根：

```json
{
  "mcpServers": {
    "xihan-docs": {
      "command": "dotnet",
      "args": ["<仓库绝对路径>/framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll"],
      "env": { "XIHAN_DOCS_ROOT": "<仓库绝对路径>" }
    }
  }
}
```

排查连接问题时直接在终端跑一次 dll：正常情况下它会静静等待 stdin 上的 JSON-RPC 请求；启动失败则立刻退出并在 stderr 打印原因。注意用 `echo '...' | dotnet ...` 手工试握手会看不到输出——`echo` 立刻关闭管道，stdin 的 EOF 会让传输层在响应写出前就拆掉连接。真实客户端会一直握着 stdin。

## 扩展点
- 往 `Resources/synonyms.json` 增补术语组即可改善「换句话说」类提问的召回。纯拉丁术语按词条匹配，中文术语按子串匹配
- 调整 `Options/DocsMcpOptions.cs` 中的 `TitleBoost` 与 `SourceWeights` 可改变排序倾向，**改完必须重跑黄金查询集**
- 收紧或放宽「文档里没有」的判定：调 `MinKnownTermCoverage`。调高更容易否认（宁可少答），调低更容易硬答（可能答错）
- 新增文档来源：扩展 `Sources/DocSourceKind.cs` 与 `DocSourceLocator.Enumerate()`
- 新增传输方式（如 HTTP）：新建项目复用 `Indexing` / `Search` / `Tools` 三层，不要改检索逻辑

## 目录结构
```text
XiHan.Framework.Docs.Mcp/
  README.md
  Program.cs
  Options/
    DocsMcpOptions.cs
  Sources/
    DocSourceKind.cs
    DocFile.cs
    DocSourceLocator.cs
  Indexing/
    DocSection.cs
    Tokenizer.cs
    MarkdownSectionSplitter.cs
    BigramIndex.cs
    DocIndex.cs
  Search/
    WeightedTerm.cs
    SynonymExpander.cs
    SearchHit.cs
    SectionScorer.cs
    RelevanceGate.cs
  Tools/
    DocsMcpTools.cs
  Resources/
    synonyms.json
```
