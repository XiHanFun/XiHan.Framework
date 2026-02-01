#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptType
// Guid:bf4bd124-dfb3-4039-ac92-ea1588294a53
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 06:10:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Enums;

/// <summary>
/// 脚本类型
/// </summary>
public enum ScriptType
{
    /// <summary>
    /// 语句脚本
    /// </summary>
    Statement,

    /// <summary>
    /// 表达式脚本
    /// </summary>
    Expression,

    /// <summary>
    /// 类定义脚本
    /// </summary>
    Class,

    /// <summary>
    /// 方法脚本
    /// </summary>
    Method,

    /// <summary>
    /// 完整程序
    /// </summary>
    Program
}
