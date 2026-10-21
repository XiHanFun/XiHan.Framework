// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Conformance;

namespace XiHan.Framework.Caching.Redis.Tests;

public sealed class RedisCacheStoreConformanceTests : CacheStoreConformance
{
    // Redis 的过期精度为毫秒，但网络往返让观测滞后
    protected override TimeSpan ExpirationGrace => TimeSpan.FromMilliseconds(500);

    protected override ICacheStore CreateStore()
    {
        var configuration = RedisTestServer.RequireConfiguration();
        return new RedisCacheStore(RedisTestServer.Connect(), RedisTestServer.Options(configuration));
    }
}
