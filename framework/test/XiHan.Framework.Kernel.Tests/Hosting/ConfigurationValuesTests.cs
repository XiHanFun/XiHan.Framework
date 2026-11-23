// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 配置标量的统一读取：缺失返回空，写了却不合法抛出带完整配置键的异常
/// </summary>
/// <remarks>
/// 原先各包的选项类各写一份解析，同一种写法有的包收、有的包拒，装不下的秒数还会抛不带配置键的 <see cref="OverflowException"/>
/// </remarks>
public sealed class ConfigurationValuesTests
{
    /// <summary>
    /// 测试用的配置节路径
    /// </summary>
    private const string Section = "XiHan:Demo";

    [Fact]
    public void MissingKeys_AreNull_ForEveryKind()
    {
        var section = Configuration().GetSection(Section);

        Assert.Null(ConfigurationValues.ReadSeconds(section, "TimeoutSeconds"));
        Assert.Null(ConfigurationValues.ReadInt32(section, "Count", 0));
        Assert.Null(ConfigurationValues.ReadInt64(section, "Bytes", 0));
        Assert.Null(ConfigurationValues.ReadBoolean(section, "Enabled"));
        Assert.Null(ConfigurationValues.ReadEnum<DayOfWeek>(section, "Day"));
    }

    [Theory]
    [InlineData("30", 30d)]
    [InlineData("0.5", 0.5d)]
    [InlineData("1e3", 1000d)]
    [InlineData(" 2 ", 2d)]
    public void Seconds_AcceptFiniteNumbers_InTheInvariantCulture(string raw, double seconds)
        => Assert.Equal(TimeSpan.FromSeconds(seconds), ConfigurationValues.ReadSeconds(SectionWith("TimeoutSeconds", raw), "TimeoutSeconds"));

    [Theory]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e400")]
    [InlineData("1e300")]
    [InlineData("1,5")]
    public void Seconds_RejectEverythingElse_NamingTheFullKey(string raw)
    {
        // 1e300 是有限的数，但装不进 TimeSpan：原先在 TimeSpan.FromSeconds 抛 OverflowException
        var thrown = Assert.Throws<ArgumentException>(() => ConfigurationValues.ReadSeconds(SectionWith("TimeoutSeconds", raw), "TimeoutSeconds"));

        Assert.Equal($"'XiHan:Demo:TimeoutSeconds' must be a positive number of seconds, but was '{raw}'.", thrown.Message);
    }

    [Fact]
    public void Seconds_AllowZero_OnlyWhenAsked()
    {
        Assert.Equal(TimeSpan.Zero, ConfigurationValues.ReadSeconds(SectionWith("CooldownSeconds", "0"), "CooldownSeconds", allowZero: true));

        var thrown = Assert.Throws<ArgumentException>(() => ConfigurationValues.ReadSeconds(SectionWith("CooldownSeconds", "-0.1"), "CooldownSeconds", allowZero: true));
        Assert.Equal("'XiHan:Demo:CooldownSeconds' must be a non-negative number of seconds, but was '-0.1'.", thrown.Message);
    }

    [Theory]
    [InlineData("10", 10)]
    [InlineData(" 10 ", 10)]
    [InlineData("+10", 10)]
    public void Integers_AcceptWhatTheConfigurationBinderAccepts(string raw, int expected)
    {
        Assert.Equal(expected, ConfigurationValues.ReadInt32(SectionWith("Count", raw), "Count", 1));
        Assert.Equal(expected, ConfigurationValues.ReadInt64(SectionWith("Count", raw), "Count", 1));
    }

    [Theory]
    [InlineData("0", 1, int.MaxValue, "a positive integer")]
    [InlineData("-1", 0, int.MaxValue, "a non-negative integer")]
    [InlineData("4", 5, int.MaxValue, "an integer not less than 5")]
    [InlineData("11", 1, 10, "an integer between 1 and 10")]
    [InlineData("1.5", 1, 10, "an integer between 1 and 10")]
    [InlineData("many", 0, int.MaxValue, "a non-negative integer")]
    [InlineData("", 0, int.MaxValue, "a non-negative integer")]
    [InlineData("2147483648", 0, int.MaxValue, "a non-negative integer")]
    public void Int32_OutOfRangeOrMalformed_SaysWhatIsExpected(string raw, int minimum, int maximum, string expected)
    {
        var thrown = Assert.Throws<ArgumentException>(() => ConfigurationValues.ReadInt32(SectionWith("Count", raw), "Count", minimum, maximum));

        Assert.Equal($"'XiHan:Demo:Count' must be {expected}, but was '{raw}'.", thrown.Message);
    }

