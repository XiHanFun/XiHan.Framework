// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Http.Enums;

namespace XiHan.Framework.Http.Tests.Enums;

/// <summary>
/// HTTP 相关枚举值的测试
/// </summary>
public class EnumsTests
{
    /// <summary>
    /// 代理类型枚举具有五个有序成员
    /// </summary>
    [Fact]
    public void ProxyType_HasFiveOrderedMembers()
    {
        Assert.Equal(0, (int)ProxyType.Http);
        Assert.Equal(1, (int)ProxyType.Https);
        Assert.Equal(2, (int)ProxyType.Socks4);
        Assert.Equal(3, (int)ProxyType.Socks4A);
        Assert.Equal(4, (int)ProxyType.Socks5);
    }

    /// <summary>
    /// 代理选择策略与请求组别枚举成员完整
    /// </summary>
    [Fact]
    public void ProxySelectionStrategy_And_HttpGroupEnum_HaveExpectedMembers()
    {
        Assert.Equal(0, (int)ProxySelectionStrategy.RoundRobin);
        Assert.Equal(1, (int)ProxySelectionStrategy.Random);
        Assert.Equal(2, (int)ProxySelectionStrategy.LeastUsed);
        Assert.Equal(3, (int)ProxySelectionStrategy.FastestResponse);
        Assert.Equal(4, (int)ProxySelectionStrategy.Priority);

        Assert.Equal(0, (int)HttpGroupEnum.Remote);
        Assert.Equal(1, (int)HttpGroupEnum.Local);
    }
}
