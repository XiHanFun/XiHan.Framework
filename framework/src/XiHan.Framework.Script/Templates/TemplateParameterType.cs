#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateParameterType
// Guid:85b487ee-f070-4d82-b41e-6bdc928e3fa6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 8:09:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Templates;

/// <summary>
/// 模板参数类型
/// </summary>
public enum TemplateParameterType
{
    /// <summary>
    /// 字符串
    /// </summary>
    String,

    /// <summary>
    /// 整数
    /// </summary>
    Integer,

    /// <summary>
    /// 双精度浮点数
    /// </summary>
    Double,

    /// <summary>
    /// 布尔值
    /// </summary>
    Boolean,

    /// <summary>
    /// 枚举
    /// </summary>
    Enum
}
