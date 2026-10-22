// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Configuration.Abstractions;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.Configuration.Conformance;

/// <summary>
/// <see cref="ISecretStore"/> 的一致性套件。继承并实现 <see cref="CreateFixtureAsync"/>，全部用例随之执行。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class SecretStoreConformance
{
    /// <summary>
    /// 创建夹具。每个用例调用一次。
    /// </summary>
    protected abstract Task<SecretStoreFixture> CreateFixtureAsync();

    /// <summary>
    /// 用例 1：批量读取逐项返回且顺序一致，重复项各自作答，不存在的项是 NOTFOUND，空集合返回空；值的文本表示被遮蔽。
    /// </summary>
    [Fact]
    public async Task Get_ReturnsPerItemInOrder_AndMissingIsNotFound()
    {
        var fixture = await CreateFixtureAsync().ConfigureAwait(false);
        var prefix = "conformance/" + Guid.NewGuid().ToString("N")[..8];
        SecretName present = prefix + "/db-password";
        SecretName missing = prefix + "/missing";
        await fixture.PutAsync(present, "s3cr3t", CancellationToken.None).ConfigureAwait(false);

        var lookups = await fixture.Store.GetAsync([present, missing, present], CancellationToken.None).ConfigureAwait(false);
        Assert.Equal([present, missing, present], lookups.Select(static l => l.Name));
        Assert.Equal("s3cr3t", lookups[0].Result.ValueOrThrow().Value);
        Assert.Equal(ConfigurationErrors.SecretNotFound, lookups[1].Result.Error?.Code);
        Assert.Equal("s3cr3t", lookups[2].Result.ValueOrThrow().Value);
        Assert.Equal(SecretValue.Redacted, lookups[0].Result.ValueOrThrow().ToString());
        Assert.Empty(await fixture.Store.GetAsync([], CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 2：覆盖后读到新值。
    /// </summary>
    [Fact]
    public async Task Get_AfterOverwrite_ReturnsNewValue()
    {
        var fixture = await CreateFixtureAsync().ConfigureAwait(false);
        SecretName name = "conformance/" + Guid.NewGuid().ToString("N")[..8] + "/token";
        await fixture.PutAsync(name, "one", CancellationToken.None).ConfigureAwait(false);
        await fixture.PutAsync(name, "two", CancellationToken.None).ConfigureAwait(false);
        Assert.Equal("two", (await fixture.Store.GetAsync(name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Value);
    }
}
