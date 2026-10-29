// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Configuration.Conformance.Remote;

namespace XiHan.Framework.Configuration.Abstractions.Tests;

/// <summary>
/// 让进程内远程配置跑完整套 <see cref="RemoteConfigurationClientConformance"/> 用例。
/// </summary>
/// <remarks>
/// 同时验证套件本身。
/// </remarks>
public sealed class InMemoryRemoteConfigurationConformanceTests : RemoteConfigurationClientConformance
{
    /// <summary>
    /// 进程内夹具。
    /// </summary>
    private sealed class Fixture : RemoteConfigurationFixture
    {
        /// <summary>
        /// 被测客户端。
        /// </summary>
        private readonly InMemoryRemoteConfiguration _client = new();

        /// <inheritdoc />
        public override IRemoteConfigurationClient Client => _client;

        /// <inheritdoc />
        public override Task PublishAsync(string namespaceName, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken)
        {
            _client.Publish(namespaceName, values);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    protected override Task<RemoteConfigurationFixture> CreateFixtureAsync() => Task.FromResult<RemoteConfigurationFixture>(new Fixture());
}
