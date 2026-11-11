// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Configuration.Abstractions.Secrets;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Tests;

/// <summary>
/// 密钥配置源：映射、缺失语义、整体失败、重读、回调异常、名字校验
/// </summary>
public sealed class SecretsConfigurationSourceTests
{
    [Fact]
    public async Task Load_MapsSecretsToKeys_AndRefreshesKeepingOldValuesOnFailure()
    {
        var store = new InMemorySecretStore();
        store.Put(new SecretName("orders/db"), "pw1");
        store.Put(new SecretName("orders/api"), "key1");
        var errors = new List<Error>();
        // 重读只由用例推进时钟触发：不推进就不重读，次数与时机都确定
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMinutes(1);
        var root = new ConfigurationBuilder()
            .AddXiHanSecrets(store, new SecretsConfigurationOptions(new Dictionary<string, SecretName>
            {
                ["ConnectionStrings:Orders"] = new SecretName("orders/db"),
                ["Api:Key"] = new SecretName("orders/api"),
            })
            {
                RefreshInterval = interval,
                Clock = clock,
                OnError = errors.Add,
            })
            .Build();
        using var disposable = root as IDisposable;

        Assert.Equal("pw1", root["ConnectionStrings:Orders"]);
        Assert.Equal("key1", root["Api:Key"]);
        Assert.Equal(1, store.Reads);

        var reloaded = new TaskCompletionSource();
        ChangeToken.OnChange(root.GetReloadToken, () => reloaded.TrySetResult());

        // 重读到同样的内容不触发重载：重读一次接一次，下一次开始时上一次已经写完，四次重读里前三次确定已写回
        for (var cycle = 0; cycle < 4; cycle++)
        {
            await RefreshAsync(clock, interval, store);
        }

        Assert.Equal(5, store.Reads);
        Assert.False(reloaded.Task.IsCompleted);

        // 值变了才触发，重载时已是新值
        store.Put(new SecretName("orders/db"), "pw2");
        await RefreshAsync(clock, interval, store);
        await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("pw2", root["ConnectionStrings:Orders"]);

        // 重读时被拒绝的项保留旧值并回调
        store.Denied.Add("orders/api");
        await RefreshAsync(clock, interval, store);
        await WaitUntilAsync(() => errors.Count > 0);
        Assert.Equal(ConfigurationErrors.SecretDenied, errors[0].Code);
        Assert.Equal("key1", root["Api:Key"]);

        // 整体抛出同样只报告
        store.Throw = true;
        await RefreshAsync(clock, interval, store);
        await WaitUntilAsync(() => errors.Any(static e => e.Code == ConfigurationErrors.SecretsUnavailable));
        Assert.Equal("pw2", root["ConnectionStrings:Orders"]);
    }

