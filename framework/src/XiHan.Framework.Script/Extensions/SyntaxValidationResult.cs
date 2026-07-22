// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Extensions;

/// <summary>
/// 语法验证结果
/// </summary>
public class SyntaxValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public IList<Microsoft.CodeAnalysis.Diagnostic> Errors { get; set; } = [];

    /// <summary>
    /// 警告信息
    /// </summary>
    public IList<Microsoft.CodeAnalysis.Diagnostic> Warnings { get; set; } = [];

    /// <summary>
    /// 编译时间(毫秒)
    /// </summary>
    public long CompilationTimeMs { get; set; }

    /// <summary>
    /// 格式化错误信息
    /// </summary>
    /// <returns>格式化的错误信息</returns>
    public string FormatErrors()
    {
        return string.Join(Environment.NewLine, Errors.Select(e => e.ToString()));
    }

    /// <summary>
    /// 格式化警告信息
    /// </summary>
    /// <returns>格式化的警告信息</returns>
    public string FormatWarnings()
    {
        return string.Join(Environment.NewLine, Warnings.Select(w => w.ToString()));
    }
}
