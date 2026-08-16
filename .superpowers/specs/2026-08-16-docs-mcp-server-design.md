# 曦寒框架文档 MCP Server 设计

- **日期**：2026-08-16
- **状态**：已评审通过，待实现
- **范围**：单一子系统，可由一份实现计划覆盖

## 1. 背景与目标

`XiHan.Framework` 是纯类库仓库，66 个 NuGet 包，文档量大且高度结构化，但 AI 助手（Claude Code / Cursor）在协助下游应用开发时无法准确引用这些文档——它只能靠 grep 碰运气，或者凭训练数据编造 API。

本设计构建一个**本机 stdio MCP Server**，把仓库内的文档变成 AI 可检索的知识源，相当于给曦寒框架做一个自己的 Context7。

**成功标准**：AI 助手被问到「分布式事件什么时候发出去」「Redis 事件总线怎么配」这类问题时，能检索到对应文档章节的原文并给出带出处（文件路径 + 行号）的回答，而不是编造。

## 2. 非目标

明确不做，避免范围蔓延：

- **不做远程 HTTP 部署**。本设计只交付本机 stdio，随仓库走。日后若要对外提供，是独立项目。
- **不做语义检索 / 向量嵌入**。语料只有 111 篇、1.5 MB，BM25 的统计优势都发挥不出来，嵌入模型的复杂度和离线索引流程不划算。
- **不改动 `XiHan.Framework.Web.Mcp` 与 `XiHan.Framework.AI/Mcp`**。那两者是给下游应用暴露 `IAiSkill` 的运行时能力，与本设计无任何关系，不共享代码也不互相依赖。
- **不索引源代码**。只索引 Markdown 文档。代码检索由仓库已有的 CodeGraph 负责。

## 3. 内容来源

四类来源，各带一个分类标签，检索时可过滤：

| 分类 | 路径 | 数量 | 性质 |
| --- | --- | --- | --- |
| `Guide` | `docs/guide/*.md` | 38 | 任务导向：怎么选、易错点、最佳实践 |
| `Package` | `docs/packages/*.md` | 67 | API 参考：配置项、工作原理、完整清单 |
| `Root` | `docs/*.md`（非递归） | 6 | 全局：index / introduction / overview / quickstart / why / changelog |
| `PackageReadme` | `framework/src/*/README.md` | 52 | 简洁包说明，固定七段结构，与代码同步度最高 |

合计 163 个文件、约 1.5 MB。注意 `framework/src/` 下有 66 个项目但只有 52 个带 README——枚举时按实际存在的文件收集，缺失不报错。

`docs/` 下的子目录只取 `guide/` 与 `packages/`，**不递归扫描整个 `docs/`**——这样 `.superpowers/`、`.vitepress/`、`node_modules/` 等目录自然被排除，本设计文档自身也不会污染检索结果。枚举 `framework/src/*/README.md` 时必须排除 `obj/` 与 `bin/` 目录。

`PackageReadme` 与 `Package` 内容有重叠，靠来源加权与同文件章节上限控制重复结果。

## 4. 架构

### 4.1 项目落位

主项目 `framework/tool/XiHan.Framework.Docs.Mcp/`，与既有的 `framework/tool/Region/` 平级，照搬其形状：

- `Microsoft.NET.Sdk`、`OutputType=Exe`
- 按序 Import `netcore.props` / `common.props` / `version.props`，**不 Import `nuget.props`**
- `IsPackable=false`——它是仓库内部工具，不作为 NuGet 包发布，不参与 66 包的统一版本发布流程

测试项目 `framework/test/XiHan.Framework.Docs.Mcp.Tests/`，与其余 20 个测试项目同层，Import `netcore.props` / `common.props` / `test.props`。

两者都注册进 `framework/XiHan.Framework.slnx`：主项目在 `/3.tool/` 下新增的 `2.DocsMcp/` 文件夹，测试项目进既有的 `/2.tests/2.UnitTests/`。注册进解决方案意味着 CI 会构建它、会跑它的测试，不会变成无人维护的孤儿。

