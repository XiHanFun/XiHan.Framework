#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanScript
// Guid:63414530-6120-44c9-9f60-71943a75b861
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 6:27:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Script.Core;
using XiHan.Framework.Script.Extensions;
using XiHan.Framework.Script.Models;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script;

/// <summary>
/// 简化的脚本执行器
/// 提供静态方法快速执行脚本
/// </summary>
public static class XiHanScript
{
    private static readonly Lazy<IScriptEngine> _defaultEngine = new(() => ScriptEngineFactory.CreateDefault());

    /// <summary>
    /// 默认脚本引擎
    /// </summary>
    public static IScriptEngine Engine => _defaultEngine.Value;

    /// <summary>
    /// 执行脚本代码
    /// </summary>
    /// <param name="code">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static async Task<ScriptResult> RunAsync(string code, ScriptOptions? options = null)
    {
        return await Engine.ExecuteAsync(code, options);
    }

    /// <summary>
    /// 执行脚本代码（同步）
    /// </summary>
    /// <param name="code">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static ScriptResult Run(string code, ScriptOptions? options = null)
    {
        return Engine.Execute(code, options);
    }

    /// <summary>
    /// 执行脚本并返回强类型结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="code">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static async Task<ScriptResult<T>> RunAsync<T>(string code, ScriptOptions? options = null)
    {
        return await Engine.ExecuteAsync<T>(code, options);
    }

    /// <summary>
    /// 执行脚本并返回强类型结果（同步）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="code">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static ScriptResult<T> Run<T>(string code, ScriptOptions? options = null)
    {
        return Engine.Execute<T>(code, options);
    }

    /// <summary>
    /// 评估表达式
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="options">脚本选项</param>
    /// <returns>表达式结果</returns>
    public static async Task<object?> EvalAsync(string expression, ScriptOptions? options = null)
    {
        return await Engine.EvaluateAsync(expression, options);
    }

    /// <summary>
    /// 评估表达式（同步）
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="options">脚本选项</param>
    /// <returns>表达式结果</returns>
    public static object? Eval(string expression, ScriptOptions? options = null)
    {
        return Engine.Evaluate(expression, options);
    }

    /// <summary>
    /// 评估表达式并返回强类型结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="expression">表达式</param>
    /// <param name="options">脚本选项</param>
    /// <returns>表达式结果</returns>
    public static async Task<T?> EvalAsync<T>(string expression, ScriptOptions? options = null)
    {
        return await Engine.EvaluateAsync<T>(expression, options);
    }

    /// <summary>
    /// 评估表达式并返回强类型结果（同步）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="expression">表达式</param>
    /// <param name="options">脚本选项</param>
    /// <returns>表达式结果</returns>
    public static T? Eval<T>(string expression, ScriptOptions? options = null)
    {
        return Engine.Evaluate<T>(expression, options);
    }

    /// <summary>
    /// 执行脚本文件
    /// </summary>
    /// <param name="filePath">脚本文件路径</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static async Task<ScriptResult> RunFileAsync(string filePath, ScriptOptions? options = null)
    {
        return await Engine.ExecuteFileAsync(filePath, options);
    }

    /// <summary>
    /// 执行脚本文件（同步）
    /// </summary>
    /// <param name="filePath">脚本文件路径</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public static ScriptResult RunFile(string filePath, ScriptOptions? options = null)
    {
        return Engine.ExecuteFile(filePath, options);
    }

    /// <summary>
    /// 创建脚本引擎构建器
    /// </summary>
    /// <returns>构建器实例</returns>
    public static ScriptEngineBuilder CreateBuilder()
    {
        return new ScriptEngineBuilder();
    }
}
