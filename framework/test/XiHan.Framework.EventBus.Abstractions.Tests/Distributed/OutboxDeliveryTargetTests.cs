// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests.Distributed;

/// <summary>
/// 发件箱投递目标契约测试
/// </summary>
public class OutboxDeliveryTargetTests
{
    /// <summary>
    /// 租户标识必须大于零
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void 租户标识必须大于零(long tenantId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OutboxDeliveryTarget(tenantId));
    }

    /// <summary>
    /// 默认启用并保留租户信息
    /// </summary>
    [Fact]
    public void 默认启用并保留租户信息()
    {
        var target = new OutboxDeliveryTarget(1001, "acme");

        Assert.Equal(1001, target.TenantId);
        Assert.Equal("acme", target.TenantName);
        Assert.True(target.IsEnabled);
    }

    /// <summary>
    /// 可以构造停用的目标
    /// </summary>
    [Fact]
    public void 可以构造停用的目标()
    {
        Assert.False(new OutboxDeliveryTarget(1001, isEnabled: false).IsEnabled);
    }

    /// <summary>
    /// 目标页不接受空引用的目标集合
    /// </summary>
    [Fact]
    public void 目标页不接受空引用的目标集合()
    {
        Assert.Throws<ArgumentNullException>(() => new OutboxDeliveryTargetPage(null!, null));
    }

    /// <summary>
    /// 空游标表示目录末尾
    /// </summary>
    /// <param name="cursor">游标</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void 空游标表示目录末尾(string? cursor)
    {
        var page = new OutboxDeliveryTargetPage([new OutboxDeliveryTarget(1)], cursor);

        Assert.Null(page.NextCursor);
        Assert.Single(page.Targets);
    }

    /// <summary>
    /// 空页没有目标且位于目录末尾
    /// </summary>
    [Fact]
    public void 空页没有目标且位于目录末尾()
    {
        Assert.Empty(OutboxDeliveryTargetPage.Empty.Targets);
        Assert.Null(OutboxDeliveryTargetPage.Empty.NextCursor);
    }
}
