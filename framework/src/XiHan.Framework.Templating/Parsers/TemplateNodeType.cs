// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
