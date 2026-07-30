// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Exceptions.Abstracts;

/// <summary>
/// 日志级别接口
/// </summary>
public interface IHasLogLevel
{
    /// <summary>
    /// 日志级别
    /// </summary>
    LogLevel LogLevel { get; set; }
}
