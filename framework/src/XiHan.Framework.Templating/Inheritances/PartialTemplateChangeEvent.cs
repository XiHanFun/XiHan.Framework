// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板片段变化事件
/// </summary>
public record PartialTemplateChangeEvent
{
    /// <summary>
    /// 变化类型
    /// </summary>
    public PartialChangeType ChangeType { get; init; }

    /// <summary>
    /// 片段名称
    /// </summary>
    public string PartialName { get; init; } = string.Empty;

    /// <summary>
    /// 片段路径
    /// </summary>
    public string? PartialPath { get; init; }

    /// <summary>
    /// 变化时间
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;
}
