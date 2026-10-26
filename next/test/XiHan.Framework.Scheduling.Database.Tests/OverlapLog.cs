// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 多节点用例的运行记录：每次运行所在的节点、运行 ID 与开始、结束时刻，供断言区间不交叠。
/// </summary>
internal sealed class OverlapLog
{
    /// <summary>
    /// 保护记录的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 记录。
    /// </summary>
    private readonly List<(string NodeId, string RunId, DateTimeOffset StartedAt, DateTimeOffset CompletedAt)> _runs = [];

    /// <summary>
    /// 已记录的运行快照。
    /// </summary>
    public IReadOnlyList<(string NodeId, string RunId, DateTimeOffset StartedAt, DateTimeOffset CompletedAt)> Runs
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
    /// 记录一次已结束的运行。
    /// </summary>
    public void Record(string nodeId, string runId, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        lock (_lock)
        {
            _runs.Add((nodeId, runId, startedAt, completedAt));
        }
    }
}
