// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 一次检索：问题文本、集合、条数、过滤、嵌入 Provider 与最低得分。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record KnowledgeQuery
{
    /// <summary>
    /// 问题文本。
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 集合；空取 <see cref="KnowledgeOptions.Collection"/>。
    /// </summary>
    public string? Collection { get; init; }

    /// <summary>
    /// 条数；空取 <see cref="KnowledgeOptions.Top"/>。
    /// </summary>
    public int? Top
    {
        get => field;
        init => field = value is null or >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Top must be at least 1.");
    }

    /// <summary>
    /// 过滤。
    /// </summary>
    public RetrievalFilter? Filter { get; init; }

    /// <summary>
    /// 嵌入 Provider；空取 <see cref="KnowledgeOptions.EmbeddingProvider"/>。
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// 最低得分；低于它的命中被丢弃。
    /// </summary>
    public double? MinScore { get; init; }
}
