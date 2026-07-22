// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板源位置
/// </summary>
public record TemplateSourceLocation
{
    /// <summary>
    /// 行号
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// 列号
    /// </summary>
    public int Column { get; init; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// 源码片段
    /// </summary>
    public string? SourceSnippet { get; init; }
}
