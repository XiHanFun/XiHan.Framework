// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 以 UTF-8 字节交给写出器的字符串
/// </summary>
/// <remarks>
/// 经 <see cref="Utf8TextConverter"/> 写成 JSON 字符串，字节可以不是合法的 UTF-8
/// </remarks>
/// <param name="Value">字符串的 UTF-8 字节</param>
[JsonConverter(typeof(Utf8TextConverter))]
internal sealed record Utf8Text(byte[] Value);
