// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Logging;

/// <summary>
/// 自身日志接口
/// </summary>
public interface IExceptionWithSelfLogging
{
    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="logger"></param>
    void Log(ILogger logger);
}
