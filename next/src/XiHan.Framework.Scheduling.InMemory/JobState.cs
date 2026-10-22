// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Scheduling.Abstractions;

namespace XiHan.Framework.Scheduling.InMemory;

/// <summary>
/// 调度器里一个作业的可变状态；由调度器的锁保护。
/// </summary>
internal sealed class JobState
{
    /// <summary>
    /// 创建。
    /// </summary>
    public JobState(JobDefinition definition)
    {
        Definition = definition;
    }

    /// <summary>
    /// 定义。
    /// </summary>
    public JobDefinition Definition { get; }

    /// <summary>
    /// 下一次按计划运行的时刻；手动计划或未启动为 <see langword="null"/>。
    /// </summary>
    public DateTimeOffset? NextRun { get; set; }

    /// <summary>
    /// 最近一次运行。
    /// </summary>
    public JobOutcome? Last { get; set; }

    /// <summary>
    /// 正在运行的次数。
    /// </summary>
    public int RunningCount { get; set; }

    /// <summary>
    /// 状态快照。
    /// </summary>
    public JobStatus Snapshot() => new(Definition, NextRun, Last, RunningCount);
}
