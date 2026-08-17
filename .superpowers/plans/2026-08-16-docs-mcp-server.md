# 曦寒框架文档 MCP Server 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个本机 stdio MCP Server，把仓库内 163 篇 Markdown 文档变成 AI 助手可检索的知识源。

**Architecture:** 启动时扫描四类文档来源，按 Markdown 标题切成章节，建立内存 bigram 倒排索引；查询时经人工术语同义词表扩展后打分排序，通过 `search_docs` / `read_doc` / `list_docs` 三个 MCP 工具返回带出处的章节原文。mtime 轮询实现文档热更新。

**Tech Stack:** .NET 10 / C# latest、`ModelContextProtocol` 2.2.0（stdio 传输）、`Microsoft.Extensions.Hosting` 10.0.0、xunit.v3 4.0.0。

**设计文档：** `.superpowers/specs/2026-08-16-docs-mcp-server-design.md`

## Global Constraints

以下是项目级约束，**每个任务的要求都隐含包含本节全部内容**：

- 目标框架 `net10.0`，`LangVersion` 为 `latest`（由 `props/netcore.props` 提供）
- `Nullable` 与 `ImplicitUsings` 全局 enable，`GenerateDocumentationFile` 为 True（由 `props/common.props` 提供）——public 成员缺 `<summary>` 会告警
- **每个 `.cs` 文件必须以这两行开头**，由分析器 `XHFH001` 检查：
  ```csharp
  // Copyright (c) 2021-Present XiHanFun and contributors.
  // Licensed under the MIT License. See LICENSE in the project root for license information.
  ```
- 注释与 XML 文档注释**一律简体中文**
- 使用 file-scoped namespace、primary constructor、表达式体属性/访问器；**表达式体方法与构造函数明确关闭**（见 `framework/.editorconfig`）
- 主项目按序 Import `netcore.props` / `common.props` / `version.props`，**不 Import `nuget.props`**；`OutputType=Exe`、`IsPackable=false`
- 测试项目按序 Import `netcore.props` / `common.props` / `test.props`，并用 `PackageReference Update` 覆盖版本为：`xunit.v3` 4.0.0、`Microsoft.NET.Test.Sdk` 18.9.0、`xunit.runner.visualstudio` 4.0.0、`coverlet.collector` 10.0.1
- **代码中禁止出现 `Console.WriteLine`**。stdout 是 MCP 的 JSON-RPC 协议通道，任何写入都会破坏连接。诊断输出一律用 `ILogger` 或 `Console.Error.WriteLine`
- 提交信息用中文 Conventional Commits，scope 固定为 `docs-mcp`，例如 `feat(docs-mcp): 新增中文分词器`
- 根命名空间 `XiHan.Framework.Docs.Mcp`，子命名空间 `.Sources` / `.Indexing` / `.Search` / `.Tools` / `.Options`

## File Structure

**主项目** `framework/tool/XiHan.Framework.Docs.Mcp/`：

| 文件 | 职责 |
| --- | --- |
| `XiHan.Framework.Docs.Mcp.csproj` | 项目定义 |
| `README.md` | 仓库固定七段结构的模块说明 |
| `Program.cs` | Host 组装、stdio 传输、stderr 日志、启动失败处理 |
| `Options/DocsMcpOptions.cs` | 来源权重、topN 上限、节流间隔等可调参数 |
| `Sources/DocSourceKind.cs` | 来源分类枚举 |
| `Sources/DocFile.cs` | 单个文档文件的描述 |
| `Sources/DocSourceLocator.cs` | 仓库根定位 + 四类来源枚举 |
| `Indexing/DocSection.cs` | 章节模型 |
| `Indexing/Tokenizer.cs` | 中文 bigram + 英文 token + PascalCase 拆词 |
| `Indexing/MarkdownSectionSplitter.cs` | Markdown → 章节切片 |
| `Indexing/BigramIndex.cs` | 倒排索引 |
| `Indexing/DocIndex.cs` | 索引门面 + mtime 热更新 |
| `Search/WeightedTerm.cs` | 带权查询词 |
| `Search/SynonymExpander.cs` | 术语同义词扩展 |
| `Search/SearchHit.cs` | 单条检索结果 |
| `Search/SectionScorer.cs` | 排序 |
| `Tools/DocsMcpTools.cs` | 三个 MCP 工具 |
| `Resources/synonyms.json` | 术语表 |

**测试项目** `framework/test/XiHan.Framework.Docs.Mcp.Tests/`：`TokenizerTests` / `MarkdownSectionSplitterTests` / `DocSourceLocatorTests` / `BigramIndexTests` / `SynonymExpanderTests` / `SectionScorerTests` / `GoldenQueryTests`。

---

### Task 1: 项目骨架与解决方案注册

**Files:**
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj`
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Program.cs`
- Create: `framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj`
- Test: `framework/test/XiHan.Framework.Docs.Mcp.Tests/SmokeTests.cs`
- Modify: `framework/XiHan.Framework.slnx`

**Interfaces:**
- Consumes: 无（首个任务）
- Produces: 两个可构建的项目；命名空间根 `XiHan.Framework.Docs.Mcp`；后续任务的所有代码都放进这两个项目

- [ ] **Step 1: 创建主项目 csproj**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <Import Project="..\..\props\netcore.props" />
    <Import Project="..\..\props\common.props" />
    <Import Project="..\..\props\version.props" />

    <PropertyGroup>
        <Title>XiHan.Framework.Docs.Mcp</Title>
        <AssemblyName>XiHan.Framework.Docs.Mcp</AssemblyName>
        <PackageId>XiHan.Framework.Docs.Mcp</PackageId>
        <Description>曦寒框架文档 MCP 服务端</Description>
        <OutputType>Exe</OutputType>
        <!-- 仓库内部的文档检索工具，不对外发布 -->
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="ModelContextProtocol" Version="2.2.0" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    </ItemGroup>

    <ItemGroup>
        <None Update="Resources\synonyms.json" CopyToOutputDirectory="PreserveNewest" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: 创建最小 Program.cs**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Program.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdout 是 MCP 的 JSON-RPC 协议通道，全部日志必须走 stderr
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

- [ ] **Step 3: 构建主项目，确认包 API 名称正确**

运行：`dotnet build framework/tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj -c Release`

期望：构建成功。

如果 `AddMcpServer` / `WithStdioServerTransport` / `WithToolsFromAssembly` 报「找不到方法」，说明 `ModelContextProtocol` 2.2.0 的 API 与此处不同。此时运行以下命令查看实际导出的扩展方法，并按实际名称修正上面的代码：

```bash
dotnet list framework/tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj package
```

然后到 `~/.nuget/packages/modelcontextprotocol/2.2.0/lib/` 下确认程序集，或参考 `framework/src/XiHan.Framework.Web.Mcp/Extensions/DependencyInjection/XiHanWebMcpServiceCollectionExtensions.cs` 里对同版本 SDK 的调用方式。**不要跳过这一步**——后续 Task 9、Task 10 都依赖这三个方法名。

- [ ] **Step 4: 创建测试项目 csproj**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <Import Project="..\..\props\netcore.props" />
    <Import Project="..\..\props\common.props" />
    <Import Project="..\..\props\test.props" />

    <ItemGroup>
      <ProjectReference Include="..\..\tool\XiHan.Framework.Docs.Mcp\XiHan.Framework.Docs.Mcp.csproj" />
    </ItemGroup>

    <ItemGroup>
      <PackageReference Update="coverlet.collector" Version="10.0.1">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      </PackageReference>
      <PackageReference Update="Microsoft.NET.Test.Sdk" Version="18.9.0" />
      <PackageReference Update="xunit.runner.visualstudio" Version="4.0.0">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      </PackageReference>
      <PackageReference Update="xunit.v3" Version="4.0.0" />
    </ItemGroup>

</Project>
```

注意：主项目是 `OutputType=Exe`，测试项目引用它是合法的（`Program.cs` 的顶级语句会生成一个 `internal class Program`，不影响引用）。

- [ ] **Step 5: 写冒烟测试**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/SmokeTests.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 冒烟测试：确认测试项目与主项目的引用链路通畅
/// </summary>
public class SmokeTests
{
    /// <summary>
    /// 测试项目能够正常运行
    /// </summary>
    [Fact]
    public void 测试项目可以运行()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 6: 运行测试确认通过**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj`

期望：1 个测试通过。

- [ ] **Step 7: 注册进 slnx**

修改 `framework/XiHan.Framework.slnx`：

在 `/3.tool/` 文件夹下，`1.ProjectStandardization` 之后新增一个文件夹并放入主项目：

```xml
<Folder Name="/3.tool/2.DocsMcp/">
  <Project Path="tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj" />
</Folder>
```

在 `/2.tests/2.UnitTests/` 文件夹内新增一行：

```xml
<Project Path="test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj" />
```

- [ ] **Step 8: 构建整个解决方案确认无破坏**

运行：`dotnet build framework/XiHan.Framework.slnx -c Release -p:GeneratePackageOnBuild=false`

期望：构建成功，且输出中出现 `XiHan.Framework.Docs.Mcp` 与 `XiHan.Framework.Docs.Mcp.Tests`。

- [ ] **Step 9: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp framework/test/XiHan.Framework.Docs.Mcp.Tests framework/XiHan.Framework.slnx
git commit -m "feat(docs-mcp): 新增文档 MCP Server 项目骨架"
```

---

### Task 2: 中英混合分词器

**Files:**
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Indexing/Tokenizer.cs`
- Test: `framework/test/XiHan.Framework.Docs.Mcp.Tests/TokenizerTests.cs`

**Interfaces:**
- Consumes: Task 1 的项目骨架
- Produces: `public static class Tokenizer`，方法 `public static IReadOnlyList<string> Tokenize(string? text)`。Task 5（BigramIndex）与 Task 6（SynonymExpander）都调用它。

**背景：** 中文没有空格分词。这里对连续中文字符切 bigram（双字），对英文标识符既保留整词又按 PascalCase 拆词，这样 `ILocalEventBus` 和「event bus」都能命中同一段文本。

- [ ] **Step 1: 写失败的测试**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/TokenizerTests.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 分词器测试
/// </summary>
public class TokenizerTests
{
    /// <summary>
    /// 连续中文按双字切分
    /// </summary>
    [Fact]
    public void 中文切成双字词()
    {
        var terms = Tokenizer.Tokenize("分布式事件");

        Assert.Equal(["分布", "布式", "式事", "事件"], terms);
    }

    /// <summary>
    /// 单个中文字符不产生词条，避免噪声
    /// </summary>
    [Fact]
    public void 单字中文被丢弃()
    {
        var terms = Tokenizer.Tokenize("包");

        Assert.Empty(terms);
    }

    /// <summary>
    /// 英文标识符既保留整词又按 PascalCase 拆词
    /// </summary>
    [Fact]
    public void 帕斯卡命名同时保留整词与拆词()
    {
        var terms = Tokenizer.Tokenize("ILocalEventBus");

        Assert.Contains("ilocaleventbus", terms);
        Assert.Contains("local", terms);
        Assert.Contains("event", terms);
        Assert.Contains("bus", terms);
    }

    /// <summary>
    /// 单字符英文词条被丢弃（如 ILocalEventBus 拆出的 I）
    /// </summary>
    [Fact]
    public void 单字符英文被丢弃()
    {
        var terms = Tokenizer.Tokenize("ILocalEventBus");

        Assert.DoesNotContain("i", terms);
    }

    /// <summary>
    /// 中英混排时两套规则各自生效
    /// </summary>
    [Fact]
    public void 中英混排各自切分()
    {
        var terms = Tokenizer.Tokenize("使用 EventBus 发布");

        Assert.Contains("使用", terms);
        Assert.Contains("eventbus", terms);
        Assert.Contains("发布", terms);
    }

