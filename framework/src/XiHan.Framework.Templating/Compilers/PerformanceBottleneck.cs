#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PerformanceBottleneck
// Guid:1e5ca26d-d19a-4a53-8aae-466c441c565d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 4:19:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 性能瓶颈
/// </summary>
public record PerformanceBottleneck
{
    /// <summary>
    /// 瓶颈类型
    /// </summary>
    public BottleneckType Type { get; init; }

    /// <summary>
    /// 瓶颈描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 影响程度
    /// </summary>
    public ImpactLevel Impact { get; init; }

    /// <summary>
    /// 瓶颈位置
    /// </summary>
    public TemplateSourceLocation? Location { get; init; }

    /// <summary>
    /// 修复建议
    /// </summary>
    public string? Suggestion { get; init; }
}
