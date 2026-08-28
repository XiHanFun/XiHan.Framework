// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.MultiTenancy.ConfigurationStore;

namespace XiHan.Framework.MultiTenancy.Tests.ConfigurationStore;

/// <summary>
/// 曦寒默认租户存储选项的测试
/// </summary>
/// <remarks>
/// 这个选项类只有一个集合属性，但它是整条「配置 → 租户列表」链路的落点，
/// 所以除了默认值，还用真实的 IConfiguration 走一遍绑定，把配置节名称、字段名、类型转换一起验证掉。
/// 配置节名称写错、字段类型不兼容都不会报错，只会静默绑出空列表，光靠单元测默认值抓不到。
/// </remarks>
public class XiHanDefaultTenantStoreOptionsTests
{
    /// <summary>
    /// 配置节名称不漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:MultiTenancy:DefaultStore", XiHanDefaultTenantStoreOptions.SectionName);
    }

    /// <summary>
    /// 默认租户集合是空数组而不是 null
    /// </summary>
    /// <remarks>
    /// 租户存储直接对这个数组取 Length，默认给 null 会让「没有配置任何租户」这条最常见路径变成空引用。
    /// </remarks>
    [Fact]
    public void Constructor_InitializesEmptyTenantArray()
    {
        var options = new XiHanDefaultTenantStoreOptions();

        Assert.NotNull(options.Tenants);
        Assert.Empty(options.Tenants);
    }

    /// <summary>
    /// 租户集合可被整体替换
    /// </summary>
    [Fact]
    public void Tenants_IsWritable()
    {
        var options = new XiHanDefaultTenantStoreOptions
        {
            Tenants = [new TenantConfiguration(1L, "acme", "ACME")]
        };

        var tenant = Assert.Single(options.Tenants);
        Assert.Equal(1L, tenant.Id);
        Assert.Equal("acme", tenant.Name);
    }

    /// <summary>
    /// 不同实例之间不共享租户数组
    /// </summary>
    [Fact]
    public void Instances_DoNotShareTenantArray()
    {
        var first = new XiHanDefaultTenantStoreOptions
        {
            Tenants = [new TenantConfiguration(1L, "acme", "ACME")]
        };
        var second = new XiHanDefaultTenantStoreOptions();

        Assert.NotSame(first.Tenants, second.Tenants);
        Assert.Empty(second.Tenants);
    }

    /// <summary>
    /// 从真实配置绑定出完整的租户列表
    /// </summary>
    /// <remarks>
    /// 同时覆盖三件事：配置节路径正确、数值与布尔字段能转换、未配置 IsActive 时保留构造函数给的激活默认值。
    /// </remarks>
    [Fact]
    public void Bind_FromConfiguration_ProducesTenantList()
    {
        var editionId = Guid.NewGuid();
        var options = BindFromConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:MultiTenancy:DefaultStore:Tenants:0:Id"] = "1",
            ["XiHan:MultiTenancy:DefaultStore:Tenants:0:Name"] = "acme",
            ["XiHan:MultiTenancy:DefaultStore:Tenants:0:NormalizedName"] = "ACME",
            ["XiHan:MultiTenancy:DefaultStore:Tenants:0:IsActive"] = "false",
            ["XiHan:MultiTenancy:DefaultStore:Tenants:0:EditionId"] = editionId.ToString(),
            ["XiHan:MultiTenancy:DefaultStore:Tenants:1:Id"] = "2",
            ["XiHan:MultiTenancy:DefaultStore:Tenants:1:Name"] = "globex"
        });

        Assert.Equal(2, options.Tenants.Length);

        Assert.Equal(1L, options.Tenants[0].Id);
        Assert.Equal("acme", options.Tenants[0].Name);
        Assert.Equal("ACME", options.Tenants[0].NormalizedName);
        Assert.False(options.Tenants[0].IsActive);
        Assert.Equal<Guid?>(editionId, options.Tenants[0].EditionId);

        Assert.Equal(2L, options.Tenants[1].Id);
        Assert.Equal("globex", options.Tenants[1].Name);
        Assert.True(options.Tenants[1].IsActive);
    }

    /// <summary>
    /// 配置里没有对应节时绑定出空租户列表
    /// </summary>
    [Fact]
    public void Bind_WhenSectionMissing_KeepsEmptyTenantList()
    {
        var options = BindFromConfiguration(new Dictionary<string, string?>
        {
            ["Unrelated:Key"] = "value"
        });

        Assert.NotNull(options.Tenants);
        Assert.Empty(options.Tenants);
    }

    /// <summary>
    /// 租户的连接字符串子节能被绑定成键值集合
    /// </summary>
    /// <remarks>
    /// 连接字符串是 Dictionary 的派生类型，绑定器要能识别成字典而不是普通对象，
    /// 否则整节配置会被静默丢掉，租户拿到的连接串为空——这类故障只有在切库时才会暴露。
    /// </remarks>
    [Fact]
    public void Bind_TenantConnectionStrings_ProducesKeyedEntries()
    {
        var options = BindFromConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:MultiTenancy:DefaultStore:Tenants:0:Id"] = "1",
            ["XiHan:MultiTenancy:DefaultStore:Tenants:0:Name"] = "acme",
            ["XiHan:MultiTenancy:DefaultStore:Tenants:0:ConnectionStrings:Default"] = "Server=.;Database=Acme",
            ["XiHan:MultiTenancy:DefaultStore:Tenants:0:ConnectionStrings:Report"] = "Server=.;Database=AcmeReport"
        });

        var tenant = Assert.Single(options.Tenants);
        Assert.NotNull(tenant.ConnectionStrings);
        Assert.Equal("Server=.;Database=Acme", tenant.ConnectionStrings.Default);
        Assert.Equal("Server=.;Database=AcmeReport", tenant.ConnectionStrings["Report"]);
    }

    /// <summary>
    /// 用内存配置绑定出默认租户存储选项
    /// </summary>
    /// <param name="values">配置键值对</param>
    /// <returns>默认租户存储选项</returns>
    private static XiHanDefaultTenantStoreOptions BindFromConfiguration(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.Configure<XiHanDefaultTenantStoreOptions>(configuration.GetSection(XiHanDefaultTenantStoreOptions.SectionName));

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<XiHanDefaultTenantStoreOptions>>().Value;
    }
}
