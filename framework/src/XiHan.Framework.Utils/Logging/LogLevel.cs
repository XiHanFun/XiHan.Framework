#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogLevel
// Guid:5e2d8b83-6a1b-4f8f-9d3a-3aef5a0b1f21
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/18 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 日志等级
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 无输出
    /// </summary>
    None = 0,

    /// <summary>
    /// 错误
    /// </summary>
    Error = 1,

    /// <summary>
    /// 警告
    /// </summary>
    Warn = 2,

    /// <summary>
    /// 处理
    /// </summary>
    Handle = 3,

    /// <summary>
    /// 成功
    /// </summary>
    Success = 4,

    /// <summary>
    /// 信息
    /// </summary>
    Info = 5,
}
