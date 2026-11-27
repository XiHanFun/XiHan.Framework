// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 作业登记表
/// </summary>
/// <remarks>
/// 名字 → 定义；同名重复登记与登记时就判定得出的不合法定义（说明与数据里的孤立代理项、触发器名重复）是编程错误（ADR-0335）。只经 <c>AddJob</c> 在装配时填充，装配之后只读（ADR-0135）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobRegistry
{
    /// <summary>
    /// 名字 → 定义，按名字排序
    /// </summary>
    private readonly SortedDictionary<string, JobDefinition> _jobs = new(StringComparer.Ordinal);

    /// <summary>
    /// 全部定义，按名字排序
    /// </summary>
    public IReadOnlyCollection<JobDefinition> Jobs => _jobs.Values;

    /// <summary>
    /// 按名字找定义
    /// </summary>
    public Maybe<JobDefinition> Find(JobName name) => _jobs.TryGetValue(name.Value, out var definition) ? Maybe.Some(definition) : default;

    /// <summary>
    /// 登记
    /// </summary>
    /// <remarks>
    /// 定义不合法即抛出 <see cref="ArgumentException"/>，不等到写入存储：以前进入服务状态后每个周期同步都以校验错误失败、记一条日志，按名字排在它后面的代码作业也一直写不进去（ADR-0335）。日历此刻可能还没登记，留到调度器启动时核对
    /// </remarks>
    internal JobRegistry Add(JobDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (JobDefinitionRules.FindProblem(definition, calendars: null).TryGetValue(out var problem))
        {
            throw new ArgumentException($"Job '{definition.Name}' is invalid: {problem}.", nameof(definition));
        }

        if (!_jobs.TryAdd(definition.Name.Value, definition))
        {
            throw new ArgumentException($"Job '{definition.Name}' is registered more than once.", nameof(definition));
        }

        return this;
    }
}
