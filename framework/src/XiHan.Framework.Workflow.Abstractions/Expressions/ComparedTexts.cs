// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 逐层比较容器时已取出的字符串文本
/// </summary>
/// <remarks>
/// 两个容器 <c>==</c> 深比较、<c>contains</c> 的元素与要找的值都是容器时用（ADR-0336）：读的一方量规模时取出每个字符串的文本记在这里，深比较按同一个值找回这份文本，不再取第二遍。
/// 只记取文本要解码或经转换器写出的值（从 JSON 文本读来的、由别的 CLR 类型构造的）；进程内的字符串取出来就是同一个实例，不必记。按引用找：同一个值在两侧（<c>a == a</c>）也只取一次。一次比较一份，比完即弃，不跨运算留着文本
/// </remarks>
internal sealed class ComparedTexts
{
    /// <summary>
    /// 值 → 已取出的文本，按引用比较
    /// </summary>
    /// <remarks>
    /// 第一次记的时候才建
    /// </remarks>
    private Dictionary<JsonValue, string>? _texts;

    /// <summary>
    /// 量规模时取一个字符串标量的文本
    /// </summary>
    /// <remarks>
    /// 记过的直接交出（同一个值在两侧），没记过的取出、要解码的记下
    /// </remarks>
    /// <param name="value">JSON 类型是字符串的标量</param>
    /// <returns>值的文本</returns>
    public string Take(JsonValue value)
    {
        if (_texts is not null && _texts.TryGetValue(value, out var known))
        {
            return known;
        }

        var text = WorkflowValues.AsString(value);

        // 从 JSON 文本读来的值每取一次都要解码全文、分配新的字符串；由别的 CLR 类型构造的每取一次都要经转换器写一遍 JSON。进程内的字符串取出来是同一个实例
        if (value.TryGetValue<JsonElement>(out _) || !value.TryGetValue<string>(out _))
        {
            (_texts ??= new Dictionary<JsonValue, string>(ReferenceEqualityComparer.Instance))[value] = text;
        }

        return text;
    }

    /// <summary>
    /// 比较时取一个字符串标量的文本
    /// </summary>
    /// <remarks>
    /// 量的时候记过的交出那一份；没记过的照常取：进程内的字符串，以及超过 64 层、量的时候只算一个值的容器里的
    /// </remarks>
    /// <param name="value">JSON 类型是字符串的标量</param>
    /// <returns>值的文本</returns>
    public string Of(JsonValue value)
        => _texts is not null && _texts.TryGetValue(value, out var known) ? known : WorkflowValues.AsString(value);
}
