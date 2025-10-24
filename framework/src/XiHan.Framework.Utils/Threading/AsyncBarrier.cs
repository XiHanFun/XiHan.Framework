#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AsyncBarrier
// Guid:3b355cbc-1679-4a26-99ef-dd675f14a91d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 3:24:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Threading;

/// <summary>
/// 异步屏障
/// </summary>
/// <remarks>
/// 基于无锁编程技术实现的高性能异步屏障，允许多个异步操作在某个点上等待，直到所有操作都到达该点后才能继续执行
/// 使用原子操作避免锁竞争，支持动态参与者管理、取消令牌、完成回调等高级功能
/// </remarks>
public class AsyncBarrier : IDisposable
{
    #region 私有字段

    /// <summary>
    /// 后处理回调
    /// </summary>
    private readonly Func<int, Task>? _postPhaseAction;

    /// <summary>
    /// 参与者计数和阶段状态的组合值
    /// 高32位：阶段编号，低32位：参与者信息
    /// 参与者信息中：高16位为总数，低16位为当前计数
    /// </summary>
    private long _state;

    /// <summary>
    /// 当前阶段的任务完成源
    /// </summary>
    private volatile TaskCompletionSource<bool>? _currentPhaseCompletion;

    /// <summary>
    /// 是否已释放
    /// </summary>
    private volatile bool _disposed;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化异步屏障
    /// </summary>
    /// <param name="participantCount">参与者数量</param>
    /// <exception cref="ArgumentOutOfRangeException">参与者数量必须大于0且不超过65535</exception>
    public AsyncBarrier(int participantCount)
    {
        if (participantCount is <= 0 or > 0xFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(participantCount), "参与者数量必须在1到65535之间");
        }

