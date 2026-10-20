// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 查询失败：带上可直接展示的说明与候选
/// </summary>
internal sealed class QueryException(string message, IReadOnlyList<string>? suggestions = null) : Exception(message)
{
    /// <summary>
    /// 候选
    /// </summary>
    public IReadOnlyList<string> Suggestions { get; } = suggestions ?? [];
}

/// <summary>
/// 命令行与 MCP 工具共用的查询
/// </summary>
internal static class Queries
{
    private const string Prefix = "XiHan.Framework.";

    /// <summary>
    /// 按包名、短名、文档页名或模块类名找包
    /// </summary>
    public static PackageInfo? FindPackage(FrameworkKnowledge knowledge, string name)
    {
        var raw = name.Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        var exact = knowledge.Packages.FirstOrDefault(p => string.Equals(p.Id, raw, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var key = Normalize(raw.StartsWith("packages/", StringComparison.OrdinalIgnoreCase) ? raw["packages/".Length..] : raw);
        return knowledge.Packages.FirstOrDefault(p => Normalize(p.Id) == key)
            ?? knowledge.Packages.FirstOrDefault(p => p.Modules.Any(m => string.Equals(m.Name, raw, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// 找包，找不到时抛出带候选的 QueryException
    /// </summary>
    public static PackageInfo RequirePackage(FrameworkKnowledge knowledge, string name)
    {
        return FindPackage(knowledge, name)
            ?? throw new QueryException($"没有名为「{name}」的框架包。用 list 查看全部包。", SuggestPackages(knowledge, name));
    }

    /// <summary>
    /// 找不到包时的候选：名字互相包含或编辑距离不超过 2
    /// </summary>
    public static IReadOnlyList<string> SuggestPackages(FrameworkKnowledge knowledge, string name)
    {
        var key = Normalize(name);
        if (key.Length == 0)
        {
            return [];
        }

        return knowledge.Packages
            .Select(p => (p.Id, Score: Normalize(p.Id).Contains(key, StringComparison.Ordinal) || key.Contains(Normalize(p.Id), StringComparison.Ordinal) ? 0 : Distance(Normalize(p.Id), key)))
            .Where(x => x.Score <= 2)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .Take(6)
            .Select(x => x.Id)
            .ToList();
    }

    /// <summary>
    /// 按所在层分组列包，可按关键字过滤
    /// </summary>
    public static IReadOnlyList<(string Layer, IReadOnlyList<PackageInfo> Packages)> ListPackages(FrameworkKnowledge knowledge, string? layer = null, string? query = null)
    {
        var filtered = knowledge.Packages.Where(p =>
            (string.IsNullOrWhiteSpace(layer) || (p.Layer ?? string.Empty).Contains(layer.Trim(), StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(query)
                || p.Id.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
                || p.Description.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
                || (p.Summary ?? string.Empty).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)));
        return filtered
            .GroupBy(p => p.Layer ?? "其他")
            .OrderBy(group => LayerRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (group.Key, (IReadOnlyList<PackageInfo>)group.ToList()))
            .ToList();
    }

    /// <summary>
    /// 层的先后：按依赖方向从公共层排到 Web 层，其余排在最后
    /// </summary>
    private static int LayerRank(string layer)
    {
        string[] order = ["公共", "元数据", "核心", "领域", "应用", "基础设施", "Web"];
        var index = Array.FindIndex(order, keyword => layer.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? order.Length : index;
    }

    /// <summary>
    /// 按站内路径或包名找文档页
    /// </summary>
    public static DocPage RequirePage(FrameworkKnowledge knowledge, string name)
    {
        var key = name.Trim().Trim('/').Replace(".md", string.Empty, StringComparison.Ordinal);
        if (key.EndsWith("/index", StringComparison.Ordinal))
        {
            key = key[..^"/index".Length];
        }

        var page = knowledge.Pages.FirstOrDefault(p => p.Path == key);
        if (page is not null)
        {
            return page;
        }

        var package = FindPackage(knowledge, name);
        if (package?.DocPath is not null)
        {
            return knowledge.Pages.First(p => p.Path == package.DocPath);
        }

        var tail = knowledge.Pages.Where(p => p.Path.Split('/')[^1] == key).ToList();
        if (tail.Count == 1)
        {
            return tail[0];
        }

        var near = tail.Select(p => p.Path)
            .Concat(knowledge.Pages.Where(p => p.Path.Contains(key, StringComparison.OrdinalIgnoreCase)).Select(p => p.Path))
            .Distinct()
            .Take(8)
            .ToList();
        throw new QueryException($"没有名为「{name}」的文档页。写站内路径（如 guide/caching）或包名（如 Caching）。", near);
    }

    /// <summary>
    /// 全文检索：标题、小节标题与正文按权重计分
    /// </summary>
    public static IReadOnlyList<(DocPage Page, int Score, string Snippet)> Search(FrameworkKnowledge knowledge, string query, int limit = 10)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
        {
            throw new QueryException("检索词不能为空。");
        }

        var results = new List<(DocPage Page, int Score, string Snippet)>();
        foreach (var page in knowledge.Pages)
        {
            var score = 0;
            var snippet = string.Empty;
            foreach (var term in terms)
            {
                var termScore = 0;
                if (page.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    termScore += 20;
                }

                foreach (var line in page.Markdown.Split('\n'))
                {
                    if (!line.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    termScore += line.StartsWith('#') ? 5 : 1;
                    if (snippet.Length == 0 && !line.StartsWith("# ", StringComparison.Ordinal))
                    {
                        snippet = line.Trim().TrimStart('>').Trim().Replace("**", string.Empty, StringComparison.Ordinal);
                    }
                }

                if (termScore == 0)
                {
                    score = 0;
                    break;
                }

                score += Math.Min(termScore, 60);
            }

            if (score > 0)
            {
                results.Add((page, score, snippet.Length > 160 ? $"{snippet[..160]}…" : snippet));
            }
        }

        if (results.Count == 0)
        {
            throw new QueryException($"文档里没有同时提到「{string.Join("」「", terms)}」的页面。换个说法，或用 list 浏览全部包。");
        }

        return results.OrderByDescending(r => r.Score).ThenBy(r => r.Page.Path, StringComparer.Ordinal).Take(limit).ToList();
    }

    /// <summary>
    /// 查配置节：不给关键字时列出全部；关键字可写配置节、包名或选项类名
    /// </summary>
    public static IReadOnlyList<ConfigSection> Config(FrameworkKnowledge knowledge, string? query = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return knowledge.Sections;
        }

        var key = query.Trim();
        var bySection = knowledge.Sections.Where(s => s.Section.Contains(key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Options.Name, key, StringComparison.OrdinalIgnoreCase)).ToList();
        if (bySection.Count > 0)
        {
            return bySection;
        }

        var package = FindPackage(knowledge, key);
        if (package is not null)
        {
            var own = knowledge.Sections.Where(s => s.Package == package.Id).ToList();
            if (own.Count > 0)
            {
                return own;
            }

            throw new QueryException($"{package.Id} 没有绑定配置节的选项类。");
        }

        throw new QueryException($"没有匹配「{query}」的配置节。不带参数可列出全部配置节。", SuggestPackages(knowledge, key));
    }

    /// <summary>
    /// 取更新日志：只给 from 时取这一版，给 from 与 to 时取 (from, to] 区间，都不给时取最近一版；keyword 只保留含它的条目
    /// </summary>
    public static IReadOnlyList<ChangelogSection> Changelog(FrameworkKnowledge knowledge, string? from = null, string? to = null, string? keyword = null)
    {
        var sections = knowledge.Changelog;
        if (sections.Count == 0)
        {
            throw new QueryException("更新日志为空。");
        }

        IReadOnlyList<ChangelogSection> picked;
        if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
        {
            var (low, high) = CompareVersions(from, to) <= 0 ? (from, to) : (to, from);
            picked = sections.Where(s => CompareVersions(s.Version, low) > 0 && CompareVersions(s.Version, high) <= 0).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(from))
        {
            var version = from.Trim().TrimStart('v');
            picked = sections.Where(s => s.Version == version).ToList();
            if (picked.Count == 0)
            {
                throw new QueryException($"更新日志里没有 {from}。", sections.Take(8).Select(s => s.Version).ToList());
            }
        }
        else
        {
            picked = [sections[0]];
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return picked;
        }

        var needles = new List<string> { keyword.Trim() };
        if (FindPackage(knowledge, keyword) is { } package)
        {
            needles.Add(package.Id);
            needles.Add(package.Id[Prefix.Length..]);
        }

        return picked
            .Select(s => s with
            {
                Body = string.Join('\n', s.Body.Split('\n').Where(line => line.TrimStart().StartsWith("- ", StringComparison.Ordinal)
                    && needles.Any(n => line.Contains(n, StringComparison.OrdinalIgnoreCase)))),
            })
            .Where(s => s.Body.Length > 0)
            .ToList();
    }

    /// <summary>
    /// 语义化版本比较：预发布排在同号正式版之前
    /// </summary>
    public static int CompareVersions(string a, string b)
    {
        static (int[] Core, string Pre) Split(string v)
        {
            var parts = v.Trim().TrimStart('v').Split('-', 2);
            var core = parts[0].Split('.').Select(n => int.TryParse(n, out var value) ? value : 0).ToArray();
            return (core, parts.Length > 1 ? parts[1] : string.Empty);
        }

        var (ca, pa) = Split(a);
        var (cb, pb) = Split(b);
        for (var i = 0; i < Math.Max(ca.Length, cb.Length); i++)
        {
            var diff = (i < ca.Length ? ca[i] : 0) - (i < cb.Length ? cb[i] : 0);
            if (diff != 0)
            {
                return diff;
            }
        }

        if (pa == pb)
        {
            return 0;
        }

        if (pa.Length == 0)
        {
            return 1;
        }

        return pb.Length == 0 ? -1 : string.CompareOrdinal(pa, pb);
    }

    private static string Normalize(string name)
    {
        var value = name.Trim();
        if (value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Prefix.Length..];
        }

        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var previous = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var current = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                previous = current;
            }
        }

        return row[b.Length];
    }
}
