#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateContextAccessor
// Guid:6n1p5p0l-4o7q-5m0n-1p6p-5l0o7q4n1p6p
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Abstractions;

namespace XiHan.Framework.Templating.Implementations;

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
