// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 记录启停时机的后台服务
/// </summary>
/// <remarks>
/// 宿主调用 <see cref="StartAsync"/> 与 <see cref="StopAsync"/> 时往日志写 <c>background:start</c> 与 <c>background:stop</c>，与 <see cref="RecordingFeature"/> 共用一份日志，用来观察它相对特性启停的先后
/// </remarks>
/// <param name="log">事件日志</param>
internal sealed class RecordingBackgroundService(List<string> log) : BackgroundService
{
    /// <summary>
    /// 执行体开始运行时完成
    /// </summary>
    public TaskCompletionSource Executing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 记录 <c>background:start</c> 再启动执行体
    /// </summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        log.Add("background:start");
        return base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// 记录 <c>background:stop</c> 再停止执行体
    /// </summary>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        log.Add("background:stop");
        return base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// 标记已开始运行，然后等到停止
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Executing.TrySetResult();
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 停止时取消是正常结束
        }
    }
}