    [Fact]
    public void Load_MissingSecret_ThrowsUnlessOptional()
    {
        var store = new InMemorySecretStore();
        store.Put(new SecretName("present"), "v");
        var mappings = new Dictionary<string, SecretName> { ["A"] = new SecretName("present"), ["B"] = new SecretName("absent") };
        var errors = new List<Error>();

        var thrown = Assert.Throws<FrameworkException>(() => new ConfigurationBuilder().AddXiHanSecrets(store, new SecretsConfigurationOptions(mappings) { OnError = errors.Add }).Build());
        Assert.Equal(ConfigurationErrors.SecretNotFound, thrown.Code);
        Assert.Contains("'B'", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(ConfigurationErrors.SecretNotFound, Assert.Single(errors).Code);

        var optional = new ConfigurationBuilder().AddXiHanSecrets(store, new SecretsConfigurationOptions(mappings) { Optional = true }).Build();
        Assert.Equal("v", optional["A"]);
        Assert.Null(optional["B"]);

        // 密钥库整体抛出与返回失败一样处理：先回调，再以不可达抛出，原异常作为内部异常
        store.Throw = true;
        errors.Clear();
        var broken = Assert.Throws<FrameworkException>(() => new ConfigurationBuilder().AddXiHanSecrets(store, new SecretsConfigurationOptions(mappings) { OnError = errors.Add }).Build());
        Assert.Equal(ConfigurationErrors.SecretsUnavailable, broken.Code);
        Assert.IsType<InvalidOperationException>(broken.InnerException);
        Assert.Equal("The secret store is broken.", Assert.Single(errors).Details?["reason"]);
    }

    [Fact]
    public async Task Load_OptionalTimeout_StillStartsRefreshing()
    {
        var store = new InMemorySecretStore { Hang = true };
        store.Put(new SecretName("orders/db"), "pw1");
        var errors = new ConcurrentQueue<Error>();
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMinutes(1);
        // 首次读取挂起到超时：可选时该源为空并回调一次
        var root = new ConfigurationBuilder()
            .AddXiHanSecrets(store, new SecretsConfigurationOptions(new Dictionary<string, SecretName> { ["ConnectionStrings:Orders"] = new SecretName("orders/db") })
            {
                LoadTimeout = TimeSpan.FromMilliseconds(50),
                RefreshInterval = interval,
                Clock = clock,
                Optional = true,
                OnError = errors.Enqueue,
            })
            .Build();
        using var disposable = root as IDisposable;
        Assert.Null(root["ConnectionStrings:Orders"]);
        var timedOut = Assert.Single(errors);
        Assert.Equal(ConfigurationErrors.SecretsUnavailable, timedOut.Code);
        Assert.Equal("load timeout", timedOut.Details?["reason"]);

        // 仍按 RefreshInterval 重读：密钥库恢复后第一次重读装入并触发重载
        var reloaded = new TaskCompletionSource();
        ChangeToken.OnChange(root.GetReloadToken, () => reloaded.TrySetResult());
        store.Hang = false;
        await RefreshAsync(clock, interval, store);
        await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("pw1", root["ConnectionStrings:Orders"]);
    }

    [Fact]
    public async Task Miscount_IsAFailure_OnLoadAndRefresh_AndOnErrorThrowingDoesNotStopRefreshing()
    {
        var store = new InMemorySecretStore { Short = true };
        store.Put(new SecretName("a"), "1");
        store.Put(new SecretName("b"), "2");
        var mappings = new Dictionary<string, SecretName> { ["A"] = new SecretName("a"), ["B"] = new SecretName("b") };

        // 首次读取条数不符：回调一次不可达，再按 Optional 抛出或为空
        var errors = new ConcurrentQueue<Error>();
        var thrown = Assert.Throws<FrameworkException>(() => new ConfigurationBuilder().AddXiHanSecrets(store, new SecretsConfigurationOptions(mappings) { OnError = errors.Enqueue }).Build());
        Assert.Equal(ConfigurationErrors.SecretsUnavailable, thrown.Code);
        Assert.Equal("the secret store returned 1 results for 2 names", Assert.Single(errors).Details?["reason"]);
        errors.Clear();
        var optional = new ConfigurationBuilder().AddXiHanSecrets(store, new SecretsConfigurationOptions(mappings) { Optional = true, OnError = errors.Enqueue }).Build();
        Assert.Null(optional["A"]);
        Assert.Equal(ConfigurationErrors.SecretsUnavailable, Assert.Single(errors).Code);

        // 重读时条数不符：回调并保留全部旧值；回调自己抛出也不终止重读
        store.Short = false;
        var reports = new ConcurrentQueue<Error>();
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMinutes(1);
        var root = new ConfigurationBuilder()
            .AddXiHanSecrets(store, new SecretsConfigurationOptions(mappings)
            {
                RefreshInterval = interval,
                Clock = clock,
                OnError = error =>
                {
                    reports.Enqueue(error);
                    throw new InvalidOperationException("The error sink is broken.");
                },
            })
            .Build();
        using var disposable = root as IDisposable;
        store.Short = true;
        store.Put(new SecretName("a"), "changed");
        await RefreshAsync(clock, interval, store);
        await WaitUntilAsync(() => !reports.IsEmpty);
        Assert.Equal(ConfigurationErrors.SecretsUnavailable, Assert.Single(reports).Code);
        Assert.Equal(("1", "2"), (root["A"], root["B"]));

        store.Short = false;
        await RefreshAsync(clock, interval, store);
        await WaitUntilAsync(() => root["A"] == "changed");
        Assert.Single(reports);
    }

    [Fact]
    public void SecretNames_AndOptions_Validate()
    {
        Assert.True(SecretName.IsValid("orders/Db-Password.v2"));
        foreach (var invalid in new[] { "", "/lead", "trail/", "a//b", "a/../b", "-x", "a b" })
        {
            Assert.False(SecretName.IsValid(invalid));
            Assert.Throws<ArgumentException>(() => new SecretName(invalid));
        }

        Assert.Throws<ArgumentException>(static () => new SecretsConfigurationOptions(new Dictionary<string, SecretName>()));
        Assert.Throws<ArgumentException>(static () => new SecretsConfigurationOptions(new Dictionary<string, SecretName> { [" "] = new SecretName("a") }));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new SecretsConfigurationOptions(new Dictionary<string, SecretName> { ["A"] = new SecretName("a") }) { RefreshInterval = TimeSpan.Zero });
        Assert.Same(TimeProvider.System, new SecretsConfigurationOptions(new Dictionary<string, SecretName> { ["A"] = new SecretName("a") }).Clock);
        Assert.Throws<ArgumentNullException>(static () => new SecretsConfigurationOptions(new Dictionary<string, SecretName> { ["A"] = new SecretName("a") }) { Clock = null! });
        Assert.Equal(SecretValue.Redacted, new SecretValue("x", "1").ToString());
        Assert.Equal("1", new SecretValue("x", "1").Version);
        Assert.DoesNotContain("x", new SecretLookup(new SecretName("a"), new SecretValue("x")).ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 推进一个重读周期，等这次重读开始
    /// </summary>
    private static Task RefreshAsync(FakeTimeProvider clock, TimeSpan interval, InMemorySecretStore store)
    {
        var reads = store.Reads;
        clock.Advance(interval);
        return WaitUntilAsync(() => store.Reads > reads);
    }

    /// <summary>
    /// 轮询直到条件成立，最多 10 秒
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
