// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Enums;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 升级状态枚举测试
/// </summary>
/// <remarks>
/// 该枚举会随升级版本快照一起序列化给客户端（System.Text.Json 默认按数值序列化），
/// 所以数值与成员集合都属于对外协议，必须锁死。
/// </remarks>
public class UpgradeStatusTests
{
    /// <summary>
    /// 各状态的数值不允许漂移
    /// </summary>
    /// <param name="status">升级状态</param>
    /// <param name="expected">期望数值</param>
    [Theory]
    [InlineData(UpgradeStatus.Normal, 0)]
    [InlineData(UpgradeStatus.Upgrading, 1)]
    [InlineData(UpgradeStatus.Completed, 2)]
    [InlineData(UpgradeStatus.Failed, 3)]
    public void Value_OfEachMember_IsStable(UpgradeStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    /// <summary>
    /// 成员集合与顺序固定为四个
    /// </summary>
    [Fact]
    public void Members_AreExactlyFourInDeclaredOrder()
    {
        Assert.Equal(["Normal", "Upgrading", "Completed", "Failed"], Enum.GetNames<UpgradeStatus>());
    }

    /// <summary>
    /// 默认值是正常状态，未初始化的快照不会被误判为升级中
    /// </summary>
    [Fact]
    public void Default_IsNormal()
    {
        Assert.Equal(UpgradeStatus.Normal, default(UpgradeStatus));
    }
}
