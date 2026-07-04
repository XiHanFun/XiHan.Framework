#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RetrievalFilter
// Guid:a1b2c3d4-e5f6-4a16-9c16-0a0b0c0d0e16
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
