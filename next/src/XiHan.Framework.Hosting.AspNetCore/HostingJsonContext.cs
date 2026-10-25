// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Hosting.AspNetCore.Health;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// 宿主响应体的源生成序列化上下文：驼峰属性名、忽略 <c>null</c>，保持 AOT 友好。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProblemResponse))]
[JsonSerializable(typeof(HealthResponse))]
internal sealed partial class HostingJsonContext : JsonSerializerContext;
