// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 载荷向量的请求类型与任意 JSON 的源生成序列化上下文
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(VectorOrder))]
[JsonSerializable(typeof(VectorProfile))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class VectorJsonContext : JsonSerializerContext;
