// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace KubernetesApp;

/// <summary>
/// 端点响应的序列化上下文（AOT 友好，不用反射）。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BannerView))]
[JsonSerializable(typeof(ConfigView))]
[JsonSerializable(typeof(LeaderView))]
internal sealed partial class SampleJsonContext : JsonSerializerContext;
