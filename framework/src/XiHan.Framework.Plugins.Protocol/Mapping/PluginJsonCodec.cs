// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Google.Protobuf;

namespace XiHan.Framework.Plugins.Protocol.Mapping;

/// <summary>
/// 协议消息的 proto3 JSON 映射（Wasm 载体的编码，<c>spec/plugin/wasm.md</c> §2）
/// </summary>
/// <remarks>
/// 字段名 lowerCamelCase、枚举为名字、缺省值省略、未知字段忽略。字符串与 UTF-8 字节之间按严格的 UTF-8 转换，不把不合法的输入换成 U+FFFD（RFC-0001「JSON 文本」，ADR-0312）
/// </remarks>
internal static class PluginJsonCodec
{
    /// <summary>
    /// 严格的 UTF-8 编码
    /// </summary>
    /// <remarks>
    /// 解码遇到非法的字节序列、编码遇到孤立的代理项都抛出，不悄悄换成 U+FFFD：换出来的仍是一条合法的消息，读的一方看不出客体写错了（ADR-0312）
    /// </remarks>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// 写出：缺省值省略
    /// </summary>
    private static readonly JsonFormatter _formatter = new(JsonFormatter.Settings.Default);

    /// <summary>
    /// 读取：忽略未知字段，便于插件用更新的消息定义
    /// </summary>
    private static readonly JsonParser _parser = new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

    /// <summary>
    /// 消息 → UTF-8 JSON
    /// </summary>
    /// <remarks>
    /// 字段里有孤立的代理项时写出器即抛出 <see cref="ArgumentException"/>，写给客体的字节因此总是合法的 UTF-8
    /// </remarks>
    public static byte[] Encode(IMessage message) => _strictUtf8.GetBytes(_formatter.Format(message));

    /// <summary>
    /// UTF-8 JSON → 消息
    /// </summary>
    /// <remarks>
    /// 不是合法 JSON、与消息不符或有非法的 UTF-8 字节序列返回空
    /// </remarks>
    public static TMessage? Decode<TMessage>(ReadOnlySpan<byte> utf8)
        where TMessage : IMessage<TMessage>, new()
    {
        try
        {
            // 先严格解码：非法的字节序列与不是 JSON 一样不是一条消息，不换成 U+FFFD 再解析（wasm.md §2）
            return _parser.Parse<TMessage>(_strictUtf8.GetString(utf8));
        }
        catch (DecoderFallbackException)
        {
            return default;
        }
        catch (InvalidProtocolBufferException)
        {
            return default;
        }
        catch (InvalidJsonException)
        {
            return default;
        }
    }
}
