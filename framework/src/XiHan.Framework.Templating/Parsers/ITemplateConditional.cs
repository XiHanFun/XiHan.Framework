// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Parsers;

/// <summary>
/// 模板条件接口
/// </summary>
public interface ITemplateConditional : ITemplateNode
{
    /// <summary>
    /// 条件表达式
    /// </summary>
    ITemplateExpression Condition { get; }

    /// <summary>
    /// 真值块
    /// </summary>
    ITemplateNode TrueBlock { get; }

    /// <summary>
    /// 假值块
    /// </summary>
    ITemplateNode? FalseBlock { get; }
}
