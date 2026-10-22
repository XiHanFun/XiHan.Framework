// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Configuration.Conformance;

namespace XiHan.Framework.Configuration.Abstractions.Tests;

/// <summary>
/// 让进程内密钥库跑完整套 <see cref="SecretStoreConformance"/> 用例，同时验证套件本身。
/// </summary>
public sealed class InMemorySecretStoreConformanceTests : SecretStoreConformance
{
    /// <summary>
    /// 进程内夹具。
    /// </summary>
    private sealed class Fixture : SecretStoreFixture
    {
        /// <summary>
        /// 被测密钥库。
        /// </summary>
        private readonly InMemorySecretStore _store = new();

        /// <inheritdoc />
        public override ISecretStore Store => _store;

        /// <inheritdoc />
        public override Task PutAsync(SecretName name, string value, CancellationToken cancellationToken)
        {
            _store.Put(name, value);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    protected override Task<SecretStoreFixture> CreateFixtureAsync() => Task.FromResult<SecretStoreFixture>(new Fixture());
}
