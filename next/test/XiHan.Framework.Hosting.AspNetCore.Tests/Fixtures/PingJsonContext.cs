// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Hosting.AspNetCore.Tests.Fixtures;

/// <summary>
/// 问候操作请求与响应的源生成序列化上下文。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PingRequest))]
[JsonSerializable(typeof(PingResponse))]
internal sealed partial class PingJsonContext : JsonSerializerContext;
