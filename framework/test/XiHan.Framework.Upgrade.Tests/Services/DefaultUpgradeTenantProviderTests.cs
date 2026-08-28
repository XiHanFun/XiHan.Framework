// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Services;
using XiHan.Framework.Upgrade.Tests.Fakes;

namespace XiHan.Framework.Upgrade.Tests.Services;

/// <summary>
/// 默认升级租户提供者测试
/// </summary>
/// <remarks>
/// 默认实现只返回「当前租户」这一条，多租户批量升级需要应用层自行替换实现。
/// 这里锁住「永远返回且只返回一条」的契约，避免多租户开关打开后误升级到零个租户。
/// </remarks>
public class DefaultUpgradeTenantProviderTests
{
    /// <summary>
    /// 没有当前租户时返回一条空租户（宿主）记录
    /// </summary>
    [Fact]
    public void GetTenants_WhenNoCurrentTenant_ReturnsSingleHostTenant()
    {
        var provider = new DefaultUpgradeTenantProvider();

        var tenant = Assert.Single(provider.GetTenants());

        Assert.Null(tenant.TenantId);
        Assert.Null(tenant.Name);
    }

    /// <summary>
    /// 存在当前租户时原样返回该租户的标识与名称
    /// </summary>
    [Fact]
    public void GetTenants_WhenCurrentTenantAvailable_ReturnsThatTenant()
    {
        var provider = new DefaultUpgradeTenantProvider(new FakeCurrentTenant(9, "租户九"));

        var tenant = Assert.Single(provider.GetTenants());

        Assert.NotNull(tenant.TenantId);
        Assert.Equal(9L, tenant.TenantId!.Value);
        Assert.Equal("租户九", tenant.Name);
    }

    /// <summary>
    /// 当前租户被切换后再取，返回的是切换后的租户
    /// </summary>
    [Fact]
    public void GetTenants_AfterTenantChanged_ReflectsCurrentTenant()
    {
        var currentTenant = new FakeCurrentTenant(1, "一号");
        var provider = new DefaultUpgradeTenantProvider(currentTenant);

        using (currentTenant.Change(2, "二号"))
        {
            var inner = Assert.Single(provider.GetTenants());
            Assert.NotNull(inner.TenantId);
            Assert.Equal(2L, inner.TenantId!.Value);
            Assert.Equal("二号", inner.Name);
        }

        var restored = Assert.Single(provider.GetTenants());
        Assert.NotNull(restored.TenantId);
        Assert.Equal(1L, restored.TenantId!.Value);
    }
}
