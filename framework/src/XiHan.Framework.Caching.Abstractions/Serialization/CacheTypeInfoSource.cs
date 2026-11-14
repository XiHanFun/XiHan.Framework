// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Serialization;

/// <summary>
/// 组件放进缓存的类型的序列化元数据
/// </summary>
/// <remarks>
/// 经 <see cref="CacheSerializerRegistration.AddTypeInfo"/> 登记，容器里每个组件一项。应用登记 <see cref="System.Text.Json.JsonSerializerOptions"/> 时由 <see cref="CacheSerializerRegistration.TryAddJsonSerializer"/> 并进解析器；应用自己登记 <see cref="ICacheSerializer"/> 时要自行处理这些类型（ADR-0229）
/// </remarks>
/// <param name="Resolver">组件的源生成上下文或其组合</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CacheTypeInfoSource(IJsonTypeInfoResolver Resolver);
