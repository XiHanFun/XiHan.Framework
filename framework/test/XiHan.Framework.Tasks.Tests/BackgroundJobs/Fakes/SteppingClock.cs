// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 每读一次就自动前进固定步长的时钟
/// </summary>
/// <remarks>
/// 用来在毫秒级的用例里制造"一轮跑了好几分钟"的效果：Worker 判断是否该给分布式锁续期，
/// 靠的正是 <see cref="IClock.Now"/> 的推进量。用真实时钟就得让用例真等上几分钟，
/// 用固定时钟又永远推不到续期阈值，只有"读一次走一步"能在不等待的前提下精确造出长轮次。
/// <para>
/// 步长在构造时给定，读取次序与推进量都是确定的，因此断言不依赖机器快慢。
/// </para>
/// </remarks>
public sealed class SteppingClock : IClock
{
    private readonly object _gate = new();
    private readonly TimeSpan _step;
    private DateTime _current;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="start">起始时间</param>
    /// <param name="step">每次读取后前进的步长</param>
    public SteppingClock(DateTime start, TimeSpan step)
    {
        _current = start;
        _step = step;
    }

    /// <summary>
    /// 当前时间（每读一次自动前进一个步长）
    /// </summary>
    public DateTime Now
    {
        get
        {
            lock (_gate)
            {
                var current = _current;
                _current = _current.Add(_step);
                return current;
            }
        }
    }

    /// <summary>
    /// 时间类型
    /// </summary>
    public DateTimeKind Kind => DateTimeKind.Utc;

    /// <summary>
    /// 是否支持多时区
    /// </summary>
    public bool SupportsMultipleTimezone => false;

    /// <summary>
    /// 规范化时间
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>规范化时间</returns>
    public DateTime Normalize(DateTime dateTime)
    {
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }

    /// <summary>
    /// 转换为用户时间
    /// </summary>
    /// <param name="utcDateTime">UTC 时间</param>
    /// <returns>用户时间</returns>
    public DateTime ConvertToUserTime(DateTime utcDateTime)
    {
        return utcDateTime;
    }

    /// <summary>
    /// 转换为用户时间
    /// </summary>
    /// <param name="dateTimeOffset">时间偏移</param>
    /// <returns>用户时间</returns>
    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset)
    {
        return dateTimeOffset;
    }

    /// <summary>
    /// 转换为 UTC 时间
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>UTC 时间</returns>
    public DateTime ConvertToUtc(DateTime dateTime)
    {
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }
}
