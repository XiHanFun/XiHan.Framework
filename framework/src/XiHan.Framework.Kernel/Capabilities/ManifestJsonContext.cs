// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <see cref="ManifestDocument"/> 的源生成序列化上下文
/// </summary>
/// <remarks>
/// camelCase 属性名，写出时省略 null 并缩进。读取按 Schema 严格执行（RFC-0002 解析规则 2，ADR-0235）：成员名区分大小写、未知成员与重复成员拒绝。内核零反射，清单序列化只能走这里
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(ManifestDocument))]
internal sealed partial class ManifestJsonContext : JsonSerializerContext;
