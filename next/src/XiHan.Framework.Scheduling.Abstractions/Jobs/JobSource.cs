// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 作业的来源。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum JobSource
{
    /// <summary>
    /// 代码登记（<c>AddJob</c>），每次启动重新写入存储。
    /// </summary>
    Code = 0,

    /// <summary>
    /// 运行时登记（<see cref="Schedulers.IJobScheduler.ScheduleAsync"/>），只存在于存储。
    /// </summary>
    Runtime = 1,

    /// <summary>
    /// 代码登记、运行时改过的作业。
    /// </summary>
    /// <remarks>
    /// 由 <see cref="Schedulers.IJobScheduler.ScheduleAsync"/> 替换代码作业而来；启动时代码定义没变就保留运行时的修改（ADR-0053）。
    /// </remarks>
    Overridden = 2,
}
