// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Exceptions;

/// <summary>
/// 脚本执行异常基类
/// </summary>
public class ScriptException : Exception
{
    /// <summary>
    /// 初始化脚本异常
    /// </summary>
    public ScriptException()
    {
    }

    /// <summary>
    /// 初始化脚本异常
    /// </summary>
    /// <param name="message">异常消息</param>
    public ScriptException(string message) : base(message)
    {
    }

    /// <summary>
    /// 初始化脚本异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public ScriptException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}
