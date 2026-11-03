// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.VectorData;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.VectorData;

/// <summary>
/// 向量存储里的知识块
/// </summary>
/// <remarks>
/// 元数据以 JSON 字符串保存；集合定义由 <see cref="Definition"/> 按维度构造（余弦相似度）
/// 键是由记录 ID 推出的 <see cref="Guid"/>（<see cref="KeyOf"/>）：Qdrant 这类连接器不接受字符串键；记录 ID 另存 <see cref="RecordId"/>，对外的 ID 不变
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KnowledgeVectorRecord
{
    /// <summary>
    /// 向量键（由 <see cref="RecordId"/> 经 <see cref="KeyOf"/> 推出）
    /// </summary>
    public Guid Key { get; set; }

    /// <summary>
    /// 记录 ID（<c>&lt;documentId&gt;#&lt;chunkIndex&gt;</c>）
    /// </summary>
    public string RecordId { get; set; } = string.Empty;

    /// <summary>
    /// 文档 ID
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// 块序号
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 块文本
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 元数据（JSON 对象字符串）
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// 向量
    /// </summary>
    public ReadOnlyMemory<float> Vector { get; set; }

    /// <summary>
    /// 由记录 ID 推出向量键
    /// </summary>
    /// <remarks>
    /// 同一个记录 ID 永远得到同一个键，重写入因此覆盖旧记录
    /// 算法是存储格式的一部分（RFC-0022），改了已有集合的键就对不上：UTF-8 字节的 SHA-256 取前 16 字节，写上版本 8 与变体位（RFC 9562），按大端读成 UUID
    /// </remarks>
    public static Guid KeyOf(string recordId)
    {
        ArgumentNullException.ThrowIfNull(recordId);
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(recordId), hash);
        // 版本 8 是 RFC 9562 留给自定义算法的；不用基于 SHA-1 的版本 5，这里也不需要与别的系统互通
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16], bigEndian: true);
    }

    /// <summary>
    /// 集合定义
    /// </summary>
    public static VectorStoreCollectionDefinition Definition(int dimensions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensions, 1);
        return new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty(nameof(Key), typeof(Guid)),
                new VectorStoreDataProperty(nameof(RecordId), typeof(string)),
                new VectorStoreDataProperty(nameof(DocumentId), typeof(string)) { IsIndexed = true },
                new VectorStoreDataProperty(nameof(ChunkIndex), typeof(int)) { IsIndexed = true },
                new VectorStoreDataProperty(nameof(Text), typeof(string)),
                new VectorStoreDataProperty(nameof(Metadata), typeof(string)),
                new VectorStoreVectorProperty(nameof(Vector), typeof(ReadOnlyMemory<float>), dimensions) { DistanceFunction = DistanceFunction.CosineSimilarity },
            ],
        };
    }
}