    [Fact]
    public void Int64_CoversValuesBeyondInt32_AndItsOwnRange()
    {
        Assert.Equal(8_589_934_592L, ConfigurationValues.ReadInt64(SectionWith("Bytes", "8589934592"), "Bytes", 1));
        Assert.Equal(-5L, ConfigurationValues.ReadInt64(SectionWith("Offset", "-5"), "Offset", -10, 10));

        var thrown = Assert.Throws<ArgumentException>(() => ConfigurationValues.ReadInt64(SectionWith("Bytes", "0"), "Bytes", 1, 1024));
        Assert.Equal("'XiHan:Demo:Bytes' must be an integer between 1 and 1024, but was '0'.", thrown.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigurationValues.ReadInt64(SectionWith("Bytes", "1"), "Bytes", 2, 1));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("False", false)]
    [InlineData(" TRUE ", true)]
    public void Booleans_AreTrueOrFalse_IgnoringCase(string raw, bool expected)
        => Assert.Equal(expected, ConfigurationValues.ReadBoolean(SectionWith("Enabled", raw), "Enabled"));

    [Theory]
    [InlineData("")]
    [InlineData("yes")]
    [InlineData("1")]
    public void Booleans_RejectEverythingElse(string raw)
    {
        var thrown = Assert.Throws<ArgumentException>(() => ConfigurationValues.ReadBoolean(SectionWith("Enabled", raw), "Enabled"));

        Assert.Equal($"'XiHan:Demo:Enabled' must be 'true' or 'false', but was '{raw}'.", thrown.Message);
    }

    [Fact]
    public void Enums_MatchMemberNames_IgnoringCase_AndNeverNumbers()
    {
        Assert.Equal(FailurePolicy.IsolateFeature, ConfigurationValues.ReadEnum<FailurePolicy>(SectionWith("Policy", "isolateFeature"), "Policy"));

        // 数字写法过得了 Enum.TryParse：1 换一个版本可能就是另一个成员，所以不收
        foreach (var raw in new[] { "1", "0", "Unknown", "", "FailFast,IsolateFeature" })
        {
            var thrown = Assert.Throws<ArgumentException>(() => ConfigurationValues.ReadEnum<FailurePolicy>(SectionWith("Policy", raw), "Policy"));
            Assert.StartsWith("'XiHan:Demo:Policy' must be one of '", thrown.Message, StringComparison.Ordinal);
            Assert.EndsWith($", but was '{raw}'.", thrown.Message, StringComparison.Ordinal);
            Assert.Contains($"'{nameof(FailurePolicy.IsolateFeature)}'", thrown.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RootConfiguration_UsesTheKeyAsGiven_SinceThereIsNoSectionPath()
    {
        var root = Configuration(("XiHan:Demo:Count", "x"));

        var thrown = Assert.Throws<ArgumentException>(() => ConfigurationValues.ReadInt32(root, "XiHan:Demo:Count", 0));

        Assert.Equal("'XiHan:Demo:Count' must be a non-negative integer, but was 'x'.", thrown.Message);
    }

    [Fact]
    public void Arguments_AreChecked()
    {
        Assert.Throws<ArgumentNullException>(static () => ConfigurationValues.ReadBoolean(null!, "Enabled"));
        Assert.Throws<ArgumentException>(static () => ConfigurationValues.ReadBoolean(Configuration(), string.Empty));
    }

    /// <summary>
    /// 只有这一个键的配置节
    /// </summary>
    private static IConfigurationSection SectionWith(string key, string value) => Configuration(($"{Section}:{key}", value)).GetSection(Section);

    /// <summary>
    /// 用键值对构造内存配置
    /// </summary>
    private static IConfigurationRoot Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();
}
