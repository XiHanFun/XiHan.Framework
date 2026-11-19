// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 枚举按缺省转换器读整数的源生成序列化上下文
/// </summary>
/// <remarks>
/// 类型上标了转换器的 <see cref="ShapeMode"/> 仍读成员名
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ShapeRequest))]
internal sealed partial class IntegerEnumJsonContext : JsonSerializerContext;
