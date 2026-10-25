// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc.Proto;

/// <summary>
/// 字段值（或数组元素、映射值）的类型：种类，以及 <see cref="ProtoKind.Message"/> 时的消息。
/// </summary>
/// <param name="Kind">种类。</param>
/// <param name="Message">嵌套消息；只有 <see cref="ProtoKind.Message"/> 时有值。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ProtoValueType(ProtoKind Kind, ProtoMessage? Message)
{
    /// <summary>
    /// 标量类型。
    /// </summary>
    public static ProtoValueType Scalar(ProtoKind kind)
    {
        if (kind == ProtoKind.Message)
        {
            throw new ArgumentException("A message value type needs the message; use Of(ProtoMessage).", nameof(kind));
        }

        return new ProtoValueType(kind, null);
    }

    /// <summary>
    /// 消息类型。
    /// </summary>
    public static ProtoValueType Of(ProtoMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new ProtoValueType(ProtoKind.Message, message);
    }

    /// <summary>
    /// 是否可打包（proto3 的 repeated 数值与布尔默认打包）。
    /// </summary>
    public bool IsPackable => Kind is ProtoKind.Bool or ProtoKind.Varint32 or ProtoKind.Varint64 or ProtoKind.Fixed32 or ProtoKind.Fixed64;
}
