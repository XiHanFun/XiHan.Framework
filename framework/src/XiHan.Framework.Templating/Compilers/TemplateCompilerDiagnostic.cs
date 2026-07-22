// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
