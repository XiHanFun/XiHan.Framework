// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 记录一次等待是被取消结束还是自然结束。
/// </summary>
internal sealed class CancellationProbe
{
    /// <summary>
    /// 第一次结束时完成。
    /// </summary>
    private readonly TaskCompletionSource<bool> _observed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 结束方式：被取消为真，等满时长自然结束为假。
    /// </summary>
    public Task<bool> Observed => _observed.Task;

    /// <summary>
    /// 等到令牌取消，最多 <paramref name="limit"/>（防止缺陷存在时用例挂死）。
    /// </summary>
    /// <remarks>
    /// 被取消时记录后重新抛出，调用方照常按取消处理。
    /// </remarks>
    public async Task WaitAsync(TimeSpan limit, CancellationToken cancellationToken)
    {
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
