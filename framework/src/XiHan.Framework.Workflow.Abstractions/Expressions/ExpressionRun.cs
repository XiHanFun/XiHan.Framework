// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 一次求值或一次渲染的状态：作用域与累计的工作量
/// </summary>
/// <remarks>
/// 工作量的规则来自 RFC-0021「表达式」的「长度与上限」（ADR-0296、ADR-0303、ADR-0309）：每开始求值一个节点记 <see cref="StepWork"/>；运算读到的字符串记它的长度（<c>isNullOrEmpty</c> 只读到能判定为止），读到的数字记它的写法的长度（从 JSON 文本读来的是原文，别的是写成 JSON 的文本），读到的数组与对象记它里面每个值 <see cref="StepWork"/>（它自己也算一个）加上全部字符串、数字与成员名的长度；累计超过 <see cref="MaxWork"/> 即以 <see cref="WorkflowMessages.ExpressionBudgetExceeded"/> 失败。工作量只取决于表达式、值与求值的次序，与平台和机器的快慢无关，所以能写进向量。读标量的方法交回运算接着用的值或文本，一次运算里同一个字符串只取出一次。一次 <c>Render</c> 的全部占位共用一个实例
/// </remarks>
internal sealed class ExpressionRun
{
    /// <summary>
    /// 一次求值的工作量上限（UTF-16 码元）
    /// </summary>
    /// <remarks>
    /// 拼接上限 4194304 的 4 倍：把四个 1048576 的字符串拼到上限是 9437632，渲染出 4194304 个码元的模板也在以内；读写几百万个码元的运算做几次、遍历几十万个值的容器一两次就撞上
    /// </remarks>
    public const int MaxWork = 16 * 1024 * 1024;

    /// <summary>
    /// 一步的工作量：每个节点、容器里的每个值
    /// </summary>
    /// <remarks>
    /// 访问一个值（取它的类型、读出数字、比较、写成文本）比复制一个码元贵两个数量级：按 64 个码元记，上限之内遍历的值不超过二十六万个
    /// </remarks>
    public const int StepWork = 64;

    /// <summary>
    /// 数组与对象的最大嵌套层数
    /// </summary>
    /// <remarks>
    /// 与值的文本相同（RFC-0001「JSON 文本」）：更深的容器只可能由进程内的代码构造，写成文本是 <c>null</c>，这里也只算一个值，递归因此有界
    /// </remarks>
    private const int MaxDepth = 64;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="scope">求值的作用域</param>
    public ExpressionRun(ExpressionScope scope)
    {
        Scope = scope;
    }

    /// <summary>
    /// 求值的作用域
    /// </summary>
    public ExpressionScope Scope { get; }

    /// <summary>
    /// 到目前为止累计的工作量
    /// </summary>
    public long Work { get; private set; }

    /// <summary>
    /// 开始求值一个节点：记一步
    /// </summary>
    public void Step() => Add(StepWork);

    /// <summary>
    /// 读一个值：字符串与数字记它的长度，容器记它的规模，别的不记
    /// </summary>
    public void Read(JsonNode? node) => Add(Measure(node, MaxWork - Work));

    /// <summary>
    /// 读一个值，只在它是字符串或数字时记它的长度
    /// </summary>
    /// <remarks>
    /// 算术、比较与数字函数把字符串当数字文本读、把数字从它的写法读出值：容器不读，它们在核对时就是错误；布尔与 null 的读取量本来是零。量字符串的长度要取出全文，取出的这份文本经返回值交给运算（<see cref="Materialize"/>），运算不再取第二遍
    /// </remarks>
    /// <param name="node">值</param>
    /// <returns>运算接着用的值：从 JSON 文本读来的字符串换成已取出的文本，别的原样</returns>
    public JsonNode? ReadScalar(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return node;
        }

        if (value.GetValueKind() != JsonValueKind.String)
        {
            Add(MeasureScalar(value));
            return value;
        }

