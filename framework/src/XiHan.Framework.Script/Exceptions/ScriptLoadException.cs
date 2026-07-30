// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Exceptions;

/// <summary>
/// 脚本加载异常
/// </summary>
public class ScriptLoadException : ScriptException
{
    /// <summary>
    /// 初始化脚本加载异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="scriptPath">脚本路径</param>
    public ScriptLoadException(string message, string? scriptPath = null) : base(message)
    {
        ScriptPath = scriptPath;
    }

    /// <summary>
    /// 初始化脚本加载异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    /// <param name="scriptPath">脚本路径</param>
    public ScriptLoadException(string message, Exception innerException, string? scriptPath = null)
        : base(message, innerException)
    {
        ScriptPath = scriptPath;
    }

    /// <summary>
    /// 脚本路径
    /// </summary>
    public string? ScriptPath { get; }
}
