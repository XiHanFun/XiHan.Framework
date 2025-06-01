#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptExecutionException
// Guid:022776d8-0718-4b4f-8914-f8a0cb212c0c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 6:18:22
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Exceptions;

/// <summary>
/// 脚本执行异常
/// </summary>
public class ScriptExecutionException : ScriptException
{
    /// <summary>
    /// 初始化脚本执行异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="executionTimeMs">执行时间</param>
    public ScriptExecutionException(string message, string? scriptCode = null, long executionTimeMs = 0)
        : base(message)
    {
        ScriptCode = scriptCode;
        ExecutionTimeMs = executionTimeMs;
    }

    /// <summary>
    /// 初始化脚本执行异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="executionTimeMs">执行时间</param>
    public ScriptExecutionException(string message, Exception? innerException, string? scriptCode = null, long executionTimeMs = 0)
        : base(message, innerException)
    {
        ScriptCode = scriptCode;
        ExecutionTimeMs = executionTimeMs;
    }

    /// <summary>
    /// 脚本代码
    /// </summary>
    public string? ScriptCode { get; }

    /// <summary>
    /// 执行时间(毫秒)
    /// </summary>
    public long ExecutionTimeMs { get; }
}
