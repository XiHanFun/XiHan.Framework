// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 解析随工具嵌入的项目文件、模块类与选项类源码
/// </summary>
internal static partial class SourceParser
{
    /// <summary>
    /// 读项目文件：包说明、引用的框架包与第三方包
    /// </summary>
    public static (string Description, IReadOnlyList<string> FrameworkDependencies, IReadOnlyList<PackageDependency> ThirdParty) ParseProject(string xml)
    {
        var document = XDocument.Parse(xml);
        var description = document.Descendants("Description").FirstOrDefault()?.Value.Trim() ?? string.Empty;
        var framework = document.Descendants("ProjectReference")
            .Select(node => (string?)node.Attribute("Include"))
            .OfType<string>()
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .Where(name => name.StartsWith("XiHan.Framework.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
        var thirdParty = document.Descendants("PackageReference")
            .Select(node => new PackageDependency((string?)node.Attribute("Include") ?? string.Empty, (string?)node.Attribute("Version") ?? string.Empty))
            .Where(dependency => dependency.Name.Length > 0)
            .OrderBy(dependency => dependency.Name, StringComparer.Ordinal)
            .ToList();
        return (description, framework, thirdParty);
    }

    /// <summary>
    /// 读模块类：类名与 [DependsOn] 列出的模块
    /// </summary>
    public static IReadOnlyList<ModuleInfo> ParseModules(string source)
    {
        var modules = new List<ModuleInfo>();
        foreach (Match match in ModuleClassRegex().Matches(source))
        {
            var name = match.Groups["name"].Value;
            var attributeStart = source.LastIndexOf("[DependsOn(", match.Index, StringComparison.Ordinal);
            var dependsOn = new List<string>();
            if (attributeStart >= 0 && IsAttributeOf(source, attributeStart, match.Index))
            {
                var attributeEnd = source.IndexOf(")]", attributeStart, StringComparison.Ordinal);
                var body = source[attributeStart..attributeEnd];
                dependsOn.AddRange(TypeofRegex().Matches(body).Select(hit => hit.Groups[1].Value));
            }

            modules.Add(new ModuleInfo(name, dependsOn));
        }

        return modules;
    }

    /// <summary>
    /// 读选项类：带 SectionName 常量的类型及同文件里的其他公开类型
    /// </summary>
    public static (string Section, OptionType Options, IReadOnlyList<OptionType> Others)? ParseOptions(string source)
    {
        var section = SectionNameRegex().Match(source);
        if (!section.Success)
        {
            return null;
        }

        var types = ParseTypes(source);
        var owner = types.LastOrDefault(type => type.Start < section.Index);
        if (owner is null)
        {
            return null;
        }

        var others = types.Where(type => type != owner).Select(type => type.ToOptionType()).ToList();
        return (section.Groups[1].Value, owner.ToOptionType(), others);
    }

    /// <summary>
    /// 把 XML 文档注释压成一段纯文本
    /// </summary>
    public static string CleanDocComment(IEnumerable<string> lines)
    {
        var text = string.Join(' ', lines.Select(line => line.Trim().TrimStart('/').Trim()));
        var summary = SummaryRegex().Match(text);
        var body = summary.Success ? summary.Groups[1].Value : string.Empty;
        var remarks = RemarksRegex().Match(text);
        if (remarks.Success)
        {
            body = $"{body} {remarks.Groups[1].Value}";
        }

        body = CrefRegex().Replace(body, hit => hit.Groups[1].Value.Split('.').Last());
        body = TagRegex().Replace(body, string.Empty);
        return WhitespaceRegex().Replace(body, " ").Trim();
    }

    private static bool IsAttributeOf(string source, int attributeStart, int classStart)
    {
        var between = source[attributeStart..classStart];
        var close = between.IndexOf(")]", StringComparison.Ordinal);
        return close >= 0 && !between[(close + 2)..].Contains("class ", StringComparison.Ordinal);
    }

    private static List<ParsedType> ParseTypes(string source)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var types = new List<ParsedType>();
        var stack = new Stack<(ParsedType Type, int Depth)>();
        var docs = new List<string>();
        var depth = 0;
        var offset = 0;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("///", StringComparison.Ordinal))
            {
                docs.Add(line);
            }
            else if (line.Length > 0 && !line.StartsWith('['))
            {
                var typeMatch = TypeDeclarationRegex().Match(line);
                if (typeMatch.Success)
                {
                    var type = new ParsedType(typeMatch.Groups["name"].Value, CleanDocComment(docs), offset);
                    types.Add(type);
                    stack.Push((type, depth));
                }
                else if (stack.Count > 0 && depth == stack.Peek().Depth + 1)
                {
                    var property = PropertyRegex().Match(line);
                    if (property.Success)
                    {
                        var initializer = property.Groups["init"].Success ? property.Groups["init"].Value.Trim() : null;
                        if (initializer is not null && !line.EndsWith(';'))
                        {
                            initializer += " …";
                        }

                        stack.Peek().Type.Properties.Add(new OptionProperty(
                            property.Groups["name"].Value,
                            WhitespaceRegex().Replace(property.Groups["type"].Value.Trim(), " "),
                            initializer,
                            CleanDocComment(docs)));
                    }
                }

                docs.Clear();
            }

            foreach (var ch in raw)
            {
                if (ch == '{')
                {
                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                    while (stack.Count > 0 && depth <= stack.Peek().Depth)
                    {
                        stack.Pop();
                    }
                }
            }

            offset += raw.Length + 1;
        }

