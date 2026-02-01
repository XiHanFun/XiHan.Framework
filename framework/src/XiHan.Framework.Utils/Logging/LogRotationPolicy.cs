#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogRotationPolicy
// Guid:3d613bb8-8ddc-4c6f-9bcc-ce7c0ce3d853
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/18 06:14:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
