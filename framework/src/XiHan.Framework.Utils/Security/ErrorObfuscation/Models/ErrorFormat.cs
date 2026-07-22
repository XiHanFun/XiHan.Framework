// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
