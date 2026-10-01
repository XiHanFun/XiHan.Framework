// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Features;
using XiHan.Framework.MultiTenancy.Tests.Fakes;

namespace XiHan.Framework.MultiTenancy.Tests;

/// <summary>
/// 租户功能检查器的平台态与跨租户边界测试
/// </summary>
/// <remarks>
/// 覆盖三类边界：平台态（无租户上下文或租户标识不大于 0）取不到租户级功能值，
/// 不同租户之间的功能值互不影响且随 <c>Change</c> 作用域切换，
/// 以及设定值不修剪、功能名会修剪的口径。
/// </remarks>
public class TenantFeatureCheckerBoundaryTests
{
    private const string FeatureName = "saas.export";
    private const string FeatureKey = "Feature:saas.export";

    /// <summary>
    /// 平台态取不到功能值，且不会去查设置存储
    /// </summary>
    /// <param name="tenantId">租户标识，null 表示无租户上下文</param>
    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task GetValueOrNullAsync_InPlatformState_ReturnsNullWithoutQueryingStore(long? tenantId)
    {
        var store = new FakeSettingStore();
        store.Seed(FeatureKey, "T", "0", "true");
        store.Seed(FeatureKey, "T", "-1", "true");
        store.Seed(FeatureKey, "T", string.Empty, "true");
        var checker = new TenantFeatureChecker(new FakeCurrentTenant { Id = tenantId }, store);

        Assert.Null(await checker.GetValueOrNullAsync(FeatureName));
        Assert.Empty(store.GetOrNullCalls);
    }

    /// <summary>
    /// 平台态下是否启用完全由调用方的默认值决定
    /// </summary>
    /// <param name="tenantId">租户标识，null 表示无租户上下文</param>
    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task IsEnabledAsync_InPlatformState_FollowsCallerDefaultValue(long? tenantId)
    {
        var checker = new TenantFeatureChecker(new FakeCurrentTenant { Id = tenantId }, new FakeSettingStore());

        Assert.False(await checker.IsEnabledAsync(FeatureName));
        Assert.False(await checker.IsEnabledAsync(FeatureName, defaultValue: false));
        Assert.True(await checker.IsEnabledAsync(FeatureName, defaultValue: true));
    }

    /// <summary>
    /// 租户甲设了功能而租户乙未设时判定互不串味
    /// </summary>
    [Fact]
    public async Task IsEnabledAsync_AcrossTenants_DoesNotLeak()
    {
        var store = new FakeSettingStore();
        store.Seed(FeatureKey, "T", "1001", "true");
        var currentTenant = new FakeCurrentTenant { Id = 1001 };
        var checker = new TenantFeatureChecker(currentTenant, store);

        Assert.True(await checker.IsEnabledAsync(FeatureName));

        currentTenant.Id = 1002;

        Assert.Null(await checker.GetValueOrNullAsync(FeatureName));
        Assert.False(await checker.IsEnabledAsync(FeatureName));
    }

    /// <summary>
    /// 两个租户各自显式设置时各取各的值
    /// </summary>
    [Fact]
    public async Task GetValueOrNullAsync_WithDifferentValuesPerTenant_ReturnsEachOwnValue()
    {
        var store = new FakeSettingStore();
        store.Seed(FeatureKey, "T", "1001", "true");
        store.Seed(FeatureKey, "T", "1002", "false");
        var currentTenant = new FakeCurrentTenant { Id = 1001 };
        var checker = new TenantFeatureChecker(currentTenant, store);

        Assert.Equal("true", await checker.GetValueOrNullAsync(FeatureName));

        currentTenant.Id = 1002;

        Assert.Equal("false", await checker.GetValueOrNullAsync(FeatureName));
    }

