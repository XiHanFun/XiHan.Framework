#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateSourceLocation
// Guid:d12ffb94-db2c-45c1-80b7-08a4dc54392a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:56:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
