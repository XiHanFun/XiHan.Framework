// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 一篇文档要保留的块数
/// </summary>
/// <remarks>
/// 放进 <see cref="IKnowledgeStore.TrimAsync"/> 的集合：块序号不小于 <see cref="ChunkCount"/> 的记录被删掉
/// </remarks>
/// <param name="DocumentId">文档 ID</param>
/// <param name="ChunkCount">保留的块数，不能为负</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record KnowledgeTrim(string DocumentId, int ChunkCount)
{
    /// <summary>
    /// 文档 ID
    /// </summary>
    public string DocumentId { get; } = !string.IsNullOrWhiteSpace(DocumentId) ? DocumentId : throw new ArgumentException("The document ID must not be empty.", nameof(DocumentId));

    /// <summary>
    /// 保留的块数
    /// </summary>
    public int ChunkCount { get; } = ChunkCount >= 0 ? ChunkCount : throw new ArgumentOutOfRangeException(nameof(ChunkCount), ChunkCount, "The chunk count must not be negative.");
}
