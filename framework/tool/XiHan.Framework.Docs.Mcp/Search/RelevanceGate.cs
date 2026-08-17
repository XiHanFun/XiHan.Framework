// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;

namespace XiHan.Framework.Docs.Mcp.Search;

/// <summary>
/// 相关性截断：判断一个查询到底是不是在问曦寒框架的文档
/// </summary>
/// <param name="options">可调参数</param>
/// <remarks>
/// 这是排序之后的一道判定，不参与排序、也不改变 <see cref="SectionScorer.Rank"/> 的结果。
/// 排序回答「哪几段最像」，本判定回答「像的这几段值不值得拿出去」——
/// 两者需要的信号不同：排序不需要 IDF，而「这个查询是不是在问我们的文档」恰恰只能靠 IDF，
/// 因为只有 IDF 能区分「怎么」（到处都是，无信息量）与「Kubernetes」（零出现，决定性）。
/// </remarks>
public sealed class RelevanceGate(DocsMcpOptions options)
{
    /// <summary>
    /// 判断查询是否落在已索引文档的范围内
    /// </summary>
    /// <param name="query">用户查询串</param>
    /// <param name="index">倒排索引</param>
    /// <param name="totalSections">章节总数</param>
    /// <returns>落在范围内时为 true；为 false 时调用方应走显式否认分支</returns>
    public bool IsAboutIndexedDocs(string query, BigramIndex index, int totalSections)
    {
        return IsAboutIndexedDocs(query, index, totalSections, out _);
    }

    /// <summary>
    /// 判断查询是否落在已索引文档的范围内，并带出本次判定用的覆盖率
    /// </summary>
    /// <param name="query">用户查询串</param>
    /// <param name="index">倒排索引</param>
    /// <param name="totalSections">章节总数</param>
    /// <param name="coverage">本次判定用的覆盖率，供调用方记日志——只在返回 false 时才有诊断意义</param>
    /// <returns>落在范围内时为 true；为 false 时调用方应走显式否认分支</returns>
    /// <remarks>
    /// 有这个重载是为了让调用方能把覆盖率写进日志：0.90 这个阈值是在离线的黄金查询集上标定的，
    /// 要拿真实流量复核它，就得知道被拒绝的查询实际落在多少。
    /// <para>
    /// 语料太小以致判据不生效时，<paramref name="coverage"/> 取 1.0——那条路径必然返回 true，
    /// 而调用方只在拒绝时才会去看这个值。
    /// </para>
    /// </remarks>
    public bool IsAboutIndexedDocs(string query, BigramIndex index, int totalSections, out double coverage)
    {
        if (totalSections < options.MinSectionsForRelevanceCutoff)
        {
            coverage = 1.0;
            return true;
        }

        coverage = MeasureKnownTermCoverage(query, index, totalSections);
        return coverage >= options.MinKnownTermCoverage;
    }

    /// <summary>
    /// 计算查询里「语料认识的词条」占全部词条的 IDF 加权比例
    /// </summary>
    /// <param name="query">用户查询串</param>
    /// <param name="index">倒排索引</param>
    /// <param name="totalSections">章节总数</param>
    /// <returns>0 到 1 之间的比例，无可用词条时返回 1</returns>
    /// <remarks>
    /// 只统计用户自己写下的词，不统计同义词扩展出来的词：
    /// 扩展词是从术语表里取的，按构造必然存在于语料中，算进去只会把比例推向 1。
    /// </remarks>
    public double MeasureKnownTermCoverage(string query, BigramIndex index, int totalSections)
    {
        ArgumentNullException.ThrowIfNull(index);

        if (totalSections <= 0)
        {
            return 1.0;
        }

        var artifacts = FindBoundaryArtifacts(query, index);
        var known = 0.0;
        var all = 0.0;

        foreach (var term in Tokenizer.Tokenize(query).Distinct(StringComparer.Ordinal))
        {
            if (artifacts.Contains(term))
            {
                continue;
            }

            var weight = Math.Log((totalSections + 1.0) / (index.Find(term).Count + 1.0));
            all += weight;

            if (IsKnown(term, index))
            {
                known += weight;
            }
        }

        return all <= 0 ? 1.0 : known / all;
    }

    /// <summary>
    /// 判断一个词条是否被语料认识
    /// </summary>
    /// <param name="term">词条</param>
    /// <param name="index">倒排索引</param>
    /// <returns>认识时为 true</returns>
    private bool IsKnown(string term, BigramIndex index)
    {
        var minimum = IsChineseBigram(term) ? 1 : options.MinLatinTermDocumentFrequency;
        return index.Find(term).Count >= minimum;
    }

    /// <summary>
    /// 找出跨词边界的中文二元伪影
    /// </summary>
    /// <param name="query">用户查询串</param>
    /// <param name="index">倒排索引</param>
    /// <returns>应当从统计中剔除的词条集合</returns>
    /// <remarks>
    /// 「路由为什么没有动词」切出的 <c>由为</c>、<c>么没</c>、<c>有动</c> 在语料里都是零命中，
    /// 但它们不是「文档没讲的概念」，只是分词器在两个词之间切出来的碎片。
    /// 判别方式：语料不认识它，可它在同一段连续中文里紧挨着一个语料认识的二元词——
    /// 真正陌生的概念（「红烧肉」的 <c>红烧</c>、<c>烧肉</c>）左右都是同样陌生的碎片，不会被误剔。
    /// 不做这一步，上面那条查询的覆盖率只有 0.484，比不相关查询还低，判据直接失效。
    /// </remarks>
    private HashSet<string> FindBoundaryArtifacts(string query, BigramIndex index)
    {
        var artifacts = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(query))
        {
            return artifacts;
        }

        var position = 0;

        while (position < query.Length)
        {
            if (!IsCjk(query[position]))
            {
                position++;
                continue;
            }

            var start = position;
            while (position < query.Length && IsCjk(query[position]))
            {
                position++;
            }

            CollectRunArtifacts(query.AsSpan(start, position - start), index, artifacts);
        }

        return artifacts;
    }

    /// <summary>
    /// 在一段连续中文里收集伪影
    /// </summary>
    /// <param name="run">一段连续中文</param>
    /// <param name="index">倒排索引</param>
    /// <param name="artifacts">收集结果</param>
    private void CollectRunArtifacts(ReadOnlySpan<char> run, BigramIndex index, HashSet<string> artifacts)
    {
        // 只有一个二元词时无从判断左右邻接，一律不当作伪影
        if (run.Length < 3)
        {
            return;
        }

        var bigrams = new string[run.Length - 1];
        var known = new bool[bigrams.Length];

        for (var i = 0; i < bigrams.Length; i++)
        {
            bigrams[i] = run.Slice(i, 2).ToString();
            known[i] = IsKnown(bigrams[i], index);
        }

        for (var i = 0; i < bigrams.Length; i++)
        {
            if (known[i])
            {
                continue;
            }

            if ((i > 0 && known[i - 1]) || (i < bigrams.Length - 1 && known[i + 1]))
            {
                artifacts.Add(bigrams[i]);
            }
        }
    }

    /// <summary>
    /// 判断词条是否为中文二元词
    /// </summary>
    private static bool IsChineseBigram(string term)
    {
        return term.Length == 2 && IsCjk(term[0]) && IsCjk(term[1]);
    }

    /// <summary>
    /// 判断字符是否属于中日韩统一表意文字区段
    /// </summary>
    private static bool IsCjk(char value)
    {
        return value is >= '一' and <= '鿿';
    }
}
