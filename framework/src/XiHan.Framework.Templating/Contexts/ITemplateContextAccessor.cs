// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板上下文访问器
/// </summary>
public interface ITemplateContextAccessor
{
    /// <summary>
    /// 当前模板上下文
    /// </summary>
    ITemplateContext? Current { get; set; }
}
