// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Resilience;
using XiHan.Framework.Kernel.Tests.Fixtures;

namespace XiHan.Framework.Kernel.Tests.Resilience;

/// <summary>
/// <see cref="ResilienceOptions"/> 与 <see cref="ResiliencePolicy"/> 的读取。
/// </summary>
/// <remarks>
/// 默认无限制、键的解析、按操作覆盖与默认合并、非法值拒绝。
/// </remarks>
public sealed class ResilienceOptionsTests
{
    [Fact]
    public void FromConfiguration_Empty_HasNoLimits()
    {
        var options = ResilienceOptions.FromConfiguration(Hosts.Configuration());

        Assert.True(options.Defaults.IsEmpty);
        Assert.Empty(options.Operations);
        Assert.Same(options.Defaults, options.PolicyFor("orders.create"));
        Assert.Equal(TimeSpan.FromSeconds(1), options.Defaults.Window);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Defaults.BreakDuration);
    }

    [Fact]
    public void FromConfiguration_ReadsDefaults_AndMergesOperationOverrides()
    {
        var options = ResilienceOptions.FromConfiguration(Hosts.Configuration(
            ("XiHan:Kernel:Resilience:PermitLimit", "100"),
            ("XiHan:Kernel:Resilience:WindowSeconds", "60"),
            ("XiHan:Kernel:Resilience:QueueLimit", "5"),
            ("XiHan:Kernel:Resilience:ConcurrencyLimit", "8"),
            ("XiHan:Kernel:Resilience:ConcurrencyQueueLimit", "2"),
            ("XiHan:Kernel:Resilience:FailureThreshold", "3"),
            ("XiHan:Kernel:Resilience:BreakSeconds", "0.5"),
            ("XiHan:Kernel:Resilience:Operations:reports.export:ConcurrencyLimit", "1"),
            ("XiHan:Kernel:Resilience:Operations:reports.export:PermitLimit", "10")));

        var defaults = options.Defaults;
        Assert.Equal(100, defaults.PermitLimit);
        Assert.Equal(TimeSpan.FromSeconds(60), defaults.Window);
        Assert.Equal(5, defaults.QueueLimit);
        Assert.Equal(8, defaults.ConcurrencyLimit);
        Assert.Equal(2, defaults.ConcurrencyQueueLimit);
        Assert.Equal(3, defaults.FailureThreshold);
        Assert.Equal(TimeSpan.FromMilliseconds(500), defaults.BreakDuration);

        // 覆盖只改写出的键，其余沿用默认策略
        var export = options.PolicyFor("reports.export");
        Assert.Equal(1, export.ConcurrencyLimit);
        Assert.Equal(10, export.PermitLimit);
        Assert.Equal(TimeSpan.FromSeconds(60), export.Window);
        Assert.Equal(5, export.QueueLimit);
        Assert.Equal(3, export.FailureThreshold);
        Assert.Same(defaults, options.PolicyFor("reports.list"));
    }

    [Theory]
    [InlineData("PermitLimit", "0")]
    [InlineData("PermitLimit", "many")]
    [InlineData("WindowSeconds", "-1")]
    [InlineData("QueueLimit", "-1")]
    [InlineData("ConcurrencyLimit", "0")]
    [InlineData("ConcurrencyQueueLimit", "x")]
    [InlineData("FailureThreshold", "0")]
    [InlineData("BreakSeconds", "0")]
    [InlineData("Operations:reports.export:PermitLimit", "0")]
    public void FromConfiguration_RejectsInvalidValues(string key, string value)
    {
        var configuration = Hosts.Configuration(($"XiHan:Kernel:Resilience:{key}", value));

        var exception = Assert.Throws<ArgumentException>(() => ResilienceOptions.FromConfiguration(configuration));
        Assert.Contains(key.Split(':')[^1], exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_RejectsInvalidOperationId()
    {
        var configuration = Hosts.Configuration(("XiHan:Kernel:Resilience:Operations:Not An Id:PermitLimit", "1"));

        var exception = Assert.Throws<ArgumentException>(() => ResilienceOptions.FromConfiguration(configuration));
        Assert.Contains("Not An Id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_RejectsInvalidValues_WhenConstructedInCode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResiliencePolicy { PermitLimit = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResiliencePolicy { Window = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResiliencePolicy { QueueLimit = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResiliencePolicy { ConcurrencyLimit = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResiliencePolicy { ConcurrencyQueueLimit = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResiliencePolicy { FailureThreshold = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResiliencePolicy { BreakDuration = TimeSpan.FromSeconds(-1) });
        Assert.False(new ResiliencePolicy { ConcurrencyLimit = 4 }.IsEmpty);
    }
}
