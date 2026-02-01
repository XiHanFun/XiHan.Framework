#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateInheritanceInfo
// Guid:d27c4074-5301-4856-bcf7-cdc2f82f2389
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:10:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
