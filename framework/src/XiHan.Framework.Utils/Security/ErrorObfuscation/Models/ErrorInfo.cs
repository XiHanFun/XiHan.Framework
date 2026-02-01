#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ErrorInfo
// Guid:9c0d1e2f-4a5b-6c7d-8e9f-0a1b2c3d4e5f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
