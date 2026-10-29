// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 多租户契约层的单元测试。
/// </summary>
/// <remarks>
/// 租户 ID 文法、<see cref="TenantInfo"/> 默认值与 <see cref="MultiTenancyOptions"/> 的配置解析。
/// </remarks>
public sealed class TenantContractTests
{
    /// <summary>
    /// 用内存键值对构建配置。
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Theory]
    [InlineData("acme", true)]
    [InlineData("Acme-01", true)]
    [InlineData("t.1_x", true)]
    [InlineData("", false)]
    [InlineData("-lead", false)]
    [InlineData("has space", false)]
    [InlineData("中文", false)]
    public void TenantIdentifier_ValidatesShape(string value, bool valid)
    {
        Assert.Equal(valid, TenantIdentifier.IsValid(value));
        if (valid)
        {
            Assert.Equal(value, new TenantInfo { Id = value }.Id);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => new TenantInfo { Id = value });
        }
    }

    [Fact]
    public void TenantIdentifier_RejectsOverlongAndNull()
    {
        Assert.True(TenantIdentifier.IsValid(new string('a', TenantIdentifier.MaxLength)));
        Assert.False(TenantIdentifier.IsValid(new string('a', TenantIdentifier.MaxLength + 1)));
        Assert.False(TenantIdentifier.IsValid(null));
        Assert.Throws<ArgumentNullException>(static () => TenantIdentifier.Validate(null!));
    }

    [Fact]
    public void TenantInfo_DefaultsToEnabled_WithNoProperties()
    {
        var tenant = new TenantInfo { Id = "acme" };

        Assert.True(tenant.IsEnabled);
        Assert.Null(tenant.DisplayName);
        Assert.Empty(tenant.Properties);
        Assert.Throws<ArgumentNullException>(static () => new TenantInfo { Id = "acme", Properties = null! });
    }

    [Fact]
    public void Options_FromConfiguration_ParsesAndRejectsInvalid()
    {
        var options = MultiTenancyOptions.FromConfiguration(Build(
            ("XiHan:MultiTenancy:DefaultTenant", "acme"),
            ("XiHan:MultiTenancy:RequireTenant", "true"),
            ("XiHan:MultiTenancy:ValidateAgainstStore", "false"),
            ("XiHan:MultiTenancy:TenantClaim", "tid")));

        Assert.Equal("acme", options.DefaultTenant);
        Assert.True(options.RequireTenant);
        Assert.False(options.ValidateAgainstStore);
        Assert.Equal("tid", options.TenantClaim);

        var defaults = MultiTenancyOptions.FromConfiguration(Build());
        Assert.Null(defaults.DefaultTenant);
        Assert.False(defaults.RequireTenant);
        Assert.True(defaults.ValidateAgainstStore);
        Assert.Equal("tenant", defaults.TenantClaim);
        Assert.Null(MultiTenancyOptions.FromConfiguration(Build(("XiHan:MultiTenancy:DefaultTenant", ""))).DefaultTenant);

        Assert.Throws<ArgumentException>(() => MultiTenancyOptions.FromConfiguration(Build(("XiHan:MultiTenancy:RequireTenant", "yes"))));
        Assert.Throws<ArgumentException>(() => MultiTenancyOptions.FromConfiguration(Build(("XiHan:MultiTenancy:DefaultTenant", "bad id"))));
        Assert.Throws<ArgumentException>(() => MultiTenancyOptions.FromConfiguration(Build(("XiHan:MultiTenancy:TenantClaim", " "))));
    }
}
