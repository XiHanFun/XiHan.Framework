// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 面向设计器的活动描述
/// </summary>
/// <remarks>
/// 显示名、分类、说明与输入属性；由登记时给出，<see cref="Definitions.IWorkflowDefinitionCatalog.ActivityTypes"/> 返回按调用方区域渲染过的副本（<see cref="Localize"/>）。面向人的文字给消息键（<see cref="DisplayNameKey"/>、<see cref="DescriptionKey"/>、<see cref="CategoryKey"/>，RFC-0010，ADR-0106）；直接给的文本是没有键或目录里找不到时的回退
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ActivityDescriptor
{
    /// <summary>
    /// 创建
    /// </summary>
    public ActivityDescriptor(string type, string? displayName = null, string? category = null, string? description = null, IReadOnlyList<ActivityInputDescriptor>? inputs = null)
    {
        Type = !string.IsNullOrWhiteSpace(type) ? type : throw new ArgumentException("The activity type must not be empty.", nameof(type));
        DisplayName = displayName ?? type;
        Category = category;
        CategoryName = category;
        Description = description;
        Inputs = inputs ?? [];
    }

    /// <summary>
    /// 活动类型名
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// 显示名
    /// </summary>
    /// <remarks>
    /// 没给时是活动类型名
    /// </remarks>
    public string DisplayName { get; init; }

    /// <summary>
    /// 显示名的消息键
    /// </summary>
    public string? DisplayNameKey { get; init; }

    /// <summary>
    /// 分类（如 <c>Control</c>、<c>Human</c>、<c>Integration</c>）
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// 分类的显示名
    /// </summary>
    /// <remarks>
    /// 没给时同 <see cref="Category"/>
    /// </remarks>
    public string? CategoryName { get; init; }

    /// <summary>
    /// 分类显示名的消息键
    /// </summary>
    public string? CategoryKey { get; init; }

    /// <summary>
    /// 说明
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 说明的消息键
    /// </summary>
    public string? DescriptionKey { get; init; }

    /// <summary>
    /// 输入属性
    /// </summary>
    public IReadOnlyList<ActivityInputDescriptor> Inputs { get; init; }

    /// <summary>
    /// 按当前消息作用域渲染面向人的文字
    /// </summary>
    /// <remarks>
    /// 调用链里是调用方的区域；有消息键且目录里有这个区域的文案时替换显示名、分类显示名、说明与各输入的说明，否则保留原值
    /// </remarks>
    public ActivityDescriptor Localize() => this with
    {
        DisplayName = Resolve(DisplayNameKey) ?? DisplayName,
        CategoryName = Resolve(CategoryKey) ?? CategoryName,
        Description = Resolve(DescriptionKey) ?? Description,
        Inputs = [.. Inputs.Select(static input => input.Localize())],
    };

    /// <summary>
    /// 按当前消息作用域解析消息键
    /// </summary>
    /// <remarks>
    /// 没有键、没有解析器或目录里没有文案时为 <see langword="null"/>（调用方保留原值，不显示键本身）
    /// </remarks>
    internal static string? Resolve(string? key)
        => key is not null && MessageScope.Current is { Resolver: { } resolver } scope && resolver.TryResolve(key, scope.Locale, null, out var text) ? text : null;
}
