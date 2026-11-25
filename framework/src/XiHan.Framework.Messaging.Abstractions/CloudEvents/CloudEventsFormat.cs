// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.CloudEvents;

/// <summary>
/// CloudEvents 1.0 结构化 JSON 编码（<c>application/cloudevents+json</c>）
/// </summary>
/// <remarks>
/// JSON 数据内联在 <c>data</c>，其他数据放在 <c>data_base64</c>，<c>dataschema</c> 有则写成同名的成员（ADR-0305），扩展属性是顶层成员。读取的规则在 RFC-0009「信封」逐条写明（ADR-0283）：文档先过 RFC-0001「JSON 文本」的共同检查，<c>time</c> 只认 RFC 3339 的一条文法，<c>data</c> 与 <c>data_base64</c> 互斥，扩展属性的值是字符串、数字或布尔。写出的文档一定读得回来
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class CloudEventsFormat
{
    /// <summary>
    /// 结构化编码的内容类型
    /// </summary>
    public const string ContentType = "application/cloudevents+json";

    /// <summary>
    /// 文档不写 <c>datacontenttype</c> 而数据在 <c>data_base64</c> 里时的数据内容类型
    /// </summary>
    private const string BinaryDataContentType = "application/octet-stream";

    /// <summary>
    /// 文档不写 <c>datacontenttype</c> 时的数据内容类型
    /// </summary>
    private const string DefaultDataContentType = "application/json";

    /// <summary>
    /// 文档根的路径
    /// </summary>
    private const string RootPath = "$";

    /// <summary>
    /// 编码
    /// </summary>
    /// <remarks>
    /// 写出的文档必须读得回来，写不出这样的文档时抛出携带 <see cref="MessagingErrors.InvalidEnvelope"/> 的 <see cref="FrameworkException"/>：JSON 数据不是合法 JSON，或文档过不了读取一侧的共同检查（RFC-0001「JSON 文本」）——JSON 数据里有重复的成员名、嵌套让文档超过 64 层、字符串里有不成对的代理项转义。扩展属性与规范属性同名的信封构造不出来（<see cref="EnvelopeExtensions.ReservedNames"/>，ADR-0298），写出之后的检查照样覆盖整份文档
    /// </remarks>
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
            DataSchema = envelope.DataSchema,
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

        byte[] encoded;
        try
        {
            encoded = JsonSerializer.SerializeToUtf8Bytes(document, MessagingJsonContext.Default.CloudEventDocument);
        }
        catch (JsonException exception)
        {
            // 序列化器自己的深度上限同为 64 层：JSON 数据嵌套到 64 层时文档是 65 层，写不出来
            throw new FrameworkException(MessagingErrors.InvalidEnvelope, Unreadable(envelope), exception);
        }

        // 写出的文档自己过一遍读取一侧的检查：过不了的在发布时就拒绝，不等消费方把它当成无法解码的消息送进死信
        // 能让它过不了的只有调用方给的 JSON 数据（重复成员、不成对的代理项转义）；与规范属性重名的扩展在构造信封时已经拒绝（ADR-0298），
        // 这里仍查整份文档而不只查数据：信封以后多承载一个规范属性而保留名漏了更新，写出的重复成员照样在发布时拦下
        if (JsonText.FindProblem(encoded).TryGetValue(out var problem))
        {
            throw new FrameworkException(
                MessagingErrors.InvalidEnvelope,
                problem.Kind == JsonTextProblemKind.DuplicateMember ? $"Envelope '{envelope.Id}' cannot be encoded: member '{problem.Path}' would appear twice in the document." : Unreadable(envelope));
        }

        return encoded;
    }

    /// <summary>
    /// 写出的文档过不了读取一侧语法检查时的异常消息
    /// </summary>
    /// <param name="envelope">写不出的信封</param>
    private static string Unreadable(MessageEnvelope envelope)
        => $"Envelope '{envelope.Id}' cannot be encoded: its JSON data is nested too deeply (the document may not exceed {JsonText.MaxDepth} levels) or contains a string that is not valid Unicode.";

    /// <summary>
    /// 解码
    /// </summary>
    /// <remarks>
    /// 文档不合法返回 <see cref="MessagingErrors.InvalidEnvelope"/>（RFC-0009「信封」）：没过 RFC-0001「JSON 文本」的共同检查（不是 JSON、嵌套超过 64 层、字符串不是合法的 Unicode、任何一层有重复的成员名，含 <c>data</c> 内部）；不是 JSON 对象；<c>specversion</c> 不是 <c>1.0</c>；<c>id</c>、<c>source</c>、<c>type</c> 缺失或为空；<c>dataschema</c> 不是字符串或为空（它是信封承载的规范属性，不落进扩展属性，ADR-0305）；<c>time</c> 不合 <see cref="TimestampText"/> 的文法；<c>data</c> 与 <c>data_base64</c> 同时出现；<c>data_base64</c> 不是 Base64；扩展属性名不合文法或是保留名（ADR-0298），或值是对象、数组。
    /// 读出的 <c>time</c> 是 UTC 时刻。文档带 <c>data_base64</c> 而不写 <c>datacontenttype</c> 时数据内容类型是 <c>application/octet-stream</c>，否则缺省 <c>application/json</c>。扩展属性的值：字符串照原样，数字与布尔保留 JSON 原文，<c>null</c> 视为没有这个属性
    /// </remarks>
    public static Result<MessageEnvelope> Decode(ReadOnlyMemory<byte> json)
    {
        // 先过共同的读取检查：重复的成员不取第一个也不取最后一个，整个文档不合法；语法错误只写 $，重复成员写它的路径
        if (JsonText.FindProblem(json.Span).TryGetValue(out var problem))
        {
            return InvalidJson(problem.Path);
        }

        CloudEventDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json.Span, MessagingJsonContext.Default.CloudEventDocument);
        }
        catch (JsonException exception)
        {
            // 语法已经查过，到这里的是形状不对：顶层不是对象，或规范属性不是字符串。只留路径：异常消息带目标类型的全名
            return InvalidJson(exception.Path ?? RootPath);
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
            // 只认 RFC 3339 的一条文法（与截止时间的文本同一条），不交给通用的日期解析：它照收空格代替 T、只有日期等写法，没有偏移的还按本机时区读
            if (!TimestampText.TryParse(document.Time, out var parsedTime))
            {
                return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidTime, MessageArguments.Of(("time", document.Time)));
            }

            time = parsedTime;
        }

        // data 与 data_base64 互斥（CloudEvents 的 JSON 格式）：两个都带着数据时不挑一个读
        if (document.Data is not null && document.DataBase64 is not null)
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeDataConflict);
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

            // 扩展值是字符串、数字或布尔：数字与布尔保留 JSON 原文（1.50 仍是 1.50）；null 视为没有这个属性；对象与数组装不进一个字符串值，整个信封不合法
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    extensions[name] = value.GetString() ?? string.Empty;
                    break;
                case JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False:
                    extensions[name] = value.GetRawText();
                    break;
                case JsonValueKind.Null:
                    break;
                default:
                    return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidExtensionValue, MessageArguments.Of(("name", name)));
            }
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
                // 空串与只有空白的 dataschema 由信封的 init 拒绝，与 datacontenttype 为空同一条路（ADR-0305）
                DataSchema = document.DataSchema,
                // 数据在 data_base64 里而文档没写内容类型：不是 JSON，按二进制读；读成缺省的 application/json 会得到一个写不回去的信封
                DataContentType = document.DataContentType ?? (document.DataBase64 is not null ? BinaryDataContentType : DefaultDataContentType),
                Data = data,
                Extensions = extensions,
            });
        }
        catch (ArgumentException exception)
        {
            return Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalid, MessageArguments.Of(("reason", exception.Message)));
        }
    }

    /// <summary>
    /// 文档不是合法的 CloudEvents JSON
    /// </summary>
    /// <param name="path">出错的位置：语法错误与顶层不是对象为 <c>$</c>，重复的成员或类型不对的属性为它的路径</param>
    private static Error InvalidJson(string path)
        => Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidJson, MessageArguments.Of(("reason", path)));
}
