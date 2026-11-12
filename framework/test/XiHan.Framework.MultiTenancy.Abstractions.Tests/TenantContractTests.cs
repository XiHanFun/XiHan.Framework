// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 多租户契约层的单元测试
/// </summary>
/// <remarks>
/// 租户 ID 文法、<see cref="TenantInfo"/> 默认值与 <see cref="MultiTenancyOptions"/> 的配置解析
/// </remarks>
public sealed class TenantContractTests
{
    /// <summary>
    /// 用内存键值对构建配置
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Theory]
    [InlineData("acme", true)]
    [InlineData("acme-01", true)]
    [InlineData("t.1_x", true)]
    // 只有小写：大写的 ID 在不区分大小写的排序规则、文件系统与主机名里会与小写的同一个 ID 撞在一起（ADR-0196）
    [InlineData("Acme-01", false)]
    [InlineData("ACME", false)]
    // 整串匹配：结尾的换行不被 $ 放过
    [InlineData("acme\n", false)]
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

    [Theory]
    [InlineData("acme")]
    [InlineData("Acme")]
    [InlineData("acme.cn")]
    [InlineData("t_42")]
    [InlineData("acme-01")]
    [InlineData("Acme-01")]
    [InlineData("acme\n")]
    [InlineData("-lead")]
    [InlineData(".lead")]
    [InlineData("has space")]
    [InlineData("中文")]
    [InlineData("a:b")]
    public void TenantIdentifier_MatchesResourceNameTenantSegment(string value)
    {
        // RFC-0007：租户 ID 与 xrn 资源名的租户段同一文法，合法的租户 ID 拼进资源名都能原样解析回来，不合法的都被拒绝
        var parsed = ResourceName.TryParse($"xrn:cn:shop:{value}:order/1", out var name);

        Assert.Equal(TenantIdentifier.IsValid(value), parsed);
        if (parsed)
        {
            Assert.Equal(value, name.Tenant);
        }

        var longest = new string('a', TenantIdentifier.MaxLength);
        Assert.True(ResourceName.TryParse($"xrn:cn:shop:{longest}:order/1", out _));
        Assert.False(ResourceName.TryParse($"xrn:cn:shop:{longest}a:order/1", out _));
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
            ("XiHan:MultiTenancy:TenantClaim", "tid"),
            ("XiHan:MultiTenancy:ShardCacheSize", "256")));

        Assert.Equal("acme", options.DefaultTenant);
        Assert.True(options.RequireTenant);
        Assert.False(options.ValidateAgainstStore);
        Assert.Equal("tid", options.TenantClaim);
        Assert.Equal(256, options.ShardCacheSize);
        Assert.Equal(10_000, MultiTenancyOptions.FromConfiguration(Build()).ShardCacheSize);
        foreach (var invalid in new[] { "0", "-1", "many", "1.5" })
        {
            Assert.Throws<ArgumentException>(() => MultiTenancyOptions.FromConfiguration(Build(("XiHan:MultiTenancy:ShardCacheSize", invalid))));
        }

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
