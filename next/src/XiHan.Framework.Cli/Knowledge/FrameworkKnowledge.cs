// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Text.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 随工具嵌入的框架离线知识库。
/// </summary>
/// <remarks>
/// 包与特性清单（<c>catalog.json</c>）、配置节（<c>configuration.json</c>）与文档页（文档站、RFC 与约定、各包 README）都在构建时嵌入本程序集，数据与工具的版本一致（ADR-0076）。
/// </remarks>
internal sealed class FrameworkKnowledge
{
    /// <summary>
    /// 包与特性清单的资源名。
    /// </summary>
    public const string CatalogResource = "knowledge/catalog.json";

    /// <summary>
    /// 配置节索引的资源名。
    /// </summary>
    public const string ConfigurationResource = "knowledge/configuration.json";

    /// <summary>
    /// 文档页资源名的前缀，其后是仓库内路径。
    /// </summary>
    public const string DocsPrefix = "docs/";

    /// <summary>
    /// 摘要的最大长度。
    /// </summary>
    private const int MaxSummaryLength = 160;

    /// <summary>
    /// 创建。
    /// </summary>
    public FrameworkKnowledge(CatalogDocument catalog, ConfigurationDocument configuration, IReadOnlyList<DocPage> pages)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(pages);
        Packages = catalog.Packages;
        Features = catalog.Features;
        Sections = configuration.Sections;
        Pages = pages;
    }

    /// <summary>
    /// 数据对应的框架版本，即工具自身的版本。
    /// </summary>
    public static string Version => FrameworkInfo.Version;

    /// <summary>
    /// 全部正式包。
    /// </summary>
    public IReadOnlyList<PackageEntry> Packages { get; }

    /// <summary>
    /// 全部公开特性的清单。
    /// </summary>
    public IReadOnlyList<FeatureEntry> Features { get; }

    /// <summary>
    /// 全部配置节。
    /// </summary>
    public IReadOnlyList<ConfigSectionEntry> Sections { get; }

    /// <summary>
    /// 全部文档页，按路径排序。
    /// </summary>
    public IReadOnlyList<DocPage> Pages { get; }

    /// <summary>
    /// 从本程序集的嵌入资源读出。
    /// </summary>
    public static FrameworkKnowledge Load() => Load(typeof(FrameworkKnowledge).Assembly);

    /// <summary>
    /// 从给定程序集的嵌入资源读出。
    /// </summary>
    /// <remarks>
    /// 缺少索引资源是构建缺陷，抛 <see cref="InvalidOperationException"/>。
    /// </remarks>
    public static FrameworkKnowledge Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var catalog = JsonSerializer.Deserialize(ReadResource(assembly, CatalogResource), KnowledgeJsonContext.Files.CatalogDocument)
            ?? throw new InvalidOperationException($"Embedded resource '{CatalogResource}' is empty.");
        var configuration = JsonSerializer.Deserialize(ReadResource(assembly, ConfigurationResource), KnowledgeJsonContext.Files.ConfigurationDocument)
            ?? throw new InvalidOperationException($"Embedded resource '{ConfigurationResource}' is empty.");
        var pages = new List<DocPage>();
        // 资源名里的目录分隔符在 Windows 上构建时是反斜杠，统一成正斜杠再当路径用
        foreach (var name in assembly.GetManifestResourceNames().Select(static n => (Raw: n, Normalized: n.Replace('\\', '/'))).OrderBy(static n => n.Normalized, StringComparer.Ordinal))
        {
            if (!name.Normalized.StartsWith(DocsPrefix, StringComparison.Ordinal) || !name.Normalized.EndsWith(".md", StringComparison.Ordinal))
            {
                continue;
            }

            var page = ParsePage(name.Normalized[DocsPrefix.Length..], ReadResource(assembly, name.Raw));
            if (page.Markdown.Length > 0)
            {
                pages.Add(page);
            }
        }

        return new FrameworkKnowledge(catalog, configuration, pages);
    }

    /// <summary>
    /// 解析一页 Markdown。
    /// </summary>
    /// <remarks>
    /// 去掉开头的 frontmatter；标题取第一个一级标题，摘要取标题后第一段正文的首行。
    /// </remarks>
    public static DocPage ParsePage(string path, string content)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);
        var markdown = StripFrontmatter(content.Replace("\r\n", "\n", StringComparison.Ordinal)).Trim();
        var title = path;
        var summary = string.Empty;
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("# ", StringComparison.Ordinal) && title == path)
            {
                title = line[2..].Trim();
                continue;
            }

            // 摘要只取正文段落：跳过标题、引用、表格、代码围栏、列表、容器与图片
            if (line.Length == 0 || line[0] is '#' or '>' or '|' or '`' or '-' or '*' or ':' or '<' or '!' or '[')
            {
                continue;
            }

            summary = line.Replace("**", string.Empty, StringComparison.Ordinal);
            break;
        }

        return new DocPage(path, title, summary.Length > MaxSummaryLength ? summary[..MaxSummaryLength] + "…" : summary, markdown);
    }

    /// <summary>
    /// 去掉开头 <c>---</c> 包围的 frontmatter。
    /// </summary>
    private static string StripFrontmatter(string text)
    {
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return text;
        }

        var end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        return end < 0 ? text : text[(text.IndexOf('\n', end + 1) is var next and >= 0 ? next + 1 : text.Length)..];
    }

    /// <summary>
    /// 读一个嵌入资源的文本。
    /// </summary>
    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing from '{assembly.GetName().Name}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
