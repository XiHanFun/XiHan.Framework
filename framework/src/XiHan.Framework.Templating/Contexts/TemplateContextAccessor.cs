// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