        // 初始状态：阶段0，参与者总数为participantCount，当前计数为0
        _state = (long)participantCount << 16;
        _currentPhaseCompletion = new TaskCompletionSource<bool>();
    }

    /// <summary>
    /// 初始化异步屏障（带后处理回调）
    /// </summary>
    /// <param name="participantCount">参与者数量</param>
    /// <param name="postPhaseAction">阶段完成后的回调函数</param>
    /// <exception cref="ArgumentOutOfRangeException">参与者数量必须大于0且不超过65535</exception>
    public AsyncBarrier(int participantCount, Func<int, Task>? postPhaseAction) : this(participantCount)
    {
        _postPhaseAction = postPhaseAction;
    }

    #endregion

    #region 公开属性

    /// <summary>
    /// 当前参与者总数
    /// </summary>
    public int ParticipantCount
    {
        get
        {
            ThrowIfDisposed();
            var state = Interlocked.Read(ref _state);
            return (int)((state >> 16) & 0xFFFF);
        }
    }

    /// <summary>
    /// 当前阶段编号
    /// </summary>
    public long CurrentPhaseNumber
    {
        get
        {
            ThrowIfDisposed();
            var state = Interlocked.Read(ref _state);
            return state >> 32;
        }
    }

    /// <summary>
    /// 当前阶段已到达的参与者数量
    /// </summary>
    public int ParticipantsArrived
    {
        get
        {
            ThrowIfDisposed();
            var state = Interlocked.Read(ref _state);
            return (int)(state & 0xFFFF);
        }
    }

    /// <summary>
    /// 当前阶段剩余的参与者数量
    /// </summary>
    public int ParticipantsRemaining
    {
        get
        {
            ThrowIfDisposed();
            var state = Interlocked.Read(ref _state);
            var totalCount = (int)((state >> 16) & 0xFFFF);
            var currentCount = (int)(state & 0xFFFF);
            return totalCount - currentCount;
        }
    }

    #endregion

    #region 核心方法

    /// <summary>
    /// 发出信号并等待其他参与者到达屏障
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>屏障信息</returns>
    /// <exception cref="InvalidOperationException">屏障已被销毁或无效操作</exception>
    /// <exception cref="OperationCanceledException">操作被取消</exception>
    public async Task<BarrierPostPhaseInfo> SignalAndWaitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        while (true)
        {
            var originalState = Interlocked.Read(ref _state);
            var phaseNumber = originalState >> 32;
            var totalCount = (int)((originalState >> 16) & 0xFFFF);
            var currentCount = (int)(originalState & 0xFFFF);

            if (totalCount == 0)
            {
                throw new InvalidOperationException("屏障没有参与者");
            }

            // 尝试增加当前计数
            var newCount = currentCount + 1;
            var newState = (originalState & 0xFFFFFFFF0000L) | (uint)newCount;

            if (Interlocked.CompareExchange(ref _state, newState, originalState) == originalState)
            {
                // 成功更新状态
                if (newCount >= totalCount)
                {
                    // 我是最后一个参与者，触发阶段完成
                    await CompletePhaseAsync(phaseNumber, totalCount);
                    return new BarrierPostPhaseInfo(phaseNumber, totalCount);
                }

                // 等待其他参与者
                var completionSource = _currentPhaseCompletion;
                if (completionSource != null)
                {
                    await WaitForPhaseCompletionAsync(completionSource, cancellationToken);
                    return new BarrierPostPhaseInfo(phaseNumber, totalCount);
                }
            }
            // 如果CAS失败，重试
        }
    }

    /// <summary>
    /// 发出信号并等待其他参与者到达屏障（带超时）
    /// </summary>
    /// <param name="timeout">超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>屏障信息</returns>
    /// <exception cref="TimeoutException">等待超时</exception>
    public async Task<BarrierPostPhaseInfo> SignalAndWaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return await SignalAndWaitAsync(combinedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"等待屏障超时（{timeout.TotalMilliseconds}ms）");
        }
    }

    /// <summary>
    /// 添加参与者
    /// </summary>
    /// <param name="participantCount">要添加的参与者数量</param>
    /// <returns>当前阶段编号</returns>
    /// <exception cref="ArgumentOutOfRangeException">参与者数量无效</exception>
    /// <exception cref="InvalidOperationException">无效操作</exception>
    public long AddParticipant(int participantCount = 1)
    {
        ThrowIfDisposed();

        if (participantCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(participantCount), "参与者数量必须大于0");
        }

        while (true)
        {
            var originalState = Interlocked.Read(ref _state);
            var phaseNumber = originalState >> 32;
            var totalCount = (int)((originalState >> 16) & 0xFFFF);
            var currentCount = (int)(originalState & 0xFFFF);

            var newTotalCount = totalCount + participantCount;
            if (newTotalCount > 0xFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(participantCount), "参与者总数不能超过65535");
            }

            var newState = (phaseNumber << 32) | ((long)newTotalCount << 16) | (uint)currentCount;

            if (Interlocked.CompareExchange(ref _state, newState, originalState) == originalState)
            {
                return phaseNumber;
            }
            // CAS失败，重试
        }
    }

    /// <summary>
    /// 移除参与者
    /// </summary>
    /// <param name="participantCount">要移除的参与者数量</param>
    /// <returns>当前阶段编号</returns>
    /// <exception cref="ArgumentOutOfRangeException">参与者数量无效</exception>
    /// <exception cref="InvalidOperationException">无效操作</exception>
    public long RemoveParticipant(int participantCount = 1)
    {
        ThrowIfDisposed();

        if (participantCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(participantCount), "参与者数量必须大于0");
        }

        while (true)
        {
            var originalState = Interlocked.Read(ref _state);
            var phaseNumber = originalState >> 32;
            var totalCount = (int)((originalState >> 16) & 0xFFFF);
            var currentCount = (int)(originalState & 0xFFFF);

            var newTotalCount = totalCount - participantCount;
            if (newTotalCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(participantCount), "移除的参与者数量不能超过当前总数");
            }

            var newState = (phaseNumber << 32) | ((long)newTotalCount << 16) | (uint)currentCount;

            if (Interlocked.CompareExchange(ref _state, newState, originalState) == originalState)
            {
                // 检查是否需要触发阶段完成
                if (newTotalCount > 0 && currentCount >= newTotalCount)
                {
                    Task.Run(async () => await CompletePhaseAsync(phaseNumber, newTotalCount));
                }

                return phaseNumber;
            }
            // CAS失败，重试
        }
    }

    /// <summary>
    /// 重置屏障到初始状态
    /// </summary>
    /// <param name="participantCount">新的参与者数量（可选）</param>
    public void Reset(int? participantCount = null)
    {
        ThrowIfDisposed();

        var newParticipantCount = participantCount ?? ParticipantCount;
        if (newParticipantCount is <= 0 or > 0xFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(participantCount), "参与者数量必须在1到65535之间");
        }

        // 取消当前阶段的等待
        var oldCompletion = Interlocked.Exchange(ref _currentPhaseCompletion, new TaskCompletionSource<bool>());
        oldCompletion?.SetException(new InvalidOperationException("屏障已重置"));

        // 重置状态到阶段0
        var newState = (long)newParticipantCount << 16;
        Interlocked.Exchange(ref _state, newState);
    }

    #endregion

    #region 高级功能

    /// <summary>
    /// 等待指定阶段完成
    /// </summary>
    /// <param name="phaseNumber">阶段编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待任务</returns>
    public async Task WaitForPhaseAsync(long phaseNumber, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        while (CurrentPhaseNumber < phaseNumber)
        {
            var completionSource = _currentPhaseCompletion;
            if (completionSource != null && !completionSource.Task.IsCompleted)
            {
                await WaitForPhaseCompletionAsync(completionSource, cancellationToken);
            }

            // 防止忙等待
            if (CurrentPhaseNumber < phaseNumber)
            {
                await Task.Yield();
            }
        }
    }

    /// <summary>
    /// 尝试信号并等待（非阻塞）
    /// </summary>
    /// <returns>如果立即完成返回true，否则返回false</returns>
    public bool TrySignalAndWait()
    {
        ThrowIfDisposed();

        while (true)
        {
            var originalState = Interlocked.Read(ref _state);
            var totalCount = (int)((originalState >> 16) & 0xFFFF);
            var currentCount = (int)(originalState & 0xFFFF);

            if (totalCount == 0 || currentCount >= totalCount)
            {
                return false;
            }

            var newCount = currentCount + 1;
            var newState = (originalState & 0xFFFFFFFF0000L) | (uint)newCount;

            if (Interlocked.CompareExchange(ref _state, newState, originalState) == originalState)
            {
                if (newCount >= totalCount)
                {
                    var phaseNumber = originalState >> 32;
                    Task.Run(async () => await CompletePhaseAsync(phaseNumber, totalCount));
                    return true;
                }
                return false;
            }
            // CAS失败，重试
        }
    }

    /// <summary>
    /// 获取屏障状态信息
    /// </summary>
    /// <returns>屏障状态</returns>
    public BarrierStatus GetStatus()
    {
        ThrowIfDisposed();

        var state = Interlocked.Read(ref _state);
        var phaseNumber = state >> 32;
        var totalCount = (int)((state >> 16) & 0xFFFF);
        var currentCount = (int)(state & 0xFFFF);

        return new BarrierStatus
        {
            CurrentPhase = phaseNumber,
            ParticipantCount = totalCount,
            ParticipantsArrived = currentCount,
            ParticipantsRemaining = totalCount - currentCount,
            IsCompleted = currentCount >= totalCount,
            WaitingTasksCount = 0 // 无锁实现中无法精确统计等待任务数
        };
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 等待阶段完成
    /// </summary>
    /// <param name="completion">完成源</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async Task WaitForPhaseCompletionAsync(TaskCompletionSource<bool> completion, CancellationToken cancellationToken)
    {
        // 创建一个可取消的任务
        var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);

        var completedTask = await Task.WhenAny(completion.Task, cancellationTask);

        if (completedTask == cancellationTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// 完成当前阶段
    /// </summary>
    /// <param name="phaseNumber">阶段编号</param>
    /// <param name="_">参与者数量（未使用）</param>
    private async Task CompletePhaseAsync(long phaseNumber, int _)
    {
        // 执行后处理回调
        if (_postPhaseAction != null)
        {
            try
            {
                await _postPhaseAction((int)phaseNumber);
            }
            catch (Exception ex)
            {
                // 回调异常不应影响屏障正常工作
                Console.WriteLine($"屏障后处理回调异常: {ex.Message}");
            }
        }

        // 准备下一阶段
        var nextPhaseCompletion = new TaskCompletionSource<bool>();

        // 原子地更新阶段并重置计数
        while (true)
        {
            var originalState = Interlocked.Read(ref _state);
            var currentPhase = originalState >> 32;

            if (currentPhase != phaseNumber)
            {
                // 阶段已经被其他线程更新了
                return;
            }

            var totalCount = (int)((originalState >> 16) & 0xFFFF);
            var newState = ((currentPhase + 1) << 32) | ((long)totalCount << 16);

            if (Interlocked.CompareExchange(ref _state, newState, originalState) == originalState)
            {
                // 成功更新到下一阶段
                var oldCompletion = Interlocked.Exchange(ref _currentPhaseCompletion, nextPhaseCompletion);
                oldCompletion?.SetResult(true);
                break;
            }
            // CAS失败，重试
        }
    }

    /// <summary>
    /// 检查是否已释放
    /// </summary>
    /// <exception cref="ObjectDisposedException">对象已释放</exception>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AsyncBarrier));
    }

    #endregion

    #region IDisposable 实现

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源的具体实现
    /// </summary>
    /// <param name="disposing">是否正在释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;

            // 取消所有等待的任务
            var oldCompletion = Interlocked.Exchange(ref _currentPhaseCompletion, null);
            oldCompletion?.SetException(new ObjectDisposedException(nameof(AsyncBarrier)));
        }
    }

    #endregion
}

