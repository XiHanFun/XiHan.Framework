// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Values;

/// <summary>
/// 求值结果与 JSON 值之间的换算。
/// </summary>
/// <remarks>
/// 求值得到的无值写回变量、输出与 JSON 时就是 JSON 的 null（ADR-0059）。
/// </remarks>
internal static class JsonMaybe
{
    /// <summary>
    /// 无值换成 <see langword="null"/>。
    /// </summary>
    public static JsonNode? OrNull(this Maybe<JsonNode> value) => value.TryGetValue(out var node) ? node : null;
}
