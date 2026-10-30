// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 作业类型登记表。
/// </summary>
/// <remarks>
/// 类型键 → 类型。存储里的作业只记类型键，运行时经此表解析；以实例注册进容器，特性与应用共同填充。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobTypeRegistry
{
    /// <summary>
    /// 按键索引的类型。
    /// </summary>
    private readonly Dictionary<string, Type> _types = new(StringComparer.Ordinal);

    /// <summary>
    /// 按类型索引的键。
    /// </summary>
    private readonly Dictionary<Type, string> _keys = [];

    /// <summary>
    /// 已登记的类型键。
    /// </summary>
    /// <remarks>
    /// 按序数排序；管理界面据此列出运行时新建作业可选的类型（ADR-0053）。
    /// </remarks>
    public IReadOnlyList<string> Keys => [.. _types.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// 类型的默认键：类型全名。
    /// </summary>
    public static string KeyOf(Type jobType)
    {
        ArgumentNullException.ThrowIfNull(jobType);
        return jobType.FullName ?? jobType.Name;
    }

    /// <summary>
    /// 按键找类型。
    /// </summary>
    /// <remarks>
    /// 未登记为无值。
    /// </remarks>
    public Maybe<Type> Find(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _types.TryGetValue(key, out var type) ? Maybe.Some(type) : default;
    }

    /// <summary>
    /// 类型的键。
    /// </summary>
    /// <remarks>
    /// 未登记为无值。
    /// </remarks>
    public Maybe<string> KeyFor(Type jobType)
    {
        ArgumentNullException.ThrowIfNull(jobType);
        return _keys.TryGetValue(jobType, out var key) ? Maybe.Some(key) : default;
    }

    /// <summary>
    /// 登记。
    /// </summary>
    /// <remarks>
    /// 同一个键对应不同类型是编程错误，同一类型用同一个键重复登记是幂等的。
    /// </remarks>
    public JobTypeRegistry Add(string key, Type jobType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(jobType);
        if (!typeof(IJob).IsAssignableFrom(jobType))
        {
            throw new ArgumentException($"Job type '{jobType}' must implement {nameof(IJob)}.", nameof(jobType));
        }

        if (_types.TryGetValue(key, out var existing))
        {
            if (existing != jobType)
            {
                throw new ArgumentException($"Job type key '{key}' is already registered for '{existing}'.", nameof(key));
            }

            return this;
        }

        _types[key] = jobType;
        _keys.TryAdd(jobType, key);
        return this;
    }
}
