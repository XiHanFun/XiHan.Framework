#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateLoop
// Guid:4bd21112-8841-4310-956f-1da7861a54d8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:59:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Parsers;

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
