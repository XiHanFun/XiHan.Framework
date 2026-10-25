// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Configuration.Conformance.Remote;

namespace XiHan.Framework.Configuration.Nacos.Tests;

/// <summary>
/// 让 <see cref="NacosConfigurationClient"/> 对真实 Nacos 跑完整套 <see cref="RemoteConfigurationClientConformance"/> 用例；未设置 <c>XIHAN_TEST_NACOS</c> 时跳过。
/// </summary>
public sealed class NacosRemoteConfigurationConformanceTests : RemoteConfigurationClientConformance
{
    /// <summary>
    /// 夹具：客户端指向测试服务，发布经 Nacos 的发布接口。
    /// </summary>
    private sealed class Fixture : RemoteConfigurationFixture
    {
        /// <summary>
        /// 被测客户端。
        /// </summary>
        private readonly NacosConfigurationClient _client = NacosConfigurationClient.Create(NacosTestServer.Options());

        /// <inheritdoc />
        public override IRemoteConfigurationClient Client => _client;

        /// <inheritdoc />
        public override Task PublishAsync(string namespaceName, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken)
            => NacosTestServer.PublishAsync(namespaceName, values, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task<RemoteConfigurationFixture> CreateFixtureAsync()
    {
        _ = NacosTestServer.RequireServiceUrl();
        return Task.FromResult<RemoteConfigurationFixture>(new Fixture());
    }
}
