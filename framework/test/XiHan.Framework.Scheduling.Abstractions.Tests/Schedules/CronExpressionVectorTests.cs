// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.Abstractions.Tests.Schedules;

/// <summary>
/// 回放 <c>cron-expressions.json</c>：cron 表达式的写法与下一次触发
/// </summary>
/// <remarks>
/// 规范 RFC-0013「作业与触发器」的 cron：5、6、7 段与各段的写法，按时区的墙上时间匹配，夏令时开始时被跳过的墙上时间不运行、结束时重复的只运行第一次，搜索上限之内没有匹配即没有下一次（ADR-0277）。时区只取向量给的 IANA 名，不看本机时区
/// </remarks>
public sealed class CronExpressionVectorTests
{
    /// <summary>
    /// 向量里时刻的写法：UTC，秒以下可以没有
    /// </summary>
    private const string InstantFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly CronVectors _vectors = ConformanceVectors.Load<CronVectors>("cron-expressions.json");

    [Fact]
    public void Next_GivesTheOccurrencesOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            if (!CronExpression.TryParse(vector.Expression, out var cron))
            {
                failures.Add($"{vector.Name}: '{vector.Expression}' was rejected");
                continue;
            }

            // 比向量列的多推一次：exhausted 说的是列完之后还有没有下一次
            var zone = vector.TimeZone is null ? null : TimeZoneInfo.FindSystemTimeZoneById(vector.TimeZone);
            var occurrences = new List<DateTimeOffset>();
            var cursor = Utc(vector.After);
            var exhausted = false;
            while (occurrences.Count <= vector.Next.Length)
            {
                if (!(zone is null ? cron.Next(cursor) : cron.Next(cursor, zone)).TryGetValue(out var next))
                {
                    exhausted = true;
                    break;
                }

                if (next <= cursor)
                {
                    failures.Add($"{vector.Name}: '{Describe(next)}' is not strictly later than '{Describe(cursor)}'");
                    break;
                }

                occurrences.Add(next);
                cursor = next;
            }

            var actual = occurrences.Take(vector.Next.Length).Select(Describe).ToList();
            var expected = vector.Next.Select(static text => Describe(Utc(text))).ToList();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal) || exhausted != vector.Exhausted)
            {
                failures.Add($"{vector.Name}: got [{string.Join(", ", actual)}] and exhausted = {exhausted}, the vector has [{string.Join(", ", expected)}] and exhausted = {vector.Exhausted}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TryParse_RejectsTheInvalidExpressions()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var vector in _vectors.Invalid)
        {
            Assert.False(CronExpression.TryParse(vector.Expression, out var parsed), vector.Name);
            Assert.Null(parsed);
            Assert.Throws<ArgumentException>(() => CronExpression.Parse(vector.Expression));
        }
    }

    [Fact]
    public void TheVectors_CoverEveryFormTimeZonesDaylightSavingAndExhaustion()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过
        // 5 段、6 段与带年份的 7 段
        Assert.Contains(_vectors.Cases, static vector => vector.Expression.Split(' ').Length == 5);
        Assert.Contains(_vectors.Cases, static vector => vector.Expression.Split(' ').Length == 6);
        Assert.Contains(_vectors.Cases, static vector => vector.Expression.Split(' ').Length == 7);

        // 带时区的用例里要有跨过夏令时切换的：列出的触发时刻前后偏移不同
        Assert.Contains(_vectors.Cases, static vector => vector.TimeZone is null);
        Assert.Contains(_vectors.Cases, static vector => vector.TimeZone is not null && Offsets(vector).Count == 1);
        Assert.Contains(_vectors.Cases, static vector => vector.TimeZone is not null && Offsets(vector).Count > 1);

        // 列完就没有下一次的、一次也没有的、列完还有的
        Assert.Contains(_vectors.Cases, static vector => vector.Exhausted && vector.Next.Length > 0);
        Assert.Contains(_vectors.Cases, static vector => vector.Exhausted && vector.Next.Length == 0);
        Assert.Contains(_vectors.Cases, static vector => !vector.Exhausted && vector.Next.Length > 1);

        // 起点带小数秒的：从下一个整秒起
        Assert.Contains(_vectors.Cases, static vector => Utc(vector.After).Ticks % TimeSpan.TicksPerSecond != 0);
    }

    /// <summary>
    /// 一个带时区的用例里，起点与各触发时刻在该时区的偏移（去重）
    /// </summary>
    private static HashSet<TimeSpan> Offsets(CronCase vector)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(vector.TimeZone ?? throw new InvalidOperationException($"Case '{vector.Name}' has no time zone."));
        return [.. vector.Next.Prepend(vector.After).Select(text => zone.GetUtcOffset(Utc(text)))];
    }

    /// <summary>
    /// 向量里的 UTC 时刻
    /// </summary>
    private static DateTimeOffset Utc(string text)
        => DateTimeOffset.ParseExact(text, InstantFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    /// <summary>
    /// 失败信息里的时刻：换算到 UTC，秒以下有才写
    /// </summary>
    private static string Describe(DateTimeOffset instant)
        => instant.UtcDateTime.ToString(InstantFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Cases">合法的表达式与它们的触发时刻</param>
    /// <param name="Invalid">必须拒绝的表达式</param>
    private sealed record CronVectors(CronCase[] Cases, InvalidCronCase[] Invalid);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Expression">表达式</param>
    /// <param name="TimeZone">IANA 时区名；<see langword="null"/> 表示 UTC</param>
    /// <param name="After">起点（UTC）</param>
    /// <param name="Next">从起点起依次推算的触发时刻（UTC）</param>
    /// <param name="Exhausted">列完之后是否再没有下一次</param>
    private sealed record CronCase(string Name, string Expression, string? TimeZone, string After, string[] Next, bool Exhausted);

    /// <summary>
    /// 一个必须拒绝的表达式
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Expression">表达式</param>
    private sealed record InvalidCronCase(string Name, string Expression);
}
