#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryKeyword
// Guid:a2f29b87-33fc-4fc9-81fe-af4ae70713c7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/10 6:42:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Shared.Paging.Models;

/// <summary>
/// 关键字搜索（关键字文本 + 参与搜索的字段）
/// </summary>
public class QueryKeyword
{
    /// <summary>
    /// 搜索关键字（可选）
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// 允许关键字搜索的字段
    /// </summary>
    public List<string> Fields { get; set; } = [];
}