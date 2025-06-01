#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CompilationResult
// Guid:b3ce966c-0c04-4430-b042-9909757742eb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 6:11:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace XiHan.Framework.Script.Core;

/// <summary>
/// 编译结果
/// </summary>
public class CompilationResult
{
    /// <summary>
    /// 编译是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 编译后的程序集
    /// </summary>
    public byte[]? Assembly { get; set; }

    /// <summary>
    /// 符号文件
    /// </summary>
    public byte[]? Symbols { get; set; }

    /// <summary>
    /// 诊断信息
    /// </summary>
    public ImmutableArray<Diagnostic> Diagnostics { get; set; }

    /// <summary>
    /// 编译时间（毫秒）
    /// </summary>
    public long CompilationTimeMs { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 程序集名称
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="assembly">程序集字节</param>
    /// <param name="symbols">符号字节</param>
    /// <param name="assemblyName">程序集名称</param>
    /// <returns>成功结果</returns>
    public static CompilationResult Success(byte[] assembly, byte[]? symbols = null, string? assemblyName = null)
    {
        return new CompilationResult
        {
            IsSuccess = true,
            Assembly = assembly,
            Symbols = symbols,
            AssemblyName = assemblyName
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="diagnostics">诊断信息</param>
    /// <returns>失败结果</returns>
    public static CompilationResult Failure(ImmutableArray<Diagnostic> diagnostics)
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        var errorMessage = string.Join(Environment.NewLine, errors.Select(e => e.ToString()));

        return new CompilationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Diagnostics = diagnostics
        };
    }
}
