// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.VectorData;
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
    /// 主键（由 DocumentId + ChunkIndex 确定性派生，便于按文档 upsert/删除）
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
    /// 由文档 id + 切片序号确定性派生主键（同文档同序号恒等，重建即覆盖）
    /// </summary>
    public static Guid MakeId(string documentId, int index)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{documentId}:{index}"));
        return new Guid(bytes);
    }
}
