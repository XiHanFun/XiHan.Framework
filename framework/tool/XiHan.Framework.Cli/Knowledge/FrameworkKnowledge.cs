// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 随工具嵌入的离线知识库：包、模块、配置节、文档页与更新日志
/// </summary>
internal sealed class FrameworkKnowledge
{
    /// <summary>
    /// 文档站地址
    /// </summary>
    public const string Site = "https://framework.docs.xihanfun.com";

    private FrameworkKnowledge(
        string version,
        IReadOnlyList<PackageInfo> packages,
        IReadOnlyList<ConfigSection> sections,
        IReadOnlyList<DocPage> pages,
        IReadOnlyList<ChangelogSection> changelog,
        string skill)
    {
        Version = version;
        Packages = packages;
        Sections = sections;
        Pages = pages;
        Changelog = changelog;
        Skill = skill;
    }

    /// <summary>
    /// 数据对应的框架版本，即工具自身的版本
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 全部框架包
    /// </summary>
    public IReadOnlyList<PackageInfo> Packages { get; }

    /// <summary>
    /// 全部配置节
    /// </summary>
    public IReadOnlyList<ConfigSection> Sections { get; }

    /// <summary>
    /// 全部文档页（不含更新日志）
    /// </summary>
    public IReadOnlyList<DocPage> Pages { get; }

    /// <summary>
    /// 更新日志，新版本在前
    /// </summary>
    public IReadOnlyList<ChangelogSection> Changelog { get; }

    /// <summary>
    /// XiHan.Framework 使用技能 SKILL.md 的原文
    /// </summary>
    public string Skill { get; }

    /// <summary>
    /// 从程序集的嵌入资源读出全部知识
    /// </summary>
    public static FrameworkKnowledge Load(Assembly? assembly = null)
    {
        assembly ??= typeof(FrameworkKnowledge).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .ToDictionary(name => name.Replace('\\', '/'), name => Read(assembly, name), StringComparer.Ordinal);
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
        return Build(version, resources);
    }

    /// <summary>
    /// 由资源名到内容的映射构建知识库；资源名形如 docs/guide/caching.md、src/XiHan.Framework.Caching.csproj
    /// </summary>
    public static FrameworkKnowledge Build(string version, IReadOnlyDictionary<string, string> resources)
    {
        var pages = new List<DocPage>();
        string? changelog = null;
        foreach (var (name, content) in resources.Where(pair => pair.Key.StartsWith("docs/", StringComparison.Ordinal)).OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var relative = name["docs/".Length..];
            if (relative == "changelog.md")
            {
                changelog = content;
                continue;
            }

            if (relative == "index.md")
            {
                continue;
            }

            var markdown = MarkdownText.StripFrontmatter(content.Replace("\r\n", "\n")).Trim();
            var path = relative[..^".md".Length];
            path = path.EndsWith("/index", StringComparison.Ordinal) ? path[..^"/index".Length] : path;
            pages.Add(new DocPage(path, MarkdownText.Title(markdown) is { Length: > 0 } title ? title : path, MarkdownText.Summary(markdown), markdown));
        }

        var packagePages = pages
            .Where(page => page.Path.StartsWith("packages/", StringComparison.Ordinal))
            .Select(page => (Page: page, Id: MarkdownText.Field(page.Markdown, "NuGet")))
            .Where(pair => pair.Id is not null)
            .ToDictionary(pair => pair.Id!, pair => pair.Page, StringComparer.Ordinal);

        var modulesByProject = new Dictionary<string, List<ModuleInfo>>(StringComparer.Ordinal);
        var sections = new List<ConfigSection>();
        foreach (var (name, content) in resources.Where(pair => pair.Key.StartsWith("src/", StringComparison.Ordinal) && pair.Key.EndsWith(".cs", StringComparison.Ordinal)))
        {
            var project = name["src/".Length..].Split('/')[0];
            if (name.EndsWith("Module.cs", StringComparison.Ordinal))
            {
                if (!modulesByProject.TryGetValue(project, out var list))
                {
                    modulesByProject[project] = list = [];
                }

                list.AddRange(SourceParser.ParseModules(content));
            }
            else if (SourceParser.ParseOptions(content) is { } parsed)
            {
                var referenced = parsed.Options.Properties.Select(p => p.Type).ToList();
                var nested = parsed.Others
                    .Where(type => referenced.Any(propertyType => ContainsTypeName(propertyType, type.Name)))
                    .ToList();
                sections.Add(new ConfigSection { Section = parsed.Section, Package = project, Options = parsed.Options, NestedTypes = nested });
            }
        }

        var packages = new List<PackageInfo>();
        foreach (var (name, content) in resources.Where(pair => pair.Key.StartsWith("src/", StringComparison.Ordinal) && pair.Key.EndsWith(".csproj", StringComparison.Ordinal)))
        {
            var id = Path.GetFileNameWithoutExtension(name);
            var (description, frameworkDependencies, thirdParty) = SourceParser.ParseProject(content);
            packagePages.TryGetValue(id, out var page);
            packages.Add(new PackageInfo
            {
                Id = id,
                Description = description,
                Layer = page is null ? null : MarkdownText.Field(page.Markdown, "所在层"),
                Summary = page?.Summary,
                DocPath = page?.Path,
                Modules = modulesByProject.TryGetValue(id, out var modules) ? modules : [],
                FrameworkDependencies = frameworkDependencies,
                ThirdPartyDependencies = thirdParty,
            });
        }

        return new FrameworkKnowledge(
            version,
            packages.OrderBy(package => package.Id, StringComparer.Ordinal).ToList(),
            sections.OrderBy(section => section.Section, StringComparer.Ordinal).ToList(),
            pages,
            changelog is null ? [] : ChangelogParser.Parse(changelog),
            resources.TryGetValue("skills/xihan-framework/SKILL.md", out var skill) ? skill : string.Empty);
    }

    private static bool ContainsTypeName(string typeText, string name)
    {
        var index = typeText.IndexOf(name, StringComparison.Ordinal);
        while (index >= 0)
        {
            var before = index == 0 || !char.IsLetterOrDigit(typeText[index - 1]);
            var end = index + name.Length;
            var after = end == typeText.Length || !char.IsLetterOrDigit(typeText[end]);
            if (before && after)
            {
                return true;
            }

            index = typeText.IndexOf(name, end, StringComparison.Ordinal);
        }

        return false;
    }

    private static string Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"读不到嵌入资源 {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// 更新日志按版本切段
/// </summary>
internal static class ChangelogParser
{
    /// <summary>
    /// 把「## v1.2.3 (日期)」开头的各段切出来
    /// </summary>
    public static IReadOnlyList<ChangelogSection> Parse(string markdown)
    {
        var sections = new List<ChangelogSection>();
        var parts = markdown.Replace("\r\n", "\n").Split("\n## ");
        foreach (var part in parts.Skip(1))
        {
            var newline = part.IndexOf('\n');
            var heading = (newline < 0 ? part : part[..newline]).Trim();
            var tokens = heading.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                continue;
            }

            var version = tokens[0].TrimStart('v');
            var date = tokens.Length > 1 ? tokens[1].Trim('(', ')', ' ') : string.Empty;
            sections.Add(new ChangelogSection(version, date, newline < 0 ? string.Empty : part[(newline + 1)..].Trim()));
        }

        return sections;
    }
}
