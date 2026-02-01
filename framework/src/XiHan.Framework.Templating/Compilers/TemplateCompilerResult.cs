#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateCompilerResult
// Guid:045d15f2-36ba-4930-8a39-248c872f71fa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:55:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 编译结果
/// </summary>
/// <typeparam name="T">结果类型</typeparam>
public record TemplateCompilerResult<T>
{
    /// <summary>
    /// 编译结果
    /// </summary>
    public T? Result { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 编译时间（毫秒）
    /// </summary>
    public long CompilationTimeMs { get; init; }

    /// <summary>
    /// 编译诊断信息
    /// </summary>
    public ICollection<TemplateCompilerDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// 成功结果
    /// </summary>
    /// <param name="result">编译结果</param>
    /// <param name="compilationTime">编译时间</param>
    /// <returns>编译结果</returns>
    public static TemplateCompilerResult<T> Success(T result, long compilationTime = 0)
        => new() { Result = result, IsSuccess = true, CompilationTimeMs = compilationTime };

    /// <summary>
    /// 失败结果
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="compilationTime">编译时间</param>
    /// <returns>编译结果</returns>
    public static TemplateCompilerResult<T> Failure(string errorMessage, long compilationTime = 0)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, CompilationTimeMs = compilationTime };
}
