// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.VectorData;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.AI.Rag;

/// <summary>
/// 向量库知识切片记录（Microsoft.Extensions.VectorData 记录模型）
/// </summary>
/// <remarks>
/// 键用 <see cref="Guid"/>（Qdrant 仅支持 Guid/ulong）。
/// 字段模型不走特性而由 <see cref="CreateDefinition"/> 在运行期构建，使向量维度可配置。
/// </remarks>
public sealed class VectorStoreKnowledgeRecord
{
    /// <summary>
    /// 主键（由 TenantId、DocumentId 与 ChunkIndex 确定性派生，便于按租户文档 upsert/删除）
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属文档 id（过滤/删除维度）
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// 租户 id（0=平台全局；过滤隔离维度）
    /// </summary>
    public long TenantId { get; set; }

    /// <summary>
    /// 切片序号
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 切片文本
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 文档标题（引用展示）
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 来源标识（引用溯源）
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 嵌入向量
    /// </summary>
    public ReadOnlyMemory<float>? Embedding { get; set; }

    /// <summary>
    /// 按指定维度构建集合定义（DocumentId/TenantId 建索引以支持 pre-filter）
    /// </summary>
    /// <param name="dimensions">向量维度</param>
    /// <returns>集合定义</returns>
    public static VectorStoreCollectionDefinition CreateDefinition(int dimensions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensions, 1);

        return new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty(nameof(Id), typeof(Guid)),
                new VectorStoreDataProperty(nameof(DocumentId), typeof(string)) { IsIndexed = true },
                new VectorStoreDataProperty(nameof(TenantId), typeof(long)) { IsIndexed = true },
                new VectorStoreDataProperty(nameof(ChunkIndex), typeof(int)),
                new VectorStoreDataProperty(nameof(Text), typeof(string)),
                new VectorStoreDataProperty(nameof(Title), typeof(string)),
                new VectorStoreDataProperty(nameof(Source), typeof(string)),
                new VectorStoreVectorProperty(nameof(Embedding), typeof(ReadOnlyMemory<float>?), dimensions)
                {
                    DistanceFunction = DistanceFunction.CosineSimilarity,
                    IndexKind = IndexKind.Hnsw
                }
            ]
        };
    }

    /// <summary>
    /// 校验嵌入模型实际输出维度与集合配置一致
    /// </summary>
    /// <param name="actual">嵌入模型实际输出维度</param>
    /// <param name="expected">集合配置维度</param>
    /// <exception cref="InvalidOperationException">维度不一致。</exception>
    public static void EnsureDimensions(int actual, int expected)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"嵌入维度不匹配：模型输出 {actual} 维，向量集合配置为 {expected} 维。" +
                "请将配置维度改为模型的实际维度，并更换集合名或删除原集合后重建索引。");
        }
    }

    /// <summary>
    /// 由租户 id、文档 id 与切片序号确定性派生主键（同租户文档同序号恒等，重建即覆盖）。
    /// </summary>
    /// <param name="tenantId">租户标识，0 表示平台全局数据</param>
    /// <param name="documentId">文档标识</param>
    /// <param name="index">切片序号</param>
    /// <returns>租户隔离的确定性主键</returns>
    /// <exception cref="ArgumentOutOfRangeException">租户标识或切片序号小于零</exception>
    /// <exception cref="ArgumentException">文档标识为空白</exception>
    public static Guid MakeId(long tenantId, string documentId, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        // 租户 0 保持既有键，避免平台全局文档升级时无谓改变向量主键。
        if (tenantId == 0)
        {
            return MakeLegacyId(documentId, index);
        }

        // 使用定长二进制字段和长度前缀，避免分隔符拼接产生跨字段歧义。
        var documentBytes = Encoding.UTF8.GetBytes(documentId);
        var input = new byte[sizeof(long) + sizeof(int) + documentBytes.Length + sizeof(int)];
        BinaryPrimitives.WriteInt64BigEndian(input.AsSpan(0, sizeof(long)), tenantId);
        BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(sizeof(long), sizeof(int)), documentBytes.Length);
        documentBytes.CopyTo(input.AsSpan(sizeof(long) + sizeof(int), documentBytes.Length));
        BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(sizeof(long) + sizeof(int) + documentBytes.Length), index);

        var hash = SHA256.HashData(input);
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// 生成平台全局文档的旧版确定性主键。
    /// </summary>
    /// <param name="documentId">文档标识</param>
    /// <param name="index">切片序号</param>
    /// <returns>与租户标识为 0 时相同的主键</returns>
    [Obsolete("请传入 TenantId；此重载仅用于平台全局文档。")]
    public static Guid MakeId(string documentId, int index)
    {
        return MakeId(0, documentId, index);
    }

    internal static Guid MakeLegacyId(string documentId, int index)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{documentId}:{index}"));
        return new Guid(bytes);
    }
}
