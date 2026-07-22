// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板优化选项
/// </summary>
public record TemplateOptimizationOptions
{
    /// <summary>
    /// 是否启用常量折叠
    /// </summary>
    public bool EnableConstantFolding { get; init; } = true;

    /// <summary>
    /// 是否启用死代码消除
    /// </summary>
    public bool EnableDeadCodeElimination { get; init; } = true;

    /// <summary>
    /// 是否启用内联优化
    /// </summary>
    public bool EnableInlining { get; init; } = true;

    /// <summary>
    /// 是否启用循环优化
    /// </summary>
    public bool EnableLoopOptimization { get; init; } = true;

    /// <summary>
    /// 是否启用表达式优化
    /// </summary>
    public bool EnableExpressionOptimization { get; init; } = true;

    /// <summary>
    /// 是否启用字符串优化
    /// </summary>
    public bool EnableStringOptimization { get; init; } = true;

    /// <summary>
    /// 是否启用缓存优化
    /// </summary>
    public bool EnableCacheOptimization { get; init; } = true;

    /// <summary>
    /// 内联阈值
    /// </summary>
    public int InlineThreshold { get; init; } = 100;

    /// <summary>
    /// 最大优化级别
    /// </summary>
    public OptimizationLevel MaxOptimizationLevel { get; init; } = OptimizationLevel.Aggressive;

    /// <summary>
    /// 优化超时时间（毫秒）
    /// </summary>
    public int OptimizationTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// 默认优化选项
    /// </summary>
    public static TemplateOptimizationOptions Default => new();

    /// <summary>
    /// 保守优化选项
    /// </summary>
    public static TemplateOptimizationOptions Conservative => new()
    {
        EnableConstantFolding = true,
        EnableDeadCodeElimination = false,
        EnableInlining = false,
        EnableLoopOptimization = false,
        EnableExpressionOptimization = true,
        EnableStringOptimization = true,
        EnableCacheOptimization = true,
        MaxOptimizationLevel = OptimizationLevel.Basic
    };

    /// <summary>
    /// 激进优化选项
    /// </summary>
    public static TemplateOptimizationOptions Aggressive => new()
    {
        EnableConstantFolding = true,
        EnableDeadCodeElimination = true,
        EnableInlining = true,
        EnableLoopOptimization = true,
        EnableExpressionOptimization = true,
        EnableStringOptimization = true,
        EnableCacheOptimization = true,
        InlineThreshold = 1000,
        MaxOptimizationLevel = OptimizationLevel.Aggressive
    };
}
