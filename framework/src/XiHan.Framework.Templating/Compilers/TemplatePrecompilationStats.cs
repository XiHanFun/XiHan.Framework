// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板预编译统计
/// </summary>
public record TemplatePrecompilationStats
{
    /// <summary>
    /// 原始大小（字节）
    /// </summary>
    public int OriginalSize { get; init; }

    /// <summary>
    /// 编译后大小（字节）
    /// </summary>
    public int CompiledSize { get; init; }

    /// <summary>
    /// 压缩比例
    /// </summary>
    public double CompressionRatio => OriginalSize > 0 ? (double)CompiledSize / OriginalSize : 0;

    /// <summary>
    /// 依赖数量
    /// </summary>
    public int DependencyCount { get; init; }

    /// <summary>
    /// 生成的指令数
    /// </summary>
    public int InstructionCount { get; init; }

    /// <summary>
    /// 缓存键
    /// </summary>
    public string? CacheKey { get; init; }
}
