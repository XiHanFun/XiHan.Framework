// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Configuration.Abstractions;
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.Configuration.Conformance.Remote;

/// <summary>
/// <see cref="IRemoteConfigurationClient"/> 的一致性套件。
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateFixtureAsync"/>，全部用例随之执行。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class RemoteConfigurationClientConformance
{
    /// <summary>
    /// 未变化的取值允许阻塞的时间（长轮询实现在此之内返回无值）。
    /// </summary>
    protected virtual TimeSpan UnchangedWait => TimeSpan.FromSeconds(3);

    /// <summary>
    /// 创建夹具。
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次。
    /// </remarks>
    protected abstract Task<RemoteConfigurationFixture> CreateFixtureAsync();

    /// <summary>
    /// 用例 1：发布后取到整份文档与非空版本。
    /// </summary>
    /// <remarks>
    /// 再次以该版本取值返回无值。
    /// </remarks>
    [Fact]
    public async Task Fetch_ReturnsPublishedDocument_AndUnchangedForKnownVersion()
    {
        var fixture = await CreateFixtureAsync().ConfigureAwait(false);
        var ns = Namespace();
        await fixture.PublishAsync(ns, new Dictionary<string, string?> { ["Greeting"] = "hello", ["Nested:Count"] = "2", ["Empty"] = null }, CancellationToken.None).ConfigureAwait(false);

        var document = (await fixture.Client.FetchAsync(ns, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
        Assert.False(string.IsNullOrWhiteSpace(document.Version));
        Assert.Equal("hello", document.Values["Greeting"]);
        Assert.Equal("2", document.Values["Nested:Count"]);
        Assert.True(document.Values.ContainsKey("Empty"));
        Assert.Null(document.Values["Empty"]);

        using var wait = new CancellationTokenSource(UnchangedWait);
        var unchanged = (await fixture.Client.FetchAsync(ns, document.Version, wait.Token).ConfigureAwait(false)).ValueOrThrow();
        Assert.False(unchanged.HasValue);
    }

    /// <summary>
    /// 用例 2：再次发布后以旧版本取值得到新文档与不同的版本。
    /// </summary>
    /// <remarks>
    /// 以空版本取值总是返回文档。
    /// </remarks>
    [Fact]
    public async Task Fetch_AfterRepublish_ReturnsNewVersion()
    {
        var fixture = await CreateFixtureAsync().ConfigureAwait(false);
        var ns = Namespace();
        await fixture.PublishAsync(ns, new Dictionary<string, string?> { ["Greeting"] = "hello" }, CancellationToken.None).ConfigureAwait(false);
        var first = (await fixture.Client.FetchAsync(ns, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();

        await fixture.PublishAsync(ns, new Dictionary<string, string?> { ["Greeting"] = "bye" }, CancellationToken.None).ConfigureAwait(false);
        var second = (await fixture.Client.FetchAsync(ns, first.Version, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
        Assert.NotEqual(first.Version, second.Version);
        Assert.Equal("bye", second.Values["Greeting"]);
        Assert.Equal(second.Version, (await fixture.Client.FetchAsync(ns, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Version);
    }

    /// <summary>
    /// 用例 3：不存在的命名空间是 NOTFOUND。
    /// </summary>
    [Fact]
    public async Task Fetch_UnknownNamespace_IsNotFound()
    {
        var fixture = await CreateFixtureAsync().ConfigureAwait(false);
        var fetched = await fixture.Client.FetchAsync(Namespace(), null, CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(ConfigurationErrors.NamespaceNotFound, fetched.Error?.Code);
    }

    /// <summary>
    /// 套件内唯一的命名空间。
    /// </summary>
    private static string Namespace() => "conformance." + Guid.NewGuid().ToString("N")[..8];
}
