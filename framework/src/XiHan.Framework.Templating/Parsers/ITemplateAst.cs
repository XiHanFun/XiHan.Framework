// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Parsers;

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
