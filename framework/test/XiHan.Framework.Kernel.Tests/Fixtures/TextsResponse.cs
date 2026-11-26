// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 字符串出现在值、字典的键与数组元素里的响应
/// </summary>
/// <param name="Text">值里的字符串</param>
/// <param name="Tags">键与值都是字符串的字典</param>
/// <param name="Items">字符串数组</param>
internal sealed record TextsResponse(string Text, IReadOnlyDictionary<string, string> Tags, IReadOnlyList<string> Items);
