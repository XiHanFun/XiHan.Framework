// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Time.Testing;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 记录已创建计时器的可控时间提供器
/// </summary>
public sealed class TimerTrackingTimeProvider : FakeTimeProvider
{
    private readonly object _gate = new();
    private readonly List<TimeSpan> _dueTimes = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="start">起始时间</param>
    public TimerTrackingTimeProvider(DateTimeOffset start)
        : base(start)
    {
    }

    /// <summary>
    /// 已创建的计时器数量
    /// </summary>
    public int TimerCount
    {
        get
        {
            lock (_gate)
            {
                return _dueTimes.Count;
            }
        }
    }

    /// <summary>
    /// 已创建计时器的到期间隔（按创建顺序）
    /// </summary>
    public IReadOnlyList<TimeSpan> DueTimes
    {
        get
        {
            lock (_gate)
            {
                return [.. _dueTimes];
            }
        }
    }

    /// <summary>
    /// 创建计时器并记录其到期间隔
    /// </summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        lock (_gate)
        {
            _dueTimes.Add(dueTime);
        }

        return timer;
    }
}
