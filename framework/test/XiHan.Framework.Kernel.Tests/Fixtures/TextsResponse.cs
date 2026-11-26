// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 字符串出现在值、键、数组元素与节点里的响应
/// </summary>
/// <param name="Text">值里的字符串</param>
/// <param name="Tags">键与值都是字符串的字典</param>
/// <param name="Items">字符串数组</param>
/// <param name="Node">成员名与值都是字符串的 JSON 节点</param>
/// <param name="Bytes">由转换器以 UTF-8 字节写出的字符串</param>
internal sealed record TextsResponse(string Text, IReadOnlyDictionary<string, string> Tags, IReadOnlyList<string> Items, JsonObject Node, Utf8Text Bytes);
