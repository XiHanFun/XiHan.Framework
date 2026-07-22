// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
