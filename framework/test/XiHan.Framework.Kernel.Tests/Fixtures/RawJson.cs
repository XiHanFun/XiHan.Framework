// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 原样嵌进响应的 JSON 文本
/// </summary>
/// <remarks>
/// 经 <see cref="RawJsonConverter"/> 以 <c>WriteRawValue</c> 写出：只核对语法，不核对深度、重复成员与转义
/// </remarks>
/// <param name="Text">JSON 文本</param>
[JsonConverter(typeof(RawJsonConverter))]
internal sealed record RawJson(string Text);
