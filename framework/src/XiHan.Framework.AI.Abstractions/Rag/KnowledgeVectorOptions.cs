// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.AI.Abstractions.Rag;

/// <summary>
/// 知识向量集合配置（集合名与向量维度）
/// </summary>
/// <remarks>维度须与嵌入模型输出维度一致；改维度后需换集合名或删除原集合重建。</remarks>
public sealed class KnowledgeVectorOptions
{
    /// <summary>
    /// 默认集合名
    /// </summary>
    public const string DefaultCollectionName = "xihan_knowledge";

    /// <summary>
    /// 默认向量维度（text-embedding-3-small）
    /// </summary>
    public const int DefaultDimensions = 1536;

    /// <summary>
    /// 集合名
    /// </summary>
    public string CollectionName { get; set; } = DefaultCollectionName;

    /// <summary>
    /// 向量维度
    /// </summary>
    public int Dimensions { get; set; } = DefaultDimensions;
}
