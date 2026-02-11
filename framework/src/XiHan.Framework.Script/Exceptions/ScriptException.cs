#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptException
// Guid:bc1c1cf1-95ef-4fb8-8b31-d685635a01c2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 06:07:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
