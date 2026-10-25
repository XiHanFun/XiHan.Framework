// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc.Proto;

/// <summary>
/// 字段的基数。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum ProtoCardinality
{
    /// <summary>单值，隐式存在（proto3 默认值不上线）。</summary>
    Singular = 0,

    /// <summary>单值，显式存在（<c>optional</c>）；JSON 侧可空。</summary>
    Optional = 1,

    /// <summary>重复（<c>repeated</c>）；JSON 侧为数组。</summary>
    Repeated = 2,

    /// <summary>字符串键的映射（<c>map&lt;string, T&gt;</c>）；JSON 侧为对象。</summary>
    Map = 3,
}
