// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 一次求值或一次渲染的状态：作用域与累计的工作量
/// </summary>
/// <remarks>
/// 工作量的规则来自 RFC-0021「表达式」的「长度与上限」（ADR-0296）：每开始求值一个节点记 <see cref="StepWork"/>；运算读到的字符串记它的长度，读到的数组与对象记它里面每个值 <see cref="StepWork"/>（它自己也算一个）加上全部字符串与成员名的长度；累计超过 <see cref="MaxWork"/> 即以 <see cref="WorkflowMessages.ExpressionBudgetExceeded"/> 失败。工作量只取决于表达式、值与求值的次序，与平台和机器的快慢无关，所以能写进向量。一次 <c>Render</c> 的全部占位共用一个实例
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
    /// 读一个值：字符串记它的长度，容器记它的规模，别的不记
    /// </summary>
    public void Read(JsonNode? node) => Add(Measure(node, MaxWork - Work));

    /// <summary>
    /// 读一个值，只在它是字符串时记它的长度
    /// </summary>
    /// <remarks>
    /// 算术、比较与数字函数只把字符串当数字文本读：容器与别的标量不读，它们在核对时就是错误或不必看内容
    /// </remarks>
    public void ReadIfString(JsonNode? node)
    {
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            Add(WorkflowValues.AsString(value).Length);
        }
    }

    /// <summary>
    /// 把一个值读 <paramref name="times"/> 次
    /// </summary>
    /// <remarks>
    /// <c>contains</c> 在数组里找时，每个元素都与要找的值比一次：按元素个数乘以它的读取量记。次数为零不读，结果相同（零乘以任何数都是零）
    /// </remarks>
    public void ReadTimes(JsonNode? node, int times)
    {
        if (times <= 0)
        {
            return;
        }

        // 先按剩下的额度除以次数量：一次的读取量超过它，乘起来一定超过上限，不必量完整个值
        var share = (MaxWork - Work) / times;
        var amount = Measure(node, share);
        if (amount > share)
        {
            Add(MaxWork - Work + 1);
        }

        Add(amount * times);
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
                return value.GetValueKind() == JsonValueKind.String ? WorkflowValues.AsString(value).Length : 0;
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
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                total += WorkflowValues.AsString(value).Length;
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
