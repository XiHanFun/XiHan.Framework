// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 活动登记表：类型名 → 定义；以实例注册，特性与应用共同填充。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ActivityRegistry
{
    /// <summary>
    /// 类型名 → 定义，按名字排序。
    /// </summary>
    private readonly SortedDictionary<string, ActivityDefinition> _activities = new(StringComparer.Ordinal);

    /// <summary>
    /// 全部定义，按类型名排序。
    /// </summary>
    public IReadOnlyCollection<ActivityDefinition> Activities => _activities.Values;

    /// <summary>
    /// 按类型名找定义。
    /// </summary>
    public Maybe<ActivityDefinition> Find(ActivityType type) => _activities.TryGetValue(type.Value, out var definition) ? Maybe.Some(definition) : default;

    /// <summary>
    /// 是否已登记。
    /// </summary>
    public bool Contains(ActivityType type) => _activities.ContainsKey(type.Value);

    /// <summary>
    /// 登记；同一类型名登记两个不同的类是编程错误，同一个类重复登记忽略。
    /// </summary>
    public ActivityRegistry Add(ActivityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (_activities.TryGetValue(definition.Type.Value, out var existing))
        {
            if (existing.ActivityClass != definition.ActivityClass)
            {
                throw new ArgumentException($"Activity type '{definition.Type}' is already registered with '{existing.ActivityClass}'.", nameof(definition));
            }

            return this;
        }

        _activities[definition.Type.Value] = definition;
        return this;
    }
}
