// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 把 <see cref="Utf8Text"/> 以 UTF-8 字节写出的转换器
/// </summary>
/// <remarks>
/// 写出走 <see cref="Utf8JsonWriter.WriteStringValue(ReadOnlySpan{byte})"/>，字符串不经 UTF-16
/// </remarks>
internal sealed class Utf8TextConverter : JsonConverter<Utf8Text>
{
    /// <inheritdoc />
    public override Utf8Text Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var buffer = new byte[reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length];
        return new Utf8Text(buffer[..reader.CopyString(buffer)]);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Utf8Text value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
