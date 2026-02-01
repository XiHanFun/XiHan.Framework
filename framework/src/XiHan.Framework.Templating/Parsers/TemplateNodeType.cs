#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateNodeType
// Guid:6ed617a8-7c76-478e-b000-d6b263ee2c79
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:59:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Parsers;

/// <summary>
/// 模板节点类型
/// </summary>
public enum TemplateNodeType
{
    /// <summary>
    /// 文本节点
    /// </summary>
    Text,

    /// <summary>
    /// 表达式节点
    /// </summary>
    Expression,

    /// <summary>
    /// 条件节点
    /// </summary>
    Conditional,

    /// <summary>
    /// 循环节点
    /// </summary>
    Loop,

    /// <summary>
    /// 片段节点
    /// </summary>
    Partial,

    /// <summary>
    /// 块节点
    /// </summary>
    Block,

    /// <summary>
    /// 继承节点
    /// </summary>
    Extends
}
