// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.Conformance;

namespace XiHan.Framework.Caching.Redis.Tests;

/// <summary>
/// 让 <see cref="RedisCacheStore"/> 跑完整套 <see cref="CacheStoreConformance"/> 用例。
/// </summary>
/// <remarks>
/// 没有真实 Redis 时跳过。
/// </remarks>
public sealed class RedisCacheStoreConformanceTests : CacheStoreConformance
{
    /// <summary>
    /// 放宽到 500 毫秒：Redis 的过期精度是毫秒，但网络往返让观测滞后。
    /// </summary>
    protected override TimeSpan ExpirationGrace => TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 用共享连接与随机键前缀创建存储，使各用例互不干扰。
    /// </summary>
    protected override ICacheStore CreateStore()
    {
        var configuration = RedisTestServer.RequireConfiguration();
        return new RedisCacheStore(RedisTestServer.Connect(), RedisTestServer.Options(configuration));
    }
}
