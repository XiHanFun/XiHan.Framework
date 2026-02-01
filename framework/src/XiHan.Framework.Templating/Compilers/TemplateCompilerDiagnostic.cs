#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateCompilerDiagnostic
// Guid:5698547d-41cf-4cd3-ba59-22cda0239f0c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:55:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 编译诊断信息
/// </summary>
public record TemplateCompilerDiagnostic
{
    /// <summary>
    /// 诊断级别
    /// </summary>
    public TemplateDiagnosticLevel Level { get; init; }

    /// <summary>
    /// 诊断消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 源位置
    /// </summary>
    public TemplateSourceLocation? Location { get; init; }

    /// <summary>
    /// 诊断代码
    /// </summary>
    public string? Code { get; init; }
}
