// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Core;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Tests.Fakes;

/// <summary>
/// 手写的脚本引擎替身
/// </summary>
/// <remarks>
/// 扩展方法层的价值全在编排逻辑上(失败分支映射、批量顺序、超时包装、诊断分流)，
/// 用替身把 Roslyn 真实编译排除在外，才能对这些分支做确定性断言，也不会让用例耗时随编译器波动。
/// </remarks>
internal sealed class FakeScriptEngine : IScriptEngine
{
    /// <summary>
    /// 按调用顺序记录执行过的脚本代码
    /// </summary>
    public List<string> ExecutedScripts { get; } = [];

    /// <summary>
    /// 按调用顺序记录执行时捕获到的脚本选项
    /// </summary>
    public List<ScriptOptions?> CapturedOptions { get; } = [];

    /// <summary>
    /// 按调用顺序记录编译过的脚本代码
    /// </summary>
    public List<string> CompiledScripts { get; } = [];

    /// <summary>
    /// 按调用顺序记录执行过的脚本文件路径
    /// </summary>
    public List<string> ExecutedFiles { get; } = [];

    /// <summary>
    /// 清除缓存的调用次数
    /// </summary>
    public int ClearCacheCallCount { get; private set; }

    /// <summary>
    /// 自定义执行行为，为空时返回回显脚本代码的成功结果
    /// </summary>
    public Func<string, ScriptOptions?, ScriptResult>? ExecuteHandler { get; set; }

    /// <summary>
    /// 自定义编译行为，为空时返回固定成功结果
    /// </summary>
    public Func<string, ScriptOptions?, CompilationResult>? CompileHandler { get; set; }

    /// <summary>
    /// 执行时抛出的异常，用于驱动扩展方法的异常映射分支
    /// </summary>
    public Exception? ExecuteException { get; set; }

    /// <summary>
    /// 模拟的执行耗时(毫秒)
    /// </summary>
    public int ExecuteDelayMs { get; set; }

    /// <summary>
    /// 对外返回的引擎统计信息
    /// </summary>
    public EngineStatistics Statistics { get; set; } = new();

    /// <summary>
    /// 执行脚本代码
    /// </summary>
    public async Task<ScriptResult> ExecuteAsync(string scriptCode, ScriptOptions? options = null)
    {
        ExecutedScripts.Add(scriptCode);
        CapturedOptions.Add(options);

        if (ExecuteDelayMs > 0)
        {
            await Task.Delay(ExecuteDelayMs);
        }

        if (ExecuteException is not null)
        {
            throw ExecuteException;
        }

        return ExecuteHandler is null ? ScriptResult.Success(scriptCode) : ExecuteHandler(scriptCode, options);
    }

    /// <summary>
    /// 执行脚本代码并返回强类型结果
    /// </summary>
    public async Task<ScriptResult<T>> ExecuteAsync<T>(string scriptCode, ScriptOptions? options = null)
    {
        var result = await ExecuteAsync(scriptCode, options);
        return ScriptResult<T>.FromBase(result);
    }

    /// <summary>
    /// 执行脚本文件
    /// </summary>
    public async Task<ScriptResult> ExecuteFileAsync(string scriptFilePath, ScriptOptions? options = null)
    {
        ExecutedFiles.Add(scriptFilePath);
        CapturedOptions.Add(options);

        if (ExecuteDelayMs > 0)
        {
            await Task.Delay(ExecuteDelayMs);
        }

        if (ExecuteException is not null)
        {
            throw ExecuteException;
        }

        return ExecuteHandler is null ? ScriptResult.Success(scriptFilePath) : ExecuteHandler(scriptFilePath, options);
    }

    /// <summary>
    /// 执行脚本文件并返回强类型结果
    /// </summary>
    public async Task<ScriptResult<T>> ExecuteFileAsync<T>(string scriptFilePath, ScriptOptions? options = null)
    {
        var result = await ExecuteFileAsync(scriptFilePath, options);
        return ScriptResult<T>.FromBase(result);
    }

    /// <summary>
    /// 编译脚本代码
    /// </summary>
    public Task<CompilationResult> CompileAsync(string scriptCode, ScriptOptions? options = null)
    {
        CompiledScripts.Add(scriptCode);

        var result = CompileHandler is null
            ? CompilationResult.Success([1, 2, 3], null, "FakeAssembly")
            : CompileHandler(scriptCode, options);

        return Task.FromResult(result);
    }

    /// <summary>
    /// 创建脚本实例
    /// </summary>
    public async Task<T?> CreateInstanceAsync<T>(string scriptCode, ScriptOptions? options = null) where T : class
    {
        var result = await ExecuteAsync(scriptCode, options);
        return result.IsSuccess ? result.Value as T : null;
    }

    /// <summary>
    /// 评估表达式
    /// </summary>
    public async Task<object?> EvaluateAsync(string expression, ScriptOptions? options = null)
    {
        var result = await ExecuteAsync(expression, options);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// 评估表达式并返回强类型结果
    /// </summary>
    public async Task<T?> EvaluateAsync<T>(string expression, ScriptOptions? options = null)
    {
        var value = await EvaluateAsync(expression, options);
        return value is T typed ? typed : default;
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void ClearCache()
    {
        ClearCacheCallCount++;
    }

    /// <summary>
    /// 获取引擎统计信息
    /// </summary>
    public EngineStatistics GetStatistics()
    {
        return Statistics;
    }
}
