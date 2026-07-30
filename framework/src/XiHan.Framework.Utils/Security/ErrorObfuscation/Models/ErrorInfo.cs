// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Models;

/// <summary>
/// 错误信息结构
/// </summary>
internal record ErrorInfo
{
    /// <summary>
    /// 编程语言
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 堆栈跟踪
    /// </summary>
    public required string StackTrace { get; init; }

    /// <summary>
    /// 异常类型
    /// </summary>
    public required string ExceptionType { get; init; }
}
