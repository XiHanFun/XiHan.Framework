#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateAstBuilder
// Guid:f7035fcc-cb4b-40e3-8be7-69dc23051997
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:58:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Parsers;

/// <summary>
/// 模板抽象语法树构建器
/// </summary>
public interface ITemplateAstBuilder
{
    /// <summary>
    /// 构建模板节点
    /// </summary>
    /// <param name="nodeType">节点类型</param>
    /// <param name="content">节点内容</param>
    /// <returns>模板节点</returns>
    ITemplateNode CreateNode(TemplateNodeType nodeType, string content);

    /// <summary>
    /// 构建表达式节点
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <returns>表达式节点</returns>
    ITemplateExpression CreateExpression(string expression);

    /// <summary>
    /// 构建条件节点
    /// </summary>
    /// <param name="condition">条件表达式</param>
    /// <param name="trueBlock">真值块</param>
    /// <param name="falseBlock">假值块</param>
    /// <returns>条件节点</returns>
    ITemplateConditional CreateConditional(ITemplateExpression condition, ITemplateNode trueBlock, ITemplateNode? falseBlock = null);

    /// <summary>
    /// 构建循环节点
    /// </summary>
    /// <param name="itemVariable">循环项变量</param>
    /// <param name="collection">集合表达式</param>
    /// <param name="body">循环体</param>
    /// <returns>循环节点</returns>
    ITemplateLoop CreateLoop(string itemVariable, ITemplateExpression collection, ITemplateNode body);
}
