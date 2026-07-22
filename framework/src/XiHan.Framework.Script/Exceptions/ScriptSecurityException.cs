// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Exceptions;

/// <summary>
/// 脚本安全异常
/// </summary>
public class ScriptSecurityException : ScriptException
{
    /// <summary>
    /// 初始化脚本安全异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="violationType">违规类型</param>
    public ScriptSecurityException(string message, string violationType) : base(message)
    {
        ViolationType = violationType;
    }

    /// <summary>
    /// 初始化脚本安全异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="violationType">违规类型</param>
    /// <param name="innerException">内部异常</param>
    public ScriptSecurityException(string message, string violationType, Exception innerException)
        : base(message, innerException)
    {
        ViolationType = violationType;
    }

    /// <summary>
    /// 安全违规类型
    /// </summary>
    public string ViolationType { get; }
}
