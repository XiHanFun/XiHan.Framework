// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Parsers;

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
