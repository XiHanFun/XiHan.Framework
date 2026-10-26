// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 面向设计器的活动描述：显示名、分类、说明与输入属性；由登记时给出，<see cref="Definitions.IWorkflowDefinitionCatalog.ActivityTypes"/> 返回。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ActivityDescriptor
{
    /// <summary>
    /// 创建。
    /// </summary>
    public ActivityDescriptor(string type, string? displayName = null, string? category = null, string? description = null, IReadOnlyList<ActivityInputDescriptor>? inputs = null)
    {
        Type = !string.IsNullOrWhiteSpace(type) ? type : throw new ArgumentException("The activity type must not be empty.", nameof(type));
        DisplayName = displayName ?? type;
        Category = category;
        Description = description;
        Inputs = inputs ?? [];
    }

    /// <summary>
    /// 活动类型名。
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// 显示名。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 分类（如 <c>Control</c>、<c>Human</c>、<c>Integration</c>）。
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// 输入属性。
    /// </summary>
    public IReadOnlyList<ActivityInputDescriptor> Inputs { get; }
}