    /// <summary>
    /// 空输入返回空集合而非抛出
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 空输入返回空集合(string? input)
    {
        Assert.Empty(Tokenizer.Tokenize(input));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~TokenizerTests"`

期望：编译失败，提示找不到类型 `Tokenizer`。

- [ ] **Step 3: 实现分词器**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Indexing/Tokenizer.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 中英混合分词器
/// </summary>
/// <remarks>
/// 中文没有空格分隔，故对连续中文字符切双字词（bigram）；英文与标识符按非字母数字边界切词，
/// 并额外按帕斯卡/驼峰命名拆分，使 <c>ILocalEventBus</c> 与「event bus」能命中同一段文本。
/// 长度不足 2 的词条一律丢弃，因为它们的区分度过低。
/// </remarks>
public static class Tokenizer
{
    /// <summary>
    /// 把文本切分为词条，可能包含重复项（重复本身携带词频信息）
    /// </summary>
    /// <param name="text">待切分的文本</param>
    /// <returns>词条列表，输入为空时返回空集合</returns>
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var terms = new List<string>();
        var index = 0;

        while (index < text.Length)
        {
            var current = text[index];

            if (IsCjk(current))
            {
                var start = index;
                while (index < text.Length && IsCjk(text[index]))
                {
                    index++;
                }

                AppendCjkBigrams(text.AsSpan(start, index - start), terms);
            }
            else if (char.IsLetterOrDigit(current))
            {
                var start = index;
                while (index < text.Length && char.IsLetterOrDigit(text[index]) && !IsCjk(text[index]))
                {
                    index++;
                }

                AppendAsciiTerms(text.AsSpan(start, index - start), terms);
            }
            else
            {
                index++;
            }
        }

        return terms;
    }

    /// <summary>
    /// 判断字符是否属于中日韩统一表意文字区段
    /// </summary>
    private static bool IsCjk(char value)
    {
        return value is >= '一' and <= '鿿';
    }

    /// <summary>
    /// 对一段连续中文追加双字词，单字段落直接丢弃
    /// </summary>
    private static void AppendCjkBigrams(ReadOnlySpan<char> span, List<string> terms)
    {
        for (var i = 0; i + 1 < span.Length; i++)
        {
            terms.Add(span.Slice(i, 2).ToString());
        }
    }

    /// <summary>
    /// 对一段英文或数字追加整词以及帕斯卡拆词结果
    /// </summary>
    private static void AppendAsciiTerms(ReadOnlySpan<char> span, List<string> terms)
    {
        if (span.Length >= 2)
        {
            terms.Add(span.ToString().ToLowerInvariant());
        }

        var start = 0;
        for (var i = 1; i <= span.Length; i++)
        {
            if (!IsWordBoundary(span, i))
            {
                continue;
            }

            var part = span[start..i];
            if (part.Length >= 2)
            {
                var lowered = part.ToString().ToLowerInvariant();
                if (!terms.Contains(lowered))
                {
                    terms.Add(lowered);
                }
            }

            start = i;
        }
    }

    /// <summary>
    /// 判断位置 <paramref name="i"/> 是否为帕斯卡/驼峰命名的词边界
    /// </summary>
    private static bool IsWordBoundary(ReadOnlySpan<char> span, int i)
    {
        if (i == span.Length)
        {
            return true;
        }

        // 小写后接大写：eventBus 在 B 处断开
        if (char.IsUpper(span[i]) && !char.IsUpper(span[i - 1]))
        {
            return true;
        }

        // 连续大写后接小写：HTTPServer 在 S 处断开
        return char.IsUpper(span[i]) && char.IsUpper(span[i - 1]) && i + 1 < span.Length && char.IsLower(span[i + 1]);
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~TokenizerTests"`

期望：7 个测试全部通过。

- [ ] **Step 5: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp/Indexing/Tokenizer.cs framework/test/XiHan.Framework.Docs.Mcp.Tests/TokenizerTests.cs
git commit -m "feat(docs-mcp): 新增中英混合分词器"
```

---

### Task 3: 来源分类与文档文件模型

**Files:**
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Sources/DocSourceKind.cs`
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Sources/DocFile.cs`
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Indexing/DocSection.cs`

**Interfaces:**
- Consumes: Task 1 的项目骨架
- Produces:
  - `enum DocSourceKind { Guide, Package, PackageReadme, Root }`
  - `sealed record DocFile(string AbsolutePath, string RelativePath, DocSourceKind Source, DateTime LastWriteUtc)`
  - `sealed record DocSection(string RelativePath, DocSourceKind Source, string DocumentTitle, string Heading, string TitlePath, string Content, int StartLine, int EndLine)`
  - Task 4、5、7、8、9 全部依赖这三个类型

**说明：** 这三个是纯数据类型，没有行为，因此不写单元测试——测试它们等于测试编译器。它们的正确性由后续任务的测试间接覆盖。

- [ ] **Step 1: 创建来源分类枚举**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Sources/DocSourceKind.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Sources;

/// <summary>
/// 文档来源分类
/// </summary>
public enum DocSourceKind
{
    /// <summary>
    /// 使用指南，任务导向：怎么选、易错点、最佳实践（docs/guide）
    /// </summary>
    Guide,

    /// <summary>
    /// 包文档，API 参考：配置项、工作原理、完整清单（docs/packages）
    /// </summary>
    Package,

    /// <summary>
    /// 包自带的 README，简洁说明，与代码同步度最高（framework/src/*/README.md）
    /// </summary>
    PackageReadme,

    /// <summary>
    /// 文档站根目录的全局文档，如快速开始与更新日志（docs/*.md）
    /// </summary>
    Root
}
```

- [ ] **Step 2: 创建文档文件模型**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Sources/DocFile.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Sources;

/// <summary>
/// 一个被索引的文档文件
/// </summary>
/// <param name="AbsolutePath">磁盘上的绝对路径</param>
/// <param name="RelativePath">相对仓库根的路径，统一使用正斜杠，对外展示用</param>
/// <param name="Source">来源分类</param>
/// <param name="LastWriteUtc">最后写入时间，用于热更新判定</param>
public sealed record DocFile(
    string AbsolutePath,
    string RelativePath,
    DocSourceKind Source,
    DateTime LastWriteUtc);
```

- [ ] **Step 3: 创建章节模型**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Indexing/DocSection.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 文档中的一个章节，检索与返回的最小单位
/// </summary>
/// <param name="RelativePath">所属文件相对仓库根的路径</param>
/// <param name="Source">来源分类</param>
/// <param name="DocumentTitle">所属文档的一级标题</param>
/// <param name="Heading">本章节的二级标题，前言章节为「概述」</param>
/// <param name="TitlePath">标题路径，形如「事件总线 &gt; 本地事件还是分布式事件」</param>
/// <param name="Content">章节正文原文，不含标题行</param>
/// <param name="StartLine">起始行号，从 1 开始</param>
/// <param name="EndLine">结束行号，从 1 开始，含此行</param>
public sealed record DocSection(
    string RelativePath,
    DocSourceKind Source,
    string DocumentTitle,
    string Heading,
    string TitlePath,
    string Content,
    int StartLine,
    int EndLine);
```

- [ ] **Step 4: 构建确认无误**

运行：`dotnet build framework/tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj -c Release`

期望：构建成功，无告警。

- [ ] **Step 5: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp/Sources framework/tool/XiHan.Framework.Docs.Mcp/Indexing/DocSection.cs
git commit -m "feat(docs-mcp): 新增来源分类与文档章节模型"
```

---

### Task 4: Markdown 章节切片器

**Files:**
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Indexing/MarkdownSectionSplitter.cs`
- Test: `framework/test/XiHan.Framework.Docs.Mcp.Tests/MarkdownSectionSplitterTests.cs`

**Interfaces:**
- Consumes: Task 3 的 `DocSection`、`DocSourceKind`
- Produces: `public static class MarkdownSectionSplitter`，方法
  `public static IReadOnlyList<DocSection> Split(string relativePath, DocSourceKind source, string markdown)`。
  Task 8（DocIndex）调用它。

**背景与关键坑：** 这批文档里到处是 ` ```bash ` 代码块中的 `# 注释`、C# 的 `#region` 与 `#pragma`。如果按行首 `#` 无脑切分，`dotnet add package` 那类代码块会被切成假章节。**跳过代码围栏内部的 `#` 是本任务唯一真正的难点。**

- [ ] **Step 1: 写失败的测试**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/MarkdownSectionSplitterTests.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// Markdown 章节切片器测试
/// </summary>
public class MarkdownSectionSplitterTests
{
    /// <summary>
    /// 按二级标题切分，标题路径拼接一级标题
    /// </summary>
    [Fact]
    public void 按二级标题切分()
    {
        const string Markdown = """
            # 事件总线

            前言内容。

            ## 本地事件

            本地事件正文。

            ## 分布式事件

            分布式事件正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/event-bus.md", DocSourceKind.Guide, Markdown);

        Assert.Equal(3, sections.Count);
        Assert.Equal("概述", sections[0].Heading);
        Assert.Equal("本地事件", sections[1].Heading);
        Assert.Equal("事件总线 > 本地事件", sections[1].TitlePath);
        Assert.Equal("分布式事件", sections[2].Heading);
    }

    /// <summary>
    /// 代码围栏内部的井号不得被当作标题
    /// </summary>
    [Fact]
    public void 代码围栏内的井号不切分()
    {
        const string Markdown = """
            # 安装

            ## 安装与启用

            ```bash
            # 安装这个包
            dotnet add package XiHan.Framework.EventBus
            ```

            ```csharp
            #region 注册
            services.AddXiHanEventBus();
            #endregion
            ```

            正文结束。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/packages/eventbus.md", DocSourceKind.Package, Markdown);

        Assert.Single(sections);
        Assert.Equal("安装与启用", sections[0].Heading);
        Assert.Contains("dotnet add package", sections[0].Content);
        Assert.Contains("#region", sections[0].Content);
    }

    /// <summary>
    /// 三级及更深标题并入所属二级章节，不单独成章
    /// </summary>
    [Fact]
    public void 三级标题并入二级章节()
    {
        const string Markdown = """
            # 事件总线

            ## 工作原理

            ### 发布路径

            发布路径正文。

            ### 订阅路径

            订阅路径正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/packages/eventbus.md", DocSourceKind.Package, Markdown);

        Assert.Single(sections);
        Assert.Equal("工作原理", sections[0].Heading);
        Assert.Contains("发布路径正文", sections[0].Content);
        Assert.Contains("订阅路径正文", sections[0].Content);
    }

    /// <summary>
    /// YAML frontmatter 不进入索引
    /// </summary>
    [Fact]
    public void 跳过前置元数据块()
    {
        const string Markdown = """
            ---
            layout: home
            title: 不该被索引
            ---

            # 真正的标题

            ## 章节

            正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/index.md", DocSourceKind.Root, Markdown);

        Assert.Equal("真正的标题", sections[0].DocumentTitle);
        Assert.DoesNotContain(sections, s => s.Content.Contains("layout: home"));
    }

    /// <summary>
    /// 一级标题之后、第一个二级标题之前的前言自成「概述」章节
    /// </summary>
    [Fact]
    public void 前言自成概述章节()
    {
        const string Markdown = """
            # 事件总线

            这是最精华的定位说明。

            ## 章节

            正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/event-bus.md", DocSourceKind.Guide, Markdown);

        Assert.Equal("概述", sections[0].Heading);
        Assert.Contains("最精华的定位说明", sections[0].Content);
        Assert.Equal("事件总线 > 概述", sections[0].TitlePath);
    }

    /// <summary>
    /// 没有前言时不产生空的概述章节
    /// </summary>
    [Fact]
    public void 无前言时不产生空概述()
    {
        const string Markdown = """
            # 标题

            ## 章节

            正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/x.md", DocSourceKind.Guide, Markdown);

        Assert.Single(sections);
        Assert.Equal("章节", sections[0].Heading);
    }

    /// <summary>
    /// 超长章节按空行二次切分，标题路径带分片序号
    /// </summary>
    [Fact]
    public void 超长章节二次切分()
    {
        var longParagraph = new string('文', 1500);
        var markdown = $"""
            # 标题

            ## 超长章节

            {longParagraph}

            {longParagraph}

            {longParagraph}
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/x.md", DocSourceKind.Guide, markdown);

        Assert.True(sections.Count > 1);
        Assert.All(sections, s => Assert.Equal("超长章节", s.Heading));
        Assert.Contains(sections, s => s.TitlePath.EndsWith("(1/2)") || s.TitlePath.EndsWith("(1/3)"));
    }

    /// <summary>
    /// 缺少一级标题时用文件名兜底
    /// </summary>
    [Fact]
    public void 缺少一级标题时用文件名兜底()
    {
        const string Markdown = """
            ## 只有二级标题

            正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/event-bus.md", DocSourceKind.Guide, Markdown);

        Assert.Equal("event-bus", sections[0].DocumentTitle);
    }

    /// <summary>
    /// 行号从 1 开始且指向标题行
    /// </summary>
    [Fact]
    public void 行号从一开始且指向标题行()
    {
        const string Markdown = """
            # 标题

            ## 第一节

            正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/x.md", DocSourceKind.Guide, Markdown);

        Assert.Equal(3, sections[0].StartLine);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~MarkdownSectionSplitterTests"`

期望：编译失败，提示找不到类型 `MarkdownSectionSplitter`。

- [ ] **Step 3: 实现切片器**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Indexing/MarkdownSectionSplitter.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 把一篇 Markdown 切分为若干章节
/// </summary>
/// <remarks>
/// 按一级与二级标题切分，三级及更深标题并入所属章节以避免切得过碎。
/// 关键约束：必须跳过代码围栏内部的井号——这批文档中大量存在 bash 的注释与 C# 的 #region，
/// 无脑按行首井号切分会把代码块切成假章节。
/// </remarks>
public static class MarkdownSectionSplitter
{
    /// <summary>
    /// 单个章节正文的字符数上限，超过则按空行二次切分
    /// </summary>
    private const int MaxSectionLength = 4000;

    /// <summary>
    /// 「概述」章节的固定标题
    /// </summary>
    private const string PreambleHeading = "概述";

    /// <summary>
    /// 切分 Markdown 文本
    /// </summary>
    /// <param name="relativePath">相对仓库根的路径</param>
    /// <param name="source">来源分类</param>
    /// <param name="markdown">Markdown 原文</param>
    /// <returns>章节列表，正文全为空白时返回空集合</returns>
    public static IReadOnlyList<DocSection> Split(string relativePath, DocSourceKind source, string markdown)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(markdown);

        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var startIndex = SkipFrontMatter(lines);
        var documentTitle = Path.GetFileNameWithoutExtension(relativePath);

        var blocks = new List<(string Heading, int StartLine, List<string> Body)>();
        var currentHeading = PreambleHeading;
        var currentStartLine = startIndex + 1;
        var currentBody = new List<string>();
        var fence = string.Empty;

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (fence.Length > 0)
            {
                // 处于代码围栏内：只判断是否闭合，绝不识别标题
                if (trimmed.StartsWith(fence, StringComparison.Ordinal))
                {
                    fence = string.Empty;
                }

                currentBody.Add(line);
                continue;
            }

            var opening = ReadFenceMarker(trimmed);
            if (opening.Length > 0)
            {
                fence = opening;
                currentBody.Add(line);
                continue;
            }

            if (TryReadHeading(trimmed, out var level, out var heading))
            {
                if (level == 1)
                {
                    documentTitle = heading;
                    continue;
                }

                blocks.Add((currentHeading, currentStartLine, currentBody));
                currentHeading = heading;
                currentStartLine = i + 1;
                currentBody = [];
                continue;
            }

            currentBody.Add(line);
        }

        blocks.Add((currentHeading, currentStartLine, currentBody));

        var sections = new List<DocSection>();
        foreach (var block in blocks)
        {
            var content = string.Join("\n", block.Body).Trim();
            if (content.Length == 0)
            {
                continue;
            }

            AppendSections(sections, relativePath, source, documentTitle, block.Heading, content, block.StartLine, block.Body.Count);
        }

        return sections;
    }

    /// <summary>
    /// 跳过文件开头的 YAML 前置元数据块，返回正文起始行下标
    /// </summary>
    private static int SkipFrontMatter(string[] lines)
    {
        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            return 0;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// 读取代码围栏起始标记，非围栏行返回空串
    /// </summary>
    private static string ReadFenceMarker(string trimmed)
    {
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return "```";
        }

        return trimmed.StartsWith("~~~", StringComparison.Ordinal) ? "~~~" : string.Empty;
    }

    /// <summary>
    /// 尝试把一行识别为一级或二级标题
    /// </summary>
    private static bool TryReadHeading(string trimmed, out int level, out string heading)
    {
        level = 0;
        heading = string.Empty;

        var hashCount = 0;
        while (hashCount < trimmed.Length && trimmed[hashCount] == '#')
        {
            hashCount++;
        }

        // 三级及更深标题并入所属章节，不作为切分点
        if (hashCount is not (1 or 2))
        {
            return false;
        }

        if (hashCount >= trimmed.Length || trimmed[hashCount] != ' ')
        {
            return false;
        }

        level = hashCount;
        heading = trimmed[(hashCount + 1)..].Trim();
        return heading.Length > 0;
    }

    /// <summary>
    /// 把一个章节正文追加进结果，超长时按空行二次切分
    /// </summary>
    private static void AppendSections(
        List<DocSection> sections,
        string relativePath,
        DocSourceKind source,
        string documentTitle,
        string heading,
        string content,
        int startLine,
        int lineCount)
    {
        var basePath = $"{documentTitle} > {heading}";
        var endLine = startLine + lineCount;

        if (content.Length <= MaxSectionLength)
        {
            sections.Add(new DocSection(relativePath, source, documentTitle, heading, basePath, content, startLine, endLine));
            return;
        }

        var chunks = SplitByBlankLine(content);
        for (var i = 0; i < chunks.Count; i++)
        {
            var titlePath = $"{basePath} ({i + 1}/{chunks.Count})";
            sections.Add(new DocSection(relativePath, source, documentTitle, heading, titlePath, chunks[i], startLine, endLine));
        }
    }

    /// <summary>
    /// 按空行把超长正文聚合成不超过上限的若干片段
    /// </summary>
    private static List<string> SplitByBlankLine(string content)
    {
        var paragraphs = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var builder = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (builder.Length > 0 && builder.Length + paragraph.Length > MaxSectionLength)
            {
                chunks.Add(builder.ToString().Trim());
                builder.Clear();
            }

            builder.Append(paragraph).Append("\n\n");
        }

        if (builder.Length > 0)
        {
            chunks.Add(builder.ToString().Trim());
        }

        return chunks.Count > 0 ? chunks : [content];
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~MarkdownSectionSplitterTests"`

期望：9 个测试全部通过。若「超长章节二次切分」失败，检查 `MaxSectionLength` 与测试中构造的文本长度是否匹配（1500 字 × 3 段 = 4500 字，应切成 2 片）。

- [ ] **Step 5: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp/Indexing/MarkdownSectionSplitter.cs framework/test/XiHan.Framework.Docs.Mcp.Tests/MarkdownSectionSplitterTests.cs
git commit -m "feat(docs-mcp): 新增 Markdown 章节切片器"
```

---

### Task 5: 仓库根定位与文档来源枚举

**Files:**
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Sources/DocSourceLocator.cs`
- Test: `framework/test/XiHan.Framework.Docs.Mcp.Tests/DocSourceLocatorTests.cs`

**Interfaces:**
- Consumes: Task 3 的 `DocFile`、`DocSourceKind`
- Produces:
  - `sealed class DocsRootNotFoundException : Exception`
  - `sealed class DocSourceLocator(string repositoryRoot)`
  - `static string DocSourceLocator.ResolveRepositoryRoot(string startDirectory, string? environmentOverride)`
  - `IReadOnlyList<DocFile> Enumerate()`
  - `string RepositoryRoot { get; }`
  - `bool TryResolveDocumentPath(string relativePath, out string absolutePath)` — Task 9 的 `read_doc` 用它做路径逃逸校验
  - Task 8（DocIndex）与 Task 9（工具层）依赖它

- [ ] **Step 1: 写失败的测试**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/DocSourceLocatorTests.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 仓库根定位与来源枚举测试
/// </summary>
public class DocSourceLocatorTests : IDisposable
{
    private readonly string _root;

    /// <summary>
    /// 构造一个最小的仓库结构用于测试
    /// </summary>
    public DocSourceLocatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xihan-docs-mcp-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(_root, "docs", "guide"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "packages"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", ".vitepress"));
        Directory.CreateDirectory(Path.Combine(_root, "framework", "src", "XiHan.Framework.Utils"));
        Directory.CreateDirectory(Path.Combine(_root, "framework", "src", "XiHan.Framework.Core", "obj"));

        File.WriteAllText(Path.Combine(_root, "docs", "guide", "event-bus.md"), "# 事件总线");
        File.WriteAllText(Path.Combine(_root, "docs", "packages", "eventbus.md"), "# EventBus");
        File.WriteAllText(Path.Combine(_root, "docs", "quickstart.md"), "# 快速开始");
        File.WriteAllText(Path.Combine(_root, "docs", ".vitepress", "config.md"), "# 不该被索引");
        File.WriteAllText(Path.Combine(_root, "framework", "src", "XiHan.Framework.Utils", "README.md"), "# Utils");
        File.WriteAllText(Path.Combine(_root, "framework", "src", "XiHan.Framework.Core", "obj", "README.md"), "# 不该被索引");
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 从深层子目录逐层向上找到仓库根
    /// </summary>
    [Fact]
    public void 逐层向上找到仓库根()
    {
        var deep = Path.Combine(_root, "framework", "src", "XiHan.Framework.Utils");

        var resolved = DocSourceLocator.ResolveRepositoryRoot(deep, environmentOverride: null);

        Assert.Equal(Path.GetFullPath(_root), Path.GetFullPath(resolved));
    }

    /// <summary>
    /// 环境变量指定的路径优先于向上查找
    /// </summary>
    [Fact]
    public void 环境变量优先()
    {
        var resolved = DocSourceLocator.ResolveRepositoryRoot(Path.GetTempPath(), _root);

        Assert.Equal(Path.GetFullPath(_root), Path.GetFullPath(resolved));
    }

    /// <summary>
    /// 找不到仓库根时抛出专用异常，而不是静默返回空索引
    /// </summary>
    [Fact]
    public void 找不到仓库根时抛出()
    {
        var isolated = Path.Combine(Path.GetTempPath(), "xihan-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);

        try
        {
            Assert.Throws<DocsRootNotFoundException>(
                () => DocSourceLocator.ResolveRepositoryRoot(isolated, environmentOverride: null));
        }
        finally
        {
            Directory.Delete(isolated, recursive: true);
        }
    }

    /// <summary>
    /// 四类来源各自被正确分类
    /// </summary>
    [Fact]
    public void 四类来源分类正确()
    {
        var files = new DocSourceLocator(_root).Enumerate();

        Assert.Contains(files, f => f.RelativePath == "docs/guide/event-bus.md" && f.Source == DocSourceKind.Guide);
        Assert.Contains(files, f => f.RelativePath == "docs/packages/eventbus.md" && f.Source == DocSourceKind.Package);
        Assert.Contains(files, f => f.RelativePath == "docs/quickstart.md" && f.Source == DocSourceKind.Root);
        Assert.Contains(files, f => f.RelativePath == "framework/src/XiHan.Framework.Utils/README.md" && f.Source == DocSourceKind.PackageReadme);
    }

    /// <summary>
    /// docs 下只取 guide 与 packages 两个子目录，其余子目录不递归
    /// </summary>
    [Fact]
    public void 不递归扫描其他文档子目录()
    {
        var files = new DocSourceLocator(_root).Enumerate();

        Assert.DoesNotContain(files, f => f.RelativePath.Contains(".vitepress"));
    }

    /// <summary>
    /// 中间产物目录下的 README 不被索引
    /// </summary>
    [Fact]
    public void 跳过中间产物目录()
    {
        var files = new DocSourceLocator(_root).Enumerate();

        Assert.DoesNotContain(files, f => f.RelativePath.Contains("/obj/"));
    }

    /// <summary>
    /// 相对路径统一使用正斜杠，保证跨平台输出一致
    /// </summary>
    [Fact]
    public void 相对路径统一正斜杠()
    {
        var files = new DocSourceLocator(_root).Enumerate();

        Assert.All(files, f => Assert.DoesNotContain('\\', f.RelativePath));
    }

    /// <summary>
    /// 逃逸仓库根的路径被拒绝
    /// </summary>
    [Fact]
    public void 拒绝逃逸仓库根的路径()
    {
        var locator = new DocSourceLocator(_root);

        Assert.False(locator.TryResolveDocumentPath("../../etc/passwd", out _));
    }

    /// <summary>
    /// 仓库根内的合法路径可以解析
    /// </summary>
    [Fact]
    public void 接受仓库根内的合法路径()
    {
        var locator = new DocSourceLocator(_root);

        Assert.True(locator.TryResolveDocumentPath("docs/guide/event-bus.md", out var absolute));
        Assert.True(File.Exists(absolute));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~DocSourceLocatorTests"`

期望：编译失败，提示找不到类型 `DocSourceLocator`。

- [ ] **Step 3: 实现定位器**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Sources/DocSourceLocator.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Sources;

/// <summary>
/// 找不到仓库根时抛出
/// </summary>
/// <remarks>
/// 刻意让进程启动失败，而不是带着空索引进入服务状态——
/// 一个「连得上但永远搜不到东西」的服务端比一个起不来的更难排查。
/// </remarks>
public sealed class DocsRootNotFoundException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">错误信息</param>
    public DocsRootNotFoundException(string message) : base(message)
    {
    }
}

/// <summary>
/// 定位仓库根并枚举四类文档来源
/// </summary>
/// <param name="repositoryRoot">仓库根的绝对路径</param>
public sealed class DocSourceLocator(string repositoryRoot)
{
    /// <summary>
    /// 仓库根的绝对路径
    /// </summary>
    public string RepositoryRoot { get; } = Path.GetFullPath(repositoryRoot);

    /// <summary>
    /// 解析仓库根：环境变量优先，否则从指定目录逐层向上查找
    /// </summary>
    /// <param name="startDirectory">向上查找的起点目录</param>
    /// <param name="environmentOverride">环境变量 XIHAN_DOCS_ROOT 的值，可为空</param>
    /// <returns>仓库根的绝对路径</returns>
    /// <exception cref="DocsRootNotFoundException">无法定位仓库根时抛出</exception>
    public static string ResolveRepositoryRoot(string startDirectory, string? environmentOverride)
    {
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            if (IsRepositoryRoot(environmentOverride))
            {
                return Path.GetFullPath(environmentOverride);
            }

            throw new DocsRootNotFoundException(
                $"环境变量 XIHAN_DOCS_ROOT 指向 '{environmentOverride}'，但该目录下缺少 docs 或 framework 子目录。");
        }

        var attempted = new List<string>();
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (current is not null)
        {
            attempted.Add(current.FullName);
            if (IsRepositoryRoot(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DocsRootNotFoundException(
            $"""
             无法定位曦寒框架仓库根：从 '{startDirectory}' 逐层向上均未找到同时包含 docs 与 framework 子目录的路径。
             已尝试：{string.Join(" -> ", attempted)}
             请设置环境变量 XIHAN_DOCS_ROOT 指向仓库根目录后重试。
             """);
    }

    /// <summary>
    /// 枚举全部待索引文档
    /// </summary>
    /// <returns>按相对路径排序的文档文件列表</returns>
    public IReadOnlyList<DocFile> Enumerate()
    {
        var files = new List<DocFile>();

        CollectDirectory(files, Path.Combine(RepositoryRoot, "docs", "guide"), DocSourceKind.Guide);
        CollectDirectory(files, Path.Combine(RepositoryRoot, "docs", "packages"), DocSourceKind.Package);
        CollectDirectory(files, Path.Combine(RepositoryRoot, "docs"), DocSourceKind.Root);
        CollectPackageReadmes(files);

        return [.. files.OrderBy(f => f.RelativePath, StringComparer.Ordinal)];
    }

    /// <summary>
    /// 把相对路径解析为绝对路径，并校验其未逃逸仓库根
    /// </summary>
    /// <param name="relativePath">相对仓库根的路径</param>
    /// <param name="absolutePath">解析出的绝对路径</param>
    /// <returns>路径合法且位于仓库根内时为 true</returns>
    public bool TryResolveDocumentPath(string relativePath, out string absolutePath)
    {
        absolutePath = string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        var combined = Path.GetFullPath(Path.Combine(RepositoryRoot, relativePath));
        var prefix = RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        absolutePath = combined;
        return true;
    }

    /// <summary>
    /// 判断目录是否同时包含 docs 与 framework 子目录
    /// </summary>
    private static bool IsRepositoryRoot(string directory)
    {
        return Directory.Exists(Path.Combine(directory, "docs"))
            && Directory.Exists(Path.Combine(directory, "framework"));
    }

    /// <summary>
    /// 收集一个目录下的 Markdown 文件，仅顶层不递归
    /// </summary>
    private void CollectDirectory(List<DocFile> files, string directory, DocSourceKind source)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
        {
            files.Add(CreateDocFile(path, source));
        }
    }

    /// <summary>
    /// 收集各包目录下的 README，仅取包目录的直接子文件，天然排除 obj 与 bin
    /// </summary>
    private void CollectPackageReadmes(List<DocFile> files)
    {
        var srcRoot = Path.Combine(RepositoryRoot, "framework", "src");
        if (!Directory.Exists(srcRoot))
        {
            return;
        }

        foreach (var packageDirectory in Directory.EnumerateDirectories(srcRoot))
        {
            var readme = Path.Combine(packageDirectory, "README.md");
            if (File.Exists(readme))
            {
                files.Add(CreateDocFile(readme, DocSourceKind.PackageReadme));
            }
        }
    }

    /// <summary>
    /// 由绝对路径构造文档文件描述
    /// </summary>
    private DocFile CreateDocFile(string absolutePath, DocSourceKind source)
    {
        var relative = Path.GetRelativePath(RepositoryRoot, absolutePath).Replace('\\', '/');
        return new DocFile(absolutePath, relative, source, File.GetLastWriteTimeUtc(absolutePath));
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~DocSourceLocatorTests"`

期望：9 个测试全部通过。

- [ ] **Step 5: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp/Sources/DocSourceLocator.cs framework/test/XiHan.Framework.Docs.Mcp.Tests/DocSourceLocatorTests.cs
git commit -m "feat(docs-mcp): 新增仓库根定位与文档来源枚举"
```

---

### Task 6: 倒排索引

**Files:**
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Indexing/BigramIndex.cs`
- Test: `framework/test/XiHan.Framework.Docs.Mcp.Tests/BigramIndexTests.cs`

**Interfaces:**
- Consumes: Task 2 的 `Tokenizer`
- Produces:
  - `readonly record struct Posting(int SectionId, bool InTitle)`
  - `sealed class BigramIndex`，方法 `void Add(int sectionId, string title, string body)` 与 `IReadOnlyList<Posting> Find(string term)`
  - Task 7（SectionScorer）与 Task 8（DocIndex）依赖它

**说明：** 同一章节内同一词条只保留一条 posting，标题命中优先。这样 Task 7 的「命中数 ÷ 查询词总数」才是真正的覆盖率，不会被词频扭曲。

- [ ] **Step 1: 写失败的测试**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/BigramIndexTests.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 倒排索引测试
/// </summary>
public class BigramIndexTests
{
    /// <summary>
    /// 正文中的词条可以被检索到
    /// </summary>
    [Fact]
    public void 正文词条可检索()
    {
        var index = new BigramIndex();
        index.Add(0, "标题", "分布式事件");

        var postings = index.Find("事件");

        Assert.Single(postings);
        Assert.Equal(0, postings[0].SectionId);
    }

    /// <summary>
    /// 标题中的词条带有标题标记
    /// </summary>
    [Fact]
    public void 标题词条带标记()
    {
        var index = new BigramIndex();
        index.Add(0, "分布式事件", "无关正文");

        var postings = index.Find("事件");

        Assert.True(postings[0].InTitle);
    }

    /// <summary>
    /// 同一章节内同词条只保留一条，标题命中优先
    /// </summary>
    [Fact]
    public void 同章节同词条去重且标题优先()
    {
        var index = new BigramIndex();
        index.Add(0, "分布式事件", "分布式事件分布式事件");

        var postings = index.Find("事件");

        Assert.Single(postings);
        Assert.True(postings[0].InTitle);
    }

    /// <summary>
    /// 同一词条出现在不同章节时各保留一条
    /// </summary>
    [Fact]
    public void 跨章节各保留一条()
    {
        var index = new BigramIndex();
        index.Add(0, "甲", "分布式事件");
        index.Add(1, "乙", "分布式事件");

        var postings = index.Find("事件");

        Assert.Equal(2, postings.Count);
    }

    /// <summary>
    /// 未收录的词条返回空集合而非抛出
    /// </summary>
    [Fact]
    public void 未收录词条返回空集合()
    {
        var index = new BigramIndex();
        index.Add(0, "标题", "正文");

        Assert.Empty(index.Find("不存在的词"));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~BigramIndexTests"`

期望：编译失败，提示找不到类型 `BigramIndex`。

- [ ] **Step 3: 实现倒排索引**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Indexing/BigramIndex.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 倒排索引中的一条记录
/// </summary>
/// <param name="SectionId">章节在章节列表中的下标</param>
/// <param name="InTitle">该词条是否出现在章节标题中</param>
public readonly record struct Posting(int SectionId, bool InTitle);

/// <summary>
/// 词条到章节的倒排索引
/// </summary>
/// <remarks>
/// 同一章节内同一词条只保留一条记录，标题命中优先。
/// 这样打分时的「命中词数 ÷ 查询词总数」才是真正的覆盖率，不会被词频扭曲。
/// </remarks>
public sealed class BigramIndex
{
    private readonly Dictionary<string, List<Posting>> _postings = new(StringComparer.Ordinal);

    /// <summary>
    /// 把一个章节的标题与正文加入索引
    /// </summary>
    /// <param name="sectionId">章节下标</param>
    /// <param name="title">章节标题路径</param>
    /// <param name="body">章节正文</param>
    public void Add(int sectionId, string title, string body)
    {
        var titleTerms = Tokenizer.Tokenize(title).ToHashSet(StringComparer.Ordinal);

        foreach (var term in titleTerms)
        {
            AddPosting(term, new Posting(sectionId, InTitle: true));
        }

        foreach (var term in Tokenizer.Tokenize(body).ToHashSet(StringComparer.Ordinal))
        {
            if (!titleTerms.Contains(term))
            {
                AddPosting(term, new Posting(sectionId, InTitle: false));
            }
        }
    }

    /// <summary>
    /// 查找一个词条对应的全部章节
    /// </summary>
    /// <param name="term">词条</param>
    /// <returns>记录列表，未收录时为空集合</returns>
    public IReadOnlyList<Posting> Find(string term)
    {
        return _postings.TryGetValue(term, out var list) ? list : [];
    }

    /// <summary>
    /// 追加一条记录
    /// </summary>
    private void AddPosting(string term, Posting posting)
    {
        if (!_postings.TryGetValue(term, out var list))
        {
            list = [];
            _postings[term] = list;
        }

        list.Add(posting);
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~BigramIndexTests"`

期望：5 个测试全部通过。

- [ ] **Step 5: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp/Indexing/BigramIndex.cs framework/test/XiHan.Framework.Docs.Mcp.Tests/BigramIndexTests.cs
git commit -m "feat(docs-mcp): 新增词条倒排索引"
```

---

### Task 7: 术语同义词扩展

**Files:**
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Search/WeightedTerm.cs`
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Search/SynonymExpander.cs`
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Resources/synonyms.json`
- Test: `framework/test/XiHan.Framework.Docs.Mcp.Tests/SynonymExpanderTests.cs`

**Interfaces:**
- Consumes: Task 2 的 `Tokenizer`
- Produces:
  - `readonly record struct WeightedTerm(string Term, double Weight)`
  - `sealed class SynonymExpander`，静态工厂 `static SynonymExpander Load(string? jsonPath, ILogger logger)`，方法 `IReadOnlyList<WeightedTerm> Expand(string query)`
  - Task 8（SectionScorer 的调用方）与 Task 9（工具层）依赖它

**背景：** 纯字面匹配的唯一弱点是「换句话说」——问「怎么避免重复消费」不会命中「收件箱去重」，因为字面零重叠。术语表用几十行 JSON 精准补掉这个缺口。扩展词权重折半，避免淹没用户的原始意图。

- [ ] **Step 1: 创建术语表**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Resources/synonyms.json`：

```json
[
  ["重复消费", "去重", "幂等", "Inbox", "收件箱"],
  ["可靠投递", "Outbox", "发件箱", "最终一致"],
  ["事务", "工作单元", "UoW", "UnitOfWork", "提交", "回滚"],
  ["拦截器", "过滤器", "Filter", "AOP", "动态代理", "Castle"],
  ["依赖注入", "DI", "容器", "服务注册", "ServiceCollection"],
  ["模块", "Module", "DependsOn", "生命周期", "装配"],
  ["动态接口", "动态API", "DynamicApi", "应用服务", "控制器"],
  ["多租户", "MultiTenancy", "租户隔离", "TenantId"],
  ["缓存", "Cache", "分布式缓存", "过期", "失效"],
  ["权限", "授权", "Authorization", "策略", "Policy"],
  ["认证", "登录", "Authentication", "令牌", "Token", "JWT"],
  ["审计", "Auditing", "操作日志", "变更记录"],
  ["雪花ID", "分布式ID", "Snowflake", "主键生成"],
  ["对象映射", "ObjectMapping", "DTO转换", "Mapper"],
  ["本地化", "国际化", "多语言", "Localization", "i18n"],
  ["虚拟文件", "嵌入资源", "VirtualFileSystem", "内嵌文件"],
  ["后台任务", "定时任务", "调度", "Tasks", "Cron"],
  ["全文检索", "搜索引擎", "Elasticsearch", "SearchEngines"],
  ["消息队列", "Broker", "Kafka", "RabbitMQ", "中间件"],
  ["可观测", "监控", "指标", "链路追踪", "Observability", "OpenTelemetry"],
  ["限流", "熔断", "Traffic", "流量控制"],
  ["对象存储", "文件上传", "ObjectStorage", "OSS"],
  ["校验", "验证", "Validation", "数据注解"],
  ["配置", "选项", "Options", "SectionName", "appsettings"],
  ["实时通信", "Realtime", "SignalR", "WebSocket", "推送"],
  ["工作流", "Workflow", "流程引擎", "审批"],
  ["机器人", "Bot", "通知渠道", "告警推送"],
  ["技能", "Skill", "AIFunction", "函数调用", "工具调用"]
]
```

- [ ] **Step 2: 写失败的测试**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/SynonymExpanderTests.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Docs.Mcp.Search;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 术语同义词扩展测试
/// </summary>
public class SynonymExpanderTests
{
    /// <summary>
    /// 原始查询词权重为 1
    /// </summary>
    [Fact]
    public void 原始查询词满权重()
    {
        var expander = CreateExpander("""[["重复消费", "去重", "收件箱"]]""");

        var terms = expander.Expand("分布式事件");

        Assert.All(terms, t => Assert.Equal(1.0, t.Weight));
    }

    /// <summary>
    /// 命中术语组时把同组其余术语按半权加入
    /// </summary>
    [Fact]
    public void 命中术语组时半权扩展()
    {
        var expander = CreateExpander("""[["重复消费", "去重", "收件箱"]]""");

        var terms = expander.Expand("怎么避免重复消费");

        Assert.Contains(terms, t => t.Term == "收件" && t.Weight == 0.5);
        Assert.Contains(terms, t => t.Term == "去重" && t.Weight == 0.5);
    }

    /// <summary>
    /// 同一词条同时来自原始查询与扩展时保留高权重
    /// </summary>
    [Fact]
    public void 同词条保留高权重()
    {
        var expander = CreateExpander("""[["重复消费", "重复提交"]]""");

        var terms = expander.Expand("重复消费");

        Assert.Equal(1.0, terms.Single(t => t.Term == "重复").Weight);
    }

    /// <summary>
    /// 术语表文件缺失时降级为不扩展，服务照常
    /// </summary>
    [Fact]
    public void 术语表缺失时降级()
    {
        var expander = SynonymExpander.Load(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"),
            NullLogger.Instance);

        var terms = expander.Expand("重复消费");

        Assert.NotEmpty(terms);
        Assert.All(terms, t => Assert.Equal(1.0, t.Weight));
    }

    /// <summary>
    /// 术语表格式损坏时降级为不扩展，服务照常
    /// </summary>
    [Fact]
    public void 术语表损坏时降级()
    {
        var expander = CreateExpander("{ 这不是合法的 JSON 数组");

        var terms = expander.Expand("重复消费");

        Assert.NotEmpty(terms);
        Assert.All(terms, t => Assert.Equal(1.0, t.Weight));
    }

    /// <summary>
    /// 用指定内容写一个临时术语表并加载
    /// </summary>
    private static SynonymExpander CreateExpander(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);

        try
        {
            return SynonymExpander.Load(path, NullLogger.Instance);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
```

- [ ] **Step 3: 运行测试确认失败**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~SynonymExpanderTests"`

期望：编译失败，提示找不到类型 `SynonymExpander`。

- [ ] **Step 4: 实现带权词条**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Search/WeightedTerm.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Search;

/// <summary>
/// 带权重的查询词条
/// </summary>
/// <param name="Term">词条</param>
/// <param name="Weight">权重，用户原始查询词为 1.0，同义词扩展出的为 0.5</param>
public readonly record struct WeightedTerm(string Term, double Weight);
```

- [ ] **Step 5: 实现同义词扩展器**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Search/SynonymExpander.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Docs.Mcp.Indexing;

namespace XiHan.Framework.Docs.Mcp.Search;

/// <summary>
/// 按框架术语表扩展查询词
/// </summary>
/// <remarks>
/// 纯字面匹配无法处理「换句话说」的提问，例如问「怎么避免重复消费」与文档中的「收件箱去重」字面零重叠。
/// 术语表用几十行 JSON 精准补掉这个缺口。扩展词权重折半，避免淹没用户的原始意图。
/// 术语表是增强而非必需：文件缺失或格式损坏时降级为不扩展，服务照常。
/// </remarks>
public sealed class SynonymExpander
{
    /// <summary>
    /// 同义词扩展出的词条权重
    /// </summary>
    private const double ExpandedWeight = 0.5;

    private readonly IReadOnlyList<string[]> _groups;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="groups">等价术语分组</param>
    private SynonymExpander(IReadOnlyList<string[]> groups)
    {
        _groups = groups;
    }

    /// <summary>
    /// 从术语表文件加载，失败时返回一个不做扩展的实例
    /// </summary>
    /// <param name="jsonPath">术语表路径，可为空</param>
    /// <param name="logger">日志记录器，警告写入 stderr</param>
    /// <returns>扩展器实例，永不为空</returns>
    public static SynonymExpander Load(string? jsonPath, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
        {
            logger.LogWarning("未找到术语表 {Path}，同义词扩展已禁用，检索仍可正常工作。", jsonPath);
            return new SynonymExpander([]);
        }

        try
        {
            var groups = JsonSerializer.Deserialize<string[][]>(File.ReadAllText(jsonPath));
            return new SynonymExpander(groups is null ? [] : [.. groups]);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "术语表 {Path} 解析失败，同义词扩展已禁用，检索仍可正常工作。", jsonPath);
            return new SynonymExpander([]);
        }
    }

    /// <summary>
    /// 把查询串扩展为带权词条集合
    /// </summary>
    /// <param name="query">用户查询串</param>
    /// <returns>去重后的带权词条，同一词条保留最高权重</returns>
    public IReadOnlyList<WeightedTerm> Expand(string query)
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var term in Tokenizer.Tokenize(query))
        {
            weights[term] = 1.0;
        }

        foreach (var group in _groups)
        {
            var matched = group.Any(member => query.Contains(member, StringComparison.OrdinalIgnoreCase));
            if (!matched)
            {
                continue;
            }

            foreach (var member in group)
            {
                if (query.Contains(member, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var term in Tokenizer.Tokenize(member))
                {
                    if (!weights.ContainsKey(term))
                    {
                        weights[term] = ExpandedWeight;
                    }
                }
            }
        }

        return [.. weights.Select(pair => new WeightedTerm(pair.Key, pair.Value))];
    }
}
```

- [ ] **Step 6: 运行测试确认通过**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~SynonymExpanderTests"`

期望：5 个测试全部通过。

- [ ] **Step 7: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp/Search framework/tool/XiHan.Framework.Docs.Mcp/Resources framework/test/XiHan.Framework.Docs.Mcp.Tests/SynonymExpanderTests.cs
git commit -m "feat(docs-mcp): 新增框架术语同义词扩展"
```

---

### Task 8: 检索排序

**Files:**
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Options/DocsMcpOptions.cs`
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Search/SearchHit.cs`
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Search/SectionScorer.cs`
- Test: `framework/test/XiHan.Framework.Docs.Mcp.Tests/SectionScorerTests.cs`

**Interfaces:**
- Consumes: Task 3 的 `DocSection`、Task 6 的 `BigramIndex` 与 `Posting`、Task 7 的 `WeightedTerm`
- Produces:
  - `sealed class DocsMcpOptions`，属性 `TitleBoost`（默认 3.0）、`MaxSectionsPerFile`（默认 2）、`DefaultLimit`（默认 5）、`MaxLimit`（默认 15）、`RefreshThrottle`（默认 2 秒）、`SourceWeights`（`IReadOnlyDictionary<DocSourceKind, double>`）
  - `sealed record SearchHit(DocSection Section, double Score)`
  - `sealed class SectionScorer(DocsMcpOptions options)`，方法
    `IReadOnlyList<SearchHit> Rank(IReadOnlyList<WeightedTerm> queryTerms, IReadOnlyList<DocSection> sections, BigramIndex index, DocSourceKind? sourceFilter, int limit)`
  - Task 9（工具层）依赖它

**打分规则：** 命中权重累加（标题命中乘以 `TitleBoost`）÷ 查询词总权重 × 来源权重。覆盖率优先，防止长章节靠字数多取胜。

- [ ] **Step 1: 写失败的测试**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/SectionScorerTests.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 检索排序测试
/// </summary>
public class SectionScorerTests
{
    /// <summary>
    /// 覆盖率高的短章节应胜过命中数相同但查询覆盖不全的场景
    /// </summary>
    [Fact]
    public void 覆盖率优先于篇幅()
    {
        var sections = new List<DocSection>
        {
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "甲", "分布式事件发布"),
            CreateSection("docs/guide/b.md", DocSourceKind.Guide, "乙", "分布式" + new string('文', 3000))
        };

        var hits = Rank(sections, "分布式事件发布");

        Assert.Equal("docs/guide/a.md", hits[0].Section.RelativePath);
    }

    /// <summary>
    /// 标题命中的章节排在正文命中之前
    /// </summary>
    [Fact]
    public void 标题命中优先()
    {
        var sections = new List<DocSection>
        {
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "无关标题", "分布式事件"),
            CreateSection("docs/guide/b.md", DocSourceKind.Guide, "分布式事件", "无关正文")
        };

        var hits = Rank(sections, "分布式事件");

        Assert.Equal("docs/guide/b.md", hits[0].Section.RelativePath);
    }

    /// <summary>
    /// 同一文件最多返回两个章节，避免一篇文章洗版
    /// </summary>
    [Fact]
    public void 同文件最多两个章节()
    {
        var sections = new List<DocSection>
        {
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "章节一", "分布式事件"),
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "章节二", "分布式事件"),
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "章节三", "分布式事件"),
            CreateSection("docs/guide/b.md", DocSourceKind.Guide, "章节四", "分布式事件")
        };

        var hits = Rank(sections, "分布式事件");

        Assert.Equal(2, hits.Count(h => h.Section.RelativePath == "docs/guide/a.md"));
    }

    /// <summary>
    /// 指南来源的权重高于包 README
    /// </summary>
    [Fact]
    public void 指南来源权重更高()
    {
        var sections = new List<DocSection>
        {
            CreateSection("framework/src/X/README.md", DocSourceKind.PackageReadme, "标题", "分布式事件"),
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "标题", "分布式事件")
        };

        var hits = Rank(sections, "分布式事件");

        Assert.Equal(DocSourceKind.Guide, hits[0].Section.Source);
    }

    /// <summary>
    /// 来源过滤只返回指定分类
    /// </summary>
    [Fact]
    public void 来源过滤生效()
    {
        var sections = new List<DocSection>
        {
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "标题", "分布式事件"),
            CreateSection("docs/packages/b.md", DocSourceKind.Package, "标题", "分布式事件")
        };

        var hits = Rank(sections, "分布式事件", DocSourceKind.Package);

        Assert.Single(hits);
        Assert.Equal(DocSourceKind.Package, hits[0].Section.Source);
    }

    /// <summary>
    /// 零命中返回空集合
    /// </summary>
    [Fact]
    public void 零命中返回空集合()
    {
        var sections = new List<DocSection>
        {
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "标题", "正文")
        };

        Assert.Empty(Rank(sections, "完全不相干的查询内容"));
    }

    /// <summary>
    /// 用给定章节集合执行一次排序
    /// </summary>
    private static IReadOnlyList<SearchHit> Rank(List<DocSection> sections, string query, DocSourceKind? filter = null)
    {
        var index = new BigramIndex();
        for (var i = 0; i < sections.Count; i++)
        {
            index.Add(i, sections[i].TitlePath, sections[i].Content);
        }

        var options = new DocsMcpOptions();
        var terms = Tokenizer.Tokenize(query).Distinct().Select(t => new WeightedTerm(t, 1.0)).ToList();

        return new SectionScorer(options).Rank(terms, sections, index, filter, options.DefaultLimit);
    }

    /// <summary>
    /// 构造一个测试用章节
    /// </summary>
    private static DocSection CreateSection(string path, DocSourceKind source, string heading, string content)
    {
        return new DocSection(path, source, "文档", heading, $"文档 > {heading}", content, 1, 10);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~SectionScorerTests"`

期望：编译失败，提示找不到类型 `DocsMcpOptions`、`SectionScorer`。

- [ ] **Step 3: 实现配置项**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Options/DocsMcpOptions.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Options;

/// <summary>
/// 文档 MCP 服务端的可调参数
/// </summary>
public sealed class DocsMcpOptions
{
    /// <summary>
    /// 标题命中的加权倍数，标题是人工撰写的最强信号
    /// </summary>
    public double TitleBoost { get; init; } = 3.0;

    /// <summary>
    /// 同一文件最多返回的章节数，防止一篇文章洗掉整个结果列表
    /// </summary>
    public int MaxSectionsPerFile { get; init; } = 2;

    /// <summary>
    /// 检索结果的默认条数
    /// </summary>
    public int DefaultLimit { get; init; } = 5;

    /// <summary>
    /// 检索结果的条数上限，超出时截断而非报错
    /// </summary>
    public int MaxLimit { get; init; } = 15;

    /// <summary>
    /// 热更新检查的节流间隔，两次查询间隔小于此值时跳过 mtime 扫描
    /// </summary>
    public TimeSpan RefreshThrottle { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 单篇文档整体返回的字符数上限，超出时改为返回章节目录
    /// </summary>
    public int MaxWholeDocumentLength { get; init; } = 30 * 1024;

    /// <summary>
    /// 各来源的权重，指南略高是因为任务导向的提问占多数
    /// </summary>
    public IReadOnlyDictionary<DocSourceKind, double> SourceWeights { get; init; } =
        new Dictionary<DocSourceKind, double>
        {
            [DocSourceKind.Guide] = 1.2,
            [DocSourceKind.Package] = 1.0,
            [DocSourceKind.Root] = 0.9,
            [DocSourceKind.PackageReadme] = 0.8
        };
}
```

- [ ] **Step 4: 实现检索结果模型**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Search/SearchHit.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;

namespace XiHan.Framework.Docs.Mcp.Search;

/// <summary>
/// 一条检索结果
/// </summary>
/// <param name="Section">命中的章节</param>
/// <param name="Score">得分，供调用方判断可信度</param>
public sealed record SearchHit(DocSection Section, double Score);
```

- [ ] **Step 5: 实现排序器**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Search/SectionScorer.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Search;

/// <summary>
/// 按覆盖率、标题加权与来源加权对候选章节排序
/// </summary>
/// <param name="options">可调参数</param>
public sealed class SectionScorer(DocsMcpOptions options)
{
    /// <summary>
    /// 对命中章节排序并截断
    /// </summary>
    /// <param name="queryTerms">带权查询词条</param>
    /// <param name="sections">全部章节，下标与索引中的 SectionId 对应</param>
    /// <param name="index">倒排索引</param>
    /// <param name="sourceFilter">来源过滤，为空表示不过滤</param>
    /// <param name="limit">返回条数</param>
    /// <returns>按得分降序排列的结果</returns>
    public IReadOnlyList<SearchHit> Rank(
        IReadOnlyList<WeightedTerm> queryTerms,
        IReadOnlyList<DocSection> sections,
        BigramIndex index,
        DocSourceKind? sourceFilter,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(queryTerms);
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(index);

        var totalWeight = queryTerms.Sum(t => t.Weight);
        if (totalWeight <= 0 || sections.Count == 0)
        {
            return [];
        }

        var accumulated = new Dictionary<int, double>();

        foreach (var term in queryTerms)
        {
            foreach (var posting in index.Find(term.Term))
            {
                if (posting.SectionId >= sections.Count)
                {
                    continue;
                }

                if (sourceFilter is not null && sections[posting.SectionId].Source != sourceFilter)
                {
                    continue;
                }

                var contribution = term.Weight * (posting.InTitle ? options.TitleBoost : 1.0);
                accumulated[posting.SectionId] = accumulated.GetValueOrDefault(posting.SectionId) + contribution;
            }
        }

        var ranked = accumulated
            .Select(pair => new SearchHit(
                sections[pair.Key],
                pair.Value / totalWeight * ResolveSourceWeight(sections[pair.Key].Source)))
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Section.RelativePath, StringComparer.Ordinal);

        return [.. TakeWithPerFileCap(ranked, limit)];
    }

    /// <summary>
    /// 查询来源权重，未配置的来源按 1.0 处理
    /// </summary>
    private double ResolveSourceWeight(DocSourceKind source)
    {
        return options.SourceWeights.TryGetValue(source, out var weight) ? weight : 1.0;
    }

    /// <summary>
    /// 按同文件章节上限贪心取结果
    /// </summary>
    private IEnumerable<SearchHit> TakeWithPerFileCap(IEnumerable<SearchHit> ranked, int limit)
    {
        var perFile = new Dictionary<string, int>(StringComparer.Ordinal);
        var taken = 0;

        foreach (var hit in ranked)
        {
            if (taken >= limit)
            {
                yield break;
            }

            var used = perFile.GetValueOrDefault(hit.Section.RelativePath);
            if (used >= options.MaxSectionsPerFile)
            {
                continue;
            }

            perFile[hit.Section.RelativePath] = used + 1;
            taken++;
            yield return hit;
        }
    }
}
```

- [ ] **Step 6: 运行测试确认通过**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~SectionScorerTests"`

期望：6 个测试全部通过。

- [ ] **Step 7: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp/Options framework/tool/XiHan.Framework.Docs.Mcp/Search framework/test/XiHan.Framework.Docs.Mcp.Tests/SectionScorerTests.cs
git commit -m "feat(docs-mcp): 新增章节检索排序"
```

---

### Task 9: 索引门面与热更新

**Files:**
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Indexing/DocIndex.cs`
- Test: `framework/test/XiHan.Framework.Docs.Mcp.Tests/DocIndexTests.cs`

**Interfaces:**
- Consumes: Task 4 的 `MarkdownSectionSplitter`、Task 5 的 `DocSourceLocator`、Task 6 的 `BigramIndex`、Task 8 的 `DocsMcpOptions`
- Produces:
  - `sealed class DocIndex(DocSourceLocator locator, DocsMcpOptions options, TimeProvider timeProvider, ILogger<DocIndex> logger)`
  - `IReadOnlyList<DocSection> Sections { get; }`
  - `BigramIndex Index { get; }`
  - `IReadOnlyList<DocFile> Files { get; }`
  - `void EnsureFresh()`
  - Task 10（工具层）依赖它

**热更新策略：** 每次查询前，若距上次检查超过 `RefreshThrottle`，扫描全部来源文件的路径与 mtime 组成签名；签名变化就整体重建。选轮询而非 `FileSystemWatcher`，因为后者在网络盘、WSL 挂载与编辑器「写临时文件再改名」的保存流程下会静默漏事件。

- [ ] **Step 1: 写失败的测试**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/DocIndexTests.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 索引门面与热更新测试
/// </summary>
public class DocIndexTests : IDisposable
{
    private readonly string _root;
    private readonly string _guidePath;

    /// <summary>
    /// 构造一个最小仓库结构
    /// </summary>
    public DocIndexTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xihan-docindex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "guide"));
        Directory.CreateDirectory(Path.Combine(_root, "framework", "src"));

        _guidePath = Path.Combine(_root, "docs", "guide", "event-bus.md");
        File.WriteAllText(_guidePath, "# 事件总线\n\n## 本地事件\n\n最初的内容。\n");
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 首次调用即建立索引
    /// </summary>
    [Fact]
    public void 首次调用建立索引()
    {
        var (index, _) = CreateIndex();

        index.EnsureFresh();

        Assert.NotEmpty(index.Sections);
        Assert.Contains(index.Sections, s => s.Heading == "本地事件");
    }

    /// <summary>
    /// 节流窗口内的重复调用不重新扫描
    /// </summary>
    [Fact]
    public void 节流窗口内不重复扫描()
    {
        var (index, _) = CreateIndex();
        index.EnsureFresh();

        File.WriteAllText(_guidePath, "# 事件总线\n\n## 全新章节\n\n改过的内容。\n");
        index.EnsureFresh();

        Assert.DoesNotContain(index.Sections, s => s.Heading == "全新章节");
    }

    /// <summary>
    /// 超过节流窗口且文件变化时重建索引
    /// </summary>
    [Fact]
    public void 文件变化后重建索引()
    {
        var (index, time) = CreateIndex();
        index.EnsureFresh();

        File.WriteAllText(_guidePath, "# 事件总线\n\n## 全新章节\n\n改过的内容。\n");
        File.SetLastWriteTimeUtc(_guidePath, DateTime.UtcNow.AddMinutes(1));
        time.Advance(TimeSpan.FromSeconds(5));
        index.EnsureFresh();

        Assert.Contains(index.Sections, s => s.Heading == "全新章节");
    }

    /// <summary>
    /// 新增文件后被纳入索引
    /// </summary>
    [Fact]
    public void 新增文件被纳入索引()
    {
        var (index, time) = CreateIndex();
        index.EnsureFresh();

        File.WriteAllText(
            Path.Combine(_root, "docs", "guide", "caching.md"),
            "# 缓存\n\n## 分布式缓存\n\n缓存正文。\n");
        time.Advance(TimeSpan.FromSeconds(5));
        index.EnsureFresh();

        Assert.Contains(index.Sections, s => s.RelativePath == "docs/guide/caching.md");
    }

    /// <summary>
    /// 构造被测索引与可控时钟
    /// </summary>
    private (DocIndex Index, FakeTimeProvider Time) CreateIndex()
    {
        var time = new FakeTimeProvider();
        var index = new DocIndex(
            new DocSourceLocator(_root),
            new DocsMcpOptions(),
            time,
            NullLogger<DocIndex>.Instance);

        return (index, time);
    }
}
```

- [ ] **Step 2: 为测试项目添加可控时钟包**

修改 `framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj`，在 `ProjectReference` 那个 `ItemGroup` 之后新增：

```xml
    <ItemGroup>
      <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.0.0" />
    </ItemGroup>
```

若还原失败提示找不到该版本，运行 `dotnet package search Microsoft.Extensions.TimeProvider.Testing --take 5` 查最新稳定版并替换版本号。这个包提供 `FakeTimeProvider`，用于在测试中直接推进时钟而不必真的等待节流窗口。

- [ ] **Step 3: 运行测试确认失败**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~DocIndexTests"`

期望：编译失败，提示找不到类型 `DocIndex`。

- [ ] **Step 4: 实现索引门面**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Indexing/DocIndex.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 索引门面：负责建立章节集合与倒排索引，并按文件修改时间做热更新
/// </summary>
/// <param name="locator">文档来源定位器</param>
/// <param name="options">可调参数</param>
/// <param name="timeProvider">时钟，便于测试注入</param>
/// <param name="logger">日志记录器，全部写入 stderr</param>
/// <remarks>
/// 热更新采用 mtime 轮询而非 FileSystemWatcher：后者在网络磁盘、WSL 挂载
/// 以及编辑器「写临时文件再改名」的保存流程下会静默漏事件，
/// 而这里全量重建只需几百毫秒，不值得为省这点成本引入一个会失效的机制。
/// </remarks>
public sealed class DocIndex(
    DocSourceLocator locator,
    DocsMcpOptions options,
    TimeProvider timeProvider,
    ILogger<DocIndex> logger)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _contentCache = new(StringComparer.Ordinal);

    private DateTimeOffset _lastCheck = DateTimeOffset.MinValue;
    private string _signature = string.Empty;

    /// <summary>
    /// 当前全部章节，下标与倒排索引中的 SectionId 对应
    /// </summary>
    public IReadOnlyList<DocSection> Sections { get; private set; } = [];

    /// <summary>
    /// 当前倒排索引
    /// </summary>
    public BigramIndex Index { get; private set; } = new();

    /// <summary>
    /// 当前被索引的文件列表
    /// </summary>
    public IReadOnlyList<DocFile> Files { get; private set; } = [];

    /// <summary>
    /// 确保索引是最新的，必要时重建
    /// </summary>
    public void EnsureFresh()
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (Sections.Count > 0 && now - _lastCheck < options.RefreshThrottle)
            {
                return;
            }

            _lastCheck = now;

            var files = locator.Enumerate();
            var signature = ComputeSignature(files);
            if (signature == _signature)
            {
                return;
            }

            _signature = signature;
            Rebuild(files);
        }
    }

    /// <summary>
    /// 由文件路径与修改时间组成签名，用于判断是否需要重建
    /// </summary>
    private static string ComputeSignature(IReadOnlyList<DocFile> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files)
        {
            builder.Append(file.RelativePath).Append('#').Append(file.LastWriteUtc.Ticks).Append(';');
        }

        return builder.ToString();
    }

    /// <summary>
    /// 全量重建章节集合与倒排索引
    /// </summary>
    private void Rebuild(IReadOnlyList<DocFile> files)
    {
        var sections = new List<DocSection>();
        var index = new BigramIndex();

        foreach (var file in files)
        {
            var content = ReadContent(file);
            if (content is null)
            {
                continue;
            }

            sections.AddRange(MarkdownSectionSplitter.Split(file.RelativePath, file.Source, content));
        }

        for (var i = 0; i < sections.Count; i++)
        {
            index.Add(i, sections[i].TitlePath, sections[i].Content);
        }

        Sections = sections;
        Index = index;
        Files = files;

        logger.LogInformation("文档索引已重建：{FileCount} 个文件，{SectionCount} 个章节。", files.Count, sections.Count);
    }

    /// <summary>
    /// 读取文件内容，读取失败时沿用缓存中的旧内容，不让整次重建失败
    /// </summary>
    private string? ReadContent(DocFile file)
    {
        try
        {
            var content = File.ReadAllText(file.AbsolutePath);
            _contentCache[file.RelativePath] = content;
            return content;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (_contentCache.TryGetValue(file.RelativePath, out var cached))
            {
                logger.LogWarning(ex, "读取 {Path} 失败，沿用上一次的内容。", file.RelativePath);
                return cached;
            }

            logger.LogWarning(ex, "读取 {Path} 失败且无缓存，本次跳过该文件。", file.RelativePath);
            return null;
        }
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~DocIndexTests"`

期望：4 个测试全部通过。

若 `Lock` 类型报找不到，说明该 API 需要 .NET 9 以上——本项目为 net10.0 应当可用；若仍失败，把 `private readonly Lock _gate = new();` 改为 `private readonly object _gate = new();`。

- [ ] **Step 6: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp/Indexing/DocIndex.cs framework/test/XiHan.Framework.Docs.Mcp.Tests/DocIndexTests.cs framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj
git commit -m "feat(docs-mcp): 新增索引门面与文档热更新"
```

---

### Task 10: 三个 MCP 工具

**Files:**
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/Tools/DocsMcpTools.cs`
- Test: `framework/test/XiHan.Framework.Docs.Mcp.Tests/DocsMcpToolsTests.cs`

**Interfaces:**
- Consumes: Task 5 的 `DocSourceLocator`、Task 7 的 `SynonymExpander`、Task 8 的 `SectionScorer` 与 `DocsMcpOptions`、Task 9 的 `DocIndex`
- Produces: `sealed class DocsMcpTools(DocIndex index, DocSourceLocator locator, SynonymExpander expander, SectionScorer scorer, DocsMcpOptions options)`，三个公开方法 `SearchDocs`、`ReadDoc`、`ListDocs`，均返回 `string`。Task 11 的 `Program` 通过 `WithToolsFromAssembly()` 自动发现它们。

**返回 Markdown 文本而非 JSON**，因为调用方是语言模型：同样的信息量下 Markdown 的 token 消耗更低，且模型对带标题与出处的文本理解更稳。

- [ ] **Step 1: 写失败的测试**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/DocsMcpToolsTests.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;
using XiHan.Framework.Docs.Mcp.Tools;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// MCP 工具层测试
/// </summary>
public class DocsMcpToolsTests : IDisposable
{
    private readonly string _root;

    /// <summary>
    /// 构造一个最小仓库结构
    /// </summary>
    public DocsMcpToolsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xihan-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "guide"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "packages"));
        Directory.CreateDirectory(Path.Combine(_root, "framework", "src"));

        File.WriteAllText(
            Path.Combine(_root, "docs", "guide", "event-bus.md"),
            "# 事件总线\n\n发布方不认识订阅方。\n\n## 本地事件还是分布式事件\n\n分布式事件在事务提交之后发布。\n");
        File.WriteAllText(
            Path.Combine(_root, "docs", "packages", "caching.md"),
            "# 缓存包\n\n## 配置项\n\n缓存过期时间的配置说明。\n");
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 检索结果带出处：相对路径、标题路径与行号
    /// </summary>
    [Fact]
    public void 检索结果带出处()
    {
        var result = CreateTools().SearchDocs("分布式事件什么时候发布", source: null, limit: 5);

        Assert.Contains("docs/guide/event-bus.md", result);
        Assert.Contains("本地事件还是分布式事件", result);
        Assert.Contains("事务提交之后", result);
    }

    /// <summary>
    /// 零命中时明确告知文档中没有，而不是返回空内容诱导模型编造
    /// </summary>
    [Fact]
    public void 零命中时明确告知()
    {
        var result = CreateTools().SearchDocs("量子纠缠的宏观表现", source: null, limit: 5);

        Assert.Contains("未找到", result);
    }

    /// <summary>
    /// 来源过滤生效：限定指南时不返回包文档
    /// </summary>
    [Fact]
    public void 来源过滤生效()
    {
        var tools = CreateTools();

        var unfiltered = tools.SearchDocs("缓存过期配置", source: null, limit: 5);
        var filtered = tools.SearchDocs("缓存过期配置", source: "guide", limit: 5);

        Assert.Contains("docs/packages/caching.md", unfiltered);
        Assert.Contains("未找到", filtered);
    }

    /// <summary>
    /// 读取整篇文档返回原文
    /// </summary>
    [Fact]
    public void 读取整篇文档()
    {
        var result = CreateTools().ReadDoc("docs/guide/event-bus.md", section: null);

        Assert.Contains("发布方不认识订阅方", result);
        Assert.Contains("分布式事件在事务提交之后发布", result);
    }

    /// <summary>
    /// 指定章节时只返回该节
    /// </summary>
    [Fact]
    public void 读取指定章节()
    {
        var result = CreateTools().ReadDoc("docs/guide/event-bus.md", "本地事件还是分布式事件");

        Assert.Contains("事务提交之后", result);
        Assert.DoesNotContain("发布方不认识订阅方", result);
    }

    /// <summary>
    /// 路径不存在时给出候选建议而不是裸错误
    /// </summary>
    [Fact]
    public void 路径不存在时给出建议()
    {
        var result = CreateTools().ReadDoc("docs/guide/eventbus.md", section: null);

        Assert.Contains("未找到", result);
        Assert.Contains("event-bus.md", result);
    }

    /// <summary>
    /// 逃逸仓库根的路径被拒绝
    /// </summary>
    [Fact]
    public void 拒绝逃逸路径()
    {
        var result = CreateTools().ReadDoc("../../secrets.txt", section: null);

        Assert.Contains("拒绝", result);
    }

    /// <summary>
    /// 默认列表不展开章节标题，控制 token 消耗
    /// </summary>
    [Fact]
    public void 默认列表不展开章节()
    {
        var result = CreateTools().ListDocs(source: null, includeSections: false);

        Assert.Contains("docs/guide/event-bus.md", result);
        Assert.DoesNotContain("本地事件还是分布式事件", result);
    }

    /// <summary>
    /// 显式要求时展开章节标题
    /// </summary>
    [Fact]
    public void 显式要求时展开章节()
    {
        var result = CreateTools().ListDocs(source: null, includeSections: true);

        Assert.Contains("本地事件还是分布式事件", result);
    }

    /// <summary>
    /// 构造被测工具层
    /// </summary>
    private DocsMcpTools CreateTools()
    {
        var locator = new DocSourceLocator(_root);
        var options = new DocsMcpOptions();
        var index = new DocIndex(locator, options, TimeProvider.System, NullLogger<DocIndex>.Instance);

        return new DocsMcpTools(
            index,
            locator,
            SynonymExpander.Load(jsonPath: null, NullLogger.Instance),
            new SectionScorer(options),
            options);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~DocsMcpToolsTests"`

期望：编译失败，提示找不到类型 `DocsMcpTools`。

- [ ] **Step 3: 实现工具层**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/Tools/DocsMcpTools.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tools;

/// <summary>
/// 曦寒框架文档的三个 MCP 工具
/// </summary>
/// <param name="index">索引门面</param>
/// <param name="locator">文档来源定位器</param>
/// <param name="expander">同义词扩展器</param>
/// <param name="scorer">排序器</param>
/// <param name="options">可调参数</param>
/// <remarks>
/// 工具数量刻意压到三个：工具越多，模型越容易选错或漏用。
/// 来源区分用参数表达，而不是拆成 search_guide / search_packages 之类的多个工具。
/// 全部返回 Markdown 文本而非 JSON，因为调用方是语言模型，同等信息量下 Markdown 的 token 消耗更低。
/// </remarks>
[McpServerToolType]
public sealed class DocsMcpTools(
    DocIndex index,
    DocSourceLocator locator,
    SynonymExpander expander,
    SectionScorer scorer,
    DocsMcpOptions options)
{
    /// <summary>
    /// 检索曦寒框架文档，返回最相关的章节原文
    /// </summary>
    /// <param name="query">自然语言问题或关键词</param>
    /// <param name="source">来源过滤</param>
    /// <param name="limit">返回条数</param>
    /// <returns>带出处的章节原文</returns>
    [McpServerTool(Name = "search_docs")]
    [Description("检索曦寒框架（XiHan.Framework）的文档，返回最相关的章节原文与出处。用它回答框架的用法、配置、API 与设计原理问题，不要凭记忆作答。")]
    public string SearchDocs(
        [Description("自然语言问题或关键词，例如「分布式事件什么时候发出去」")] string query,
        [Description("来源过滤：guide 使用指南、packages 包文档、readme 包自述、root 全局文档、all 全部。默认 all")] string? source = null,
        [Description("返回的章节数，默认 5，最大 15")] int limit = 5)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "查询串为空，请给出要检索的问题或关键词。";
            }

            index.EnsureFresh();

            var (filter, notice) = ParseSource(source);
            var effectiveLimit = Math.Clamp(limit <= 0 ? options.DefaultLimit : limit, 1, options.MaxLimit);
            var terms = expander.Expand(query);
            var hits = scorer.Rank(terms, index.Sections, index.Index, filter, effectiveLimit);

            if (hits.Count == 0)
            {
                return BuildEmptyResult(query, notice);
            }

            var builder = new StringBuilder();
            if (notice.Length > 0)
            {
                builder.AppendLine(notice).AppendLine();
            }

            builder.AppendLine($"检索「{query}」共命中 {hits.Count} 个章节：").AppendLine();

            foreach (var hit in hits)
            {
                builder
                    .AppendLine($"## {hit.Section.TitlePath}")
                    .AppendLine($"- 出处：`{hit.Section.RelativePath}` 第 {hit.Section.StartLine}-{hit.Section.EndLine} 行")
                    .AppendLine($"- 来源：{DescribeSource(hit.Section.Source)}；得分：{hit.Score:F2}")
                    .AppendLine()
                    .AppendLine(hit.Section.Content)
                    .AppendLine();
            }

            return builder.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"检索时发生错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 读取一篇文档的全文或指定章节
    /// </summary>
    /// <param name="path">相对仓库根的文档路径</param>
    /// <param name="section">章节标题，为空则返回全文</param>
    /// <returns>文档原文</returns>
    [McpServerTool(Name = "read_doc")]
    [Description("读取曦寒框架的一篇文档。先用 search_docs 找到路径，需要更多上下文时再用本工具。")]
    public string ReadDoc(
        [Description("相对仓库根的路径，例如 docs/guide/event-bus.md")] string path,
        [Description("章节标题，只返回该节。留空返回全文；全文过长时会改为返回章节目录")] string? section = null)
    {
        try
        {
            index.EnsureFresh();

            if (!locator.TryResolveDocumentPath(path, out var absolutePath))
            {
                return $"拒绝访问 `{path}`：路径必须是仓库根内的相对路径。";
            }

            if (!File.Exists(absolutePath))
            {
                return BuildPathSuggestion(path);
            }

            var sections = index.Sections.Where(s => s.RelativePath == path.Replace('\\', '/')).ToList();

            if (!string.IsNullOrWhiteSpace(section))
            {
                var matched = sections.Where(s => s.Heading.Contains(section, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matched.Count == 0)
                {
                    var available = string.Join("、", sections.Select(s => s.Heading).Distinct());
                    return $"文档 `{path}` 中未找到章节「{section}」。可用章节：{available}";
                }

                var builder = new StringBuilder();
                foreach (var item in matched)
                {
                    builder
                        .AppendLine($"## {item.TitlePath}")
                        .AppendLine($"- 出处：`{item.RelativePath}` 第 {item.StartLine}-{item.EndLine} 行")
                        .AppendLine()
                        .AppendLine(item.Content)
                        .AppendLine();
                }

                return builder.ToString().TrimEnd();
            }

            var content = File.ReadAllText(absolutePath);
            if (content.Length <= options.MaxWholeDocumentLength)
            {
                return $"# `{path}`\n\n{content}";
            }

            var headings = string.Join("\n", sections.Select(s => $"- {s.Heading}"));
            return $"""
                文档 `{path}` 共 {content.Length} 个字符，超过单次返回上限。
                请用 section 参数指定要读的章节。可用章节：

                {headings}
                """;
        }
        catch (Exception ex)
        {
            return $"读取文档时发生错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 列出全部被索引的文档
    /// </summary>
    /// <param name="source">来源过滤</param>
    /// <param name="includeSections">是否展开章节标题</param>
    /// <returns>文档清单</returns>
    [McpServerTool(Name = "list_docs")]
    [Description("列出曦寒框架全部文档，用于建立整体地图。默认只列标题与摘要；includeSections 为 true 时展开章节标题，输出会大幅变长。")]
    public string ListDocs(
        [Description("来源过滤：guide、packages、readme、root、all。默认 all")] string? source = null,
        [Description("是否展开每篇的章节标题，默认 false")] bool includeSections = false)
    {
        try
        {
            index.EnsureFresh();

            var (filter, notice) = ParseSource(source);
            var files = filter is null ? index.Files : index.Files.Where(f => f.Source == filter).ToList();

            var builder = new StringBuilder();
            if (notice.Length > 0)
            {
                builder.AppendLine(notice).AppendLine();
            }

            builder.AppendLine($"共 {files.Count} 篇文档：").AppendLine();

            foreach (var file in files)
            {
                var sections = index.Sections.Where(s => s.RelativePath == file.RelativePath).ToList();
                var title = sections.FirstOrDefault()?.DocumentTitle ?? file.RelativePath;

                builder.AppendLine($"- `{file.RelativePath}` — {title}");

                var summary = BuildSummary(sections);
                if (summary.Length > 0)
                {
                    builder.AppendLine($"  {summary}");
                }

                if (!includeSections)
                {
                    continue;
                }

                foreach (var heading in sections.Select(s => s.Heading).Distinct())
                {
                    builder.AppendLine($"  - {heading}");
                }
            }

            return builder.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"列出文档时发生错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 解析来源参数，无法识别时按全部处理并附提示
    /// </summary>
    private static (DocSourceKind? Filter, string Notice) ParseSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return (null, string.Empty);
        }

        return source.ToLowerInvariant() switch
        {
            "guide" => (DocSourceKind.Guide, string.Empty),
            "packages" or "package" => (DocSourceKind.Package, string.Empty),
            "readme" => (DocSourceKind.PackageReadme, string.Empty),
            "root" => (DocSourceKind.Root, string.Empty),
            _ => (null, $"提示：无法识别的 source 取值「{source}」，已按 all 处理。可用取值为 guide、packages、readme、root、all。")
        };
    }

    /// <summary>
    /// 描述来源分类，便于模型判断内容性质
    /// </summary>
    private static string DescribeSource(DocSourceKind source)
    {
        return source switch
        {
            DocSourceKind.Guide => "使用指南",
            DocSourceKind.Package => "包文档",
            DocSourceKind.PackageReadme => "包自述",
            DocSourceKind.Root => "全局文档",
            _ => "未知"
        };
    }

    /// <summary>
    /// 从概述章节取一句话摘要
    /// </summary>
    private static string BuildSummary(IReadOnlyList<DocSection> sections)
    {
        var preamble = sections.FirstOrDefault(s => s.Heading == "概述");
        if (preamble is null)
        {
            return string.Empty;
        }

        foreach (var line in preamble.Content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('>') || trimmed.StartsWith('-') || trimmed.StartsWith('|') || trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            var cleaned = trimmed.Replace("**", string.Empty).Replace("`", string.Empty);
            return cleaned.Length <= 80 ? cleaned : cleaned[..80];
        }

        return string.Empty;
    }

    /// <summary>
    /// 构造零命中时的回复，明确告知文档中没有，避免模型自行编造
    /// </summary>
    private string BuildEmptyResult(string query, string notice)
    {
        var candidates = string.Join("\n", index.Files.Take(10).Select(f => $"- `{f.RelativePath}`"));

        return $"""
            {notice}
            未找到与「{query}」相关的文档内容。曦寒框架的文档中没有涵盖这个主题，请不要基于猜测作答。

            可以换个关键词再试，或用 list_docs 查看全部文档。部分文档：
            {candidates}
            """.Trim();
    }

    /// <summary>
    /// 构造路径不存在时的候选建议
    /// </summary>
    private string BuildPathSuggestion(string path)
    {
        var target = Path.GetFileNameWithoutExtension(path);
        var suggestions = index.Files
            .OrderByDescending(f => CountCommonCharacters(Path.GetFileNameWithoutExtension(f.RelativePath), target))
            .Take(3)
            .Select(f => $"- `{f.RelativePath}`");

        return $"""
            未找到文档 `{path}`。你可能是指：
            {string.Join("\n", suggestions)}
            """;
    }

    /// <summary>
    /// 统计两个文件名的共同字符数，用于粗略推荐相近路径
    /// </summary>
    private static int CountCommonCharacters(string left, string right)
    {
        var pool = right.ToLowerInvariant().ToList();
        var count = 0;

        foreach (var ch in left.ToLowerInvariant())
        {
            if (pool.Remove(ch))
            {
                count++;
            }
        }

        return count;
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~DocsMcpToolsTests"`

期望：9 个测试全部通过。

若 `[McpServerTool]` 或 `[McpServerToolType]` 报找不到，回到 Task 1 Step 3 确认 `ModelContextProtocol` 2.2.0 的实际特性名称，并同步修正此处。

- [ ] **Step 5: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp/Tools framework/test/XiHan.Framework.Docs.Mcp.Tests/DocsMcpToolsTests.cs
git commit -m "feat(docs-mcp): 新增文档检索的三个 MCP 工具"
```

---

### Task 11: 服务端组装与启动失败处理

**Files:**
- Modify: `framework/tool/XiHan.Framework.Docs.Mcp/Program.cs`

**Interfaces:**
- Consumes: Task 5 的 `DocSourceLocator` 与 `DocsRootNotFoundException`、Task 7 的 `SynonymExpander`、Task 8 的 `DocsMcpOptions` 与 `SectionScorer`、Task 9 的 `DocIndex`、Task 10 的 `DocsMcpTools`
- Produces: 可运行的 stdio MCP Server

**两个必须做对的点：** 日志全部走 stderr（stdout 是协议通道）；仓库根找不到时以退出码 1 结束，而不是带空索引进入服务状态。

- [ ] **Step 1: 改写 Program.cs**

把 `framework/tool/XiHan.Framework.Docs.Mcp/Program.cs` 整体替换为：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;
using XiHan.Framework.Docs.Mcp.Tools;

string repositoryRoot;
try
{
    repositoryRoot = DocSourceLocator.ResolveRepositoryRoot(
        AppContext.BaseDirectory,
        Environment.GetEnvironmentVariable("XIHAN_DOCS_ROOT"));
}
catch (DocsRootNotFoundException ex)
{
    // stdout 是 MCP 协议通道，错误信息只能走 stderr
    await Console.Error.WriteLineAsync(ex.Message);
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

// 所有日志强制写入 stderr：写入 stdout 会插进 JSON-RPC 流中破坏连接
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(new DocsMcpOptions());
builder.Services.AddSingleton(new DocSourceLocator(repositoryRoot));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DocIndex>();
builder.Services.AddSingleton<SectionScorer>();
builder.Services.AddSingleton(provider => SynonymExpander.Load(
    Path.Combine(AppContext.BaseDirectory, "Resources", "synonyms.json"),
    provider.GetRequiredService<ILoggerFactory>().CreateLogger<SynonymExpander>()));
builder.Services.AddSingleton<DocsMcpTools>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

// 启动时同步建立索引，建完才开始接受请求
host.Services.GetRequiredService<DocIndex>().EnsureFresh();

await host.RunAsync();
return 0;
```

- [ ] **Step 2: 构建确认通过**

运行：`dotnet build framework/tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj -c Release`

期望：构建成功，无告警。

- [ ] **Step 3: 手动验证启动失败路径**

在仓库外的临时目录运行程序，确认它拒绝启动而不是静默服务：

```bash
cd /tmp && XIHAN_DOCS_ROOT=/tmp dotnet <仓库绝对路径>/framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll; echo "退出码: $?"
```

期望：stderr 输出「环境变量 XIHAN_DOCS_ROOT 指向 '/tmp'，但该目录下缺少 docs 或 framework 子目录。」，退出码为 1。

- [ ] **Step 4: 手动验证正常启动与协议握手**

用一次 `initialize` 请求确认 stdout 只有干净的 JSON-RPC：

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"manual","version":"1.0"}}}' | dotnet framework/tool/XiHan.Framework.Docs.Mcp/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.dll 2>/dev/null
```

期望：stdout 输出一行合法 JSON，包含 `"result"` 与 `serverInfo`；**不含任何日志文本**。如果 stdout 里混进了日志，说明日志配置没生效，必须先修好再继续。

- [ ] **Step 5: 提交**

```bash
git add framework/tool/XiHan.Framework.Docs.Mcp/Program.cs
git commit -m "feat(docs-mcp): 组装 stdio 服务端并处理启动失败"
```

---

### Task 12: 黄金查询集与交付文档

**Files:**
- Create: `framework/test/XiHan.Framework.Docs.Mcp.Tests/GoldenQueryTests.cs`
- Create: `framework/tool/XiHan.Framework.Docs.Mcp/README.md`

**Interfaces:**
- Consumes: Task 5 的 `DocSourceLocator`、Task 7 的 `SynonymExpander`、Task 8 的 `SectionScorer` 与 `DocsMcpOptions`、Task 9 的 `DocIndex`
- Produces: 跑在真实 `docs/` 上的回归防线 + 使用说明

**为什么这组测试是整个项目最重要的部分：** 它是唯一能防止「越调越差」的机制。日后调整权重让某个查询变准时，它会立刻暴露是否弄坏了其他查询。

- [ ] **Step 1: 写黄金查询集测试**

创建 `framework/test/XiHan.Framework.Docs.Mcp.Tests/GoldenQueryTests.cs`：

```csharp
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 黄金查询集：跑真实文档，确保检索质量不随权重调整而退化
/// </summary>
/// <remarks>
/// 这组断言是唯一能防止「越调越差」的机制。若某条断言失效，先确认是文档改动
/// 还是排序规则退化——两者都需要人工判断，不要直接放宽断言了事。
/// </remarks>
public class GoldenQueryTests
{
    private static readonly Lazy<(DocIndex Index, SectionScorer Scorer, SynonymExpander Expander, DocsMcpOptions Options)> Shared =
        new(BuildIndex);

    /// <summary>
    /// 每条查询的期望命中文件必须出现在前三名
    /// </summary>
    /// <param name="query">查询串</param>
    /// <param name="expectedPathFragment">期望命中的路径片段</param>
    [Theory]
    [InlineData("分布式事件什么时候发出去", "docs/guide/event-bus.md")]
    [InlineData("Redis 事件总线怎么配", "docs/packages/eventbus-redis.md")]
    [InlineData("动态 API 路由为什么没有动词", "docs/guide/dynamic-api.md")]
    [InlineData("怎么避免重复消费", "docs/packages/eventbus.md")]
    [InlineData("ILocalEventBus", "eventbus")]
    [InlineData("模块的生命周期钩子有哪些", "docs/guide/modularity.md")]
    [InlineData("多租户怎么隔离数据", "docs/guide/multi-tenancy.md")]
    public void 期望文件出现在前三名(string query, string expectedPathFragment)
    {
        var (index, scorer, expander, options) = Shared.Value;
        index.EnsureFresh();

        var hits = scorer.Rank(expander.Expand(query), index.Sections, index.Index, sourceFilter: null, limit: 3);

        Assert.NotEmpty(hits);
        Assert.Contains(hits, hit => hit.Section.RelativePath.Contains(expectedPathFragment, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 索引规模符合预期，防止来源枚举被意外破坏
    /// </summary>
    [Fact]
    public void 索引覆盖四类来源()
    {
        var (index, _, _, _) = Shared.Value;
        index.EnsureFresh();

        Assert.Contains(index.Files, f => f.Source == DocSourceKind.Guide);
        Assert.Contains(index.Files, f => f.Source == DocSourceKind.Package);
        Assert.Contains(index.Files, f => f.Source == DocSourceKind.Root);
        Assert.Contains(index.Files, f => f.Source == DocSourceKind.PackageReadme);
        Assert.True(index.Sections.Count > 500, $"章节数只有 {index.Sections.Count}，切片器可能出了问题。");
    }

    /// <summary>
    /// 用真实仓库构造索引
    /// </summary>
    private static (DocIndex, SectionScorer, SynonymExpander, DocsMcpOptions) BuildIndex()
    {
        var root = DocSourceLocator.ResolveRepositoryRoot(
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("XIHAN_DOCS_ROOT"));

        var options = new DocsMcpOptions();
        var index = new DocIndex(
            new DocSourceLocator(root),
            options,
            TimeProvider.System,
            NullLogger<DocIndex>.Instance);

        var synonymsPath = Path.Combine(
            root, "framework", "tool", "XiHan.Framework.Docs.Mcp", "Resources", "synonyms.json");

        return (index, new SectionScorer(options), SynonymExpander.Load(synonymsPath, NullLogger.Instance), options);
    }
}
```

- [ ] **Step 2: 运行黄金查询集**

运行：`dotnet test framework/test/XiHan.Framework.Docs.Mcp.Tests/XiHan.Framework.Docs.Mcp.Tests.csproj --filter "FullyQualifiedName~GoldenQueryTests"`

期望：8 个测试全部通过。

**若某条查询未命中期望文件**，按这个顺序排查，不要直接放宽断言：
1. 用 `dotnet run --project framework/tool/XiHan.Framework.Docs.Mcp` 手工跑一次该查询，看实际排在前面的是什么
2. 若是同义词覆盖不足 → 往 `Resources/synonyms.json` 补一组术语
3. 若是标题权重不够 → 调 `DocsMcpOptions.TitleBoost`
4. 若是来源权重压制了正确结果 → 调 `SourceWeights`
5. 每次调整后重跑整个黄金查询集，确认没有弄坏其他条目

- [ ] **Step 3: 写模块 README**

创建 `framework/tool/XiHan.Framework.Docs.Mcp/README.md`，沿用仓库固定七段结构：

````markdown
# XiHan.Framework.Docs.Mcp

## 概述
把曦寒框架仓库内的 163 篇 Markdown 文档变成 AI 助手可检索的知识源，以本机 stdio MCP Server 的形式对外提供。仓库内部工具，不发布为 NuGet 包。

## 核心能力
- 索引四类文档来源：使用指南、包文档、文档站全局文档、各包 README
- 按 Markdown 标题切成章节，建立内存 bigram 倒排索引，支持中英混合检索
- 框架术语同义词扩展，补足纯字面匹配处理不了的「换句话说」提问
- 文档保存后自动热更新，无需重启客户端
- 三个 MCP 工具：`search_docs` / `read_doc` / `list_docs`

## 依赖关系
- `ModelContextProtocol` 2.2.0：MCP 协议与 stdio 传输
- `Microsoft.Extensions.Hosting` 10.0.0：宿主与依赖注入
- 不依赖仓库内任何其他项目

## 配置与约定
| 项 | 说明 |
| --- | --- |
| `XIHAN_DOCS_ROOT` | 环境变量，显式指定仓库根。不设置时从程序集所在目录逐层向上查找同时含 `docs/` 与 `framework/` 的目录 |
| `Resources/synonyms.json` | 术语同义词表，缺失时降级为不扩展 |
| 日志 | 全部写入 stderr。stdout 是 MCP 的 JSON-RPC 协议通道，禁止写入 |

## 使用方式
先构建：

```bash
dotnet build framework/tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj -c Release
```

在 Claude Code 中注册（`.mcp.json`，把 `<仓库绝对路径>` 换成实际路径）：

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

其他 MCP 客户端（Cursor 等）配置形式相同：`command` 为 `dotnet`，`args` 指向构建产物的 dll。

## 扩展点
- 往 `Resources/synonyms.json` 增补术语组即可改善「换句话说」类提问的召回
- 调整 `Options/DocsMcpOptions.cs` 中的 `TitleBoost` 与 `SourceWeights` 可改变排序倾向，**改完必须重跑黄金查询集**
- 新增文档来源：扩展 `Sources/DocSourceKind.cs` 与 `DocSourceLocator.Enumerate()`

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
  Tools/
    DocsMcpTools.cs
  Resources/
    synonyms.json
```
````

- [ ] **Step 4: 跑全量测试确认整体无破坏**

运行：`dotnet test framework/XiHan.Framework.slnx -c Release`

期望：全部测试通过，包含新增的 `XiHan.Framework.Docs.Mcp.Tests`。

- [ ] **Step 5: 提交**

```bash
git add framework/test/XiHan.Framework.Docs.Mcp.Tests/GoldenQueryTests.cs framework/tool/XiHan.Framework.Docs.Mcp/README.md
git commit -m "feat(docs-mcp): 新增黄金查询集与模块说明"
```

---

## 验收清单

全部任务完成后，逐项确认：

- [ ] `dotnet build framework/XiHan.Framework.slnx -c Release -p:GeneratePackageOnBuild=false` 成功
- [ ] `dotnet test framework/XiHan.Framework.slnx -c Release` 全绿
- [ ] `framework/nupkgs` 下**没有** `XiHan.Framework.Docs.Mcp` 的包（确认 `IsPackable=false` 生效）
- [ ] 在仓库外目录启动，退出码为 1 且 stderr 有清晰提示
- [ ] `initialize` 握手的 stdout 是干净 JSON，无日志混入
- [ ] 在 Claude Code 中注册后，问「曦寒框架的分布式事件什么时候发出去」能得到带 `docs/guide/event-bus.md` 出处的回答
- [ ] 修改任一文档并保存后，再次查询能读到新内容，无需重启客户端
