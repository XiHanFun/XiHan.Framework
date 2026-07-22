// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Exceptions;

/// <summary>
/// 用户友好异常
/// </summary>
public class UserFriendlyException : BusinessException, IUserFriendlyException
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="code"></param>
    /// <param name="details"></param>
    /// <param name="innerException"></param>
    /// <param name="logLevel"></param>
    public UserFriendlyException(string message, string? code = null, string? details = null, Exception? innerException = null, LogLevel logLevel = LogLevel.Warning)
        : base(code, message, details, innerException, logLevel)
    {
        Details = details;
    }

    /// <summary>
    /// 构造函数（可本地化消息）
    /// </summary>
    /// <param name="localizableMessage">可本地化消息（XiHan.Framework.Localization.Abstractions.ILocalizableString），由响应过滤器按请求文化解析</param>
    /// <param name="fallbackMessage">回退消息（本地化键缺失时使用，也作为非本地化日志/兜底文本）</param>
    /// <param name="code"></param>
    /// <param name="details"></param>
    /// <param name="innerException"></param>
    /// <param name="logLevel"></param>
    public UserFriendlyException(object localizableMessage, string? fallbackMessage = null, string? code = null, string? details = null, Exception? innerException = null, LogLevel logLevel = LogLevel.Warning)
        : base(code, fallbackMessage, details, innerException, logLevel)
    {
        Details = details;
        LocalizableMessage = localizableMessage;
    }
}
