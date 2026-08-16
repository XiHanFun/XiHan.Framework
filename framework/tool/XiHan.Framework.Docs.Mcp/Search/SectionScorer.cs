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
    /// <remarks>
    /// 得分 = 命中词条的权重之和 ÷ 查询词条的权重之和 × 来源权重，标题命中的词条先被 <c>TitleBoost</c> 放大。
    /// <para>
    /// 那个除以 <c>totalWeight</c> 的动作不参与同一次排序的定序：分母在一次查询里对所有章节都是同一个常数，
    /// 除以它是单调缩放，名次一个都不会变。它存在的理由是让分数跨查询可比——
    /// 一条四词查询与一条八词查询若都被某个章节完全命中，都应该报出同一个数（来源权重 × 1.0）；
    /// 不除的话八词查询会报出双倍。工具层会把这个分数原样打印给模型当作置信度提示
    /// （<c>DocsMcpTools.SearchDocs</c> 输出的「得分」一行），所以跨查询可比是对外的实际契约，而不只是内部细节。
    /// 这一点由 <c>SectionScorerTests.归一化让分数跨查询可比</c> 钉住：去掉除法那条测试会红，其余测试照旧全绿。
    /// </para>
    /// <para>
    /// 「覆盖面广的短章节胜过啰嗦的长章节」这条抗长文性质不是这里的除法带来的，
    /// 而是来自 <see cref="BigramIndex"/> 的「同章节同词条只保留一条记录」——
    /// 长章节把同一个词重复一百遍也只记一条 posting，得不到额外的分。
    /// 对应的测试是 <c>BigramIndexTests.同章节同词条去重且标题优先</c>。
    /// </para>
    /// </remarks>
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

        // 除以 totalWeight 是归一化而不是定序手段：分母对本次查询的每个章节都一样，删掉它名次不变。
        // 留着是因为分数要打印给模型看，词条数不同的两条查询「都全中」时必须报出同一个数。
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
