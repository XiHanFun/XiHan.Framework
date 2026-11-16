// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <see cref="ManifestDocument"/> 的源生成序列化上下文
/// </summary>
/// <remarks>
/// camelCase 属性名，只用于读取：形状已由 <see cref="ManifestShape"/> 按 Schema 核对过（RFC-0002 解析规则 2，ADR-0236），这里仍按严格选项读，未知成员与重复成员拒绝。写出由 <see cref="ManifestReader.Write"/> 按规范排版手写。内核零反射，清单反序列化只能走这里
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(ManifestDocument))]
internal sealed partial class ManifestJsonContext : JsonSerializerContext;
