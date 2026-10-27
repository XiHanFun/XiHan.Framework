// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Persistence.Abstractions.Identifiers;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="SnowflakeOptions"/>：缺省值、逐键读取、非法值拒绝。
/// </summary>
public sealed class SnowflakeOptionsTests
{
    [Fact]
    public void Defaults_MatchAdr()
    {
        var options = SnowflakeOptions.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), options.Epoch);
        Assert.Null(options.WorkerId);
        Assert.Equal(TimeSpan.FromMilliseconds(100), options.ClockDriftTolerance);
        Assert.Equal(TimeSpan.FromSeconds(30), options.LeaseTimeToLive);
    }

    [Fact]
    public void FromConfiguration_ReadsEveryKey_AndNormalizesEpochToUtc()
    {
        var options = SnowflakeOptions.FromConfiguration(Build(
            ("Epoch", "2025-06-01T08:00:00+08:00"),
            ("WorkerId", "1023"),
            ("ClockDriftToleranceMilliseconds", "0"),
            ("LeaseTimeToLiveSeconds", "5")));

        Assert.Equal(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero), options.Epoch);
        Assert.Equal(TimeSpan.Zero, options.Epoch.Offset);
        Assert.Equal(1023, options.WorkerId);
        Assert.Equal(TimeSpan.Zero, options.ClockDriftTolerance);
        Assert.Equal(TimeSpan.FromSeconds(5), options.LeaseTimeToLive);
        // 没有时区的 epoch 按 UTC 理解
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), SnowflakeOptions.FromConfiguration(Build(("Epoch", "2024-01-01"))).Epoch);
    }

    [Theory]
    [InlineData("Epoch", "yesterday")]
    [InlineData("WorkerId", "1024")]
    [InlineData("WorkerId", "-1")]
    [InlineData("WorkerId", "a")]
    [InlineData("ClockDriftToleranceMilliseconds", "-5")]
    [InlineData("LeaseTimeToLiveSeconds", "0")]
    public void FromConfiguration_RejectsInvalidValues(string key, string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => SnowflakeOptions.FromConfiguration(Build((key, value))));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Init_ValidatesRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeOptions { WorkerId = 1024 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeOptions { ClockDriftTolerance = TimeSpan.FromMilliseconds(-1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeOptions { LeaseTimeToLive = TimeSpan.Zero });
    }

    /// <summary>
    /// 在雪花配置节下写入若干键。
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>($"{SnowflakeOptions.ConfigurationSection}:{e.Key}", e.Value)))
            .Build();
}
