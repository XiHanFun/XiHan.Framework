// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 摄入一篇文档：切块、嵌入、写入集合；同一文档再次摄入先删后写。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record KnowledgeIngestRequest
{
    /// <summary>
    /// 文档 ID。
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// 正文。
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 集合；空取 <see cref="KnowledgeOptions.Collection"/>。
    /// </summary>
    public string? Collection { get; init; }

    /// <summary>
    /// 随每个块保存的元数据（可按键值相等过滤）。
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// 嵌入 Provider；空取 <see cref="KnowledgeOptions.EmbeddingProvider"/>。
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// 切块选项；空取 <see cref="KnowledgeOptions.Chunking"/>。
    /// </summary>
    public ChunkingOptions? Chunking { get; init; }
}
