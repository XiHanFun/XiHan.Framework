// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;

namespace XiHan.Framework.Primitives.Tests.Messages;

/// <summary>
/// 回放 <c>message-templates.json</c>：消息键文法、宽松与严格格式化、<see cref="MessageTemplate.Describe(string, IReadOnlyDictionary{string, object?}?)"/>
/// </summary>
/// <remarks>
/// 各语言 SDK 回放同一份向量（RFC-0010「模板」，ADR-0233）；参考实现在不变文化下必须给出同样的结果
/// </remarks>
public sealed class MessageTemplateVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly TemplateVectors _vectors = ConformanceVectors.Load<TemplateVectors>("message-templates.json");

    [Fact]
    public void IsValidKey_MatchesConformanceVectors()
    {
        Assert.NotEmpty(_vectors.Keys.Valid);
        Assert.All(_vectors.Keys.Valid, key => Assert.True(MessageTemplate.IsValidKey(key), key));
        Assert.All(_vectors.Keys.Invalid, key => Assert.False(MessageTemplate.IsValidKey(key), key));
    }

    [Fact]
    public void FormatAndTryFormat_MatchConformanceVectors()
    {
        var failures = new List<string>();
        foreach (var vector in _vectors.Format)
        {
            var arguments = ToArguments(vector.Arguments);
            string lenient;
            try
            {
                lenient = MessageTemplate.Format(vector.Template, arguments, CultureInfo.InvariantCulture);
            }
            catch (FormatException exception)
            {
                // 逐项收集，一次列出全部不符的向量
                failures.Add($"{vector.Name}: {exception.Message}");
                continue;
            }

            if (lenient != vector.Lenient)
            {
                failures.Add($"{vector.Name}: lenient '{lenient}', expected '{vector.Lenient}'");
            }

            var strict = MessageTemplate.TryFormat(vector.Template, arguments, CultureInfo.InvariantCulture, out var text) ? text : null;
            if (strict != vector.Strict)
            {
                failures.Add($"{vector.Name}: strict '{strict ?? "<failed>"}', expected '{vector.Strict ?? "<failed>"}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Describe_MatchesConformanceVectors()
    {
        Assert.NotEmpty(_vectors.Describe);
        Assert.All(_vectors.Describe, vector => Assert.Equal(vector.Text, MessageTemplate.Describe(vector.Key, ToArguments(vector.Arguments))));
    }

    /// <summary>
    /// 把向量里的 JSON 参数转成模板参数
    /// </summary>
    /// <remarks>
    /// <c>null</c> 表示没有参数；整数写法的数字为 <see cref="long"/>，其余数字为 <see cref="double"/>，与 SDK 按 JSON 取值的方式一致
    /// </remarks>
    private static Dictionary<string, object?>? ToArguments(JsonElement element)
        => element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : element.EnumerateObject().ToDictionary(property => property.Name, property => ToValue(property.Value));

    /// <summary>
    /// 一个 JSON 标量的 .NET 值
    /// </summary>
    private static object? ToValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            // 两个分支先装箱：否则条件表达式把 long 提升成 double，整数就丢了类型
            JsonValueKind.Number => value.TryGetInt64(out var integer) ? (object)integer : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => throw new InvalidOperationException($"Template vectors only use scalar arguments, got '{value.ValueKind}'."),
        };

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    private sealed record TemplateVectors(KeyVectors Keys, FormatVector[] Format, DescribeVector[] Describe);

    /// <summary>
    /// 合法与非法的消息键
    /// </summary>
    private sealed record KeyVectors(string[] Valid, string[] Invalid);

    /// <summary>
    /// 一次格式化，<see cref="Strict"/> 为 <see langword="null"/> 表示严格格式化失败
    /// </summary>
    private sealed record FormatVector(string Name, string Template, JsonElement Arguments, string Lenient, string? Strict);

    /// <summary>
    /// 一次 Describe，至多一个参数
    /// </summary>
    private sealed record DescribeVector(string Key, JsonElement Arguments, string Text);
}
