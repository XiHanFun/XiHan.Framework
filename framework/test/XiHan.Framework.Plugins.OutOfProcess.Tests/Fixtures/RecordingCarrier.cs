// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Threading.Channels;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 记下生命周期组件每一次通知的假载体
/// </summary>
/// <remarks>
/// 重试的结果由用例给：<see cref="Recover"/> 每次重试调一次
/// </remarks>
internal sealed class RecordingCarrier : IPluginCarrier
{
    /// <summary>
    /// 排上的重试，按通知的顺序
    /// </summary>
    private readonly Channel<(TimeSpan Delay, int Attempt)> _scheduled = Channel.CreateUnbounded<(TimeSpan Delay, int Attempt)>();

    /// <summary>
    /// 每次重试怎样结束
    /// </summary>
    /// <remarks>
    /// 缺省一直失败
    /// </remarks>
    public Func<Result> Recover { get; set; } = static () => Result.Failure(Error.Literal(PluginErrors.Crashed, "The fake carrier did not come back."));

    /// <inheritdoc />
    public bool IsStopped { get; set; }

    /// <summary>
    /// 重试了多少次
    /// </summary>
    public int Recoveries { get; private set; }

    /// <summary>
    /// 进入过的状态，按顺序
    /// </summary>
    public List<LifecycleState> Entered { get; } = [];

    /// <summary>
    /// 重试成功时报出的累计次数
    /// </summary>
    public List<int> Restarted { get; } = [];

    /// <summary>
    /// 次数用完时报出的上限与时间窗
    /// </summary>
    public List<(int Limit, TimeSpan Window)> Exhausted { get; } = [];

    /// <summary>
    /// 等下一次排上的重试
    /// </summary>
    public async Task<(TimeSpan Delay, int Attempt)> NextScheduledAsync()
    {
        // 缺陷存在时不让用例挂死
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await _scheduled.Reader.ReadAsync(limit.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// 此刻有没有还没取走的排期
    /// </summary>
    public bool HasScheduled => _scheduled.Reader.TryPeek(out _);

    /// <inheritdoc />
    public Task<Result> RecoverAsync(CancellationToken cancellationToken)
    {
        Recoveries++;
        return Task.FromResult(Recover());
    }

    /// <inheritdoc />
    public void OnStateEntered(LifecycleState state) => Entered.Add(state);

    /// <inheritdoc />
    public void OnRestartScheduled(TimeSpan delay, int attempt) => _scheduled.Writer.TryWrite((delay, attempt));

    /// <inheritdoc />
    public void OnRestarted(int restarts) => Restarted.Add(restarts);

    /// <inheritdoc />
    public void OnRestartLimitReached(int limit, TimeSpan window) => Exhausted.Add((limit, window));
}
