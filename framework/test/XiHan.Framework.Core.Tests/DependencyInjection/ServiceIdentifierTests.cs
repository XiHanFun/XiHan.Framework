// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 服务标识符测试
/// </summary>
/// <remarks>
/// 服务标识符同时充当缓存服务提供器的字典键与暴露清单的去重键，
/// 因此相等性契约必须严格：键为空与键非空永不相等，相等的标识必须给出相同哈希。
/// </remarks>
public class ServiceIdentifierTests
{
    /// <summary>
    /// 仅指定服务类型时服务键为空
    /// </summary>
    [Fact]
    public void Constructor_WithTypeOnly_LeavesServiceKeyNull()
    {
        var identifier = new ServiceIdentifier(typeof(ISidContract));

        Assert.Equal(typeof(ISidContract), identifier.ServiceType);
        Assert.Null(identifier.ServiceKey);
    }

    /// <summary>
    /// 类型相同且都无键时相等
    /// </summary>
    [Fact]
    public void Equals_WhenSameTypeAndNoKey_ReturnsTrue()
    {
        var left = new ServiceIdentifier(typeof(ISidContract));
        var right = new ServiceIdentifier(typeof(ISidContract));

        Assert.True(left.Equals(right));
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// 类型不同时不相等
    /// </summary>
    [Fact]
    public void Equals_WhenDifferentType_ReturnsFalse()
    {
        var left = new ServiceIdentifier(typeof(ISidContract));
        var right = new ServiceIdentifier(typeof(ISidOtherContract));

        Assert.False(left.Equals(right));
        Assert.True(left != right);
    }

    /// <summary>
    /// 类型相同且键相同时相等
    /// </summary>
    [Fact]
    public void Equals_WhenSameTypeAndSameKey_ReturnsTrue()
    {
        var left = new ServiceIdentifier("k", typeof(ISidContract));
        var right = new ServiceIdentifier("k", typeof(ISidContract));

        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// 类型相同但键不同时不相等
    /// </summary>
    [Fact]
    public void Equals_WhenSameTypeAndDifferentKey_ReturnsFalse()
    {
        var left = new ServiceIdentifier("left", typeof(ISidContract));
        var right = new ServiceIdentifier("right", typeof(ISidContract));

        Assert.False(left.Equals(right));
    }

    /// <summary>
    /// 有键与无键永不相等
    /// </summary>
    [Fact]
    public void Equals_WhenOneSideHasKey_ReturnsFalse()
    {
        var keyed = new ServiceIdentifier("k", typeof(ISidContract));
        var plain = new ServiceIdentifier(typeof(ISidContract));

        Assert.False(keyed.Equals(plain));
        Assert.False(plain.Equals(keyed));
    }

    /// <summary>
    /// 与非同类对象比较时不相等
    /// </summary>
    [Fact]
    public void Equals_WhenOtherObjectType_ReturnsFalse()
    {
        var identifier = new ServiceIdentifier(typeof(ISidContract));

        Assert.False(identifier.Equals("not-an-identifier"));
    }

    /// <summary>
    /// 装箱比较走同一套相等语义
    /// </summary>
    [Fact]
    public void Equals_WhenBoxed_UsesSameSemantics()
    {
        object left = new ServiceIdentifier("k", typeof(ISidContract));
        object right = new ServiceIdentifier("k", typeof(ISidContract));

        Assert.True(left.Equals(right));
    }

    /// <summary>
    /// 可作为字典键区分键控与非键控服务
    /// </summary>
    [Fact]
    public void GetHashCode_AllowsUseAsDictionaryKey()
    {
        Dictionary<ServiceIdentifier, string> map = new()
        {
            [new ServiceIdentifier(typeof(ISidContract))] = "plain",
            [new ServiceIdentifier("k", typeof(ISidContract))] = "keyed"
        };

        Assert.Equal("plain", map[new ServiceIdentifier(typeof(ISidContract))]);
        Assert.Equal("keyed", map[new ServiceIdentifier("k", typeof(ISidContract))]);
    }
}

/// <summary>
/// 服务标识测试用契约
/// </summary>
internal interface ISidContract;

/// <summary>
/// 服务标识测试用另一契约
/// </summary>
internal interface ISidOtherContract;
