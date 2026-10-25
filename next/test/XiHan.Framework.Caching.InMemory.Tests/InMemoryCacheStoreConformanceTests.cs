// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.Conformance;

namespace XiHan.Framework.Caching.InMemory.Tests;

/// <summary>
/// 让 <see cref="InMemoryCacheStore"/> 跑完整套 <see cref="CacheStoreConformance"/> 用例。
/// </summary>
public sealed class InMemoryCacheStoreConformanceTests : CacheStoreConformance
{
    /// <summary>
    /// 用默认选项与系统时钟创建全新的内存存储。
    /// </summary>
    protected override ICacheStore CreateStore() => new InMemoryCacheStore(new InMemoryCachingOptions(), TimeProvider.System);
}