### 4.2 进程模型

stdio MCP Server，由 MCP 客户端拉起，生命周期跟随客户端。

**硬约束：stdout 是 MCP 的 JSON-RPC 协议通道。** 任何写入 stdout 的内容都会插进协议流中破坏连接。因此：

- 所有日志强制走 stderr（Host 的 logging 显式配置为 stderr provider）
- 代码中禁止出现 `Console.WriteLine`；诊断输出一律 `Console.Error.WriteLine` 或 `ILogger`

这不是可选的最佳实践，是 stdio MCP 最常见的翻车点。

### 4.3 文档根目录定位

三段回退：

1. 环境变量 `XIHAN_DOCS_ROOT` 显式指定 → 直接使用
2. 否则从程序集所在目录逐层向上查找，直到某层**同时存在 `docs/` 与 `framework/` 两个子目录** → 认定为仓库根
3. 都找不到 → **启动失败**：`DocSourceLocator` 抛出异常，`Program` 捕获后向 stderr 输出清晰错误信息（含已尝试的搜索路径与 `XIHAN_DOCS_ROOT` 的用法提示），进程以退出码 1 结束

第 3 点是刻意选择。一个「连得上但永远搜不到东西」的 MCP Server 比一个起不来的更难排查。

### 4.4 索引生命周期与热更新

启动时同步扫描并建立索引（1.5 MB 文本，预期 < 1 秒），建完才开始接受请求。

热更新采用 **mtime 轮询**：每次查询前，若距上次检查超过 2 秒，则扫描全部来源文件的 mtime 与文件集合；有任何变动就整体重建索引。

选择 mtime 轮询而非 `FileSystemWatcher`，理由是后者在网络磁盘、WSL 挂载、以及编辑器「写临时文件再改名」的保存流程下会漏事件，而这里全量重建本来就只需几百毫秒。效果是文档保存后，下一次查询即命中新内容，无需重启客户端。

## 5. 组件设计

六个组件，各自单一职责、可独立测试。

| 组件 | 命名空间 | 职责 |
| --- | --- | --- |
| `DocSourceLocator` | `Sources` | 定位仓库根、枚举四类来源文件并打分类标签 |
| `MarkdownSectionSplitter` | `Indexing` | 一篇 Markdown → 多个 `DocSection` |
| `Tokenizer` | `Indexing` | 中文 bigram + 英文 token + PascalCase 拆词 |
| `BigramIndex` | `Indexing` | 倒排索引：term → 出现的章节及权重信息 |
| `SynonymExpander` | `Search` | 查询词的框架术语扩展 |
| `SectionScorer` | `Search` | 候选章节排序 |

外加一个薄工具层 `DocsMcpTools`（`Tools` 命名空间），只做参数校验与结果组装。

### 5.1 切片规则（`MarkdownSectionSplitter`）

- 按 `#` 与 `##` 标题切分；`###` 及更深层级并入所属 `##` 章节，避免切得过碎
- **必须跳过代码围栏（``` 与 ~~~）内部的 `#`**。这批文档中大量存在 bash 的 `# 注释`、C# 的 `#region` / `#pragma`，不处理会把 `dotnet add package` 那类代码块切成假章节。这是切片器唯一真正的坑
- YAML frontmatter（文件开头的 `---` 块）跳过，不进入索引
- frontmatter 之后、第一个 `##` 之前的前言自成一个「概述」章节。这批文档的前言往往是最精华的定位说明
- 单个章节正文超过 4000 字符时，按空行分段做二次切分，避免一个章节吃掉整个响应
- 每个章节保留：相对路径、来源分类、**标题路径**（如 `事件总线 > 本地事件还是分布式事件`）、原文、起止行号

### 5.2 分词规则（`Tokenizer`）

对同一段文本同时产出两类 term：

- **中文**：连续 CJK 字符切 bigram（「分布式事件」→ `分布`、`布式`、`式事`、`事件`）
- **英文与标识符**：按非字母数字字符切 token 并小写；额外对 PascalCase / camelCase 拆词

