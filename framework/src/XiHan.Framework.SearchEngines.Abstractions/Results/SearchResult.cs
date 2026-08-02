// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.SearchEngines.Results;

/// <summary>
/// 检索命中项
/// </summary>
/// <typeparam name="TDocument">文档类型</typeparam>
/// <param name="Id">文档标识</param>
/// <param name="Document">文档</param>
/// <param name="Score">相关度得分，未按相关度检索时为 0</param>
/// <param name="Highlights">高亮片段，键为字段名</param>
public sealed record SearchHit<TDocument>(
    string Id,
    TDocument Document,
    double Score,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Highlights);

/// <summary>
/// 检索结果
/// </summary>
/// <typeparam name="TDocument">文档类型</typeparam>
public sealed class SearchResult<TDocument>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="hits">命中项</param>
    /// <param name="totalCount">命中总数，不受分页影响</param>
    public SearchResult(IReadOnlyList<SearchHit<TDocument>> hits, long totalCount)
    {
        ArgumentNullException.ThrowIfNull(hits);

        Hits = hits;
        TotalCount = totalCount;
    }

    /// <summary>
    /// 命中项
    /// </summary>
    public IReadOnlyList<SearchHit<TDocument>> Hits { get; }

    /// <summary>
    /// 命中总数，不受分页影响
    /// </summary>
    public long TotalCount { get; }

    /// <summary>
    /// 空结果
    /// </summary>
    public static SearchResult<TDocument> Empty { get; } = new([], 0);
}
