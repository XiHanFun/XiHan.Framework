// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Parsers;

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 表达式树编译器
/// </summary>
public interface IExpressionTreeCompiler : ITemplateCompiler<Expression<Func<ITemplateContext, string>>>
{
    /// <summary>
    /// 编译表达式
    /// </summary>
    /// <param name="expression">模板表达式</param>
    /// <returns>表达式树</returns>
    Expression<Func<ITemplateContext, object?>> CompileExpression(ITemplateExpression expression);

    /// <summary>
    /// 编译条件表达式
    /// </summary>
    /// <param name="conditional">条件节点</param>
    /// <returns>条件表达式树</returns>
    Expression<Func<ITemplateContext, bool>> CompileConditional(ITemplateConditional conditional);
}
