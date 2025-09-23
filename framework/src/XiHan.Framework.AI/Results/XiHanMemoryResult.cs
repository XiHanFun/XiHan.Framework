#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanMemoryResult
// Guid:13881a66-8f6d-4977-bed1-8737689aaf93
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 记忆搜索结果
/// </summary>
public class XiHanMemoryResult
{
    /// <summary>
    /// 记忆Id
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 文本内容
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 相关度得分
    /// </summary>
    public double Relevance { get; set; }

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}
