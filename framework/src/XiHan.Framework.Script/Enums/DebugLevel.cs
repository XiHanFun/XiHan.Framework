#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DebugLevel
// Guid:56faa594-bcb9-4503-b970-bfe1be89162a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/01 11:02:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Enums;

/// <summary>
/// 调试级别
/// </summary>
public enum DebugLevel
{
    /// <summary>
    /// 无输出
    /// </summary>
    None,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 信息
    /// </summary>
    Information,

    /// <summary>
    /// 详细
    /// </summary>
    Verbose
}
