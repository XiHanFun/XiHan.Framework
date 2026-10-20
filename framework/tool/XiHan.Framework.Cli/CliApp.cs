// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol.Server;
using XiHan.Framework.Cli.Integration;
using XiHan.Framework.Cli.Knowledge;
using XiHan.Framework.Cli.Mcp;

namespace XiHan.Framework.Cli;

/// <summary>
/// 命令行的输入输出环境
/// </summary>
/// <param name="Out">标准输出</param>
/// <param name="Error">标准错误</param>
/// <param name="WorkingDirectory">识别项目所用的目录</param>
internal sealed record CliEnvironment(TextWriter Out, TextWriter Error, string WorkingDirectory);

/// <summary>
/// 命令行入口：解析参数、分派命令、按 markdown 或 json 输出
/// </summary>
internal static class CliApp
{
    /// <summary>
    /// 帮助文本
    /// </summary>
    public const string Help = """
        xihan-framework —— XiHan.Framework 的离线知识库与 MCP 服务端

        用法：xihan-framework <命令> [参数] [选项]

        查询
          list [--layer <层>] [--query <关键字>]          按层列出框架包与模块类
          info <包|模块>                                  安装、模块依赖、包依赖与配置节
          doc [<页面|包>]                                 文档页全文；不带参数列出全部页面
          config [<配置节|包|选项类>]                     配置节的属性、类型与缺省值；不带参数列出全部
          search <关键字…>                                在全部文档里检索
          changelog [<版本> [<版本>]] [--keyword <词>]    更新日志，两个版本时取区间

        集成
          mcp                                             以 stdio 启动 MCP 服务端
          setup --client <claude|cursor|vscode|codex>     写入 MCP 配置与技能
                [--mode <mcp|skill|both>] [--pin <版本>] [--dry-run] [--check]

        选项
          --format <markdown|json>   输出格式，缺省 markdown
          -v, --version              打印版本
          -h, --help                 打印本帮助

        包名可写 XiHan.Framework.Caching、Caching 或模块类名 XiHanCachingModule；页面写站内路径，如 guide/caching。
        """;

    private static readonly HashSet<string> Flags = ["--dry-run", "--check", "--help", "-h", "--version", "-v"];

    private static readonly HashSet<string> ValueOptions = ["--layer", "--query", "--keyword", "--format", "--client", "--mode", "--pin"];

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// 运行一次命令，返回退出码：0 成功，1 查不到或核对未通过，2 用法错误
    /// </summary>
    public static async Task<int> RunAsync(string[] args, CliEnvironment environment, FrameworkKnowledge? knowledge = null, CancellationToken cancellationToken = default)
    {
        var positionals = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (Flags.Contains(arg))
            {
                options[arg.TrimStart('-')] = "true";
            }
            else if (ValueOptions.Contains(arg))
            {
                if (i + 1 >= args.Length)
                {
                    await environment.Error.WriteLineAsync($"{arg} 后面要跟取值");
                    return 2;
                }

                options[arg[2..]] = args[++i];
            }
            else if (arg.StartsWith('-'))
            {
                await environment.Error.WriteLineAsync($"不认识的选项：{arg}\n\n{Help}");
                return 2;
            }
            else
            {
                positionals.Add(arg);
            }
        }

        if (options.ContainsKey("help") || options.ContainsKey("h") || (positionals.Count == 0 && !options.ContainsKey("version") && !options.ContainsKey("v")))
        {
            await environment.Out.WriteLineAsync(Help);
            return 0;
        }

        knowledge ??= FrameworkKnowledge.Load();
        if (options.ContainsKey("version") || options.ContainsKey("v"))
        {
            await environment.Out.WriteLineAsync(knowledge.Version);
            return 0;
        }

