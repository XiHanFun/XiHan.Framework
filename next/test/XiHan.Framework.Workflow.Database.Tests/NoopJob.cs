// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 什么都不做的作业。
/// </summary>
/// <remarks>
/// 只用来让调度在套件宿主里发布作业事件；触发器一年到期一次，用例期间不运行。
/// </remarks>
internal sealed class NoopJob : IJob
{
    /// <inheritdoc />
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Success());
}
