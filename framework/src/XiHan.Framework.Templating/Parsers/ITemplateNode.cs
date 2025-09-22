#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateNode
// Guid:e5e42f1b-83f5-4f45-b46e-032c744b7f6d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 3:58:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