        var command = positionals[0];
        var rest = positionals.Skip(1).ToList();
        var project = ProjectDetector.Detect(environment.WorkingDirectory);
        var notice = ProjectDetector.VersionNotice(project, knowledge.Version);
        try
        {
            if (command == "mcp")
            {
                if (notice is not null)
                {
                    await environment.Error.WriteLineAsync(notice);
                }

                var instructions = $"XiHan.Framework {knowledge.Version} 的离线知识库。写代码前用 xihan_framework_info 核对包与模块类、用 xihan_framework_config 核对配置键，用 xihan_framework_search / xihan_framework_doc 查用法。"
                    + (notice is null ? string.Empty : $"\n{notice}");
                await using var transport = new StdioServerTransport(McpHost.ServerName);
                await McpHost.RunAsync(transport, McpHost.CreateOptions(knowledge, instructions), cancellationToken);
                return 0;
            }

            if (command == "setup")
            {
                return await SetupAsync(options, knowledge, project, environment);
            }

            var format = options.GetValueOrDefault("format", "markdown");
            if (format is not ("markdown" or "json"))
            {
                await environment.Error.WriteLineAsync($"--format 只能是 markdown / json，收到 {format}");
                return 2;
            }

            var (markdown, json) = Query(command, rest, options, knowledge);
            if (notice is not null)
            {
                await environment.Error.WriteLineAsync(notice);
            }

            await environment.Out.WriteLineAsync(format == "json" ? JsonSerializer.Serialize(json, Json) : markdown);
            return 0;
        }
        catch (QueryException error)
        {
            await environment.Error.WriteLineAsync(error.Message);
            if (error.Suggestions.Count > 0)
            {
                await environment.Error.WriteLineAsync($"候选：\n{string.Join('\n', error.Suggestions.Select(s => $"  {s}"))}");
            }

            return 1;
        }
        catch (UsageException error)
        {
            await environment.Error.WriteLineAsync(error.Message);
            return 2;
        }
        catch (InvalidOperationException error)
        {
            await environment.Error.WriteLineAsync(error.Message);
            return 1;
        }
    }

    private static (string Markdown, object? Json) Query(string command, List<string> rest, Dictionary<string, string> options, FrameworkKnowledge knowledge)
    {
        switch (command)
        {
            case "list":
            {
                var groups = Queries.ListPackages(knowledge, options.GetValueOrDefault("layer"), options.GetValueOrDefault("query"));
                return (Rendering.PackageList(knowledge, groups), groups.Select(g => new { layer = g.Layer, packages = g.Packages }));
            }

            case "info":
            {
                var package = Queries.RequirePackage(knowledge, Require(rest, "info <包|模块>"));
                return (Rendering.PackageInfo(knowledge, package), new { package, sections = knowledge.Sections.Where(s => s.Package == package.Id) });
            }

            case "doc":
            {
                if (rest.Count == 0)
                {
                    return (Rendering.PageList(knowledge), knowledge.Pages.Select(p => new { p.Path, p.Title, p.Summary }));
                }

                var page = Queries.RequirePage(knowledge, rest[0]);
                return (page.Markdown, page);
            }

            case "config":
            {
                var query = rest.Count == 0 ? null : string.Join(' ', rest);
                var sections = Queries.Config(knowledge, query);
                return (Rendering.ConfigSections(sections, query is not null), sections);
            }

            case "search":
            {
                var query = string.Join(' ', rest);
                if (query.Length == 0)
                {
                    throw new UsageException("缺少参数。用法：xihan-framework search <关键字…>");
                }

                var results = Queries.Search(knowledge, query);
                return (Rendering.SearchResults(query, results), results.Select(r => new { r.Page.Path, r.Page.Title, r.Page.Summary, r.Score, r.Snippet }));
            }

            case "changelog":
            {
                var sections = Queries.Changelog(knowledge, rest.ElementAtOrDefault(0), rest.ElementAtOrDefault(1), options.GetValueOrDefault("keyword"));
                return (Rendering.Changelog(sections), sections);
            }

            default:
                throw new UsageException($"未知命令：{command}。xihan-framework --help 查看全部命令");
        }
    }

    private static async Task<int> SetupAsync(Dictionary<string, string> options, FrameworkKnowledge knowledge, ProjectInfo project, CliEnvironment environment)
    {
        var client = options.GetValueOrDefault("client");
        if (client is null || !SetupPlanner.Clients.Contains(client))
        {
            throw new UsageException($"--client 只能是 {string.Join(" / ", SetupPlanner.Clients)}");
        }

        var mode = options.GetValueOrDefault("mode", "both");
        if (!SetupPlanner.Modes.Contains(mode))
        {
            throw new UsageException($"--mode 只能是 {string.Join(" / ", SetupPlanner.Modes)}");
        }

        if (project.Root is null)
        {
            throw new UsageException("当前目录及其上级都没有项目（.git、解决方案或项目文件），在项目根目录里运行 setup");
        }

        var version = options.GetValueOrDefault("pin") ?? project.FrameworkVersion ?? knowledge.Version;
        var actions = SetupPlanner.Plan(client, mode, project.Root, version, OperatingSystem.IsWindows(), knowledge.Skill);
        if (options.ContainsKey("check"))
        {
            var problems = actions.Select(SetupPlanner.Check).OfType<string>().ToList();
            foreach (var problem in problems)
            {
                await environment.Error.WriteLineAsync($"✗ {problem}");
            }

            if (problems.Count > 0)
            {
                return 1;
            }

            await environment.Out.WriteLineAsync($"✓ {client} 的 XiHan.Framework 配置已就绪（XiHan.Framework.Cli@{version}）");
            return 0;
        }

        var dryRun = options.ContainsKey("dry-run");
        foreach (var action in actions)
        {
            if (!dryRun)
            {
                SetupPlanner.Apply(action);
            }

            await environment.Out.WriteLineAsync($"{(dryRun || action.Kind == "print" ? string.Empty : "✓ ")}{SetupPlanner.Describe(action)}");
        }

        return 0;
    }

    private static string Require(List<string> rest, string usage)
    {
        return rest.Count > 0 ? rest[0] : throw new UsageException($"缺少参数。用法：xihan-framework {usage}");
    }

    private sealed class UsageException(string message) : Exception(message);
}
