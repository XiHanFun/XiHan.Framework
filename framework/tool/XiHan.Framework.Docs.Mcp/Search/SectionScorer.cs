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
