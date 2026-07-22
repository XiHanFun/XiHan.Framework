// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
