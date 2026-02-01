#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogFormat
// Guid:529ac4c1-accc-4c90-a63a-30b894bf3d42
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/18 06:14:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 日志格式
/// </summary>
public enum LogFormat
{
    /// <summary>
    /// 纯文本格式
    /// </summary>
    Text,

    /// <summary>
    /// JSON 格式
    /// </summary>
    Json,

    /// <summary>
    /// 结构化格式（键值对）
    /// </summary>
    Structured
}
