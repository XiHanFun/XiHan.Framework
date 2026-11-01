// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 记录一次等待何时开始。
/// </summary>
/// <remarks>
/// 用例据此确认插件的回调已经到达宿主、插件调用正在进行，再触发截止时间或取消。
/// </remarks>
internal sealed class CancellationProbe
{
    /// <summary>
    /// 第一次开始等待时完成。
    /// </summary>
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 开始等待。
    /// </summary>
    public Task Entered => _entered.Task;

    /// <summary>
    /// 等到令牌取消，最多 <paramref name="limit"/>（防止缺陷存在时用例挂死）。
    /// </summary>
    /// <remarks>
    /// 被取消时照常抛出，调用方按取消处理。
    /// </remarks>
    public async Task WaitAsync(TimeSpan limit, CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        await Task.Delay(limit, cancellationToken).ConfigureAwait(false);
    }
}
