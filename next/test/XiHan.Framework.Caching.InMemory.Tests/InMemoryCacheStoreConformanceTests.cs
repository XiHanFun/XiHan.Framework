// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Conformance;

namespace XiHan.Framework.Caching.InMemory.Tests;

public sealed class InMemoryCacheStoreConformanceTests : CacheStoreConformance
{
    protected override ICacheStore CreateStore() => new InMemoryCacheStore(new InMemoryCachingOptions(), TimeProvider.System);
}