PascalCase 拆词让 `ILocalEventBus` 同时索引为 `ilocaleventbus`、`local`、`event`、`bus`，因此用户问「event bus 怎么用」和问 `ILocalEventBus` 都能命中。

单字符的中文 term 不入索引（噪声太大）；单字符英文 token 同样丢弃。

### 5.3 同义词扩展（`SynonymExpander`）

术语表 `Resources/synonyms.json`，随构建复制到输出目录。格式为「一组等价术语」的数组：

```json
[
  ["重复消费", "去重", "幂等", "Inbox", "收件箱"],
  ["事务", "工作单元", "UoW", "UnitOfWork"],
  ["拦截器", "过滤器", "Filter", "AOP", "动态代理"]
]
```

查询时：若查询串包含某组中的任一术语，则把该组其余术语一并加入查询 term 集合，且**扩展词的权重折半**（避免扩展词淹没用户的原始意图）。

首版收录 20–30 条框架核心术语。文件缺失或格式损坏时降级为不扩展，stderr 警告一次，服务照常——术语表是增强，不是必需。

### 5.4 排序规则（`SectionScorer`）

这是日后最常调整的地方，因此规则必须足够简单可懂：

1. **基础分 = 命中 term 数 ÷ 查询 term 总数**。覆盖率优先，防止长章节靠字数多取胜
2. **标题命中 ×3**。标题是人工撰写的最强信号
3. **来源加权**（可配置，默认值）：`Guide` 1.2、`Package` 1.0、`PackageReadme` 0.8、`Root` 0.9。指南略高，因为任务导向的提问占多数
4. **同一文件最多返回 2 个章节**，防止一篇文章洗掉整个结果列表

## 6. 工具契约

三个工具。工具数量刻意压到最少——工具越多，AI 越容易选错或漏用；来源区分用参数表达而非拆成不同工具。

### 6.1 `search_docs`

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `query` | string | 是 | 自然语言问题或关键词 |
| `source` | string | 否 | `guide` / `packages` / `readme` / `root` / `all`，默认 `all` |
| `limit` | int | 否 | 返回章节数，默认 5，上限 15；超过上限时截断到 15 而非报错 |

`source` 取值到分类标签的映射固定为：`guide` → `Guide`、`packages` → `Package`、`readme` → `PackageReadme`、`root` → `Root`、`all` → 不过滤。取值不认识时按 `all` 处理并在结果中附一句提示。

每个结果返回：相对路径、标题路径、行号区间、来源分类、分数、章节原文。

**零命中时不返回空数组**，而是返回明确的「文档中没有相关内容」加上最接近的几个文档标题作为建议。空数组会诱使 AI 自行编造答案；明确的否定回答才能让它如实说不知道。

### 6.2 `read_doc`

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 相对路径，如 `docs/guide/event-bus.md` |
| `section` | string | 否 | 章节标题，只返回该节 |

- 未指定 `section` 且全文超过 30 KB 时，改为返回「章节目录 + 前言」并提示使用 `section` 参数。当前最大文件 39.5 KB，整篇灌入上下文是纯浪费
- **路径安全**：解析后的绝对路径必须仍在仓库根之内，拒绝 `../` 逃逸
- 路径不存在时返回「找不到」加最接近的 3 个候选路径

### 6.3 `list_docs`

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `source` | string | 否 | 同 `search_docs` |
| `includeSections` | bool | 否 | 默认 `false` |

- 默认只返回「路径 + H1 标题 + 一句话摘要」，163 行，约 2–3K tokens
- `includeSections=true` 才展开各章节标题，会膨胀到 15K+ tokens，因此不能是默认值

