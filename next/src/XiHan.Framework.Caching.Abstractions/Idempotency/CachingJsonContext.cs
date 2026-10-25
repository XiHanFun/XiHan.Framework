// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Caching.Abstractions.Idempotency;

/// <summary>
/// <see cref="IdempotencyRecord"/> 的源生成 JSON 上下文。
/// </summary>
[JsonSerializable(typeof(IdempotencyRecord))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class CachingJsonContext : JsonSerializerContext;
