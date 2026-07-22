// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Exceptions;

/// <summary>
/// 脚本超时异常
/// </summary>
public class ScriptTimeoutException : ScriptExecutionException
{
    /// <summary>
    /// 初始化脚本超时异常
    /// </summary>
    /// <param name="timeoutMs">超时时间</param>
    /// <param name="scriptCode">脚本代码</param>
    public ScriptTimeoutException(int timeoutMs, string? scriptCode = null)
        : base($"脚本执行超时({timeoutMs}ms)", scriptCode, timeoutMs)
    {
        TimeoutMs = timeoutMs;
    }

    /// <summary>
    /// 初始化脚本超时异常
    /// </summary>
    /// <param name="timeoutMs">超时时间</param>
    /// <param name="innerException">内部异常</param>
    /// <param name="scriptCode">脚本代码</param>
    public ScriptTimeoutException(int timeoutMs, Exception innerException, string? scriptCode = null)
        : base($"脚本执行超时({timeoutMs}ms)", innerException, scriptCode, timeoutMs)
    {
        TimeoutMs = timeoutMs;
    }

    /// <summary>
    /// 超时时间(毫秒)
    /// </summary>
    public int TimeoutMs { get; }
}
