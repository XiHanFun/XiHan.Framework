// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.Abstractions.Tests.Schedules;

/// <summary>
/// 回放 <c>cron-expressions.json</c>：cron 表达式的写法与下一次触发
/// </summary>
/// <remarks>
/// 规范 RFC-0013「作业与触发器」的 cron：5、6、7 段与各段的写法，按时区的墙上时间匹配，夏令时开始时被跳过的墙上时间不运行、结束时重复的只运行第一次，下一次的墙上时间与起点相差超过搜索上限（1830 天）即没有下一次，步长不超过这一段能写的最大值（ADR-0277、ADR-0286、ADR-0297）。时区只取向量给的 IANA 名，不看本机时区
/// </remarks>
public sealed class CronExpressionVectorTests
{
    /// <summary>
    /// 向量里时刻的写法：UTC，秒以下可以没有
    /// </summary>
    private const string InstantFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    /// <summary>
    /// 搜索上限：下一次的墙上时间与起点最多相差这么多
    /// </summary>
    private static readonly TimeSpan _searchLimit = TimeSpan.FromDays(1830);

    /// <summary>
    /// 各段的名字与步长的上限（这一段能写的最大值，星期在 0 到 7 里数）
    /// </summary>
    private static readonly (string Field, int Maximum)[] _stepMaximums =
    [
        ("second", 59), ("minute", 59), ("hour", 23), ("day-of-month", 31), ("month", 12), ("day-of-week", 7), ("year", 2099),
    ];

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly CronVectors _vectors = SpecVectors.Read<CronVectors>("scheduling", "cron-expressions.json");

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
        Assert.Contains(_vectors.Cases, static vector => Fields(vector.Expression).Length == 5);
        Assert.Contains(_vectors.Cases, static vector => Fields(vector.Expression).Length == 6);
        Assert.Contains(_vectors.Cases, static vector => Fields(vector.Expression).Length == 7);

        // 写法的细则（ADR-0286）：以制表符分段的、两端带空格的，小写的名字，单个值带步长，步长与年份的上限
        Assert.Contains(_vectors.Cases, static vector => vector.Expression.Contains('\t') && !vector.Expression.Contains(' '));
        Assert.Contains(_vectors.Cases, static vector => vector.Expression.StartsWith(' ') && vector.Expression.EndsWith(' '));
        Assert.Contains(_vectors.Cases, static vector => vector.Expression.Contains("jan", StringComparison.Ordinal));
        Assert.Contains(_vectors.Cases, static vector => Fields(vector.Expression)[0] == "10/15");
        Assert.Contains(_vectors.Cases, static vector => Fields(vector.Expression) is { Length: 7 } fields && fields[6] == "1970");
        Assert.Contains(_vectors.Cases, static vector => Fields(vector.Expression) is { Length: 7 } fields && fields[6] == "2099");
        Assert.Contains(_vectors.Cases, static vector => vector.Expression.Contains("L-30", StringComparison.Ordinal));

        // 必须拒绝的：年份、L-n 各比上限多一的，步长超出 32 位整数的，结尾带换行的，夹着不换行空格的
        Assert.Contains(_vectors.Invalid, static vector => vector.Expression.EndsWith(" 1969", StringComparison.Ordinal));
        Assert.Contains(_vectors.Invalid, static vector => vector.Expression.EndsWith(" 2100", StringComparison.Ordinal));
        Assert.Contains(_vectors.Invalid, static vector => vector.Expression.Contains("L-31", StringComparison.Ordinal));
        Assert.Contains(_vectors.Invalid, static vector => vector.Expression.Contains("/2147483648", StringComparison.Ordinal));

        // 步长的上限两侧（ADR-0297）：每一段都有步长正好是上限的合法用例与比上限多一的必须拒绝的用例
        foreach (var (field, maximum) in _stepMaximums)
        {
            Assert.True(_vectors.Cases.Any(vector => Steps(vector.Expression, field).Contains(maximum)), $"No valid case has a step of {maximum} in field '{field}'.");
            Assert.True(_vectors.Invalid.Any(vector => Steps(vector.Expression, field).Contains(maximum + 1)), $"No invalid case has a step of {maximum + 1} in field '{field}'.");
        }
        Assert.Contains(_vectors.Invalid, static vector => vector.Expression.EndsWith('\n'));
        Assert.Contains(_vectors.Invalid, static vector => vector.Expression.Contains('\u00a0'));

        // 搜索上限的两侧：同一个起点，正好相差 1830 天的有、多 1 秒的没有
        Assert.Contains(_vectors.Cases, static vector => vector.Next.Length == 1 && Utc(vector.Next[0]) - Utc(vector.After) == _searchLimit);
        Assert.Contains(_vectors.Cases, static vector => vector.Next.Length == 0 && vector.Exhausted && Fields(vector.Expression) is { Length: 7 } fields && fields[0] == "1" && fields[6] == "2031");

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
    /// 表达式的各段
    /// </summary>
    /// <remarks>
    /// 按空格与制表符分开，连着的算一个
    /// </remarks>
    private static string[] Fields(string expression)
        => expression.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// 表达式里某一段各项所带的步长
    /// </summary>
    /// <remarks>
    /// 5 段从分开始，6 段与 7 段从秒开始；读不成整数的步长不计
    /// </remarks>
    private static List<int> Steps(string expression, string field)
    {
        var fields = Fields(expression);
        var names = fields.Length == 5 ? _stepMaximums.Skip(1).Take(5) : _stepMaximums.Take(fields.Length);
        var index = names.Select(static entry => entry.Field).ToList().IndexOf(field);
        if (index < 0 || index >= fields.Length)
        {
            return [];
        }

        var steps = new List<int>();
        foreach (var item in fields[index].Split(','))
        {
            var slash = item.IndexOf('/', StringComparison.Ordinal);
            if (slash >= 0 && int.TryParse(item.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var step))
            {
                steps.Add(step);
            }
        }

        return steps;
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
