// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Database.Trail;

/// <summary>
/// 事件与存储行的往返映射。细节与错误细节手写成 JSON 对象（不经反射，保持 AOT 兼容）：值只允许 JSON 可表示的——
/// <see langword="null"/>、字符串、布尔、整数与浮点、时刻与 <see cref="Guid"/>（写成字符串）、<see cref="JsonElement"/> / <see cref="JsonNode"/>、嵌套字典与列表，其余按 <see cref="object.ToString"/>；读回时每个值是 <see cref="JsonElement"/>。
/// 错误按码与消息键还原，读取方可再按调用方区域翻译；没有键的字面错误用写入时渲染的消息原样还原（线上还原，不再翻译）。
/// </summary>
internal static class AuditRecordMapping
{
    /// <summary>
    /// 事件 → 存储行。
    /// </summary>
    public static AuditRecord ToRecord(AuditEvent auditEvent, Guid id, long sequence)
    {
        var error = auditEvent.Outcome.Error;
        return new AuditRecord
        {
            Id = id,
            Sequence = sequence,
            Timestamp = auditEvent.Timestamp,
            Action = auditEvent.Action,
            Succeeded = error is null,
            ErrorCode = error?.Code,
            ErrorMessageKey = error?.MessageKey,
            // 写入时按进程默认区域渲染一次：字面错误只能靠它还原，带键的错误读回后仍按键翻译
            ErrorMessage = error?.Message,
            ErrorDetailsJson = error?.Details is { } errorDetails ? WriteJson(errorDetails) : null,
            TraceId = auditEvent.TraceId,
            Resource = auditEvent.Resource,
            PrincipalId = auditEvent.PrincipalId,
            TenantId = auditEvent.TenantId,
            DetailsJson = auditEvent.Details is { } details ? WriteJson(details) : null,
        };
    }

    /// <summary>
    /// 存储行 → 事件。
    /// </summary>
    public static AuditEvent ToEvent(AuditRecord record)
        => new(record.Timestamp, record.Action, ToOutcome(record), record.TraceId)
        {
            Resource = record.Resource,
            PrincipalId = record.PrincipalId,
            TenantId = record.TenantId,
            Details = record.DetailsJson is { } json ? ReadJson(json) : null,
        };

    /// <summary>
    /// 把细节写成 JSON 对象文本。
    /// </summary>
    public static string WriteJson(IReadOnlyDictionary<string, object?> values)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in values)
            {
                writer.WritePropertyName(name);
                WriteValue(writer, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// 把 JSON 对象文本读成细节；每个值是脱离文档的 <see cref="JsonElement"/>，不是对象时为空字典。
    /// </summary>
    public static IReadOnlyDictionary<string, object?> ReadJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                // 文档随 using 释放，值必须克隆出来才能在之后使用
                values[property.Name] = property.Value.Clone();
            }
        }

        return values;
    }

    /// <summary>
    /// 还原结果：成功，或按错误码与消息键（没有键时按文本）还原的失败。
    /// </summary>
    private static Result ToOutcome(AuditRecord record)
    {
        if (record.Succeeded || record.ErrorCode is null)
        {
            return Result.Success();
        }

        var details = record.ErrorDetailsJson is { } json ? ReadJson(json) : null;
        // 有消息键的错误按键还原，读取方可再按调用方区域翻译；没有键的字面错误只能原样还原写入时的文案
        return Result.Failure(record.ErrorMessageKey is { } key
            ? new Error(record.ErrorCode, key, details)
            : Error.Literal(record.ErrorCode, record.ErrorMessage ?? record.ErrorCode, details));
    }

    /// <summary>
    /// 写一个细节值。
    /// </summary>
    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
            case JsonElement { ValueKind: JsonValueKind.Undefined }:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case JsonNode node:
                node.WriteTo(writer);
                break;
            case DateTimeOffset moment:
                writer.WriteStringValue(moment);
                break;
            case DateTime moment:
                writer.WriteStringValue(moment);
                break;
            case Guid guid:
                writer.WriteStringValue(guid);
                break;
            case IReadOnlyDictionary<string, object?> nested:
                writer.WriteStartObject();
                foreach (var (name, item) in nested)
                {
                    writer.WritePropertyName(name);
                    WriteValue(writer, item);
                }

                writer.WriteEndObject();
                break;
            case IEnumerable items:
                writer.WriteStartArray();
                foreach (var item in items)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                WriteScalar(writer, value);
                break;
        }
    }

    /// <summary>
    /// 写数值；非有限的浮点数 JSON 表示不了，退回字符串；其余类型按不变文化的 <see cref="object.ToString"/>。
    /// </summary>
    private static void WriteScalar(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case short or byte or sbyte or ushort:
                writer.WriteNumberValue(Convert.ToInt32(value, CultureInfo.InvariantCulture));
                break;
            case uint number:
                writer.WriteNumberValue(number);
                break;
            case ulong number:
                writer.WriteNumberValue(number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case double number when double.IsFinite(number):
                writer.WriteNumberValue(number);
                break;
            case float number when float.IsFinite(number):
                writer.WriteNumberValue(number);
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
