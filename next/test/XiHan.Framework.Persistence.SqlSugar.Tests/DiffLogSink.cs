// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 测试收集的 SqlSugar 差异日志：每条事件连同触发时调用方的 AsyncLocal 值一起记下，用来断言回调在调用方的执行流里同步触发。
/// </summary>
internal sealed class DiffLogSink
{
    /// <summary>
    /// 调用方在写入前设置的环境值。
    /// </summary>
    private static readonly AsyncLocal<string?> _ambient = new();

    /// <summary>
    /// 保护记录的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 记录。
    /// </summary>
    private readonly List<(DiffLogModel Model, string? Ambient)> _events = [];

    /// <summary>
    /// 调用方的环境值；只在设置它的执行流及其后续里可见。
    /// </summary>
    public static string? Ambient
    {
        get => _ambient.Value;
        set => _ambient.Value = value;
    }

    /// <summary>
    /// 已记录的事件快照。
    /// </summary>
    public IReadOnlyList<(DiffLogModel Model, string? Ambient)> Events
    {
        get
        {
            lock (_lock)
            {
                return _events.ToList();
            }
        }
    }

    /// <summary>
    /// 作为 <c>AopEvents.OnDiffLogEvent</c> 的回调：记下事件快照与此刻的环境值。
    /// SqlSugar 在分表插入的各组之间复用同一个模型、每组替换其前后值，所以要在回调里复制，不能留引用。
    /// </summary>
    public void Record(DiffLogModel model)
    {
        var snapshot = new DiffLogModel
        {
            DiffType = model.DiffType,
            BusinessData = model.BusinessData,
            BeforeData = model.BeforeData,
            AfterData = model.AfterData,
            Sql = model.Sql,
            Parameters = model.Parameters,
            Time = model.Time,
        };
        lock (_lock)
        {
            _events.Add((snapshot, Ambient));
        }
    }

    /// <summary>
    /// 清空记录。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }
}
