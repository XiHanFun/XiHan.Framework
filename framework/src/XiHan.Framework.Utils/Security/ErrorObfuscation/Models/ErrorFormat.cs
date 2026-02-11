#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ErrorFormat
// Guid:8b9c0d1e-3f4a-5b6c-7d8e-9f0a1b2c3d4e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Models;

/// <summary>
/// 错误格式枚举
/// </summary>
public enum ErrorFormat
{
    /// <summary>
    /// JSON 对象格式
    /// </summary>
    JsonObject,

    /// <summary>
    /// JSON 数组格式
    /// </summary>
    JsonArray,

    /// <summary>
    /// 纯文本格式
    /// </summary>
    PlainText,

    /// <summary>
    /// XML 格式
    /// </summary>
    Xml,

    /// <summary>
    /// HTML 错误页面格式
    /// </summary>
    Html
}
