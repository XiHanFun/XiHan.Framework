#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VectorStoreKnowledgeRecord
// Guid:b2c3d4e5-f6a7-4b10-9d10-1a1b1c1d1e10
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.VectorData;

namespace XiHan.Framework.AI.Rag;

/// <summary>
/// 向量库知识切片记录（Microsoft.Extensions.VectorData 记录模型）
/// </summary>
/// <remarks>
/// 键用 <see cref="Guid"/>（Qdrant 仅支持 Guid/ulong，string 会在建集合时抛异常；Guid 兼容各连接器）。
/// 过滤字段（DocumentId/TenantId）标 <c>IsIndexed</c> 以支持 pre-filter；向量维度固定为
/// <see cref="EmbeddingDimensions"/>（对齐 text-embedding-3-small=1536；换维度模型须改此常量并重建集合）。
/// </remarks>
public sealed class VectorStoreKnowledgeRecord
{
    /// <summary>
    /// 向量维度（编译期常量，须与嵌入模型输出维度一致）
    /// </summary>
    public const int EmbeddingDimensions = 1536;

    /// <summary>
    /// 集合名
    /// </summary>
    public const string CollectionName = "xihan_knowledge";

    /// <summary>
    /// 主键（由 DocumentId + ChunkIndex 确定性派生，便于按文档 upsert/删除）
    /// </summary>
    [VectorStoreKey]
    public Guid Id { get; set; }

    /// <summary>
    /// 所属文档 id（过滤/删除维度）
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// 租户 id（0=平台全局；过滤隔离维度）
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public long TenantId { get; set; }

    /// <summary>
    /// 切片序号
    /// </summary>
    [VectorStoreData]
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 切片文本
    /// </summary>
    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 文档标题（引用展示）
    /// </summary>
    [VectorStoreData]
    public string? Title { get; set; }

    /// <summary>
    /// 来源标识（引用溯源）
    /// </summary>
    [VectorStoreData]
    public string? Source { get; set; }

    /// <summary>
    /// 嵌入向量
    /// </summary>
    [VectorStoreVector(EmbeddingDimensions, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? Embedding { get; set; }

    /// <summary>
    /// 由文档 id + 切片序号确定性派生主键（同文档同序号恒等，重建即覆盖）
    /// </summary>
    public static Guid MakeId(string documentId, int index)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{documentId}:{index}"));
        return new Guid(bytes);
    }
}
