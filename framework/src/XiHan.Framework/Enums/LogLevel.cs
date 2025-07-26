#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogLevel
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5d0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 日志级别
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 跟踪
    /// </summary>
    Trace = 0,

    /// <summary>
    /// 调试
    /// </summary>
    Debug = 1,

    /// <summary>
    /// 信息
    /// </summary>
    Information = 2,

    /// <summary>
    /// 警告
    /// </summary>
    Warning = 3,

    /// <summary>
    /// 错误
    /// </summary>
    Error = 4,

    /// <summary>
    /// 严重错误
    /// </summary>
    Critical = 5,

    /// <summary>
    /// 无
    /// </summary>
    None = 6
}
