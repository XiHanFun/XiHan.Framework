// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 进程外插件选项的配置读取。
/// </summary>
/// <remarks>
/// 配置与属性的取值范围一致：不合规的值以带配置键的 <see cref="ArgumentException"/> 拒绝，而不是在属性赋值时抛出不带键的 <see cref="ArgumentOutOfRangeException"/>。
/// </remarks>
public sealed class OutOfProcessPluginOptionsTests
{
    [Fact]
    public void FromConfiguration_ReadsTimingsAndLimits()
    {
        var options = OutOfProcessPluginOptions.FromConfiguration(Configuration(("HandshakeTimeoutSeconds", "2.5"), ("HealthFailures", "1"), ("RestartLimit", "0")));

        Assert.Equal(TimeSpan.FromSeconds(2.5), options.HandshakeTimeout);
        Assert.Equal(1, options.HealthFailures);
        Assert.Equal(0, options.RestartLimit);
    }

    [Theory]
    [InlineData("HealthFailures", "0", "a positive integer")]
    [InlineData("HealthFailures", "1.5", "a positive integer")]
    [InlineData("RestartLimit", "-1", "a non-negative integer")]
    [InlineData("StartTimeoutSeconds", "0", "a positive number")]
    public void FromConfiguration_RejectsOutOfRangeValues_WithTheConfigurationKey(string key, string value, string expected)
    {
        var exception = Assert.Throws<ArgumentException>(() => OutOfProcessPluginOptions.FromConfiguration(Configuration((key, value))));

        Assert.Contains($"{OutOfProcessPluginOptions.ConfigurationSection}:{key} must be {expected}", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 配置节下的键值。
    /// </summary>
    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>($"{OutOfProcessPluginOptions.ConfigurationSection}:{v.Key}", v.Value))).Build();
}
