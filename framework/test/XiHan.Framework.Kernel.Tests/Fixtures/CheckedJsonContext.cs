// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// <see cref="CheckedResponse"/> 的源生成 JSON 上下文
/// </summary>
/// <remarks>
/// 成员名与字典的键都按小驼峰改写：<c>"A"</c> 与 <c>"a"</c> 两个键写出来同名
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CheckedResponse))]
internal sealed partial class CheckedJsonContext : JsonSerializerContext;
