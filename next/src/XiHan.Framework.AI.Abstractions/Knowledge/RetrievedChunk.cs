// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 检索命中的块：记录（不含向量）与相似度得分（越大越相似）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record RetrievedChunk
{
    /// <summary>
    /// 记录 ID。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 文档 ID。
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// 块序号。
    /// </summary>
    public required int ChunkIndex { get; init; }

    /// <summary>
    /// 块文本。
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 元数据。
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 得分。
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// 从记录创建。
    /// </summary>
    public static RetrievedChunk From(KnowledgeRecord record, double score)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new RetrievedChunk { Id = record.Id, DocumentId = record.DocumentId, ChunkIndex = record.ChunkIndex, Text = record.Text, Metadata = record.Metadata, Score = score };
    }
}
