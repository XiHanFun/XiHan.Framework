// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Email.Abstractions;
using XiHan.Framework.Bot.Email.Options;
using XiHan.Framework.Bot.Email.Stores;

namespace XiHan.Framework.Bot.Email.Tests.Stores;

/// <summary>
/// <see cref="DefaultEmailConfigStore"/> 测试
/// </summary>
/// <remarks>
/// 默认实现只是 IOptionsMonitor 的薄封装，关键契约有三条：
/// 1) 取的是 CurrentValue（热更新可见），不是构造时快照；
/// 2) 永远不返回 null（选项系统总能给出默认实例）；
/// 3) 实现了 IEmailConfigStore，可被 TryAdd 语义替换。
/// </remarks>
public class DefaultEmailConfigStoreTests
{
    /// <summary>
    /// 从真实选项容器取到 Configure 委托写入的配置
    /// </summary>
    [Fact]
    public async Task GetAsync_FromRealOptionsContainer_ReturnsConfiguredValue()
    {
        var services = new ServiceCollection();
        services.Configure<EmailOptions>(options =>
        {
            options.Enabled = false;
            options.IsBodyHtml = false;
            options.From.SmtpHost = "smtp.example.com";
            options.To.Add("to@example.com");
        });
        await using var provider = services.BuildServiceProvider();
        var store = new DefaultEmailConfigStore(provider.GetRequiredService<IOptionsMonitor<EmailOptions>>());

        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(options);
        Assert.False(options.Enabled);
        Assert.False(options.IsBodyHtml);
        Assert.Equal("smtp.example.com", options.From.SmtpHost);
        Assert.Single(options.To);
        Assert.Equal("to@example.com", options.To[0]);
    }

    /// <summary>
    /// 未做任何 Configure 时返回默认配置而不是 null
    /// </summary>
    /// <remarks>
    /// EmailBotProvider 把 null 当作"未配置"直接拒发，所以默认实现必须给出非 null 的默认实例，
    /// 让"启用但未填发件人"落到更明确的发件人校验分支上。
    /// </remarks>
    [Fact]
    public async Task GetAsync_WithoutConfigure_ReturnsDefaultInstance()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        await using var provider = services.BuildServiceProvider();
        var store = new DefaultEmailConfigStore(provider.GetRequiredService<IOptionsMonitor<EmailOptions>>());

        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(options);
        Assert.True(options.Enabled);
        Assert.Equal(string.Empty, options.From.SmtpHost);
    }

    /// <summary>
    /// 每次调用都重新读取 CurrentValue，热更新对调用方可见
    /// </summary>
    [Fact]
    public async Task GetAsync_ReReadsCurrentValue_OnEveryCall()
    {
        var monitor = new MutableOptionsMonitor<EmailOptions>(new EmailOptions { Enabled = true });
        var store = new DefaultEmailConfigStore(monitor);

        var before = await store.GetAsync(TestContext.Current.CancellationToken);
        monitor.Set(new EmailOptions { Enabled = false });
        var after = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.True(before.Enabled);
        Assert.False(after.Enabled);
        Assert.NotSame(before, after);
    }

    /// <summary>
    /// 已完成的任务不受取消令牌影响
    /// </summary>
    /// <remarks>
    /// 默认实现是纯内存读取，不做取消检查；调用方传入已取消的令牌也应正常拿到配置，
    /// 取消语义留给真正发起 IO 的实现（如数据库配置存储）。
    /// </remarks>
    [Fact]
    public async Task GetAsync_WithCancelledToken_StillCompletes()
    {
        var monitor = new MutableOptionsMonitor<EmailOptions>(new EmailOptions());
        var store = new DefaultEmailConfigStore(monitor);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var options = await store.GetAsync(cts.Token);

        Assert.NotNull(options);
    }

    /// <summary>
    /// 默认实现满足 IEmailConfigStore 抽象
    /// </summary>
    [Fact]
    public void Type_ImplementsEmailConfigStoreAbstraction()
    {
        var monitor = new MutableOptionsMonitor<EmailOptions>(new EmailOptions());

        var store = new DefaultEmailConfigStore(monitor);

        Assert.IsAssignableFrom<IEmailConfigStore>(store);
    }

    /// <summary>
    /// 可切换当前值的选项监视器替身
    /// </summary>
    /// <typeparam name="T">选项类型</typeparam>
    private sealed class MutableOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private T _value;

        public MutableOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name)
        {
            return _value;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }

        public void Set(T value)
        {
            _value = value;
        }
    }
}
