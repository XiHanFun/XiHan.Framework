#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplatePerformanceReport
// Guid:63128735-968e-4b9a-ae23-f33e1b5beda8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 4:19:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板性能报告
/// </summary>
public record TemplatePerformanceReport
{
    /// <summary>
    /// 模板复杂度
    /// </summary>
    public int ComplexityScore { get; init; }

    /// <summary>
    /// 预估渲染时间（毫秒）
    /// </summary>
    public double EstimatedRenderTimeMs { get; init; }

    /// <summary>
    /// 内存使用预估（字节）
    /// </summary>
    public long EstimatedMemoryUsage { get; init; }

    /// <summary>
    /// 性能瓶颈
    /// </summary>
    public ICollection<PerformanceBottleneck> Bottlenecks { get; init; } = [];

    /// <summary>
    /// 优化建议
    /// </summary>
    public ICollection<string> OptimizationSuggestions { get; init; } = [];

    /// <summary>
    /// 性能评级
    /// </summary>
    public PerformanceRating Rating { get; init; }
}
