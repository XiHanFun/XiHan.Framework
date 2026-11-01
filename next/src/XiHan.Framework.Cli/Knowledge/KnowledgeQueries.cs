// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 知识库的三类查询：包与能力、配置节、文档。
/// </summary>
/// <remarks>
/// 查不到时返回 <see cref="CliErrors.KnowledgeNotFound"/>，文案里带上最接近的候选，编码助手据此改写名字再查。
/// </remarks>
internal static class KnowledgeQueries
{
    /// <summary>
    /// 检索结果的缺省条数。
    /// </summary>
    public const int DefaultLimit = 10;

    /// <summary>
    /// 检索结果的最大条数。
    /// </summary>
    public const int MaxLimit = 50;

    /// <summary>
    /// 包 ID 的公共前缀。
    /// </summary>
    private const string PackagePrefix = "XiHan.Framework.";

    /// <summary>
    /// 候选的最大个数。
    /// </summary>
    private const int MaxCandidates = 8;

    /// <summary>
    /// 摘录的最大长度。
    /// </summary>
    private const int MaxSnippetLength = 160;

    /// <summary>
    /// 查包、特性或能力。
    /// </summary>
    /// <remarks>
    /// 依次按包（ID、短名、文档页名、清单 ID、特性类型名）与能力 ID 匹配；名字为空时列出全部包。
    /// </remarks>
    public static Result<InfoResponse> Info(FrameworkKnowledge knowledge, string? name)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        if (string.IsNullOrWhiteSpace(name))
        {
            var index = knowledge.Packages
                .Select(p => new PackageSummary(p.Id, p.Ring, p.Summary, knowledge.Features.Where(f => f.Package == p.Id).Select(static f => f.Id).ToList()))
                .ToList();
            return new InfoResponse(FrameworkKnowledge.Version, null, index, null);
        }

        var text = name.Trim();
        if (FindPackage(knowledge, text) is { } package)
        {
            return new InfoResponse(FrameworkKnowledge.Version, [ViewOf(knowledge, package)], null, null);
        }

        if (FindCapability(knowledge, text) is { } capability)
        {
            return new InfoResponse(FrameworkKnowledge.Version, null, null, capability);
        }

