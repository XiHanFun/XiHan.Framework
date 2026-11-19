// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// Result 与 Error 的线上 JSON 编码
/// </summary>
/// <remarks>
/// 读写照 RFC-0001「Result」，与各语言 SDK 逐字一致（ADR-0244）：有值成功 <c>{"ok":true,"value":…}</c>，无值成功只写 <c>{"ok":true}</c>，失败 <c>{"ok":false,"error":{…}}</c>；
/// <see cref="Error"/> 按 <c>code</c>、<c>message</c>、<c>messageKey</c>、<c>details</c>、<c>inner</c> 的顺序写出，<c>message</c> 是发送端已解析的文案（<see cref="Error.Message"/>），没有键、细节为空、没有内部错误时对应成员不写。
/// 读取先过「JSON 文本」的共同检查（语法、64 层、合法 Unicode、任何一层的重复成员），再按规则核对；不合法时返回 <see cref="ErrorCode.InvalidArgument"/> 失败，消息键 <see cref="KernelMessages.WireInvalidJson"/> 或 <see cref="KernelMessages.WireInvalid"/>，<c>reason</c> 是出错的路径。
/// 对方在线上说的失败是读出的结果本身，所以读取返回结果的结果：外层失败是编码不合法，外层成功时内层才是对方的成功或失败。
/// 值经调用方给的 <see cref="JsonTypeInfo{T}"/>（源生成）读写，数字的写法由它决定；要按值模型原样往返任意 JSON 用 <see cref="JsonElement"/>，错误细节按值模型读写（64 位范围内的整数写法是整数，其余是双精度）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class ResultJson
{
    /// <summary>
    /// 结果里 <c>value</c> 成员的路径
    /// </summary>
    private const string ValuePath = "$.value";

    /// <summary>
    /// 严格的 UTF-8 编码
    /// </summary>
    /// <remarks>
    /// 文本里的孤立代理项不悄悄换成 U+FFFD，按不是 JSON 处理
    /// </remarks>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// 写出无值的结果
    /// </summary>
    /// <param name="result">要写出的结果；<c>default</c> 按 <see cref="ErrorCode.UninitializedResult"/> 失败写出</param>
    /// <returns>成功时为 <c>{"ok":true}</c>，失败时带 <c>error</c></returns>
    public static string Write(Result result)
        => WriteEnvelope(result.Error, default);

    /// <summary>
    /// 写出有值的结果
    /// </summary>
    /// <remarks>
    /// 成功值按 <paramref name="valueInfo"/> 写出；可能没有的值用 <see cref="WriteMaybe{T}(Result{Maybe{T}}, JsonTypeInfo{T})"/>
    /// </remarks>
    /// <param name="result">要写出的结果；<c>default</c> 按 <see cref="ErrorCode.UninitializedResult"/> 失败写出</param>
    /// <param name="valueInfo">值的类型的序列化元数据（源生成）</param>
    /// <returns>成功时为 <c>{"ok":true,"value":…}</c>，失败时带 <c>error</c></returns>
    /// <exception cref="ArgumentException">成功值写出来是 JSON 的 <c>null</c></exception>
    public static string Write<T>(Result<T> result, JsonTypeInfo<T> valueInfo)
    {
        ArgumentNullException.ThrowIfNull(valueInfo);
        if (result.Error is { } failure)
        {
            return WriteEnvelope(failure, default);
        }

        var value = JsonSerializer.SerializeToUtf8Bytes(result.ValueOrThrow(), valueInfo);
        // 有值成功的 value 不能是 null：写出去对方读不出，可能没有的值是 Maybe
        if (value.AsSpan().SequenceEqual("null"u8))
        {
            throw new ArgumentException("A successful result needs a value on the wire, but the value is written as JSON null; a value that may be absent must be a 'Maybe' written with 'ResultJson.WriteMaybe'.", nameof(result));
        }

        return WriteEnvelope(null, value);
    }

    /// <summary>
    /// 写出值类型为 Maybe 的结果
    /// </summary>
    /// <remarks>
    /// 无值的 <see cref="Maybe{T}"/> 写成 <c>"value":null</c>，有值时按 <paramref name="valueInfo"/> 写出值本身（RFC-0001「Maybe」）
    /// </remarks>
    /// <param name="result">要写出的结果；<c>default</c> 按 <see cref="ErrorCode.UninitializedResult"/> 失败写出</param>
    /// <param name="valueInfo">Maybe 里的值的类型的序列化元数据（源生成）</param>
    /// <returns>成功时为 <c>{"ok":true,"value":…}</c>，失败时带 <c>error</c></returns>
    /// <exception cref="ArgumentException">有值的 Maybe 里的值写出来是 JSON 的 <c>null</c></exception>
    public static string WriteMaybe<T>(Result<Maybe<T>> result, JsonTypeInfo<T> valueInfo)
    {
        ArgumentNullException.ThrowIfNull(valueInfo);
        if (result.Error is { } failure)
        {
            return WriteEnvelope(failure, default);
        }

        if (!result.ValueOrThrow().TryGetValue(out var some))
        {
            return WriteEnvelope(null, "null"u8);
        }

        var value = JsonSerializer.SerializeToUtf8Bytes(some, valueInfo);
        // 有值写成 null 就与无值分不开，对方读成 None
        if (value.AsSpan().SequenceEqual("null"u8))
        {
            throw new ArgumentException("The value of a 'Maybe' that has one is written as JSON null, which reads back as no value.", nameof(result));
        }

        return WriteEnvelope(null, value);
    }

    /// <summary>
    /// 写出一个错误
    /// </summary>
    /// <param name="error">要写出的错误</param>
    /// <returns>按 <c>code</c>、<c>message</c>、<c>messageKey</c>、<c>details</c>、<c>inner</c> 的顺序写出的 JSON 对象</returns>
    public static string WriteError(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteErrorObject(writer, error);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// 读取无值的结果
    /// </summary>
    /// <remarks>
    /// 成功只能是 <c>{"ok":true}</c>：带了 <c>value</c> 即不合法，<c>{}</c> 与 <c>null</c> 也一样
    /// </remarks>
    /// <param name="json">线上的 JSON 文本</param>
    /// <returns>外层失败表示编码不合法；外层成功时是读出的结果</returns>
    public static Result<Result> Read(string json)
    {
        var parsed = Parse(json);
        if (parsed.Error is { } invalid)
        {
            return invalid;
        }

        using var document = parsed.ValueOrThrow();
        var envelope = ReadEnvelope(document.RootElement);
        if (envelope.Error is { } malformed)
        {
            return malformed;
        }

        var read = envelope.ValueOrThrow();
        if (read.Failure is { } failure)
        {
            return Result.Success(Result.Failure(failure));
        }

        return read.Value is null ? Result.Success(Result.Success()) : Invalid(ValuePath);
    }

    /// <summary>
    /// 读取有值的结果
    /// </summary>
    /// <remarks>
    /// 成功必须带 <c>value</c> 且不是 <c>null</c>；可能没有的值用 <see cref="ReadMaybe{T}(string, JsonTypeInfo{T})"/>。值不合类型时 <c>reason</c> 是值里出错的路径（如 <c>$.value.id</c>）
    /// </remarks>
    /// <param name="json">线上的 JSON 文本</param>
    /// <param name="valueInfo">值的类型的序列化元数据（源生成）</param>
    /// <returns>外层失败表示编码不合法；外层成功时是读出的结果</returns>
    public static Result<Result<T>> Read<T>(string json, JsonTypeInfo<T> valueInfo)
    {
        ArgumentNullException.ThrowIfNull(valueInfo);
        var parsed = Parse(json);
        if (parsed.Error is { } invalid)
        {
            return invalid;
        }

        using var document = parsed.ValueOrThrow();
        var envelope = ReadEnvelope(document.RootElement);
        if (envelope.Error is { } malformed)
        {
            return malformed;
        }

        var read = envelope.ValueOrThrow();
        if (read.Failure is { } failure)
        {
            return Result.Success(Result.Failure<T>(failure));
        }

        if (read.Value is not { ValueKind: not JsonValueKind.Null } value)
        {
            return Invalid(ValuePath);
        }

        var decoded = DecodeValue(value, valueInfo);
        return decoded.Error is { } mismatch ? mismatch : Result.Success(Result.Success(decoded.ValueOrThrow()));
    }

    /// <summary>
    /// 读取值类型为 Maybe 的结果
    /// </summary>
    /// <remarks>
    /// 成功必须带 <c>value</c>，<c>null</c> 即无值；其余按 <paramref name="valueInfo"/> 读成有值
    /// </remarks>
    /// <param name="json">线上的 JSON 文本</param>
    /// <param name="valueInfo">Maybe 里的值的类型的序列化元数据（源生成）</param>
    /// <returns>外层失败表示编码不合法；外层成功时是读出的结果</returns>
    public static Result<Result<Maybe<T>>> ReadMaybe<T>(string json, JsonTypeInfo<T> valueInfo)
    {
        ArgumentNullException.ThrowIfNull(valueInfo);
        var parsed = Parse(json);
        if (parsed.Error is { } invalid)
        {
            return invalid;
        }

        using var document = parsed.ValueOrThrow();
        var envelope = ReadEnvelope(document.RootElement);
        if (envelope.Error is { } malformed)
        {
            return malformed;
        }

        var read = envelope.ValueOrThrow();
        if (read.Failure is { } failure)
        {
            return Result.Success(Result.Failure<Maybe<T>>(failure));
        }

        if (read.Value is not { } value)
        {
            return Invalid(ValuePath);
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return Result.Success(Result.Success(Maybe.None<T>()));
        }

        var decoded = DecodeValue(value, valueInfo);
        return decoded.Error is { } mismatch ? mismatch : Result.Success(Result.Success(Maybe.Some(decoded.ValueOrThrow())));
    }

    /// <summary>
    /// 读取一个错误
    /// </summary>
    /// <remarks>
    /// 带 <c>messageKey</c> 的读成带键的错误、文案为 <c>message</c>，没有键的读成自由文本错误（<c>Error.Literal</c>）；空的 <c>details</c> 读成没有细节。
    /// <c>message</c> 只有空白时也不合法：.NET 的错误文案不能是空白
    /// </remarks>
    /// <param name="json">线上的 JSON 文本</param>
    /// <returns>外层失败表示编码不合法；外层成功时是读出的错误</returns>
    public static Result<Error> ReadError(string json)
    {
        var parsed = Parse(json);
        if (parsed.Error is { } invalid)
        {
            return Result.Failure<Error>(invalid);
        }

        using var document = parsed.ValueOrThrow();
        return WireErrorReader.Read(document.RootElement, JsonPath.Root);
    }

    /// <summary>
    /// 写出结果的外层
    /// </summary>
    /// <param name="failure">失败时的错误；成功为 <see langword="null"/></param>
    /// <param name="value">成功值的 JSON 文本；为空表示无值成功</param>
    private static string WriteEnvelope(Error? failure, ReadOnlySpan<byte> value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            if (failure is not null)
            {
                writer.WriteBoolean("ok", false);
                writer.WritePropertyName("error");
                WriteErrorObject(writer, failure);
            }
            else
            {
                writer.WriteBoolean("ok", true);
                // 无值成功只写 ok：以前各 SDK 分别写成 {"ok":true} 与 {"ok":true,"value":{}}，互相读不出（ADR-0233）
                if (!value.IsEmpty)
                {
                    writer.WritePropertyName("value");
                    writer.WriteRawValue(value, skipInputValidation: true);
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// 写出一个错误对象，内部错误递归
    /// </summary>
    private static void WriteErrorObject(Utf8JsonWriter writer, Error error)
    {
        writer.WriteStartObject();
        writer.WriteString("code", error.Code);
        // message 总是写出，是发送端已解析的文案；messageKey 让接收端可再翻译，Literal 没有键就不写
        writer.WriteString("message", error.Message);
        if (error.MessageKey is { } key)
        {
            writer.WriteString("messageKey", key);
        }

        // 空细节与没有细节相同，不写出
        if (error.Details is { Count: > 0 } details)
        {
            writer.WriteStartObject("details");
            foreach (var (name, value) in details)
            {
                writer.WritePropertyName(name);
                JsonValues.Write(writer, value);
            }

            writer.WriteEndObject();
        }

        if (error.Inner is { } inner)
        {
            writer.WritePropertyName("inner");
            WriteErrorObject(writer, inner);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 把文本解析成通过共同检查的 JSON 文档
    /// </summary>
    /// <remarks>
    /// 交出的文档由调用方释放
    /// </remarks>
    private static Result<JsonDocument> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        byte[] utf8;
        try
        {
            utf8 = _strictUtf8.GetBytes(json);
        }
        catch (EncoderFallbackException)
        {
            return InvalidJson(JsonPath.Root);
        }

        // 先过共同的读取检查：语法错误一律写 $，重复成员写第一处重复的路径，不论它在 value、details 还是 inner 里（RFC-0001「JSON 文本」，ADR-0236）
        if (JsonText.Check(utf8) is { } problem)
        {
            return InvalidJson(problem.Path);
        }

        // 检查通过之后的文本 JsonDocument 一定读得出：深度同为 64 层，字符串都能取值
        return JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = JsonText.MaxDepth });
    }

    /// <summary>
    /// 读结果的外层，交出对方的失败或成功值的成员
    /// </summary>
    /// <remarks>
    /// <c>ok</c> 必填且是布尔；失败必须带 <c>error</c>、不能带 <c>value</c>；成功不能带 <c>error</c>。成员名区分大小写，未知成员忽略
    /// </remarks>
    private static Result<Envelope> ReadEnvelope(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Invalid(JsonPath.Root);
        }

        if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return Invalid(JsonPath.Member(JsonPath.Root, "ok"));
        }

        var hasValue = root.TryGetProperty("value", out var value);
        var hasError = root.TryGetProperty("error", out var error);
        var errorPath = JsonPath.Member(JsonPath.Root, "error");
        if (ok.ValueKind == JsonValueKind.True)
        {
            return hasError ? Invalid(errorPath) : new Envelope(null, hasValue ? value : null);
        }

        // 失败带 value 不合法，哪怕 error 本身合法
        if (hasValue)
        {
            return Invalid(ValuePath);
        }

        if (!hasError)
        {
            return Invalid(errorPath);
        }

        var failure = WireErrorReader.Read(error, errorPath);
        return failure.Error is { } malformed ? malformed : new Envelope(failure.ValueOrThrow(), null);
    }

    /// <summary>
    /// 按类型读成功值
    /// </summary>
    /// <remarks>
    /// 先按序列化元数据以规范的顺序核对形状，<c>reason</c> 不随类型的声明方式而变（RFC-0011 规则 2）；路径从 <c>$.value</c> 起
    /// </remarks>
    private static Result<T> DecodeValue<T>(JsonElement value, JsonTypeInfo<T> info)
    {
        if (JsonRequestShape.FindProblem(value, info) is { } path)
        {
            return Invalid(InValue(path));
        }

        try
        {
            // 转换器把非 null 的 JSON 读成 null 时同样没有成功值
            return value.Deserialize(info) is { } decoded ? Result.Success(decoded) : Invalid(ValuePath);
        }
        catch (JsonException exception)
        {
            // 只留路径：消息带目标类型的全名（ADR-0124）。读取器的错误（序列化上下文把 MaxDepth 设得更小时）写值本身
            return Invalid(exception.InnerException is JsonException ? ValuePath : InValue(exception.Path ?? JsonPath.Root));
        }
    }

    /// <summary>
    /// 值内的路径换成从结果的根起
    /// </summary>
    private static string InValue(string path) => ValuePath + path[JsonPath.Root.Length..];

    /// <summary>
    /// 编码不合规则的错误
    /// </summary>
    internal static Error Invalid(string reason)
        => Error.Validation(ErrorCode.InvalidArgument, KernelMessages.WireInvalid, MessageArguments.Of(("reason", reason)));

    /// <summary>
    /// 文本不是合法 JSON 的错误
    /// </summary>
    private static Error InvalidJson(string reason)
        => Error.Validation(ErrorCode.InvalidArgument, KernelMessages.WireInvalidJson, MessageArguments.Of(("reason", reason)));

    /// <summary>
    /// 结果外层读出的内容
    /// </summary>
    /// <param name="Failure">失败时对方的错误；成功为 <see langword="null"/></param>
    /// <param name="Value">成功时的 <c>value</c> 成员；没有这个成员为 <see langword="null"/></param>
    private readonly record struct Envelope(Error? Failure, JsonElement? Value);
}
