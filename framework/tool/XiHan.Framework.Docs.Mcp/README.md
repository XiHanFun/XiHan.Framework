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
| 日志 | 全部写入 stderr，且一律为结构化日志（字段名即占位符名）。stdout 是 MCP 的 JSON-RPC 协议通道，禁止写入——代码里不出现任何 `Console.WriteLine`。每次工具调用记一条，字段见下文「日志里能看到什么」 |

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

### 首次接入检查

新克隆的仓库里 `bin/` 是空的，而 `.mcp.json` 直接指向构建产物——**没构建过就一定连不上**，客户端通常只回一句语焉不详的错误。第一次接入按下面五步从上往下走一遍，命令都在仓库根执行。

**1. 确认 SDK 版本对得上**

```bash
dotnet --version
```

仓库根的 `global.json` 钉的是 `10.0.302` + `rollForward: latestFeature`，因此这里应打印 `10.0.302`，或同属 10.0 而特性带更高的版本（本机实测 `10.0.400`，属正常上滚）。若报错说找不到与 `global.json` 兼容的 SDK，装上 .NET SDK 10.0.302 或更高再继续——SDK 对不上是最常见的失败原因。

**2. 构建服务端**

```bash
dotnet build framework/tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj -c Release
```

**3. 确认 dll 真的落在预期路径**

PowerShell：

```powershell
Test-Path framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll
```

bash：

```bash
ls -l framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll
```

PowerShell 打印 `True`、bash 列出文件即算通过；否则回第 2 步看构建输出。

**4. 确认客户端看得见这个服务端**

重启或重连 MCP 客户端。Claude Code 里执行 `/mcp`，`xihan-docs` 应处于 connected，工具列表里应有 `search_docs`、`read_doc`、`list_docs` 三项，缺一不可。

**5. 连不上就直接在终端跑一次 dll**

```bash
dotnet framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll
```

健康的服务端会先把索引日志打到 **stderr**：

```text
info: XiHan.Framework.Docs.Mcp.Indexing.DocIndex[0]
      文档索引已重建：163 个文件，1720 个章节，耗时 412 毫秒。
```

随后再打几行宿主启动日志（`transport reading messages`、`Application started`、`Content root path` 等），然后就静静等待 stdin 上的 JSON-RPC 请求——除了每次工具调用各记一条（见下节）之外不再有新输出，这正是正常状态，按 Ctrl+C 结束即可。启动失败则**不会有索引日志**，进程立刻退出并在 stderr 打印原因（最常见的是找不到仓库根，按上文 `XIHAN_DOCS_ROOT` 那一行处理）。

两处容易误判：Git Bash 下那行中文可能显示成乱码，认 `163` 与 `1720` 两个数字即可；另外别用 `echo '...' | dotnet ...` 手工试握手——`echo` 立刻关闭管道，stdin 的 EOF 会让传输层在响应写出前就拆掉连接，真实客户端则会一直握着 stdin。

### 日志里能看到什么

三个工具**从不把异常抛给客户端**——失败会被包成一段说明文字返回。这对模型友好，但也意味着「它什么都没找到」这句反馈，背后可能是真的零命中、相关性截断拒绝、工具内部异常、索引正在重建，或者索引是旧的。日志是唯一把这些分开的地方，全部为结构化日志（字段名即占位符名），全部写 stderr：

| 场景 | 级别 | 关键字段 |
| --- | --- | --- |
| 检索命中 | Information | `Tool`、`Query`、`Source`、`HitCount`、`TopScore`、`ElapsedMs` |
| 检索零命中 | Information | `Tool`、`Query`、`Source`、`SectionCount`、`ElapsedMs`（**没有** `HitCount`） |
| 被相关性截断拒绝 | Information | `Tool`、`Query`、`HitCount`、`Coverage`、`Threshold`、`ElapsedMs` |
| `read_doc` 各条出口 | Information | `Tool`、`Outcome`（`返回全文` / `返回章节` / `章节未找到` / `未在索引内`）、`Path`、`Section` |
| `read_doc` 路径越界 | Warning | `Tool`、`Path` |
| `read_doc` 命中索引却读不到文件（索引已旧） | Warning | `Tool`、`Path` |
| `list_docs` | Information | `Tool`、`Source`、`FileCount`、`TotalFileCount`、`ElapsedMs` |
| 工具抛异常 | Error | `Tool`、`ExceptionType`、`ExceptionMessage`，并附异常对象 |
| 索引重建 | Information | `FileCount`、`SectionCount`、`ElapsedMs` |

`Coverage` 是把 `MinKnownTermCoverage`（默认 0.90）拿真实流量复核的依据：那个阈值是在离线黄金查询集上标定的，只有把被拒绝查询的实际覆盖率记下来，才判断得了它在真实提问上偏紧还是偏松。

查询串是文档问题不是凭据，记录它是有意的；**密钥与任何请求头一律不记**。

### 两份配置，别拿错

仓库里有两份形态不同的配置，差别在 dll 路径是相对还是绝对：

| 文件 | 路径形态 | 适用场景 | 怎么用 |
| --- | --- | --- | --- |
| `.mcp.json` | 相对路径，无 `env` | 客户端以**仓库根**为工作目录（Claude Code 打开本仓库时即是如此） | 已随仓库提供，构建完直接生效，不用改 |
| `.mcp.json.example` | 绝对路径 + 显式 `XIHAN_DOCS_ROOT` | 客户端工作目录**不在仓库内**：Cursor、VS Code、Windsurf，或全局注册的服务端 | **模板**：复制进客户端自己的配置再把路径改成你机器上的实际路径，不要原样使用 |

`.mcp.json` 走相对路径，客户端工作目录一旦不是仓库根，`dotnet` 就找不到这个 dll：

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

服务端启动后会从程序集所在目录逐层向上找到仓库根，因此这份配置不需要额外指定路径。

`.mcp.json.example` 是给其他客户端抄的模板，文件名带 `.example` 就意味着没有任何客户端会去读它；里面的绝对路径是作者机器上的，必须换成你自己的：

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

也可以用 CLI 一行注册，同样走绝对路径，仓库根仍由程序集目录向上推断：

```bash
claude mcp add xihan-docs -- dotnet <仓库绝对路径>/framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll
```

其他 MCP 客户端（Cursor、VS Code、Windsurf 等）配置形式相同：`command` 为 `dotnet`，`args` 指向构建产物的 dll。

### 关于访问令牌

本服务端**不需要也不提供**访问令牌，这不是遗漏。stdio 传输下客户端自己拉起这个进程、进程只服务这一个客户端、环境变量也由同一方设置——没有第二方需要被认证，令牌认证不了任何东西，加上去只是装饰。

令牌真正有意义的地方是「扩展点」里提到的网络传输：一旦 `Tools` 层被搬到 HTTP 后面，端点就对整个网络可达，此时必须做鉴权。届时应照搬 `XiHan.Framework.Web.Mcp` 已有的 fail-closed 范式（配置节 `XiHan:AI:Mcp`，`Enabled` + `ApiKey`，未开启或未配密钥时既不注册服务也不映射端点），而不是在 stdio 形态下先放一个不起作用的字段。

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
