#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptTimeoutException
// Guid:5f39a7bc-a6cf-4fb7-beff-a347f57eeb33
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 6:18:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
