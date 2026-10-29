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
    /// 保护记录的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 记录。
    /// </summary>
    private readonly List<(JobRun Run, string? Correlation, InvocationOrigin? Origin)> _runs = [];

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
    /// 记录一次运行。
    /// </summary>
    public void Record(JobRun run, string? correlation, InvocationOrigin? origin)
    {
        ArgumentNullException.ThrowIfNull(run);
        lock (_lock)
        {
            _runs.Add((run, correlation, origin));
        }
    }

    /// <summary>
    /// 等到某作业的记录数达到 <paramref name="count"/>。
    /// </summary>
    /// <remarks>
    /// 超时抛出。
    /// </remarks>
    public async Task WaitAsync(JobName name, int count, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (Runs.Count(r => r.Run.Name == name) < count)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Expected {count} runs of '{name}' but got {Runs.Count(r => r.Run.Name == name)}.");
            }

            await Task.Delay(20).ConfigureAwait(false);
        }
    }
}
