// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 每取一次文本就记一次数的字符串值
/// </summary>
/// <remarks>
/// 由带自定义转换器的 CLR 对象构造的值，取它的文本要经转换器写一遍 JSON（<c>WorkflowValues.AsString</c>）：转换器写的次数就是取文本的次数。值的 JSON 类型在第一次查看时算出、之后记着，<see cref="ToValue"/> 先查看一次再清零，计数里只有取文本
/// </remarks>
internal sealed class CountingText
{
    /// <summary>
    /// 经计数转换器写的类型元数据
    /// </summary>
    private static readonly JsonTypeInfo<CountingText> _typeInfo = JsonMetadataServices.CreateValueInfo<CountingText>(new JsonSerializerOptions { TypeInfoResolver = JsonTypeInfoResolver.Combine() }, new Converter());

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="text">写成 JSON 的字符串</param>
    public CountingText(string text)
    {
        Text = text;
    }

    /// <summary>
    /// 写成 JSON 的字符串
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 取过文本的次数
    /// </summary>
    public int Takes { get; set; }

    /// <summary>
    /// 以这个对象构造的 JSON 值，计数清零
    /// </summary>
    public JsonValue ToValue()
    {
        var value = JsonValue.Create(this, _typeInfo) ?? throw new InvalidOperationException("A non-null object always makes a JSON value.");
        // 第一次查看类型时写一遍 JSON 算出类型并记住：不算取文本
        _ = value.GetValueKind();
        Takes = 0;
        return value;
    }

    /// <summary>
    /// 写一次记一次数的转换器
    /// </summary>
    private sealed class Converter : JsonConverter<CountingText>
    {
        /// <inheritdoc />
        public override CountingText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException("The counting text is write-only.");

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, CountingText value, JsonSerializerOptions options)
        {
            value.Takes++;
            writer.WriteStringValue(value.Text);
        }
    }
}
