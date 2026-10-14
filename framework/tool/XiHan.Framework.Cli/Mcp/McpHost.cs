// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using XiHan.Framework.Cli.Knowledge;

namespace XiHan.Framework.Cli.Mcp;

/// <summary>
/// 把知识库查询暴露为 MCP 工具与提示词
/// </summary>
internal static class McpHost
{
    /// <summary>
    /// 服务端名称
    /// </summary>
    public const string ServerName = "xihan-framework";

    /// <summary>
    /// 构建服务端选项：只读工具与提示词
    /// </summary>
    public static McpServerOptions CreateOptions(FrameworkKnowledge knowledge, string instructions)
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in CreateTools(knowledge))
        {
            tools.Add(tool);
        }

        var prompts = new McpServerPrimitiveCollection<McpServerPrompt>();
        foreach (var prompt in CreatePrompts(knowledge))
        {
            prompts.Add(prompt);
        }

        return new McpServerOptions
        {
            ServerInfo = new Implementation { Name = ServerName, Title = "XiHan.Framework", Version = knowledge.Version },
            ServerInstructions = instructions,
            ToolCollection = tools,
            PromptCollection = prompts,
        };
    }

    /// <summary>
    /// 在给定的传输上运行服务端，直到对端断开
    /// </summary>
    public static async Task RunAsync(ITransport transport, McpServerOptions options, CancellationToken cancellationToken)
    {
        await using var server = McpServer.Create(transport, options);
        await server.RunAsync(cancellationToken);
    }

    /// <summary>
    /// 全部工具
    /// </summary>
    public static IReadOnlyList<McpServerTool> CreateTools(FrameworkKnowledge knowledge)
    {
        return
        [
            Tool(
                "xihan_framework_list",
                "列出框架包",
                "按所在层列出 XiHan.Framework 的全部 NuGet 包与模块类。不确定某个能力在哪个包时先调它，或用 xihan_framework_search 检索。",
                ([Description("只列某一层，如 基础设施、Web")] string? layer = null, [Description("按包名或说明里的关键字过滤")] string? query = null) =>
                    Guard(() => Rendering.PackageList(knowledge, Queries.ListPackages(knowledge, layer, query)))),
            Tool(
                "xihan_framework_info",
                "包与模块",
                "取一个包的概况：安装命令、模块类与 [DependsOn] 依赖、引用的框架包与第三方包、绑定的配置节。写 [DependsOn] 或加包引用前先查它，不要凭记忆写模块名。",
                ([Description("包名（XiHan.Framework.Caching）、短名（Caching）或模块类名（XiHanCachingModule）")] string package) =>
                    Guard(() => Rendering.PackageInfo(knowledge, Queries.RequirePackage(knowledge, package)))),
            Tool(
                "xihan_framework_doc",
                "完整文档",
                "取文档页的完整 Markdown：开发指南（guide/caching、guide/dynamic-api）、包参考页（写包名即可）或开始页（quickstart、overview）。不传 name 时列出全部页面。",
                ([Description("站内路径或包名")] string? name = null) =>
                    Guard(() => string.IsNullOrWhiteSpace(name) ? Rendering.PageList(knowledge) : Queries.RequirePage(knowledge, name).Markdown)),
            Tool(
                "xihan_framework_config",
                "配置节",
                "查 appsettings 配置节：每个属性的名字、类型、缺省值与说明，并给出按缺省值写出的配置片段。不传 query 时列出全部配置节。写配置时只用这里查得到的键。",
                ([Description("配置节（XiHan:Caching:Redis）、选项类名或包名")] string? query = null) =>
                    Guard(() => Rendering.ConfigSections(Queries.Config(knowledge, query), !string.IsNullOrWhiteSpace(query)))),
            Tool(
                "xihan_framework_search",
                "检索文档",
                "在全部文档里检索关键字（多个词用空格分开，需同时出现），按相关度返回页面与摘录。适合回答「X 该用哪个包、怎么做」。",
                ([Description("检索词，如 分布式锁、动态 API 路由")] string query) =>
                    Guard(() => Rendering.SearchResults(query, Queries.Search(knowledge, query)))),
            Tool(
                "xihan_framework_changelog",
                "更新日志",
                "查更新日志。只传 from 取这一版；传 from 与 to 取两版之间（不含 from、含 to）的全部变更，用于升级迁移；keyword 只保留提到它的条目（可写包名）。都不传时取最近一版。",
                ([Description("版本号，如 4.6.0")] string? from = null, [Description("版本号，如 4.6.2")] string? to = null, [Description("关键字或包名")] string? keyword = null) =>
                    Guard(() => Rendering.Changelog(Queries.Changelog(knowledge, from, to, keyword)))),
        ];
    }

    /// <summary>
    /// 全部提示词
    /// </summary>
    public static IReadOnlyList<McpServerPrompt> CreatePrompts(FrameworkKnowledge knowledge)
    {
        return
        [
            McpServerPrompt.Create(
                () => $"你是 XiHan.Framework 的专家。以下是使用规范，回答与写代码都按它执行；涉及具体包、模块或配置时先调 xihan_framework_info / xihan_framework_config / xihan_framework_doc 查证，不要凭记忆写。\n\n{SkillBody(knowledge)}",
                new McpServerPromptCreateOptions { Name = "xihan-framework-expert", Title = "XiHan.Framework 专家", Description = "以 XiHan.Framework 专家身份工作：先查包、模块与配置再写代码。" }),
            McpServerPrompt.Create(
                ([Description("模块要提供的能力")] string description) => string.Join('\n',
                    "用 XiHan.Framework 写一个模块，提供下面的能力：",
                    string.Empty,
                    description,
                    string.Empty,
                    "步骤：",
                    "1. 调 xihan_framework_search 与 xihan_framework_list 找到要依赖的框架包，调 xihan_framework_info 核对模块类名与 [DependsOn]；",
                    "2. 调 xihan_framework_doc 读 guide/modularity 与 guide/lifecycle，按模块生命周期在 ConfigureServices 等钩子里注册服务；",
                    "3. 需要配置时定义带 SectionName 常量的选项类并绑定配置节，键名对照 xihan_framework_config 的现有约定；",
                    "4. 交付完整代码：模块类、服务、选项类、需要的 NuGet 包引用与 appsettings 片段。"),
                new McpServerPromptCreateOptions { Name = "xihan-framework-module", Title = "写一个框架模块", Description = "按描述写一个 XiHan.Framework 模块。" }),
        ];
    }

    /// <summary>
    /// 技能正文，去掉 frontmatter
    /// </summary>
    public static string SkillBody(FrameworkKnowledge knowledge)
    {
        return MarkdownText.StripFrontmatter(knowledge.Skill.Replace("\r\n", "\n")).Trim();
    }

    private static McpServerTool Tool(string name, string title, string description, Delegate handler)
    {
        return McpServerTool.Create(
            handler,
            new McpServerToolCreateOptions { Name = name, Title = title, Description = description, ReadOnly = true, Idempotent = true, OpenWorld = false, Destructive = false });
    }

    private static CallToolResult Guard(Func<string> body)
    {
        try
        {
            return new CallToolResult { Content = [new TextContentBlock { Text = body() }] };
        }
        catch (QueryException error)
        {
            var near = error.Suggestions.Count == 0 ? string.Empty : $"\n\n候选：\n{string.Join('\n', error.Suggestions.Select(s => $"- {s}"))}";
            return new CallToolResult { Content = [new TextContentBlock { Text = error.Message + near }], IsError = true };
        }
    }
}