        var text = WorkflowValues.AsString(value);
        Add(text.Length);
        return Materialize(value, text);
    }

    /// <summary>
    /// 读一个值并取出它的文本
    /// </summary>
    /// <remarks>
    /// 读取量与 <see cref="Read"/> 相同，文本与 <see cref="WorkflowValueText.Of"/> 相同。字符串只取出一次，同一份文本既量长度又交给运算：<c>len</c>（ADR-0309）、<c>upper</c> 等文本函数、拼接与渲染以前各取两遍，从 JSON 文本读来的值每取一遍都要解码全文。数字先记它的写法的长度再写成文本，读先于运算
    /// </remarks>
    /// <param name="node">值</param>
    /// <returns>值的文本形式</returns>
    public string ReadText(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return string.Empty;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                var text = WorkflowValues.AsString(value);
                Add(text.Length);
                return text;
            case JsonValue value:
                Add(MeasureScalar(value));
                return WorkflowValues.AsString(value);
            default:
                // 数组与对象先量规模再写成文本：量到超过上限即停，几十万个值的容器不必先写出来
                Read(node);
                return WorkflowValueText.Of(node);
        }
    }

    /// <summary>
    /// 读一个字符串，只读到能判定它是不是空串为止
    /// </summary>
    /// <remarks>
    /// <c>isNullOrEmpty</c> 用（ADR-0309）：看到第一个码元就知道不是空串，非空的记 1、空串记 0，与它多长无关。判定不取出整个字符串：从 JSON 文本读来的看原文是不是 <c>""</c>（空串在 JSON 里只有这一种写法，带转义的至少写出一个码元），进程内构造的直接看长度
    /// </remarks>
    /// <param name="value">JSON 类型是字符串的标量</param>
    /// <returns>是不是空串</returns>
    public bool ReadEmptiness(JsonValue value)
    {
        var empty = IsEmptyString(value);
        if (!empty)
        {
            Add(1);
        }

        return empty;
    }

    /// <summary>
    /// 把一个值读 <paramref name="times"/> 次
    /// </summary>
    /// <remarks>
    /// <c>contains</c> 在数组里找时，每个元素都与要找的值比一次：按元素个数乘以它的读取量记。次数为零不读，结果相同（零乘以任何数都是零）。要找的是字符串时只取出一次，经返回值交给每一次比较（<see cref="Materialize"/>）
    /// </remarks>
    /// <param name="node">值</param>
    /// <param name="times">次数</param>
    /// <returns>比较时用的值：从 JSON 文本读来的字符串换成已取出的文本，别的原样</returns>
    public JsonNode? ReadTimes(JsonNode? node, int times)
    {
        if (times <= 0)
        {
            return node;
        }

        // 先按剩下的额度除以次数量：一次的读取量超过它，乘起来一定超过上限，不必量完整个值
        var share = (MaxWork - Work) / times;
        long amount;
        var compared = node;
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            var text = WorkflowValues.AsString(value);
            amount = text.Length;
            compared = Materialize(value, text);
        }
        else
        {
            amount = Measure(node, share);
        }

        if (amount > share)
        {
            Add(MaxWork - Work + 1);
        }

        Add(amount * times);
        return compared;
    }

    /// <summary>
    /// 一个值的读取量
    /// </summary>
    /// <remarks>
    /// 累计超过 <paramref name="limit"/> 即停下返回：再量下去也只是超过上限，几十万个值的容器不必走完
    /// </remarks>
    /// <param name="node">值</param>
    /// <param name="limit">还剩的额度</param>
    private static long Measure(JsonNode? node, long limit)
    {
        switch (node)
        {
            case null:
                return 0;
            case JsonValue value:
                return MeasureScalar(value);
            default:
                long total = 0;
                MeasureContainer(node, 1, limit, ref total);
                return total;
        }
    }

    /// <summary>
    /// 累加容器里一个值的规模
    /// </summary>
    /// <param name="node">容器里的值（最外层是容器自己）</param>
    /// <param name="depth">本值作为容器时所在的层数，最外层为 1</param>
    /// <param name="limit">还剩的额度</param>
    /// <param name="total">累计的规模</param>
    private static void MeasureContainer(JsonNode? node, int depth, long limit, ref long total)
    {
        // 每个值一步，null、布尔、数字只有这一步
        total += StepWork;
        if (total > limit)
        {
            return;
        }

        switch (node)
        {
            case JsonValue value:
                total += MeasureScalar(value);
                return;
            case JsonArray array when depth <= MaxDepth:
                foreach (var item in array)
                {
                    MeasureContainer(item, depth + 1, limit, ref total);
                    if (total > limit)
                    {
                        return;
                    }
                }

                return;
            case JsonObject obj when depth <= MaxDepth:
                foreach (var (name, member) in obj)
                {
                    total += name.Length;
                    MeasureContainer(member, depth + 1, limit, ref total);
                    if (total > limit)
                    {
                        return;
                    }
                }

                return;
            default:
                return;
        }
    }

    /// <summary>
    /// 一个标量的读取量：字符串是它的长度，数字是它的写法的长度，布尔是 0
    /// </summary>
    private static long MeasureScalar(JsonValue value) => value.GetValueKind() switch
    {
        JsonValueKind.String => WorkflowValues.AsString(value).Length,
        JsonValueKind.Number => NumberLength(value),
        _ => 0,
    };

    /// <summary>
    /// 量过长度的字符串交给运算接着用的值
    /// </summary>
    /// <remarks>
    /// 由 JSON 文本读来的值每取一次文本都要解码全文、分配一个新的字符串：换成已取出的文本，运算再当数字、时间或文本读都不再解码，结果相同（两者当数字读都是按这段文本过数字的文法）。进程内的字符串取出来是同一个实例，不必换；由 <see cref="Guid"/>、<see cref="DateTimeOffset"/> 等构造的值文本很短，换成字符串之后当数字读的规则也不同（<see cref="WorkflowValues.TryDecimal"/> 不把它们当数字文本读），留原样
    /// </remarks>
    /// <param name="value">JSON 类型是字符串的标量</param>
    /// <param name="text">已取出的文本</param>
    private static JsonValue Materialize(JsonValue value, string text)
        => value.TryGetValue<JsonElement>(out _) ? JsonValue.Create(text) : value;

    /// <summary>
    /// 一个 JSON 类型是字符串的标量是不是空串
    /// </summary>
    /// <remarks>
    /// 不取出整个字符串：由 JSON 文本读来的值取出要先解码全文（四百万个码元约两毫秒），而原文带两侧的引号，空串正好是两个字节
    /// </remarks>
    private static bool IsEmptyString(JsonValue value)
    {
        if (value.TryGetValue<JsonElement>(out var element))
        {
            return JsonMarshal.GetRawUtf8Value(element).Length == 2;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return text.Length == 0;
        }

        // 由 Guid、DateTimeOffset 等构造、写成 JSON 是字符串的值：它们的文本都很短
        return WorkflowValues.AsString(value).Length == 0;
    }

    /// <summary>
    /// 一个数字的写法的长度
    /// </summary>
    /// <remarks>
    /// 读一个数字要扫描它的写法（RFC-0021「长度与上限」，ADR-0303）：从 JSON 文本读来的是原文（JSON 的数字只有 ASCII 字符，字节数就是码元数），原文几百万位的数字每读一次都要重新解析；由 CLR 类型构造的数字按它写成 JSON 的文本，与写进存储再读回来的原文相同。表达式得出的数字已经去掉结尾的零（<see cref="WorkflowNumberText.Normalize"/>），写成 JSON 就是数字的文本
    /// </remarks>
    private static long NumberLength(JsonValue value)
    {
        if (value.TryGetValue<JsonElement>(out var element))
        {
            return JsonMarshal.GetRawUtf8Value(element).Length;
        }

        // 常见的几种直接量出长度，不分配；与 System.Text.Json 的写法相同：不变文化、缺省格式
        Span<char> buffer = stackalloc char[64];
        if (value.TryGetValue<decimal>(out var number) && number.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture))
        {
            return written;
        }

        if (value.TryGetValue<int>(out var small) && small.TryFormat(buffer, out written, default, CultureInfo.InvariantCulture))
        {
            return written;
        }

        if (value.TryGetValue<long>(out var large) && large.TryFormat(buffer, out written, default, CultureInfo.InvariantCulture))
        {
            return written;
        }

        return value.ToJsonString().Length;
    }

    /// <summary>
    /// 记一笔工作量，超过上限即失败
    /// </summary>
    /// <remarks>
    /// 每记一笔就比较（RFC-0021「长度与上限」）：步先于节点的操作数记，读先于运算自己的核对，所以撞上上限的那一刻在各实现上相同
    /// </remarks>
    private void Add(long amount)
    {
        Work += amount;
        if (Work > MaxWork)
        {
            throw new ExpressionException(WorkflowMessages.ExpressionBudgetExceeded, MessageArguments.Of(("max", MaxWork)));
        }
    }
}
