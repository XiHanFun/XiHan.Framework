// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Parsers;

/// <summary>
/// 模板节点访问器
/// </summary>
public interface ITemplateNodeVisitor
{
    /// <summary>
    /// 访问文本节点
    /// </summary>
    /// <param name="node">文本节点</param>
    void VisitText(ITemplateNode node);

    /// <summary>
    /// 访问表达式节点
    /// </summary>
    /// <param name="expression">表达式节点</param>
    void VisitExpression(ITemplateExpression expression);

    /// <summary>
    /// 访问条件节点
    /// </summary>
    /// <param name="conditional">条件节点</param>
    void VisitConditional(ITemplateConditional conditional);

    /// <summary>
    /// 访问循环节点
    /// </summary>
    /// <param name="loop">循环节点</param>
    void VisitLoop(ITemplateLoop loop);

    /// <summary>
    /// 访问片段节点
    /// </summary>
    /// <param name="node">片段节点</param>
    void VisitPartial(ITemplateNode node);

    /// <summary>
    /// 访问块节点
    /// </summary>
    /// <param name="node">块节点</param>
    void VisitBlock(ITemplateNode node);
}
