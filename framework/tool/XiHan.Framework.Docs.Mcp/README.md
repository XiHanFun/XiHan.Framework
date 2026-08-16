# XiHan.Framework.Docs.Mcp

## 概述
把曦寒框架仓库内的 163 篇 Markdown 文档变成 AI 助手可检索的知识源，以本机 stdio MCP Server 的形式对外提供。仓库内部工具，不发布为 NuGet 包（`IsPackable=false`）。

## 核心能力
- 索引四类文档来源：使用指南（`docs/guide`）、包文档（`docs/packages`）、文档站全局文档（`docs` 顶层）、各包 README（`framework/src/*/README.md`）
- 按 Markdown 标题切成章节（当前语料 1720 个），建立内存 bigram 倒排索引，支持中英混合检索
- 框架术语同义词扩展，补足纯字面匹配处理不了的「换句话说」提问
- 相关性截断：问的不是框架文档时，返回明确的「文档中没有，请不要基于猜测作答」，而不是几段蹭词的正文
- 文档保存后自动热更新（mtime 轮询），无需重启客户端
- 三个 MCP 工具：`search_docs` / `read_doc` / `list_docs`。`read_doc` 只放行被索引的那些文档，仓库内的源码与配置文件一律按「未找到」处理，工具的能力边界与它自己的描述一致

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

**仓库根已内建 `.mcp.json`**，构建完就能用，不需要写任何配置：

```json
{
  "mcpServers": {
    "xihan-docs": {
      "command": "dotnet",
      "args": ["framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll"]
    }
  }
}
```

这份配置走相对路径，前提是客户端以仓库根为工作目录（Claude Code 打开本仓库时即是如此）。服务端启动后会从程序集所在目录逐层向上找到仓库根，因此不需要额外指定路径。

**客户端工作目录不在仓库内时**，改用绝对路径并显式指定仓库根——`.mcp.json.example` 就是这个形态，照抄并把路径换成你机器上的实际路径即可：

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

也可以用 CLI 一行注册：

```bash
claude mcp add xihan-docs -- dotnet <仓库绝对路径>/framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll
```

注册后用 `/mcp` 确认 `xihan-docs` 处于 connected，工具列表里应有 `search_docs`、`read_doc`、`list_docs` 三项。

其他 MCP 客户端（Cursor、VS Code、Windsurf 等）配置形式相同：`command` 为 `dotnet`，`args` 指向构建产物的 dll。

### 关于访问令牌

本服务端**不需要也不提供**访问令牌，这不是遗漏。stdio 传输下客户端自己拉起这个进程、进程只服务这一个客户端、环境变量也由同一方设置——没有第二方需要被认证，令牌认证不了任何东西，加上去只是装饰。

令牌真正有意义的地方是「扩展点」里提到的网络传输：一旦 `Tools` 层被搬到 HTTP 后面，端点就对整个网络可达，此时必须做鉴权。届时应照搬 `XiHan.Framework.Web.Mcp` 已有的 fail-closed 范式（配置节 `XiHan:AI:Mcp`，`Enabled` + `ApiKey`，未开启或未配密钥时既不注册服务也不映射端点），而不是在 stdio 形态下先放一个不起作用的字段。

排查连接问题时直接在终端跑一次 dll：正常情况下它会静静等待 stdin 上的 JSON-RPC 请求；启动失败则立刻退出并在 stderr 打印原因。注意用 `echo '...' | dotnet ...` 手工试握手会看不到输出——`echo` 立刻关闭管道，stdin 的 EOF 会让传输层在响应写出前就拆掉连接。真实客户端会一直握着 stdin。

## 扩展点
- 往 `Resources/synonyms.json` 增补术语组即可改善「换句话说」类提问的召回。纯拉丁术语按词条匹配，中文术语按子串匹配
- 调整 `Options/DocsMcpOptions.cs` 中的 `TitleBoost` 与 `SourceWeights` 可改变排序倾向，**改完必须重跑黄金查询集**
- 收紧或放宽「文档里没有」的判定：调 `MinKnownTermCoverage`。调高更容易否认（宁可少答），调低更容易硬答（可能答错）
- 新增文档来源：扩展 `Sources/DocSourceKind.cs` 与 `DocSourceLocator.Enumerate()`
- 新增传输方式（如 HTTP）：新建项目复用 `Indexing` / `Search` / `Tools` 三层，不要改检索逻辑

**动排序之前必须先知道这件事：排序层不区分查询词的信息量。** `SectionScorer` 给每个查询词条同样的权重，于是中文 bigram 切出来的 `怎么`、`什么`、`时候` 这类疑问碎片，以及 `线怎`、`么配` 这类跨词边界伪影，和 `Redis`、`收件箱` 一样重；命中标题时还会被 `TitleBoost` 一起放大三倍。截断层用 IDF 解决过同一个问题（`Search/RelevanceGate.cs` 的类注释讲了另一面：排序不需要 IDF、截断才需要），排序层没有跟进是刻意的取舍而不是遗漏——所以上面「调 `TitleBoost` 与 `SourceWeights` 改变排序倾向」这条，能调的只是来源与标题的倾斜，调不动词与词之间的轻重。

代价有据可查：标定时有两条查询因此被放弃，没能写进黄金查询集。

| 被放弃的查询 | 期望文件 | 实测情况 |
| --- | --- | --- |
| `Redis 事件总线怎么配` | `docs/packages/eventbus-redis.md` | 只到第 5 名。挡在前面的是标题含 `事件`、`线怎`、`怎么` 的噪声章节——低信息量词条拿满权重再被标题加权放大 |
| `怎么避免重复消费` | `docs/packages/eventbus.md` | 内容确实在那儿，同义词扩展也生效了，但扩展词只有 0.5 权重，分散在多个章节里，敌不过第一名靠标题里一个 `怎么` 拿到的 1.0 × 3.0 |

最小的改法是把 `SectionScorer` 的词权重乘上 IDF，并复用 `RelevanceGate` 已有的跨词边界伪影剔除逻辑。**动手之前先把这两条查询加回 `GoldenQueryTests` 当验收标准**，否则改完没有任何东西能证明是变好了而不是变坏了。

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
    IndexSnapshot.cs
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
