// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 把一次运行变成一次内核调用。
/// </summary>
/// <remarks>
/// 新作用域、新上下文（关联 ID = 运行 ID），经完整管道执行作业；失败或异常都是调用结果。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobDispatcher
{
    /// <summary>
    /// 调用上下文里存放当前运行的键。
    /// </summary>
    private static readonly ContextItemKey<JobRun> _runKey = new("xihan.scheduling.run");

    /// <summary>
    /// 根服务提供者。
    /// </summary>
    /// <remarks>
    /// 每次运行从它创建新作用域。
    /// </remarks>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 应用。
    /// </summary>
    /// <remarks>
    /// 运行经它的完整管道执行。
    /// </remarks>
    private readonly App _app;

    /// <summary>
    /// 创建。
    /// </summary>
    public JobDispatcher(IServiceProvider services, App app)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _app = app ?? throw new ArgumentNullException(nameof(app));
    }

    /// <summary>
    /// 读取本次调用正在执行的运行。
    /// </summary>
    public static Maybe<JobRun> GetRun(InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Get(_runKey);
    }

    /// <summary>
    /// 执行一次运行：<paramref name="jobType"/> 是已登记的作业类型（作用域服务）。
    /// </summary>
    public async ValueTask<Result> DispatchAsync(JobRun run, Type jobType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(jobType);
        // 每次运行一个新作用域与新调用上下文：运行 ID 作为关联 ID，便于在日志与追踪里串起一次运行
        await using var scope = _services.CreateAsyncScope();
        var baggage = new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Correlation] = run.Id };
        var context = App.CreateContext(scope.ServiceProvider, cancellationToken, baggage: baggage);
        context.Set(_runKey, run);
        context.SetOrigin(new InvocationOrigin { Protocol = InvocationProtocols.Job, Target = run.Name.Value });
        try
        {
            return await _app.InvokeAsync(context, async ctx =>
            {
                var job = (IJob)ctx.Services.GetRequiredService(jobType);
                var result = await job.RunAsync(run, ctx, ctx.CancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    ctx.SetOutcome(result);
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // 管道外（如上下文构造、应用未就绪）抛出的异常也翻译为运行失败，调度器据此记录状态
            return Error.Unexpected(SchedulingErrors.JobFailed, SchedulingMessages.JobFailed, details: MessageArguments.Of(("name", run.Name.Value), ("reason", exception.Message)));
        }
    }
}
