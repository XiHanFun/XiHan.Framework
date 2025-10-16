#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ArgumentParseException
// Guid:24bff3e3-e2fd-4853-a2a5-3a030e023190
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 5:09:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DevTools.CommandLine.Arguments;

/// <summary>
/// 参数解析异常
/// </summary>
public class ArgumentParseException : Exception
{
    /// <summary>
    /// 创建参数解析异常
    /// </summary>
    /// <param name="message">错误消息</param>
    public ArgumentParseException(string message) : base(message)
    {
    }

    /// <summary>
    /// 创建参数解析异常
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="argumentName">参数名称</param>
    public ArgumentParseException(string message, string argumentName) : base(message)
    {
        ArgumentName = argumentName;
    }

    /// <summary>
    /// 创建参数解析异常
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public ArgumentParseException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// 参数名称
    /// </summary>
    public string? ArgumentName { get; }
}
