// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.CloudEvents;

/// <summary>
/// CloudEvents 1.0 结构化 JSON 编码（<c>application/cloudevents+json</c>）。JSON 数据内联在 <c>data</c>，其他数据放在 <c>data_base64</c>，扩展属性是顶层成员。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class CloudEventsFormat
{
    /// <summary>
    /// 结构化编码的内容类型。
    /// </summary>
    public const string ContentType = "application/cloudevents+json";

    /// <summary>
    /// 编码；JSON 数据不是合法 JSON 时抛出携带 <see cref="MessagingErrors.InvalidEnvelope"/> 的 <see cref="FrameworkException"/>。
    /// </summary>
    public static byte[] Encode(MessageEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var document = new CloudEventDocument
        {
            SpecVersion = MessageEnvelope.SpecVersion,
            Id = envelope.Id,
            Source = envelope.Source,
            Type = envelope.Type,
            Time = envelope.Time?.ToString("O", CultureInfo.InvariantCulture),
            Subject = envelope.Subject,
            DataContentType = envelope.DataContentType,
        };

        // JSON 数据内联为 data 成员，其他数据按 Base64 放进 data_base64，二者互斥；空数据两者都不写
        if (envelope.Data.Length > 0)
        {
            if (envelope.IsJsonData)
            {
                try
                {
                    using var parsed = JsonDocument.Parse(envelope.Data);
                    document.Data = parsed.RootElement.Clone();
                }
                catch (JsonException exception)
                {
                    throw new FrameworkException(MessagingErrors.InvalidEnvelope, $"Envelope '{envelope.Id}' declares JSON data, but the data is not valid JSON.", exception);
                }
            }
            else
            {
                document.DataBase64 = Convert.ToBase64String(envelope.Data.Span);
            }
        }

        // 扩展属性是顶层成员，与规范属性平级，经 JsonExtensionData 写出
        if (envelope.Extensions.Count > 0)
        {
            document.Extensions = envelope.Extensions.ToDictionary(
                static e => e.Key,
                static e => JsonSerializer.SerializeToElement(e.Value, MessagingJsonContext.Default.String),
                StringComparer.Ordinal);
        }

        return JsonSerializer.SerializeToUtf8Bytes(document, MessagingJsonContext.Default.CloudEventDocument);
    }

    /// <summary>
    /// 解码；文档不合法返回 <see cref="MessagingErrors.InvalidEnvelope"/>。
    /// </summary>
    public static Result<MessageEnvelope> Decode(ReadOnlyMemory<byte> json)
    {
        CloudEventDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json.Span, MessagingJsonContext.Default.CloudEventDocument);
        }
        catch (JsonException exception)
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidJson, MessageArguments.Of(("reason", exception.Message)));
        }

        if (document is null || document.SpecVersion != MessageEnvelope.SpecVersion)
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeSpecVersion, MessageArguments.Of(("specVersion", MessageEnvelope.SpecVersion)));
        }

        if (string.IsNullOrWhiteSpace(document.Id) || string.IsNullOrWhiteSpace(document.Source) || string.IsNullOrWhiteSpace(document.Type))
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeAttributesRequired);
        }

        DateTimeOffset? time = null;
        if (document.Time is not null)
        {
            if (!DateTimeOffset.TryParse(document.Time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedTime))
            {
                return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidTime, MessageArguments.Of(("time", document.Time)));
            }

            time = parsedTime;
        }

        ReadOnlyMemory<byte> data = default;
        if (document.Data is { } element)
        {
            data = Encoding.UTF8.GetBytes(element.GetRawText());
        }
        else if (document.DataBase64 is not null)
        {
            try
            {
                data = Convert.FromBase64String(document.DataBase64);
            }
            catch (FormatException)
            {
                return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidBase64);
            }
        }

        var extensions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in document.Extensions ?? [])
        {
            if (!EnvelopeExtensions.IsValidName(name))
            {
                return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidExtension, MessageArguments.Of(("name", name)));
            }

            // 扩展值只接受字符串、数字与布尔（数字与布尔按原文保留）；其他类型按空字符串处理
            extensions[name] = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
                _ => string.Empty,
            };
        }

        // 信封构造器会再次校验必填项与扩展名，把它的异常转为验证错误而不是抛出
        try
        {
            return Result.Success(new MessageEnvelope
            {
                Id = document.Id,
                Source = document.Source,
                Type = document.Type,
                Time = time,
                Subject = document.Subject,
                DataContentType = document.DataContentType ?? "application/json",
                Data = data,
                Extensions = extensions,
            });
        }
        catch (ArgumentException exception)
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalid, MessageArguments.Of(("reason", exception.Message)));
        }
    }
}
