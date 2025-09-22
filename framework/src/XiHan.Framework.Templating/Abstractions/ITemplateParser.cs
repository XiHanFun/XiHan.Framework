#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateParser
// Guid:4b9e3d8f-2c5e-4f8b-9e4d-3f8c5b9e2d8f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Abstractions;

/// <summary>
/// 模板解析器接口
/// </summary>
public interface ITemplateParser
{
    /// <summary>
    /// 解析模板源码
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>模板抽象语法树</returns>
    ITemplateAst Parse(string templateSource);

    /// <summary>
    /// 解析模板文件
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模板抽象语法树</returns>
    Task<ITemplateAst> ParseFileAsync(string templateFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解析模板表达式
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <returns>表达式节点</returns>
    ITemplateExpression ParseExpression(string expression);
}

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

/// <summary>
/// 模板抽象语法树
/// </summary>
public interface ITemplateAst
{
    /// <summary>
    /// 根节点
    /// </summary>
    ITemplateNode RootNode { get; }

    /// <summary>
    /// 模板变量
    /// </summary>
    IReadOnlyCollection<string> Variables { get; }

    /// <summary>
    /// 模板片段
    /// </summary>
    IReadOnlyCollection<string> Partials { get; }

    /// <summary>
    /// 访问节点
    /// </summary>
    /// <param name="visitor">访问器</param>
    void Accept(ITemplateNodeVisitor visitor);
}

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

/// <summary>
/// 模板节点接口
/// </summary>
public interface ITemplateNode
{
    /// <summary>
    /// 节点类型
    /// </summary>
    TemplateNodeType NodeType { get; }

    /// <summary>
    /// 节点内容
    /// </summary>
    string Content { get; }

    /// <summary>
    /// 子节点
    /// </summary>
    IReadOnlyCollection<ITemplateNode> Children { get; }

    /// <summary>
    /// 添加子节点
    /// </summary>
    /// <param name="child">子节点</param>
    void AddChild(ITemplateNode child);

    /// <summary>
    /// 接受访问器
    /// </summary>
    /// <param name="visitor">访问器</param>
    void Accept(ITemplateNodeVisitor visitor);
}

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

/// <summary>
/// 模板循环接口
/// </summary>
public interface ITemplateLoop : ITemplateNode
{
    /// <summary>
    /// 循环项变量名
    /// </summary>
    string ItemVariable { get; }

    /// <summary>
    /// 集合表达式
    /// </summary>
    ITemplateExpression Collection { get; }

    /// <summary>
    /// 循环体
    /// </summary>
    ITemplateNode Body { get; }
}

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
