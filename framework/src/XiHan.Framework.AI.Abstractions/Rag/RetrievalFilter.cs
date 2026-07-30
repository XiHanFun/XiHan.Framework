// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.AI.Abstractions.Rag;

/// <summary>
/// 检索过滤（向量库 pre-filter，作用于已索引字段）
/// </summary>
public sealed class RetrievalFilter
{
    /// <summary>
    /// 限定租户（0=平台全局；null 不限）
    /// </summary>
    public long? TenantId { get; init; }

    /// <summary>
    /// 限定文档（null 不限）
    /// </summary>
    public string? DocumentId { get; init; }
}
