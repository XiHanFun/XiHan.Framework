// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 请求与响应的源生成序列化上下文：camelCase，允许从字符串读数字（命令行选项都是字符串）。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(EchoRequest))]
[JsonSerializable(typeof(EchoReply))]
internal sealed partial class EchoJsonContext : JsonSerializerContext;
