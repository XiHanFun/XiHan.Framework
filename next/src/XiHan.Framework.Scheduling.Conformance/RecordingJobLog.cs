// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// 套件作业的记录与行为开关。
/// </summary>
/// <remarks>
/// 记录每次运行与上下文里的关联 ID、调用来源；可让运行失败、抛出或停留一段时间。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RecordingJobLog
{
    /// <summary>
    /// 保护记录与等待者的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 记录。
    /// </summary>
    private readonly List<(JobRun Run, string? Correlation, InvocationOrigin? Origin)> _runs = [];

    /// <summary>
    /// 等待某作业记录数达到目标的等待者。
    /// </summary>
    private readonly List<(JobName Name, int Count, TaskCompletionSource Reached)> _waiters = [];

    /// <summary>
    /// 下一次运行返回的失败。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时成功。
    /// </remarks>
    public Error? FailWith { get; set; }

    /// <summary>
    /// 下一次运行是否抛出异常。
    /// </summary>
    public bool Throw { get; set; }

    /// <summary>
    /// 每次运行停留的时间。
    /// </summary>
    /// <remarks>
    /// 按作业注入的 <see cref="TimeProvider"/> 计时，响应取消。
    /// </remarks>
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// 已记录的运行快照。
    /// </summary>
    public IReadOnlyList<(JobRun Run, string? Correlation, InvocationOrigin? Origin)> Runs
    {
        get
        {
            lock (_lock)
            {
                return _runs.ToList();
            }
        }
    }

    /// <summary>
    /// 记录一次运行，唤醒数量已达到的等待者。
    /// </summary>
    public void Record(JobRun run, string? correlation, InvocationOrigin? origin)
    {
        ArgumentNullException.ThrowIfNull(run);
        List<TaskCompletionSource> reached = [];
        lock (_lock)
        {
            _runs.Add((run, correlation, origin));
            for (var index = _waiters.Count - 1; index >= 0; index--)
            {
                if (CountOf(_waiters[index].Name) >= _waiters[index].Count)
                {
                    reached.Add(_waiters[index].Reached);
                    _waiters.RemoveAt(index);
                }
            }
        }

        // 锁外完成：等待者的续体异步执行，不在作业的线程上、也不在锁内运行
        foreach (var waiter in reached)
        {
            waiter.TrySetResult();
        }
    }

    /// <summary>
    /// 等到某作业的记录数达到 <paramref name="count"/>。
    /// </summary>
    /// <remarks>
    /// 由记录唤醒，不轮询；<paramref name="timeout"/> 只是防止用例挂死的上限，超过抛出 <see cref="TimeoutException"/>。
    /// </remarks>
    public async Task WaitAsync(JobName name, int count, TimeSpan timeout)
    {
        TaskCompletionSource reached;
        lock (_lock)
        {
            if (CountOf(name) >= count)
            {
                return;
            }

            reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((name, count, reached));
        }

        try
        {
            await reached.Task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            lock (_lock)
            {
                _waiters.RemoveAll(waiter => waiter.Reached == reached);
            }

            throw new TimeoutException($"Expected {count} runs of '{name}' but got {Runs.Count(r => r.Run.Name == name)}.");
        }
    }

    /// <summary>
    /// 某作业已记录的次数（调用方持有锁）。
    /// </summary>
    private int CountOf(JobName name) => _runs.Count(r => r.Run.Name == name);
}
