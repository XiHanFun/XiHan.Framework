// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Values;

/// <summary>
/// 工作流里时间文本的读入与写出
/// </summary>
/// <remarks>
/// 规则来自 RFC-0021「表达式」的「时间」（ADR-0287），不交给 <see cref="DateTimeOffset.TryParse(string?, out DateTimeOffset)"/>：它接受月份名、斜线日期、只有时间（补上机器当天的日期）等写法，同一段文本在不同的日子读出不同的时刻，别的语言也做不到同样宽松。文法是截止时间文本（RFC-0003 规则 8）的超集：可以只有日期，时间可以不写秒，偏移可以不写（当作 UTC），日期与时间之间可以是空格；写出与截止时间相同。活动的时刻属性（<c>Delay.Until</c>、<c>UserTask.DueAt</c>）、书签载荷里的时刻与 <see cref="WorkflowValues.TryConvert{T}"/> 读时间也经这里（ADR-0296）
/// </remarks>
internal static class WorkflowTimeText
{
    /// <summary>
    /// 只有日期的写法的长度：<c>2026-01-02</c>
    /// </summary>
    private const int DateLength = 10;

    /// <summary>
    /// 日期加时与分的长度：<c>2026-01-02T03:04</c>
    /// </summary>
    private const int MinuteLength = 16;

    /// <summary>
    /// 按文法读时间文本
    /// </summary>
    /// <remarks>
    /// <c>^[0-9]{4}-[0-9]{2}-[0-9]{2}([Tt ][0-9]{2}:[0-9]{2}(:[0-9]{2}(\.[0-9]{1,9})?)?([Zz]|[+-][0-9]{2}:[0-9]{2})?)?\z</c>，按整串匹配，首尾的空白不裁剪。没有时间是当天零点，没有秒是零秒，没有偏移是 UTC；取值范围、闰年与小数的截断都与 <see cref="DeadlineText.TryParse"/> 相同。读出的时刻偏移为零
    /// </remarks>
    /// <param name="text">时间文本</param>
    /// <param name="time">读出的时刻（UTC）；读不出时为 <c>default</c></param>
    public static bool TryParse(string text, out DateTimeOffset time)
    {
        time = default;
        if (text.Length == DateLength)
        {
            return DeadlineText.TryParse(text + "T00:00:00Z", out time);
        }

        if (text.Length < MinuteLength || text[DateLength] is not ('T' or 't' or ' '))
        {
            return false;
        }

        // 补成截止时间的完整写法再读：分之后只能是秒、偏移或结尾，小数只能跟在秒后面
        var rest = text.AsSpan(MinuteLength);
        var hasSeconds = rest.Length > 0 && rest[0] == ':';
        if (!hasSeconds && rest.Length > 0 && rest[0] is not ('Z' or 'z' or '+' or '-'))
        {
            return false;
        }

        var hasOffset = rest.IndexOfAny("Zz+-") >= 0;
        var complete = string.Concat(text.AsSpan(0, DateLength), "T", text.AsSpan(DateLength + 1, MinuteLength - DateLength - 1), hasSeconds ? string.Empty : ":00");
        return DeadlineText.TryParse(string.Concat(complete, rest, hasOffset ? string.Empty : "Z"), out time);
    }

    /// <summary>
    /// 按文法读一个值里的时间
    /// </summary>
    /// <remarks>
    /// 只认 JSON 的字符串（含由 <see cref="DateTimeOffset"/> 等构造、写成 JSON 是字符串的值），按 <see cref="TryParse"/> 读；<see langword="null"/>、数字、布尔与容器都不是时间
    /// </remarks>
    /// <param name="node">值</param>
    /// <param name="time">读出的时刻（UTC）；读不出时为 <c>default</c></param>
    public static bool TryRead(JsonNode? node, out DateTimeOffset time)
    {
        time = default;
        return node is JsonValue value && value.GetValueKind() == JsonValueKind.String && TryParse(WorkflowValues.AsString(value), out time);
    }

    /// <summary>
    /// 写成时间文本
    /// </summary>
    /// <remarks>
    /// UTC、七位小数、<c>+00:00</c>（如 <c>2026-01-02T03:04:05.0000000+00:00</c>），与截止时间的写法相同：定宽，按序数比较的先后就是时刻的先后
    /// </remarks>
    public static string Format(DateTimeOffset time) => DeadlineText.Format(time);
}
