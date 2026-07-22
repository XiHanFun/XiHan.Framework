// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 日志轮转策略
/// </summary>
public enum LogRotationPolicy
{
    /// <summary>
    /// 按文件大小轮转
    /// </summary>
    Size,

    /// <summary>
    /// 按时间轮转（每天）
    /// </summary>
    Daily,

    /// <summary>
    /// 按时间轮转（每小时）
    /// </summary>
    Hourly,

    /// <summary>
    /// 混合策略（大小 + 时间）
    /// </summary>
    Hybrid
}
