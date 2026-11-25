// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Primitives.Tests.Invocation;

/// <summary>
/// 回放 <c>deadlines.json</c> 的文法部分：截止时间文本的解析与写出
/// </summary>
/// <remarks>
/// <c>plugin/deadlines.json</c> 对 <see cref="DeadlineText"/>（RFC-0003 规则 8，ADR-0276、ADR-0278）：合法与不合法的文本、写出的文本由 HTTP 载体与插件协议共用；各载体怎样取到这段文本另有回放（<c>context/http-deadline.json</c> 在宿主的测试里，向量的 <c>effective</c> 在插件宿主的测试里）
/// </remarks>
public sealed class DeadlineTextVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly DeadlineVectors _vectors = SpecVectors.Read<DeadlineVectors>("plugin", "deadlines.json");

    [Fact]
    public void TryParse_ReadsTheValidTexts()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var vector in _vectors.Valid)
        {
            Assert.True(DeadlineText.TryParse(vector.Text, out var parsed), vector.Text);
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
            Assert.False(DeadlineText.TryParse(text, out var parsed), text);
            Assert.Equal(default, parsed);
        }

        Assert.False(DeadlineText.TryParse(null, out _));
        Assert.False(DeadlineText.TryParse(string.Empty, out _));
    }

    [Fact]
    public void TryParse_DoesNotReadTheLenientTexts()
    {
        // 通用的日期解析读得出这些写法，HTTP 入口在改用本文法之前照收（ADR-0276 背景第 2 条）；向量换了写法后不能一条都不剩
        foreach (var text in new[] { "2026-11-18 03:25:00Z", "2026-11-18T03:25:00", "2026-11-18T03:25Z", "2026-11-18", "2026-11-18T03:25:00+0800", " 2026-11-18T03:25:00Z", "2026-11-18T03:25:00Z ", "Wed, 18 Nov 2026 03:25:00 GMT", "11/18/2026 03:25:00" })
        {
            Assert.Contains(text, _vectors.Invalid);
            Assert.False(DeadlineText.TryParse(text, out _), text);
        }
    }

    [Fact]
    public void TryParse_TruncatesFinerFractions_InsteadOfRounding()
    {
        // 四舍五入会把 .9999999995 进到下一秒，最晚的时刻因此读不出
        Assert.True(DeadlineText.TryParse("2026-11-18T03:25:00.123456789Z", out var truncated));
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 3, 25, 0, TimeSpan.Zero).AddTicks(1234567), truncated);

        Assert.True(DeadlineText.TryParse("9999-12-31T23:59:59.999999999Z", out var latest));
        Assert.Equal(DateTimeOffset.MaxValue, latest);
    }

    [Fact]
    public void Format_WritesTheVectorText()
    {
        Assert.NotEmpty(_vectors.Written);
        foreach (var vector in _vectors.Written)
        {
            var text = DeadlineText.Format(Utc(vector.Utc));
            Assert.Equal(vector.Text, text);

            // 写出的文本自己读得回同一时刻
            Assert.True(DeadlineText.TryParse(text, out var parsed), text);
            Assert.Equal(Utc(vector.Utc), parsed);
        }
    }

    [Fact]
    public void Format_ConvertsToUniversalTime()
    {
        var local = new DateTimeOffset(2026, 11, 18, 11, 25, 0, TimeSpan.FromHours(8));

        Assert.Equal("2026-11-18T03:25:00.0000000+00:00", DeadlineText.Format(local));
        Assert.Equal("9999-12-31T23:59:59.9999999+00:00", DeadlineText.Format(DateTimeOffset.MaxValue));
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
    /// <param name="Written">时刻与写出的文本</param>
    private sealed record DeadlineVectors(DeadlineSample[] Valid, string[] Invalid, JsonElement Effective, DeadlineSample[] Written);

    /// <summary>
    /// 一段文本与它对应的时刻
    /// </summary>
    /// <param name="Text">截止时间的文本</param>
    /// <param name="Utc">精确到纳秒的 UTC 时刻</param>
    private sealed record DeadlineSample(string Text, string Utc);
}
