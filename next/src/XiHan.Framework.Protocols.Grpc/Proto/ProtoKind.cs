// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc.Proto;

/// <summary>
/// 字段值在 protobuf 上的种类。JSON Schema 的映射见 <see cref="ProtoSchema"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum ProtoKind
{
    /// <summary>
    /// UTF-8 文本（<c>string</c>，长度前缀）。
    /// </summary>
    Text = 0,

    /// <summary>
    /// 字节串（<c>bytes</c>）；JSON 侧为 base64 字符串。
    /// </summary>
    Bytes = 1,

    /// <summary>
    /// 布尔（<c>bool</c>，varint）。
    /// </summary>
    Bool = 2,

    /// <summary>
    /// 32 位整数（<c>int32</c>，varint）。
    /// </summary>
    Varint32 = 3,

    /// <summary>
    /// 64 位整数（<c>int64</c>，varint）。
    /// </summary>
    Varint64 = 4,

    /// <summary>
    /// 单精度浮点（<c>float</c>，fixed32）。
    /// </summary>
    Fixed32 = 5,

    /// <summary>
    /// 双精度浮点（<c>double</c>，fixed64）。
    /// </summary>
    Fixed64 = 6,

    /// <summary>
    /// 任意精度数字：线上为十进制文本（<c>string</c>），JSON 侧为数字；用于 decimal 与没有 32 / 64 位格式的整数。
    /// </summary>
    TextNumber = 7,

    /// <summary>
    /// 任意 JSON：线上为 JSON 文本（<c>string</c>）；用于没有约束的 Schema。
    /// </summary>
    Json = 8,

    /// <summary>
    /// 嵌套消息（长度前缀）。
    /// </summary>
    Message = 9,
}
