// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Timing.Tests;

/// <summary>
/// 时钟选项测试
/// </summary>
/// <remarks>
/// 选项只有一个 Kind，但它是整个时间管理包的总开关：默认值决定了框架开箱即用时
/// 既不按 UTC 存时间、也不宣称支持多时区。默认值一旦漂移，落库时间的语义会整体改变，
/// 所以在这里把默认值与它的下游后果一起锁死。
/// </remarks>
public class XiHanClockOptionsTests
{
    /// <summary>
    /// 默认时间类型是未指定
    /// </summary>
    [Fact]
    public void Kind_ByDefault_IsUnspecified()
    {
        var options = new XiHanClockOptions();

        Assert.Equal(DateTimeKind.Unspecified, options.Kind);
    }

    /// <summary>
    /// 时间类型可写入并原样读回
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Kind_AfterSet_ReturnsAssignedValue(DateTimeKind kind)
    {
        var options = new XiHanClockOptions
        {
            Kind = kind
        };

        Assert.Equal(kind, options.Kind);
    }

    /// <summary>
    /// 不同实例之间不共享状态
    /// </summary>
    [Fact]
    public void Kind_AcrossInstances_IsNotShared()
    {
        var configured = new XiHanClockOptions
        {
            Kind = DateTimeKind.Utc
        };

        var fresh = new XiHanClockOptions();

        Assert.Equal(DateTimeKind.Utc, configured.Kind);
        Assert.Equal(DateTimeKind.Unspecified, fresh.Kind);
    }
}
