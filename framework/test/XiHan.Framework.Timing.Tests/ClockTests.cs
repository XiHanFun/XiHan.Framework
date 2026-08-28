// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Timing.Tests.Fakes;

namespace XiHan.Framework.Timing.Tests;

/// <summary>
/// 时钟测试
/// </summary>
/// <remarks>
/// 时钟的全部行为都由 <see cref="XiHanClockOptions.Kind"/> 驱动，因此按
/// Utc / Local / Unspecified 三种配置铺开完整行为矩阵：Now 的 Kind、Normalize 的九宫格、
/// SupportsMultipleTimezone 的取值，以及三个转换方法的短路分支。
/// 时区一律用 <see cref="RecordingTimezoneProvider"/> 提供的 UTC+08:00 自定义时区，
/// 断言既不依赖运行机器的本地时区，也不依赖操作系统时区库。
/// </remarks>
public class ClockTests
{
    /// <summary>
    /// Kind 直接透传时钟选项，不做任何加工
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Kind_ForEveryConfiguredKind_MirrorsOptions(DateTimeKind configuredKind)
    {
        var clock = CreateClock(configuredKind);

        Assert.Equal(configuredKind, clock.Kind);
    }

    /// <summary>
    /// 只有 UTC 配置才宣称支持多时区
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Utc, true)]
    [InlineData(DateTimeKind.Local, false)]
    [InlineData(DateTimeKind.Unspecified, false)]
    public void SupportsMultipleTimezone_ForEveryConfiguredKind_OnlyTrueForUtc(DateTimeKind configuredKind, bool expected)
    {
        var clock = CreateClock(configuredKind);

        Assert.Equal(expected, clock.SupportsMultipleTimezone);
    }

    /// <summary>
    /// UTC 配置下取到的是带 Utc 标记的当前 UTC 时刻
    /// </summary>
    [Fact]
    public void Now_WhenKindIsUtc_ReturnsCurrentUtcInstant()
    {
        var clock = CreateClock(DateTimeKind.Utc);

        var before = DateTime.UtcNow;
        var now = clock.Now;
        var after = DateTime.UtcNow;

        Assert.Equal(DateTimeKind.Utc, now.Kind);
        Assert.InRange(now, before, after);
    }

    /// <summary>
    /// 本地配置下取到的是带 Local 标记的当前本地时刻
    /// </summary>
    [Fact]
    public void Now_WhenKindIsLocal_ReturnsCurrentLocalInstant()
    {
        var clock = CreateClock(DateTimeKind.Local);

        var before = DateTime.Now;
        var now = clock.Now;
        var after = DateTime.Now;

        Assert.Equal(DateTimeKind.Local, now.Kind);
        Assert.InRange(now, before, after);
    }

    /// <summary>
    /// 未指定配置下走的是本地时钟而非 UTC 时钟
    /// </summary>
    /// <remarks>
    /// 这里只锁死取值来源（本地墙上时间），不锁死返回值的 Kind：
    /// 实现返回的是 <c>DateTime.Now</c>，其 Kind 为 Local，与 <see cref="IClock.Kind"/> 宣称的
    /// Unspecified 并不一致，该不一致已在交付报告中作为疑似缺陷提出。
    /// </remarks>
    [Fact]
    public void Now_WhenKindIsUnspecified_ReadsLocalWallClock()
    {
        var clock = CreateClock(DateTimeKind.Unspecified);

        var before = DateTime.Now;
        var now = clock.Now;
        var after = DateTime.Now;

        Assert.InRange(now, before, after);
    }

    /// <summary>
    /// 无需换算的组合只调整 Kind，绝不改动时间刻度
    /// </summary>
    /// <remarks>
    /// 覆盖三类分支：时钟为 Unspecified 时原样返回；两者 Kind 相同时原样返回；
    /// 输入为 Unspecified 时落到 SpecifyKind，把墙上时间直接贴上时钟的 Kind。
    /// </remarks>
    [Theory]
    [InlineData(DateTimeKind.Unspecified, DateTimeKind.Utc, DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified, DateTimeKind.Local, DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified, DateTimeKind.Unspecified, DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc, DateTimeKind.Utc, DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local, DateTimeKind.Local, DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc, DateTimeKind.Unspecified, DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local, DateTimeKind.Unspecified, DateTimeKind.Local)]
    public void Normalize_WhenNoShiftNeeded_KeepsTicksAndAppliesClockKind(
        DateTimeKind clockKind,
        DateTimeKind inputKind,
        DateTimeKind expectedKind)
    {
        var clock = CreateClock(clockKind);
        var input = new DateTime(2024, 3, 15, 10, 30, 45, inputKind);

        var result = clock.Normalize(input);

        Assert.Equal(input.Ticks, result.Ticks);
        Assert.Equal(expectedKind, result.Kind);
    }

    /// <summary>
    /// UTC 时钟遇到本地时间时执行真实换算
    /// </summary>
    [Fact]
    public void Normalize_WhenUtcClockAndLocalInput_ConvertsToUniversalTime()
    {
        var clock = CreateClock(DateTimeKind.Utc);
        var input = new DateTime(2024, 3, 15, 10, 30, 45, DateTimeKind.Local);

        var result = clock.Normalize(input);

        Assert.Equal(input.ToUniversalTime(), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    /// <summary>
    /// 本地时钟遇到 UTC 时间时执行真实换算
    /// </summary>
    [Fact]
    public void Normalize_WhenLocalClockAndUtcInput_ConvertsToLocalTime()
    {
        var clock = CreateClock(DateTimeKind.Local);
        var input = new DateTime(2024, 3, 15, 10, 30, 45, DateTimeKind.Utc);

        var result = clock.Normalize(input);

        Assert.Equal(input.ToLocalTime(), result);
        Assert.Equal(DateTimeKind.Local, result.Kind);
    }

    /// <summary>
    /// UTC 时钟且已设置用户时区时，把 UTC 时间搬到用户时区的墙上时间
    /// </summary>
    [Fact]
    public void ConvertToUserTime_WhenUtcClockAndTimezoneSet_ShiftsIntoUserTimezone()
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            DateTimeKind.Utc,
            new FakeCurrentTimezoneProvider { TimeZone = RecordingTimezoneProvider.PlusEightTimeZoneId },
            timezoneProvider);
        var utcDateTime = new DateTime(2024, 3, 15, 2, 0, 0, DateTimeKind.Utc);

        var result = clock.ConvertToUserTime(utcDateTime);

        Assert.Equal(new DateTime(2024, 3, 15, 10, 0, 0), result);
        Assert.Equal(1, timezoneProvider.GetTimeZoneInfoCallCount);
        Assert.Equal(RecordingTimezoneProvider.PlusEightTimeZoneId, timezoneProvider.LastRequestedTimeZoneId);
    }

    /// <summary>
    /// 时钟不支持多时区时原样返回，且不去解析时区
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ConvertToUserTime_WhenClockDoesNotSupportMultipleTimezone_ReturnsInputUntouched(DateTimeKind clockKind)
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            clockKind,
            new FakeCurrentTimezoneProvider { TimeZone = RecordingTimezoneProvider.PlusEightTimeZoneId },
            timezoneProvider);
        var utcDateTime = new DateTime(2024, 3, 15, 2, 0, 0, DateTimeKind.Utc);

        var result = clock.ConvertToUserTime(utcDateTime);

        Assert.Equal(utcDateTime, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(0, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 入参不是 UTC 时间时原样返回，避免二次换算
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ConvertToUserTime_WhenInputIsNotUtcKind_ReturnsInputUntouched(DateTimeKind inputKind)
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            DateTimeKind.Utc,
            new FakeCurrentTimezoneProvider { TimeZone = RecordingTimezoneProvider.PlusEightTimeZoneId },
            timezoneProvider);
        var input = new DateTime(2024, 3, 15, 2, 0, 0, inputKind);

        var result = clock.ConvertToUserTime(input);

        Assert.Equal(input, result);
        Assert.Equal(inputKind, result.Kind);
        Assert.Equal(0, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 用户时区为空或全空白时原样返回
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertToUserTime_WhenTimezoneIsNullOrWhiteSpace_ReturnsInputUntouched(string? timeZone)
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            DateTimeKind.Utc,
            new FakeCurrentTimezoneProvider { TimeZone = timeZone },
            timezoneProvider);
        var utcDateTime = new DateTime(2024, 3, 15, 2, 0, 0, DateTimeKind.Utc);

        var result = clock.ConvertToUserTime(utcDateTime);

        Assert.Equal(utcDateTime, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(0, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 时间偏移换算保持同一时刻，只把偏移量换成用户时区的偏移量
    /// </summary>
    [Fact]
    public void ConvertToUserTime_WithOffset_WhenTimezoneSet_KeepsInstantAndAppliesTimezoneOffset()
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            DateTimeKind.Utc,
            new FakeCurrentTimezoneProvider { TimeZone = RecordingTimezoneProvider.PlusEightTimeZoneId },
            timezoneProvider);
        var input = new DateTimeOffset(2024, 3, 15, 2, 0, 0, TimeSpan.Zero);

        var result = clock.ConvertToUserTime(input);

        Assert.Equal(TimeSpan.FromHours(8), result.Offset);
        Assert.Equal(input.UtcDateTime, result.UtcDateTime);
        Assert.Equal(new DateTime(2024, 3, 15, 10, 0, 0), result.DateTime);
        Assert.Equal(1, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 时间偏移换算同样受多时区开关约束
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ConvertToUserTime_WithOffset_WhenClockDoesNotSupportMultipleTimezone_ReturnsInputUntouched(DateTimeKind clockKind)
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            clockKind,
            new FakeCurrentTimezoneProvider { TimeZone = RecordingTimezoneProvider.PlusEightTimeZoneId },
            timezoneProvider);
        var input = new DateTimeOffset(2024, 3, 15, 2, 0, 0, TimeSpan.FromHours(3));

        var result = clock.ConvertToUserTime(input);

        Assert.Equal(input.Offset, result.Offset);
        Assert.Equal(input, result);
        Assert.Equal(0, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 时间偏移换算在用户时区为空或全空白时原样返回
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertToUserTime_WithOffset_WhenTimezoneIsNullOrWhiteSpace_ReturnsInputUntouched(string? timeZone)
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            DateTimeKind.Utc,
            new FakeCurrentTimezoneProvider { TimeZone = timeZone },
            timezoneProvider);
        var input = new DateTimeOffset(2024, 3, 15, 2, 0, 0, TimeSpan.FromHours(3));

        var result = clock.ConvertToUserTime(input);

        Assert.Equal(input.Offset, result.Offset);
        Assert.Equal(input, result);
        Assert.Equal(0, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 未标记 Kind 的时间被当作用户时区的墙上时间换算回 UTC
    /// </summary>
    [Fact]
    public void ConvertToUtc_WhenInputKindIsUnspecified_InterpretsInputInUserTimezone()
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            DateTimeKind.Utc,
            new FakeCurrentTimezoneProvider { TimeZone = RecordingTimezoneProvider.PlusEightTimeZoneId },
            timezoneProvider);
        var input = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Unspecified);

        var result = clock.ConvertToUtc(input);

        Assert.Equal(new DateTime(2024, 3, 15, 2, 0, 0, DateTimeKind.Utc), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(1, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 标记为 Local 的时间会被强行改判为用户时区的墙上时间
    /// </summary>
    /// <remarks>
    /// 实现在换算前先把 Kind 抹成 Unspecified，所以结果与运行机器的本地时区完全无关，
    /// 这一点正是本用例要锁死的：换同一台机器的不同本地时区，断言值不应变化。
    /// </remarks>
    [Fact]
    public void ConvertToUtc_WhenInputKindIsLocal_IgnoresMachineLocalTimezone()
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            DateTimeKind.Utc,
            new FakeCurrentTimezoneProvider { TimeZone = RecordingTimezoneProvider.PlusEightTimeZoneId },
            timezoneProvider);
        var input = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Local);

        var result = clock.ConvertToUtc(input);

        Assert.Equal(new DateTime(2024, 3, 15, 2, 0, 0, DateTimeKind.Utc), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    /// <summary>
    /// 已经是 UTC 的时间原样返回，不做二次换算
    /// </summary>
    [Fact]
    public void ConvertToUtc_WhenInputIsAlreadyUtc_ReturnsInputUntouched()
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            DateTimeKind.Utc,
            new FakeCurrentTimezoneProvider { TimeZone = RecordingTimezoneProvider.PlusEightTimeZoneId },
            timezoneProvider);
        var input = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc);

        var result = clock.ConvertToUtc(input);

        Assert.Equal(input, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(0, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 时钟不支持多时区时不做任何换算
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ConvertToUtc_WhenClockDoesNotSupportMultipleTimezone_ReturnsInputUntouched(DateTimeKind clockKind)
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            clockKind,
            new FakeCurrentTimezoneProvider { TimeZone = RecordingTimezoneProvider.PlusEightTimeZoneId },
            timezoneProvider);
        var input = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Unspecified);

        var result = clock.ConvertToUtc(input);

        Assert.Equal(input, result);
        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
        Assert.Equal(0, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 用户时区为空或全空白时不做任何换算
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertToUtc_WhenTimezoneIsNullOrWhiteSpace_ReturnsInputUntouched(string? timeZone)
    {
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(
            DateTimeKind.Utc,
            new FakeCurrentTimezoneProvider { TimeZone = timeZone },
            timezoneProvider);
        var input = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Unspecified);

        var result = clock.ConvertToUtc(input);

        Assert.Equal(input, result);
        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
        Assert.Equal(0, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 用户时区变更后立即生效，时钟每次换算都重新读取当前时区
    /// </summary>
    [Fact]
    public void ConvertToUtc_WhenTimezoneChangesBetweenCalls_ReReadsCurrentTimezone()
    {
        var currentTimezoneProvider = new FakeCurrentTimezoneProvider();
        var timezoneProvider = new RecordingTimezoneProvider();
        var clock = CreateClock(DateTimeKind.Utc, currentTimezoneProvider, timezoneProvider);
        var input = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Unspecified);

        var withoutTimezone = clock.ConvertToUtc(input);
        currentTimezoneProvider.TimeZone = RecordingTimezoneProvider.PlusEightTimeZoneId;
        var withTimezone = clock.ConvertToUtc(input);

        Assert.Equal(input, withoutTimezone);
        Assert.Equal(new DateTime(2024, 3, 15, 2, 0, 0, DateTimeKind.Utc), withTimezone);
        Assert.Equal(1, timezoneProvider.GetTimeZoneInfoCallCount);
    }

    /// <summary>
    /// 构造指定 Kind 的时钟，时区相关依赖使用默认替身
    /// </summary>
    /// <param name="kind">时间类型</param>
    /// <returns>时钟</returns>
    private static Clock CreateClock(DateTimeKind kind)
    {
        return CreateClock(kind, new FakeCurrentTimezoneProvider(), new RecordingTimezoneProvider());
    }

    /// <summary>
    /// 构造指定 Kind 与时区依赖的时钟
    /// </summary>
    /// <param name="kind">时间类型</param>
    /// <param name="currentTimezoneProvider">当前时区提供器</param>
    /// <param name="timezoneProvider">时区提供器</param>
    /// <returns>时钟</returns>
    private static Clock CreateClock(
        DateTimeKind kind,
        ICurrentTimezoneProvider currentTimezoneProvider,
        ITimezoneProvider timezoneProvider)
    {
        // 全限定：测试命名空间嵌套在 XiHan.Framework.Timing 之下，裸写 Options 有被解析到同级命名空间的风险
        var options = Microsoft.Extensions.Options.Options.Create(new XiHanClockOptions { Kind = kind });

        return new Clock(options, currentTimezoneProvider, timezoneProvider);
    }
}
