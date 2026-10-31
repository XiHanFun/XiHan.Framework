// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Tests;

/// <summary>
/// 远程配置源：首次加载、前缀、轮询重载、失败语义、回调异常、子键枚举。
/// </summary>
public sealed class RemoteConfigurationSourceTests
{
    [Fact]
    public async Task Load_MountsTheDocument_AndReloadsWhenTheVersionChanges()
    {
        var remote = new InMemoryRemoteConfiguration();
        remote.Publish("orders", new Dictionary<string, string?> { ["Greeting"] = "hello", ["Items:0"] = "a", ["Items:1"] = "b", ["Items:10"] = "c" });
        // 轮询只由用例推进时钟触发
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMinutes(1);
        var root = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Orders:Local"] = "yes", ["Orders:Items:2"] = "local" })
            .AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("orders") { Prefix = "Orders", PollInterval = interval, Clock = clock })
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
        Assert.Equal(1, remote.Fetches);
        await PollAsync(clock, interval, remote);
        await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("bye", root["Orders:Greeting"]);
        Assert.Null(root["Orders:Items:0"]);
        Assert.Equal(2, remote.Fetches);
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
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMinutes(1);
        var root = new ConfigurationBuilder().AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("orders") { PollInterval = interval, Clock = clock, OnError = errors.Add }).Build();
        using var disposable = root as IDisposable;
        remote.Fail = Error.Unavailable(ConfigurationErrors.RemoteUnavailable, ConfigurationMessages.RemoteUnavailable, MessageArguments.Of(("namespace", "orders"), ("reason", "down")));
        await PollAsync(clock, interval, remote);
        await PollAsync(clock, interval, remote);
        await WaitUntilAsync(() => errors.Count >= 2);
        Assert.Equal("hello", root["Greeting"]);

        // 抛出的异常同样只报告并保留数据
        remote.Fail = null;
        remote.Throw = true;
        var before = errors.Count;
        await PollAsync(clock, interval, remote);
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
    public async Task Load_ClientThrows_IsAFailure_ThatHonoursOptional_AndPollingStillStarts()
    {
        var remote = new InMemoryRemoteConfiguration { Throw = true };
        remote.Publish("orders", new Dictionary<string, string?> { ["Greeting"] = "hello" });

        // 必需：抛出与返回失败一样处理——先回调，再以不可达抛出，原异常作为内部异常
        var errors = new ConcurrentQueue<Error>();
        var thrown = Assert.Throws<FrameworkException>(() => new ConfigurationBuilder().AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("orders") { PollInterval = null, OnError = errors.Enqueue }).Build());
        Assert.Equal(ConfigurationErrors.RemoteUnavailable, thrown.Code);
        Assert.IsType<InvalidOperationException>(thrown.InnerException);
        Assert.Equal(ConfigurationErrors.RemoteUnavailable, Assert.Single(errors).Code);

        // 可选：为空并回调，照常开始轮询；客户端恢复后第一次轮询装入整份文档并触发重载
        errors.Clear();
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMinutes(1);
        var root = new ConfigurationBuilder().AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("orders") { PollInterval = interval, Clock = clock, Optional = true, OnError = errors.Enqueue }).Build();
        using var disposable = root as IDisposable;
        Assert.Empty(root.AsEnumerable());
        Assert.Equal("The remote configuration is broken.", Assert.Single(errors).Details?["reason"]);

        var reloaded = new TaskCompletionSource();
        ChangeToken.OnChange(root.GetReloadToken, () => reloaded.TrySetResult());
        remote.Throw = false;
        await PollAsync(clock, interval, remote);
        await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("hello", root["Greeting"]);
    }

    [Fact]
    public async Task OnErrorThrowing_NeitherChangesTheLoadResult_NorStopsPolling()
    {
        var remote = new InMemoryRemoteConfiguration();
        var reports = 0;
        void Broken(Error _)
        {
            Interlocked.Increment(ref reports);
            throw new InvalidOperationException("The error sink is broken.");
        }

        // 首次加载：回调抛出不改变 Optional 的结果
        var optional = new ConfigurationBuilder().AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("orders") { PollInterval = null, Optional = true, OnError = Broken }).Build();
        Assert.Empty(optional.AsEnumerable());
        var required = Assert.Throws<FrameworkException>(() => new ConfigurationBuilder().AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("orders") { PollInterval = null, OnError = Broken }).Build());
        Assert.Equal(ConfigurationErrors.NamespaceNotFound, required.Code);
        Assert.Equal(2, Volatile.Read(ref reports));

        // 轮询：回调抛出之后下一个周期照常取值、照常回调
        remote.Publish("orders", new Dictionary<string, string?> { ["Greeting"] = "hello" });
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMinutes(1);
        var root = new ConfigurationBuilder().AddXiHanRemoteConfiguration(remote, new RemoteConfigurationOptions("orders") { PollInterval = interval, Clock = clock, OnError = Broken }).Build();
        using var disposable = root as IDisposable;
        remote.Fail = Error.Unavailable(ConfigurationErrors.RemoteUnavailable, ConfigurationMessages.RemoteUnavailable, MessageArguments.Of(("namespace", "orders"), ("reason", "down")));
        await PollAsync(clock, interval, remote);
        await WaitUntilAsync(() => Volatile.Read(ref reports) == 3);
        await PollAsync(clock, interval, remote);
        await WaitUntilAsync(() => Volatile.Read(ref reports) == 4);

        // 远端恢复后照常装入新版本
        remote.Fail = null;
        remote.Publish("orders", new Dictionary<string, string?> { ["Greeting"] = "bye" });
        await PollAsync(clock, interval, remote);
        await WaitUntilAsync(() => root["Greeting"] == "bye");
        Assert.Equal(4, Volatile.Read(ref reports));
    }

    [Fact]
    public void ClientFactory_EachProviderOwnsItsClient_PassedClientsAreNotDisposed()
    {
        var created = new List<DisposableRemoteConfiguration>();
        IRemoteConfigurationClient Create()
        {
            var client = new DisposableRemoteConfiguration();
            created.Add(client);
            return client;
        }

        var options = new RemoteConfigurationOptions("orders") { PollInterval = null };

        // 同一构建器构建两次：各建一个客户端，各随自己的配置根释放
        var builder = new ConfigurationBuilder().AddXiHanRemoteConfiguration(Create, options);
        var first = builder.Build();
        var second = builder.Build();
        Assert.Equal(2, created.Count);
        ((IDisposable)first).Dispose();
        Assert.True(created[0].Disposed);
        Assert.False(created[1].Disposed);
        Assert.Equal("hello", second["Greeting"]);
        ((IDisposable)second).Dispose();
        Assert.True(created[1].Disposed);

        // ConfigurationManager 改动构建器属性时重建全部源：被替换的 Provider 连同它的客户端释放，新 Provider 用新客户端
        created.Clear();
        var manager = new ConfigurationManager();
        manager.AddXiHanRemoteConfiguration(Create, options);
        Assert.Equal("hello", manager["Greeting"]);
        ((IConfigurationBuilder)manager).Properties["rebuild"] = true;
        Assert.Equal(2, created.Count);
        Assert.True(created[0].Disposed);
        Assert.False(created[1].Disposed);
        Assert.Equal("hello", manager["Greeting"]);
        manager.Dispose();
        Assert.True(created[1].Disposed);

        // 传入现成的客户端：调用方持有，配置根释放时不释放它
        using var passed = new DisposableRemoteConfiguration();
        var root = new ConfigurationBuilder().AddXiHanRemoteConfiguration(passed, options).Build();
        ((IDisposable)root).Dispose();
        Assert.False(passed.Disposed);
    }

    [Fact]
    public void Options_AndDocument_Validate()
    {
        Assert.Throws<ArgumentException>(static () => new RemoteConfigurationOptions(" "));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new RemoteConfigurationOptions("a") { PollInterval = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new RemoteConfigurationOptions("a") { LoadTimeout = TimeSpan.Zero });
        Assert.Null(new RemoteConfigurationOptions("a") { PollInterval = null }.PollInterval);
        Assert.Same(TimeProvider.System, new RemoteConfigurationOptions("a").Clock);
        Assert.Throws<ArgumentNullException>(static () => new RemoteConfigurationOptions("a") { Clock = null! });
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
    /// 推进一个轮询周期，等这次取值开始。
    /// </summary>
    private static Task PollAsync(FakeTimeProvider clock, TimeSpan interval, InMemoryRemoteConfiguration remote)
    {
        var fetches = remote.Fetches;
        clock.Advance(interval);
        return WaitUntilAsync(() => remote.Fetches > fetches);
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
