// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 登记的活动：类型名、实现类与出边选择方式。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ActivityDefinition
{
    /// <summary>
    /// 创建；<paramref name="activityClass"/> 必须实现 <see cref="IActivity"/>。
    /// </summary>
    public ActivityDefinition(ActivityType type, Type activityClass, ActivityOutgoing outgoing = ActivityOutgoing.AllMatched, ActivityDescriptor? descriptor = null)
    {
        Type = type;
        ActivityClass = activityClass ?? throw new ArgumentNullException(nameof(activityClass));
        if (!typeof(IActivity).IsAssignableFrom(activityClass))
        {
            throw new ArgumentException($"Activity class '{activityClass}' must implement {nameof(IActivity)}.", nameof(activityClass));
        }

        Outgoing = outgoing;
        Descriptor = descriptor ?? new ActivityDescriptor(type.Value);
    }

    /// <summary>
    /// 类型名。
    /// </summary>
    public ActivityType Type { get; }

    /// <summary>
    /// 实现类（作用域服务）。
    /// </summary>
    public Type ActivityClass { get; }

    /// <summary>
    /// 出边选择方式。
    /// </summary>
    public ActivityOutgoing Outgoing { get; }

    /// <summary>
    /// 面向设计器的描述（显示名、分类、输入属性）。
    /// </summary>
    public ActivityDescriptor Descriptor { get; }
}