**一句话摘要的取法**（明确定义，避免实现时各自发挥）：取「概述」章节正文中第一个非空、且不以 `>` / `-` / `|` / ` ``` ` 开头的行，去掉 Markdown 行内标记后截断到 80 个字符。取不到时留空字符串，不报错。

## 7. 错误处理

原则：**永远返回可行动的信息，绝不抛裸异常，绝不让 stdio 连接断开。**

| 情况 | 处理 |
| --- | --- |
| 仓库根找不到 | 启动失败，stderr 清晰信息 + 非零退出码 |
| `synonyms.json` 缺失或损坏 | 降级为不扩展，stderr 警告一次，服务照常 |
| `read_doc` 路径不存在 | 返回「找不到」+ 最接近的 3 个路径 |
| 路径逃逸仓库根 | 明确拒绝并说明原因 |
| 查询零命中 | 返回「文档中没有」+ 建议关键词 |
| 重建索引时某文件读取失败（被编辑器锁定） | 跳过该文件、沿用旧索引中该文件的内容、stderr 警告，不让整次重建失败 |
| 工具内部任何异常 | 捕获后返回结构化错误信息 |

## 8. 测试策略

### 8.1 单元测试（内嵌字符串，不依赖真实 `docs/`）

- **切片器**：代码围栏内的 `#` 不切分、`###` 并入 `##`、frontmatter 跳过、前言成章节、超长章节二次切分
- **分词器**：中英混排、PascalCase 拆词、单字符 term 丢弃
- **排序器**：覆盖率优先（短章节命中 3/3 必须赢过长章节命中 3/8）、标题加权、同文件上限 2
- **定位器**：逐层向上查找、找不到时抛出、路径逃逸拒绝

### 8.2 黄金查询集（跑真实 `docs/`）

一组「查询 → 必须出现在 top3 的文件」断言：

| 查询 | 期望命中 | 验证点 |
| --- | --- | --- |
| 分布式事件什么时候发出去 | `docs/guide/event-bus.md` | 基础中文检索 |
| Redis 事件总线怎么配 | `docs/packages/eventbus-redis.md` | 包级精确定位 |
| 动态 API 路由为什么没有动词 | `docs/guide/dynamic-api.md` | 长查询 |
| 怎么避免重复消费 | `docs/packages/eventbus.md` | 同义词扩展 |
| `ILocalEventBus` | `docs/packages/eventbus-abstractions.md` 或 `eventbus.md` | 英文标识符拆词 |

这组测试是整个设计中**唯一能防止「越调越差」的机制**：调整权重让某个查询变准时，它会立刻暴露是否弄坏了其他查询。

CI 能跑（`docs/` 就在同一仓库，不需要外部服务，因此不需要 `Assert.SkipWhen`）。文档大改导致断言失效时测试会红——这是特性而非缺陷，它在提醒文档改动影响了检索行为。

## 9. 编码约定

遵循仓库既有约定：

- 每个 `.cs` 文件以两行版权声明开头（`XHFH001` 分析器检查）
- 注释与 XML 文档注释一律简体中文
- file-scoped namespace、primary constructor、表达式体属性
- `Nullable` 与 `ImplicitUsings` 已由 `common.props` 全局开启

## 10. 关键权衡记录

| 决策 | 选择 | 放弃的方案与理由 |
| --- | --- | --- |
| 检索算法 | 内存 bigram 倒排 + 自写加权 | SQLite FTS5：多一个 native 依赖，跨平台易踩坑；语料只有 111 篇，BM25 的 IDF 统计不稳，精度优势发挥不出来；排序规则想针对「标题命中加分」这类框架特有需求调整时反而更难 |
| 语义能力 | 人工同义词表 | 向量嵌入：需引入嵌入模型、API 密钥与离线建索引流程，且索引会与文档不同步 |
| 热更新 | mtime 轮询 | `FileSystemWatcher`：在网络盘、WSL、编辑器临时文件保存流程下会静默漏事件 |
| 项目位置 | `framework/tool/` | `framework/src/`：会被当成 NuGet 包发布并绑定统一版本号，但它是 console 工具不是类库，语义不符 |
| 工具数量 | 3 个 | 按来源拆成 6–8 个：语义更明确但撑满 AI 的工具列表，且常发生「该搜 packages 却只搜了 guide」 |
