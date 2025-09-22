#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateOptimizationResult
// Guid:f92744da-c0e9-40ca-b265-5f09163ac17d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 4:18:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Parsers;

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板优化结果
/// </summary>
public record TemplateOptimizationResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 优化后的模板
    /// </summary>
    public string? OptimizedTemplate { get; init; }

    /// <summary>
    /// 优化后的AST
    /// </summary>
    public ITemplateAst? OptimizedAst { get; init; }

    /// <summary>
    /// 优化统计
    /// </summary>
    public TemplateOptimizationStats Stats { get; init; } = new();

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 优化时间（毫秒）
    /// </summary>
    public long OptimizationTimeMs { get; init; }

    /// <summary>
    /// 优化建议
    /// </summary>
    public ICollection<string> Suggestions { get; init; } = [];
}
