#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplatePrecompilationOptions
// Guid:ede4d0f0-e8b2-40bf-9503-f322610656be
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:16:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板预编译选项
/// </summary>
public record TemplatePrecompilationOptions
{
    /// <summary>
    /// 是否启用压缩
    /// </summary>
    public bool EnableCompression { get; init; } = true;

    /// <summary>
    /// 是否生成调试信息
    /// </summary>
    public bool GenerateDebugInfo { get; init; } = false;

    /// <summary>
    /// 是否生成源映射
    /// </summary>
    public bool GenerateSourceMap { get; init; } = false;

    /// <summary>
    /// 目标格式
    /// </summary>
    public PrecompilationTargetFormat TargetFormat { get; init; } = PrecompilationTargetFormat.Binary;

    /// <summary>
    /// 输出目录
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// 是否保留原始模板
    /// </summary>
    public bool PreserveOriginalTemplate { get; init; } = false;

    /// <summary>
    /// 优化选项
    /// </summary>
    public TemplateOptimizationOptions? OptimizationOptions { get; init; }

    /// <summary>
    /// 默认预编译选项
    /// </summary>
    public static TemplatePrecompilationOptions Default => new();

    /// <summary>
    /// 生产环境预编译选项
    /// </summary>
    public static TemplatePrecompilationOptions Production => new()
    {
        EnableCompression = true,
        GenerateDebugInfo = false,
        GenerateSourceMap = false,
        TargetFormat = PrecompilationTargetFormat.Binary,
        PreserveOriginalTemplate = false,
        OptimizationOptions = TemplateOptimizationOptions.Aggressive
    };

    /// <summary>
    /// 开发环境预编译选项
    /// </summary>
    public static TemplatePrecompilationOptions Development => new()
    {
        EnableCompression = false,
        GenerateDebugInfo = true,
        GenerateSourceMap = true,
        TargetFormat = PrecompilationTargetFormat.Source,
        PreserveOriginalTemplate = true,
        OptimizationOptions = TemplateOptimizationOptions.Conservative
    };
}
