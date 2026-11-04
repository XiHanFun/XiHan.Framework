// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// 让替身 Provider 在观察令牌之前停住、等用例放行的闸门
/// </summary>
/// <remarks>
/// Provider 到闸门时通知 <see cref="Waiting"/>，用例触发完令牌再 <see cref="Release"/>：Provider 观察时该触发的令牌都已触发，结果与触发顺序、线程调度无关
/// </remarks>
internal sealed class ProviderGate
{
    /// <summary>
    /// Provider 已到闸门
    /// </summary>
    private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 用例已放行
    /// </summary>
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 为真时 Provider 观察到取消就抛出，为假时接住取消、交回截断的结果
    /// </summary>
    public bool Throws { get; init; }

    /// <summary>
    /// Provider 到闸门时完成
    /// </summary>
    public Task Waiting => _waiting.Task;

    /// <summary>
    /// Provider 调用：通知已到闸门并等放行
    /// </summary>
    public async Task PassAsync()
    {
        _waiting.TrySetResult();
        await _release.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// 放行
    /// </summary>
    public void Release() => _release.TrySetResult();
}