        var candidates = knowledge.Packages.Select(static p => p.Id)
            .Concat(knowledge.Features.Select(static f => f.Id))
            .Concat(knowledge.Features.SelectMany(static f => f.Provides.Select(static p => p[..p.IndexOf('@', StringComparison.Ordinal)])))
            .Distinct(StringComparer.Ordinal);
        return NotFound(text, Suggest(text, candidates));
    }

    /// <summary>
    /// 查配置节。
    /// </summary>
    /// <remarks>
    /// 依次按节路径（含子节）、完整键路径、包、Options 类名与键名或说明里的文字匹配；查询为空时列出全部配置节。
    /// </remarks>
    public static Result<ConfigResponse> Config(FrameworkKnowledge knowledge, string? query)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        if (string.IsNullOrWhiteSpace(query))
        {
            var index = knowledge.Sections.Select(static s => new SectionSummary(s.Section, s.Package, s.Options, s.Keys.Count)).ToList();
            return new ConfigResponse(FrameworkKnowledge.Version, null, index);
        }

        var text = query.Trim();
        var bySection = knowledge.Sections
            .Where(s => s.Section.Equals(text, StringComparison.OrdinalIgnoreCase) || s.Section.StartsWith(text + ":", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (bySection.Count > 0)
        {
            return Sections(bySection.Select(static s => (s, s.Keys)));
        }

        // 完整键路径：键按占位符逐段匹配（绝对键可能登记在别的节下，所以查全部节）
        var byPath = knowledge.Sections
            .Select(s => (Section: s, Keys: (IReadOnlyList<ConfigKeyEntry>)s.Keys.Where(k => PathMatches(PathOf(s.Section, k), text)).ToList()))
            .Where(static m => m.Keys.Count > 0)
            .ToList();
        if (byPath.Count > 0)
        {
            return Sections(byPath);
        }

        if (FindPackage(knowledge, text) is { } package)
        {
            var own = knowledge.Sections.Where(s => s.Package == package.Id).ToList();
            return own.Count > 0
                ? Sections(own.Select(static s => (s, s.Keys)))
                : Error.NotFound(CliErrors.KnowledgeNotFound, CliMessages.KnowledgeNoSections, MessageArguments.Of(("package", package.Id)));
        }

        var byOptions = knowledge.Sections
            .Where(s => s.Options.Equals(text, StringComparison.OrdinalIgnoreCase) || s.Options.EndsWith("." + text, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (byOptions.Count > 0)
        {
            return Sections(byOptions.Select(static s => (s, s.Keys)));
        }

        // 最后按键名与说明里的文字找：只列出命中的键
        var byText = knowledge.Sections
            .Select(s => (Section: s, Keys: (IReadOnlyList<ConfigKeyEntry>)s.Keys.Where(k => k.Key.Contains(text, StringComparison.OrdinalIgnoreCase) || k.Summary.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList()))
            .Where(static m => m.Keys.Count > 0)
            .ToList();
        if (byText.Count > 0)
        {
            return Sections(byText);
        }

        return NotFound(text, Suggest(text, knowledge.Sections.Select(static s => s.Section)));
    }

    /// <summary>
    /// 检索文档，或取一页全文。
    /// </summary>
    /// <remarks>
    /// 给了页面时取全文；否则按检索词计分（标题 20、小节标题 5、正文每行 1，每个词封顶 60），所有词都要出现。
    /// </remarks>
    public static Result<SearchResponse> Search(FrameworkKnowledge knowledge, string? query, string? page, int? limit)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        if (!string.IsNullOrWhiteSpace(page))
        {
            var found = FindPage(knowledge, page.Trim());
            return found.TryGetValue(out var document) ? new SearchResponse(FrameworkKnowledge.Version, null, document) : found.Error!;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return Error.Validation(CliErrors.KnowledgeSearchInvalid, CliMessages.KnowledgeQueryRequired);
        }

        var take = limit ?? DefaultLimit;
        if (take is < 1 or > MaxLimit)
        {
            return Error.Validation(CliErrors.KnowledgeSearchInvalid, CliMessages.KnowledgeLimitInvalid, MessageArguments.Of(("limit", take), ("max", MaxLimit)));
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hits = new List<SearchHit>();
        foreach (var document in knowledge.Pages)
        {
            if (Score(document, terms) is { } hit)
            {
                hits.Add(hit);
            }
        }

        if (hits.Count == 0)
        {
            return NotFound(query.Trim(), []);
        }

        return new SearchResponse(FrameworkKnowledge.Version, hits.OrderByDescending(static h => h.Score).ThenBy(static h => h.Path, StringComparer.Ordinal).Take(take).ToList(), null);
    }

    /// <summary>
    /// 按包 ID、短名、文档页名、清单 ID 或特性类型名找包。
    /// </summary>
    public static PackageEntry? FindPackage(FrameworkKnowledge knowledge, string name)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentNullException.ThrowIfNull(name);
        var exact = knowledge.Packages.FirstOrDefault(p => p.Id.Equals(name, StringComparison.OrdinalIgnoreCase) || p.Id.Equals(PackagePrefix + name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        // 短名与文档页名：去掉前缀与分隔符后比较，Caching.Redis、caching-redis、packages/caching-redis 都指向同一个包
        var slug = name.StartsWith("packages/", StringComparison.OrdinalIgnoreCase) ? name["packages/".Length..] : name;
        slug = slug.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? slug[..^3] : slug;
        var key = Normalize(slug);
        var byKey = knowledge.Packages.FirstOrDefault(p => Normalize(p.Id) == key);
        if (byKey is not null)
        {
            return byKey;
        }

        var feature = knowledge.Features.FirstOrDefault(f => f.Id.Equals(name, StringComparison.OrdinalIgnoreCase)
            || f.Type.Equals(name, StringComparison.OrdinalIgnoreCase)
            || SimpleTypeName(f.Type).Equals(name, StringComparison.OrdinalIgnoreCase));
        return feature is null ? null : knowledge.Packages.FirstOrDefault(p => p.Id == feature.Package);
    }

    /// <summary>
    /// 按仓库内路径、文件名、RFC 编号或包 ID 找一页文档。
    /// </summary>
    public static Result<DocPage> FindPage(FrameworkKnowledge knowledge, string name)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentNullException.ThrowIfNull(name);
        var path = name.Replace('\\', '/').Trim('/');
        string[] candidates =
        [
            path,
            path + ".md",
            "docs-next/" + path,
            "docs-next/" + path + ".md",
            "next/spec/rfcs/" + path,
            "next/spec/rfcs/" + path + ".md",
            "next/spec/conventions/" + path,
            "next/spec/conventions/" + path + ".md",
        ];
        foreach (var candidate in candidates)
        {
            var exact = knowledge.Pages.FirstOrDefault(p => p.Path.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }

        if (FindPackage(knowledge, path) is { } package
            && knowledge.Pages.FirstOrDefault(p => p.Path == ReadmePath(package.Id)) is { } readme)
        {
            return readme;
        }

        // 只写文件名或 RFC 编号（0006、rfc-0006）时取唯一的那一页
        var number = path.StartsWith("rfc-", StringComparison.OrdinalIgnoreCase) ? path[4..] : path;
        var tail = knowledge.Pages
            .Where(p => p.Path.EndsWith("/" + path, StringComparison.OrdinalIgnoreCase)
                || p.Path.EndsWith("/" + path + ".md", StringComparison.OrdinalIgnoreCase)
                || (number.Length == 4 && number.All(char.IsAsciiDigit) && p.Path.StartsWith("next/spec/rfcs/" + number + "-", StringComparison.Ordinal)))
            .ToList();
        if (tail.Count == 1)
        {
            return tail[0];
        }

        var near = tail.Select(static p => p.Path)
            .Concat(knowledge.Pages.Where(p => p.Path.Contains(path, StringComparison.OrdinalIgnoreCase)).Select(static p => p.Path))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxCandidates)
            .ToList();
        return NotFound(name, near.Count > 0 ? near : Suggest(path, knowledge.Pages.Select(static p => p.Path)));
    }

    /// <summary>
    /// 包 README 的路径。
    /// </summary>
    public static string ReadmePath(string packageId) => "next/src/" + packageId + "/README.md";

    /// <summary>
    /// 包在文档站的参考页路径。
    /// </summary>
    /// <remarks>
    /// <c>XiHan.Framework.Caching.Redis</c> → <c>docs-next/packages/caching-redis.md</c>。
    /// </remarks>
    public static string PackagePagePath(string packageId)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        var name = packageId.StartsWith(PackagePrefix, StringComparison.Ordinal) ? packageId[PackagePrefix.Length..] : packageId;
        return "docs-next/packages/" + name.Replace('.', '-').ToLowerInvariant() + ".md";
    }

    /// <summary>
    /// 一个包的详情。
    /// </summary>
    private static PackageView ViewOf(FrameworkKnowledge knowledge, PackageEntry package)
    {
        var docs = new[] { PackagePagePath(package.Id), ReadmePath(package.Id) }
            .Where(path => knowledge.Pages.Any(p => p.Path == path))
            .ToList();
        return new PackageView(
            package.Id,
            package.Ring,
            package.Summary,
            package.Description,
            $"dotnet add package {package.Id} --version {FrameworkKnowledge.Version}",
            package.References,
            package.Dependencies,
            package.FrameworkReferences,
            knowledge.Features.Where(f => f.Package == package.Id).ToList(),
            knowledge.Sections.Where(s => s.Package == package.Id).Select(static s => s.Section).ToList(),
            docs);
    }

    /// <summary>
    /// 按能力 ID 找提供者与依赖方。
    /// </summary>
    /// <remarks>
    /// 名字可带 <c>@版本</c> 与可选标记 <c>?</c>，只按 ID 匹配。
    /// </remarks>
    private static CapabilityView? FindCapability(FrameworkKnowledge knowledge, string name)
    {
        var at = name.IndexOf('@', StringComparison.Ordinal);
        var id = (at >= 0 ? name[..at] : name).TrimEnd('?').ToLowerInvariant();
        var providers = new List<CapabilityProvider>();
        var consumers = new List<CapabilityConsumer>();
        foreach (var feature in knowledge.Features)
        {
            foreach (var provided in feature.Provides)
            {
                var separator = provided.IndexOf('@', StringComparison.Ordinal);
                if (string.Equals(provided[..separator], id, StringComparison.Ordinal))
                {
                    providers.Add(new CapabilityProvider(feature.Id, feature.Type, feature.Package, feature.Role, int.Parse(provided[(separator + 1)..], CultureInfo.InvariantCulture)));
                }
            }

            consumers.AddRange(feature.Requires.Where(r => r.Id == id).Select(r => new CapabilityConsumer(feature.Id, feature.Package, r.Version, r.Optional)));
        }

        return providers.Count == 0 && consumers.Count == 0 ? null : new CapabilityView(id, providers, consumers);
    }

    /// <summary>
    /// 把命中的节与键组装成输出。
    /// </summary>
    private static ConfigResponse Sections(IEnumerable<(ConfigSectionEntry Section, IReadOnlyList<ConfigKeyEntry> Keys)> matches)
    {
        var views = matches.Select(static m => new SectionView(
                m.Section.Section,
                m.Section.Options,
                m.Section.Package,
                m.Section.Binding,
                m.Keys.Select(k => new KeyView(PathOf(m.Section.Section, k), k.Type, k.Default, k.Required, k.Minimum, k.ExclusiveMinimum, k.Maximum, k.Values, k.Summary)).ToList(),
                Appsettings(m.Section.Section, m.Keys)))
            .ToList();
        return new ConfigResponse(FrameworkKnowledge.Version, views, null);
    }

    /// <summary>
    /// 按缺省值与必填键的示例写出 appsettings 片段。
    /// </summary>
    /// <remarks>
    /// 只含没有占位符、且有缺省值或是必填的键；数值与布尔按 JSON 类型写。
    /// </remarks>
    public static string Appsettings(string section, IReadOnlyList<ConfigKeyEntry> keys)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(keys);
        var root = new JsonObject();
        foreach (var key in keys)
        {
            var value = key.Default ?? (key.Required ? key.Sample : null);
            if (value is null || key.Key.Contains('{', StringComparison.Ordinal))
            {
                continue;
            }

            var segments = PathOf(section, key).Split(':');
            var node = root;
            foreach (var segment in segments[..^1])
            {
                if (node[segment] is not JsonObject child)
                {
                    child = [];
                    node[segment] = child;
                }

                node = child;
            }

            node[segments[^1]] = key.Type switch
            {
                "integer" or "number" when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
                "boolean" when bool.TryParse(value, out var flag) => JsonValue.Create(flag),
                _ => JsonValue.Create(value),
            };
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            root.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// 键的完整路径。
    /// </summary>
    /// <remarks>
    /// 以 <c>XiHan:</c> 开头的键是绝对路径（Options 类读了自己节以外的键），其余接在节后。
    /// </remarks>
    public static string PathOf(string section, ConfigKeyEntry key)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(key);
        return key.Key.StartsWith("XiHan:", StringComparison.Ordinal) ? key.Key : section + ":" + key.Key;
    }

    /// <summary>
    /// 键的模式（占位符匹配任意一段）是否与路径相同。
    /// </summary>
    private static bool PathMatches(string pattern, string path)
    {
        var expected = pattern.Split(':');
        var actual = path.Split(':');
        if (expected.Length != actual.Length)
        {
            return false;
        }

        for (var index = 0; index < expected.Length; index++)
        {
            if (!(expected[index].StartsWith('{') && expected[index].EndsWith('}')) && !expected[index].Equals(actual[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 一页对一组检索词的得分。
    /// </summary>
    /// <remarks>
    /// 任何一个词没出现即不命中。
    /// </remarks>
    private static SearchHit? Score(DocPage page, IReadOnlyList<string> terms)
    {
        var score = 0;
        var snippet = string.Empty;
        var lines = page.Markdown.Split('\n');
        foreach (var term in terms)
        {
            var termScore = page.Title.Contains(term, StringComparison.OrdinalIgnoreCase) || page.Path.Contains(term, StringComparison.OrdinalIgnoreCase) ? 20 : 0;
            foreach (var line in lines)
            {
                if (!line.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                termScore += line.StartsWith('#') ? 5 : 1;
                if (snippet.Length == 0 && !line.StartsWith("# ", StringComparison.Ordinal))
                {
                    snippet = line.Trim().TrimStart('>', '|', '-', '*', ' ').Replace("**", string.Empty, StringComparison.Ordinal);
                }
            }

            if (termScore == 0)
            {
                return null;
            }

            score += Math.Min(termScore, 60);
        }

        return new SearchHit(page.Path, page.Title, page.Summary, score, snippet.Length > MaxSnippetLength ? snippet[..MaxSnippetLength] + "…" : snippet);
    }

    /// <summary>
    /// 查不到：有候选时文案带上候选。
    /// </summary>
    private static Error NotFound(string subject, List<string> candidates) => candidates.Count == 0
        ? Error.NotFound(CliErrors.KnowledgeNotFound, CliMessages.KnowledgeNotFound, MessageArguments.Of(("subject", subject)))
        : Error.NotFound(CliErrors.KnowledgeNotFound, CliMessages.KnowledgeNotFoundCandidates, MessageArguments.Of(("subject", subject), ("candidates", string.Join(", ", candidates))));

    /// <summary>
    /// 候选：规范化后互相包含，或编辑距离不超过 2。
    /// </summary>
    private static List<string> Suggest(string text, IEnumerable<string> names)
    {
        var key = Normalize(text);
        if (key.Length == 0)
        {
            return [];
        }

        return names
            .Select(n => (Name: n, Key: Normalize(n)))
            .Select(n => (n.Name, Score: n.Key.Contains(key, StringComparison.Ordinal) || key.Contains(n.Key, StringComparison.Ordinal) ? 0 : Distance(n.Key, key)))
            .Where(static n => n.Score <= 2)
            .OrderBy(static n => n.Score)
            .ThenBy(static n => n.Name, StringComparer.Ordinal)
            .Select(static n => n.Name)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxCandidates)
            .ToList();
    }

    /// <summary>
    /// 规范化：去掉包 ID 前缀与分隔符，转小写。
    /// </summary>
    private static string Normalize(string text)
    {
        var trimmed = text.Trim();
        trimmed = trimmed.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase) ? trimmed[PackagePrefix.Length..] : trimmed;
        return new string([.. trimmed.Where(static c => c is not ('.' or '-' or '_' or ':' or '/' or ' ')).Select(char.ToLowerInvariant)]);
    }

    /// <summary>
    /// 类型名去掉命名空间与泛型参数。
    /// </summary>
    private static string SimpleTypeName(string type)
    {
        var generic = type.IndexOf('<', StringComparison.Ordinal);
        var name = generic >= 0 ? type[..generic] : type;
        return name[(name.LastIndexOf('.') + 1)..];
    }

    /// <summary>
    /// 编辑距离。
    /// </summary>
    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
