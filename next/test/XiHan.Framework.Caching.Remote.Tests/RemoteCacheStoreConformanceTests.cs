// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Conformance;

namespace XiHan.Framework.Caching.Remote.Tests;

/// <summary>
/// 让远程适配器 + Go 写的 cache-go 插件跑完整套 <see cref="CacheStoreConformance"/> 用例（P4-07）；本机没有 go 时跳过。
/// </summary>
public sealed class RemoteCacheStoreConformanceTests(RemoteHostFixture fixture) : CacheStoreConformance, IClassFixture<RemoteHostFixture>
{
    /// <summary>
    /// 放宽到 500 毫秒：过期在插件进程里判定，gRPC 往返让观测滞后。
    /// </summary>
    protected override TimeSpan ExpirationGrace => TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 共用宿主里的远程存储，按用例加随机键前缀。
    /// </summary>
    protected override ICacheStore CreateStore() => new PrefixedCacheStore(fixture.Store);
}
