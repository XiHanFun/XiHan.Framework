// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// 写出与转交的严格写出
/// </summary>
/// <remarks>
/// RFC-0001「写出与转交」：System.Text.Json 的写出器把孤立的代理项与非法的 UTF-8 换成 U+FFFD 照写，得到的仍是合法的文本，这里把它当作写不出。
/// 内置的编码器总把换出来的写成 <c>\uFFFD</c>，文本里没有这个转义就没有换过的字符；有，就换不转义非 ASCII 的内置编码器再写一遍分辨：本来就是 U+FFFD 的字符原样写成 <c>EF BF BD</c>，换出来的仍是 <c>\uFFFD</c>，这一遍只看不交（ADR-0318「修订」）。
/// 类型化的响应编成载荷时用它（ADR-0318、ADR-0319、ADR-0320）
/// </remarks>
internal static class StrictJson
{
    /// <summary>
    /// 写不出时抛出的异常
    /// </summary>
    /// <param name="inner">System.Text.Json 自己抛出的异常；没有为 <see langword="null"/></param>
    public static ArgumentException Unwritable(Exception? inner)
        => new("The value cannot be written as JSON text: a string has a lone surrogate or invalid UTF-8, the text nests deeper than 64 levels, or an object has a duplicate member.", inner);

    /// <summary>
    /// 按序列化元数据写出类型化的值
    /// </summary>
    /// <remarks>
    /// 交出的字节与 <see cref="JsonSerializer.SerializeToUtf8Bytes(object?, JsonTypeInfo)"/> 写出的相同。写不出只抛 <see cref="ArgumentException"/>：字符串被写出器换成了 U+FFFD，
    /// <paramref name="checkText"/> 为真时整段文本没过 <see cref="JsonText.FindProblem"/>，或者 System.Text.Json 自己抛出 <see cref="JsonException"/>（超过写出深度等，放进 <see cref="Exception.InnerException"/>，ADR-0320）
    /// </remarks>
    /// <param name="value">要写出的值</param>
    /// <param name="info">写出用的序列化元数据</param>
    /// <param name="checkText">是否核对整段文本；外层另有核对时不必</param>
    public static byte[] Serialize(object? value, JsonTypeInfo info, bool checkText)
    {
        byte[] payload;
        try
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(value, info);
        }
        catch (JsonException exception)
        {
            throw Unwritable(exception);
        }

        return (checkText && JsonText.FindProblem(payload).HasValue) || HasReplacedCharacter(value, info, payload) ? throw Unwritable(null) : payload;
    }

    /// <summary>
    /// 按序列化元数据写出时是否有字符被换成了 U+FFFD
    /// </summary>
    /// <param name="value">写出的值</param>
    /// <param name="info">写出用的序列化元数据</param>
    /// <param name="payload">已经写出的文本</param>
    public static bool HasReplacedCharacter(object? value, JsonTypeInfo info, byte[] payload)
    {
        // 自定义的编码器不在此列（实测有的不报需要转义，写出器就把孤立的代理项连同后面的字符截掉、不留痕迹），总要再写一遍
        var encoder = info.Options.Encoder;
        if (!HasEscapedReplacement(payload) && (encoder is null || encoder.GetType() == JavaScriptEncoder.Default.GetType()))
        {
            return false;
        }

        var check = new ArrayBufferWriter<byte>(payload.Length);
        using (var writer = new Utf8JsonWriter(check, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = info.Options.MaxDepth, SkipValidation = true }))
        {
            JsonSerializer.Serialize(writer, value, info);
        }

        return HasEscapedReplacement(check.WrittenSpan);
    }

    /// <summary>
    /// 文本里有没有转义的 U+FFFD
    /// </summary>
    private static bool HasEscapedReplacement(ReadOnlySpan<byte> text) => text.IndexOf("\\uFFFD"u8) >= 0;
}
