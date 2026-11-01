// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 记录一次等待何时开始、是被取消结束还是自然结束。
/// </summary>
internal sealed class CancellationProbe
{
    /// <summary>
    /// 第一次开始等待时完成。
    /// </summary>
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 第一次结束时完成。
    /// </summary>
    private readonly TaskCompletionSource<bool> _observed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 开始等待：用例据此确认回调已经到达宿主，再推进时钟。
    /// </summary>
    public Task Entered => _entered.Task;

    /// <summary>
    /// 结束方式：被取消为真，等满时长自然结束为假。
    /// </summary>
    public Task<bool> Observed => _observed.Task;

    /// <summary>
    /// 等待所在调用上下文的截止时间（由宿主操作在等待之前记下）。
    /// </summary>
    public DateTimeOffset? Deadline { get; set; }

    /// <summary>
    /// 等到令牌取消，最多 <paramref name="limit"/>（防止缺陷存在时用例挂死）。
    /// </summary>
    /// <remarks>
    /// 被取消时记录后重新抛出，调用方照常按取消处理。
    /// </remarks>
    public async Task WaitAsync(TimeSpan limit, CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        try
        {
            await Task.Delay(limit, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _observed.TrySetResult(true);
            throw;
        }

        _observed.TrySetResult(false);
    }
}
