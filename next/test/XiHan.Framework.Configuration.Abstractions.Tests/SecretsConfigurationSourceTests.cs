// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Configuration.Abstractions.Secrets;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Tests;

/// <summary>
/// 密钥配置源：映射、缺失语义、重读、名字校验。
/// </summary>
public sealed class SecretsConfigurationSourceTests
{
    [Fact]
    public async Task Load_MapsSecretsToKeys_AndRefreshesKeepingOldValuesOnFailure()
    {
        var store = new InMemorySecretStore();
        store.Put("orders/db", "pw1");
        store.Put("orders/api", "key1");
        var errors = new List<Error>();
        // 重读只由用例推进时钟触发：不推进就不重读，次数与时机都确定
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMinutes(1);
        var root = new ConfigurationBuilder()
            .AddXiHanSecrets(store, new SecretsConfigurationOptions(new Dictionary<string, SecretName>
            {
                ["ConnectionStrings:Orders"] = "orders/db",
                ["Api:Key"] = "orders/api",
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
        store.Put("orders/db", "pw2");
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
        store.Put("present", "v");
        var mappings = new Dictionary<string, SecretName> { ["A"] = "present", ["B"] = "absent" };
        var errors = new List<Error>();

        var thrown = Assert.Throws<FrameworkException>(() => new ConfigurationBuilder().AddXiHanSecrets(store, new SecretsConfigurationOptions(mappings) { OnError = errors.Add }).Build());
        Assert.Equal(ConfigurationErrors.SecretNotFound, thrown.Code);
        Assert.Contains("'B'", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(ConfigurationErrors.SecretNotFound, Assert.Single(errors).Code);

        var optional = new ConfigurationBuilder().AddXiHanSecrets(store, new SecretsConfigurationOptions(mappings) { Optional = true }).Build();
        Assert.Equal("v", optional["A"]);
        Assert.Null(optional["B"]);

        store.Throw = true;
        Assert.Throws<InvalidOperationException>(() => new ConfigurationBuilder().AddXiHanSecrets(store, new SecretsConfigurationOptions(mappings)).Build());
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
        Assert.Throws<ArgumentException>(static () => new SecretsConfigurationOptions(new Dictionary<string, SecretName> { [" "] = "a" }));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new SecretsConfigurationOptions(new Dictionary<string, SecretName> { ["A"] = "a" }) { RefreshInterval = TimeSpan.Zero });
        Assert.Same(TimeProvider.System, new SecretsConfigurationOptions(new Dictionary<string, SecretName> { ["A"] = "a" }).Clock);
        Assert.Throws<ArgumentNullException>(static () => new SecretsConfigurationOptions(new Dictionary<string, SecretName> { ["A"] = "a" }) { Clock = null! });
        Assert.Equal(SecretValue.Redacted, new SecretValue("x", "1").ToString());
        Assert.Equal("1", new SecretValue("x", "1").Version);
        Assert.DoesNotContain("x", new SecretLookup("a", new SecretValue("x")).ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 推进一个重读周期，等这次重读开始。
    /// </summary>
    private static Task RefreshAsync(FakeTimeProvider clock, TimeSpan interval, InMemorySecretStore store)
    {
        var reads = store.Reads;
        clock.Advance(interval);
        return WaitUntilAsync(() => store.Reads > reads);
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
