// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 把 <see cref="RawJson"/> 原样写出的转换器
/// </summary>
internal sealed class RawJsonConverter : JsonConverter<RawJson>
{
    /// <inheritdoc />
    public override RawJson Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return new RawJson(document.RootElement.GetRawText());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, RawJson value, JsonSerializerOptions options)
        => writer.WriteRawValue(value.Text);
}
