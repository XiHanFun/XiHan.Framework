// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Google.Protobuf;

namespace XiHan.Framework.Plugins.Protocol.Mapping;

/// <summary>
/// 协议消息的 proto3 JSON 映射（Wasm 载体的编码，<c>spec/plugin/wasm.md</c> §2）：字段名 lowerCamelCase、枚举为名字、缺省值省略、未知字段忽略。
/// </summary>
internal static class PluginJsonCodec
{
    /// <summary>
    /// 写出：缺省值省略。
    /// </summary>
    private static readonly JsonFormatter _formatter = new(JsonFormatter.Settings.Default);

    /// <summary>
    /// 读取：忽略未知字段，便于插件用更新的消息定义。
    /// </summary>
    private static readonly JsonParser _parser = new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

    /// <summary>
    /// 消息 → UTF-8 JSON。
    /// </summary>
    public static byte[] Encode(IMessage message) => Encoding.UTF8.GetBytes(_formatter.Format(message));

    /// <summary>
    /// UTF-8 JSON → 消息；不是合法 JSON 或与消息不符返回空。
    /// </summary>
    public static TMessage? Decode<TMessage>(ReadOnlySpan<byte> utf8)
        where TMessage : IMessage<TMessage>, new()
    {
        try
        {
            return _parser.Parse<TMessage>(Encoding.UTF8.GetString(utf8));
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
