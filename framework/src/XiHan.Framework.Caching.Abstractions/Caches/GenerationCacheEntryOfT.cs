// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 代数缓存条目的存储形状：值与写入时记下的区代数、范围代数
/// </summary>
/// <remarks>
/// 读取时两个代数都等于当前代数才算命中。跨进程缓存要序列化它：用 <see cref="GenerationCache{T}"/> 的组件在自己的源生成上下文里登记 <c>GenerationCacheEntry&lt;T&gt;</c> 与 <see cref="string"/>（代数本身），再经 <see cref="Serialization.CacheSerializerRegistration.AddTypeInfo"/> 交给缓存序列化器。
/// <typeparamref name="T"/> 的序列化形状变了要换区名：滚动升级时新旧实例共用缓存后端，换了区名才不会读到对方写的条目而反序列化失败
/// </remarks>
/// <param name="Region">写入时的区代数</param>
/// <param name="Scope">写入时的范围代数</param>
/// <param name="Value">值</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record GenerationCacheEntry<T>(string Region, string Scope, T Value)
    where T : notnull;
