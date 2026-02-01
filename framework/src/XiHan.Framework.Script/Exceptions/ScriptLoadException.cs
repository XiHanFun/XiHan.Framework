#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptLoadException
// Guid:4bb85015-13e8-45a7-aad9-5f8032036d6e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 06:19:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
