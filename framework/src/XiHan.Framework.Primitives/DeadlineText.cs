// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 截止时间文本的解析与写出
/// </summary>
/// <remarks>
/// 截止时间出进程时各载体（HTTP 的 <c>xihan-deadline</c> 头、插件协议的 <c>InvocationMetadata.deadline</c>）共用同一条文法（RFC-0003 规则 8，ADR-0276、ADR-0278）：RFC 3339 <c>date-time</c> 的子集，不认闰秒，小数一到九位。读入按文法整串匹配，不交给 <see cref="DateTimeOffset.TryParse(string?, out DateTimeOffset)"/>（它接受空格分隔、缺偏移、只有日期、RFC 1123 等写法，别的语言做不到同样宽松）；读不出就当作没有给出截止时间，调用方不报错、也不换别的格式再试。写出一律 UTC、七位小数、<c>+00:00</c>。解析在 <see cref="TimestampText"/>，与消息信封的 <c>time</c> 共用（ADR-0283）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class DeadlineText
{
    /// <summary>
    /// 按文法读截止时间
    /// </summary>
    /// <remarks>
    /// <c>^[0-9]{4}-[0-9]{2}-[0-9]{2}[Tt][0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,9})?([Zz]|[+-][0-9]{2}:[0-9]{2})\z</c>，日期按公历、不认闰秒，换算成 UTC 之后（不计小数）在 0001-01-01T00:00:00 到 9999-12-31T23:59:59 之间；比 100 纳秒更细的小数截断、不进位。读出的时刻偏移为零。不合文法、越界、空串与 <see langword="null"/> 都返回 <see langword="false"/>；首尾的空白不裁剪，带着就读不出。与 <see cref="TimestampText.TryParse"/> 是同一个解析
    /// </remarks>
    /// <param name="text">载体里的文本</param>
    /// <param name="deadline">读出的截止时间（UTC）；读不出时为 <c>default</c></param>
    public static bool TryParse([NotNullWhen(true)] string? text, out DateTimeOffset deadline)
        => TimestampText.TryParse(text, out deadline);

    /// <summary>
    /// 写成截止时间的文本
    /// </summary>
    /// <remarks>
    /// 先换算成 UTC，再写成七位小数加 <c>+00:00</c>（.NET 的 <c>O</c> 格式，如 <c>2026-11-18T03:25:00.1234567+00:00</c>）；写出的文本 <see cref="TryParse"/> 读得回同一时刻
    /// </remarks>
    /// <param name="deadline">截止时间，偏移不限</param>
    public static string Format(DateTimeOffset deadline) => deadline.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
