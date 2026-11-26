// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 把 <see cref="RepeatedMember"/> 写成有重复成员的对象的转换器
/// </summary>
/// <remarks>
/// 写出器不核对成员名是否重复
/// </remarks>
internal sealed class RepeatedMemberConverter : JsonConverter<RepeatedMember>
{
    /// <inheritdoc />
    public override RepeatedMember Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("The fixture is only written.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, RepeatedMember value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(value.Name, 1);
        writer.WriteNumber(value.Name, 2);
        writer.WriteEndObject();
    }
}
