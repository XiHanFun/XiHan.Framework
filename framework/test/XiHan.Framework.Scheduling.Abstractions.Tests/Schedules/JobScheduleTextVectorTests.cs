// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.Abstractions.Tests.Schedules;

/// <summary>
/// 回放 <c>schedule-texts.json</c>：计划的文本形式
/// </summary>
/// <remarks>
/// 规范 RFC-0013「计划的文本形式」：各部分之间正好一个空格，间隔、次数、cron 与选项的读法与写法相同，选项有固定次序、每个至多一次，时刻按 RFC-0003 规则 8 读、写回时换算成 UTC（ADR-0297）。向量的时区只用不依赖时区库的 UTC
/// </remarks>
public sealed class JobScheduleTextVectorTests
{
    /// <summary>
    /// 向量里时刻的写法：UTC、七位小数
    /// </summary>
    private const string InstantFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ScheduleTextVectors _vectors = SpecVectors.Read<ScheduleTextVectors>("scheduling", "schedule-texts.json");

    [Fact]
    public void TryParse_ReadsTheValidTexts_AndToStringWritesTheCanonicalText()
    {
        Assert.NotEmpty(_vectors.Valid);
        var failures = new List<string>();
        foreach (var vector in _vectors.Valid)
        {
            if (!JobSchedule.TryParse(vector.Text, out var schedule))
            {
                failures.Add($"{vector.Name}: '{vector.Text}' was rejected");
                continue;
            }

            var actual = (Kind(schedule.Kind), schedule.Interval?.Ticks, schedule.RepeatCount, schedule.Cron?.Expression, schedule.TimeZoneId, Instant(schedule.At), Instant(schedule.StartAt), Instant(schedule.EndAt), schedule.Calendar);
            var expected = (vector.Kind, vector.IntervalTicks, vector.RepeatCount, vector.Cron, vector.TimeZone, Instant(vector.At), Instant(vector.StartAt), Instant(vector.EndAt), vector.Calendar);
            if (actual != expected)
            {
                failures.Add($"{vector.Name}: parsed into {actual}, the vector has {expected}");
            }

            if (schedule.ToString() != vector.Canonical)
            {
                failures.Add($"{vector.Name}: written back as '{schedule}', the vector has '{vector.Canonical}'");
            }

            // 写回的文本本身合法，读回再写出不变
            if (!JobSchedule.TryParse(vector.Canonical, out var again) || again.ToString() != vector.Canonical)
            {
                failures.Add($"{vector.Name}: the text written back '{vector.Canonical}' does not read back to itself");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TryParse_RejectsTheInvalidTexts()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var vector in _vectors.Invalid)
        {
            Assert.False(JobSchedule.TryParse(vector.Text, out var parsed), vector.Name);
            Assert.Null(parsed);
            Assert.Throws<ArgumentException>(() => JobSchedule.Parse(vector.Text));
        }
    }

    [Fact]
    public void TheVectors_CoverEveryKindTheOptionsTheLimitsAndTheRewrittenInstants()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过
        // 四种种类、带全部选项的、cron 带时区的
        Assert.Equal(["cron", "every", "manual", "once"], _vectors.Valid.Select(static vector => vector.Kind).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(_vectors.Valid, static vector => vector is { StartAt: not null, EndAt: not null, Calendar: not null });
        Assert.Contains(_vectors.Valid, static vector => vector is { Kind: "cron", TimeZone: not null, StartAt: not null });

        // 间隔与次数的上限、最短的间隔
        Assert.Contains(_vectors.Valid, static vector => vector.IntervalTicks == TimeSpan.MaxValue.Ticks && vector.RepeatCount == int.MaxValue);
        Assert.Contains(_vectors.Valid, static vector => vector.IntervalTicks == 1);
        Assert.Contains(_vectors.Invalid, static vector => vector.Text == "every 10675199.02:48:05.4775808");
        Assert.Contains(_vectors.Invalid, static vector => vector.Text.EndsWith(" x2147483648", StringComparison.Ordinal));

        // 写回时时刻的写法会变的（换算成 UTC、补足或截到七位小数），其余照原样
        Assert.Contains(_vectors.Valid, static vector => vector.Text != vector.Canonical);
        Assert.All(_vectors.Valid.Where(static vector => vector.Text != vector.Canonical), static vector => Assert.True(vector.At is not null || vector.StartAt is not null, vector.Name));

        // 必须拒绝的：别的空白、连着的空格、次序不对与重复的选项、跟错种类的时区
        Assert.Contains(_vectors.Invalid, static vector => vector.Text.Contains('\t'));
        Assert.Contains(_vectors.Invalid, static vector => vector.Text.Contains('\u00a0'));
        Assert.Contains(_vectors.Invalid, static vector => vector.Text.Contains("  ", StringComparison.Ordinal));
        Assert.Contains(_vectors.Invalid, static vector => vector.Text.StartsWith("every ", StringComparison.Ordinal) && vector.Text.Contains(" tz=", StringComparison.Ordinal));
        Assert.Contains(_vectors.Invalid, static vector => vector.Text.Contains("calendar=a calendar=b", StringComparison.Ordinal));
    }

    /// <summary>
    /// 种类在文本里的写法
    /// </summary>
    private static string Kind(JobScheduleKind kind) => kind switch
    {
        JobScheduleKind.Manual => "manual",
        JobScheduleKind.Interval => "every",
        JobScheduleKind.Cron => "cron",
        JobScheduleKind.Once => "once",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The schedule kind is unknown."),
    };

    /// <summary>
    /// 时刻换算成 UTC、按向量的写法写出
    /// </summary>
    private static string? Instant(DateTimeOffset? value)
        => value?.UtcDateTime.ToString(InstantFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// 向量里的时刻按同一写法规范化
    /// </summary>
    private static string? Instant(string? text)
        => text is null ? null : Instant(DateTimeOffset.ParseExact(text, InstantFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Valid">合法的文本</param>
    /// <param name="Invalid">必须拒绝的文本</param>
    private sealed record ScheduleTextVectors(ScheduleTextCase[] Valid, InvalidScheduleText[] Invalid);

    /// <summary>
    /// 一段合法的文本、读出的各部分与写回的文本
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Text">文本</param>
    /// <param name="Kind">种类：manual、every、cron 或 once</param>
    /// <param name="IntervalTicks">间隔（100 纳秒为单位），只对 every</param>
    /// <param name="RepeatCount">总运行次数，只对 every</param>
    /// <param name="Cron">cron 的表达式</param>
    /// <param name="TimeZone">时区名；<see langword="null"/> 表示 UTC</param>
    /// <param name="At">一次的时刻（UTC）</param>
    /// <param name="StartAt">起始时刻（UTC）</param>
    /// <param name="EndAt">结束时刻（UTC）</param>
    /// <param name="Calendar">排除日历的名字</param>
    /// <param name="Canonical">写回的文本</param>
    private sealed record ScheduleTextCase(string Name, string Text, string Kind, long? IntervalTicks, int? RepeatCount, string? Cron, string? TimeZone, string? At, string? StartAt, string? EndAt, string? Calendar, string Canonical);

    /// <summary>
    /// 一段必须拒绝的文本
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Text">文本</param>
    private sealed record InvalidScheduleText(string Name, string Text);
}
