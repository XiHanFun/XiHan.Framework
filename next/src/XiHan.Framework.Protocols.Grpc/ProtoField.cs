// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc;

/// <summary>
/// 消息的一个字段：字段号按 Schema 里属性的声明顺序从 1 起分配，JSON 名就是 Schema 里的属性名。
/// </summary>
/// <param name="Number">字段号。</param>
/// <param name="JsonName">JSON 属性名。</param>
/// <param name="Value">值类型（重复字段是元素类型，映射字段是映射值类型，键固定为字符串）。</param>
/// <param name="Cardinality">基数。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ProtoField(int Number, string JsonName, ProtoValueType Value, ProtoCardinality Cardinality);
