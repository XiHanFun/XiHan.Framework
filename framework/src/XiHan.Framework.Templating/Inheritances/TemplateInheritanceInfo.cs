// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板继承信息
/// </summary>
public record TemplateInheritanceInfo
{
    /// <summary>
    /// 是否有继承
    /// </summary>
    public bool HasInheritance { get; init; }

    /// <summary>
    /// 父布局名称
    /// </summary>
    public string? ParentLayout { get; init; }

    /// <summary>
    /// 块定义
    /// </summary>
    public IDictionary<string, TemplateBlock> Blocks { get; init; } = new Dictionary<string, TemplateBlock>();

    /// <summary>
    /// 内容区域
    /// </summary>
    public string? ContentArea { get; init; }

    /// <summary>
    /// 继承链
    /// </summary>
    public IList<string> InheritanceChain { get; init; } = [];
}