    /// <summary>
    /// Change 作用域内取值随上下文变，离开作用域后还原
    /// </summary>
    [Fact]
    public async Task IsEnabledAsync_InsideChangeScope_FollowsScopedTenantAndRestoresAfterwards()
    {
        var store = new FakeSettingStore();
        store.Seed(FeatureKey, "T", "1001", "true");
        var currentTenant = new CurrentTenant(new FakeCurrentTenantAccessor());
        var checker = new TenantFeatureChecker(currentTenant, store);

        Assert.False(await checker.IsEnabledAsync(FeatureName));

        using (currentTenant.Change(1001))
        {
            Assert.True(await checker.IsEnabledAsync(FeatureName));

            using (currentTenant.Change(1002))
            {
                Assert.False(await checker.IsEnabledAsync(FeatureName));
            }

            Assert.True(await checker.IsEnabledAsync(FeatureName));
        }

        Assert.False(await checker.IsEnabledAsync(FeatureName));
        Assert.Equal(["1001", "1002", "1001"], store.GetOrNullCalls.Select(call => call.ProviderKey));
    }

    /// <summary>
    /// 在 Change 作用域内切到平台态后取不到功能值
    /// </summary>
    [Fact]
    public async Task GetValueOrNullAsync_WhenScopeChangesToPlatform_ReturnsNull()
    {
        var store = new FakeSettingStore();
        store.Seed(FeatureKey, "T", "1001", "true");
        var currentTenant = new CurrentTenant(new FakeCurrentTenantAccessor());
        var checker = new TenantFeatureChecker(currentTenant, store);

        using (currentTenant.Change(1001))
        {
            Assert.Equal("true", await checker.GetValueOrNullAsync(FeatureName));

            using (currentTenant.Change(null))
            {
                Assert.Null(await checker.GetValueOrNullAsync(FeatureName));
            }

            Assert.Equal("true", await checker.GetValueOrNullAsync(FeatureName));
        }
    }

    /// <summary>
    /// 功能名两侧的空白会被修剪后再查询
    /// </summary>
    [Fact]
    public async Task GetValueOrNullAsync_WithPaddedFeatureName_TrimsNameBeforeQuery()
    {
        var store = new FakeSettingStore();
        store.Seed(FeatureKey, "T", "1001", "true");
        var checker = new TenantFeatureChecker(new FakeCurrentTenant { Id = 1001 }, store);

        Assert.Equal("true", await checker.GetValueOrNullAsync("  saas.export  "));
        Assert.Equal(FeatureKey, Assert.Single(store.GetOrNullCalls).Name);
    }

    /// <summary>
    /// 设定值不修剪：带空白的真值判为未启用，不会被默认值放行
    /// </summary>
    /// <remarks>
    /// 方向是保守拒绝，设定值需精确写成 true／false 等取值。
    /// </remarks>
    [Fact]
    public async Task IsEnabledAsync_WithPaddedTruthValue_IsNotEnabledEvenWithDefaultTrue()
    {
        var store = new FakeSettingStore();
        store.Seed(FeatureKey, "T", "1001", "  true  ");
        var checker = new TenantFeatureChecker(new FakeCurrentTenant { Id = 1001 }, store);

        Assert.Equal("  true  ", await checker.GetValueOrNullAsync(FeatureName));
        Assert.False(await checker.IsEnabledAsync(FeatureName));
        Assert.False(await checker.IsEnabledAsync(FeatureName, defaultValue: true));
    }

    /// <summary>
    /// 全空白的设定值视同未设置，按调用方默认值决定
    /// </summary>
    [Fact]
    public async Task IsEnabledAsync_WithWhitespaceOnlyValue_FollowsCallerDefaultValue()
    {
        var store = new FakeSettingStore();
        store.Seed(FeatureKey, "T", "1001", "   ");
        var checker = new TenantFeatureChecker(new FakeCurrentTenant { Id = 1001 }, store);

        Assert.False(await checker.IsEnabledAsync(FeatureName));
        Assert.True(await checker.IsEnabledAsync(FeatureName, defaultValue: true));
    }

    /// <summary>
    /// 显式关闭的值不受默认值影响
    /// </summary>
    [Fact]
    public async Task IsEnabledAsync_WithExplicitOff_IgnoresDefaultValue()
    {
        var store = new FakeSettingStore();
        store.Seed(FeatureKey, "T", "1001", "false");
        var checker = new TenantFeatureChecker(new FakeCurrentTenant { Id = 1001 }, store);

        Assert.False(await checker.IsEnabledAsync(FeatureName, defaultValue: true));
    }
}
