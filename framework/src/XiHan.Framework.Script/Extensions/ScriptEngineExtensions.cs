#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptEngineExtensions
// Guid:ba00b6af-991e-45cd-ae05-f86de9029009
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 6:07:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.CodeAnalysis;
using XiHan.Framework.Script.Core;
using XiHan.Framework.Script.Enums;
using XiHan.Framework.Script.Exceptions;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Extensions;

/// <summary>
/// 脚本引擎扩展方法
/// </summary>
public static class ScriptEngineExtensions
{
    /// <summary>
    /// 同步执行脚本代码
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static ScriptResult Execute(this IScriptEngine engine, string scriptCode, ScriptOptions? options = null)
    {
        return engine.ExecuteAsync(scriptCode, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 同步执行脚本代码并返回指定类型的结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static ScriptResult<T> Execute<T>(this IScriptEngine engine, string scriptCode, ScriptOptions? options = null)
    {
        return engine.ExecuteAsync<T>(scriptCode, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 同步执行脚本文件
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptFilePath">脚本文件路径</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static ScriptResult ExecuteFile(this IScriptEngine engine, string scriptFilePath, ScriptOptions? options = null)
    {
        return engine.ExecuteFileAsync(scriptFilePath, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 同步执行脚本文件并返回指定类型的结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptFilePath">脚本文件路径</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static ScriptResult<T> ExecuteFile<T>(this IScriptEngine engine, string scriptFilePath, ScriptOptions? options = null)
    {
        return engine.ExecuteFileAsync<T>(scriptFilePath, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 同步编译脚本代码
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>编译结果</returns>
    public static CompilationResult Compile(this IScriptEngine engine, string scriptCode, ScriptOptions? options = null)
    {
        return engine.CompileAsync(scriptCode, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 同步创建脚本实例
    /// </summary>
    /// <typeparam name="T">实例类型</typeparam>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>脚本实例</returns>
    public static T? CreateInstance<T>(this IScriptEngine engine, string scriptCode, ScriptOptions? options = null) where T : class
    {
        return engine.CreateInstanceAsync<T>(scriptCode, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 同步评估表达式
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="expression">表达式</param>
    /// <param name="options">脚本选项</param>
    /// <returns>表达式结果</returns>
    public static object? Evaluate(this IScriptEngine engine, string expression, ScriptOptions? options = null)
    {
        return engine.EvaluateAsync(expression, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 同步评估表达式并返回指定类型
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="engine">脚本引擎</param>
    /// <param name="expression">表达式</param>
    /// <param name="options">脚本选项</param>
    /// <returns>表达式结果</returns>
    public static T? Evaluate<T>(this IScriptEngine engine, string expression, ScriptOptions? options = null)
    {
        return engine.EvaluateAsync<T>(expression, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 执行脚本并获取返回值，失败时抛出异常
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>返回值</returns>
    /// <exception cref="ScriptException">脚本执行失败时抛出</exception>
    public static async Task<object?> ExecuteOrThrowAsync(this IScriptEngine engine, string scriptCode, ScriptOptions? options = null)
    {
        var result = await engine.ExecuteAsync(scriptCode, options);
        return !result.IsSuccess
            ? throw new ScriptExecutionException(result.ErrorMessage ?? "脚本执行失败", result.Exception, scriptCode, result.ExecutionTimeMs)
            : result.Value;
    }

    /// <summary>
    /// 执行脚本并获取强类型返回值，失败时抛出异常
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>强类型返回值</returns>
    /// <exception cref="ScriptException">脚本执行失败时抛出</exception>
    public static async Task<T?> ExecuteOrThrowAsync<T>(this IScriptEngine engine, string scriptCode, ScriptOptions? options = null)
    {
        var result = await engine.ExecuteAsync<T>(scriptCode, options);
        return !result.IsSuccess
            ? throw new ScriptExecutionException(result.ErrorMessage ?? "脚本执行失败", result.Exception, scriptCode, result.ExecutionTimeMs)
            : result.Value;
    }

    /// <summary>
    /// 编译脚本，失败时抛出异常
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>编译结果</returns>
    /// <exception cref="ScriptCompilationException">编译失败时抛出</exception>
    public static async Task<CompilationResult> CompileOrThrowAsync(this IScriptEngine engine, string scriptCode, ScriptOptions? options = null)
    {
        var result = await engine.CompileAsync(scriptCode, options);
        return !result.IsSuccess
            ? throw new ScriptCompilationException(result.ErrorMessage ?? "脚本编译失败", result.Diagnostics)
            : result;
    }

    /// <summary>
    /// 批量执行脚本
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scripts">脚本代码集合</param>
    /// <param name="options">脚本选项</param>
    /// <param name="parallel">是否并行执行</param>
    /// <returns>执行结果集合</returns>
    public static async Task<IEnumerable<ScriptResult>> ExecuteBatchAsync(this IScriptEngine engine,
        IEnumerable<string> scripts, ScriptOptions? options = null, bool parallel = false)
    {
        if (parallel)
        {
            var tasks = scripts.Select(script => engine.ExecuteAsync(script, options));
            return await Task.WhenAll(tasks);
        }
        else
        {
            var results = new List<ScriptResult>();
            foreach (var script in scripts)
            {
                var result = await engine.ExecuteAsync(script, options);
                results.Add(result);
            }
            return results;
        }
    }

    /// <summary>
    /// 执行脚本并返回性能统计信息
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="iterations">执行次数</param>
    /// <param name="options">脚本选项</param>
    /// <returns>性能统计信息</returns>
    public static async Task<PerformanceStatistics> BenchmarkAsync(this IScriptEngine engine,
        string scriptCode, int iterations = 100, ScriptOptions? options = null)
    {
        var results = new List<ScriptResult>();
        var startTime = DateTime.Now;
        var startMemory = GC.GetTotalMemory(false);

        // 预热
        for (var i = 0; i < 5; i++)
        {
            await engine.ExecuteAsync(scriptCode, options);
        }

        // 强制垃圾回收
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 实际测试
        for (var i = 0; i < iterations; i++)
        {
            var result = await engine.ExecuteAsync(scriptCode, options);
            results.Add(result);
        }

        var endTime = DateTime.Now;
        var endMemory = GC.GetTotalMemory(false);

        return new PerformanceStatistics
        {
            TotalIterations = iterations,
            TotalTimeMs = (long)(endTime - startTime).TotalMilliseconds,
            MemoryUsageBytes = endMemory - startMemory,
            SuccessCount = results.Count(r => r.IsSuccess),
            FailureCount = results.Count(r => !r.IsSuccess),
            AverageExecutionTimeMs = results.Average(r => r.ExecutionTimeMs),
            MinExecutionTimeMs = results.Min(r => r.ExecutionTimeMs),
            MaxExecutionTimeMs = results.Max(r => r.ExecutionTimeMs),
            CacheHitCount = results.Count(r => r.FromCache),
            AverageCompilationTimeMs = results.Where(r => r.CompilationTimeMs > 0).DefaultIfEmpty().Average(r => r?.CompilationTimeMs ?? 0)
        };
    }

    /// <summary>
    /// 执行带超时控制的脚本
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="timeoutMs">超时时间(毫秒)</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    /// <exception cref="ScriptTimeoutException">脚本执行超时时抛出</exception>
    public static async Task<ScriptResult> ExecuteWithTimeoutAsync(this IScriptEngine engine,
        string scriptCode, int timeoutMs, ScriptOptions? options = null)
    {
        options ??= ScriptOptions.Default;
        options = options.WithTimeout(timeoutMs);

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var task = engine.ExecuteAsync(scriptCode, options);
            return await task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new ScriptTimeoutException(timeoutMs, scriptCode);
        }
        catch (TimeoutException)
        {
            throw new ScriptTimeoutException(timeoutMs, scriptCode);
        }
    }

    /// <summary>
    /// 安全执行脚本(带完整异常处理)
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static async Task<ScriptResult> ExecuteSafelyAsync(this IScriptEngine engine,
        string scriptCode, ScriptOptions? options = null)
    {
        try
        {
            return await engine.ExecuteAsync(scriptCode, options);
        }
        catch (ScriptTimeoutException ex)
        {
            return ScriptResult.Failure($"脚本执行超时: {ex.TimeoutMs}ms", ex);
        }
        catch (ScriptSecurityException ex)
        {
            return ScriptResult.Failure($"脚本安全检查失败: {ex.Message} (类型: {ex.ViolationType})", ex);
        }
        catch (ScriptLoadException ex)
        {
            return ScriptResult.Failure($"脚本加载失败: {ex.Message} (路径: {ex.ScriptPath})", ex);
        }
        catch (ScriptCompilationException ex)
        {
            return ScriptResult.Failure($"脚本编译失败: {ex.Message}\n{ex.GetFormattedErrors()}", ex);
        }
        catch (ScriptExecutionException ex)
        {
            return ScriptResult.Failure($"脚本执行失败: {ex.Message} (耗时: {ex.ExecutionTimeMs}ms)", ex);
        }
        catch (ScriptException ex)
        {
            return ScriptResult.Failure($"脚本操作失败: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            return ScriptResult.Failure($"未知错误: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 安全执行脚本文件(带完整异常处理)
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptFilePath">脚本文件路径</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static async Task<ScriptResult> ExecuteFileSafelyAsync(this IScriptEngine engine,
        string scriptFilePath, ScriptOptions? options = null)
    {
        try
        {
            return await engine.ExecuteFileAsync(scriptFilePath, options);
        }
        catch (ScriptLoadException ex)
        {
            return ScriptResult.Failure($"脚本文件加载失败: {ex.Message} (文件: {ex.ScriptPath})", ex);
        }
        catch (ScriptSecurityException ex)
        {
            return ScriptResult.Failure($"脚本文件安全检查失败: {ex.Message} (类型: {ex.ViolationType})", ex);
        }
        catch (ScriptTimeoutException ex)
        {
            return ScriptResult.Failure($"脚本文件执行超时: {ex.TimeoutMs}ms", ex);
        }
        catch (ScriptCompilationException ex)
        {
            return ScriptResult.Failure($"脚本文件编译失败: {ex.Message}\n{ex.GetFormattedErrors()}", ex);
        }
        catch (ScriptExecutionException ex)
        {
            return ScriptResult.Failure($"脚本文件执行失败: {ex.Message} (耗时: {ex.ExecutionTimeMs}ms)", ex);
        }
        catch (ScriptException ex)
        {
            return ScriptResult.Failure($"脚本文件操作失败: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            return ScriptResult.Failure($"脚本文件处理时发生未知错误: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 验证脚本安全性
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>安全验证结果</returns>
    public static async Task<SecurityValidationResult> ValidateSecurityAsync(this IScriptEngine engine,
        string scriptCode, ScriptOptions? options = null)
    {
        try
        {
            // 先进行语法验证
            var syntaxResult = await engine.ValidateSyntaxAsync(scriptCode, options);

            if (!syntaxResult.IsValid)
            {
                return new SecurityValidationResult
                {
                    IsSecure = false,
                    Issues = ["脚本包含语法错误"],
                    RiskLevel = SecurityRiskLevel.High
                };
            }

            // 检查危险关键字
            var dangerousKeywords = new[]
            {
                "unsafe", "fixed", "stackalloc",
                "DllImport", "Marshal",
                "Assembly.Load", "Activator.CreateInstance",
                "Process.Start", "File.Delete", "Directory.Delete",
                "Registry.", "Environment.Exit"
            };

            var issues = new List<string>();
            var riskLevel = SecurityRiskLevel.Low;

            foreach (var keyword in dangerousKeywords)
            {
                if (scriptCode.Contains(keyword))
                {
                    issues.Add($"检测到危险关键字: {keyword}");
                    riskLevel = SecurityRiskLevel.High;
                }
            }

            // 检查网络相关操作
            var networkKeywords = new[] { "HttpClient", "WebRequest", "Socket", "TcpClient", "UdpClient" };
            foreach (var keyword in networkKeywords)
            {
                if (scriptCode.Contains(keyword))
                {
                    issues.Add($"检测到网络操作: {keyword}");
                    if (riskLevel < SecurityRiskLevel.Medium)
                    {
                        riskLevel = SecurityRiskLevel.Medium;
                    }
                }
            }

            // 检查文件系统操作
            var fileKeywords = new[] { "File.", "Directory.", "FileStream", "StreamWriter", "StreamReader" };
            foreach (var keyword in fileKeywords)
            {
                if (scriptCode.Contains(keyword))
                {
                    issues.Add($"检测到文件系统操作: {keyword}");
                    if (riskLevel < SecurityRiskLevel.Medium)
                    {
                        riskLevel = SecurityRiskLevel.Medium;
                    }
                }
            }

            return new SecurityValidationResult
            {
                IsSecure = riskLevel == SecurityRiskLevel.Low,
                Issues = issues,
                RiskLevel = riskLevel
            };
        }
        catch (Exception ex)
        {
            return new SecurityValidationResult
            {
                IsSecure = false,
                Issues = [$"安全验证过程中发生错误: {ex.Message}"],
                RiskLevel = SecurityRiskLevel.High
            };
        }
    }

    /// <summary>
    /// 验证脚本语法
    /// </summary>
    /// <param name="engine">脚本引擎</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>语法验证结果</returns>
    public static async Task<SyntaxValidationResult> ValidateSyntaxAsync(this IScriptEngine engine,
        string scriptCode, ScriptOptions? options = null)
    {
        var compilationResult = await engine.CompileAsync(scriptCode, options);

        return new SyntaxValidationResult
        {
            IsValid = compilationResult.IsSuccess,
            Errors = [.. compilationResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)],
            Warnings = [.. compilationResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning)],
            CompilationTimeMs = compilationResult.CompilationTimeMs
        };
    }
}
