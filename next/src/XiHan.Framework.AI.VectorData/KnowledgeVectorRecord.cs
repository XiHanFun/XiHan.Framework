// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.VectorData;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.VectorData;

/// <summary>
/// 向量存储里的知识块。
/// </summary>
/// <remarks>
/// 元数据以 JSON 字符串保存；集合定义由 <see cref="Definition"/> 按维度构造（余弦相似度）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KnowledgeVectorRecord
{
    /// <summary>
    /// 记录 ID（<c>&lt;documentId&gt;#&lt;chunkIndex&gt;</c>）。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 文档 ID。
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// 块序号。
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 块文本。
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 元数据（JSON 对象字符串）。
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// 向量。
    /// </summary>
    public ReadOnlyMemory<float> Vector { get; set; }

    /// <summary>
    /// 集合定义。
    /// </summary>
    public static VectorStoreCollectionDefinition Definition(int dimensions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensions, 1);
        return new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty(nameof(Id), typeof(string)),
                new VectorStoreDataProperty(nameof(DocumentId), typeof(string)) { IsIndexed = true },
                new VectorStoreDataProperty(nameof(ChunkIndex), typeof(int)),
                new VectorStoreDataProperty(nameof(Text), typeof(string)),
                new VectorStoreDataProperty(nameof(Metadata), typeof(string)),
                new VectorStoreVectorProperty(nameof(Vector), typeof(ReadOnlyMemory<float>), dimensions) { DistanceFunction = DistanceFunction.CosineSimilarity },
            ],
        };
    }
}
