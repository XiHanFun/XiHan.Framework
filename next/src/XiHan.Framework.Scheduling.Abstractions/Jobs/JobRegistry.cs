// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 作业登记表：名字 → 定义；同名重复登记是编程错误。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobRegistry
{
    /// <summary>
    /// 名字 → 定义，按名字排序。
    /// </summary>
    private readonly SortedDictionary<string, JobDefinition> _jobs = new(StringComparer.Ordinal);

    /// <summary>
    /// 全部定义，按名字排序。
    /// </summary>
    public IReadOnlyCollection<JobDefinition> Jobs => _jobs.Values;

    /// <summary>
    /// 按名字找定义。
    /// </summary>
    public Maybe<JobDefinition> Find(JobName name) => _jobs.TryGetValue(name.Value, out var definition) ? Maybe.Some(definition) : default;

    /// <summary>
    /// 登记。
    /// </summary>
    public JobRegistry Add(JobDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!_jobs.TryAdd(definition.Name.Value, definition))
        {
            throw new ArgumentException($"Job '{definition.Name}' is registered more than once.", nameof(definition));
        }

        return this;
    }
}
