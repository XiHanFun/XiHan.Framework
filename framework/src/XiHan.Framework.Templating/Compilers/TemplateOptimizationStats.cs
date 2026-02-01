#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateOptimizationStats
// Guid:2739ebbe-1264-4bc4-9087-5badda7a49d7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:19:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板优化统计
/// </summary>
public record TemplateOptimizationStats
{
    /// <summary>
    /// 原始大小（字节）
    /// </summary>
    public int OriginalSize { get; init; }

    /// <summary>
    /// 优化后大小（字节）
    /// </summary>
    public int OptimizedSize { get; init; }

    /// <summary>
    /// 压缩比例
    /// </summary>
    public double CompressionRatio => OriginalSize > 0 ? (double)OptimizedSize / OriginalSize : 0;

    /// <summary>
    /// 移除的死代码行数
    /// </summary>
    public int DeadCodeLinesRemoved { get; init; }

    /// <summary>
    /// 内联的表达式数量
    /// </summary>
    public int InlinedExpressions { get; init; }

    /// <summary>
    /// 优化的循环数量
    /// </summary>
    public int OptimizedLoops { get; init; }

    /// <summary>
    /// 常量折叠次数
    /// </summary>
    public int ConstantFolds { get; init; }

    /// <summary>
    /// 应用的优化次数
    /// </summary>
    public int OptimizationsApplied { get; init; }
}
