// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents.Inbound;

/// <summary>
/// CloudEvents 1.0 的 HTTP 协议绑定
/// </summary>
/// <remarks>
/// 读请求（binary / structured / batch）成信封，把信封写进出站请求（单条 binary / structured，多条 batch）。binary 模式的头值按规范百分号编码非可打印 ASCII 字符；structured 与 batch 复用 <see cref="CloudEventsFormat"/> 的结构化 JSON
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class CloudEventsHttpBinding
{
    /// <summary>
    /// binary 模式属性头的前缀
    /// </summary>
    public const string HeaderPrefix = "ce-";

    /// <summary>
    /// structured 模式的内容类型
    /// </summary>
    public const string StructuredContentType = CloudEventsFormat.ContentType;

    /// <summary>
    /// batch 模式的内容类型
    /// </summary>
    public const string BatchContentType = "application/cloudevents-batch+json";

    /// <summary>
    /// 规范版本头
    /// </summary>
    private const string SpecVersionHeader = HeaderPrefix + "specversion";

    /// <summary>
    /// 文档根的路径
    /// </summary>
    private const string RootPath = "$";

    /// <summary>
    /// 把 binary 的数据当作结构化文档的 <c>data</c> 成员时，数据前面的文本
    /// </summary>
    private static ReadOnlySpan<byte> DataMemberPrefix => "{\"data\":"u8;

    /// <summary>
    /// 严格的 UTF-8 编码
    /// </summary>
    /// <remarks>
    /// 遇到孤立的代理项抛出，不悄悄换成 U+FFFD（ADR-0318）
    /// </remarks>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// 不作为扩展属性的核心属性名
    /// </summary>
    /// <remarks>
    /// <c>dataschema</c> 是信封承载的规范属性（ADR-0305），不在这里就会被当成名字是保留名的扩展属性
    /// </remarks>
    private static readonly string[] _coreAttributes = ["specversion", "id", "source", "type", "time", "subject", "dataschema", "datacontenttype"];

    /// <summary>
    /// 判断请求的内容模式
    /// </summary>
    /// <remarks>
    /// <c>Content-Type</c> 是结构化或批量格式，否则有 <c>ce-specversion</c> 头即 binary；都不是则无值
    /// </remarks>
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
    /// 读请求里的事件
    /// </summary>
    /// <remarks>
    /// batch 以外总是一个元素。不是 CloudEvent 返回 <see cref="CloudEventsErrors.UnsupportedContentType"/>，信封不合法返回 <see cref="MessagingErrors.InvalidEnvelope"/>；请求体超过 <see cref="CloudEventsProtocolOptions.MaxRequestBodyBytes"/> 返回 <see cref="CloudEventsErrors.BodyTooLarge"/>，batch 的事件数超过 <see cref="CloudEventsProtocolOptions.MaxBatchSize"/> 返回 <see cref="CloudEventsErrors.BatchTooLarge"/>（ADR-0187）
    /// </remarks>
    public static async ValueTask<Result<IReadOnlyList<MessageEnvelope>>> ReadAsync(HttpRequest request, CloudEventsProtocolOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        if (!ModeOf(request).TryGetValue(out var mode))
        {
            return Error.Validation(CloudEventsErrors.UnsupportedContentType, CloudEventsMessages.ContentTypeUnsupported, MessageArguments.Of(("contentType", request.ContentType ?? string.Empty)));
        }

        var read = await HttpInvocation.ReadBodyAsync(request.HttpContext, options.MaxRequestBodyBytes, cancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var body))
        {
            return Error.Validation(CloudEventsErrors.BodyTooLarge, CloudEventsMessages.BodyTooLarge, MessageArguments.Of(("max", options.MaxRequestBodyBytes)));
        }

        switch (mode)
        {
            case CloudEventsContentMode.Structured:
                var decoded = CloudEventsFormat.Decode(body);
                return decoded.TryGetValue(out var envelope) ? Result.Success<IReadOnlyList<MessageEnvelope>>([envelope]) : decoded.Error!;
            case CloudEventsContentMode.Batch:
                return ReadBatch(body, options.MaxBatchSize);
            default:
                return ReadBinary(request, body);
        }
    }

    /// <summary>
    /// 把多个信封以 batch 模式写进出站请求
    /// </summary>
    /// <remarks>
    /// <c>application/cloudevents-batch+json</c>，请求体是结构化 JSON 事件的数组，顺序同入参
    /// </remarks>
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
    /// 把一个信封写进出站请求
    /// </summary>
    /// <remarks>
    /// binary 模式写 <c>ce-*</c> 头与数据体，structured 模式写结构化 JSON 体；多条用 <see cref="WriteBatch"/>。写不出的信封在动请求之前抛出携带 <see cref="MessagingErrors.InvalidEnvelope"/> 的 <see cref="FrameworkException"/>：structured 照 <see cref="CloudEventsFormat.Encode"/>；binary 的字符串属性有孤立的代理项时同样抛出，消息与 <see cref="CloudEventsFormat.Encode"/> 的相同、带属性名（<c>spec/protocol/cloudevents.md</c>「出站」，ADR-0318，以前编成 U+FFFD 的 <c>%EF%BF%BD</c>）。binary 声明为 JSON 的数据（<see cref="MessageEnvelope.IsJsonData"/>）照 <see cref="CloudEventsFormat.Encode"/> 核对，structured 写不出的（不是 JSON、重复的成员、嵌套让文档超过 64 层、字符串不是合法的 Unicode）抛出同样的异常（ADR-0323，以前原样作请求体发出）
    /// </remarks>
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
                // 写任何头之前先核对：与 structured 同一个口径，写到一半才发现就留下半份请求（RFC-0001「写出与转交」，ADR-0318）。
                // 声明为 JSON 的数据原样作请求体，接收方照结构化 JSON 的规则读它：照结构化 JSON 写一遍，属性与数据的核对、先后与异常都与 structured 相同（ADR-0323）；
                // 其余数据不按 JSON 读，只查属性
                if (envelope.IsJsonData && !envelope.Data.IsEmpty)
                {
                    _ = CloudEventsFormat.Encode(envelope);
                }
                else if (LoneSurrogateAttributeOf(envelope) is { } attribute)
                {
                    throw new FrameworkException(MessagingErrors.InvalidEnvelope, $"Envelope '{envelope.Id}' cannot be encoded: attribute '{attribute}' has a lone surrogate.");
                }

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

                if (envelope.DataSchema is { } dataSchema)
                {
                    request.Headers.TryAddWithoutValidation(HeaderPrefix + "dataschema", EncodeHeaderValue(dataSchema));
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
    /// 按 CloudEvents HTTP 绑定编码头值
    /// </summary>
    /// <remarks>
    /// 可打印 ASCII（<c>%</c> 除外）原样，其余按 UTF-8 字节百分号编码。孤立的代理项没有 UTF-8 字节，抛出 <see cref="ArgumentException"/>（ADR-0318，以前编成 U+FFFD 的 <c>%EF%BF%BD</c>）
    /// </remarks>
    public static string EncodeHeaderValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsEncodable(value))
        {
            throw new ArgumentException("The header value has a lone surrogate and cannot be percent-encoded as UTF-8.", nameof(value));
        }

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
    /// 字符串里有孤立的代理项的第一个属性
    /// </summary>
    /// <remarks>
    /// 属性与先后都与 <see cref="CloudEventsFormat.Encode"/> 的核对相同：规范属性按结构化文档里的先后，再按扩展属性的顺序；扩展属性名合文法（ASCII），只查值
    /// </remarks>
    /// <returns>都能严格编码成 UTF-8 时为 <see langword="null"/>；否则是属性名</returns>
    private static string? LoneSurrogateAttributeOf(MessageEnvelope envelope)
    {
        (string Name, string? Value)[] attributes =
        [
            ("id", envelope.Id),
            ("source", envelope.Source),
            ("type", envelope.Type),
            ("subject", envelope.Subject),
            ("dataschema", envelope.DataSchema),
            ("datacontenttype", envelope.DataContentType),
        ];
        foreach (var (name, value) in attributes)
        {
            if (value is not null && !IsEncodable(value))
            {
                return name;
            }
        }

        foreach (var (name, value) in envelope.Extensions)
        {
            if (!IsEncodable(value))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// 字符串能否严格编码成 UTF-8
    /// </summary>
    /// <remarks>
    /// 编码不成的只有孤立的代理项
    /// </remarks>
    private static bool IsEncodable(string text)
    {
        try
        {
            _strictUtf8.GetByteCount(text);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// 解码头值的百分号编码
    /// </summary>
    /// <remarks>
    /// CloudEvents HTTP 协议绑定的百分号解码（<c>spec/protocol/cloudevents.md</c>「binary 的头值」，ADR-0323）：逐个字符换成字节，<c>%</c> 与紧跟的两位十六进制数字（大小写都收）是一个字节，其余字符是它的 UTF-8 编码，再按 UTF-8 严格解码；不必要的编码照收。
    /// <c>%</c> 后不是两位十六进制数字、字节不是合法的 UTF-8、值里有孤立的代理项时无值（以前用 <see cref="Uri.UnescapeDataString(string)"/>，原样留成百分号文本，返回 <see langword="string"/>）
    /// </remarks>
    /// <returns>解码出的值；读不出时无值</returns>
    public static Maybe<string> DecodeHeaderValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // 没有 % 的值不用换字节：编得成 UTF-8（没有孤立的代理项）就是它本身
        if (!value.Contains('%'))
        {
            return UnicodeText.HasLoneSurrogate(value) ? default(Maybe<string>) : value;
        }

        // 一个字符至多三个 UTF-8 字节（代理对两个字符四个字节），百分号序列三个字符一个字节
        var bytes = ArrayPool<byte>.Shared.Rent(value.Length * 3);
        try
        {
            var length = 0;
            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] == '%')
                {
                    // 后面不足两个字符或不是十六进制数字：没编码的 % 是写错的值，不照原样留下（ADR-0323）
                    if (index + 2 >= value.Length || !byte.TryParse(value.AsSpan(index + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var octet))
                    {
                        return default;
                    }

                    bytes[length++] = octet;
                    index += 2;
                    continue;
                }

                // 孤立的代理项没有 UTF-8 编码
                if (!Rune.TryGetRuneAt(value, index, out var rune))
                {
                    return default;
                }

                length += rune.EncodeToUtf8(bytes.AsSpan(length));
                index += rune.Utf16SequenceLength - 1;
            }

            var decoded = bytes.AsSpan(0, length);
            return Utf8.IsValid(decoded) ? Encoding.UTF8.GetString(decoded) : default(Maybe<string>);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    /// <summary>
    /// batch
    /// </summary>
    /// <remarks>
    /// JSON 数组，每个元素一个结构化事件；元素数超过 <paramref name="max"/> 时整批失败、一个也不解码，第一个不合法的元素让整批失败
    /// </remarks>
    private static Result<IReadOnlyList<MessageEnvelope>> ReadBatch(byte[] body, int max)
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

            var count = document.RootElement.GetArrayLength();
            if (count > max)
            {
                return Error.Validation(CloudEventsErrors.BatchTooLarge, CloudEventsMessages.BatchTooLarge, MessageArguments.Of(("count", count), ("max", max)));
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
    /// binary
    /// </summary>
    /// <remarks>
    /// <c>ce-specversion</c>、<c>ce-id</c>、<c>ce-source</c>、<c>ce-type</c> 必填，<c>ce-time</c>（<see cref="TimestampText"/> 的文法，读出 UTC 时刻）、<c>ce-subject</c>、<c>ce-dataschema</c>（ADR-0305）可选，值为空与没有这个头相同，其余 <c>ce-*</c> 是扩展属性（名字不能是保留名，<c>ce-data</c> 因此不合法，ADR-0298）；每个 <c>ce-*</c> 头的值经 <see cref="DecodeHeaderValue"/> 解码，读不出即 <see cref="CloudEventsMessages.HeaderInvalid"/>（ADR-0323）；数据内容类型取 <c>Content-Type</c>，数据取请求体，声明为 JSON 的数据过与 structured 相同的检查（<see cref="DataProblemOf"/>，ADR-0323）
    /// </remarks>
    private static Result<IReadOnlyList<MessageEnvelope>> ReadBinary(HttpRequest request, byte[] body)
    {
        var headers = request.Headers;

        // 规范属性的头按固定的先后先解码：读不出的值不能当作另一个值往下核对，第一个读不出的即信封不合法（ADR-0323）
        var core = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in _coreAttributes)
        {
            if (headers.TryGetValue(HeaderPrefix + attribute, out var raw))
            {
                if (!DecodeHeaderValue(raw.ToString()).TryGetValue(out var decoded))
                {
                    return HeaderInvalid(HeaderPrefix + attribute);
                }

                core[attribute] = decoded;
            }
        }

        // 缺失的头为空串
        string HeaderValue(string name) => core.GetValueOrDefault(name, string.Empty);

        if (!string.Equals(HeaderValue("specversion"), MessageEnvelope.SpecVersion, StringComparison.Ordinal))
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeSpecVersion, MessageArguments.Of(("specVersion", MessageEnvelope.SpecVersion)));
        }

        var id = HeaderValue("id");
        var source = HeaderValue("source");
        var type = HeaderValue("type");
        if (id.Length == 0 || source.Length == 0 || type.Length == 0)
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeAttributesRequired);
        }

        DateTimeOffset? time = null;
        var timeText = HeaderValue("time");
        if (timeText.Length > 0)
        {
            // 与结构化 JSON 的 time 同一条文法（RFC-0009「信封」，ADR-0283）：不交给通用的日期解析，它把没有偏移的时间按本机时区读
            if (!TimestampText.TryParse(timeText, out var parsed))
            {
                return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidTime, MessageArguments.Of(("time", timeText)));
            }

            time = parsed;
        }

        var subject = HeaderValue("subject");
        var dataSchema = HeaderValue("dataschema");
        // 规范要求 binary 模式用 Content-Type 承载数据内容类型；给了 ce-datacontenttype 也接受
        var explicitContentType = HeaderValue("datacontenttype");
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

            if (!DecodeHeaderValue(header.Value.ToString()).TryGetValue(out var value))
            {
                return HeaderInvalid(HeaderPrefix + name);
            }

            extensions[name] = value;
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
                // 头值为空按没有读，与 ce-subject、ce-time 相同；只有空白的由信封的 init 拒绝
                DataSchema = dataSchema.Length > 0 ? dataSchema : null,
                Data = body,
                Extensions = extensions,
            };
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                envelope = envelope with { DataContentType = contentType };
            }

            // 声明为 JSON 的数据过与 structured 相同的检查：读进来的信封都要写得成结构化 JSON，否则处理器收到 structured 读不进的数据，发布模式下远程代理写不出它（ADR-0323）
            if (envelope.IsJsonData && body.Length > 0 && DataProblemOf(body).TryGetValue(out var path))
            {
                return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidJson, MessageArguments.Of(("reason", path)));
            }

            return Result.Success<IReadOnlyList<MessageEnvelope>>([envelope]);
        }
        catch (ArgumentException exception)
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalid, MessageArguments.Of(("reason", exception.Message)));
        }
    }

    /// <summary>
    /// binary 声明为 JSON 的数据没过检查的位置
    /// </summary>
    /// <remarks>
    /// 照 structured 读同一个事件：把数据当作结构化文档里的 <c>data</c> 成员（第 2 层），这份文档过 RFC-0001「JSON 文本」的共同检查，位置写法与 <see cref="CloudEventsFormat.Decode"/> 的相同；包进去之后合法、单独却不是一个 JSON 值的（如 <c>1,"a":2</c>）同样不合法，位置为 <c>$</c>
    /// </remarks>
    /// <returns>通过时无值；否则是 <c>reason</c> 参数</returns>
    private static Maybe<string> DataProblemOf(byte[] data)
    {
        var length = DataMemberPrefix.Length + data.Length + 1;
        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            DataMemberPrefix.CopyTo(rented);
            data.CopyTo(rented.AsSpan(DataMemberPrefix.Length));
            rented[length - 1] = (byte)'}';
            if (JsonText.FindProblem(rented.AsSpan(0, length)).TryGetValue(out var problem))
            {
                return problem.Path;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        // 包进去的文档已经过了检查，单独读数据只剩一种失败：它不是一个值，而是几个成员拼起来的文本
        var reader = new Utf8JsonReader(data);
        try
        {
            while (reader.Read())
            {
            }

            return default;
        }
        catch (JsonException)
        {
            return RootPath;
        }
    }

    /// <summary>
    /// 头值读不出的错误
    /// </summary>
    /// <param name="header">头名，小写</param>
    private static Error HeaderInvalid(string header)
        => Error.Validation(MessagingErrors.InvalidEnvelope, CloudEventsMessages.HeaderInvalid, MessageArguments.Of(("header", header)));

    /// <summary>
    /// <c>Content-Type</c> 的媒体类型段（去掉参数，小写）
    /// </summary>
    private static string MediaTypeOf(string? contentType)
        => string.IsNullOrWhiteSpace(contentType) ? string.Empty : contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
}
