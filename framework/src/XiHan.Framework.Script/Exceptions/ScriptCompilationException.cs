#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptCompilationException
// Guid:bdb5f46a-f8a8-4a0b-b48c-f2d09e6c4466
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 6:17:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace XiHan.Framework.Script.Exceptions;

/// <summary>
/// 脚本编译异常
/// </summary>
public class ScriptCompilationException : ScriptException
{
    /// <summary>
    /// 初始化脚本编译异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="diagnostics">诊断信息</param>
    public ScriptCompilationException(string message, ImmutableArray<Diagnostic> diagnostics) : base(message)
    {
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// 初始化脚本编译异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="diagnostics">诊断信息</param>
    /// <param name="innerException">内部异常</param>
    public ScriptCompilationException(string message, ImmutableArray<Diagnostic> diagnostics, Exception innerException)
        : base(message, innerException)
    {
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// 编译诊断信息
    /// </summary>
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// 获取格式化的错误信息
    /// </summary>
    /// <returns>格式化的错误信息</returns>
    public string GetFormattedErrors()
    {
        if (Diagnostics.IsEmpty)
        {
            return Message;
        }

        var errors = Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        return string.Join(Environment.NewLine, errors.Select(FormatDiagnostic));
    }

    /// <summary>
    /// 获取格式化的警告信息
    /// </summary>
    /// <returns>格式化的警告信息</returns>
    public string GetFormattedWarnings()
    {
        if (Diagnostics.IsEmpty)
        {
            return string.Empty;
        }

        var warnings = Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);
        return string.Join(Environment.NewLine, warnings.Select(FormatDiagnostic));
    }

    /// <summary>
    /// 格式化诊断信息
    /// </summary>
    /// <param name="diagnostic">诊断信息</param>
    /// <returns>格式化的诊断信息</returns>
    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location.GetLineSpan();
        return $"{diagnostic.Severity} {diagnostic.Id}: {diagnostic.GetMessage()} " +
               $"at line {location.StartLinePosition.Line + 1}, column {location.StartLinePosition.Character + 1}";
    }
}
