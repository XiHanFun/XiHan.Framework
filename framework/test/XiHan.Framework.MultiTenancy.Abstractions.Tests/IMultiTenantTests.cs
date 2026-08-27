// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 多租户实体契约的测试
/// </summary>
/// <remarks>
/// <see cref="IMultiTenant"/> 只有一个只读属性，它的全部价值在于「null 代表宿主/公共数据，非 null 代表归属某个租户」这条口径。
/// 这里除了锁契约形状，还用一次真实的过滤把口径本身跑一遍，防止后来者把 0 或负数当成宿主。
/// </remarks>
public class IMultiTenantTests
{
    /// <summary>
    /// 唯一标识为 null 表示宿主数据
    /// </summary>
    [Fact]
    public void TenantId_WhenNull_MeansHostOwnedData()
    {
        IMultiTenant entity = new FakeMultiTenantEntity(null);

        Assert.Null(entity.TenantId);
    }

    /// <summary>
    /// 唯一标识非 null 时原样表示所属租户，边界值不被归一化
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void TenantId_WhenAssigned_KeepsValueUnchanged(long tenantId)
    {
        IMultiTenant entity = new FakeMultiTenantEntity(tenantId);

        Assert.Equal<long?>(tenantId, entity.TenantId);
    }

    /// <summary>
    /// 按唯一标识过滤时宿主数据与租户数据互不串台
    /// </summary>
    /// <remarks>
    /// 0 是合法租户唯一标识，必须落在租户侧而不是宿主侧，这是租户隔离过滤最容易写错的地方。
    /// </remarks>
    [Fact]
    public void TenantId_Filtering_SeparatesHostRowsFromTenantRows()
    {
        var rows = new List<IMultiTenant>
        {
            new FakeMultiTenantEntity(null),
            new FakeMultiTenantEntity(0L),
            new FakeMultiTenantEntity(1L),
            new FakeMultiTenantEntity(1L),
            new FakeMultiTenantEntity(2L)
        };

        var hostRows = rows.Where(row => row.TenantId is null).ToList();
        var tenantZeroRows = rows.Where(row => row.TenantId == 0L).ToList();
        var tenantOneRows = rows.Where(row => row.TenantId == 1L).ToList();

        Assert.Single(hostRows);
        Assert.Single(tenantZeroRows);
        Assert.Equal(2, tenantOneRows.Count);
    }

    /// <summary>
    /// 契约要求唯一标识为只读的可空长整型
    /// </summary>
    /// <remarks>
    /// 只读是刻意的：租户归属由框架在保存时写入，实体不应对外开放 setter。
    /// </remarks>
    [Fact]
    public void Contract_TenantId_IsReadOnlyNullableLong()
    {
        var property = typeof(IMultiTenant).GetProperty(nameof(IMultiTenant.TenantId));

        Assert.NotNull(property);
        Assert.Equal(typeof(long?), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
    }

    /// <summary>
    /// 契约只包含唯一标识这一个成员，不允许悄悄膨胀
    /// </summary>
    [Fact]
    public void Contract_DeclaresOnlyTenantId()
    {
        var members = typeof(IMultiTenant).GetMembers();

        Assert.Contains(members, member => member.Name == nameof(IMultiTenant.TenantId));
        Assert.DoesNotContain(members, member => member.Name.StartsWith("set_", StringComparison.Ordinal));
    }
}
