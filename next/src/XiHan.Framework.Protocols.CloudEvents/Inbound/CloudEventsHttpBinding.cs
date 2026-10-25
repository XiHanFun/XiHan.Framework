// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents.Inbound;

/// <summary>
/// CloudEvents 1.0 的 HTTP 协议绑定：读请求（binary / structured / batch）成信封，把信封写进出站请求（单条 binary / structured，多条 batch）。
/// binary 模式的头值按规范百分号编码非可打印 ASCII 字符；structured 与 batch 复用 <see cref="CloudEventsFormat"/> 的结构化 JSON。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CloudEventsHttpBinding
{
    /// <summary>
    /// binary 模式属性头的前缀。
    /// </summary>
    public const string HeaderPrefix = "ce-";

    /// <summary>
    /// structured 模式的内容类型。
    /// </summary>
    public const string StructuredContentType = CloudEventsFormat.ContentType;

    /// <summary>
    /// batch 模式的内容类型。
    /// </summary>
    public const string BatchContentType = "application/cloudevents-batch+json";

    /// <summary>
    /// 规范版本头。
    /// </summary>
    private const string SpecVersionHeader = HeaderPrefix + "specversion";

    /// <summary>
    /// 不作为扩展属性的核心属性名。
    /// </summary>
    private static readonly string[] _coreAttributes = ["specversion", "id", "source", "type", "time", "subject", "datacontenttype"];

    /// <summary>
    /// 判断请求的内容模式：<c>Content-Type</c> 是结构化或批量格式，否则有 <c>ce-specversion</c> 头即 binary；都不是则无值。
    /// </summary>
    public static Maybe<CloudEventsContentMode> ModeOf(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var mediaType = MediaTypeOf(request.ContentType);
        if (string.Equals(mediaType, StructuredContentType, StringComparison.OrdinalIgnoreCase))
        {
            return CloudEventsContentMode.Structured;
        }

        if (string.Equals(mediaType, BatchContentType, StringComparison.OrdinalIgnoreCase))
        {
            return CloudEventsContentMode.Batch;
        }

        return request.Headers.ContainsKey(SpecVersionHeader) ? CloudEventsContentMode.Binary : default(Maybe<CloudEventsContentMode>);
    }

    /// <summary>
    /// 读请求里的事件；batch 以外总是一个元素。不是 CloudEvent 返回 <see cref="CloudEventsErrors.UnsupportedContentType"/>，信封不合法返回 <see cref="MessagingErrors.InvalidEnvelope"/>。
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<MessageEnvelope>>> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ModeOf(request).TryGetValue(out var mode))
        {
            return Error.Validation(CloudEventsErrors.UnsupportedContentType, CloudEventsMessages.ContentTypeUnsupported, MessageArguments.Of(("contentType", request.ContentType ?? string.Empty)));
        }

        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        var body = memory.ToArray();
        switch (mode)
        {
            case CloudEventsContentMode.Structured:
                var decoded = CloudEventsFormat.Decode(body);
                return decoded.TryGetValue(out var envelope) ? Result.Success<IReadOnlyList<MessageEnvelope>>([envelope]) : decoded.Error!;
            case CloudEventsContentMode.Batch:
                return ReadBatch(body);
            default:
                return ReadBinary(request, body);
        }
    }

    /// <summary>
    /// 把多个信封以 batch 模式写进出站请求：<c>application/cloudevents-batch+json</c>，请求体是结构化 JSON 事件的数组，顺序同入参。
    /// </summary>
    public static void WriteBatch(IReadOnlyList<MessageEnvelope> envelopes, HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        ArgumentNullException.ThrowIfNull(request);
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write("["u8);
        for (var index = 0; index < envelopes.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(envelopes[index]);
            if (index > 0)
            {
                buffer.Write(","u8);
            }

            buffer.Write(CloudEventsFormat.Encode(envelopes[index]));
        }

        buffer.Write("]"u8);
        request.Content = new ByteArrayContent(buffer.WrittenSpan.ToArray());
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(BatchContentType);
    }

    /// <summary>
    /// 把一个信封写进出站请求：binary 模式写 <c>ce-*</c> 头与数据体，structured 模式写结构化 JSON 体；多条用 <see cref="WriteBatch"/>。
    /// </summary>
    public static void Write(MessageEnvelope envelope, HttpRequestMessage request, CloudEventsContentMode mode)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(request);
        switch (mode)
        {
            case CloudEventsContentMode.Structured:
                request.Content = new ByteArrayContent(CloudEventsFormat.Encode(envelope));
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(StructuredContentType);
                break;
            case CloudEventsContentMode.Binary:
                request.Headers.TryAddWithoutValidation(SpecVersionHeader, MessageEnvelope.SpecVersion);
                request.Headers.TryAddWithoutValidation(HeaderPrefix + "id", EncodeHeaderValue(envelope.Id));
                request.Headers.TryAddWithoutValidation(HeaderPrefix + "source", EncodeHeaderValue(envelope.Source));
                request.Headers.TryAddWithoutValidation(HeaderPrefix + "type", EncodeHeaderValue(envelope.Type));
                if (envelope.Time is { } time)
                {
                    request.Headers.TryAddWithoutValidation(HeaderPrefix + "time", time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                }

                if (envelope.Subject is { } subject)
                {
                    request.Headers.TryAddWithoutValidation(HeaderPrefix + "subject", EncodeHeaderValue(subject));
                }

                foreach (var (name, value) in envelope.Extensions)
                {
                    request.Headers.TryAddWithoutValidation(HeaderPrefix + name, EncodeHeaderValue(value));
                }

                request.Content = new ByteArrayContent(envelope.Data.ToArray());
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(envelope.DataContentType);
                break;
            default:
                throw new ArgumentException("Batch mode is only used for inbound events and WriteBatch.", nameof(mode));
        }
    }

    /// <summary>
    /// 按 CloudEvents HTTP 绑定编码头值：可打印 ASCII（<c>%</c> 除外）原样，其余按 UTF-8 字节百分号编码。
    /// </summary>
    public static string EncodeHeaderValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);
        Span<byte> utf8 = stackalloc byte[4];
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is >= 0x20 and <= 0x7E && rune.Value != '%')
            {
                builder.Append((char)rune.Value);
                continue;
            }

            var length = rune.EncodeToUtf8(utf8);
            for (var index = 0; index < length; index++)
            {
                builder.Append('%').Append(utf8[index].ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// 解码头值的百分号编码。
    /// </summary>
    public static string DecodeHeaderValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Uri.UnescapeDataString(value);
    }

    /// <summary>
    /// batch：JSON 数组，每个元素一个结构化事件；第一个不合法的元素让整批失败。
    /// </summary>
    private static Result<IReadOnlyList<MessageEnvelope>> ReadBatch(byte[] body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidJson, MessageArguments.Of(("reason", exception.Message)));
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidJson, MessageArguments.Of(("reason", "a batch must be a JSON array of events")));
            }

            var envelopes = new List<MessageEnvelope>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var decoded = CloudEventsFormat.Decode(Encoding.UTF8.GetBytes(element.GetRawText()));
                if (!decoded.TryGetValue(out var envelope))
                {
                    return decoded.Error!;
                }

                envelopes.Add(envelope);
            }

            return Result.Success<IReadOnlyList<MessageEnvelope>>(envelopes);
        }
    }

    /// <summary>
    /// binary：<c>ce-specversion</c>、<c>ce-id</c>、<c>ce-source</c>、<c>ce-type</c> 必填，<c>ce-time</c>、<c>ce-subject</c> 可选，其余 <c>ce-*</c> 是扩展属性；数据内容类型取 <c>Content-Type</c>，数据取请求体。
    /// </summary>
    private static Result<IReadOnlyList<MessageEnvelope>> ReadBinary(HttpRequest request, byte[] body)
    {
        var headers = request.Headers;
        if (!string.Equals(HeaderValue(headers, "specversion"), MessageEnvelope.SpecVersion, StringComparison.Ordinal))
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeSpecVersion, MessageArguments.Of(("specVersion", MessageEnvelope.SpecVersion)));
        }

        var id = HeaderValue(headers, "id");
        var source = HeaderValue(headers, "source");
        var type = HeaderValue(headers, "type");
        if (id.Length == 0 || source.Length == 0 || type.Length == 0)
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeAttributesRequired);
        }

        DateTimeOffset? time = null;
        var timeText = HeaderValue(headers, "time");
        if (timeText.Length > 0)
        {
            if (!DateTimeOffset.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidTime, MessageArguments.Of(("time", timeText)));
            }

            time = parsed;
        }

        var subject = HeaderValue(headers, "subject");
        // 规范要求 binary 模式用 Content-Type 承载数据内容类型；给了 ce-datacontenttype 也接受
        var explicitContentType = HeaderValue(headers, "datacontenttype");
        var contentType = explicitContentType.Length > 0 ? explicitContentType : request.ContentType;

        var extensions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var header in headers)
        {
            if (!header.Key.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = header.Key[HeaderPrefix.Length..].ToLowerInvariant();
            if (_coreAttributes.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            if (!EnvelopeExtensions.IsValidName(name))
            {
                return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidExtension, MessageArguments.Of(("name", name)));
            }

            extensions[name] = DecodeHeaderValue(header.Value.ToString());
        }

        try
        {
            var envelope = new MessageEnvelope
            {
                Id = id,
                Source = source,
                Type = type,
                Time = time,
                Subject = subject.Length > 0 ? subject : null,
                Data = body,
                Extensions = extensions,
            };
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                envelope = envelope with { DataContentType = contentType };
            }

            return Result.Success<IReadOnlyList<MessageEnvelope>>([envelope]);
        }
        catch (ArgumentException exception)
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalid, MessageArguments.Of(("reason", exception.Message)));
        }
    }

    /// <summary>
    /// 取 <c>ce-&lt;name&gt;</c> 头并解码；缺失为空串。
    /// </summary>
    private static string HeaderValue(IHeaderDictionary headers, string name)
        => headers.TryGetValue(HeaderPrefix + name, out var values) ? DecodeHeaderValue(values.ToString()) : string.Empty;

    /// <summary>
    /// <c>Content-Type</c> 的媒体类型段（去掉参数，小写）。
    /// </summary>
    private static string MediaTypeOf(string? contentType)
        => string.IsNullOrWhiteSpace(contentType) ? string.Empty : contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
}
