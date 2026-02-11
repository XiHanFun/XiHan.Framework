#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateNodeVisitor
// Guid:2f6efe3d-d4ae-4280-82bf-648f5e9aaae6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:59:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
