// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Tests;

/// <summary>
/// 远程配置源：首次加载、前缀、轮询重载、失败语义、子键枚举。
/// </summary>
public sealed class RemoteConfigurationSourceTests
{
    [Fact]
    public async Task Load_MountsTheDocument_AndReloadsWhenTheVersionChanges()
    {
        var remote = new InMemoryRemoteConfiguration();
        remote.Publish("orders", new Dictionary<string, string?> { ["Greeting"] = "hello", ["Items:0"] = "a", ["Items:1"] = "b", ["Items:10"] = "c" });
        var root = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Orders:Local"] = "yes", ["Orders:Items:2"] = "local" })
            .AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("orders") { Prefix = "Orders", PollInterval = TimeSpan.FromMilliseconds(30) })
            .Build();
        using var disposable = root as IDisposable;

        Assert.Equal("hello", root["Orders:Greeting"]);
        Assert.Equal("yes", root["Orders:Local"]);
        // 子键按配置键顺序：数字段按数值排序，并与其他 Provider 的键合并
        Assert.Equal(["0", "1", "2", "10"], root.GetSection("Orders:Items").GetChildren().Select(static c => c.Key));
        Assert.Equal(["Greeting", "Items", "Local"], root.GetSection("Orders").GetChildren().Select(static c => c.Key));

        var reloaded = new TaskCompletionSource();
        ChangeToken.OnChange(root.GetReloadToken, () => reloaded.TrySetResult());
        remote.Publish("orders", new Dictionary<string, string?> { ["Greeting"] = "bye" });
        await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("bye", root["Orders:Greeting"]);
        Assert.Null(root["Orders:Items:0"]);
        Assert.True(remote.Fetches >= 2);
    }

    [Fact]
    public async Task Load_FailureSemantics_RequiredThrows_OptionalIsEmpty_PollFailuresKeepLastGood()
    {
        var remote = new InMemoryRemoteConfiguration();
        var errors = new List<Error>();
        var required = new ConfigurationBuilder().AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("missing") { PollInterval = null, OnError = errors.Add });
        var thrown = Assert.Throws<FrameworkException>(() => required.Build());
        Assert.Equal(ConfigurationErrors.NamespaceNotFound, thrown.Code);
        Assert.Equal(ConfigurationErrors.NamespaceNotFound, Assert.Single(errors).Code);

        var optional = new ConfigurationBuilder().AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("missing") { PollInterval = null, Optional = true }).Build();
        Assert.Empty(optional.AsEnumerable());

        remote.Publish("orders", new Dictionary<string, string?> { ["Greeting"] = "hello" });
        errors.Clear();
        var root = new ConfigurationBuilder().AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("orders") { PollInterval = TimeSpan.FromMilliseconds(20), OnError = errors.Add }).Build();
        using var disposable = root as IDisposable;
        remote.Fail = Error.Unavailable(ConfigurationErrors.RemoteUnavailable, ConfigurationMessages.RemoteUnavailable, MessageArguments.Of(("namespace", "orders"), ("reason", "down")));
        await WaitUntilAsync(() => errors.Count >= 2);
        Assert.Equal("hello", root["Greeting"]);

        // 抛出的异常同样只报告并保留数据
        remote.Fail = null;
        remote.Throw = true;
        var before = errors.Count;
        await WaitUntilAsync(() => errors.Count > before);
        Assert.Equal(ConfigurationErrors.RemoteUnavailable, errors[^1].Code);
        Assert.Equal("hello", root["Greeting"]);

        // 超时按不可达处理
        var slow = new SlowClient();
        var timeoutErrors = new List<Error>();
        var timedOut = Assert.Throws<FrameworkException>(() => new ConfigurationBuilder().AddXiHanRemoteConfiguration(slow, new RemoteConfigurationOptions("slow") { LoadTimeout = TimeSpan.FromMilliseconds(50), PollInterval = null, OnError = timeoutErrors.Add }).Build());
        Assert.Equal(ConfigurationErrors.RemoteUnavailable, timedOut.Code);
        Assert.Equal("load timeout", Assert.Single(timeoutErrors).Details?["reason"]);
    }

    [Fact]
    public void Options_AndDocument_Validate()
    {
        Assert.Throws<ArgumentException>(static () => new RemoteConfigurationOptions(" "));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new RemoteConfigurationOptions("a") { PollInterval = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new RemoteConfigurationOptions("a") { LoadTimeout = TimeSpan.Zero });
        Assert.Null(new RemoteConfigurationOptions("a") { PollInterval = null }.PollInterval);
        Assert.Throws<ArgumentException>(static () => new ConfigurationDocument("", new Dictionary<string, string?>()));
        Assert.Throws<ArgumentException>(static () => new ConfigurationDocument("1", new Dictionary<string, string?> { [""] = "x" }));
        Assert.Equal("version 1, 0 keys", ConfigurationDocument.Empty("1").ToString());
    }

    /// <summary>
    /// 永远不返回的客户端。
    /// </summary>
    private sealed class SlowClient : IRemoteConfigurationClient
    {
        /// <inheritdoc />
        public async ValueTask<Result<Maybe<ConfigurationDocument>>> FetchAsync(string namespaceName, string? knownVersion, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return default(Maybe<ConfigurationDocument>);
        }
    }

    /// <summary>
    /// 轮询直到条件成立，最多 10 秒。
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not become true in time.");
            }

            await Task.Delay(20);
        }
    }
}
