#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateExpression
// Guid:668c20fd-ecd6-4ac1-85e8-010a935ff5ca
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 3:58:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Parsers;

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
