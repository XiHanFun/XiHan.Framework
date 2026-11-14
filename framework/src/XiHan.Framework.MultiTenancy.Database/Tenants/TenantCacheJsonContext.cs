// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Caching.Abstractions.Caches;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 租户目录缓存值的源生成序列化上下文
/// </summary>
/// <remarks>
/// 经 <c>CacheSerializerRegistration.AddTypeInfo</c> 交给跨进程缓存的序列化器，应用的序列化上下文不必认识这些类型；代数是 <see cref="string"/>（ADR-0229）
/// </remarks>
[JsonSerializable(typeof(GenerationCacheEntry<TenantCacheValue>))]
[JsonSerializable(typeof(string))]
internal sealed partial class TenantCacheJsonContext : JsonSerializerContext;
