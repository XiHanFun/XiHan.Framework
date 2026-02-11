#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateContextAccessor
// Guid:158fe51c-7490-4f87-a582-643589c2821a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:05:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板上下文访问器实现
/// </summary>
public class TemplateContextAccessor : ITemplateContextAccessor
{
    private static readonly AsyncLocal<ITemplateContext?> CurrentContext = new();

    /// <summary>
    /// 当前模板上下文
    /// </summary>
    public ITemplateContext? Current
    {
        get => CurrentContext.Value;
        set => CurrentContext.Value = value;
    }
}
