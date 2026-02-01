#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateBlock
// Guid:face0b61-b890-43c4-9aa8-687d19301a9e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:11:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Compilers;

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板块
/// </summary>
public record TemplateBlock
{
    /// <summary>
    /// 块名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 块内容
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 是否可覆盖
    /// </summary>
    public bool IsOverridable { get; init; } = true;

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool IsRequired { get; init; } = false;

    /// <summary>
    /// 默认内容
    /// </summary>
    public string? DefaultContent { get; init; }

    /// <summary>
    /// 块位置
    /// </summary>
    public TemplateSourceLocation? Location { get; init; }
}
