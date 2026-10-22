// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Scheduling.Kubernetes;

/// <summary>
/// Lease 与 Status 的序列化上下文：camelCase，空值不写出。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LeaseDocument))]
[JsonSerializable(typeof(StatusDocument))]
internal sealed partial class KubernetesJsonContext : JsonSerializerContext;
