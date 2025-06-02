#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IHasLogLevel
// Guid:379d82c0-7354-45f0-93a8-d271aba2d9b3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/23 0:54:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
