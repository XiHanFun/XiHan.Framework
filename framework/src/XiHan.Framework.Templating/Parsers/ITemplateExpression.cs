// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Parsers;

/// <summary>
/// 模板表达式接口
/// </summary>
public interface ITemplateExpression : ITemplateNode
{
    /// <summary>
    /// 表达式文本
    /// </summary>
    string Expression { get; }

    /// <summary>
    /// 计算表达式
    /// </summary>
    /// <param name="context">模板上下文</param>
    /// <returns>计算结果</returns>
    object? Evaluate(ITemplateContext context);
}
