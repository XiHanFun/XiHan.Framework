// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Primitives.Tests.Invocation;

/// <summary>
/// 时间戳文本的解析：与截止时间的文本是同一条文法、同一份实现
/// </summary>
/// <remarks>
/// <see cref="TimestampText"/> 是 RFC-0003 规则 8 读法的唯一实现，<see cref="DeadlineText.TryParse"/> 转给它，消息信封的 <c>time</c> 也用它（RFC-0009「信封」第 3 条，ADR-0283）。文法的向量与截止时间共用 <c>plugin/deadlines.json</c> 的 <c>valid</c> 与 <c>invalid</c>；这里另核对两个入口逐条相同，以及信封那边关心的两件事：没有偏移的文本读不出（不按本机时区读），超出 <see cref="DateTimeOffset"/> 偏移范围的偏移换算成 UTC
/// </remarks>
public sealed class TimestampTextVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly TimestampVectors _vectors = SpecVectors.Read<TimestampVectors>("plugin", "deadlines.json");

    [Fact]
    public void TryParse_ReadsTheValidTexts_AsUtcInstants()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var vector in _vectors.Valid)
        {
            Assert.True(TimestampText.TryParse(vector.Text, out var parsed), vector.Text);
            Assert.Equal(Utc(vector.Utc), parsed);
            Assert.Equal(TimeSpan.Zero, parsed.Offset);
        }
    }

    [Fact]
    public void TryParse_RejectsTheInvalidTexts()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var text in _vectors.Invalid)
        {
            Assert.False(TimestampText.TryParse(text, out var parsed), text);
            Assert.Equal(default, parsed);
        }

        Assert.False(TimestampText.TryParse(null, out _));
        Assert.False(TimestampText.TryParse(string.Empty, out _));
    }

    [Fact]
    public void DeadlineText_TryParse_IsTheSameParser()
    {
        foreach (var text in _vectors.Valid.Select(static vector => vector.Text).Concat(_vectors.Invalid))
        {
            var read = TimestampText.TryParse(text, out var timestamp);

            Assert.Equal(read, DeadlineText.TryParse(text, out var deadline));
            Assert.Equal(timestamp, deadline);
            Assert.Equal(timestamp.Offset, deadline.Offset);
        }
    }

    [Fact]
    public void TryParse_DoesNotReadTextsWithoutAnOffset_SoTheLocalTimeZoneNeverMatters()
    {
        // 通用的日期解析把这些按本机时区读，同一段文本在不同机器上是不同的时刻
        foreach (var text in new[] { "2026-11-18T03:25:00", "2026-11-18T03:25:00.5", "2026-11-18 03:25:00", "2026-11-18", "11/18/2026 03:25:00" })
        {
            Assert.True(DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _), text);
            Assert.False(TimestampText.TryParse(text, out _), text);
        }
    }

    [Theory]
    [InlineData("2026-01-02T03:04:05+23:59", "2026-01-01T03:05:05Z")]
    [InlineData("2026-01-02T03:04:05-23:59", "2026-01-03T03:03:05Z")]
    [InlineData("2026-01-02T03:04:05+14:01", "2026-01-01T13:03:05Z")]
    public void TryParse_ReadsOffsetsDateTimeOffsetCannotHold_AsTheSameInstantInUtc(string text, string utc)
    {
        // 文法允许的偏移到 ±23:59，DateTimeOffset 只装得下 ±14:00：读出的时刻一律换算成 UTC
        Assert.False(DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _));

        Assert.True(TimestampText.TryParse(text, out var parsed));
        Assert.Equal(DateTimeOffset.ParseExact(utc, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal), parsed);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
    }

    /// <summary>
    /// 向量里精确到纳秒的 UTC 时刻
    /// </summary>
    /// <remarks>
    /// 比 100 纳秒更细的部分截断，与规范对实现时钟精度的要求一致
    /// </remarks>
    private static DateTimeOffset Utc(string text)
    {
        var whole = DateTime.ParseExact(text[..19], "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        var ticks = long.Parse(text.AsSpan(20, 7), NumberStyles.None, CultureInfo.InvariantCulture);
        return new DateTimeOffset(whole.Ticks + ticks, TimeSpan.Zero);
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Valid">合法的文本与读出的时刻</param>
    /// <param name="Invalid">不合法的文本</param>
    /// <param name="Effective">与 gRPC 截止时间取较早者，由插件宿主的 <c>ProtocolVectorTests</c> 回放</param>
    /// <param name="Written">时刻与写出的文本，由 <see cref="DeadlineTextVectorTests"/> 回放</param>
    private sealed record TimestampVectors(TimestampSample[] Valid, string[] Invalid, JsonElement Effective, TimestampSample[] Written);

    /// <summary>
    /// 一段文本与它对应的时刻
    /// </summary>
    /// <param name="Text">时间戳的文本</param>
    /// <param name="Utc">精确到纳秒的 UTC 时刻</param>
    private sealed record TimestampSample(string Text, string Utc);
}
