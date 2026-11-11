// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Redis.Tests;

/// <summary>
/// 选项的读取、绑定覆盖与凭据遮蔽
/// </summary>
/// <remarks>
/// 不需要 Redis
/// </remarks>
public sealed class RedisMessagingOptionsTests
{
    /// <summary>
    /// 配置节下的连接键
    /// </summary>
    private const string ConfigurationKey = "XiHan:Messaging:Redis:Configuration";

    [Fact]
    public void FromConfiguration_ReadsDefaults_AndEveryKey()
    {
        var defaults = RedisMessagingOptions.FromConfiguration(Build((ConfigurationKey, "cfg:6379")), default);
        Assert.Equal("cfg:6379", defaults.Configuration);
        Assert.Equal("xihan:messaging:", defaults.KeyPrefix);
        Assert.Equal("xihan", defaults.ConsumerGroup);
        Assert.Equal(10, defaults.BatchSize);
        Assert.Equal(TimeSpan.FromMilliseconds(200), defaults.PollInterval);
        Assert.Equal(TimeSpan.FromSeconds(60), defaults.ClaimIdleTime);
        Assert.Equal(100_000, defaults.MaxLength);
        Assert.Equal(TimeSpan.FromSeconds(15), defaults.HealthProbeInterval);
        Assert.Null(defaults.UserName);
        Assert.Null(defaults.Password);

        var configured = RedisMessagingOptions.FromConfiguration(
            Build(
                (ConfigurationKey, "cfg:6379"),
                ("XiHan:Messaging:Redis:KeyPrefix", "app:"),
                ("XiHan:Messaging:Redis:ConsumerGroup", "orders-api"),
                ("XiHan:Messaging:Redis:BatchSize", "32"),
                ("XiHan:Messaging:Redis:PollIntervalSeconds", "0.05"),
                ("XiHan:Messaging:Redis:ClaimIdleTimeSeconds", "120"),
                ("XiHan:Messaging:Redis:MaxLength", "5000"),
                ("XiHan:Messaging:Redis:HealthProbeIntervalSeconds", "3")),
            default);
        Assert.Equal("app:", configured.KeyPrefix);
        Assert.Equal("orders-api", configured.ConsumerGroup);
        Assert.Equal(32, configured.BatchSize);
        Assert.Equal(TimeSpan.FromMilliseconds(50), configured.PollInterval);
        Assert.Equal(TimeSpan.FromSeconds(120), configured.ClaimIdleTime);
        Assert.Equal(5000, configured.MaxLength);
        Assert.Equal(TimeSpan.FromSeconds(3), configured.HealthProbeInterval);
    }

    [Theory]
    [InlineData("XiHan:Messaging:Redis:ConsumerGroup", "a@b")]
    [InlineData("XiHan:Messaging:Redis:ConsumerGroup", "")]
    [InlineData("XiHan:Messaging:Redis:ConsumerGroup", "-starts-with-dash")]
    [InlineData("XiHan:Messaging:Redis:BatchSize", "0")]
    [InlineData("XiHan:Messaging:Redis:BatchSize", "1001")]
    [InlineData("XiHan:Messaging:Redis:BatchSize", "ten")]
    [InlineData("XiHan:Messaging:Redis:PollIntervalSeconds", "0")]
    [InlineData("XiHan:Messaging:Redis:ClaimIdleTimeSeconds", "-1")]
    [InlineData("XiHan:Messaging:Redis:ClaimIdleTimeSeconds", "0.0001")]
    [InlineData("XiHan:Messaging:Redis:MaxLength", "0")]
    [InlineData("XiHan:Messaging:Redis:HealthProbeIntervalSeconds", "abc")]
    public void FromConfiguration_RejectsInvalidValues(string key, string value)
        => Assert.Throws<ArgumentException>(() => RedisMessagingOptions.FromConfiguration(Build((ConfigurationKey, "cfg:6379"), (key, value)), default));

    [Fact]
    public void FromConfiguration_WithoutAnyConnection_Fails()
        => Assert.Throws<ArgumentException>(static () => RedisMessagingOptions.FromConfiguration(Build(), default));

    [Fact]
    public void Binding_OverridesTheConnectionAndCredentials_AndBlankValuesDoNotCount()
    {
        var bound = Binding("bound:6379", ("username", "alice"), ("password", "p,w=d"));
        var options = RedisMessagingOptions.FromConfiguration(Build((ConfigurationKey, "cfg:6379,password=old"), ("XiHan:Messaging:Redis:KeyPrefix", "app:")), bound);
        Assert.Equal("bound:6379", options.Configuration);
        Assert.Equal("alice", options.UserName);
        Assert.Equal("p,w=d", options.Password);
        // 绑定只管连接与凭据，其余键照读配置节
        Assert.Equal("app:", options.KeyPrefix);

        // 主值空白的绑定不算给出连接；只给凭据的绑定照样覆盖凭据
        var credentialsOnly = RedisMessagingOptions.FromConfiguration(Build((ConfigurationKey, "cfg:6379")), Binding(" ", ("password", "secret"), ("username", " ")));
        Assert.Equal("cfg:6379", credentialsOnly.Configuration);
        Assert.Equal("secret", credentialsOnly.Password);
        Assert.Null(credentialsOnly.UserName);
    }

    [Fact]
    public void ConnectionOptions_ApplyTheBoundCredentials_WithoutRewritingTheString()
    {
        var options = new RedisMessagingOptions { Configuration = "localhost:6379,user=old,password=old", UserName = "alice", Password = "p,w=d" };

        var connection = RedisMessageBroker.ConnectionOptions(options);

        Assert.Equal("alice", connection.User);
        Assert.Equal("p,w=d", connection.Password);
        Assert.Single(connection.EndPoints);
        Assert.Equal("old", RedisMessageBroker.ConnectionOptions(options with { UserName = null, Password = null }).Password);
    }

    [Fact]
    public void ToString_MasksTheConnectionAndCredentials()
    {
        var options = new RedisMessagingOptions { Configuration = "localhost:6379,password=hunter2", UserName = "alice", Password = "s3cret" };

        var text = options.ToString();

        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", text, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret", text, StringComparison.Ordinal);
        Assert.Contains("Configuration = ***", text, StringComparison.Ordinal);
        Assert.Contains("ConsumerGroup = xihan", text, StringComparison.Ordinal);
        Assert.Contains("MaxLength = 100000", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 用内存键值对构建配置
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    /// <summary>
    /// 绑定 <c>queue</c>
    /// </summary>
    private static Maybe<BindingValue> Binding(string value, params (string Key, string Value)[] properties)
        => Maybe.Some(new BindingValue(RedisMessagingFeature.ConnectionBinding, "cache:distributed", value, properties.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal)));
}
