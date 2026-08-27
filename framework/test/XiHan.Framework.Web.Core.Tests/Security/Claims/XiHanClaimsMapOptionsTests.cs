// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Web.Core.Security.Claims;

namespace XiHan.Framework.Web.Core.Tests.Security.Claims;

/// <summary>
/// 声明映射选项测试
/// </summary>
/// <remarks>
/// 这张表决定了 OIDC 标准声明落到框架内部用哪个声明类型，
/// 一旦 sub/role 的目标类型漂移，鉴权与审计会整体错位，属于安全契约，逐条锁死。
/// 注意 name 与 given_name 是交叉的：name 映射到"用户名"，given_name 才映射到"名字"，容易写反。
/// </remarks>
public class XiHanClaimsMapOptionsTests
{
    /// <summary>
    /// 默认只映射六个 OIDC 标准声明，不多也不少
    /// </summary>
    [Fact]
    public void Maps_ContainsExactlySixStandardKeys()
    {
        var maps = new XiHanClaimsMapOptions().Maps;

        Assert.Equal(6, maps.Count);
        Assert.Equal(
            new[] { "email", "family_name", "given_name", "name", "role", "sub" },
            maps.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// 六个标准声明分别映射到约定的框架声明类型
    /// </summary>
    [Fact]
    public void Maps_ProjectStandardClaimsToFrameworkClaimTypes()
    {
        var maps = new XiHanClaimsMapOptions().Maps;

        Assert.Equal(ClaimTypes.NameIdentifier, maps["sub"]());
        Assert.Equal(ClaimTypes.Role, maps["role"]());
        Assert.Equal(ClaimTypes.Email, maps["email"]());
        Assert.Equal(ClaimTypes.Name, maps["name"]());
        Assert.Equal(ClaimTypes.Surname, maps["family_name"]());
        Assert.Equal(ClaimTypes.GivenName, maps["given_name"]());
    }

    /// <summary>
    /// 映射目标与框架声明类型常量保持同源，不是各写各的字面量
    /// </summary>
    [Fact]
    public void Maps_AreAlignedWithXiHanClaimTypes()
    {
        var maps = new XiHanClaimsMapOptions().Maps;

        Assert.Equal(XiHanClaimTypes.UserId, maps["sub"]());
        Assert.Equal(XiHanClaimTypes.Role, maps["role"]());
        Assert.Equal(XiHanClaimTypes.Email, maps["email"]());
        Assert.Equal(XiHanClaimTypes.UserName, maps["name"]());
        Assert.Equal(XiHanClaimTypes.SurName, maps["family_name"]());
        Assert.Equal(XiHanClaimTypes.Name, maps["given_name"]());
    }

    /// <summary>
    /// 键区分大小写，大写变体不会被误映射
    /// </summary>
    [Fact]
    public void Maps_KeyLookupIsCaseSensitive()
    {
        var maps = new XiHanClaimsMapOptions().Maps;

        Assert.True(maps.ContainsKey("sub"));
        Assert.False(maps.ContainsKey("Sub"));
        Assert.False(maps.ContainsKey("SUB"));
    }

    /// <summary>
    /// 映射值是委托而非字符串快照，宿主启动后再改框架声明类型仍然生效
    /// </summary>
    [Fact]
    public void Maps_AreResolvedLazilyOnEachCall()
    {
        var options = new XiHanClaimsMapOptions();
        var target = "first";
        options.Maps["custom"] = () => target;

        Assert.Equal("first", options.Maps["custom"]());

        target = "second";

        Assert.Equal("second", options.Maps["custom"]());
    }

    /// <summary>
    /// 映射表随实例创建，改一个实例不会污染另一个
    /// </summary>
    [Fact]
    public void Maps_AreIsolatedPerInstance()
    {
        var first = new XiHanClaimsMapOptions();
        var second = new XiHanClaimsMapOptions();

        first.Maps["phone_number"] = () => XiHanClaimTypes.PhoneNumber;

        Assert.NotSame(first.Maps, second.Maps);
        Assert.True(first.Maps.ContainsKey("phone_number"));
        Assert.False(second.Maps.ContainsKey("phone_number"));
        Assert.Equal(6, second.Maps.Count);
    }
}
