#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptResult
// Guid:f8850c51-bd9a-4564-97d8-b4937732dfd6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 6:08:53
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace XiHan.Framework.Script.Models;

/// <summary>
/// 脚本执行结果
/// </summary>
public class ScriptResult
{
    /// <summary>
    /// 执行是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 返回值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 编译诊断信息
    /// </summary>
    public ImmutableArray<Diagnostic> Diagnostics { get; set; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 编译时间（毫秒）
    /// </summary>
    public long CompilationTimeMs { get; set; }

    /// <summary>
    /// 内存使用情况
    /// </summary>
    public MemoryUsage? MemoryUsage { get; set; }

    /// <summary>
    /// 是否来自缓存
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// 缓存键
    /// </summary>
    public string? CacheKey { get; set; }

    /// <summary>
    /// 附加数据
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="value">返回值</param>
    /// <returns>成功结果</returns>
    public static ScriptResult Success(object? value = null)
    {
        return new ScriptResult
        {
            IsSuccess = true,
            Value = value
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errorMessage">错误信息</param>
    /// <param name="exception">异常</param>
    /// <returns>失败结果</returns>
    public static ScriptResult Failure(string errorMessage, Exception? exception = null)
    {
        return new ScriptResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }

    /// <summary>
    /// 创建编译失败结果
    /// </summary>
    /// <param name="diagnostics">诊断信息</param>
    /// <returns>编译失败结果</returns>
    public static ScriptResult CompilationFailure(ImmutableArray<Diagnostic> diagnostics)
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        var errorMessage = string.Join(Environment.NewLine, errors.Select(e => e.ToString()));

        return new ScriptResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Diagnostics = diagnostics
        };
    }

    /// <summary>
    /// 转换为字符串
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return IsSuccess ? $"Success: {Value}" : $"Failure: {ErrorMessage}";
    }
}

/// <summary>
/// 泛型脚本执行结果
/// </summary>
/// <typeparam name="T">返回值类型</typeparam>
public class ScriptResult<T> : ScriptResult
{
    /// <summary>
    /// 强类型返回值
    /// </summary>
    public new T? Value
    {
        get => base.Value is T value ? value : default;
        set => base.Value = value;
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="value">返回值</param>
    /// <returns>成功结果</returns>
    public static ScriptResult<T> Success(T? value = default)
    {
        return new ScriptResult<T>
        {
            IsSuccess = true,
            Value = value
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errorMessage">错误信息</param>
    /// <param name="exception">异常</param>
    /// <returns>失败结果</returns>
    public new static ScriptResult<T> Failure(string errorMessage, Exception? exception = null)
    {
        return new ScriptResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }

    /// <summary>
    /// 从基类结果转换
    /// </summary>
    /// <param name="result">基类结果</param>
    /// <returns>泛型结果</returns>
    public static ScriptResult<T> FromBase(ScriptResult result)
    {
        return new ScriptResult<T>
        {
            IsSuccess = result.IsSuccess,
            Value = result.Value is T value ? value : default,
            ErrorMessage = result.ErrorMessage,
            Exception = result.Exception,
            Diagnostics = result.Diagnostics,
            ExecutionTimeMs = result.ExecutionTimeMs,
            CompilationTimeMs = result.CompilationTimeMs,
            MemoryUsage = result.MemoryUsage,
            FromCache = result.FromCache,
            CacheKey = result.CacheKey,
            Metadata = result.Metadata
        };
    }
}
