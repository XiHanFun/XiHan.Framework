#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PartialTemplateChangeEvent
// Guid:02ee9d83-af07-4f3e-b181-14b98abce269
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:15:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
