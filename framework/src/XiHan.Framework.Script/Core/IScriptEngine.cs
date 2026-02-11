#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IScriptEngine
// Guid:8d56e0bc-8387-459d-8941-bc630738b7a6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 06:06:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Core;

/// <summary>
/// 脚本引擎接口
/// 提供动态编译和执行 C# 脚本的核心功能
/// </summary>
public interface IScriptEngine
{
    /// <summary>
    /// 执行脚本代码
    /// </summary>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    Task<ScriptResult> ExecuteAsync(string scriptCode, ScriptOptions? options = null);

    /// <summary>
    /// 执行脚本代码并返回指定类型的结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    Task<ScriptResult<T>> ExecuteAsync<T>(string scriptCode, ScriptOptions? options = null);

    /// <summary>
    /// 执行脚本文件
    /// </summary>
    /// <param name="scriptFilePath">脚本文件路径</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    Task<ScriptResult> ExecuteFileAsync(string scriptFilePath, ScriptOptions? options = null);

    /// <summary>
    /// 执行脚本文件并返回指定类型的结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="scriptFilePath">脚本文件路径</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    Task<ScriptResult<T>> ExecuteFileAsync<T>(string scriptFilePath, ScriptOptions? options = null);

    /// <summary>
    /// 编译脚本代码
    /// </summary>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>编译结果</returns>
    Task<CompilationResult> CompileAsync(string scriptCode, ScriptOptions? options = null);

    /// <summary>
    /// 创建脚本实例
    /// </summary>
    /// <typeparam name="T">实例类型</typeparam>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>脚本实例</returns>
    Task<T?> CreateInstanceAsync<T>(string scriptCode, ScriptOptions? options = null) where T : class;

    /// <summary>
    /// 评估表达式
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="options">脚本选项</param>
    /// <returns>表达式结果</returns>
    Task<object?> EvaluateAsync(string expression, ScriptOptions? options = null);

    /// <summary>
    /// 评估表达式并返回指定类型
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="expression">表达式</param>
    /// <param name="options">脚本选项</param>
    /// <returns>表达式结果</returns>
    Task<T?> EvaluateAsync<T>(string expression, ScriptOptions? options = null);

    /// <summary>
    /// 清除缓存
    /// </summary>
    void ClearCache();

    /// <summary>
    /// 获取引擎统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    EngineStatistics GetStatistics();
}
