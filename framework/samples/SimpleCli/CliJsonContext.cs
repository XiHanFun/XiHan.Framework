// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace SimpleCli;

/// <summary>
/// 源生成的序列化上下文
/// </summary>
/// <remarks>
/// camelCase 属性名、不区分大小写。不需要从字符串读数字：命令行宿主按操作的请求 Schema 把字段选项写成对应类型
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GreetRequest))]
[JsonSerializable(typeof(GreetResponse))]
public sealed partial class CliJsonContext : JsonSerializerContext;
