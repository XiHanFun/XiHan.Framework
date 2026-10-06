// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.AI.Abstractions.Rag;

/// <summary>
/// 检索过滤（向量库 pre-filter，作用于已索引字段）
/// </summary>
/// <remarks>
/// 检索只在一个租户作用域内进行：平台就是 0 号租户，不存在「不限租户」的检索。
/// </remarks>
public sealed class RetrievalFilter
{
    /// <summary>
    /// 限定租户（平台为 0）
    /// </summary>
    public long TenantId { get; init; }

    /// <summary>
    /// 限定文档（null 不限）
    /// </summary>
    public string? DocumentId { get; init; }
}
