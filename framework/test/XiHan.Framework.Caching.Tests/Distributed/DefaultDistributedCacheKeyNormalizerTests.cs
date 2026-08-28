// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Tests.Fakes;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Caching.Tests.Distributed;

/// <summary>
/// 默认分布式缓存键规范化器测试
/// </summary>
/// <remarks>
/// 规范化键是跨进程共享的协议，一旦分段格式漂移，升级期间新旧实例会读到彼此的数据，
/// 所以这里把「租户段:缓存名段:业务键」三段格式与租户段的取值规则整体锁死。
/// </remarks>
public class DefaultDistributedCacheKeyNormalizerTests
{
    /// <summary>
    /// 容器里没有租户访问器时，租户段取 0
    /// </summary>
    [Fact]
    public void NormalizeKey_WithoutTenantAccessor_UsesZeroTenantSegment()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var normalizer = new DefaultDistributedCacheKeyNormalizer(provider);

        var key = normalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs("k1", "orders", false));

        Assert.Equal("0:orders:k1", key);
    }

    /// <summary>
    /// 有租户访问器但当前无租户时，租户段仍取 0
    /// </summary>
    [Fact]
    public void NormalizeKey_WhenNoCurrentTenant_UsesZeroTenantSegment()
    {
        using var provider = BuildProvider(null);
        var normalizer = new DefaultDistributedCacheKeyNormalizer(provider);

        var key = normalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs("k1", "orders", false));

        Assert.Equal("0:orders:k1", key);
    }

    /// <summary>
    /// 有当前租户时，租户段取租户标识
    /// </summary>
    /// <remarks>
    /// 用 TenantId 而不是租户名做分段：改名不应让缓存整体失效，也不应让两个租户撞到同一段。
    /// </remarks>
    [Fact]
    public void NormalizeKey_WithCurrentTenant_UsesTenantId()
    {
        using var provider = BuildProvider(new BasicTenantInfo(1024, "任意名称"));
        var normalizer = new DefaultDistributedCacheKeyNormalizer(provider);

        var key = normalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs("k1", "orders", false));

        Assert.Equal("1024:orders:k1", key);
    }

    /// <summary>
    /// 租户信息里没有标识时，租户段回落为 0
    /// </summary>
    [Fact]
    public void NormalizeKey_WhenTenantIdIsNull_UsesZeroTenantSegment()
    {
        using var provider = BuildProvider(new BasicTenantInfo(null, "宿主"));
        var normalizer = new DefaultDistributedCacheKeyNormalizer(provider);

        var key = normalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs("k1", "orders", false));

        Assert.Equal("0:orders:k1", key);
    }

    /// <summary>
    /// 声明忽略多租户时，即便存在当前租户也不带入租户段
    /// </summary>
    [Fact]
    public void NormalizeKey_WhenIgnoreMultiTenancy_DropsTenantSegment()
    {
        using var provider = BuildProvider(new BasicTenantInfo(1024));
        var normalizer = new DefaultDistributedCacheKeyNormalizer(provider);

        var key = normalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs("k1", "orders", true));

        Assert.Equal("0:orders:k1", key);
    }

    /// <summary>
    /// 空业务键规范化后得到该缓存的键前缀
    /// </summary>
    /// <remarks>
    /// 按模式查询要靠这个前缀把规范化键还原成业务键，前缀必须以分隔符收尾。
    /// </remarks>
    [Fact]
    public void NormalizeKey_WithEmptyKey_ProducesPrefixEndingWithSeparator()
    {
        using var provider = BuildProvider(new BasicTenantInfo(7));
        var normalizer = new DefaultDistributedCacheKeyNormalizer(provider);

        var prefix = normalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs(string.Empty, "orders", false));

        Assert.Equal("7:orders:", prefix);
        Assert.EndsWith(":", prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// 不同租户下同一业务键规范化后互不相同
    /// </summary>
    [Fact]
    public void NormalizeKey_ForDifferentTenants_ProducesDifferentKeys()
    {
        using var firstProvider = BuildProvider(new BasicTenantInfo(1));
        using var secondProvider = BuildProvider(new BasicTenantInfo(2));

        var first = new DefaultDistributedCacheKeyNormalizer(firstProvider)
            .NormalizeKey(new DistributedCacheKeyNormalizeArgs("k1", "orders", false));
        var second = new DefaultDistributedCacheKeyNormalizer(secondProvider)
            .NormalizeKey(new DistributedCacheKeyNormalizeArgs("k1", "orders", false));

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 规范化参数原样保留传入的三项内容
    /// </summary>
    [Fact]
    public void NormalizeArgs_KeepsProvidedValues()
    {
        var args = new DistributedCacheKeyNormalizeArgs("k1", "orders", true);

        Assert.Equal("k1", args.Key);
        Assert.Equal("orders", args.CacheName);
        Assert.True(args.IgnoreMultiTenancy);
    }

    /// <summary>
    /// 构建带指定当前租户的容器
    /// </summary>
    /// <param name="tenant">当前租户</param>
    /// <returns>服务提供者</returns>
    private static ServiceProvider BuildProvider(BasicTenantInfo? tenant)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentTenantAccessor>(new FakeCurrentTenantAccessor { Current = tenant });

        return services.BuildServiceProvider();
    }
}
