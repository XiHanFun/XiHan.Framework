// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
