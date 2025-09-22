#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateConditional
// Guid:3c707974-c6e7-4ef8-b014-130cf0095034
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 3:59:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Parsers;

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
