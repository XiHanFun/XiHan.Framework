// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.SearchEngines.Abstractions.Querying;

/// <summary>
/// 排序方向
/// </summary>
public enum SearchSortDirection
{
    /// <summary>
    /// 升序
    /// </summary>
    Ascending,

    /// <summary>
    /// 降序
    /// </summary>
    Descending
}

/// <summary>
/// 排序项
/// </summary>
/// <param name="Field">字段名，为 <see cref="SearchSort.ScoreField"/> 时按相关度排序</param>
/// <param name="Direction">排序方向</param>
public sealed record SearchSort(string Field, SearchSortDirection Direction = SearchSortDirection.Ascending)
{
    /// <summary>
    /// 相关度字段名
    /// </summary>
    public const string ScoreField = "_score";

    /// <summary>
    /// 按相关度降序
    /// </summary>
    public static SearchSort ByScore { get; } = new(ScoreField, SearchSortDirection.Descending);
}

/// <summary>
/// 检索请求
/// </summary>
public sealed class SearchRequest
{
    private int _skip;
    private int _take = 20;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="index">索引名</param>
    public SearchRequest(string index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);

        Index = index;
    }

    /// <summary>
    /// 索引名
    /// </summary>
    public string Index { get; }

    /// <summary>
    /// 关键字，为空时只按过滤条件检索
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// 参与关键字检索的字段，为空时由实现取索引中全部可检索字段
    /// </summary>
    public IReadOnlyList<string> Fields { get; init; } = [];

    /// <summary>
    /// 结构化过滤条件，条目之间为「与」关系
    /// </summary>
    public IReadOnlyList<SearchFilter> Filters { get; init; } = [];

    /// <summary>
    /// 排序项，为空时按相关度降序
    /// </summary>
    public IReadOnlyList<SearchSort> Sorts { get; init; } = [];

    /// <summary>
    /// 需要高亮的字段，为空时不返回高亮片段
    /// </summary>
    public IReadOnlyList<string> HighlightFields { get; init; } = [];

    /// <summary>
    /// 跳过条数
    /// </summary>
    public int Skip
    {
        get => _skip;
        init => _skip = value < 0
            ? throw new ArgumentOutOfRangeException(nameof(value), "跳过条数不能为负数。")
            : value;
    }

    /// <summary>
    /// 获取条数
    /// </summary>
    public int Take
    {
        get => _take;
        init => _take = value <= 0
            ? throw new ArgumentOutOfRangeException(nameof(value), "获取条数必须大于 0。")
            : value;
    }
}