        return types;
    }

    [GeneratedRegex(@"public\s+(?:sealed\s+|abstract\s+)?class\s+(?<name>\w+Module)\s*:\s*\w*Module\b")]
    private static partial Regex ModuleClassRegex();

    [GeneratedRegex(@"typeof\(\s*([\w.]+)\s*\)")]
    private static partial Regex TypeofRegex();

    [GeneratedRegex("""const\s+string\s+SectionName\s*=\s*"([^"]+)"\s*;""")]
    private static partial Regex SectionNameRegex();

    [GeneratedRegex(@"^public\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*(?:class|record|record\s+class)\s+(?<name>\w+)")]
    private static partial Regex TypeDeclarationRegex();

    [GeneratedRegex(@"^public\s+(?:required\s+|virtual\s+|override\s+)*(?<type>[\w<>\[\]?,.\s]+?)\s+(?<name>\w+)\s*\{\s*get;\s*(?:set|init);\s*\}(?:\s*=\s*(?<init>[^;]+))?;?\s*$")]
    private static partial Regex PropertyRegex();

    [GeneratedRegex("<summary>(.*?)</summary>")]
    private static partial Regex SummaryRegex();

    [GeneratedRegex("<remarks>(.*?)</remarks>")]
    private static partial Regex RemarksRegex();

    [GeneratedRegex("""<see(?:also)?\s+(?:cref|langword|href)="([^"]+)"\s*/>""")]
    private static partial Regex CrefRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed class ParsedType(string name, string summary, int start)
    {
        public string Name { get; } = name;

        public string Summary { get; } = summary;

        public int Start { get; } = start;

        public List<OptionProperty> Properties { get; } = [];

        public OptionType ToOptionType() => new(Name, Summary, Properties);
    }
}

/// <summary>
/// Markdown 文本的标题与摘要
/// </summary>
internal static partial class MarkdownText
{
    /// <summary>
    /// 去掉开头的 frontmatter
    /// </summary>
    public static string StripFrontmatter(string markdown)
    {
        var match = FrontmatterRegex().Match(markdown);
        return match.Success ? markdown[match.Length..] : markdown;
    }

    /// <summary>
    /// 一级标题的纯文本
    /// </summary>
    public static string Title(string markdown)
    {
        var match = HeadingRegex().Match(markdown);
        return match.Success ? match.Groups[1].Value.Replace("`", string.Empty).Trim() : string.Empty;
    }

    /// <summary>
    /// 摘要：引用块或一级标题后第一段正文的第一句
    /// </summary>
    public static string Summary(string markdown)
    {
        foreach (var block in WithoutFences(markdown).Split("\n\n"))
        {
            var paragraph = block.Trim();
            if (paragraph.Length == 0 || paragraph.StartsWith('#') || paragraph.StartsWith('<') || paragraph.StartsWith(":::", StringComparison.Ordinal)
                || paragraph.StartsWith('|') || paragraph.StartsWith("- ", StringComparison.Ordinal) || paragraph.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            var flat = LinkRegex().Replace(paragraph.Replace("> ", string.Empty).Replace("**", string.Empty), "$1");
            flat = WhitespaceRegex().Replace(flat, " ").Trim();
            var stop = flat.IndexOf('。');
            if (stop is >= 0 and < 200)
            {
                return flat[..(stop + 1)];
            }

            return flat.Length > 160 ? $"{flat[..160]}…" : flat;
        }

        return string.Empty;
    }

    /// <summary>
    /// 页面开头 「- **键**：值」 形式的字段
    /// </summary>
    public static string? Field(string markdown, string key)
    {
        var match = Regex.Match(markdown, $@"^- \*\*{Regex.Escape(key)}\*\*[：:]\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Replace("`", string.Empty).Trim() : null;
    }

    /// <summary>
    /// 去掉围栏代码块，按空行切段时不会切进代码
    /// </summary>
    public static string WithoutFences(string markdown)
    {
        var builder = new StringBuilder();
        var fence = string.Empty;
        foreach (var line in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.TrimStart();
            var marker = FenceMarker(trimmed);
            if (fence.Length == 0 && marker is not null)
            {
                fence = marker;
                continue;
            }

            if (fence.Length > 0)
            {
                if (marker is not null && marker[0] == fence[0] && marker.Length >= fence.Length && trimmed[marker.Length..].Trim().Length == 0)
                {
                    fence = string.Empty;
                }

                continue;
            }

            builder.Append(line).Append('\n');
        }

        return builder.ToString();
    }

    private static string? FenceMarker(string trimmed)
    {
        if (trimmed.Length < 3 || (trimmed[0] != '`' && trimmed[0] != '~'))
        {
            return null;
        }

        var length = 0;
        while (length < trimmed.Length && trimmed[length] == trimmed[0])
        {
            length++;
        }

        return length >= 3 ? trimmed[..length] : null;
    }

    [GeneratedRegex(@"^---\r?\n[\s\S]*?\r?\n---\r?\n?")]
    private static partial Regex FrontmatterRegex();

    [GeneratedRegex(@"^# (.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
