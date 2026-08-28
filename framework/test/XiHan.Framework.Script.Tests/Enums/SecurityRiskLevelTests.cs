// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Enums;

namespace XiHan.Framework.Script.Tests.Enums;

/// <summary>
/// 安全风险级别枚举测试
/// </summary>
/// <remarks>
/// <c>ValidateSecurityAsync</c> 用 <c>riskLevel &lt; SecurityRiskLevel.Medium</c> 这种数值比较来决定是否升级风险，
/// 所以成员顺序不是排版问题而是逻辑依赖：一旦把 High 排到 Low 前面，风险升级判断会整体反向。
/// </remarks>
public class SecurityRiskLevelTests
{
    /// <summary>
    /// 枚举成员的序号不允许漂移
    /// </summary>
    [Theory]
    [InlineData(SecurityRiskLevel.Low, 0)]
    [InlineData(SecurityRiskLevel.Medium, 1)]
    [InlineData(SecurityRiskLevel.High, 2)]
    public void Members_KeepStableNumericValues(SecurityRiskLevel level, int expected)
    {
        Assert.Equal(expected, (int)level);
    }

    /// <summary>
    /// 风险级别按低到高严格递增，扩展方法依赖这一比较语义
    /// </summary>
    [Fact]
    public void Members_AreOrderedFromLowToHigh()
    {
        Assert.True(SecurityRiskLevel.Low < SecurityRiskLevel.Medium);
        Assert.True(SecurityRiskLevel.Medium < SecurityRiskLevel.High);
    }

    /// <summary>
    /// 默认值落在低风险上
    /// </summary>
    [Fact]
    public void Default_IsLow()
    {
        Assert.Equal(SecurityRiskLevel.Low, default(SecurityRiskLevel));
    }
}
