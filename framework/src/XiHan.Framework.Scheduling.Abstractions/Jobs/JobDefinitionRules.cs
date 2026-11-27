// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Calendars;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 作业定义自身的合法性规则
/// </summary>
/// <remarks>
/// 运行时的 <c>ScheduleAsync</c>、代码登记的 <c>AddJob</c> 与调度器启动共用这一份（RFC-0013「调度器 <c>IJobScheduler</c>」，ADR-0321、ADR-0335）：原因是固定的英文，与 <c>SCHEDULING_VALIDATION_001</c> 的 <c>reason</c> 相同。作业类型是否登记、存储能否写入不在这里
/// </remarks>
internal static class JobDefinitionRules
{
    /// <summary>
    /// 说明与数据里有孤立的代理项时的原因
    /// </summary>
    public const string LoneSurrogateReason = "the definition has a lone surrogate in a description or the data";

    /// <summary>
    /// 找定义的第一处不合法
    /// </summary>
    /// <remarks>
    /// 依次核对：说明与数据里的孤立代理项，再按触发器的次序逐个核对名字重复与日历未登记。<paramref name="calendars"/> 为 <see langword="null"/> 时不核对日历：代码登记时日历可以在作业之后登记，登记的那一刻还判定不了
    /// </remarks>
    /// <param name="definition">作业定义</param>
    /// <param name="calendars">已登记的日历；不核对日历时为 <see langword="null"/></param>
    /// <returns>合法时无值，否则是原因</returns>
    public static Maybe<string> FindProblem(JobDefinition definition, JobCalendars? calendars)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // 说明与数据进代码作业的指纹（宽松的 UTF-8，数据先经 JSON 写出器）与存储：孤立的代理项都换成 U+FFFD，
        // 只差在它上面的两份定义指纹相同，运行时的修改在代码改了之后照样保留；名字的文法只有 ASCII，不用另判（ADR-0321）
        if (HasLoneSurrogate(definition))
        {
            return LoneSurrogateReason;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trigger in definition.Triggers)
        {
            if (!names.Add(trigger.Name))
            {
                return $"trigger '{trigger.Name}' is defined more than once";
            }

            if (calendars is not null && trigger.Schedule.Calendar is { } calendar && !calendars.Find(calendar).HasValue)
            {
                return $"calendar '{calendar}' is not registered";
            }
        }

        return Maybe.None<string>();
    }

    /// <summary>
    /// 作业或触发器的说明与数据里有没有孤立的代理项
    /// </summary>
    /// <remarks>
    /// 数据查每个成员名与字符串值，逐层往下（RFC-0013「调度器 <c>IJobScheduler</c>」，ADR-0321）
    /// </remarks>
    private static bool HasLoneSurrogate(JobDefinition definition)
        => UnicodeText.HasLoneSurrogate(definition.Description) || HasLoneSurrogate(definition.Data)
            || definition.Triggers.Any(static trigger => UnicodeText.HasLoneSurrogate(trigger.Description) || HasLoneSurrogate(trigger.Data));

    /// <summary>
    /// JSON 节点的成员名与字符串值里有没有孤立的代理项
    /// </summary>
    private static bool HasLoneSurrogate(JsonNode? node) => node switch
    {
        JsonObject members => members.Any(static member => UnicodeText.HasLoneSurrogate(member.Key) || HasLoneSurrogate(member.Value)),
        JsonArray items => items.Any(HasLoneSurrogate),
        JsonValue value => value.TryGetValue<string>(out var text) && UnicodeText.HasLoneSurrogate(text),
        _ => false,
    };
}