/// <summary>
/// 屏障阶段完成信息
/// </summary>
public class BarrierPostPhaseInfo
{
    /// <summary>
    /// 初始化屏障阶段完成信息
    /// </summary>
    /// <param name="phaseNumber">阶段编号</param>
    /// <param name="participantCount">参与者数量</param>
    public BarrierPostPhaseInfo(long phaseNumber, int participantCount)
    {
        PhaseNumber = phaseNumber;
        ParticipantCount = participantCount;
    }

    /// <summary>
    /// 完成的阶段编号
    /// </summary>
    public long PhaseNumber { get; }

    /// <summary>
    /// 参与者数量
    /// </summary>
    public int ParticipantCount { get; }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的阶段信息</returns>
    public override string ToString()
    {
        return $"阶段 {PhaseNumber} 完成，参与者数量: {ParticipantCount}";
    }
}

/// <summary>
/// 屏障状态信息
/// </summary>
public class BarrierStatus
{
    /// <summary>
    /// 当前阶段编号
    /// </summary>
    public long CurrentPhase { get; init; }

    /// <summary>
    /// 参与者总数
    /// </summary>
    public int ParticipantCount { get; init; }

    /// <summary>
    /// 已到达的参与者数量
    /// </summary>
    public int ParticipantsArrived { get; init; }

    /// <summary>
    /// 剩余的参与者数量
    /// </summary>
    public int ParticipantsRemaining { get; init; }

    /// <summary>
    /// 当前阶段是否已完成
    /// </summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// 等待中的任务数量
    /// </summary>
    public int WaitingTasksCount { get; init; }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的状态信息</returns>
    public override string ToString()
    {
        return $"阶段: {CurrentPhase}, 参与者: {ParticipantsArrived}/{ParticipantCount}, " +
               $"剩余: {ParticipantsRemaining}, 已完成: {IsCompleted}, 等待任务: {WaitingTasksCount}";
    }
}
