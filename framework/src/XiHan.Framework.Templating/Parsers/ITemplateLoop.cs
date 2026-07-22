// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
