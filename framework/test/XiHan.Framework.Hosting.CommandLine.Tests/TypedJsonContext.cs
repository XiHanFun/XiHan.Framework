// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 带请求 Schema 的操作的源生成序列化上下文
/// </summary>
/// <remarks>
/// 只有 camelCase，不允许从字符串读数字：字段选项必须按 Schema 写成 JSON 数字才读得进来
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TypedRequest))]
internal sealed partial class TypedJsonContext : JsonSerializerContext;
