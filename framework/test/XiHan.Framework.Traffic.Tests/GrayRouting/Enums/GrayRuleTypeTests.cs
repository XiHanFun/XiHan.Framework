// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Enums;

namespace XiHan.Framework.Traffic.Tests;

/// <summary>
/// 灰度规则类型枚举测试
/// </summary>
/// <remarks>
/// GrayRuleType 会随 GrayRule 一起被持久化/下发（GrayRule 的 JSON 往返按数值序列化），
/// 因此这里锁死数值而不是名称：数值漂移会让存量规则整体错位。
/// </remarks>
public class GrayRuleTypeTests
{
    /// <summary>
    /// 各枚举成员的数值必须稳定
    /// </summary>
    [Theory]
    [InlineData(GrayRuleType.Percentage, 1)]
    [InlineData(GrayRuleType.UserId, 2)]
    [InlineData(GrayRuleType.TenantId, 3)]
    [InlineData(GrayRuleType.Header, 4)]
    [InlineData(GrayRuleType.IpAddress, 5)]
    [InlineData(GrayRuleType.Custom, 99)]
    public void NumericValue_IsStable(GrayRuleType ruleType, int expected)
    {
        Assert.Equal(expected, (int)ruleType);
    }

    /// <summary>
    /// 枚举成员数量固定为 6，且没有数值为 0 的成员
    /// </summary>
    /// <remarks>
    /// 没有 0 值意味着 default(GrayRuleType) 不会意外落到某个真实规则类型上。
    /// </remarks>
    [Fact]
    public void Members_AreExactlySixAndNoneMapsToZero()
    {
        var values = Enum.GetValues<GrayRuleType>();

        Assert.Equal(6, values.Length);
        Assert.All(values, value => Assert.NotEqual(0, (int)value));
    }

    /// <summary>
    /// 未定义的数值不会被识别为合法枚举值
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(98)]
    [InlineData(100)]
    public void UndefinedNumericValue_IsNotDefined(int numericValue)
    {
        Assert.False(Enum.IsDefined((GrayRuleType)numericValue));
    }

    /// <summary>
    /// 枚举名称与数值一一对应，不存在别名成员
    /// </summary>
    [Fact]
    public void Names_MatchValuesOneToOne()
    {
        var names = Enum.GetNames<GrayRuleType>();
        var values = Enum.GetValues<GrayRuleType>();

        Assert.Equal(names.Length, values.Length);
        Assert.Equal(values.Length, values.Select(value => (int)value).Distinct().Count());
    }
}
