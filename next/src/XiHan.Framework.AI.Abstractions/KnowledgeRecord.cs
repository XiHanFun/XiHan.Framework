// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 知识库里的一个块：ID 为 <c>&lt;documentId&gt;#&lt;chunkIndex&gt;</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record KnowledgeRecord
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
    /// 块序号（从 0 起）。
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
    /// 向量。
    /// </summary>
    public required ReadOnlyMemory<float> Vector { get; init; }

    /// <summary>
    /// 记录 ID。
    /// </summary>
    public static string IdOf(string documentId, int chunkIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        return documentId + "#" + chunkIndex.ToString(CultureInfo.InvariantCulture);
    }
}
