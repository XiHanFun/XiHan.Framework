// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// <see cref="WorkflowName"/> 以字符串序列化。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowNameJsonConverter : JsonConverter<WorkflowName>
{
    /// <inheritdoc />
    public override WorkflowName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return WorkflowName.IsValid(value) ? new WorkflowName(value) : throw new JsonException($"Workflow name '{value}' is invalid.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, WorkflowName value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }
}
