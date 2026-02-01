#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateContextAccessor
// Guid:44c0c1ff-cf6e-43f7-8972-389e33564787
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:53:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
