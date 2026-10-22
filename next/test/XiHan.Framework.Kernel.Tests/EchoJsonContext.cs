// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests;

/// <summary>
/// 回声操作请求与响应的源生成序列化上下文。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EchoRequest))]
[JsonSerializable(typeof(EchoResponse))]
internal sealed partial class EchoJsonContext : JsonSerializerContext;
