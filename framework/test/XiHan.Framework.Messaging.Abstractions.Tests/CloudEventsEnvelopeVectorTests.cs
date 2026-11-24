// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 回放 <c>cloudevents-envelopes.json</c>：信封的结构化 JSON 编码
/// </summary>
/// <remarks>
/// <c>messaging/cloudevents-envelopes.json</c>（RFC-0009「信封」，<c>spec/protocol/cloudevents.md</c>「信封映射」与「入站」，ADR-0277、ADR-0283）。读得出的文档经 <see cref="CloudEventsFormat.Decode"/> 与向量给的信封逐项比较，再把向量给的信封 <see cref="CloudEventsFormat.Encode"/> 之后解码回来，读出的信封同样写出再读回；必须拒绝的文档都以同一个错误码拒绝
/// </remarks>
public sealed class CloudEventsEnvelopeVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly EnvelopeVectors _vectors = SpecVectors.Read<EnvelopeVectors>("messaging", "cloudevents-envelopes.json");

    [Fact]
    public void ContentTypeAndErrorCode_AreTheOnesInTheVectors()
    {
        Assert.Equal(CloudEventsFormat.ContentType, _vectors.ContentType);
        Assert.Equal(MessagingErrors.InvalidEnvelope, _vectors.Code);
    }

    [Fact]
    public void Decode_ReadsTheEnvelopeOfEveryValidDocument()
    {
        Assert.NotEmpty(_vectors.Valid);
        var failures = new List<string>();
        foreach (var vector in _vectors.Valid)
        {
            var decoded = CloudEventsFormat.Decode(Encoding.UTF8.GetBytes(vector.Text));
            if (!decoded.TryGetValue(out var envelope))
            {
                failures.Add($"{vector.Name}: the document was rejected with '{decoded.Error}'");
            }
            else if (Mismatch(vector.Expected, envelope) is { } mismatch)
            {
                failures.Add($"{vector.Name}: {mismatch}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Encode_WritesTheSameMapping_AndDecodesBackToTheSameEnvelope()
    {
        var failures = new List<string>();
        foreach (var vector in _vectors.Valid)
        {
            var expected = vector.Expected;
            var encoded = CloudEventsFormat.Encode(ToEnvelope(expected));

            // 编码是反过来的同一张对应：specversion 写 1.0，JSON 数据内联在 data，其他数据放 data_base64，扩展属性是顶层的字符串成员
            using var document = JsonDocument.Parse(encoded);
            var root = document.RootElement;
            var inline = root.TryGetProperty("data", out var data);
            var binary = root.TryGetProperty("data_base64", out var base64);
            if (root.GetProperty("specversion").GetString() != MessageEnvelope.SpecVersion
                || inline != (expected.Data is not null)
                || (expected.Data is { } expectedData && !JsonElement.DeepEquals(expectedData, data))
                || binary != (expected.DataBase64 is not null)
                || (expected.DataBase64 is not null && base64.GetString() != expected.DataBase64)
                || expected.Extensions.Any(extension => !root.TryGetProperty(extension.Key, out var member) || member.ValueKind != JsonValueKind.String || member.GetString() != extension.Value))
            {
                failures.Add($"{vector.Name}: the encoded document does not follow the mapping: {Encoding.UTF8.GetString(encoded)}");
                continue;
            }

            var decoded = CloudEventsFormat.Decode(encoded);
            if (!decoded.TryGetValue(out var envelope))
            {
                failures.Add($"{vector.Name}: the encoded document was rejected with '{decoded.Error}'");
            }
            else if (Mismatch(expected, envelope) is { } mismatch)
            {
                failures.Add($"{vector.Name}: after a round trip {mismatch}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Decode_ThenEncode_ThenDecode_GivesTheSameEnvelope_ForEveryValidDocument()
    {
        // 读得出的信封必须写得回去：以前文档带 data_base64 而不写 datacontenttype 时读成 application/json 的非 JSON 数据，再写出时抛出
        var failures = new List<string>();
        foreach (var vector in _vectors.Valid)
        {
            var envelope = CloudEventsFormat.Decode(Encoding.UTF8.GetBytes(vector.Text)).ValueOrThrow();
            try
            {
                var again = CloudEventsFormat.Decode(CloudEventsFormat.Encode(envelope));
                if (!again.TryGetValue(out var back))
                {
                    failures.Add($"{vector.Name}: the document written from the decoded envelope was rejected with '{again.Error}'");
                }
                else if (Mismatch(vector.Expected, back) is { } mismatch)
                {
                    failures.Add($"{vector.Name}: after decoding, encoding and decoding again {mismatch}");
                }
            }
            catch (FrameworkException exception)
            {
                failures.Add($"{vector.Name}: the decoded envelope could not be encoded: {exception.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Decode_RejectsEveryInvalidDocument_WithTheCodeInTheVectors()
    {
        Assert.NotEmpty(_vectors.Invalid);
        var failures = new List<string>();
        foreach (var vector in _vectors.Invalid)
        {
            var decoded = CloudEventsFormat.Decode(Encoding.UTF8.GetBytes(vector.Text));
            if (decoded.IsSuccess)
            {
                failures.Add($"{vector.Name}: the document was read");
            }
            else if (decoded.Error?.Code != _vectors.Code)
            {
                failures.Add($"{vector.Name}: the document was rejected with '{decoded.Error?.Code}', the vector has '{_vectors.Code}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverBothKindsOfData_AndBothSidesOfTheExtensionNameLimit()
    {
        // 防止向量换了写法后这几类一条都没核对还照样通过：内联数据、Base64 数据、没有数据，带偏移与不带偏移的时间
        Assert.Contains(_vectors.Valid, static vector => vector.Expected.Data is not null);
        Assert.Contains(_vectors.Valid, static vector => vector.Expected.DataBase64 is not null);
        Assert.Contains(_vectors.Valid, static vector => vector.Expected.Data is null && vector.Expected.DataBase64 is null);
        Assert.Contains(_vectors.Valid, static vector => vector.Expected.Time is not null && vector.Text.Contains("+08:00", StringComparison.Ordinal));
        Assert.Contains(_vectors.Valid, static vector => vector.Expected.Time is null);

        // 扩展属性名正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Valid, static vector => vector.Expected.Extensions.Keys.Any(static name => name.Length == 20));
        Assert.Contains(_vectors.Invalid, static vector => MemberNames(vector.Text).Any(static name => name.Length == 21 && name.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))));

        // 不是 JSON 对象的文本也要有
        Assert.Contains(_vectors.Invalid, static vector => MemberNames(vector.Text).Count == 0);
    }

    [Fact]
    public void TheVectors_CoverEveryRuleOfTheEnvelopeSection()
    {
        // RFC-0009「信封」五条里 ADR-0283 写明的各处，向量换了写法后不能一条都不剩
        // 第 1 条：重复的成员（顶层与 data 内部）
        Assert.Contains(_vectors.Invalid, static vector => MemberNames(vector.Text) is { Count: > 0 } names && names.Distinct(StringComparer.Ordinal).Count() < names.Count);
        Assert.Contains(_vectors.Invalid, static vector => vector.Text.Contains("\"data\":{\"a\":1,\"a\":2}", StringComparison.Ordinal));

        // 第 3 条：没有偏移的时间（通用的日期解析按本机时区读它），以及 DateTimeOffset 装不下、只有文法读得出的偏移
        Assert.Contains(_vectors.Invalid, static vector => Member(vector.Text, "time") is { ValueKind: JsonValueKind.String } time && DateTime.TryParseExact(time.GetString(), "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
        Assert.Contains(_vectors.Valid, static vector => vector.Text.Contains("+23:59", StringComparison.Ordinal) && vector.Expected.Time is not null);

        // 第 4 条：data 与 data_base64 同时出现；data_base64 而没有 datacontenttype
        Assert.Contains(_vectors.Invalid, static vector => Member(vector.Text, "data") is not null && Member(vector.Text, "data_base64") is not null);
        Assert.Contains(_vectors.Valid, static vector => Member(vector.Text, "data_base64") is not null && Member(vector.Text, "datacontenttype") is null && vector.Expected is { DataContentType: "application/octet-stream", DataBase64: not null });

        // 第 5 条：扩展属性的值是数字、布尔（保留原文）、null（没有这个属性），对象与数组不合法
        Assert.Contains(_vectors.Valid, static vector => Member(vector.Text, "seq") is { ValueKind: JsonValueKind.Number } && vector.Expected.Extensions.GetValueOrDefault("seq") == "1.50");
        Assert.Contains(_vectors.Valid, static vector => Member(vector.Text, "flag") is { ValueKind: JsonValueKind.True } && vector.Expected.Extensions.GetValueOrDefault("flag") == "true");
        Assert.Contains(_vectors.Valid, static vector => Member(vector.Text, "gone") is { ValueKind: JsonValueKind.Null } && !vector.Expected.Extensions.ContainsKey("gone"));
        Assert.Contains(_vectors.Invalid, static vector => Member(vector.Text, "detail") is { ValueKind: JsonValueKind.Object });
        Assert.Contains(_vectors.Invalid, static vector => Member(vector.Text, "tags") is { ValueKind: JsonValueKind.Array });
    }

    /// <summary>
    /// 向量给的信封
    /// </summary>
    /// <param name="expected">期望的信封</param>
    private static MessageEnvelope ToEnvelope(EnvelopeExpectation expected) => new()
    {
        Id = expected.Id,
        Source = expected.Source,
        Type = expected.Type,
        Time = expected.Time,
        Subject = expected.Subject,
        DataContentType = expected.DataContentType,
        Data = expected.Data is { } data ? Encoding.UTF8.GetBytes(data.GetRawText()) : expected.DataBase64 is { } base64 ? Convert.FromBase64String(base64) : default(ReadOnlyMemory<byte>),
        Extensions = expected.Extensions,
    };

    /// <summary>
    /// 读出的信封与期望的第一处不同
    /// </summary>
    /// <remarks>
    /// 时间按时刻比较，内联数据按 JSON 值比较（不比较空白与成员顺序），Base64 数据按字节比较；都相同为 <see langword="null"/>
    /// </remarks>
    /// <param name="expected">期望的信封</param>
    /// <param name="actual">读出的信封</param>
    private static string? Mismatch(EnvelopeExpectation expected, MessageEnvelope actual)
    {
        if (actual.Id != expected.Id || actual.Source != expected.Source || actual.Type != expected.Type)
        {
            return $"read id '{actual.Id}', source '{actual.Source}', type '{actual.Type}'";
        }

        if (actual.Time != expected.Time)
        {
            return $"read time '{actual.Time:O}', the vector has '{expected.Time:O}'";
        }

        if (actual.Subject != expected.Subject)
        {
            return $"read subject '{actual.Subject}', the vector has '{expected.Subject}'";
        }

        if (actual.DataContentType != expected.DataContentType)
        {
            return $"read datacontenttype '{actual.DataContentType}', the vector has '{expected.DataContentType}'";
        }

        if (actual.Extensions.Count != expected.Extensions.Count || expected.Extensions.Any(extension => !actual.Extensions.TryGetValue(extension.Key, out var value) || value != extension.Value))
        {
            return $"read the extensions [{string.Join(", ", actual.Extensions.OrderBy(static extension => extension.Key, StringComparer.Ordinal).Select(static extension => $"{extension.Key}={extension.Value}"))}]";
        }

        if (expected.Data is { } data)
        {
            return actual.Data.IsEmpty || !SameJson(data, actual.Data) ? $"read the data '{Encoding.UTF8.GetString(actual.Data.Span)}', the vector has '{data.GetRawText()}'" : null;
        }

        if (expected.DataBase64 is { } base64)
        {
            return actual.Data.Span.SequenceEqual(Convert.FromBase64String(base64)) ? null : $"read the data '{Convert.ToBase64String(actual.Data.Span)}' (Base64), the vector has '{base64}'";
        }

        return actual.Data.IsEmpty ? null : $"read {actual.Data.Length} bytes of data, the vector has none";
    }

    /// <summary>
    /// 字节是否是与给定值相等的 JSON
    /// </summary>
    /// <param name="expected">期望的 JSON 值</param>
    /// <param name="actual">读出的数据</param>
    private static bool SameJson(JsonElement expected, ReadOnlyMemory<byte> actual)
    {
        try
        {
            using var document = JsonDocument.Parse(actual);
            return JsonElement.DeepEquals(expected, document.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 文档顶层的一个成员
    /// </summary>
    /// <remarks>
    /// 不是 JSON 对象的文本与没有这个成员的文档为 <see langword="null"/>；重复的成员取最后一个
    /// </remarks>
    /// <param name="text">文档原文</param>
    /// <param name="name">成员名</param>
    private static JsonElement? Member(string text, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(name, out var member) ? member.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 文档顶层的成员名
    /// </summary>
    /// <remarks>
    /// 不是 JSON 对象的文本没有成员名
    /// </remarks>
    /// <param name="text">文档原文</param>
    private static List<string> MemberNames(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object ? [.. document.RootElement.EnumerateObject().Select(static member => member.Name)] : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="ContentType">结构化编码的内容类型</param>
    /// <param name="Code">拒绝文档的错误码</param>
    /// <param name="Valid">必须读得出的文档</param>
    /// <param name="Invalid">必须拒绝的文档</param>
    private sealed record EnvelopeVectors(string ContentType, string Code, ValidDocument[] Valid, InvalidDocument[] Invalid);

    /// <summary>
    /// 一份必须读得出的文档
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="Text">文档原文</param>
    /// <param name="Expected">读出的信封</param>
    private sealed record ValidDocument(string Name, string Text, EnvelopeExpectation Expected);

    /// <summary>
    /// 一份必须拒绝的文档
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="Text">文档原文</param>
    private sealed record InvalidDocument(string Name, string Text);

    /// <summary>
    /// 期望读出的信封
    /// </summary>
    /// <param name="Id">事件 ID</param>
    /// <param name="Source">来源</param>
    /// <param name="Type">事件类型</param>
    /// <param name="Time">发生时间（UTC 时刻）；<see langword="null"/> 表示没有</param>
    /// <param name="Subject">主题标识；<see langword="null"/> 表示没有</param>
    /// <param name="DataContentType">数据内容类型</param>
    /// <param name="Extensions">扩展属性，名字到字符串值</param>
    /// <param name="Data">内联的 JSON 数据；与 <paramref name="DataBase64"/> 都没有表示没有数据</param>
    /// <param name="DataBase64">数据的标准 Base64</param>
    private sealed record EnvelopeExpectation(
        string Id,
        string Source,
        string Type,
        DateTimeOffset? Time,
        string? Subject,
        string DataContentType,
        Dictionary<string, string> Extensions,
        JsonElement? Data = null,
        string? DataBase64 = null);
}
