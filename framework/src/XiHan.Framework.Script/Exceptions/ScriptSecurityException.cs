#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptSecurityException
// Guid:48aad3e6-4503-463a-b92b-7601f85dfeb8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 06:18:56
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
