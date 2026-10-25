// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 一个作业。以 <c>AddJob&lt;TJob&gt;(name, schedule)</c> 登记为作用域服务；每次运行在新作用域、新上下文里经完整内核管道执行。
/// 返回失败或抛出异常都算运行失败（异常翻译为 <see cref="SchedulingErrors.JobFailed"/>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IJob
{
    /// <summary>
    /// 运行。
    /// </summary>
    ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken);
}
