#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateAst
// Guid:2fa796ed-69b7-4d15-8d22-ad16db729c36
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:58:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
