// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// <see cref="TextsResponse"/> 的源生成 JSON 上下文
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TextsResponse))]
internal sealed partial class TextsJsonContext : JsonSerializerContext;
